import React, { useCallback, useEffect, useMemo, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { formatSize } from 'Library/LibraryItem';
import SpaceSavedChart from './SpaceSavedChart';
import styles from './SpacearrHistoryPage.css';

type ActionType = 'delete' | 'search' | 'swap' | 'duplicate';

interface HistoryEntry {
  id: number;
  timestamp: string;
  actionType: ActionType;
  title: string;
  details: string;
  spaceFreed: number;
}

interface HistoryStats {
  totalSpaceSaved: number;
  deleteCount: number;
  searchCount: number;
  swapCount: number;
  duplicateCount: number;
  monthlyData: Array<{ month: string; spaceSaved: number }>;
}

const ACTION_LABELS: Record<ActionType, string> = {
  delete: 'Deleted',
  search: 'Searched',
  swap: 'Swapped',
  duplicate: 'Deduped',
};

const ACTION_ICONS: Record<ActionType, string> = {
  delete: '\u2716',
  search: '\u2315',
  swap: '\u21C5',
  duplicate: '\u2261',
};

const ACTION_STYLES: Record<ActionType, string> = {
  delete: 'actionDelete',
  search: 'actionSearch',
  swap: 'actionSwap',
  duplicate: 'actionDuplicate',
};

// Mock data for development
const MOCK_HISTORY: HistoryEntry[] = [
  {
    id: 1,
    timestamp: '2026-03-29T14:30:00Z',
    actionType: 'delete',
    title: 'The Matrix',
    details: 'Deleted Bluray-2160p file',
    spaceFreed: 84_200_000_000,
  },
  {
    id: 2,
    timestamp: '2026-03-28T10:15:00Z',
    actionType: 'swap',
    title: 'Inception',
    details: 'Remux-2160p -> Bluray-2160p',
    spaceFreed: 55_000_000_000,
  },
  {
    id: 3,
    timestamp: '2026-03-27T08:00:00Z',
    actionType: 'search',
    title: 'Breaking Bad - S01E01',
    details: 'Searched for Bluray-720p replacement',
    spaceFreed: 0,
  },
  {
    id: 4,
    timestamp: '2026-03-26T16:45:00Z',
    actionType: 'duplicate',
    title: 'Interstellar',
    details: 'Removed duplicate Remux-2160p copy',
    spaceFreed: 85_600_000_000,
  },
  {
    id: 5,
    timestamp: '2026-03-25T12:00:00Z',
    actionType: 'swap',
    title: 'Dune',
    details: 'WEBDL-2160p -> WEBDL-1080p',
    spaceFreed: 14_000_000_000,
  },
  {
    id: 6,
    timestamp: '2026-03-24T09:30:00Z',
    actionType: 'delete',
    title: 'Game of Thrones - S08E03',
    details: 'Deleted unmonitored Bluray-1080p file',
    spaceFreed: 12_400_000_000,
  },
];

const MOCK_STATS: HistoryStats = {
  totalSpaceSaved: 251_200_000_000,
  deleteCount: 2,
  searchCount: 1,
  swapCount: 2,
  duplicateCount: 1,
  monthlyData: [
    { month: '2025-10', spaceSaved: 45_000_000_000 },
    { month: '2025-11', spaceSaved: 62_000_000_000 },
    { month: '2025-12', spaceSaved: 38_000_000_000 },
    { month: '2026-01', spaceSaved: 55_000_000_000 },
    { month: '2026-02', spaceSaved: 72_000_000_000 },
    { month: '2026-03', spaceSaved: 251_200_000_000 },
  ],
};

type SortDirection = 'asc' | 'desc';

const ALL_ACTION_TYPES: ActionType[] = ['delete', 'search', 'swap', 'duplicate'];

function SpacearrHistoryPage() {
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [stats, setStats] = useState<HistoryStats | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [filterType, setFilterType] = useState<ActionType | null>(null);
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');

  useEffect(() => {
    Promise.all([
      fetch('/api/v3/history')
        .then((res) => res.json())
        .catch(() => MOCK_HISTORY),
      fetch('/api/v3/history/stats')
        .then((res) => res.json())
        .catch(() => MOCK_STATS),
    ]).then(([historyData, statsData]) => {
      setHistory(historyData);
      setStats(statsData);
      setIsLoading(false);
    });
  }, []);

  const handleFilterToggle = useCallback(
    (type: ActionType) => {
      setFilterType((prev) => (prev === type ? null : type));
    },
    []
  );

  const handleSortToggle = useCallback(() => {
    setSortDirection((prev) => (prev === 'desc' ? 'asc' : 'desc'));
  }, []);

  const filteredHistory = useMemo(() => {
    let items = filterType
      ? history.filter((h) => h.actionType === filterType)
      : history;

    items = [...items].sort((a, b) => {
      const dateA = new Date(a.timestamp).getTime();
      const dateB = new Date(b.timestamp).getTime();

      return sortDirection === 'desc' ? dateB - dateA : dateA - dateB;
    });

    return items;
  }, [history, filterType, sortDirection]);

  if (isLoading) {
    return (
      <PageContent title="History">
        <PageContentBody>
          <div className={styles.loading}>Loading history...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  return (
    <PageContent title="History">
      <PageContentBody>
        {/* Stats Bar */}
        {stats && (
          <div className={styles.statsBar}>
            <div className={styles.totalSaved}>
              <div className={styles.totalSavedValue}>
                {formatSize(stats.totalSpaceSaved)}
              </div>
              <div className={styles.totalSavedLabel}>Total Space Saved</div>
            </div>

            <div className={styles.statDivider} />

            <div className={styles.statItem}>
              <div className={styles.statValue}>{stats.deleteCount}</div>
              <div className={styles.statLabel}>Deleted</div>
            </div>

            <div className={styles.statItem}>
              <div className={styles.statValue}>{stats.searchCount}</div>
              <div className={styles.statLabel}>Searched</div>
            </div>

            <div className={styles.statItem}>
              <div className={styles.statValue}>{stats.swapCount}</div>
              <div className={styles.statLabel}>Swapped</div>
            </div>

            <div className={styles.statItem}>
              <div className={styles.statValue}>{stats.duplicateCount}</div>
              <div className={styles.statLabel}>Deduped</div>
            </div>
          </div>
        )}

        {/* Chart */}
        {stats && stats.monthlyData.length > 0 && (
          <div className={styles.chartSection}>
            <SpaceSavedChart data={stats.monthlyData} />
          </div>
        )}

        {/* Filters */}
        <div className={styles.filterBar}>
          <span className={styles.filterLabel}>Filter:</span>

          {ALL_ACTION_TYPES.map((type) => (
            <button
              key={type}
              className={
                filterType === type
                  ? styles.filterButtonActive
                  : styles.filterButton
              }
              onClick={() => handleFilterToggle(type)}
            >
              {ACTION_LABELS[type]}
            </button>
          ))}
        </div>

        {/* Table */}
        {filteredHistory.length === 0 ? (
          <div className={styles.emptyState}>
            No history entries{filterType ? ` for "${ACTION_LABELS[filterType]}"` : ''}.
          </div>
        ) : (
          <table className={styles.table}>
            <thead>
              <tr>
                <th
                  className={styles.tableHeaderActive}
                  onClick={handleSortToggle}
                >
                  Date
                  <span className={styles.sortIndicator}>
                    {sortDirection === 'desc' ? '\u25BC' : '\u25B2'}
                  </span>
                </th>
                <th className={styles.tableHeader}>Action</th>
                <th className={styles.tableHeader}>Title</th>
                <th className={styles.tableHeader}>Details</th>
                <th className={styles.tableHeader}>Space Freed</th>
              </tr>
            </thead>

            <tbody>
              {filteredHistory.map((entry) => (
                <tr key={entry.id} className={styles.tableRow}>
                  <td className={styles.tableCell}>
                    {new Date(entry.timestamp).toLocaleDateString(undefined, {
                      year: 'numeric',
                      month: 'short',
                      day: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </td>

                  <td className={styles.tableCell}>
                    <span
                      className={
                        (styles as unknown as Record<string, string>)[ACTION_STYLES[entry.actionType]]
                      }
                    >
                      {ACTION_ICONS[entry.actionType]}{' '}
                      {ACTION_LABELS[entry.actionType]}
                    </span>
                  </td>

                  <td className={styles.tableCell}>{entry.title}</td>

                  <td className={styles.tableCell}>{entry.details}</td>

                  <td className={styles.tableCell}>
                    {entry.spaceFreed > 0 ? (
                      <span className={styles.spaceFreed}>
                        {formatSize(entry.spaceFreed)}
                      </span>
                    ) : (
                      '-'
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </PageContentBody>
    </PageContent>
  );
}

export default SpacearrHistoryPage;
