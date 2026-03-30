import React from 'react';
import { formatSize } from '../LibraryItem';
import styles from './StatsPanel.css';

interface QualityBreakdown {
  quality: string;
  size: number;
  count: number;
}

interface QuickWin {
  id: number;
  title: string;
  currentSize: number;
  estimatedSize: number;
  savings: number;
}

interface StatsPanelProps {
  totalSize?: number;
  fileCount?: number;
  qualityBreakdown?: QualityBreakdown[];
  quickWins?: QuickWin[];
}

// Mock stats for development
const MOCK_QUALITY_BREAKDOWN: QualityBreakdown[] = [
  { quality: 'Remux-2160p', size: 164_000_000_000, count: 2 },
  { quality: 'Bluray-2160p', size: 84_200_000_000, count: 1 },
  { quality: 'Bluray-1080p', size: 17_600_000_000, count: 2 },
  { quality: 'WEBDL-2160p', size: 22_600_000_000, count: 1 },
  { quality: 'WEBDL-1080p', size: 4_100_000_000, count: 1 },
  { quality: 'Bluray-720p', size: 1_800_000_000, count: 1 },
];

const MOCK_QUICK_WINS: QuickWin[] = [
  {
    id: 7,
    title: 'Interstellar',
    currentSize: 85_600_000_000,
    estimatedSize: 25_600_000_000,
    savings: 60_000_000_000,
  },
  {
    id: 3,
    title: 'Inception',
    currentSize: 78_400_000_000,
    estimatedSize: 23_400_000_000,
    savings: 55_000_000_000,
  },
  {
    id: 1,
    title: 'The Matrix',
    currentSize: 84_200_000_000,
    estimatedSize: 42_200_000_000,
    savings: 42_000_000_000,
  },
];

function StatsPanel({
  totalSize,
  fileCount,
  qualityBreakdown,
  quickWins,
}: StatsPanelProps) {
  const displayTotalSize = totalSize ?? 294_300_000_000;
  const displayFileCount = fileCount ?? 8;
  const displayBreakdown = qualityBreakdown ?? MOCK_QUALITY_BREAKDOWN;
  const displayQuickWins = quickWins ?? MOCK_QUICK_WINS;

  const maxQualitySize = Math.max(...displayBreakdown.map((q) => q.size), 1);

  return (
    <div className={styles.panel}>
      {/* Library Summary */}
      <div className={styles.section}>
        <div className={styles.sectionTitle}>Library</div>
        <div className={styles.statRow}>
          <span className={styles.statLabel}>Total Size</span>
          <span className={styles.statValue}>
            {formatSize(displayTotalSize)}
          </span>
        </div>
        <div className={styles.statRow}>
          <span className={styles.statLabel}>Files</span>
          <span className={styles.statValue}>
            {displayFileCount.toLocaleString()}
          </span>
        </div>
      </div>

      {/* Bitrate Heat Legend */}
      <div className={styles.section}>
        <div className={styles.sectionTitle}>Bitrate Heat</div>
        <div className={styles.legendBar} />
        <div className={styles.legendLabels}>
          <span>Low</span>
          <span>High</span>
        </div>
      </div>

      {/* Quality Breakdown */}
      <div className={styles.section}>
        <div className={styles.sectionTitle}>Quality Breakdown</div>
        {displayBreakdown.map((entry) => (
          <div key={entry.quality} className={styles.qualityRow}>
            <div className={styles.qualityLabel}>
              <span className={styles.qualityName}>{entry.quality}</span>
              <span className={styles.qualitySize}>
                {formatSize(entry.size)} ({entry.count})
              </span>
            </div>
            <div className={styles.qualityBar}>
              <div
                className={styles.qualityBarFill}
                style={{
                  width: `${(entry.size / maxQualitySize) * 100}%`,
                }}
              />
            </div>
          </div>
        ))}
      </div>

      {/* Quick Wins */}
      <div className={styles.section}>
        <div className={styles.sectionTitle}>Quick Wins</div>
        {displayQuickWins.map((win) => (
          <div key={win.id} className={styles.quickWinItem}>
            <div className={styles.quickWinTitle}>{win.title}</div>
            <div className={styles.quickWinSavings}>
              Save ~{formatSize(win.savings)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export default StatsPanel;
