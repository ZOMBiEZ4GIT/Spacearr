import React from 'react';
import { formatSize } from 'Library/LibraryItem';
import styles from './SpaceSavedChart.css';

interface MonthlyData {
  month: string;
  spaceSaved: number;
}

interface SpaceSavedChartProps {
  data: MonthlyData[];
}

function formatMonth(monthStr: string): string {
  const [year, month] = monthStr.split('-');
  const date = new Date(parseInt(year, 10), parseInt(month, 10) - 1);

  return date.toLocaleDateString(undefined, {
    month: 'short',
    year: '2-digit',
  });
}

function SpaceSavedChart({ data }: SpaceSavedChartProps) {
  // Show the last 6 months
  const recentData = data.slice(-6);

  if (recentData.length === 0) {
    return null;
  }

  const maxValue = Math.max(...recentData.map((d) => d.spaceSaved));

  return (
    <div className={styles.chart}>
      <div className={styles.chartTitle}>Space Saved Over Time</div>

      <div className={styles.bars}>
        {recentData.map((entry) => {
          const percentage =
            maxValue > 0 ? (entry.spaceSaved / maxValue) * 100 : 0;

          return (
            <div key={entry.month} className={styles.barRow}>
              <div className={styles.barLabel}>
                {formatMonth(entry.month)}
              </div>

              <div className={styles.barTrack}>
                <div
                  className={styles.barFill}
                  style={{ width: `${percentage}%` }}
                />
              </div>

              <div className={styles.barValue}>
                {formatSize(entry.spaceSaved)}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export default SpaceSavedChart;
