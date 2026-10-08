param(
    [string]$Destination = (Join-Path $PSScriptRoot '../AuroraAudioStudio/Runtime'),
    [string]$Cache = (Join-Path $PSScriptRoot '../../../.maintenance/windows-runtime-downloads')
)
$ErrorActionPreference = 'Stop'
$Destination = [IO.Path]::GetFullPath($Destination)
$Cache = [IO.Path]::GetFullPath($Cache)
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'windows-runtime.json') -Raw | ConvertFrom-Json -AsHashtable
[IO.Directory]::CreateDirectory($Cache) | Out-Null
foreach ($name in $manifest.Keys) {
    $entry = $manifest[$name]
    $extension = if ($entry.format -eq 'exe') { '.exe' } else { '.zip' }
    $archive = Join-Path $Cache ($entry.sha256 + $extension)
    if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $entry.sha256) {
        Write-Host "Downloading verified $name runtime..."
        # Build-time download only. End users never invoke WinGet or install system tools.
        $partial = $archive + '.part'
        if (-not (Test-Path -LiteralPath $partial) -or (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $entry.sha256) {
            Invoke-WebRequest -Uri $entry.url -OutFile $partial -TimeoutSec 600
            # SourceForge sometimes returns an HTML refresh instead of an HTTP redirect.
            if ($name -eq 'sox' -and (Get-Item -LiteralPath $partial).Length -lt 1048576) {
                $page = Get-Content -LiteralPath $partial -Raw
                $redirect = [regex]::Match($page, '<meta http-equiv="refresh" content="\d+; url=([^">]+)')
                if ($redirect.Success) {
                    $url = [Net.WebUtility]::HtmlDecode($redirect.Groups[1].Value)
                    if (([uri]$url).Scheme -ne 'https' -or ([uri]$url).Host -ne 'downloads.sourceforge.net') { throw 'Unexpected SourceForge redirect' }
                    Invoke-WebRequest -Uri $url -OutFile $partial -TimeoutSec 600
                }
            }
        }
        if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $entry.sha256) { throw "SHA-256 mismatch for $name. Retained for inspection: $partial" }
        [IO.File]::Move($partial, $archive, $true)
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "SHA-256 mismatch for $name. Retained for inspection: $archive"
    }
    if ($entry.format -eq 'exe') {
        $signature = Get-AuthenticodeSignature -LiteralPath $archive
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') {
            throw "Prerequisite $name must have a valid Microsoft signature."
        }
        if ((Get-Item -LiteralPath $archive).VersionInfo.ProductVersion -ne $entry.version) { throw "Prerequisite $name version mismatch." }
        $prerequisites = Join-Path $Destination 'prerequisites'
        [IO.Directory]::CreateDirectory($prerequisites) | Out-Null
        Copy-Item -LiteralPath $archive -Destination (Join-Path $prerequisites $entry.fileName)
        # Verify only. The build/test host must never install or repair its system runtime here.
        continue
    }
    $target = Join-Path $Destination "bin/$name"
    [IO.Directory]::CreateDirectory($target) | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($file in $zip.Entries) {
            if (-not $file.FullName.StartsWith($entry.prefix, [StringComparison]::Ordinal)) { throw "Unexpected archive root: $($file.FullName)" }
            $relative = $file.FullName.Substring($entry.prefix.Length)
            if (-not $relative) { continue }
            $path = [IO.Path]::GetFullPath((Join-Path $target $relative))
            if (-not $path.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes runtime directory.' }
            if (($file.ExternalAttributes -shr 16 -band 0xF000) -eq 0xA000) { throw 'Unexpected archive symlink.' }
            if ($file.Name -eq '') { [IO.Directory]::CreateDirectory($path) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($file, $path, $true)
        }
    } finally { $zip.Dispose() }
    foreach ($license in $entry.licenses) {
        $licensePath = Join-Path $target $license.name
        if (-not (Test-Path -LiteralPath $licensePath) -or (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash -ne $license.sha256) {
            Invoke-WebRequest -Uri $license.url -OutFile $licensePath -TimeoutSec 60
        }
        if ((Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash -ne $license.sha256) { throw "License checksum mismatch: $name / $($license.name)" }
    }
    $executable = Join-Path $target $entry.executable
    $versionArgument = if ($name -eq 'ffmpeg') { '-version' } else { '--version' }
    & $executable $versionArgument
    if ($LASTEXITCODE -ne 0) { throw "Bundled $name cannot run: $executable" }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'windows-runtime.json') -Destination (Join-Path $Destination 'windows-runtime.json')
Write-Host "Verified runtime ready: $Destination (archives and upstream licenses retained)"
