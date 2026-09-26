export function validReleases(value) {
  if (!Array.isArray(value)) throw new Error('Invalid release list');
  return value.filter(release => release && typeof release.tag_name === 'string'
    && typeof release.prerelease === 'boolean' && typeof release.draft === 'boolean'
    && Array.isArray(release.assets)).map(release => ({...release, assets: release.assets.filter(asset =>
      asset && typeof asset.name === 'string' && typeof asset.browser_download_url === 'string')}));
}

// The published snapshot makes downloads independent of unauthenticated GitHub API limits.
// A successful live API response takes precedence, including deliberate asset removal.
export async function loadReleaseData(fetcher, onData) {
  let live = false, available = false;
  const read = async (url, snapshot) => {
    const response = await fetcher(url, {signal: AbortSignal.timeout(8000), cache: 'no-cache'});
    if (!response.ok) throw new Error('Release request failed');
    const body = await response.json();
    if (snapshot && (body.schemaVersion !== 1 || !body.verifiedAt)) throw new Error('Invalid release snapshot');
    const releases = validReleases(snapshot ? body.releases : body);
    if (snapshot && !releases.length) throw new Error('Empty release snapshot');
    if (!snapshot) live = true;
    if (!snapshot || !live) { available = true; onData(releases, snapshot ? 'snapshot' : 'live'); }
  };
  await Promise.allSettled([
    read('./release-assets.json', true),
    read('https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases?per_page=100', false)
  ]);
  return available;
}
