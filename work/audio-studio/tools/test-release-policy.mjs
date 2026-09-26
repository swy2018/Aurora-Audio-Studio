import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { chooseDownload, releaseVersion, compareVersions } from '../../../docs/release-policy.mjs';
import { loadReleaseData } from '../../../docs/release-data.mjs';
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
const partialBeta = [release('2.0.0-beta.2',true,false,['windows']),release('2.0.0-beta.1',true),release('1.9.9')];
assert.equal(chooseDownload(partialBeta,'windows','beta').version,'2.0.0-beta.2');
assert.equal(chooseDownload(partialBeta,'mac','beta').version,'2.0.0-beta.1');
assert.equal(chooseDownload(partialBeta,'windows','stable').version,'1.9.9');
console.log('Website release-policy checks passed, including staggered Windows/Mac beta packages and unchanged Stable.');
const stagedStable = [release('2.0.0',false,false,['windows']),release('2.0.0-beta.4',true),release('1.9.9')];
assert.equal(chooseDownload(stagedStable,'windows','stable').version,'2.0.0');
assert.equal(chooseDownload(stagedStable,'mac','stable').version,'1.9.9');
assert.equal(chooseDownload(stagedStable,'windows','beta').version,'2.0.0');
assert.equal(chooseDownload(stagedStable,'mac','beta').version,'2.0.0-beta.4');
console.log('Platform-specific 2.0.0 stable promotion checks passed.');
const snapshot = {schemaVersion:1, verifiedAt:'2026-09-27', releases:[release('2.0.0'),release('2.0.1-beta.1',true,false,['windows'])]};
for (const status of [403,429,500]) {
  let data, source;
  const ok = await loadReleaseData(async url => url.includes('api.github') ? {ok:false,status} : {ok:true,json:async()=>snapshot}, (value, kind) => {data=value;source=kind;});
  assert(ok); assert.equal(source,'snapshot');
  assert.equal(chooseDownload(data,'windows','beta').version,'2.0.1-beta.1');
  assert.equal(chooseDownload(data,'mac','stable').version,'2.0.0');
  assert.equal(chooseDownload(data,'mac','beta').version,'2.0.0');
}
let live;
await loadReleaseData(async url => ({ok:true,json:async()=>url.includes('api.github') ? [] : snapshot}), value => live=value);
assert.deepEqual(live,[], 'live asset removal must override a cached snapshot');
assert.equal(await loadReleaseData(async()=>{throw new Error('offline');},()=>assert.fail()),false);
console.log('Release snapshot checks passed: 403/429/500, Windows-only beta, live precedence, fully offline.');
