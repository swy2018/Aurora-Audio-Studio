param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$EvidenceRoot,
      [Parameter(Mandatory)][string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
$Executable = [IO.Path]::GetFullPath($Executable)
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$results = [Collections.Generic.List[object]]::new()
foreach ($culture in @(
    @('zh-CN','回退到上一个正式版','开','关'),
    @('zh-TW','回復至上一個正式版','開','關'),
    @('en-US','Revert to previous stable release','On','Off'),
    @('ja-JP','前の正式版に戻す','オン','オフ'))) {
    $state = Join-Path $EvidenceRoot $culture[0]
    [IO.Directory]::CreateDirectory($state) | Out-Null
    $config = @{Language=$culture[0];Theme='light';AutoCheckAppUpdates=$false;AutoCheckModelUpdates=$false;
        LocalAiRoot=(Join-Path $state 'Models');OutputRoot=(Join-Path $state 'Output');ProjectsRoot=(Join-Path $state 'Projects')}
    [IO.File]::WriteAllText((Join-Path $state 'settings.json'), ($config | ConvertTo-Json))
    [IO.File]::WriteAllText((Join-Path $state 'window-state.json'), '{"Width":1440,"Height":1000,"IsMaximized":true}')
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.Environment['AURORA_DATA_ROOT'] = $state
    $app = [Diagnostics.Process]::Start($start)
    function Ui([string[]]$Arguments) {
        $raw = & winapp ui @Arguments -a $app.Id --json
        $code = $LASTEXITCODE
        $value = $raw | ConvertFrom-Json
        if ($code -ne 0 -or ($value -isnot [array] -and $value.error)) { throw ($raw -join [Environment]::NewLine) }
        return $value
    }
    try {
        $ready = Ui @('wait-for','HomeItem','-t','15000')
        if (!$ready.found -or $app.HasExited) { throw 'Application did not open.' }
        [void](Ui @('invoke','SettingsItem'))
        $ready = Ui @('wait-for','LanguagePicker','-p','IsOffscreen','--value','False','-t','5000')
        if (!$ready.found) { throw 'Settings did not open.' }
        $tree = Ui @('inspect','--interactive')
        $pickers = @($tree.windows.elements | Where-Object { $_.automationId -in 'LanguagePicker','ThemePicker','AppUpdateChannelPicker' })
        if ($pickers.Count -ne 3 -or @($pickers.x | Select-Object -Unique).Count -ne 1 -or @($pickers.width | Select-Object -Unique).Count -ne 1) { throw 'Settings pickers do not share their left edge and width.' }
        [IO.File]::WriteAllText((Join-Path $state 'alignment.json'), ($pickers | Select-Object automationId,x,y,width | ConvertTo-Json))
        [void](Ui @('screenshot','-o',(Join-Path $state 'settings.png')))
        # WinUI does not expose OnContent/OffContent as separate UIA text nodes.
        # Their bindings are covered by UpdateFlowTests; review settings.png for rendered captions.
        [void](Ui @('invoke','AboutItem'))
        $button = Ui @('wait-for','AppRollbackButton','--value',$culture[1],'-t','5000')
        if (!$button.found) { throw 'Localized rollback button is missing.' }
        $version = Ui @('wait-for','AboutVersionText','--value',$ExpectedVersion,'--contains','-t','5000')
        if (!$version.found) { throw 'About version is incorrect.' }
        [void](Ui @('screenshot','-o',(Join-Path $state 'about.png')))
        $results.Add([pscustomobject]@{Language=$culture[0];Status='PASS';Version=$ExpectedVersion;Aligned=$true;RollbackButton=$culture[1]})
    } catch {
        $results.Add([pscustomobject]@{Language=$culture[0];Status='FAIL';Detail=$_.Exception.Message})
    } finally {
        if (!$app.HasExited) { [void]$app.CloseMainWindow(); if (!$app.WaitForExit(15000)) { throw 'QA app did not close normally.' } }
    }
}
[IO.File]::WriteAllText((Join-Path $EvidenceRoot 'results.json'), ($results | ConvertTo-Json -Depth 5))
$results | Format-Table -AutoSize
if (@($results | Where-Object Status -eq FAIL).Count) { exit 1 }
