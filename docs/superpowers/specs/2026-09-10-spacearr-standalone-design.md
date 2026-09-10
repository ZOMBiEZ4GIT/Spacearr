# Spacearr v1: standalone design

Date: 2026-09-10
Supersedes: `2026-03-29-spacearr-design.md` (the Radarr-fork design)
Research basis: `docs/research/2026-09-10-arr-ecosystem-research.md`

## Mission

**The cooler looking WinDirStat for your arr stack.**

Spacearr scans the media library behind one or more Sonarr and Radarr instances, draws it as a treemap where block size is bytes and colour is bitrate heat, and lets the user reclaim space through the arr apps' own APIs. It is a visualisation tool first. Actions are a thin, preview-first layer on top.

## What it is not

- Not a media manager. It never downloads, imports, or renames.
- Not a transcoder. It never rewrites media files.
- Not a Plex/Jellyfin/Emby tool. It has no media-server dependency; watch-state cleanup is Maintainerr's job.
- Not a rules engine in v1. No scheduled or automatic deletions.
- Not a fork. It talks to Sonarr and Radarr over their v3 HTTP APIs only.

## Architecture

One process, one container, one SQLite file.

```
/src/Spacearr            ASP.NET Core 8 (net8.0) web app; serves API + embedded SPA
/src/Spacearr.Tests      xunit + FluentAssertions
/web                     Vite + React 18 + TypeScript SPA; built output embedded into the .NET app
/docker                  Dockerfile, entrypoint (PUID/PGID/UMASK), compose example
/docs                    specs, plans, research, user docs
```

### Backend (`Spacearr`)

- **Web**: ASP.NET Core minimal APIs grouped by feature under `/api/v1/...`. JSON camelCase. OpenAPI document generated at build (Swashbuckle) and served at `/api/docs` for logged-in users.
- **Data**: EF Core 8 with SQLite provider, migrations checked in. WAL mode. Database at `{config}/spacearr.db`. Config dir defaults to `/config` in Docker and `%LOCALAPPDATA%/Spacearr` or `~/.config/spacearr` otherwise; override with `SPACEARR_CONFIG_DIR`.
- **Background work**: a single `JobRunner` hosted service consuming a `Channel<JobRequest>`. One job at a time. Jobs: `ScanJob`, `EnrichJob`, `ActionJob`. Progress is published through an in-process `IProgressHub` and streamed to clients over Server-Sent Events at `/api/v1/events`.
- **Scheduling**: a `PeriodicTimer` hosted service enqueues a scan every N hours (default 6, configurable, 0 disables).
- **Logging**: Serilog to console and rolling file `{config}/logs/spacearr-.log`, 7 files retained.
- **Auth**: mandatory. First run redirects to `/setup` to create the admin user (username + password, PBKDF2 via `PasswordHasher<T>`). Cookie auth for the SPA, `X-Api-Key` header for scripts (key generated on first run, shown in Settings). No endpoint under `/api` is reachable without one of the two. `/api/v1/system/status` is the one exception and returns only version and whether setup is complete.
- **Secrets**: arr API keys are encrypted at rest with AES-256-GCM using a key stored in `{config}/secret.key` (created 0600 on first run). GET responses never return keys; they return `apiKeySet: true`.
- **External tools**: `ffprobe` is the primary media analyser (JSON output). `mediainfo` is used if present for `BitDepth`/`Bits/(Pixel*Frame)` cross-check but is optional. Tool paths configurable; presence reported in system status.

### Frontend (`web`)

- Vite, React 18, TypeScript strict, TanStack Query for server state, `react-router`. No Redux. Plain CSS with custom-property tokens and CSS Modules; no component library.
- Dark theme by default, light theme available, both fully tokenised.
- **Treemap renderer**: squarified layout via `d3-hierarchy` (`treemap` + `treemapSquarify`), rendered to a single `<canvas>` with device-pixel-ratio scaling. Own hit-testing from the layout rectangles. Hover and selection drawn on a second overlay canvas so the base layer only repaints on data or viewport change. Zoom into a group is an animated re-layout (250 ms, ease-out, honours `prefers-reduced-motion`). Poster images are drawn dimmed under the heat colour when a block is larger than 96×96 CSS px; smaller blocks are flat colour. Labels are drawn only when they fit. Target: 10,000 blocks at 60 fps on hover, first paint under 1 s.
- Pages: `Library` (treemap + table + stats + detail panel), `Duplicates`, `Activity` (job history + action log), `Settings` (Connections, Scanning, Account). `Setup` wizard for first run.

### Docker

