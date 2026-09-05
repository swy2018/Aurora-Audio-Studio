param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$EvidenceRoot, [string]$Language = 'en-US', [switch]$LanguagesOnly)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Aurora Audio Studio' -ErrorAction SilentlyContinue) { throw 'A user Aurora session is already running.' }
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$state = Join-Path $EvidenceRoot 'state'
[IO.Directory]::CreateDirectory($state) | Out-Null
$settings = @{ LocalAiRoot='C:\LocalAI'; OutputRoot=(Join-Path $EvidenceRoot 'Output'); ProjectsRoot=(Join-Path $state 'Projects'); Language=$Language; Theme='light'; AutoCheckAppUpdates=$false; AutoCheckModelUpdates=$false; SafeMode=$false }
[IO.File]::WriteAllText((Join-Path $state 'settings.json'), ($settings | ConvertTo-Json))
[IO.File]::WriteAllText((Join-Path $state 'window-state.json'), '{"Width":960,"Height":640,"IsMaximized":false}')
$start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Executable))
$start.UseShellExecute = $false
$start.EnvironmentVariables['AURORA_DATA_ROOT'] = $state
$app = [Diagnostics.Process]::Start($start)
$results = [Collections.Generic.List[object]]::new()
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $results.Add(@{name=$Name;status='PASS'}) }
    catch { $results.Add(@{name=$Name;status='FAIL';detail=$_.Exception.Message}) }
}
$mainHwnd = $null
function Ui([string[]]$Arguments) {
    $target = if ($mainHwnd) { @('-w',"$mainHwnd") } else { @('-a',"$($app.Id)") }
    $result = & winapp ui @Arguments @target 2>&1; if ($LASTEXITCODE) { throw ($result -join "`n") }; return $result
}
try {
    Ui @('wait-for','HomeItem','-t','20000') | Out-Null
    $windows = winapp ui list-windows -a $app.Id --json | ConvertFrom-Json
    $mainHwnd = ($windows | Where-Object title -match 'Aurora Audio Studio' | Select-Object -First 1).hwnd
    $navName = @{ 'en-US'='Music'; 'zh-CN'='音乐创作'; 'zh-TW'='音樂創作'; 'ja-JP'='音楽制作' }[$Language]
    Test-Case 'Sidebar language' { Ui @('wait-for','MusicItem','--value',$navName,'-t','3000') | Out-Null }
    foreach ($page in $(if ($LanguagesOnly) { @('AboutItem') } else { @('MusicItem','VoiceItem','SingingItem','SeparationItem','TranscriptionItem','SubtitlesItem','ModelsItem','TasksItem','ResultsItem','MaintenanceItem','SettingsItem','AboutItem') })) {
        Test-Case $page {
            Ui @('inspect','--interactive','--json') | Out-Null
            Ui @('invoke',$page) | Out-Null
            if ($page -in @('MusicItem','VoiceItem','SingingItem')) {
                Ui @('wait-for','OpenWorkbenchButton','-p','IsOffscreen','--value','False','-t','3000') | Out-Null
                Ui @('wait-for','OpenWorkbenchButton','-p','IsEnabled','--value','True','-t','3000') | Out-Null
            }
            if ($page -in @('SeparationItem','TranscriptionItem','SubtitlesItem')) {
                Ui @('wait-for','StickyRunButton','-p','IsOffscreen','--value','False','-t','3000') | Out-Null
                Ui @('wait-for','StickyRunButton','-p','IsEnabled','--value','False','-t','3000') | Out-Null
            }
            Ui @('screenshot','--focus','-o',(Join-Path $EvidenceRoot ($page + '.png')),'--json') | Out-Null
            $tree = Ui @('inspect','--interactive','--json')
            [IO.File]::WriteAllText((Join-Path $EvidenceRoot ($page + '.json')), ($tree -join "`n"))
        }
    }
    Test-Case 'About version' { Ui @('wait-for','AboutVersionText','--value','1.9.0','--contains','-t','3000') | Out-Null }
    foreach ($culture in @(@('zh-CN','简体中文','音乐创作'),@('zh-TW','繁體中文','音樂創作'),@('en-US','English','Music'),@('ja-JP','日本語','音楽制作'),@('zh-CN','简体中文','音乐创作'))) {
        Test-Case ("Switch language " + $culture[0]) {
            Ui @('invoke','SettingsItem') | Out-Null
            Ui @('invoke','LanguagePicker') | Out-Null
            $snapshot = (Ui @('inspect','--interactive','--json') -join "`n") | ConvertFrom-Json
            $picker = $snapshot.windows.elements | Where-Object automationId -eq 'LanguagePicker' | Select-Object -First 1
            $option = $picker.children | Where-Object name -eq $culture[1] | Select-Object -First 1
            if (!$option.selector) { throw "Language option is absent: $($culture[1])" }
            Ui @('invoke',$option.selector) | Out-Null
            $saved = [IO.File]::ReadAllText((Join-Path $state 'settings.json')) | ConvertFrom-Json
            if ($saved.Language -ne $culture[0]) { throw "Language was not persisted: $($saved.Language)" }
            Ui @('wait-for','MusicItem','--value',$culture[2],'-t','3000') | Out-Null
            Ui @('invoke','SingingItem') | Out-Null
            Ui @('screenshot','--focus','-o',(Join-Path $EvidenceRoot ("switch-" + $culture[0] + '.png')),'--json') | Out-Null
        }
    }
} finally {
    [IO.File]::WriteAllText((Join-Path $EvidenceRoot 'results.json'), ($results | ConvertTo-Json -Depth 5))
    if (!$app.HasExited) { $app.Refresh(); [void]$app.CloseMainWindow(); if (!$app.WaitForExit(30000)) { throw 'Acceptance window did not close normally.' } }
}
$results | Format-Table -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count) { exit 1 }
Write-Output 'Native UI assertions passed. Screenshots still require visual review.'
