# Spacearr: ecosystem research and launch strategy

Date: 2026-09-10
Mission statement (Roland): **"the cooler looking WinDirStat for your arr stack."**

This document has two parts. Part A is the synthesis and recommendation. Part B holds the four evidence reports (core Servarr apps, companion tools, storage-optimisation niche, launch playbook) with source URLs, lightly edited.

---

## Part A: Synthesis

### 1. Verdict in one paragraph

The niche is real and unserved. No tool gives an arr user a per-title, bitrate-aware picture of where their disk went, and every route people currently take (Radarr issues, Plex forum, Tautulli PRs) is closed, stalled, or "use TreeSize." The ecosystem's failures fall into five repeatable patterns, and Spacearr is currently exposed to three of them: it is built on a fork with no upstream, it was produced largely by AI without disclosure or review, and it shipped feature breadth instead of one polished thing. The recommendation is to keep the scanner and arr-client logic, drop the Radarr shell, and rebuild as a small standalone service whose entire identity is the treemap.

### 2. Where the arr apps went wrong

**Fragile external dependencies killed or crippled whole apps.** Readarr was retired on 27 Jun 2025 because Goodreads closed its API and the Open Library migration stalled. Lidarr depends entirely on a Servarr-hosted MusicBrainz mirror that returned 500s in April 2025 and 503s for days in August 2025, and a request for a self-hosted metadata source was closed Won't Fix the day it was opened. Plex tightened its API in 2025 and broke Tautulli remote monitoring and every Plex-watchlist tool. Lesson: Spacearr should depend only on the Sonarr/Radarr v3 APIs and the local filesystem. No Plex, no TMDB, no hosted metadata.

