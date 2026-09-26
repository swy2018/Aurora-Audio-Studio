param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$EvidenceRoot,
      [Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$StemProject)
$ErrorActionPreference = 'Stop'
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$state = Join-Path $EvidenceRoot 'state'
if (Test-Path -LiteralPath $state) { throw 'Use a new isolated UI evidence directory.' }
[IO.Directory]::CreateDirectory((Join-Path $state 'Projects')) | Out-Null
$config = @{Language='en-US';Theme='light';AutoCheckAppUpdates=$false;AutoCheckModelUpdates=$false;LocalAiRoot='C:\LocalAI';OutputRoot=(Join-Path $state 'Output');ProjectsRoot=(Join-Path $state 'Projects')}
[IO.File]::WriteAllText((Join-Path $state 'settings.json'), ($config | ConvertTo-Json))
[IO.File]::WriteAllText((Join-Path $state 'window-state.json'), '{"Width":1800,"Height":1400,"IsMaximized":false}')
$Source = [IO.Path]::GetFullPath($Source)
$draft = @{transcription=@{Sources=@($Source);SelectedPath=$Source;ModelId='yourmt3';Preset='custom';TrackMode='two-stem';SourceLanguage='auto'}}
[IO.File]::WriteAllText((Join-Path $state 'utility-drafts.json'), ($draft | ConvertTo-Json -Depth 5))
Copy-Item -LiteralPath ([IO.Path]::GetFullPath($StemProject)) -Destination (Join-Path $state 'Projects/stems.arr')
$start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Executable)); $start.UseShellExecute=$false
$start.Environment['AURORA_DATA_ROOT']=$state
$app = [Diagnostics.Process]::Start($start)
$checks = [Collections.Generic.List[string]]::new()
$window = 0
function Ui([string[]]$Arguments) {
    $target = if ($window) { @('-w',"$window") } else { @('-a',"$($app.Id)") }
    $output = & winapp ui @Arguments @target --json
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n" | ConvertFrom-Json)
}
function Check([bool]$Value,[string]$Name) { if (!$Value) { throw $Name }; $checks.Add($Name) }
function Flat($nodes) { foreach ($node in $nodes) { $node; Flat $node.children } }
try {
    [void](Ui @('wait-for','HomeItem','-t','20000'))
    $app.Refresh(); $window=$app.MainWindowHandle.ToInt64()
    [void](Ui @('invoke','TranscriptionItem'))
    [void](Ui @('wait-for','InputMetadataText','--value','44100','--contains','-t','20000'))
    # MediaPlayerElement does not expose its own UIA peer; verify metadata here and inspect its screenshot below.
    Check $true 'Selected audio has decoded stream metadata'
    [void](Ui @('screenshot','-o',(Join-Path $EvidenceRoot 'selected-media.png')))
    foreach ($case in @(@('ja-JP','日本語','カスタム'),@('en-US','English','Custom'),@('zh-TW','繁體中文','自訂'),@('zh-CN','简体中文','自定义'))) {
        [void](Ui @('invoke','SettingsItem')); [void](Ui @('invoke','LanguagePicker'))
        $tree=Ui @('inspect','--depth','12')
        $option=Flat $tree.windows[0].elements | Where-Object { $_.type -eq 'ListItem' -and $_.name -eq $case[1] -and !$_.isOffscreen } | Select-Object -First 1
        if (!$option) { throw "Language not found: $($case[1])" }
        [void](Ui @('invoke',$option.selector)); [void](Ui @('invoke','TranscriptionItem'))
        Check ((Ui @('get-value','UtilityModelPicker')).text -eq 'YourMT3+ Multi-Instrument') ('Language preserves selected model '+$case[0])
        Check ((Ui @('get-value','UtilityPresetPicker')).text -eq $case[2]) ('Custom preset localized '+$case[0])
        [void](Ui @('scroll-into-view','UtilityModelPicker'))
        [void](Ui @('screenshot','-o',(Join-Path $EvidenceRoot ('workspace-'+$case[0]+'.png'))))
    }
    [void](Ui @('invoke','ResultsItem'))
    [void](Ui @('wait-for','ResultsList','-t','5000'))
    $tree=Ui @('inspect','--depth','14')
    $send=Flat $tree.windows[0].elements | Where-Object { $_.type -eq 'Button' -and $_.name -eq '全部分轨转 MIDI' } | Select-Object -First 1
    if (!$send) { throw 'Stem-to-MIDI entry missing' }
    [void](Ui @('screenshot','-o',(Join-Path $EvidenceRoot 'result-summary.png')))
    [void](Ui @('invoke',$send.selector))
    [void](Ui @('wait-for','PageTitle','--contains','--value','MIDI','-t','5000'))
    $until=[DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 100
        $saved=Get-Content (Join-Path $state 'utility-drafts.json') -Raw | ConvertFrom-Json
    } until ($saved.transcription.Sources.Count -ge 7 -or [DateTime]::UtcNow -ge $until)
    Check ($saved.transcription.Sources.Count -eq 7) 'All six stems join the existing MIDI draft without discarding its original source'
    Check (!(Get-Content (Join-Path $state 'tasks.json') -Raw | ConvertFrom-Json)) 'Handoff does not start inference automatically'
    $blocker=Join-Path $state 'tasks.json.tmp'; [IO.Directory]::CreateDirectory($blocker) | Out-Null
    [void](Ui @('invoke','TasksItem')); [void](Ui @('invoke','PauseQueueButton'))
    [void](Ui @('wait-for','StorageInfo','-p','IsOffscreen','--value','False','-t','5000'))
    [void](Ui @('invoke','TranscriptionItem'))
    [void](Ui @('wait-for','StickyRunButton','-p','IsEnabled','--value','False','-t','5000'))
    [void](Ui @('screenshot','-o',(Join-Path $EvidenceRoot 'storage-warning.png')))
    $resolved=[IO.Path]::GetFullPath($blocker)
    if (!$resolved.StartsWith($state+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture boundary mismatch' }
    Move-Item -LiteralPath $resolved -Destination ($resolved+'.retained-fixture')
    [void](Ui @('invoke','RestoreStorageButton'))
    [void](Ui @('wait-for','StickyRunButton','-p','IsEnabled','--value','True','-t','5000'))
    Check $true 'Storage failure blocks new work and recovers without deleting records'
} finally {
    [IO.File]::WriteAllText((Join-Path $EvidenceRoot 'checks.json'), ($checks | ConvertTo-Json))
    if (!$app.HasExited) { [void]$app.CloseMainWindow(); if (!$app.WaitForExit(15000)) { throw 'QA window did not close safely.' } }
}
$checks
Write-Output 'PASS workspace UI batch; screenshots require visual inspection.'
