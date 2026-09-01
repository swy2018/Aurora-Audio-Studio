# ADR 0003: Formal model releases and feature grouping

Status: Accepted (2026-09-02)

Supersedes the Git development-branch rule in ADR 0001. The verified Hugging Face, PyPI, GitHub Release, registry, and fixed-file rules remain in force. Updated on 2026-09-02 to define a date-version fallback for upstreams without formal Releases.

## Context

Git-backed engines can be installed at a detached commit or configured with an SSH remote that Aurora cannot authenticate against. Comparing `HEAD` with a tracked development branch both fails for those installations and reports ordinary upstream commits that have not been formally released.

The flat Model Management list also mixes unrelated music, voice, separation, transcription, and subtitle components, making the catalog harder to scan as it grows.

## Decision

- Git-backed engines follow only the repository's latest public GitHub Release. Commits on `main` or another development branch do not trigger an update.
- Aurora compares the installed commit with the formal release tag through the GitHub API, so detached HEAD and SSH remote configuration do not affect checks.
- A repository with no formal Release uses the latest commit on its official default branch as a date version. Aurora displays the UTC commit date and records the exact commit SHA.
- Hugging Face models display the official `lastModified` date as their user-facing version and retain the exact snapshot SHA for comparison and installation.
- A confirmed update fetches the exact release tag over HTTPS and checks it out detached. Aurora refuses to continue when tracked local code changes could be overwritten.
- Hugging Face official model snapshots, stable PyPI package versions, GitHub Release assets, verified model registries, and pinned fixed files retain their existing official-source rules.
- Model Management groups the filtered result in the six product-feature categories: music generation, voice and cloning, singing conversion, stem separation, MIDI transcription, and subtitles and transcription.

## Consequences

Model checks favor stable published versions where they exist. ACE-Step reports v0.1.8 as current even when `main` has newer commits; Seed-VC has no formal Release, so it follows the dated, exact commit on its official default branch. Existing installed/default/optional filters continue to work inside the six feature groups.

HeartMuLa 3B, IndexTTS-2.5, SoulX-Singer-SVC, Qwen3-ASR 0.6B/1.7B, and Qwen3 ForcedAligner are optional model-management entries. Aurora never downloads them until the user confirms the installation dialog.
