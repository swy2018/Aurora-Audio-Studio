# Aurora 2.1.0-beta.1 — implementation and acceptance

Status: Windows x64 and full source published as [2.1.0-beta.1](https://github.com/swy2018/Aurora-Audio-Studio/releases/tag/v2.1.0-beta.1). Stable remains 2.0.1. Mac Beta remains 2.0.2-beta.1 pending a native build.

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
- The ordinary-user path preserves identity. On the elevated path, the old token-based launch failed at CreateProcessWithTokenW with ERROR_ACCESS_DENIED (5), both with and without loading the profile. Replaced that operation with Explorer's desktop ShellExecute. The desktop must be in the same session and non-elevated; the dispatched child also rejects elevation without retrying.
- Local website headless checks passed at 1440 and 390 pixels, Chinese and English; all four screenshots were visually inspected, with no horizontal overflow in the changed workflow copy. Stable links stayed on 2.0.1 and offline fallback used the existing verified asset snapshot. Detector output was regex-only and is not a complete contrast audit. No live website deployment occurred.

Local evidence is retained under `.maintenance/release-2.1.0-beta.1/` and `output/playwright/aurora210-*`. Detailed tests also retain isolated temporary fixtures. Private machine paths and test audio are not release assets.

## Final release verification and limits

- Background-only acceptance was requested. Interactive score controls, file-picker cancellation/export retry, audio review, subtitle edit retention and narrow-window/theme/language switching are not claimed as natively tested in this Beta. Hosted installer launch is a separate check.
- [x] **Interactive UAC, 2026-11-27:** Fresh isolated baseline, elevated diagnostic probe, then a separate full `RunAs --after-update` run using the actual installer argument. Parent exit 0; the child was the expected executable, non-elevated, with matching SID, session, profile and LocalAppData. This is a real interactive handoff with the production guard, not an in-place install on the maintainer's machine. Alternate-account and no-shell scenarios were not exercised.
- [x] Final [hosted installer acceptance](https://github.com/swy2018/Aurora-Audio-Studio/actions/runs/45994395001) passed, including installed-window startup and uninstall retention. Commit `5b22e55393f7322cb8c6f6983d11e7330f283e3d`, artifact `15194674704`. Unsigned installer: 428,737,971 bytes, product 2.1.0-beta.1, file 2.1.0.0, SHA-256 `e9705cb2c2c64dfb9617697f51b55a315ab3fd91165b07b3f1f4a025971c34f7`.
- [x] Public prerelease `460559102`; tag verified against that commit. Source ZIP: 295 files, 13,684,515 bytes, SHA-256 `503b00a23240aa5b6b2a3db97d37d4af209cae0e43d010291fb4b9f88e8581a2`. The sizes and SHA-256 digests of both assets and both checksum files matched the uploaded assets.
- [x] Download snapshot exported from published releases; platform selection returns Windows Beta 2.1.0-beta.1, Mac Beta 2.0.2-beta.1, stable 2.0.1 for both. GitHub About updated.

Earlier CI artifact 81461dc was not distributed as this Beta. No on-device installer was run on the maintainer's machine.

Mac native UI, inference, signing, notarization and DMG acceptance require a Mac. Windows/shared-code tests do not satisfy these gates. Do not advertise an unbuilt Mac 2.1.0 package. No cleanup is authorized by this checklist.