- Base `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` with `ffmpeg` (for ffprobe), `mediainfo`, `su-exec`, `tzdata` from apk.
- `PUID`, `PGID`, `UMASK`, `TZ` honoured by the entrypoint. Volume `/config`. Media volumes mounted wherever the user likes and declared as root folders in Settings.
- Multi-arch `linux/amd64` and `linux/arm64` via GitHub Actions `docker/build-push-action` to `ghcr.io/zombiez4git/spacearr` with tags `latest`, `vX.Y.Z`, `develop`.
- Port 8787.

## Data model

All tables in SQLite via EF Core. Times are UTC.

**ArrInstance**: Id, Type (Radarr|Sonarr), Name, BaseUrl, ApiKeyEncrypted, Enabled, LastSyncAt, LastSyncError, CreatedAt.

**PathMapping**: Id, ArrInstanceId, RemotePrefix (as the arr app sees it), LocalPrefix (as Spacearr sees it). Applied longest-prefix-first when matching.

**RootFolder**: Id, Path, Enabled, LastScanAt. Paths Spacearr walks.

**MediaFile**: Id, Path (unique, normalised), RootFolderId, SizeBytes, ModifiedAt, ScannedAt, DurationSeconds, Width, Height, FrameRate, VideoCodec, VideoProfile, BitDepth, OverallBitrateBps, VideoBitrateBps, Container, AudioSummary (e.g. "TrueHD 7.1, AC3 5.1"), AudioBitrateBps, HdrFormat, ProbeError (nullable; set when ffprobe failed so the UI can show "unreadable").

**MediaItem**: Id, ArrInstanceId, ExternalId (movie id or episode file id), Kind (Movie|Episode), Title, Year, SeriesId (Sonarr series id, nullable), SeriesTitle, SeasonNumber, EpisodeNumbers (comma list), QualityProfileId, QualityProfileName, QualityName (e.g. "Bluray-1080p"), Monitored, Tags (comma list of tag labels), PosterUrl, TmdbId, TvdbId, ImdbId, MediaFileId (nullable FK; null when the arr app knows the item but Spacearr has not found the file), ArrFileId (moviefile/episodefile id), ArrPath (path as reported by the arr app), SyncedAt.

Uniqueness: (ArrInstanceId, Kind, ExternalId).

**Job**: Id, Type (Scan|Enrich|Action), Status (Queued|Running|Succeeded|Failed|Cancelled), Trigger (Manual|Scheduled), QueuedAt, StartedAt, FinishedAt, Summary (JSON: filesSeen, filesProbed, filesAdded, filesRemoved, itemsMatched, itemsUnmatched, errors), Error.

**ActionLog**: Id, At, Type (Delete|Replace), MediaItemId (nullable, item may be gone), ArrInstanceId, Title, Path, SizeBytesBefore, QualityBefore, QualityAfter, Outcome (Succeeded|Failed), Detail.

**Setting**: Key (PK), Value. Keys: `scan.intervalHours`, `scan.extensions`, `tools.ffprobePath`, `tools.mediainfoPath`, `heat.mode`, `ui.theme`.

**User**: Id, Username, PasswordHash, ApiKey, CreatedAt. Single user in v1.

## Core computations

### Heat (bitrate relative to what the pixels need)

For each file with video stream data:

```
bpp = videoBitrateBps / (width * height * frameRate)          // bits per pixel per frame
codecFactor = { h264/avc: 1.00, hevc/h265: 0.60, av1: 0.50, vp9: 0.65, mpeg2: 1.50, vc1: 1.20, other: 1.00 }
nbpp = bpp / codecFactor                                        // normalised to x264-equivalent
```

`OverallBitrateBps` is used when the video stream bitrate is absent (fall back: `sizeBytes * 8 / duration`, minus audio if known).

Heat is presented two ways, switchable:
- **Relative** (default): percentile of `nbpp` within the current filtered set. 0 = greenest, 1 = reddest. Robust to a library that is all remuxes.
- **Absolute**: fixed scale, `nbpp` 0.04 → green, 0.10 → yellow, 0.20 → orange, ≥ 0.30 → red. Matches published "good quality" ranges for x264.

Colour ramp: `#2E8B57` → `#C9A227` → `#E07B1F` → `#D14D4D`, interpolated in OKLCH. Files with `ProbeError` or no video stream draw hatched grey.

### Savings estimate

For "replace with smaller," estimate the new size from library evidence: median `sizeBytes / durationSeconds` of files in the same instance with the target quality name, times this file's duration. If fewer than 5 samples exist, fall back to a fixed table of typical bits-per-second per quality name. Show as "roughly X GB, based on N similar files" or "rough estimate" when from the table.

### Duplicates

