# ADR 0004: Verified workflows and explicit localization

Status: Accepted for 1.9.0 (2026-09-05)

Supersedes the in-place Python upgrade details in ADR 0002. The formal-release and dated-snapshot policy in ADR 0003 is unchanged.

## Context

Files present and an open HTTP port did not prove that an engine could produce usable output. Moving a Windows virtual environment also broke console launchers containing absolute paths. Visual-tree translation overwrote WinUI template bindings, leaving navigation in a different language from page content. Language selection required a separate Save action far below the selector.

## Decision

- Retain every active task regardless of completed-history limits. After restart, unfinished work is retryable with its saved source language, model, preset and track mode. This is a new inference run, not a mid-inference resume.
- Accept successful outputs only through explicit task manifests or workbench completion receipts. Validate WAV data, nonempty MIDI note events, and SRT time ranges before adding results. Never infer ownership from directory modification times.
- Install Python candidates at permanent versioned paths; activate a small logical pointer only after validation. Retain previous directories and environments. Same-revision staging can resume; different revisions remain isolated.
- Resolve Seed-VC's CUDA trio and UI dependencies in one transaction. Do not install Resemblyzer, which is used by upstream evaluation scripts but not the shipped singing entry point. Run dependency and import checks before activation.
- Use attached localization keys only on authored XAML. Never overwrite framework template children. Set Windows App SDK's process-local language preference for new native controls.
- Save language selection immediately and independently of the settings form's draft paths. Preserve a connected workbench and its inputs across navigation, provided the owning engine PID still matches.
- Inject translations only into Aurora's local workbench ports. Do not translate editable text, code, paths, or model identifiers. Localize known output status fields and dropdown display labels without changing their underlying model values. Raw diagnostic logs and Windows-owned file pickers retain their original language.
- Bundle unmodified Noto Sans JP under OFL-1.1. Japanese has an explicit font and title/body scale; other languages keep their native Windows families. WebView2 exposes only the font directory through a local virtual hostname.
- Detach a MediaPlayer from its MediaPlayerElement before release. The element closes players it created; disposing one while it remains attached can produce fatal native callbacks.
- `docs/release.json` owns current release notes and version; the client catalog generates `docs/capabilities.json`. CI checks both public surfaces for drift.

## Consequences

Model Center distinguishes files present from a successfully completed short task. Verification is invalidated when model/runtime identity changes. Optional models not exercised on the acceptance machine remain explicitly unverified.

`AURORA_DATA_ROOT` is an absolute-path override for isolated testing. It redirects settings, logs, queue, WebView2 state and default output paths; it is not a second production configuration source. Tests must not point it at a user's real profile.

References: [Windows App SDK language override](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.globalization.applicationlanguages.primarylanguageoverride), [MediaPlayer lifecycle](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/play-audio-and-video-with-mediaplayer), [Noto Sans JP license](https://github.com/google/fonts/blob/main/ofl/notosansjp/OFL.txt).
