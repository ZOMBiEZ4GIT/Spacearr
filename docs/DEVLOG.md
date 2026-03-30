# Spacearr — Development Log

## 2026-03-29 — Project Kickoff

**Design completed.** Spec written to `docs/superpowers/specs/2026-03-29-spacearr-design.md`.

**Key decisions:**
- Fork Radarr (develop branch) as base — inherits .NET 8 + React + SQLite + ARR UI shell
- Treemap visualization with bitrate heat as default coloring (green=low, red=high)
- Hybrid discovery: filesystem scan (MediaInfo) + Sonarr/Radarr API enrichment
- 9 core features: Delete, Search & Replace, Quality Swap, Space Saver Recommendations, Duplicate Detection, Stale Media (future), Bulk Quality Downgrade, Bulk Delete, Tag-Based Rules
- Full standalone service on port 8787
- Stale media detection (Plex/Jellyfin/Emby integration) deferred to Phase 2

**Implementation plan created.** 11 phases, starting with Fork & Strip.

---

## 2026-03-30 — Phase 0 Complete: Fork & Strip

**Cloned Radarr develop**, stripped movie domain, rebranded to Spacearr.

**Backend stripped:** Movies, MediaFiles, MediaCover, Extras, Indexers, Download, DecisionEngine, Queue, Organizer, ImportLists, MovieStats, Qualities, CustomFormats, Profiles, MetadataSource, Credits, Blocklisting, AutoTagging + 21 health checks, 7 housekeepers, 4 converters, 9 broken migrations.

**Frontend stripped:** Movie, MovieFile, AddMovie, DiscoverMovie, Collection, Calendar, Wanted, InteractiveImport, InteractiveSearch, DownloadClient, Quality, Organize, Activity.

**Notification framework preserved** but stripped of all movie event handlers. Kept: health issue, app update, test. Stub types created for cross-cutting enums (FileDateType, ProperDownloadTypes, TMDbCountryCode).

**Build status:** `dotnet build` — 0 errors, 0 warnings. .NET 8.0.419 SDK.

**GitHub remote:** https://github.com/ZOMBiEZ4GIT/Spacearr.git

---

## 2026-03-30 — Phase 1 Complete: Core Data Layer

Created 18 files under `src/NzbDrone.Core/Spacearr/`:
- 5 enums: MediaSource, ActionType, RecommendationType, RuleActionType, ScanStatus
- 6 models: MediaFile, MediaItem, ScanJob, ActionHistory, Recommendation, Rule
- 6 repositories with custom query methods (FindByPath, GetActive, GetRecent, etc.)
- 1 migration (250_spacearr_initial_schema) creating all 6 tables with indexes
- TableMapping.cs updated with all entity registrations

Build: 0 errors, 0 warnings.

---