Computed at query time from `MediaItem`:
- Movies: group by `TmdbId` when present, else normalised `Title` + `Year`, across all Radarr instances.
- Episodes: group by `TvdbId` + `SeasonNumber` + `EpisodeNumbers` across all Sonarr instances, plus any two `MediaFile`s the same item claims.
- A group is a duplicate when it holds ≥ 2 items with distinct `MediaFileId`s.
- Wasted bytes = group total − largest member (the user may prefer to keep the smallest; the UI offers both).

### Matching files to items

Enrichment pulls every movie / episode file from each instance, applies the instance's path mappings to `ArrPath`, normalises (case-insensitive on Windows, forward slashes, trailing slash trimmed), and looks up `MediaFile.Path` exactly. Unmatched items are kept with `MediaFileId = null` and counted, and the Connections page shows "matched 1,204 of 1,210 files; 6 unmatched" with the first few unmatched paths so the user can fix the mapping.

## API surface (`/api/v1`)

Auth required on all except `system/status`.

| Method | Path | Purpose |
|---|---|---|
| GET | `system/status` | version, setupComplete, tools found |
| POST | `setup` | create admin user (only while setupComplete = false) |
| POST | `auth/login`, `auth/logout` | cookie session |
| GET | `auth/me` | current user, api key |
| GET/POST/PUT/DELETE | `instances`, `instances/{id}` | arr connections; PUT accepts apiKey only when provided |
| POST | `instances/test` | body {type, baseUrl, apiKey}; returns version, name, root folders, quality profiles |
| GET | `instances/{id}/profiles` | quality profiles (cached 10 min) |
| GET/POST/PUT/DELETE | `instances/{id}/mappings` | path mappings |
| GET/POST/PUT/DELETE | `roots`, `roots/{id}` | root folders to scan |
| POST | `roots/validate` | body {path}; returns exists, fileCount sample |
| POST | `jobs/scan` | enqueue scan (+enrich) |
| POST | `jobs/enrich` | enqueue enrich only |
| GET | `jobs`, `jobs/{id}` | history and detail |
| POST | `jobs/{id}/cancel` | cooperative cancel |
| GET | `events` | SSE: job progress, job finished |
| GET | `library` | flat list of items with file facts; query: instanceId, kind, minBytes, search, sort, order, page, pageSize |
| GET | `library/tree` | hierarchical payload for the treemap (see below); query: instanceId, kind, minBytes, colorBy |
| GET | `library/stats` | totals, per-instance, per-quality, per-codec, per-resolution breakdowns, heat distribution |
| GET | `library/{itemId}` | full detail incl. savings estimate per available lower profile |
| GET | `duplicates` | groups with wasted bytes, sorted desc |
| POST | `actions/preview` | body {type, itemId, targetProfileId?, keepItemId?}; returns exactly what will happen and the bytes |
| POST | `actions/execute` | same body plus `confirmToken` from preview; enqueues ActionJob |
| GET | `actions/log` | paginated ActionLog |
| GET/PUT | `settings` | scan interval, extensions, tool paths, heat mode |

`library/tree` returns `{ name, children: [...] }` with leaves `{ id, name, bytes, heat, color, posterUrl, quality, codec, resolution, instanceId }` and groups for Sonarr as series → season → episode. Movies are leaves at the top level. Items below `minBytes` are folded into an "Other (n files)" leaf per group. The server does the folding so the client never handles more than ~10k leaves.

## Actions

Both go through the arr app so it stays in sync. Spacearr never deletes from disk directly.

**Delete**
1. Radarr: `DELETE /api/v3/moviefile/{id}`; optionally `PUT /api/v3/movie/{id}` with `monitored=false`.
2. Sonarr: `DELETE /api/v3/episodefile/{id}`; optionally unmonitor the episode(s).
3. Re-probe: mark `MediaFile` removed if the file is gone; log.

**Replace with smaller**
1. Radarr: `PUT /api/v3/movie/{id}` with new `qualityProfileId`; `DELETE /api/v3/moviefile/{fileId}`; `POST /api/v3/command {name: "MoviesSearch", movieIds:[id]}`.
2. Sonarr: quality profile is per series. Preview must say "this changes the profile for the whole series *Title* (N episodes)." Then `PUT /api/v3/series/{id}`; `DELETE /api/v3/episodefile/{fileId}`; `POST /api/v3/command {name: "EpisodeSearch", episodeIds:[...]}`.
3. Log with before/after quality and the estimate.

Preview is mandatory: `actions/execute` rejects a body without a `confirmToken` issued in the last 10 minutes for the same body.

## UI

