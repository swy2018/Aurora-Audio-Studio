param(
    [string]$Runtime = (Join-Path $PSScriptRoot '../AuroraAudioStudio/Runtime'),
    [string]$Cache = (Join-Path $PSScriptRoot '../../../.maintenance/windows-runtime-downloads'),
    [switch]$SkipPrerequisites
)
$ErrorActionPreference = 'Stop'
$Runtime = [IO.Path]::GetFullPath($Runtime)
$Cache = [IO.Path]::GetFullPath($Cache)
$manifestPath = Join-Path $PSScriptRoot 'windows-runtime.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "Runtime bytes do not match the pinned source: $Path. Run Prepare-WindowsRuntime.ps1 and investigate stale files; nothing was deleted."
    }
}
Assert-Hash (Join-Path $Runtime 'windows-runtime.json') (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$count = 0
$allExpected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$allExpected.Add((Join-Path $Runtime 'windows-runtime.json'))
foreach ($item in Get-ChildItem -LiteralPath $Runtime -Recurse -Force) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected runtime link: $($item.FullName)" }
}
foreach ($name in $manifest.Keys) {
    $entry = $manifest[$name]
    if ($entry.format -eq 'exe') {
        $path = Join-Path $Runtime ('prerequisites/' + $entry.fileName)
        if (-not $SkipPrerequisites) { Assert-Hash $path $entry.sha256; [void]$allExpected.Add($path); $count++ }
        continue
    }
    $archive = Join-Path $Cache ($entry.sha256 + '.zip')
    Assert-Hash $archive $entry.sha256
    $target = [IO.Path]::GetFullPath((Join-Path $Runtime "bin/$name"))
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($file in $zip.Entries) {
            if (-not $file.FullName.StartsWith($entry.prefix, [StringComparison]::Ordinal)) { throw 'Unexpected archive root.' }
            if (-not $file.Name) { continue }
            $relative = $file.FullName.Substring($entry.prefix.Length)
            $path = [IO.Path]::GetFullPath((Join-Path $target $relative))
            if (-not $path.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive boundary violation.' }
            if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected runtime link: $path" }
            $stream = $file.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
            finally { $stream.Dispose() }
            Assert-Hash $path $hash
            [void]$expected.Add($path); $count++
        }
    } finally { $zip.Dispose() }
    foreach ($license in $entry.licenses) {
        $path = Join-Path $target $license.name
        Assert-Hash $path $license.sha256
        [void]$expected.Add([IO.Path]::GetFullPath($path)); $count++
    }
    if ($name -eq 'sox') {
        $path = Join-Path $target ($entry.executable + '.manifest')
        Assert-Hash $path (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'sox.exe.manifest') -Algorithm SHA256).Hash
        [void]$expected.Add([IO.Path]::GetFullPath($path)); $count++
    }
    foreach ($file in Get-ChildItem -LiteralPath $target -Recurse -File) {
        if (-not $expected.Contains($file.FullName)) { throw "Untracked runtime file would be shipped: $($file.FullName). Review it before packaging; nothing was deleted." }
    }
    $allExpected.UnionWith($expected)
}
foreach ($file in Get-ChildItem -LiteralPath $Runtime -Recurse -File -Force) {
    if (-not $allExpected.Contains($file.FullName)) { throw "Untracked runtime file would be shipped: $($file.FullName). Nothing was deleted." }
}
Write-Host "PASS $count runtime files match pinned archives, prerequisites, licenses and the Unicode manifest."
