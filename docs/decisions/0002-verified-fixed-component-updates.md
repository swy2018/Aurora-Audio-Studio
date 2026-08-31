# ADR 0002: Verified self-update adapters for fixed components

Status: Accepted (2026-09-01)

Supersedes the fixed-component manual-update decision in ADR 0001. The Hugging Face, Git, and PyPI rules from ADR 0001 remain in force.

## Context

Aurora previously detected five locally bundled or fixed components but could not update them: BS-RoFormer, YourMT3+, ByteDance Piano, Faster-Whisper XXL, and Subtitle Edit. Reporting these as manual made Model Management incomplete and forced non-technical users to understand each upstream distribution format.

Updates must remain opt-in, preserve the active version until verification succeeds, and use the component's real upstream rather than an Aurora mirror.

## Decision

- BS-RoFormer and YourMT3+ compare installed package metadata with the official PyPI JSON API and upgrade their existing isolated environments.
- BS-RoFormer model-registry weights are installed through the package's downloader, which verifies the registry SHA-256 values. The dedicated Vocals Revive V3e entry provides vocals/instrumental two-stem separation; the existing SW entry remains the multi-stem option.
- The archived ByteDance Piano checkpoint is repaired from its original Zenodo record and accepted only when its pinned SHA-256 is C3FA9730725BF4A762F1C14BC80CD5986EACDA01B026F5A4A2525CD607876141.
- Subtitle Edit follows the latest stable GitHub Release, selects the Windows x64 portable ZIP, verifies GitHub's SHA-256 digest, stages the replacement, and preserves Settings.json.
- Faster-Whisper XXL follows the highest Windows archive on its named public GitHub Release. Aurora verifies the immutable asset size and uses a GitHub-provided SHA-256 digest when available. The active runtime remains recoverable through the model transaction.
- Selecting an uninstalled workbench model or processing engine starts the same explicit, location-aware installer. No model downloads merely because Aurora starts.

## Consequences

All catalog components now expose an automatic check, install, repair, or update path. The UI no longer groups five components as permanently manual. Package formats may contain a single wrapper directory, so the staged installer normalizes that directory before marker validation and commit. Large downloads remain user-confirmed and all prior models remain available.