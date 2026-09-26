param([string]$Repository = 'swy2018/Aurora-Audio-Studio')
$ErrorActionPreference = 'Stop'
# Generate only from published assets returned by GitHub; never advertise a planned package.
$raw = & gh api "repos/$Repository/releases?per_page=100"
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch published releases.' }
$releases = $raw | ConvertFrom-Json
$snapshot = @($releases | Where-Object { -not $_.draft } | ForEach-Object {
    [ordered]@{ tag_name=$_.tag_name; draft=$false; prerelease=[bool]$_.prerelease; assets=@($_.assets | Where-Object { $_.state -eq 'uploaded' } | ForEach-Object {
        [ordered]@{ name=$_.name; browser_download_url=$_.browser_download_url; size=$_.size; digest=$_.digest }
    }) }
})
$document = [ordered]@{ schemaVersion=1; verifiedAt=[DateTimeOffset]::UtcNow.ToString('o'); releases=$snapshot }
$destination = Join-Path $PSScriptRoot '../../../docs/release-assets.json'
$document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination -Encoding utf8
Write-Output "Exported $($snapshot.Count) published releases to $destination"
