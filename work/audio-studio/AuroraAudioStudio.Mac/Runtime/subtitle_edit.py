"""Official macOS Subtitle Edit component; staged, verified, and recoverable."""
import json
from pathlib import Path
import re
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

from download_models import digest, emit

REPOSITORY = "https://api.github.com/repos/SubtitleEdit/subtitleedit/releases"
ASSET = "SubtitleEdit-macOS-ARM64.dmg"
APP = "Subtitle Edit.app"


def require_closed(base):
    current = (base / "current" / APP).resolve()
    result = subprocess.run(["/bin/ps", "-axo", "comm="], capture_output=True, text=True, check=True, timeout=10)
    if any(Path(line.strip()).resolve().is_relative_to(current) for line in result.stdout.splitlines() if line.strip()):
        raise RuntimeError("请先保存字幕并退出 Subtitle Edit，再执行安装、更新、回退或卸载。")


def release(tag=None):
    endpoint = REPOSITORY + ("/tags/" + urllib.parse.quote(tag, safe="") if tag else "/latest")
    with urllib.request.urlopen(urllib.request.Request(endpoint, headers={"User-Agent": "Aurora-Audio-Studio/1.9.0"}), timeout=45) as response:
        data = json.load(response)
    if data.get("draft") or data.get("prerelease"):
        raise ValueError("字幕编辑器不是稳定发行版。")
    asset = next((a for a in data["assets"] if a["name"] == ASSET), None)
    if not asset or not re.fullmatch(r"sha256:[0-9a-f]{64}", asset.get("digest") or ""):
        raise ValueError("官方发行版缺少 Mac ARM64 安装包或 SHA-256。")
    expected_url = "https://github.com/SubtitleEdit/subtitleedit/releases/download/" + urllib.parse.quote(data["tag_name"], safe="") + "/" + ASSET
    if asset["browser_download_url"] != expected_url:
        raise ValueError("字幕编辑器下载地址不属于官方发行版。")
    return data["tag_name"], asset


def verify(app):
    requirement = '=anchor apple generic and identifier "dk.nikse.subtitleedit" and certificate leaf[subject.OU] = "MFC9L2T4RB"'
    for arguments, label in ((["/usr/bin/codesign", "--verify", "--deep", "--strict", "-R", requirement, str(app)], "签名"),
                             (["/usr/sbin/spctl", "--assess", "--type", "execute", "--verbose=2", str(app)], "Apple 公证")):
        result = subprocess.run(arguments, capture_output=True, text=True, timeout=120)
        if result.returncode:
            raise RuntimeError(f"Subtitle Edit {label}检查未通过，未安装；请检查网络后重试，Aurora 不会绕过安全检查。\n" + result.stdout + result.stderr)


def fetch(base, asset):
    checksum = asset["digest"].split(":")[1]
    directory = base / "downloads" / checksum
    directory.mkdir(parents=True, exist_ok=True)
    destination = directory / ASSET
    if destination.is_file() and digest(destination) == checksum:
        return destination
    if destination.exists(): destination.rename(directory / (ASSET + ".rejected-" + uuid.uuid4().hex))
    partial = directory / (ASSET + ".partial")
    for _ in range(2):
        size = partial.stat().st_size if partial.exists() else 0
        if size == asset["size"] and digest(partial) == checksum:
            partial.rename(destination)
            return destination
        request = urllib.request.Request(asset["browser_download_url"], headers={"User-Agent": "Aurora-Audio-Studio/1.9.0", **({"Range": f"bytes={size}-"} if size else {})})
        try:
            response = urllib.request.urlopen(request, timeout=120)
        except urllib.error.HTTPError as error:
            if error.code == 416 and partial.exists():
                partial.rename(directory / (partial.name + ".rejected-" + uuid.uuid4().hex))
                continue
            raise
        with response:
            resume = response.status == 206 and size > 0
            if response.status == 206 and not response.headers.get("Content-Range", "").startswith(f"bytes {size}-"):
                raise ValueError("字幕编辑器续传位置无效。")
            if not resume: size = 0
            with partial.open("ab" if resume else "wb") as output:
                last = 0
                while block := response.read(1024 * 1024):
                    output.write(block); size += len(block)
                    if time.monotonic() - last > 1:
                        emit("progress", model="subtitle-edit", stage="下载 Subtitle Edit", completed=size, total=asset["size"])
                        last = time.monotonic()
        if size < asset["size"]:
            raise IOError(f"字幕编辑器下载中断，已保留 {size}/{asset['size']} 字节，再次安装可继续下载。")
        if size != asset["size"] or digest(partial) != checksum:
            partial.rename(directory / (partial.name + ".rejected-" + uuid.uuid4().hex))
            raise ValueError("字幕编辑器安装包 SHA-256 校验失败，旧版保持不变。")
        partial.rename(destination)
        return destination
    raise ValueError("字幕编辑器下载未完成，可重试。")


def install(base, update=False):
    current = base / "current"
    old = json.loads((current / "receipt.json").read_text()) if (current / "receipt.json").exists() else None
    tag, asset = release(None if update or not old else old["assets"][0]["revision"])
    dmg = fetch(base, asset)
    identity = uuid.uuid4().hex
    stage = base / ("native-staging-" + identity)
    stage.mkdir()
    mount = base / ("mount-" + identity)
    mount.mkdir()
    subprocess.run(["/usr/bin/hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint", str(mount), str(dmg)], check=True, timeout=120)
    try:
        applications = list(mount.glob("*.app"))
        if len(applications) != 1: raise ValueError("字幕编辑器安装包应用数量不正确。")
        verify(applications[0])
        subprocess.run(["/usr/bin/ditto", str(applications[0]), str(stage / APP)], check=True, timeout=180)
        verify(stage / APP)
    finally:
        subprocess.run(["/usr/bin/hdiutil", "detach", str(mount)], check=True, timeout=120)
    files = []
    for path in sorted((stage / APP).rglob("*")):
        if not path.resolve().is_relative_to(stage.resolve()): raise ValueError("应用包含越界链接。")
        if path.is_file(): files.append(dict(path=str(path.relative_to(stage)), size=path.stat().st_size, sha256=digest(path)))
    if not files: raise ValueError("字幕编辑器应用内容为空。")
    receipt = dict(model="subtitle-edit", family="subtitle-edit", installed_at=time.time(),
                   assets=[dict(url=asset["browser_download_url"], revision=tag)], files=files)
    (stage / "receipt.json").write_text(json.dumps(receipt, indent=2))
    previous = base / "previous"
    if previous.exists():
        history = base / "history"
        history.mkdir(exist_ok=True)
        previous.rename(history / identity)
    if current.exists(): current.rename(previous)
    try:
        stage.rename(current)
    except BaseException:
        if previous.exists() and not current.exists(): previous.rename(current)
        raise


def updates(base):
    old = json.loads((base / "current/receipt.json").read_text())["assets"][0]["revision"]
    tag, _ = release()
    result = dict(at=time.time(), available=tag != old, message="Subtitle Edit 有新版本：" + tag if tag != old else "Subtitle Edit 已是当前版本。", updates=[tag] if tag != old else [])
    (base / "current/update-status.json").write_text(json.dumps(result, ensure_ascii=False))
    emit("completed", model="subtitle-edit", message=result["message"])
