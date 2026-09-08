# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows x64 and macOS Apple Silicon desktop

## Stack

Desktop applications built with .NET 10: WinUI 3 / Windows App SDK / WebView2 on Windows, Avalonia and the native WebView on macOS. Shared services preserve task, settings, project, and result behavior. Each shell owns platform installation and process orchestration. Embedded model tools render their local web interfaces inside Aurora.

## Users

Creators on Windows or Mac who want to produce music, voice, singing, stems, MIDI, and subtitles locally without assembling separate AI tools by hand. Windows NVIDIA workstations and Apple Silicon Macs are equally important audiences; requirements vary by engine.

## Product Purpose

Aurora Audio Studio provides one local desktop workspace for launching, installing, and operating a set of AI audio workflows. Success means users can move from a source file or creative prompt to an organized local output without managing command lines, ports, environments, or model folders themselves.

## Positioning

Aurora unifies independently installed local audio models behind a four-language desktop shell while keeping model execution, files, and outputs on the user's machine.

## Operating Context

- Runs on Windows 10/11 x64 and macOS 26+ Apple Silicon. The current Mac real-device acceptance machine runs macOS 27 beta; do not claim all OS versions were tested.
- Uses a configurable `LocalAI` root for models and tools.
- Organizes results into a configurable `AI工作流` output root.
- Supports system-adaptive Simplified Chinese, Traditional Chinese, English, and Japanese UI.
- Language selection applies and saves immediately, independently of unsaved storage settings. Japanese uses bundled Noto Sans JP and an adapted type scale.
- Does not start with Windows.
- Models download only after an explicit install action.
- Stable is the default application update channel. Beta is opt-in and includes later stable versions; changing back does not silently downgrade. Version 1.9.9 introduces channels, followed by a functionally identical 2.0.0-beta.1. The 2.0 final release requires testing first.
- Website, README, and repository About present Aurora 2.0 with equal platform visibility. Website product screenshots are actual Mac captures, not generated product proof; explicitly identify the 1.9.9 capture version as functionally identical to Beta 1.

## Capabilities and Constraints

- Music generation with ACE-Step 1.5 XL Turbo, plus optional on-demand MiniMax-Music3 with an isolated low-VRAM CUDA runtime.
- The current MiniMax-Music3 CUDA backend and Faster-Whisper XXL binary are excluded on Mac. Native Whisper provides Mac subtitles. Six management-only models retain the same non-inference scope on both platforms.
- AI voice creation with the default Qwen3-TTS 1.7B suite, plus optional Qwen3-TTS 0.6B and F5-TTS engines.
- Singing voice cloning with Seed-VC at 44.1 kHz.
- Two-stem vocals/instrumental separation with Vocals Revive V3e, six-stem separation with BS-RoFormer-SW, and optional Demucs 4 separation.
- Default piano MIDI transcription with TransKun V2, plus YourMT3+, legacy ByteDance Piano, and lightweight Spotify Basic Pitch options.
- Video subtitles with Subtitle Edit and Faster-Whisper, plus optional Small, Large v3 Turbo, and Large v3 CTranslate2 model packs.
- Batch media intake with drag and drop, built-in preview, quality presets, live task progress, persistent logs, pending-queue pause, and a unified Results library.
- Model selection, resumable on-demand deployment with disk checks and cancellation, model manager, output-folder selection, verified app updates, per-model update checks, VRAM release, diagnostics export, and four-language switching.
- Closing Aurora stops the backends it launched.
- A redesign must preserve every existing workflow, control, and product behavior unless the user explicitly approves a functional change.
- The approved A workbench and dark rounded A-wave icon remain the visual baseline; published screenshots are retained as historical previews.
- The existing model suite remains the default. Optional engines never install automatically and do not create maintenance warnings when absent.
- The installer defaults to Program Files, performs guarded in-place upgrades under the same AppId, and preserves user-owned data and the selected install location.
- Interactive uninstall offers an explicit personal-configuration cleanup choice without touching models, processing records, source media, or outputs.

## Brand Commitments

- Product name: Aurora Audio Studio.
- The requested redesign uses a white and light-green primary palette.
- The interface should feel modern, calm, professional, and distinctly human-made, without neon AI imagery, generic glass panels, purple gradients, or card-heavy template styling.

## Evidence on Hand

- Current implementation: `work/audio-studio/AuroraAudioStudio/`.
- Current app icon and artwork: `work/audio-studio/AuroraAudioStudio/Assets/`.
- Approved interface previews: `.impeccable/mocks/aurora-optimized-a-*.png`.
- No customer claims, benchmarks, pricing, or third-party endorsements are available and none should be invented.

## Product Principles

- Preserve local-first control and explicit user consent.
- Keep complex model operations approachable without hiding task state.
- Preserve familiar workflows while allowing a complete visual reorganization.
- Make frequent production actions faster to scan and reach.
- Prefer stable, responsive, low-overhead desktop interactions over decorative effects.

## Accessibility & Inclusion

The interface must remain usable at Windows display scaling, support keyboard focus and readable contrast, and preserve all four language layouts without clipping.
