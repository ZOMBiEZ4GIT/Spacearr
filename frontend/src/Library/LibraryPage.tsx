import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { LibraryItem } from './LibraryItem';
import LibraryTable from './LibraryTable';
import DetailDrawer from './Detail/DetailDrawer';
import StatsPanel from './Stats/StatsPanel';
import LibraryToolbar from './Toolbar/LibraryToolbar';
import styles from './LibraryPage.css';

function LibraryPage() {
  const [sortKey, setSortKey] = useState<string>('size');
  const [sortDirection, setSortDirection] = useState<SortDirection>('descending');
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [sourceFilter, setSourceFilter] = useState<string>('all');
  const [minSizeFilter, setMinSizeFilter] = useState<number>(0);
  const [colorBy, setColorBy] = useState<string>('bitrate');
  const [detailItem, setDetailItem] = useState<LibraryItem | null>(null);

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
            <StatsPanel />
          </div>
        </div>

        {/* Treemap placeholder for Phase 6 */}
        <div className={styles.treemapContainer}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: '100%',
              color: 'var(--disabledColor)',
            }}
          >
            Treemap visualization will appear here (Phase 6)
          </div>
        </div>
      </div>

      <DetailDrawer item={detailItem} onClose={handleCloseDetail} />
    </PageContent>
  );
}

export default LibraryPage;
