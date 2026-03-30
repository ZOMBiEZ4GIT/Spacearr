# Spacearr — TODO

## Phase 0: Fork & Strip ✓
- [x] T0.1: Clone Radarr develop branch
- [x] T0.2: Strip backend domain modules
- [x] T0.3: Strip frontend domain modules
- [x] T0.4: Rename Radarr → Spacearr (namespaces, projects, branding, port)
- [x] T0.5: Fix compilation errors (648 errors → 0)
- [x] T0.6: Verify build (dotnet build passes, 0 errors 0 warnings)
- [x] T0.7: Create tracking docs (TODO.md, DEVLOG.md)
- [x] T0.8: Strip database migrations (deleted 9 broken migrations)

## Phase 1: Core Data Layer ✓
- [x] T1.1: Create enum types (MediaSource, ActionType, RecommendationType, RuleActionType, ScanStatus)
- [x] T1.2: Create model classes (MediaFile, MediaItem, ScanJob, ActionHistory, Recommendation, Rule)
- [x] T1.3: Create SQLite migration (250_spacearr_initial_schema)
- [x] T1.4: Repository interfaces
- [x] T1.5: Repository implementations
- [x] T1.6: Register in DI (TableMapper)
- [ ] T1.7: Unit tests (deferred)

## Phase 2: Filesystem Scanner ✓
- [x] T2.1: FileDiscoveryService (directory walking, 9 media extensions)
- [x] T2.2: MediaInfoExtractor (mediainfo CLI + ffprobe fallback, JSON parsing)
- [x] T2.3: FileScannerService (orchestration, incremental scanning)
- [x] T2.4: IncrementalScanStrategy (built into FileScannerService)
- [x] T2.5: ScanLibraryCommand + Handler
- [x] T2.6: Register scheduled task in TaskManager (360 min)
- [ ] T2.7: SignalR scan progress (deferred)
- [ ] T2.8: Scanner tests (deferred)

## Phase 3: ARR Integration ✓
- [x] T3.1: RadarrApiClient (GetMovies, TriggerSearch, UpdateQualityProfile, TestConnection)
- [x] T3.2: SonarrApiClient (GetSeries, GetEpisodeFiles, TriggerSearch, TestConnection)
- [x] T3.3: ArrConnectionTestService
- [x] T3.4: FileMatchingService (path normalization, case-insensitive, O(1) lookup)
- [x] T3.5: EnrichmentService (full pipeline: fetch, match, create/update MediaItems)
- [x] T3.6: QualityProfileCache (thread-safe, source-prefixed keys)
- [ ] T3.7: Hook enrichment into scan pipeline (deferred — will wire in Phase 7)
- [ ] T3.8: Settings UI for ARR connections (deferred — Phase 5)

## Phase 4: API Layer ✓
- [x] T4.1: LibraryController (paginated, filtered, sorted + stats)
- [x] T4.2: TreemapController (colorBy, source, minSize params)
- [x] T4.3: ScanController (trigger, status, history)
- [x] T4.4: ActionController (delete, search, qualityswap)
- [x] T4.5: RecommendationController (list, accept, dismiss, bulkdismiss)
- [x] T4.6: DuplicateController (list groups, keep action)
- [x] T4.7: HistoryController (paginated + stats)
- [x] T4.8: RuleController (full CRUD + evaluate)

## Phase 5: Frontend — Table & Stats
- [ ] T5.1: Navigation + routing (AppRoutes, nav bar)
- [ ] T5.2: Store actions + reducers
- [ ] T5.3: LibraryTable + LibraryTableRow
- [ ] T5.4: StatsPanel (summary, heat legend, quality breakdown, quick wins)
- [ ] T5.5: LibraryToolbar (filters, scan, bulk actions)
- [ ] T5.6: DetailDrawer (slide-out with file/ARR info + actions)
- [ ] T5.7: BitrateBadge + SourceBadge components
- [ ] T5.8: Library page layout (compose panels)

## Phase 6: Frontend — Treemap
- [ ] T6.1: treemapColors (bitrate heat scale + categorical)
- [ ] T6.2: treemapLayout + treemapData (d3 config + API transform)
- [ ] T6.3: TreemapTooltip
- [ ] T6.4: TreemapRenderer (SVG)
- [ ] T6.5: Treemap container
- [ ] T6.6: Interaction (zoom, selection)
- [ ] T6.7: Bidirectional table sync
- [ ] T6.8: Canvas fallback for large libraries

## Phase 7: Actions
- [ ] T7.1: DeleteActionService
- [ ] T7.2: SearchActionService
- [ ] T7.3: QualitySwapService
- [ ] T7.4: SpaceEstimationService
- [ ] T7.5: DeleteConfirmModal
- [ ] T7.6: SearchConfirmModal
- [ ] T7.7: QualitySwapModal
- [ ] T7.8: Wire actions into drawer + toolbar

## Phase 8: Recommendations & Duplicates
- [ ] T8.1: RecommendationEngine
- [ ] T8.2: DuplicateDetectionService
- [ ] T8.3: RecommendationSettings
- [ ] T8.4: Recommendations page UI
- [ ] T8.5: Duplicates page UI
- [ ] T8.6: Wire recommendation accept/dismiss
- [ ] T8.7: Wire duplicate keep/delete actions

## Phase 9: Bulk Operations & Rules
- [ ] T9.1: BulkActionService
- [ ] T9.2: RuleEngine + RuleEvaluator
- [ ] T9.3: BulkDeleteModal
- [ ] T9.4: BulkDowngradeModal
- [ ] T9.5: Rules settings UI
- [ ] T9.6: FilterCriteriaBuilder
- [ ] T9.7: Hook rules into scan pipeline

## Phase 10: History & Polish
- [ ] T10.1: History page + table
- [ ] T10.2: SpaceSavedChart
- [ ] T10.3: Health checks (ARR connectivity, scan freshness)
- [ ] T10.4: Loading skeletons, error boundaries, empty states
- [ ] T10.5: Performance pass (virtualized table, DB indexes, canvas treemap)
- [ ] T10.6: Documentation (README, Docker Compose)
- [ ] T10.7: Final QA
