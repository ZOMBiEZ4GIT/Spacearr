# Contributing

- Open an issue before a large change so the scope is agreed.
- Backend: `dotnet test src/Spacearr.Tests`. Web: `cd web && npm test && npm run lint && npm run typecheck`. Both must pass; CI also runs a Playwright end-to-end smoke test (`cd web && npm run e2e`) and builds the Docker image.
- Write the test first where practical. New API routes are covered by the auth gate test automatically — it enumerates every registered route and fails if one is reachable without a session or API key.
- Keep the "nothing leaves your network" promise: no external fonts, images, analytics, or update checks. Fonts are bundled via `@fontsource`, not loaded from a CDN.
- State in your PR whether you used AI tools (see [AI-DISCLOSURE.md](AI-DISCLOSURE.md)). Either is fine; hiding it is not.
- Commit messages: imperative subject, a body that says why.
