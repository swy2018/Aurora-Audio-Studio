import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { chooseDownload, releaseVersion, compareVersions } from '../../../docs/release-policy.mjs';
const metadata = JSON.parse(await readFile(new URL('../../../docs/release.json',import.meta.url)));
function release(version, beta=false, draft=false, platforms=['windows','mac']) {
  const tag_name='v'+version, prefix='https://github.com/swy2018/Aurora-Audio-Studio/releases/download/'+tag_name+'/';
  const names=platforms.flatMap(platform=>{const name='Aurora-Audio-Studio-'+version+(platform==='mac'?'-arm64.dmg':'-Setup-x64.exe');return [name,name+'.sha256'];});
  return {tag_name,prerelease:beta,draft,assets:names.map(name=>({name,browser_download_url:prefix+name}))};
}
const releases=[release('1.9.9'),release('2.0.0-beta.1',true),release('2.0.0-beta.10',true),release('3.0.0',false,true)];
for(const platform of ['windows','mac']) {
  assert.equal(chooseDownload(releases,platform,'stable').version,'1.9.9');
  assert.equal(chooseDownload(releases,platform,'beta').version,'2.0.0-beta.10');
  const current=release(metadata.version,metadata.version.includes('-beta.'));
  assert.equal(chooseDownload([current],platform,current.prerelease?'beta':'stable').version,metadata.version);
}
assert.equal(chooseDownload([release('2.0.0-beta.1',false)],'mac','stable'),null);
assert.equal(chooseDownload([release('1.9.9',false,false,['windows'])],'mac','stable'),null);
assert.equal(chooseDownload([{...release('1.9.9'),assets:[]}],'windows','stable'),null);
const malicious=release('1.9.9'); malicious.assets[0].browser_download_url='https://example.com/installer.exe';
assert.equal(chooseDownload([malicious],'windows','stable'),null);
assert(compareVersions(releaseVersion('2.0.0'),releaseVersion('2.0.0-beta.10'))>0);
console.log('11 website release-policy checks passed, including current release metadata and both platforms.');
