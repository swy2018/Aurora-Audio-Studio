param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$Sample,
      [Parameter(Mandatory)][string]$EvidenceRoot, [switch]$TestPicker)
$ErrorActionPreference = 'Stop'
$Sample = [IO.Path]::GetFullPath($Sample)
if (!(Test-Path -LiteralPath $Sample -PathType Leaf)) { throw 'Provide an existing, non-private test audio file.' }
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
function Ui([string[]]$Arguments) {
    $raw = & winapp ui @Arguments --json
    $code = $LASTEXITCODE
    $value = $raw | ConvertFrom-Json
    if ($code -ne 0 -or ($value -isnot [array] -and $value.error)) { throw ($raw -join [Environment]::NewLine) }
    return $value
}
function Test([string]$Name, [scriptblock]$Body) {
    try { & $Body; $results.Add([pscustomobject]@{name=$Name;status='PASS'}) }
    catch { $results.Add([pscustomobject]@{name=$Name;status='FAIL';detail=$_.Exception.Message}) }
}
function Navigate([string]$Item) {
    [void](Ui @('invoke',$Item,'-a',"$AppPid"))
    Start-Sleep -Milliseconds 250
}
function HasSample {
    $found = Ui @('search',[IO.Path]::GetFileName($Sample),'-a',"$AppPid")
    if ($found.matchCount -lt 1) { throw 'Source file is not visible.' }
}
Navigate SeparationItem
[void](Ui @('wait-for','UtilityModelPicker','-a',"$AppPid",'-t','5000'))
if ($TestPicker) {
    Test 'Original file picker still imports audio' {
        [void](Ui @('invoke','添加素材','-a',"$AppPid"))
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $picker = Ui @('list-windows','-a',"$AppPid") | Where-Object { $_.className -eq '#32770' -and $_.ownerHwnd -ne 0 } | Select-Object -First 1
            if (!$picker) { Start-Sleep -Milliseconds 200 }
        } until ($picker -or [DateTime]::UtcNow -ge $deadline)
        if (!$picker) { throw 'Owned file picker did not appear.' }
        try {
            # The combo and its Edit child share AutomationId 1148. Use the exact Edit slug.
            $tree = Ui @('inspect','1148','-w',"$($picker.hwnd)",'--depth','5')
            $edit = $tree.windows.elements.children | Where-Object type -eq 'Edit' | Select-Object -First 1
            if (!$edit) { throw 'File-name Edit control is missing.' }
            [void](Ui @('set-value',$edit.selector,$Sample,'-w',"$($picker.hwnd)"))
            if ((Ui @('get-value',$edit.selector,'-w',"$($picker.hwnd)")).text -ne $Sample) { throw 'File name did not commit.' }
            [void](Ui @('click','打开(O)','-w',"$($picker.hwnd)"))
            Start-Sleep -Milliseconds 500
            HasSample
        } finally {
            $open = Ui @('list-windows','-a',"$AppPid") | Where-Object hwnd -eq $picker.hwnd
            if ($open) { [void](Ui @('invoke','取消','-w',"$($picker.hwnd)")) }
        }
    }
}
Test 'Draft source is restored' { HasSample }
$mode = (Ui @('get-value','UtilityTrackModePicker','-a',"$AppPid")).text
Navigate TasksItem
Navigate SeparationItem
Test 'Navigation retains source and stem mode' {
    HasSample
    if ((Ui @('get-value','UtilityTrackModePicker','-a',"$AppPid")).text -ne $mode) { throw 'Stem mode changed during navigation.' }
}
Test 'Run action remains enabled for restored valid audio' {
    $state = Ui @('wait-for','RunUtilityButton','-a',"$AppPid",'-p','IsEnabled','--value','True','-t','5000')
    if (!$state.found) { throw 'Run button is disabled.' }
}
[void](Ui @('screenshot','-a',"$AppPid",'-o',(Join-Path $EvidenceRoot 'draft-ui.png')))
$results | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceRoot 'ui-results.json')
$results | Format-Table name,status -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count) { exit 1 }
