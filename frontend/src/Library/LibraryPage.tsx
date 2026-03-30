import React, { useCallback, useEffect, useMemo, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { LibraryItem, MOCK_LIBRARY_ITEMS } from './LibraryItem';
import LibraryTable from './LibraryTable';
import DetailDrawer from './Detail/DetailDrawer';
import StatsPanel from './Stats/StatsPanel';
import LibraryToolbar from './Toolbar/LibraryToolbar';
import Treemap from './Treemap/Treemap';
import { TreemapItem, ColorMode } from './Treemap/treemapTypes';
import styles from './LibraryPage.css';

interface LibraryStats {
  totalSize: number;
  fileCount: number;
  qualityBreakdown: Array<{ quality: string; size: number; count: number }>;
  quickWins: Array<{
    id: number;
    title: string;
    currentSize: number;
    estimatedSize: number;
    savings: number;
  }>;
}

interface LibraryResource {
  id: number;
  title: string;
  year?: number;
  seriesTitle?: string;
  seasonNumber?: number;
  episodeNumber?: number;
  source: string;
  qualityProfile?: string;
  monitored?: boolean;
  filePath: string;
  sizeBytes: number;
  bitrateBps: number;
  codec: string;
  resolution: string;
  durationSeconds: number;
  containerFormat: string;
}

function mapResourceToLibraryItem(resource: LibraryResource): LibraryItem {
  const isShow = resource.source === 'sonarr';
  let title = resource.title;

  if (isShow && resource.seriesTitle) {
    const season = String(resource.seasonNumber ?? 0).padStart(2, '0');
    const episode = String(resource.episodeNumber ?? 0).padStart(2, '0');
    title = `${resource.seriesTitle} - S${season}E${episode}`;
  }

  return {
    id: resource.id,
    title,
    path: resource.filePath,
    size: resource.sizeBytes,
    bitrate: Math.round(resource.bitrateBps / 1000),
    quality: resource.qualityProfile ?? 'Unknown',
    codec: resource.codec,
    resolution: resource.resolution,
    duration: resource.durationSeconds,
    container: resource.containerFormat,
    source: resource.source as 'radarr' | 'sonarr',
    year: resource.year,
    qualityProfile: resource.qualityProfile,
    monitored: resource.monitored,
  };
}

function libraryItemToTreemapItem(item: LibraryItem): TreemapItem {
  return {
    id: item.id,
    title: item.title,
    sizeBytes: item.size,
    bitrateBps: item.bitrate * 1000,
    codec: item.codec,
    resolution: item.resolution,
    qualityProfile: item.qualityProfile ?? item.quality,
    source: item.source,
    parentGroup: item.source,
  };
}

function LibraryPage() {
  const [sortKey, setSortKey] = useState<string>('size');
  const [sortDirection, setSortDirection] = useState<SortDirection>('descending');
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [sourceFilter, setSourceFilter] = useState<string>('all');
  const [minSizeFilter, setMinSizeFilter] = useState<number>(0);
  const [colorBy, setColorBy] = useState<string>('bitrate');
  const [detailItem, setDetailItem] = useState<LibraryItem | null>(null);
  const [libraryItems, setLibraryItems] = useState<LibraryItem[]>([]);
  const [stats, setStats] = useState<LibraryStats | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchData() {
      try {
        const [libraryResponse, statsResponse] = await Promise.allSettled([
          fetch('/api/v3/library'),
          fetch('/api/v3/library/stats'),
        ]);

        if (cancelled) {
          return;
        }

        if (
          libraryResponse.status === 'fulfilled' &&
          libraryResponse.value.ok
        ) {
          const resources: LibraryResource[] =
            await libraryResponse.value.json();
          setLibraryItems(resources.map(mapResourceToLibraryItem));
        } else {
          setLibraryItems(MOCK_LIBRARY_ITEMS);
        }

        if (statsResponse.status === 'fulfilled' && statsResponse.value.ok) {
          const statsData: LibraryStats = await statsResponse.value.json();
          setStats(statsData);
        }
      } catch {
        if (!cancelled) {
          setLibraryItems(MOCK_LIBRARY_ITEMS);
        }
      }
    }

    fetchData();

    return () => {
      cancelled = true;
    };
  }, []);

  const treemapItems = useMemo(
    () => libraryItems.map(libraryItemToTreemapItem),
    [libraryItems]
  );

  const handleSortChange = useCallback(
    (key: string) => {
      if (key === sortKey) {
        setSortDirection((prev) =>
          prev === 'ascending' ? 'descending' : 'ascending'
        );
      } else {
        setSortKey(key);
        setSortDirection('descending');
      }
    },
    [sortKey]
  );

  const handleItemClick = useCallback((item: LibraryItem) => {
    setDetailItem(item);
  }, []);

  const handleCloseDetail = useCallback(() => {
    setDetailItem(null);
  }, []);

  const handleScanNow = useCallback(() => {
    // Will dispatch triggerScan action when wired up
    // eslint-disable-next-line no-console
    console.log('Scan triggered');
  }, []);

  const handleBulkAction = useCallback(
    (action: string) => {
      // eslint-disable-next-line no-console
      console.log(`Bulk action: ${action} on`, selectedIds);
    },
    [selectedIds]
  );

  return (
    <PageContent title="Library">
      <div className={styles.page}>
        <LibraryToolbar
          sourceFilter={sourceFilter}
          colorBy={colorBy}
          minSizeFilter={minSizeFilter}
          selectedCount={selectedIds.length}
          onSourceFilterChange={setSourceFilter}
          onColorByChange={setColorBy}
          onMinSizeFilterChange={setMinSizeFilter}
          onScanNow={handleScanNow}
          onBulkAction={handleBulkAction}
        />

        <div className={styles.mainContent}>
          <div className={styles.tableContainer}>
            <LibraryTable
              items={libraryItems}
              sortKey={sortKey}
              sortDirection={sortDirection}
              selectedIds={selectedIds}
              sourceFilter={sourceFilter}
              minSizeFilter={minSizeFilter}
              onSortChange={handleSortChange}
              onSelectionChange={setSelectedIds}
              onItemClick={handleItemClick}
            />
          </div>

          <div className={styles.statsPanel}>
            <StatsPanel
              totalSize={stats?.totalSize}
              fileCount={stats?.fileCount}
              qualityBreakdown={stats?.qualityBreakdown}
              quickWins={stats?.quickWins}
            />
          </div>
        </div>

        <div className={styles.treemapContainer}>
          <Treemap
            items={treemapItems}
            colorMode={colorBy as ColorMode}
            onItemClick={(treemapItem) => {
              const match = libraryItems.find((li) => li.id === treemapItem.id);

              if (match) {
                handleItemClick(match);
              }
            }}
          />
        </div>
      </div>

      <DetailDrawer item={detailItem} onClose={handleCloseDetail} />
    </PageContent>
  );
}

export default LibraryPage;
