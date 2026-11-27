# ADR 0006: Verified source updates and non-destructive score preparation

Status: Accepted for implementation; native release acceptance is tracked separately.
Date: 2026-11-27

## Context

BS-RoFormer and YourMT3 maintenance no longer reaches PyPI. Their GitHub repositories still have formal releases, so default-branch activity must not become an automatic stable upgrade. Windows Basic Pitch 0.4.0 supports ONNX, but its Python 3.11 dependency metadata requires TensorFlow. Raw automatic MIDI transcription can contain dense overlaps and is not a performance-ready score.

## Decisions

- Resolve those two packages from official GitHub releases and install the exact release commit. Use a dated commit only when the repository has no formal release. Show unreleased maintenance revisions separately. Track code and weights independently. Compare installed source commits, including standard `direct_url.json` receipts; do not offer downgrades or automatically replace diverged sources.
- Keep Python 3.11 on Windows. Verify the official Basic Pitch 0.4.0 wheel hash and derive `0.4.0+aurora.onnx.1`, changing distribution metadata and RECORD only. Preserve all upstream Python, model and license bytes. Require ONNX Runtime and validate the complete dependency graph with `pip check`. Existing TensorFlow environments remain usable until the user chooses maintenance. A newer upstream release requires renewed compatibility acceptance rather than assuming this profile applies.
- Add shared MIDI score preparation, not a new acoustic model or a full notation editor. Preserve original MIDI; export MusicXML 4.0, arranged MIDI, reference-tone audio and an arrangement receipt to a new folder. Quantization, tempo override, meter, key, first-beat offset and instrument/part choices are explicit. Melody/chord simplification is opt-in, and range warnings never silently transpose notes. The score is concert pitch. Source expressive controls remain in the original MIDI; one selected meter is used for the arranged score.
- Stage exports before promotion. Validate structure and independently render representative scores, including voices, ties, percussion, accidentals and triplets. Musical accuracy and final performance engraving still require human review.
- Verify every Windows bundled runtime file against pinned distribution bytes and reject untracked payloads. On Mac, lock accepted tool versions/architecture and bundle a pinned public certifi CA source rather than the builder's trust store. Developer ID signing changes native hashes after the recorded input-verification stage; final DMG hashes remain separate.
- Updated Windows installers request an ordinary-user restart. An early startup guard prevents legacy elevated updater processes from opening a privileged workspace. Old rollback targets lacking the guard use silent installation without automatic relaunch. UAC acceptance uses the dedicated probe, never a real Aurora process whose isolated-data environment would be discarded during user-token handoff.

## Consequences and validation

GitHub availability is now necessary to verify the two source-managed packages. Failed or ambiguous metadata is unverified, not current. Source install candidates are activated only after validation. Basic Pitch ONNX reduces installation dependencies; it does not claim improved transcription accuracy. Score size and reference-audio length are bounded, and incomplete canceled exports retain `.partial` status. Mac native UI, inference, signing and notarization remain Mac-only gates.

See [2.1.0-beta.1 acceptance](../validation-2.1.0-beta.1.md) and [Mac handoff](../macOS-2.1.0-beta.1-handoff.md).