**Setup** (first run): create account → add first connection (type, name, URL with a hint "inside Docker use the container name, not localhost", API key, Test) → path mapping suggested from the arr root folders vs Spacearr's view of the disk ("Radarr reports /data/movies; Spacearr can see /media/movies. Map it?") → add root folder(s) → "Scan now." The scan page shows live progress.

**Library**: treemap fills the viewport width, about 60% of height; below it a split with the sortable table (Title, Size, Heat, Quality, Codec, Resolution, Instance) and a stats rail (total bytes, files, per-instance bars, heat legend with the current mode, top 5 largest, top 5 hottest). Toolbar: instance filter, kind filter, colour-by (Heat, Quality, Codec, Resolution, Duplicates), heat mode (Relative / Absolute), min size, search, Scan now. Clicking a block or row opens the detail panel (slides in from the right, does not cover the treemap on wide screens). Breadcrumb over the treemap for Sonarr drill-down.

**Detail panel**: poster, title, year, instance, path, size, duration, bitrate, bpp and heat position, codec, profile, resolution, HDR, audio, quality profile, monitored, tags; "Open in Radarr/Sonarr"; buttons "Delete…" and "Replace with smaller…". Each opens a preview modal that lists the exact API calls in plain words, the bytes, the estimate, and for Sonarr the series-wide warning. Confirm button reads "Delete 44.1 GB" or "Replace, free 44.1 GB now."

**Duplicates**: groups as rows, expanded to members with size/quality/bitrate/instance side by side; "Keep this" on each member previews deleting the others.

**Activity**: jobs with progress and summaries; action log.

**Settings**: Connections (instances, test, mappings with the match count), Scanning (roots, interval, extensions, tools status), Account (change password, show/regenerate API key).

Design: dark by default, dense but readable, one accent (amber `#E3A83C` dark / `#B07410` light), heat colours reserved for meaning. Typeface: Inter Tight for UI, JetBrains Mono for numbers. Not a Radarr clone.

## Error handling

- Arr unreachable during enrich: job records the error per instance, continues with others, `LastSyncError` shown on the connection card.
- ffprobe failure on a file: `ProbeError` set, file still counted by bytes, drawn hatched, listed in a "couldn't read N files" note.
- Action failure mid-sequence (e.g. profile changed but delete failed): logged as Failed with which steps completed; the detail panel shows the log entry.
- Path mapping mismatch: surfaced as the unmatched count, never silent.
- Auth: five failed logins per minute per IP triggers a 60 s lockout.

## Testing

- Unit: heat computation, path normalisation and mapping, duplicate grouping, savings estimate, ffprobe JSON parsing (fixtures from real ffprobe output), tree folding, confirm-token issuance.
- Integration: EF migrations apply on a fresh SQLite; API auth gate on every route (a test enumerates endpoints and asserts 401 without auth); arr clients against a stub HTTP server with recorded Radarr v6 / Sonarr v4 responses.
- Frontend: Vitest for layout helpers and colour ramp; Playwright smoke: setup → connect stub → scan fixture folder → treemap renders → detail → preview.
- Fixture library: generated small MKV/MP4 files (ffmpeg `testsrc`) with known dimensions and bitrates, plus a stub arr server that serves matching movie/episode metadata. Used by integration and Playwright tests.

## Non-functional

- 10,000 files: scan (probe) bounded by ffprobe speed, parallelised 4-wide; incremental rescans skip unchanged (size + mtime) files.
- Treemap first paint < 1 s for 10k leaves; hover repaint < 16 ms.
- Image < 250 MB. Memory < 300 MB at 10k files.
- Startup applies migrations automatically; refuses to start on a database from a newer version.

## Launch deliverables (in the plan, not optional)

- README with screenshots, compose block, "what it does not do."
- `AI-DISCLOSURE.md`: built with Claude under human direction and review; how review happens.
- `SECURITY.md` with a contact; the auth-gate test in CI.
- GitHub Actions: build + test on PR; multi-arch image on tag.
- `docs/` user guide: install, connect, path mapping, heat explained, actions explained.
- Unraid CA template XML in `docker/unraid/`.

## Migration from the fork

The `rebuild` branch replaces the working tree. Code ported from the fork (commit `c0bdfea`): `Scanner/FileDiscoveryService`, `Scanner/MediaInfoExtractor` (ffprobe parsing), `Scanner/FileScannerService` (incremental logic), `ArrIntegration/RadarrApiClient`, `SonarrApiClient`, `ArrApiModels`, `FileMatchingService`, `QualityProfileCache`. Everything else is rewritten. The old `config/` directory, `_output`, `_temp`, `_tests`, `radarr-fork`, `temp-clone` are removed and ignored.
