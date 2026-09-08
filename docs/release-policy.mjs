export function releaseVersion(tag) {
  const match = /^v?(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$/.exec(tag);
  return match ? [Number(match[1]), Number(match[2]), Number(match[3]), match[4] ? Number(match[4]) : Number.MAX_SAFE_INTEGER] : null;
}
export function compareVersions(a,b) {
  for (let i=0;i<4;i++) if(a[i]!==b[i]) return a[i]-b[i];
  return 0;
}
export function chooseDownload(releases,platform,channel) {
  const candidates = releases.filter(r => !r.draft && releaseVersion(r.tag_name)
    && (channel === 'beta' ? r.prerelease && r.tag_name.includes('-beta.') : !r.prerelease && !r.tag_name.includes('-beta.')))
    .sort((a,b) => compareVersions(releaseVersion(b.tag_name),releaseVersion(a.tag_name)));
  for (const release of candidates) {
    const version = release.tag_name.replace(/^v/,'');
    const name = 'Aurora-Audio-Studio-' + version + (platform === 'mac' ? '-arm64.dmg' : '-Setup-x64.exe');
    const installer = release.assets.find(a => a.name === name);
    const checksum = release.assets.find(a => a.name === name + '.sha256');
    if(!installer || !checksum) continue;
    const prefix = 'https://github.com/swy2018/Aurora-Audio-Studio/releases/download/' + release.tag_name + '/';
    if(installer.browser_download_url !== prefix + name || checksum.browser_download_url !== prefix + name + '.sha256') continue;
    return {version,name,url:installer.browser_download_url,checksum:checksum.browser_download_url,beta:release.prerelease};
  }
  return null;
}
