# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop

## Stack

Windows desktop application built with .NET 10, WinUI 3, Windows App SDK, and WebView2. The native shell owns navigation, model management, settings, task status, updates, and local process orchestration. Embedded model tools render their local web interfaces inside Aurora.

## Users

Chinese-speaking Windows creators who want to produce music, voice, singing, stems, MIDI, and video subtitles locally without assembling separate AI tools by hand. The primary use is a personal production workstation with an NVIDIA GPU.

## Product Purpose

Aurora Audio Studio provides one local desktop workspace for launching, installing, and operating a set of AI audio workflows. Success means users can move from a source file or creative prompt to an organized local output without managing command lines, ports, environments, or model folders themselves.

## Positioning

Aurora unifies independently installed local audio models behind one bilingual desktop shell while keeping model execution, files, and outputs on the user's machine.

## Operating Context

- Runs on Windows as a desktop application.
- Uses a configurable `LocalAI` root for models and tools.
- Organizes results into a configurable `AI工作流` output root.
- Supports system-adaptive Simplified Chinese, Traditional Chinese, English, and Japanese UI.
- Does not start with Windows.
- Models download only after an explicit install action.

## Capabilities and Constraints

- Music generation with ACE-Step 1.5 XL Turbo.
- AI voice creation with the default Qwen3-TTS 1.7B suite, plus optional Qwen3-TTS 0.6B and F5-TTS engines.
- Singing voice cloning with Seed-VC at 44.1 kHz.
- Six-stem separation with BS-RoFormer-SW, plus optional general-purpose Demucs 4 separation.
- Multi-instrument MIDI transcription with YourMT3+ and a dedicated piano model, plus optional lightweight Spotify Basic Pitch transcription.
- Video subtitles with Subtitle Edit and Faster-Whisper, plus optional Small, Large v3 Turbo, and Large v3 CTranslate2 model packs.
- Batch media intake with drag and drop, built-in preview, quality presets, live task progress, persistent logs, pending-queue pause, and a unified Results library.
- Model selection, resumable on-demand deployment with disk checks and cancellation, model manager, output-folder selection, verified app updates, per-model update checks, VRAM release, diagnostics export, and four-language switching.
- Closing Aurora stops the backends it launched.
- A redesign must preserve every existing workflow, control, and product behavior unless the user explicitly approves a functional change.
- The approved A workbench and dark rounded A-wave icon remain the visual baseline for version 1.3.0.
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
