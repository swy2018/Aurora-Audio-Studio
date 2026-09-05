param([Parameter(Mandatory)][string]$AceRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$tools = Join-Path $repo 'work/audio-studio/AuroraAudioStudio/Tools'
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class AuroraTraditional {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
 private static extern int LCMapStringEx(string locale, uint flags, string source, int length, StringBuilder target, int capacity, IntPtr version, IntPtr reserved, IntPtr sort);
 public static string Convert(string value) {
   var output = new StringBuilder(value.Length * 2 + 1);
   if (LCMapStringEx("zh-CN", 0x04000000, value, value.Length, output, output.Capacity, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0) throw new InvalidOperationException("Traditional Chinese conversion failed");
   return output.ToString();
 }
}
'@
function Flatten($value, [string]$prefix, $result) {
    foreach ($key in $value.Keys) {
        $path = if ($prefix) { "$prefix.$key" } else { $key }
        if ($value[$key] -is [string]) { $result[$path] = $value[$key] }
        elseif ($value[$key] -is [Collections.IDictionary]) { Flatten $value[$key] $path $result }
    }
}
$flat = @{}
foreach ($language in @('zh','en','ja')) {
    $flat[$language] = @{}
    $file = Join-Path $AceRoot "acestep/ui/gradio/i18n/$language.json"
    Flatten ([IO.File]::ReadAllText($file) | ConvertFrom-Json -AsHashtable) '' $flat[$language]
}
$translations = [ordered]@{}
foreach ($key in @($flat.en.Keys | Sort-Object)) {
    $en = $flat.en[$key]; $zh = $flat.zh[$key]; $ja = $flat.ja[$key]
    if (!$en -or !$zh -or !$ja) { continue }
    $translations[$en] = @($zh, [AuroraTraditional]::Convert($zh), $en, $ja)
}
$extra = [IO.File]::ReadAllText((Join-Path $tools 'workbench-i18n-extra.json')) | ConvertFrom-Json -AsHashtable
foreach ($key in $extra.Keys) { $translations[$key] = $extra[$key] }
[IO.File]::WriteAllText((Join-Path $tools 'workbench-i18n.json'), ($translations | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Output "Generated $($translations.Count) four-language workbench entries."
