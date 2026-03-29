# Spacearr — Design Specification

## Overview

Spacearr is a storage visualization and optimization tool for the ARR media stack. It provides a WinDirStat-inspired treemap view of your media library, colored by bitrate heat, with integrated actions to delete, re-fetch, and replace media files through Sonarr and Radarr APIs.

**Base:** Fork of Radarr (inherits .NET 8 + React + SQLite stack, ARR UI shell, API framework, config/notification infrastructure).

## Architecture

### System Components

- **Spacearr Backend** (.NET 8): API server + background services for scheduled filesystem scanning
- **Spacearr Frontend** (React): Treemap visualization, sortable table, action UI — built on Radarr's existing UI shell
- **SQLite Database**: Scan cache (file metadata, sizes, bitrates, codecs), action history, configuration, recommendation state
- **External Integrations**: Sonarr API, Radarr API, filesystem (direct scan)

### Data Flow

1. **Scan**: Background service walks configured library paths. Uses MediaInfo (or similar) to extract file size, codec, resolution, bitrate, duration for each media file.
2. **Enrich**: Matches scanned files to Sonarr/Radarr items via their APIs — pulls title, quality profile, monitored status, tags, series/movie metadata.
3. **Cache**: Stores enriched file data in SQLite. Incremental scans on schedule (configurable cron), full rescan on demand.
4. **Visualize**: Frontend renders treemap (size = file size, color = bitrate heat) + sortable table + stats panel.
5. **Act**: User triggers actions (delete, search, quality swap) which execute via Sonarr/Radarr APIs and/or direct filesystem operations, logged to history.

### Data Model (Key Entities)

- **MediaFile**: path, size_bytes, bitrate_bps, codec, resolution, duration_seconds, container_format, last_scanned
- **MediaItem**: links MediaFile to ARR source (sonarr_id/radarr_id), title, quality_profile, monitored, tags
- **ScanJob**: scan_id, started_at, completed_at, files_scanned, files_added, files_removed
- **ActionHistory**: action_type (delete/search/swap), media_item_id, old_quality, new_quality, space_freed_bytes, timestamp
- **Recommendation**: media_item_id, current_size, estimated_new_size, estimated_savings, recommendation_type, dismissed
- **Rule**: name, filter_criteria (tags, quality, size thresholds), target_quality, active

## UI Layout

### Three-Panel Dashboard (Library Page)

**Top Navigation Bar:**
- Spacearr branding
- Pages: Library, Recommendations, Duplicates, History, Settings
- Last scan timestamp + search bar

**Toolbar:**
- Source filter: All / Radarr / Sonarr
- Color-by toggle: Bitrate (default) / Quality / Codec / Resolution
- Minimum size filter
- Scan Now button
- Bulk Actions dropdown

**Left Panel — Sortable Table:**
- Columns: Checkbox, Title (with subtitle: quality + codec + path), Size, Bitrate (color-coded badge), Quality Profile, Source (Radarr/Sonarr)
- Sortable by any column, default sort by size descending
- Checkbox multi-select for bulk operations
- Click row to open detail drawer

**Right Panel — Stats & Legend:**
- Library summary: total size, file count, movie count, series count
- Bitrate heat gradient legend (green = low, red = high)
- Breakdown by quality profile with progress bars
- "Quick Wins" section showing top 2-3 space saver recommendations

**Bottom Panel — Treemap:**
- Rectangle size proportional to file size
- Color determined by selected heat mode (default: bitrate)
- Hover shows tooltip with title, size, bitrate, quality
- Click selects item in table and opens detail drawer
- Zoom: click into a group (e.g., a series) to see individual episodes

### Detail Drawer (Slide-out)

Opened by clicking a row or treemap block:
- File info: full path, size, bitrate, codec, resolution, duration, container
- ARR info: title, year, quality profile, monitored status, tags
- Actions: Delete, Search & Replace, Quality Swap, Ignore
- Space Saver suggestion if applicable ("Swap to Bluray-1080p, save ~50 GB")

### Pages

**Recommendations Page:**
- Space Saver engine ranks items by potential savings if swapped to lower quality
- Each card shows: title, current size → estimated new size, estimated savings
- One-click "Swap" button (changes quality profile in ARR + triggers search)
- Bulk accept/dismiss
- Configurable: default target qualities, minimum savings threshold

**Duplicates Page:**
- Detects multiple copies/editions of the same movie or episode
- Side-by-side comparison: size, quality, bitrate, codec
- Actions: Keep Best, Keep Smallest, Delete Specific
- Groups by movie/episode with expandable rows

**History Page:**
- Chronological log of all Spacearr actions
- Columns: timestamp, action type, item title, details, space freed
- Running total of space saved (with a chart over time)
- Filter by action type

