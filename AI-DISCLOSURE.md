# AI disclosure

Spacearr is developed with AI assistance (Claude, by Anthropic). This file says exactly how, following the disclosure format the Lemmy selfhosted community adopted in 2026.

**Phase and level**

| Phase | Level |
|---|---|
| Design | Pair: the maintainer set the mission and scope; the model researched the ecosystem and drafted the spec, which the maintainer approved. |
| Implementation | Generated: most code was written by the model from a written plan with tests first. |
| Testing | Generated with human review: unit, integration and end-to-end tests are written alongside the code and run in CI. |
| Documentation | Generated with human review. |
| Review | AI + human: every task runs through an independent AI reviewer pass before being merged; the maintainer reviews and tags every release, not every commit. |
| Deployment | Human: releases are tagged by the maintainer. |

**What that means for you**

- The auth gate test (`Spacearr.Tests/Auth/AuthGateTests.cs`) enumerates every API route registered in the app and fails the build if any route other than `/api/v1/system/status`, `/api/v1/setup` and `/api/v1/auth/login` is reachable without a session or API key.
- No telemetry exists in the code, and the image makes no outbound calls except to your own Radarr/Sonarr.
- Security reports are answered by a person; see [SECURITY.md](SECURITY.md).
- Pull requests are welcome from people and from people using AI tools. Say which in the PR description. PRs without tests, or that cannot explain what they change, are closed.
