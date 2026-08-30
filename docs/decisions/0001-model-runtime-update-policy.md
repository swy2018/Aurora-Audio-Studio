# ADR 0001: Model runtime and update ownership

Status: Accepted for Aurora Audio Studio 1.4.0 (2026-08-30)

## Decision

Aurora classifies model components by the source it can verify and update safely:

- Hugging Face weights compare repository revisions and install through a staged directory switch.
- Git checkouts compare the tracked upstream commit and update with fast-forward-only pulls.
- PyPI tools live in isolated environments, compare installed package metadata with PyPI, and upgrade only after confirmation.
- Fixed runtime bundles and direct model files report their real manual upgrade owner instead of claiming to be current.

MiniMax-Music3 uses its own isolated CUDA environment and downloads only the Diffusers component paths required by Aurora. TransKun V2 uses its own PyPI environment. Neither is bundled or installed without an explicit user action.

## Consequences

The Model Management status is honest about what Aurora can automate. Large or license-sensitive models remain opt-in, and an interrupted Hugging Face weight update does not replace the active model directory.
