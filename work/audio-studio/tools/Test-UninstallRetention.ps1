param([Parameter(Mandatory)][string]$AcceptanceRoot)
$ErrorActionPreference = 'Stop'
# This test intentionally uninstalls the CI acceptance copy. Never run against a user's installation.
if ($env:GITHUB_ACTIONS -ne 'true' -or !$env:RUNNER_TEMP) { throw 'This destructive fixture is restricted to an ephemeral GitHub Actions runner.' }
$AcceptanceRoot = [IO.Path]::GetFullPath($AcceptanceRoot)
$runner = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\')
if (!$AcceptanceRoot.StartsWith($runner+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Acceptance copy must be inside RUNNER_TEMP.' }
$uninstaller = Join-Path $AcceptanceRoot 'Installed\unins000.exe'
if (!(Test-Path -LiteralPath $uninstaller)) { throw 'Acceptance uninstaller is missing.' }
$data = Join-Path $env:LOCALAPPDATA 'Aurora Audio Studio'
if (Test-Path -LiteralPath $data) { throw 'Refuse to replace an existing profile, even on CI.' }
[IO.Directory]::CreateDirectory($data) | Out-Null
$retained = @('Output\piece.wav','Models\model.bin','Projects\record.arr','Logs\user-kept.txt','custom.json')
foreach ($relative in $retained) {
    $path = Join-Path $data $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, 'user-data-'+$relative)
}
$preferences = @('settings.json','tasks.json','utility-drafts.json','window-state.json')
foreach ($name in $preferences) { [IO.File]::WriteAllText((Join-Path $data $name), '{}') }
$before = @{}
foreach ($relative in $retained) { $before[$relative] = (Get-FileHash -LiteralPath (Join-Path $data $relative)).Hash }
$process = Start-Process -FilePath $uninstaller -WindowStyle Hidden -ArgumentList @('/VERYSILENT','/NORESTART','/REMOVEUSERDATA',"/LOG=`"$(Join-Path $AcceptanceRoot 'uninstall.log')`"") -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
foreach ($relative in $retained) {
    $path = Join-Path $data $relative
    if (!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -cne $before[$relative]) { throw "User file changed: $relative" }
}
foreach ($name in $preferences) { if (Test-Path -LiteralPath (Join-Path $data $name)) { throw "Owned preference was not cleared: $name" } }
[IO.File]::WriteAllText((Join-Path $AcceptanceRoot 'uninstall-retention.json'), (@{RetainedFiles=$retained;ClearedPreferences=$preferences;Passed=$true} | ConvertTo-Json))
'PASS actual installer/uninstaller preserves nested models, outputs, records and unrelated files.'
