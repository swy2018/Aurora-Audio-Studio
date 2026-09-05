param([switch]$Check)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$metadata = Get-Content -Raw -LiteralPath (Join-Path $repo 'docs/release.json') | ConvertFrom-Json
$version = [string]$metadata.version
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $metadata.notes.Count -ne 4) { throw 'Invalid release metadata' }
$utf8 = [Text.UTF8Encoding]::new($false)
function Sync-Text([string]$relative, [string]$text) {
    $path = Join-Path $repo $relative
    $text = $text.Replace("`r`n", "`n")
    $old = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllText($path).Replace("`r`n", "`n") } else { '' }
    if ($old -ceq $text) { return }
    if ($Check) { throw "Release metadata is out of sync: $relative" }
    [IO.File]::WriteAllText($path, $text, $utf8)
}
$projectPath = 'work/audio-studio/AuroraAudioStudio/AuroraAudioStudio.csproj'
$project = [IO.File]::ReadAllText((Join-Path $repo $projectPath))
$previous = [regex]::Match($project, '<Version>([^<]+)</Version>').Groups[1].Value
$project = [regex]::Replace($project, '<Version>[^<]+</Version>', "<Version>$version</Version>")
$project = [regex]::Replace($project, '<(FileVersion|AssemblyVersion)>[^<]+</\1>', "<`$1>$version.0</`$1>")
Sync-Text $projectPath $project
foreach ($relative in @('README.md','docs/index.html','work/audio-studio/README-给音乐人的使用说明.md','work/audio-studio/AuroraAudioStudio.iss')) {
    $text = [IO.File]::ReadAllText((Join-Path $repo $relative))
    if ($previous -ne $version) { $text = $text.Replace($previous, $version) }
    Sync-Text $relative $text
}
foreach ($button in @('download','changelog')) {
    $label = if ($button -eq 'download') { '下载' } else { '更新日志' }
    Sync-Text "docs/assets/readme-button-$button.svg" @"
<svg xmlns="http://www.w3.org/2000/svg" width="174" height="36" role="img" aria-label="$label $version"><rect width="174" height="36" rx="3" fill="#4b5150"/><path fill="#0abfa9" d="M92 0h79a3 3 0 0 1 3 3v30a3 3 0 0 1-3 3H92z"/><g font-family="Segoe UI,Arial,sans-serif" font-size="14" font-weight="600" text-anchor="middle"><text x="46" y="23" fill="white">$label</text><text x="133" y="23" fill="#082a25">$version</text></g></svg>
"@
}
$readme = [IO.File]::ReadAllText((Join-Path $repo 'README.md'))
foreach ($language in @('zh','en')) {
    $index = if ($language -eq 'zh') { 0 } else { 2 }
    $body = ($metadata.notes[$index] -split "`n" | ForEach-Object { $_ -replace '^• ', '- ' }) -join "`n"
    $pattern = '(?s)<!-- release-notes-' + $language + ':start -->.*?<!-- release-notes-' + $language + ':end -->'
    $replacement = "<!-- release-notes-${language}:start -->`n$body`n<!-- release-notes-${language}:end -->"
    $readme = [regex]::Replace($readme, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement })
}
$capabilityPath = Join-Path $repo 'docs/capabilities.json'
if (Test-Path -LiteralPath $capabilityPath) {
    $capabilities = [IO.File]::ReadAllText($capabilityPath) | ConvertFrom-Json
    if ($capabilities.version -ne $version) { throw 'Run the catalog export before synchronizing release metadata.' }
    $modes = @{ 'embedded-workbench'='嵌入式工作台 / Embedded'; 'native-task'='原生任务 / Native'; 'download-only'='仅下载管理 / Download only'; 'shared-runtime'='共享组件 / Runtime'; 'external-editor'='外部编辑器 / External editor' }
    $rows = @('| 模型 / Model | 操作入口 / Interface | 上游许可 / License |', '|---|---|---|')
    foreach ($model in $capabilities.models) { $rows += "| $($model.name) | $($modes[$model.mode]) | $($model.license) |" }
    $block = "<!-- model-capabilities:start -->`n<details>`n<summary>全部模型接入状态 / All model interfaces</summary>`n`n" + ($rows -join "`n") + "`n`n</details>`n<!-- model-capabilities:end -->"
    $readme = [regex]::Replace($readme, '(?s)<!-- model-capabilities:start -->.*?<!-- model-capabilities:end -->', [Text.RegularExpressions.MatchEvaluator]{ param($m) $block })
}
Sync-Text 'README.md' $readme
$note = "## $version — $($metadata.date)`n`n" + (($metadata.notes[0] -split "`n" | ForEach-Object { $_ -replace '^• ', '- ' }) -join "`n") + "`n`n" + (($metadata.notes[2] -split "`n" | ForEach-Object { $_ -replace '^• ', '- ' }) -join "`n") + "`n"
Sync-Text 'docs/release-notes.md' $note
$changelog = [IO.File]::ReadAllText((Join-Path $repo 'CHANGELOG.md')).Replace("`r`n", "`n")
# Keep historical release text intact; regenerate only the current entry.
$pattern = '(?ms)^## ' + [regex]::Escape($version) + '.*?(?=^## |\z)'
if ([regex]::IsMatch($changelog, $pattern)) { $changelog = [regex]::Replace($changelog, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $note + "`n" }) }
else { $changelog = [regex]::Replace($changelog, '(?m)^(# [^\n]+\n)', [Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Value + "`n" + $note + "`n" }, 1) }
Sync-Text 'CHANGELOG.md' $changelog
Write-Output "Release $version metadata verified."
