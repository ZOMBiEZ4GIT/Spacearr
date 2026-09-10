# Spacearr — TODO

The fork-era phase checklist that used to live here (Phases 0–10, dating from the March 2026 Radarr fork) is retired along with that codebase — see `docs/DEVLOG.md`'s 2026-09-10 entry. What follows is the v1 launch checklist, from the standalone spec's "Launch deliverables" (`docs/superpowers/specs/2026-09-10-spacearr-standalone-design.md`).

## v1 launch checklist

- [x] README with screenshots, compose block, "what it does not do."
- [x] `AI-DISCLOSURE.md`: built with Claude under human direction and review; how review happens.
- [x] `SECURITY.md` with a reporting channel; the auth-gate test in CI.
- [x] GitHub Actions: build + test on PR (`.github/workflows/ci.yml`); multi-arch image on tag (`.github/workflows/release.yml`).
- [x] `docs/` user guide: install, connections and paths, heat explained, actions explained, FAQ.
- [x] Unraid CA template XML in `docker/unraid/` — written; Community Applications submission is still pending.

## Considered for v1.1

- Rules: scheduled or condition-based actions, so cleanup can run without a person clicking through each item.
- Bulk replace: queue a Replace across many items at once, the way duplicates' "Keep this" already queues bulk deletes.
- Notifications: a webhook or push when a scan finds something worth acting on, or when an action fails.
- Windows service / macOS launchd packaging, so "from source" isn't the only non-Docker option.
- Reverse-proxy sub-path support, so Spacearr can live at `/spacearr/` behind a shared domain instead of needing its own subdomain or port.
