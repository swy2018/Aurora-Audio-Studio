"""Bundle the build machine's native tools and their non-system dylibs."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

app = Path(sys.argv[1])
bin_dir = app / 'Contents/MacOS/Runtime/bin'
lib_dir = bin_dir / 'lib'
licenses = app / 'Contents/MacOS/Licenses/Tools'
for p in (bin_dir, lib_dir, licenses): p.mkdir(parents=True, exist_ok=True)
queue = [(Path(shutil.which(n) or '/opt/homebrew/bin/'+n).resolve(), bin_dir/n) for n in ('uv','ffmpeg','ffprobe','sox','git')]
# /usr/bin/git is an Xcode command-line-tools launcher, not a redistributable Git.
git = queue[-1][0]
if git == Path('/usr/bin/git'):
    raise RuntimeError('Build machine needs a redistributable Git (for example Homebrew git). Do not ship the Xcode launcher.')
git_core = Path(subprocess.check_output([str(git), '--exec-path'], text=True).strip())
(bin_dir/'git-core').mkdir(exist_ok=True)
for name in ('git-remote-http', 'git-remote-https'):
    queue.append(((git_core/name).resolve(), bin_dir/'git-core'/name))
templates = git.parent.parent/'share/git-core/templates'
shutil.copytree(templates, app/'Contents/MacOS/Runtime/share/git-core/templates', dirs_exist_ok=True)
certificates = app/'Contents/MacOS/Runtime/share/certs'
certificates.mkdir(parents=True, exist_ok=True)
shutil.copy2('/etc/ssl/cert.pem', certificates/'ca-bundle.crt')
# Homebrew's .NET runtime can itself depend on Homebrew libraries (e.g. Brotli).
# Include those app dylibs too, excluding their own LC_ID_DYLIB from dependencies.
def dependency_names(path):
    own_ids = subprocess.check_output(['otool', '-D', str(path)], text=True).splitlines()[1:]
    return [name for line in subprocess.check_output(['otool', '-L', str(path)], text=True).splitlines()
            if line.startswith('\t') and (name := line.strip().split(' (', 1)[0]) not in own_ids]

for native in (app/'Contents/MacOS').glob('*.dylib'):
    if any(name.startswith('/opt/homebrew/') for name in dependency_names(native)):
        queue.append((native.resolve(), native))
copies = {}; links = {}; formulas = set()
while queue:
    source, target = queue.pop(0)
    if source in copies:
        # Git's HTTPS helper is commonly a symlink to its HTTP helper. Keep both names.
        if target != copies[source] and not target.exists():
            target.symlink_to(os.path.relpath(copies[source], target.parent))
        continue
    if not source.is_file(): raise FileNotFoundError(source)
    copies[source] = target
    if source.resolve() != target.resolve(): shutil.copy2(source, target)
    target.chmod(target.stat().st_mode | 0o200)
    dependencies = []
    for name in dependency_names(source):
        if name.startswith(('/usr/lib/','/System/Library/')): continue
        if name.startswith('@rpath/'):
            commands = subprocess.check_output(['otool', '-l', str(source)], text=True)
            rpaths = re.findall(r'cmd LC_RPATH\s+cmdsize \d+\s+path (.*?) \(offset', commands)
            candidates = [Path(base.replace('@loader_path', str(source.parent))) / name.removeprefix('@rpath/') for base in rpaths]
            actual = next((candidate for candidate in candidates if candidate.is_file()), Path(name))
        else:
            actual = Path(name.replace('@loader_path',str(source.parent)))
        if not actual.is_absolute() or not actual.exists(): raise RuntimeError('Unresolved dependency: '+name+' in '+str(source))
        actual = actual.resolve()
        if actual == source: continue
        destination = lib_dir/actual.name
        if any(t == destination and s != actual for s,t in copies.items()): raise RuntimeError('Conflicting dylib '+actual.name)
        dependencies.append((name,destination))
        queue.append((actual,destination))
    links[source] = dependencies
    parts = source.parts
    if 'Cellar' in parts:
        index = parts.index('Cellar'); formula = Path(*parts[:index+3]); formulas.add(formula)
for source,target in copies.items():
    for original,dependency in links[source]:
        relative = '@loader_path/' + os.path.relpath(dependency, target.parent)
        subprocess.run(['install_name_tool','-change',original,relative,str(target)],check=True)
    if target.suffix == '.dylib': subprocess.run(['install_name_tool','-id','@loader_path/'+target.name,str(target)],check=True)
    subprocess.run(['codesign','--force','--sign','-',str(target)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    if any(name.startswith('/') and not name.startswith(('/usr/lib/', '/System/Library/')) for name in dependency_names(target)):
        raise RuntimeError('External non-system dependency: '+str(target))
for formula in sorted(formulas):
    name = formula.parent.name
    folder = licenses/name; folder.mkdir(exist_ok=True)
    for item in formula.iterdir():
        if item.is_file() and item.name.upper().startswith(('LICENSE','LICENCE','COPYING','NOTICE','COPYRIGHT')): shutil.copy2(item, folder/item.name)
    if (formula/'.brew').exists():
        for item in (formula/'.brew').glob('*.rb'): shutil.copy2(item,folder/item.name)
manifest = {str(s):str(t.relative_to(app)) for s,t in copies.items()}
(licenses/'manifest.json').write_text(json.dumps(manifest,indent=2))
(licenses/'README.txt').write_text('Bundled native tools: uv, Git (including HTTPS transport), FFmpeg/ffprobe, SoX and their runtime libraries.\nExact Homebrew build recipes, source URLs and installed license notices are included by formula directory.\nUpstream sources: https://github.com/astral-sh/uv ; https://git-scm.com ; https://ffmpeg.org ; https://sourceforge.net/projects/sox/\nThis bundle was built for Apple Silicon on macOS 26+.\n')
isolated = os.environ.copy()
isolated['PATH'] = str(bin_dir) + ':/usr/bin:/bin'
for key in ('DYLD_LIBRARY_PATH', 'DYLD_FALLBACK_LIBRARY_PATH'):
    isolated.pop(key, None)
for name in ('uv', 'git', 'ffmpeg', 'ffprobe', 'sox'):
    subprocess.run([str(bin_dir/name), '-version' if name in ('ffmpeg', 'ffprobe') else '--version'],
                   env=isolated, check=True, timeout=30, stdout=subprocess.DEVNULL)
print(f'Bundled {len(copies)} native tools and libraries.')
