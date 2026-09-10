# Changelog

All notable changes to this project are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Spacearr began as a Radarr fork; 0.1.0 is the first standalone release — the fork's history remains in git.

## [Unreleased]

### Fixed

- Scanning a large library no longer fails most probes with "No file descriptors available". Each ffprobe run leaked its stdout/stderr pipes until a garbage collection, so under Docker's default 1024 open-file limit a ~40k-file library had 97% of its files marked unreadable. The pipes are now closed when each probe returns.
- The treemap no longer returns a 500 when a file ffprobe couldn't read gets its own block. Unreadable heat is sent as `-1`, the same as the library list, instead of `NaN`, which can't be written as JSON.
- The Movies / TV filter on the Library and Duplicates pages works. `?kind=movie` returned 400 because enum query binding was case-sensitive while the API writes `movie`/`episode`.
- Logs are a fraction of the size. EF Core SQL, per-request ASP.NET Core pipeline messages and per-call HttpClient messages now log at Warning; the one-line request summary is unchanged.

## [0.1.0] - 2026-09-11

### Added

- Canvas treemap of the whole library: movies as blocks, series → season → episode drill-down, posters rendered under the heat colour on large blocks, smooth zoom and keyboard navigation.
- Bitrate heat: colour by bits per pixel per frame, normalised per codec, with relative mode (ranks within your library) and absolute mode (fixed thresholds).
- Library table and detail panel: virtualised table, per-file facts, arr facts, and an open-in-Radarr/Sonarr link.
- Connections and path mapping for any number of Radarr and Sonarr instances, with connection testing, quality-profile caching, and mapping suggestions.
- Scan and ffprobe: incremental scan with parallel probing, orphan cleanup, and a scheduler (default every 6 hours).
- Duplicates across instances, ranked by wasted bytes, with a keep-this-one action.
- Preview-first actions (delete, replace with a smaller release) through the arr apps' own APIs, gated by a signed confirm token that expires after 10 minutes, with a full action log.
- Posters proxied and cached locally so the browser never calls TMDB or TheTVDB.
- First-run wizard: connect Radarr/Sonarr, map paths, run the first scan.
- Docker image with a PUID/PGID/UMASK entrypoint and a docker-compose example.
- CI covering backend, web and end-to-end tests, plus a release workflow publishing multi-arch images.

### Security

- Authentication mandatory on every API route except the version/status check and the login/setup endpoints (`GET /api/v1/system/status`, `POST /api/v1/setup`, `POST /api/v1/auth/login`), plus the SPA shell and its static assets, enforced in CI by a test that enumerates every registered route.
- Arr API keys encrypted at rest with AES-256-GCM, key stored in `/config/secret.key` (mode 0600), never returned by the API.
- Login lockout: five failed sign-ins within a minute locks out that account, and separately that source address, for a minute each.
- Request bodies capped at 1 MiB.
- Data-protection session keys persisted to disk so sessions survive a restart.
- Container runs as the PUID/PGID user, not root.

### Known limitations

- It does not transcode.
- It does not delete from disk itself.
- It does not talk to Plex, Jellyfin or Emby, and does not know what has been watched.
- It has no rules engine and never acts on its own.
- No automatic or scheduled deletions.
- No bulk replace.
- No sub-path reverse proxy (root only).
- No UNC path support on Windows hosts.
- It trusts the Radarr/Sonarr URLs you give it, including LAN addresses, without blocking or warning on them.
- The poster cache under `/config/posters` is never pruned in v1.
- The image is about 385 MB.

[0.1.0]: https://github.com/ZOMBiEZ4GIT/Spacearr/releases/tag/v0.1.0
