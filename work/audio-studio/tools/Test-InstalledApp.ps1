param([Parameter(Mandatory)][string]$Installer, [Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $EvidenceRoot) { throw 'Use a new isolated acceptance directory.' }
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$destination = Join-Path $EvidenceRoot 'Installed'
$setup = Start-Process -FilePath ([IO.Path]::GetFullPath($Installer)) -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/DIR=`"$destination`"", "/LOG=`"$(Join-Path $EvidenceRoot 'setup.log')`"") -Wait -PassThru
if ($setup.ExitCode -ne 0) { throw "Installer failed: $($setup.ExitCode)" }
$executable = Join-Path $destination 'Aurora Audio Studio.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Installed executable is missing.' }
$state = Join-Path $EvidenceRoot 'State'
[IO.Directory]::CreateDirectory($state) | Out-Null
$settings = @{ Language='en-US'; AutoCheckAppUpdates=$false; AutoCheckModelUpdates=$false; LocalAiRoot=(Join-Path $state 'Models'); OutputRoot=(Join-Path $state 'Output'); ProjectsRoot=(Join-Path $state 'Projects') }
[IO.File]::WriteAllText((Join-Path $state 'settings.json'), ($settings | ConvertTo-Json))
$start = [Diagnostics.ProcessStartInfo]::new($executable)
$start.UseShellExecute = $false
$start.Environment['AURORA_DATA_ROOT'] = $state
$app = [Diagnostics.Process]::Start($start)
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $app.Refresh()
        if ($app.HasExited) { throw "Installed app exited before opening a window: $($app.ExitCode)" }
    } until (($app.MainWindowHandle -ne 0 -and $app.MainWindowTitle -match 'Aurora') -or [DateTime]::UtcNow -ge $deadline)
    if ($app.MainWindowHandle -eq 0 -or !$app.Responding -or $app.MainWindowTitle -notmatch 'Aurora') { throw 'Installed app did not show a responsive Aurora window.' }
    [IO.File]::WriteAllText((Join-Path $EvidenceRoot 'result.json'), (@{ Installed=$true; WindowTitle=$app.MainWindowTitle; Responsive=$app.Responding; Version=[Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion } | ConvertTo-Json))
    Write-Output 'Installed app opens a responsive Aurora window. Model inference and upgrade handoff are not covered by this smoke test.'
} finally {
    if (!$app.HasExited) { [void]$app.CloseMainWindow(); if (!$app.WaitForExit(15000)) { throw 'Acceptance app did not close normally.' } }
}