**Architecture decided in 2014 cannot be changed.** The most-upvoted open issues in both Radarr (#1910, 2017) and Sonarr (#4551, 2021, comment-locked) ask for multiple versions of one title. Maintainers say it "requires an entire rewrite." Users work around it by running two instances (1080p and 4K), which manufactures exactly the duplicates and waste Spacearr wants to show. Lesson: multi-instance support is table stakes, and cross-instance duplicates are a headline feature, not a Phase 8 extra.

**Complexity was outsourced to the community.** TRaSH Guides lists 200+ custom formats; Recyclarr, Configarr, Buildarr, Profilarr, and Notifiarr exist only to sync them. Size control in the arr apps is "a very crude rule set" by TRaSH's own description, and Radarr closed a bitrate custom-format request Won't Fix. Lesson: Spacearr must not require configuration to be useful. Connect, scan, see.

**Long platform migrations and breaking releases.** Sonarr v4 sat in public beta 13 months. Radarr v6, Lidarr v3, and Prowlarr v2 all shipped the same .NET 8 / drop-linux-x86 / remove-Basic-Auth changes within months of each other, because each is a fork carrying the others' changes. Whisparr's commit log is one-third "Sonarr Upstream Sync" batches. Lesson: forking a Servarr codebase means inheriting a fortnightly upstream you must merge forever, and even the Servarr team only manages it monthly.

**Storage insight was never built.** Radarr #9371 "Statistics Page" is open, Low priority, Help Wanted. Sort-by-size shipped in v3 after a year; bitrate display was closed as duplicate in April 2026. Neither app will downgrade a file: the cutoff only ever moves up. Lesson: this is the gap, and the apps' own trackers document the demand.

### 3. Where the companion tools died

| Tool | Fate | Cause |
|---|---|---|
| Huntarr | Author deleted repo, Discord, and Reddit account within hours on 24 Feb 2026 | Unauthenticated endpoint returned every arr API key in cleartext; 21 findings; "100% vibe coded" criticism; critics banned; later versions added telemetry and obfuscation |
| Booklore | Author nuked project, March 2026 | 20k-line AI PRs, telemetry after opt-out, AGPL to BSL switch, paid tier, hostile responses |
| Overseerr / Jellyseerr | Merged into Seerr, Feb 2026 | Healthy consolidation, not a failure; note the sunset plan and migration guide |
| Readarr | Retired Jun 2025 | Metadata source gone |
| Tdarr, FileFlows | Alive but distrusted | Closed-source V2 under EULA; paid tiers; exportarr refuses to support it |
| Excludarr, Buildarr, Watchlistarr, Doplarr, Prunerr | Dormant or archived | Single maintainer; "codebase became difficult to maintain"; Plex API changes |
| Tautulli | Alive, shrinking scope | Plex API restrictions |

Two patterns account for almost every death: a dependency the author did not control, or a trust failure (security, telemetry, licence, AI without disclosure, banning critics). Only Overseerr's exit was orderly.

### 4. The gap, with evidence

Users asking for this, and what they were told:

- Radarr #5646 (2020): "sort the movies by size so I can easily remove the largest items and/or redownload them in a sane quality." Closed.
- Radarr #11436 (Apr 2026): sort and filter library by bitrate; requester "must use third-party tools to compile bitrate data into CSV." Closed as duplicate.
- Plex forum, "filter movies by bitrate": open since 2019, "This request is 7 years old now" in July 2026.
- Plex forum, "sort library by file size" (2023): answer was "disconnect storage and use TreeSize."
- Tautulli PR #2186 adding total file size columns: open since Oct 2023.
- Sonarr #2162 (2017): "My 6TB RAID5 is fast approaching full," asking for automatic downgrade of old seasons. Closed. Staff on a 2018 request to prefer smaller files: "quality trumps that ordering."

What exists today, and what it lacks:

| Tool | Knows bytes | Knows bitrate | Knows arr titles | Treemap | Acts via arr |
|---|---|---|---|---|---|
| WinDirStat / WizTree / QDirStat | yes | no | no | yes | no |
| Tautulli media-info table | yes | yes | no (Plex) | no | no |
| medialytics (Plex-only, 211 stars) | yes | yes | no | yes, read-only | no |
| Grand Media Station (commercial desktop) | yes | yes, codec-aware verdicts | no | no | no |
| Maintainerr (2.3k stars) | as rule inputs | as rule inputs | yes | no | delete / unmonitor only |
| Reclaimerr (2026, 558 stars) | as rule inputs | no | yes | no | delete / move |
| Tdarr (closed source) | aggregate charts | per-file flow plugin | via plugin | no | transcodes, desyncs arr |
| Radarr / Sonarr | per title | per file, buried | yes | no | cannot downgrade |

Nobody combines the per-title view, bitrate-relative-to-resolution heat, cross-instance duplicates, and an arr-native reclaim action. Maintainerr is the nearest neighbour; it already stores fileBitrate and fileSize as rule fields but has no visualisation and only deletes.

### 5. Non-obvious facts that change the design

1. **Sonarr and Radarr never downgrade.** The cutoff only stops upgrades. "Downgrade and re-search" is therefore three steps: change the profile, delete the file, trigger a search. Spacearr must present this honestly as "replace with a smaller release," with the file gone in between, and default to a preview.
2. **The 1080p + 4K two-instance workaround is the norm** for power users, so Spacearr must accept N Radarr and N Sonarr connections and match files across them. The duplicates page falls out of that for free.
3. **Bitrate heat needs a codec-aware metric.** Absolute Mbps misleads: HEVC and AV1 need roughly half the bits of x264. Use bits per pixel per frame (MediaInfo reports it directly) normalised per codec, then rank relative to library peers. Established benchmarks put 0.1 to 0.2 bpp as "good" for x264.
4. **Docker path mapping is the number one first-run complaint** for every companion tool: users enter localhost, or Spacearr sees /media while Radarr sees /data/movies. The connection UI needs a hostname hint and a path-mapping step with a live "found 1,204 of 1,210 files" check.
5. **Authentication must be on by default** (Sonarr v4 made it mandatory; Huntarr died for leaving settings endpoints open). Spacearr stores arr API keys, so it is a credential store and must be treated as one.
6. **The scanner's cost is MediaInfo, not disk walking.** Incremental scans by mtime are already implemented in the current code and should stay.

### 6. The launch bar in 2026, as the community enforces it

- Multi-arch image on ghcr.io (amd64 and arm64), PUID/PGID/UMASK/TZ, /config volume, semver tags plus a develop tag, GitHub Releases with changelogs.
- README with real screenshots or a short GIF, a docker-compose block, and an explicit "what it does not do."
- Auth on by default; no unauthenticated endpoints; a basic security pass before the launch post.
- No telemetry. No licence surprises. GPLv3 is fine and expected.
- A written AI-usage disclosure in the repo and a described human review process. Lemmy's selfhosted community now requires [AIP] tags with phase-level disclosure; Pulsarr's transparency paragraph is the model.
- Safe defaults: dry-run or preview first, nothing deleted without an explicit confirm that shows the bytes.
- Docs site and a Discord; an Unraid Community Applications template and a support thread; TrueNAS via forum vote later.
- Launch in the specialised communities first (r/radarr, r/sonarr, Lemmy selfhosted with [AIP], awesome-arr), not r/selfhosted's weekly thread. Wait four months before awesome-selfhosted.
- Answer criticism in public and never ban a critic. Single-maintainer projects are judged on this alone.

### 7. The foundation decision

The repo today is roughly 10,000 lines of Spacearr code on top of 90,000 lines of retained Radarr framework. The retained part is already broken (schema, housekeepers, NuGet audit), has no upstream remote, and upstream has since moved to v6 with a release every two weeks.

**Option A: repair the fork.** Fix the schema by writing one clean initial migration, delete the housekeepers, fix the audit error, rewrite the frontend against the real API. Roughly a week to a working slice. You keep Radarr's auth, settings, SignalR, and update mechanism. You also keep 90k lines you did not write, a 2015-era Redux/webpack UI shell that will fight a canvas treemap, a large Docker image, and a fortnightly upstream you will never merge. Every Radarr framework bug is yours. No successful companion tool has taken this route; the Servarr forks are maintained by the Servarr team and still struggle.

**Option B: standalone rebuild, porting the real code.** A single ASP.NET Core 8 service with an embedded Vite/React SPA, SQLite via a light data layer, Serilog, and a scheduler. Port the 3,300 lines that are genuinely good (file discovery, MediaInfo/ffprobe extraction, incremental scan, Radarr/Sonarr clients, path matching, quality-profile cache). Write the treemap as a canvas renderer for 10k+ blocks with smooth zoom, which is the "cooler looking" part. C# is accepted in this ecosystem (Cleanuparr, Recyclarr, Ombi). Estimated two to three weeks to a launchable v1 with the scope below, and the codebase stays small enough that one person can hold it.

**Option C: standalone in Go or Node.** Smallest image, most common stack among companions. Throws away the working C# and Roland's home-turf language for no product gain.

Recommendation: **Option B.** The mission statement is a visual product; the Radarr shell contributes nothing to the visual and costs the most to keep.

### 8. Proposed v1 scope

Must ship:
- Connections: any number of Radarr and Sonarr instances, test button, Docker hostname hint, path mappings with live match count.
- Scan: root folders, MediaInfo with ffprobe fallback, incremental by mtime, progress over WebSocket or SSE.
- Treemap: size = bytes; colour modes bitrate heat (codec-aware bpp), quality profile, codec, resolution, duplicates; Sonarr drill-down series to season to episode; posters; hover tooltip; click to detail; breadcrumb.
- Table and stats: sortable by size and heat, filter by instance and minimum size, library totals, per-profile breakdown.
- Detail: file facts, arr facts, open-in-Radarr/Sonarr link.
- Actions, two only: delete (with unmonitor option) and replace-with-smaller (profile change, delete, search), both preview-first with bytes shown.
- Duplicates: same title across instances or paths, ranked by wasted bytes, keep-this-one action.
- Auth on by default, ghcr multi-arch image, docs, AI disclosure, screenshots.

Cut from v1 (revisit only if users ask):
- Rules engine, bulk downgrade, recommendation engine as a separate concept (it is the heat-sorted table with a savings estimate), history charts, notifications, the Radarr settings tree.

---

## Part B: Evidence reports

### B1. Core Servarr apps

#### Per-app status

**Sonarr** — Active. Latest stable v4.0.19.2979 (26 Jun 2026); develop builds at v4.0.20.x (10 Sep 2026) ([releases/latest](https://api.github.com/repos/Sonarr/Sonarr/releases/latest), [releases](https://github.com/Sonarr/Sonarr/releases)). The GitHub default branch is now `v5-develop`, and the v5.0 milestone is 97% complete (149 closed / 4 open) with no due date ([repo](https://github.com/Sonarr/Sonarr), [milestone](https://github.com/Sonarr/Sonarr/milestone/4)). The v4 beta was announced 24 Nov 2022 ([forum](https://forums.sonarr.tv/t/sonarr-v4-beta/31078)) and shipped 30 Dec 2023 "after a year of development and almost 1000 commits"; v3 declared EOL the same day ([forum](https://forums.sonarr.tv/t/sonarr-v4-released/33089), [TRaSH](https://trash-guides.info/Sonarr/sonarr-v3-eol/)). v4 breaking changes: .NET 6 (no mono), v2 API removed, Preferred Words replaced by Custom Formats, auth required by default ([wiki FAQ](https://wiki.servarr.com/sonarr/faq)).

**Radarr** — Active. v6.0.0 pre-release 21 Sep 2025 ("Bump to .NET 8", "Support removed for linux-x86"); first stable v6.0.4 on 16 Nov 2025; latest stable v6.3.0 (12 Jul 2026), develop at v6.4.3 (30 Aug 2026) ([v6.0.0](https://api.github.com/repos/Radarr/Radarr/releases/tags/v6.0.0.10217), [v6.0.4](https://api.github.com/repos/Radarr/Radarr/releases/tags/v6.0.4.10291), [latest](https://api.github.com/repos/Radarr/Radarr/releases/latest)). Distro fallout: DietPi's installer broke on the mono to dotnet switch ([DietPi #7837](https://github.com/MichaIng/DietPi/issues/7837)).

**Lidarr** — Active but fragile. v3.0.0 pre-release 19 Oct 2025 (.NET 8, Basic Auth removed, linux-x86 dropped); latest stable v3.1.0; develop at 3.1.5 (6 Sep 2026) ([v3.0.0](https://api.github.com/repos/Lidarr/Lidarr/releases/tags/v3.0.0.4855), [latest](https://api.github.com/repos/Lidarr/Lidarr/releases/latest)). 861 open issues. Lidarr depends entirely on a Servarr-hosted MusicBrainz mirror ([wiki](https://wiki.servarr.com/lidarr/faq)). 500-error outage 9 Apr 2025 ([#5498](https://github.com/Lidarr/Lidarr/issues/5498)); persistent 503s over multiple days in late Aug 2025 that wiped artwork ([#5579](https://github.com/Lidarr/Lidarr/issues/5579)). Request for a self-hosted metadata source closed Won't Fix the day it was opened; "no plans to allow users to use custom metadata" ([#5546](https://github.com/Lidarr/Lidarr/issues/5546)). Metadata server repo last pushed 9 Jul 2025.

**Readarr** — Retired 27 Jun 2025; repo archived. "The project's metadata has become unusable, we no longer have the time to remake or repair it, and the community effort to transition to using Open Library as the source has stalled" ([README](https://github.com/readarr/readarr)). Background: Goodreads killed API access; large authors un-addable since 2023; Open Library migration stalled on mapping 1,200+ Goodreads IDs ([wiki](https://wiki.servarr.com/readarr/metadata-issues)).

**Whisparr** — Active, two parallel lines (Sonarr-based v2, Radarr-based v3 "Eros") ([README](https://github.com/Whisparr/Whisparr)).

**Prowlarr** — Active. v2.0.0 pre-release 15 Jun 2025 (.NET 8, Basic Auth removed, linux-x86 dropped); latest stable v2.5.2 (22 Jul 2026).

**Bazarr** — Not a Servarr project; Python; GPL-3.0; stable v1.6.0 (4 Jul 2026). Servarr's wiki lists third-party companions as "not maintained, developed, nor supported by the *Arr Development Team" ([useful-tools](https://wiki.servarr.com/useful-tools)).

#### Cross-cutting themes

**Two instances for 1080p + 4K.** Radarr's most-upvoted open issue is #1910 "Trying to keep multiple versions of a movie" (2017, 72 upvotes). Maintainer comments: "a tad more complicated than that if we want to do it right" (2020); "we should do it right or not do it at all" (2022); "fundamentally requires rewriting… including database restructuring" ([comments](https://api.github.com/repos/Radarr/Radarr/issues/1910/comments?per_page=100)). Sonarr's equivalent #4551 (2021, 116 reactions, comment-locked): "We've made no progress… This is going to take an immense amount of work" (2023); "effectively requires an entire rewrite of the frontend and backend" ([#4551](https://api.github.com/repos/Sonarr/Sonarr/issues/4551)). Workaround tooling: [Syncarr](https://github.com/syncarr/syncarr).

**Custom-format complexity.** TRaSH's Radarr CF collection lists 200+ formats ([TRaSH](https://trash-guides.info/Radarr/Radarr-collection-of-custom-formats/)); a tool ecosystem exists just to sync them: Recyclarr, Configarr, Buildarr, Profilarr ([Profilarr](https://github.com/Dictionarry-Hub/profilarr), [Configarr](https://github.com/raydak-labs/configarr)).

**Per-file storage insight.** Radarr #9371 "Statistics Page" (Nov 2023) remains open, "Priority: Low / Help Wanted" ([#9371](https://github.com/Radarr/Radarr/issues/9371)).

**Database corruption/migration.** Wiki: "SQLite and network drives not play nice together and will cause a malformed database eventually" ([Radarr FAQ](https://wiki.servarr.com/radarr/faq)). Sonarr carries 213 DB migration files, Radarr 141.

**File size / bitrate.** Radarr has min/preferred/max MB-per-minute; Sonarr only min/max. Radarr "Bitrate custom format" #9108 closed Won't Fix ([#9108](https://github.com/Radarr/Radarr/issues/9108)). TRaSH notes size is "a very crude rule set" ([TRaSH](https://trash-guides.info/Radarr/Radarr-Quality-Settings-File-Size/)).

**Forks.** No written Servarr policy on "-arr" naming or fork support was found; only the third-party disclaimer. Sportarr (forked from Sonarr Oct 2025) uses the suffix without documented objection ([Sportarr](https://github.com/Sportarr/Sportarr)). Cost evidence: Whisparr's last 26 commits include 11 "Chore: Sonarr Upstream Sync – Batch NN" commits, monthly Dec 2025 to Aug 2026 ([commits](https://github.com/Whisparr/Whisparr/commits/v2-develop)).

**Codebase scale.** Radarr: 2,504 C# files, ~194k C# lines (~60k tests), ~101k TS/JS + 10k CSS, 26 projects. Stack: C#/.NET 8, React 18.3.1, TypeScript 5.7, Node 20, SQLite/Postgres, nUnit. Stars: Radarr 14.3k, Sonarr 15.4k. Cadence: Radarr ~265 releases, roughly one every two weeks ([releasealert](https://releasealert.dev/github/Radarr/Radarr)).

**License.** All Servarr apps GPL-3.0. Distributing a modified version requires releasing source under GPL and marking it changed ([GNU FAQ](https://www.gnu.org/licenses/gpl-faq.html)).

Unverified: Reddit sentiment (Reddit blocked); exact Lidarr outage durations; any Servarr naming position; maintainer quotes on fork-sync cost.

### B2. Companion tools

Method: GitHub REST API on 2026-09-10. All tools are standalone API clients; none forks a Servarr codebase.

| Tool | What it does | Stack / licence | Stars | First rel. | Latest rel. | Commits 2025 → 2026 | Status |
|---|---|---|---|---|---|---|---|
| [Maintainerr](https://github.com/Maintainerr/Maintainerr) | Rule-based deletion of unwatched media (Plex/Jellyfin/Emby) | TS (NestJS/Next), MIT | 2.3k | 2022 | v3.27.0, 2026-09-05 | 869 → 1,895 | active |
| [Janitorr](https://github.com/Schaka/janitorr) | Same for Jellyfin/Emby only | Kotlin, GPL-3 | 749 | 2024 | v2.2.0, 2026-08-22 | 78 → 42 | active |
| [Decluttarr](https://github.com/ManiMatter/decluttarr) | Removes stalled/failed items from arr queues | Python, GPL-3 | 876 | 2023 | push 2026-07-28 | 160 → 45 | active |
| Huntarr | Searched for missing/upgradable media | Python, GPL-3 | unverifiable | 2025 | v9.4.2 (Feb 2026) | — | deleted by author Feb 2026 |
| [Cleanuparr](https://github.com/Cleanuparr/Cleanuparr) | Strike-based removal of stalled/malicious downloads | C#, GPL-3 | 2.5k | 2024 | v2.10.5, 2026-08-12 | 222 → 229 | active |
| [Recyclarr](https://github.com/recyclarr/recyclarr) | Sync TRaSH CFs/profiles | C#, MIT | 2.1k | 2021 | v8.7.2, 2026-09-03 | 467 → 583 | active |
| [Profilarr](https://github.com/Dictionarry-Hub/profilarr) | Git-backed profile management | TS (Svelte), AGPL-3 | 2.6k | 2023 | v2.2.0, 2026-08-17 | 431 → 948 | active |
| [Notifiarr](https://github.com/Notifiarr/notifiarr) | Client for notifiarr.com | Go+Svelte, MIT; hosted backend | 932 | 2020 | v0.9.7, 2026-08-30 | 777 → 533 | active |
| [Unpackerr](https://github.com/Unpackerr/unpackerr) | Extracts archives for arr imports | Go, MIT | 1.5k | 2018 | v0.16.1, 2026-08-31 | 31 → 366 | active |
| [Checkrr](https://github.com/aetaric/checkrr) | Corrupt-file scanner, triggers re-download | Go, MIT | 578 | 2022 | 3.6.1, 2026-02-18 | 28 → 28 | active, low volume |
| [Prunerr](https://github.com/rpatterson/prunerr) | Disk-aware torrent pruning | Python | 54 | 2022 | v3.0.0b0, 2023-11 | 0 → 0 | dormant |
| [Ombi](https://github.com/Ombi-app/Ombi) | Request tool | C#, GPL-2 | 4.1k | 2016 | v4.60.16, 2026-09-04 | 181 → 345 | active |
| [Overseerr](https://github.com/sct/overseerr) | Request tool (Plex) | TS, MIT | 5.0k | 2020 | v1.35.0, 2026-02-15 | 51 → 3 | archived, superseded by Seerr |
| [Seerr](https://github.com/seerr-team/seerr) | Unified Overseerr+Jellyseerr | TS, MIT | 12.5k | 2022 | v3.4.1, 2026-07-30 | 350 → 373 | active |
| [Tautulli](https://github.com/Tautulli/Tautulli) | Plex analytics | Python, GPL-3 | 6.6k | 2016 | v2.18.1, 2026-08-27 | 62 → 328 | active |
| [Buildarr](https://github.com/buildarr/buildarr) | Declarative YAML config | Python, GPL-3 | 375 | 2023 | v0.8.0b1, 2024-04 | 0 → 0 | dormant |
| [Autopulse](https://github.com/dan-online/autopulse) | Rescan triggers from webhooks | Rust, MIT | 543 | 2024 | v2.0.0, 2026-06-05 | 284 → 170 | active |
| [Kometa](https://github.com/Kometa-Team/Kometa) | Plex metadata/overlays | Python, MIT | 3.4k | 2021 | v2.4.8, 2026-08-15 | 270 → 366 | active |
| [Homarr](https://github.com/homarr-labs/homarr) | Dashboard | TS, Apache-2 | 4.7k | 2024 | v1.77.0, 2026-09-06 | 2,219 → 1,532 | active |
| [Wizarr](https://github.com/wizarrrr/wizarr) | Media-server invites | Python, MIT | 3.2k | 2023 | v2026.9.0 | 1,798 → 706 | active |
| [Requestrr](https://github.com/thomst08/requestrr) | Discord request bot (fork) | C#, MIT | 488 | 2023 | V2.1.10, 2026-06 | 15 → 6 | slowing |
| [Doplarr](https://github.com/activexray/Doplarr) | Discord request bot | Clojure, MIT | 591 | 2021 | v3.8.0 | 10 → 3 | archived 2026-06-15; Rust rewrite |
| [arr-scripts](https://github.com/RandomNinjaAtk/arr-scripts) | Bash add-ons for LSIO containers | Shell, GPL-3 | 1.5k | n/a | push 2026-01-16 | 165 → 3 | slowing |
| [Swaparr](https://github.com/ThijmenGThN/swaparr) | Stalled-download remover | Rust, MIT | 395 | 2024 | 0.12.0, 2026-02 | 11 → 10 | slowing |
| [Excludarr](https://github.com/haijeploeg/excludarr) | Removes titles on streaming services | Python, MIT | 234 | 2021 | v1.0.7, 2022 | 1 → 0 | unmaintained; successor Prunarr |
| [Byparr](https://github.com/ThePhaseless/Byparr) | FlareSolverr drop-in | Python, GPL-3 | 1.9k | 2024 | v3.0.4, 2026-08 | 305 → 133 | active |
| [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr) | Cloudflare bypass | Python, MIT | 15.5k | 2020 | v3.5.0, 2026-05 | 51 → 18 | slowing |
| [Dispatcharr](https://github.com/Dispatcharr/Dispatcharr) | IPTV manager | JS/Python, AGPL-3 | 4.0k | 2025 | v0.30.0, 2026-08-29 | 2,382 → 1,764 | active |
| [Pulsarr](https://github.com/jamcalli/Pulsarr) | Plex watchlist to arr with routing | TS, AGPL-3 | 741 | 2025 | v0.19.2, 2026-09-07 | 3,274 → 1,164 | active |
| [Watchlistarr](https://github.com/nylonee/watchlistarr) | Plex watchlist RSS | Scala, GPL-3 | 365 | 2023 | v0.2.6, 2024-10 | 1 → 0 | dormant; Plex 403s |
| [Tdarr](https://github.com/HaveAGitGat/Tdarr) | Transcode automation | Closed-source V2 | 4.3k | 2019 | 2.86.01, 2026-08 | 4 → 23 | active, binaries only |

Discovered additions: [Fetcharr](https://github.com/egg82/fetcharr) (Java, 339 stars, created 2026-02-28, "human-developed Huntarr replacement"); [Reclaimerr](https://github.com/jessielw/Reclaimerr) (Python, GPL-3, 558 stars, created 2026-01, rule cleanup with file-size criteria); [Clonarr](https://github.com/ProphetSe7en/clonarr) (Go, 166 stars); [Cleanarr](https://github.com/se1exin/Cleanarr) (Plex duplicate finder, 275 stars, last push 2024-07); [medialytics](https://github.com/Drewpeifer/medialytics).

Distribution: every traction project publishes on ghcr.io; hotio carries Unpackerr, Seerr, Tautulli, Bazarr, Requestrr, Doplarr; linuxserver.io carries Ombi, Tautulli, Kometa, Bazarr; TrueCharts has Helm charts for most.

Overlap with Spacearr's niche: Maintainerr exposes `fileSize`, `fileBitrate`, `fileVideoResolution`, `fileVideoCodec`, `fileQualityCutoffMet` as rule fields ([rules.constants.ts](https://github.com/Maintainerr/Maintainerr/blob/main/apps/server/src/modules/rules/constants/rules.constants.ts)) but has no visualisation and only deletes/unmonitors. Tautulli's media-info table lists bitrate, resolution, codec, size. Tdarr has "Check Overall Bitrate" and "Compare File Size Ratio" flow plugins and aggregate pie charts. No surveyed tool provides a treemap or bitrate-heat view.

### B3. Storage-optimisation niche

Note: Reddit was blocked; evidence comes from Plex, Sonarr and Unraid forums, GitHub issues, Lemmy, and Hacker News.

**Tdarr** — ~4.3k stars; LICENSE.md is a proprietary EULA with free personal, personal subscription, and business tiers ([LICENSE.md](https://github.com/HaveAGitGat/Tdarr/blob/master/LICENSE.md)). Last public source release Beta v1.2066 (Sep 2020). exportarr declined Tdarr support in Jan 2024 because it is closed source ([exportarr #254](https://github.com/onedr0p/exportarr/issues/254)). Arr desync is documented: Tdarr_Plugins PR #493 exists because "Sonarr/Radarr would be out of sync because Tdarr altered the files" ([PR #493](https://github.com/HaveAGitGat/Tdarr_Plugins/pull/493)); Tdarr #975: "i dont want to redownload things just because radarr thinks it has a 'old' file" ([#975](https://github.com/HaveAGitGat/Tdarr/issues/975)); Radarr #7896 reports x264 CF flag persisting after an x265 transcode ([#7896](https://github.com/Radarr/Radarr/issues/7896)). Positive: an Unraid user reported 27 TB reduced by 7 TB ([forum](https://forums.unraid.net/topic/184517-shoutout-for-tdarr/)).

**Unmanic** — ~2.5k stars, GPL-3.0; latest v0.4.1 (Aug 2024) ([repo](https://github.com/unmanic/unmanic)). **FileFlows** — proprietary; Personal Free, Personal $6.99/mo, Commercial $499.99/mo ([pricing](https://fileflows.com/pricing)).

**Existing visualisation tools:** WinDirStat/WizTree/QDirStat/ncdu/dua colour by MIME type; none reads bitrate. Plex can sort by bitrate but not filter; request open since 2019, "This request is 7 years old now" in July 2026 ([forums.plex.tv 461280](https://forums.plex.tv/t/filter-movies-by-bitrate/461280)); sort by file size unavailable, answer was TreeSize ([843498](https://forums.plex.tv/t/ability-to-sort-library-by-file-size-not-bitrate/843498)). Jellyfin Reports plugin lacks file size ([#77](https://github.com/jellyfin/jellyfin-plugin-reports/issues/77)). Tautulli "Library Media Stats" PR open since Oct 2023 ([PR #2186](https://github.com/Tautulli/Tautulli/pull/2186)). Radarr #11436 (Apr 2026) bitrate sort/filter closed as duplicate ([#11436](https://github.com/Radarr/Radarr/issues/11436)). Grafana exporters expose only totals. medialytics (211 stars) has a Plex-only read-only movie treemap ([medialytics](https://github.com/Drewpeifer/medialytics)).

**Demand signals:** Radarr #5646 (2020) ([link](https://github.com/Radarr/Radarr/issues/5646)); Radarr #3897 (2019) ([link](https://github.com/Radarr/Radarr/issues/3897)); Lemmy "have radarr have a file size preference?" after a 44 GB remux ([link](https://lemmy.world/post/10124813)); Sonarr #2162 (2017) "My 6TB RAID5 is fast approaching full" ([link](https://github.com/Sonarr/Sonarr/issues/2162)); Sonarr forum 2018, staff: "quality trumps that ordering" ([link](https://forums.sonarr.tv/t/ability-to-tell-sonarr-to-prefer-smaller-downloads-of-same-quality/19524)).

**Duplicates:** Radarr supports one file per movie; "scan all folders and remove duplicates" closed Won't Fix ([#10860](https://github.com/Radarr/Radarr/issues/10860)); Sonarr #3928 acknowledges pre-cutoff duplicates ([link](https://github.com/Sonarr/Sonarr/issues/3928)). Unsolved: cross-library duplicates ranked by wasted bytes with an arr-aware keep action.

**Downgrade behaviour:** "Once the quality cutoff is reached… Sonarr will stop looking for new releases" ([Sonarr Settings](https://wiki.servarr.com/sonarr/settings)); "If you have a BluRay 2160p right now, it won't re-download a BluRay 1080p if you change the profile" ([Lemmy](https://lemmy.world/comment/16295442)). Workarounds: delete then search, or flip profile order so a lower quality ranks as an "upgrade" ([forums.sonarr.tv 11502](https://forums.sonarr.tv/t/download-lower-quality-when-higher-exists/11502)).

**Bitrate heat:** MediaInfo reports "Bits/(Pixel*Frame)"; bits-per-pixel benchmarks ~0.1 to 0.2 for good x264 quality ([Streaming Learning Center](https://streaminglearningcenter.com/encoding/what_is_data_rate_bits_per_pixel.html)). Grand Media Station gives "codec and bitrate verdicts using per-codec pixel-per-second thresholds" but is a desktop app for Plex/Jellyfin with no arr integration ([grandmediastation.com](https://grandmediastation.com/)).

### B4. Launch playbook

**Traction launches.** Seerr announced 2026-02-10 with migration guide, automatic config migration, sunset date, contributor credits ([blog](https://docs.seerr.dev/blog/seerr-release/)). Dispatcharr: ~302 stars Jul 2025 to 4,000 by Sep 2026; README with six screenshots, three compose variants, docs site, Discord ([repo](https://github.com/Dispatcharr/Dispatcharr)). Cleanuparr: explicit "why it exists," Docusaurus docs, warning that `latest` may break, contains a CLAUDE.md ([repo](https://github.com/Cleanuparr/Cleanuparr)). Pulsarr carries a transparency section: "developed with AI assistance, but every decision about architecture, features, and direction is human-made, and everything is reviewed before it ships" ([repo](https://github.com/jamcalli/Pulsarr)). Maintainerr homepage: "Founded in 2021 - real human work, before mainstream AI." Janitorr README states what it does NOT do and defaults to dry-run ([repo](https://github.com/Schaka/janitorr)).

**Roasted.** Huntarr (2026-02-24): r/selfhosted post "Your passwords and your entire arr stack's API keys are exposed" ([post](https://www.reddit.com/r/selfhosted/comments/1rckopd/huntarr_your_passwords_and_your_entire_arr_stacks/), [review](https://github.com/rfsbraz/huntarr-security-review)); 21 findings; settings endpoint "requires no login, no session, no API key"; maintainer banned critics; "100% vibe coded"; repo, Discord, Reddit deleted within hours ([piunikaweb](https://piunikaweb.com/2026/02/24/huntarr-security-vulnerability-arr-api-keys-exposed/)); ElfHosted forked v6.6.3 as Newtarr because later versions "introduced telemetry, obfuscated code" ([newtarr](https://github.com/elfhosted/newtarr)). Booklore (Mar 2026): ~20k-line AI PRs, telemetry after opt-out, AGPL to BSL, paid tier, bans; project nuked ([post](https://www.reddit.com/r/selfhosted/comments/1rs275q/psa_think_hard_before_you_deploy_booklore/), [XDA](https://www.xda-developers.com/single-maintainer-open-source-ticking-time-bomb/)). ntfy v2.18 disclosed "The code was written by Cursor and Claude" and still drew unease ([release](https://github.com/binwiederhier/ntfy/releases/tag/v2.18.0)).

**AI stance.** selfh.st 2025-07-18: r/selfhosted "overly hostile towards new projects that use em dashes and emoji" ([selfh.st](https://selfh.st/weekly/2025-07-18/)); 2026-08: "vibe coded projects need at least 6 months to a year of history"; the r/selfhosted weekly thread is "where new projects go to die" ([selfh.st](https://selfh.st/weekly/2026-09-04/)). Lemmy !selfhosted mandates [CBH]/[AIP]/[AIT] tags with phase-level disclosure; an `ai-declaration.md` satisfies it ([post](https://lemmy.world/post/48847985)). awesome-selfhosted bans LLM-generated contributions that ignore guidelines and requires first release older than 4 months ([CONTRIBUTING](https://github.com/awesome-selfhosted/awesome-selfhosted-data/blob/master/CONTRIBUTING.md)).

**Distribution.** ghcr.io everywhere; amd64+arm64 floor; hotio conventions PUID/PGID/UMASK/TZ, /config ([hotio](https://hotio.dev/containers/base/)); Unraid CA via https://ca.unraid.net/submit with a support thread ([docs](https://docs.unraid.net/community-applications/)); TrueNAS via forum vote then PR; awesome-arr has 80+ companions ([awesome-arr](https://github.com/Ravencentric/awesome-arr)); TRaSH lists sync tools only if you are active in their Discord.

**First-run norms.** Sonarr v4 forces credentials ([faq](https://github.com/Servarr/Wiki/blob/master/sonarr/faq-v4.md)); Seerr/Pulsarr wizards test the arr connection and fetch profiles and root folders; Janitorr defaults to dry-run. Dominant complaint: `ECONNREFUSED 127.0.0.1` because users type localhost inside a container ([overseerr](https://github.com/sct/overseerr/discussions/2309), [maintainerr](https://github.com/Maintainerr/Maintainerr/issues/1229)).

**Naming.** No Servarr objection to the "-arr" suffix found. Plex Meta Manager became Kometa (Apr 2024) over Plex's trademark. Dispatcharr asserts its own trademark. Huntarr's brand became toxic enough that its fork was renamed.