**Settings Page:**
- **Connections**: Sonarr URL + API key (with Test button), Radarr URL + API key (with Test button)
- **Library Paths**: Filesystem paths to scan (with browse/validate)
- **Scan Schedule**: Cron expression or simple interval selector
- **Recommendations**: Default quality targets, minimum savings threshold to surface a recommendation
- **Rules**: Tag-based quality rules (e.g., "tag:kids-movies → max 1080p")
- **General**: Port, authentication, UI preferences
- **Notifications**: Future — webhooks, Discord, etc. (inherited from Radarr's notification framework)

## Features

### 1. Delete
- Remove file from disk
- Optionally unmonitor in Sonarr/Radarr
- Confirmation dialog showing file details and size to be freed
- Logged to history

### 2. Search & Replace
- Triggers a new search in the relevant ARR app for the same item
- Uses the item's current quality profile
- Useful when a better encode becomes available

### 3. Quality Swap
- Changes the quality profile on the item in Sonarr/Radarr
- Triggers an automatic search after the profile change
- Shows estimated size difference based on historical data
- Confirmation showing: current quality → new quality, estimated size change

### 4. Space Saver Recommendations
- Background job analyzes library after each scan
- Ranks items by: (current_size - estimated_size_at_lower_quality)
- Estimation based on: average bitrate for target quality from existing library data
- Surfaces recommendations above configurable threshold (default: 10 GB savings)
- Dismissible per-item

### 5. Duplicate Detection
- Matches by: movie title + year, or series + season + episode
- Considers files across different paths
- Flags: exact duplicates (same quality), quality variants (different quality of same content), edition variants

### 6. Stale Media Detection (Future — Phase 2)
- Integration with Plex/Jellyfin/Emby watch history APIs
- Identifies large files never watched or not watched in X months
- Not in initial build — designed as a plugin point for later

### 7. Bulk Quality Downgrade
- Select multiple items → choose target quality profile
- Applies quality profile change to all selected in their respective ARR app
- Triggers search for each
- Progress indicator showing completion

### 8. Bulk Delete
- Select multiple items → confirm deletion
- Summary showing total space to be freed
- Option to unmonitor all in ARR

### 9. Tag-Based Rules
- Persistent rules stored in database
- Filter criteria: tags, quality profile, size threshold, bitrate threshold, age
- Action: enforce maximum quality (auto-downgrades), flag for review, auto-delete samples
- Evaluated after each scan; rule violations surfaced on the Recommendations page

## Treemap Visualization

### Color Modes

**Bitrate Heat (Default):**
- Green (#2d8a4e) → Yellow (#c4a43e) → Orange (#f5a623) → Red (#e94560)
- Scale: relative to library median bitrate (auto-adjusting)
- Instantly identifies "bloated" files — high bitrate relative to their peers

**Quality Profile:**
- Distinct color per quality profile (Remux-2160p, Bluray-1080p, WEB-1080p, etc.)
- Consistent colors across views

**Codec:**
- Color per codec (x264, x265/HEVC, AV1, etc.)
- Useful for identifying old x264 encodes that could benefit from x265 re-fetch

**Resolution:**
- Color per resolution bucket (2160p, 1080p, 720p, 480p)

### Interaction

- Hover: tooltip with title, size, bitrate, quality
- Click: select in table + open detail drawer
- Zoom: click a group to drill into sub-items (e.g., click a series to see episodes)
- The treemap and table are synchronized — selecting in one highlights in the other

## Forking Strategy

### Base: Radarr

Fork from Radarr's `develop` branch. Radarr is chosen because:
- Closest domain model (individual media items with file management)
- Active development, well-structured codebase
- Inherits: UI shell, API framework, SQLite layer, config system, update mechanism, Docker/service infrastructure, notification framework

### What We Keep
- NzbDrone framework layer (API, database, configuration, logging, auth)
- React UI shell (navigation, theming, layout components)
- Settings infrastructure
- Build pipeline (Docker, Windows installer, Linux packages)
- Notification system
- SignalR real-time updates

### What We Strip Out
- Movie-specific domain logic (movie metadata, TMDB integration, download client management, indexer management, quality definition presets)
- Download pipeline (we don't download — we tell Sonarr/Radarr to search)
- Calendar, wanted/missing, manual import features

### What We Add
- Filesystem scanner service with MediaInfo integration
- Sonarr/Radarr API client services
- Treemap visualization component (React, likely using a library like `react-d3-treemap` or `recharts`)
- Bitrate heat calculation engine
- Recommendations engine
- Duplicate detection engine
- Bulk action pipeline
- Rules engine
- New data models (MediaFile, MediaItem, ScanJob, ActionHistory, Recommendation, Rule)

## Non-Functional Requirements

- **Performance**: Must handle libraries of 10,000+ files without UI lag. Treemap rendering should be < 1 second. Scans should be incremental (only re-scan changed files).
- **Deployment**: Docker image (primary), Windows service, Linux systemd — matching ARR ecosystem norms.
- **Port**: Default 8989-range (configurable), following ARR convention.
- **Authentication**: Inherited from Radarr's auth system (Forms, Basic, API key).
- **API**: RESTful, matching ARR API conventions for ecosystem consistency.
