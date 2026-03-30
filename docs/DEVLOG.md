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

## 2026-03-30 — Phases 2+3 Complete: Scanner & ARR Integration

**Phase 2 — Filesystem Scanner** (5 files in `Spacearr/Scanner/`):
- FileDiscoveryService: walks root folders, filters 9 media extensions
- MediaInfoExtractor: wraps `mediainfo --Output=JSON` with ffprobe fallback, caches tool availability
- FileScannerService: full scan orchestration with incremental support (compares file mod time vs LastScanned)
- ScanLibraryCommand + Handler: NzbDrone command pattern, registered as 6h scheduled task

**Phase 3 — ARR Integration** (8 files in `Spacearr/ArrIntegration/`):
- RadarrApiClient + SonarrApiClient: v3 API clients with search, quality profile update, connection test
- FileMatchingService: path-normalized O(1) matching of scanned files to ARR items
- EnrichmentService: fetch all items from ARR, match to MediaFiles, create/update MediaItem records
- QualityProfileCache: thread-safe in-memory ID→name cache with source prefixes

Build: 0 errors, 0 warnings.

---

## 2026-03-30 — Phases 5+6 Complete: Frontend UI

**Phase 5 — Table, Stats, Navigation** (25 new files, 3 modified):
- AppRoutes.tsx rewritten for Spacearr pages; sidebar updated
- LibraryPage: three-panel layout (table + stats + treemap slot)
- LibraryTable + StatsPanel + LibraryToolbar + DetailDrawer
- BitrateBadge (green→red heat) + SourceBadge (Radarr gold / Sonarr blue)
- Redux store actions for library state

**Phase 6 — Treemap** (17 files):
- d3-hierarchy treemap with 4 color modes, drill-down zoom, 2000-item perf cap
- TreemapBlock (React.memo), TreemapTooltip, TreemapBreadcrumb

---
