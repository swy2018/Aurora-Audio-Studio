# Aurora 2.1.0-beta.1 — implementation and acceptance

Status: implementation and background regression complete; native Windows UI / elevated update handoff and release acceptance pending. **Not published.** This file is not a claim that every release gate has passed.

Baseline: `71de68dc70de41482e0e9a466f4a8b0e82e6ec75` (includes the previously accepted Mac Beta package documentation).

## Authorized scope

- Preserve subtitle edits through validation, cancellation and export errors; export completed artifacts without leaving misleading final files.
- Add explicit, backup-preserving settings/draft recovery and a safe Mac close-without-saving path.
- Restore ordinary-user privilege after Windows updates, including updates initiated by older clients.
- Close demonstrated UI exception/state gaps, validate expected separation outputs, and correct theme contrast.
- Verify bundled release inputs and extend behavioral regression coverage.
- Distinguish formal model releases, source revisions and model weights; do not call a PyPI-only check a complete update check.
- Validate a lighter Windows Basic Pitch ONNX runtime without replacing working user environments during development.
- Add editable MIDI-to-score preparation, MusicXML and arranged-MIDI export, range warnings and playback review while preserving source MIDI.

## Verified background results — 2026-11-27

- Full Windows behavior run: **385 PASS lines, exit 0**, including subtitle export, settings recovery, missing/truncated stems, canceled operations, source-update policy and score preparation. UpdateFlowTests passed separately. These are behavioral tests, not native UI automation.
- Shared Mac C# tests: **115 passed**; native Unix process checks are explicitly skipped on Windows. Existing WSL environment ran **35 Python tests** covering source policy, isolated environments and model lifecycle. No new model weights were downloaded for those tests.
- Windows 2.1.0-beta.1 compiled successfully, 0 errors; 11 existing CsWinRT1032 collection-expression warnings remain. Mac/shared compilation succeeded. A subsequent forced Mac restore with NuGet auditing enabled completed without the earlier NU1900 network warning; no dependency versions were changed.
- Four-language rows and placeholders passed; authored-string audit found 143 keys with no missing English translations and 576 complete workbench translation entries. Catalog export contains 27 components. Version synchronization and website release-policy tests passed.
- Basic Pitch isolated ONNX first installation passed: Python 3.11, 38 compatible packages, real ONNX model load, `pip check`, no TensorFlow. Original official wheel SHA-256: `738adb503aae7fdfc7d1e1511aa0ce35052315f260a19531ef4c356708425db0`. **41 upstream code/model/license files were byte-identical**, and all 44 derived-wheel RECORD entries were validated.
- Basic Pitch real backend acceptance at 18:39 +08:00: 9.6-second input, `cpu (ONNX)`, valid output and result-library registration. TensorFlow/ONNX comparison on the same short fixture produced 12 matching notes with 0-second onset/end difference; maximum tensor difference was `8.6427e-7`. This is one fixture, not a general quality or speed guarantee. Existing installed model environments were not replaced.
- Score tests: six MIDI inputs, 18 original notes, 18 grid/meter combinations, tempo changes, voices, ties, percussion, accidentals, triplets, opt-in simplification, range warnings, exact original-byte preservation, malformed-input and cancellation checks. MusicXML 4.0 XSD validation and independent Verovio 6.3.0 rendering passed; rendered six-part and triplet pages were visually inspected. Reference WAV and arranged MIDI passed structural checks. Native player interactions and professional musical accuracy remain separate checks.
- Live source resolver selected `bs-roformer-infer v0.1.5` at `244cddd4f7611956eb1cc4958e82b65b4891c019` and `mt3-infer v0.2.0` at `2d20ee5bb6ca727968bd23c6100fd2a35154166b`. Newer HEAD is not treated as a formal release. Mock regressions cover newer local packages, manual VCS installs, diverged source and malformed/prerelease targets. Full fresh Git-source installation of these two engines was not performed in this round.
- Windows bundled-tool verification passed for 451 files plus the copied manifest; unknown files and reparse points are rejected. Mac version/architecture policy tests passed, and the real pinned public certifi package/hash/license were verified. This does not prove native Mac tool execution or final signing.
- Dedicated ordinary-user restart probe retained its non-elevated SID/profile. Static independent review confirmed the startup guard precedes settings and the mutex, and legacy rollback omits automatic restart. **Elevated/same-account and alternate-account UAC handoffs remain untested.** The latter may fail closed and require manual reopening; do not claim universal automatic relaunch.
- Local website headless checks passed at 1440 and 390 pixels, Chinese and English; all four screenshots were visually inspected, with no horizontal overflow in the changed workflow copy. Stable links stayed on 2.0.1 and offline fallback used the existing verified asset snapshot. Detector output was regex-only and is not a complete contrast audit. No live website deployment occurred.

Local evidence is retained under `.maintenance/release-2.1.0-beta.1/` and `output/playwright/aurora210-*`. Detailed tests also retain isolated temporary fixtures. Private machine paths and test audio are not release assets.

## Remaining release gates

- Background-only acceptance was requested. Interactive score controls, file-picker cancellation/export retry, audio review, subtitle edit retention and narrow-window/theme/language switching are not claimed as natively tested in this Beta. Hosted installer launch is a separate check.
- Interactive UAC is explicitly deferred; no prompt on the maintainer's desktop. Elevated and alternate-account handoff are unverified, not release-test passes. Use a dedicated fixture for future testing.
- [ ] Build the delivery from a fixed, rechecked commit and validate the actual installer/payload/version/hash; installer acceptance must not overwrite the maintainer's installed app.
- [ ] Publish Windows and source, verify remote sizes/digests and preserve Stable Latest. Mac 2.1.0 packaging remains a separate Mac task.
- [ ] Export the download snapshot from real uploaded assets, verify Pages and online platform/channel selection, and update GitHub About if required. Local README, website text, About/release metadata and changelog are prepared but not yet pushed.

Read-only remote check still showed main `71de68dc70de41482e0e9a466f4a8b0e82e6ec75`, latest stable `v2.0.1` and latest available Beta `v2.0.2-beta.1`. No commit/tag/push/Release upload or local application installation has been performed for 2.1.0-beta.1.

Mac native UI, inference, signing, notarization and DMG acceptance require a Mac. Windows/shared-code tests do not satisfy these gates. Do not advertise an unbuilt Mac 2.1.0 package. No cleanup is authorized by this checklist.
