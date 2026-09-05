# Aurora 1.9.0 acceptance scope

Test date: 2026-09-05. Platform: Windows 11 x64, NVIDIA RTX 5080 (16 GB), .NET 10 / WinUI 3 / WebView2.

This record distinguishes source tests, real short inference runs, and visible UI checks. It is not a claim that every optional model or every machine has been tested.

## Real outputs

The acceptance harness invokes Aurora's production backend, task queue, project registration and output validators. Creative generation uses the real Gradio callbacks exposed in the embedded workbench. Source audio for cloning is a synthetic test voice, not an impersonation sample.

| Workflow | Tested engine | Result |
|---|---|---|
| Music | ACE-Step 1.5 XL Turbo | 10-second piano WAV, completion receipt, Results registration; PyTorch CUDA backend with staged offloading |
| Voice cloning | Qwen3-TTS 1.7B Base | Reference audio and transcript → new WAV; also submitted through the visible translated UI |
| Preset voice | Qwen3-TTS 1.7B CustomVoice | Text → WAV, receipt and Results registration |
| Voice design | Qwen3-TTS 1.7B VoiceDesign | Description and text → WAV, receipt and Results registration |
| Singing conversion | Seed-VC 44.1 kHz | Source/reference audio → completed WAV; also passed in a newly created isolated CUDA environment |
| Stem separation | BS-RoFormer Vocals Revive V3e | Vocals and instrumental WAV files registered to their task |
| Piano MIDI | TransKun | Generated piano audio → MIDI with actual note-on events; an earlier empty MIDI from a sine-wave input was correctly rejected |
| Subtitles | Faster-Whisper XXL + Large v3 Turbo | Test video → nonempty timed SRT and JSON on CUDA |

The fresh Seed-VC test reused downloaded checkpoint copies, but created a new Python environment. Its final dependency check reported 154 compatible packages. This is not a fresh offline-PC or fresh-network-download test.

## Automated and UI checks

- The 1.9.0 installer candidate completed an in-place Program Files upgrade with exit code 0 and no restart. Its installed files matched that candidate build byte-for-byte, and all 19 installed native-navigation/language assertions passed. The final package additionally corrects license-label localization and distinguishes F5-TTS code and weight licenses; those data-only changes passed background regression checks, without another interactive installation.
- Real inference and embedded-workbench checks above used the development build with isolated user data. The installed build received native UI and binary-equivalence checks; an additional installed-build inference run was not performed.
- 38 behavior checks cover restart recovery, retained active tasks, cancellation, safe mode, explicit manifests, installer staging and rollback, runtime identity, invalid MIDI/SRT, shared dependencies, immediate language persistence, and license metadata.
- Authored XAML localization audit covers 118 keys. Every native translation entry provides all four language values.
- Native navigation checks cover the six feature pages, Model Center, Task Center, Results, Maintenance, Settings and About.
- Simplified Chinese, Traditional Chinese, English and Japanese switch without using the Save button; Japanese switches back to Simplified Chinese. Language switching preserves other settings and the connected workbench's text inputs.
- Qwen, Seed-VC and ACE workbenches were inspected as real WebView2 pages, not empty HTTP responses. Japanese's bundled font was confirmed loaded in the browser, and native/embedded screenshots were reviewed at desktop and minimum-window sizes.
- Audio preview playback advanced its time slider. Three consecutive play/close cycles passed after correcting player detachment. The export folder picker opened without the earlier native crash, and the exported WAV matched its source SHA-256 exactly.
- Website desktop/mobile layout, language switching, keyboard tabs, menu state, image preview/Escape and blocked-localStorage handling passed. All 27 capability entries loaded; checked viewports had no horizontal overflow or broken images. Existing marketing screenshots were not replaced.

## Limits and honest states

- “Files present” is not inference acceptance. “Short task verified” records a successful task for the model/runtime identity on that computer; changing the identity invalidates it.
- Optional MiniMax, F5-TTS, YourMT3+, ByteDance Piano, Demucs, and other unexercised entries are not covered by the table above. Download-only entries do not expose a runnable workbench. The [catalog](capabilities.json) records exact integration modes and upstream licenses.
- MIDI playback/editing requires an external music application. Aurora can report note counts, export MIDI and open it externally; it does not bundle a MIDI synthesizer or DAW.
- Task retry restarts inference from saved inputs and parameters. Download continuation is limited to the same model revision and upstream transfer support.
- Models are not included in the installer. Model downloads require confirmation, disk space and access to their official upstreams. Existing user settings, models, source media and results are preserved.
- Raw engine logs, paths, model identifiers and Windows-owned dialogs can retain their original language. The original marketing screenshots are intentionally retained and labeled historical.
- The Windows installer is unsigned. Windows may show an unknown-publisher or SmartScreen warning.

## Reproducing source checks

```powershell
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release -- --strings .
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release -- --catalog . --check
dotnet run --project work/audio-studio/AuroraAudioStudio.UpdateFlowTests -c Release -- work/audio-studio/AuroraAudioStudio.iss
pwsh -NoProfile -File work/audio-studio/tools/sync-release.ps1 -Check
```

Real-model tests are explicitly opt-in and excluded from CI. Use a separate absolute `AURORA_DATA_ROOT` for native acceptance, never a real user's profile. See [ADR 0004](decisions/0004-verified-workflows-and-localization.md).
