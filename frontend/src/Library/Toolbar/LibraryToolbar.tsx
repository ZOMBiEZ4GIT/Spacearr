import React, { useCallback } from 'react';
import styles from './LibraryToolbar.css';

interface LibraryToolbarProps {
  sourceFilter: string;
  colorBy: string;
  minSizeFilter: number;
  selectedCount: number;
  onSourceFilterChange: (source: string) => void;
  onColorByChange: (colorBy: string) => void;
  onMinSizeFilterChange: (minSize: number) => void;
  onScanNow: () => void;
  onBulkAction: (action: string) => void;
}

const SOURCE_OPTIONS = [
  { key: 'all', label: 'All' },
  { key: 'radarr', label: 'Radarr' },
  { key: 'sonarr', label: 'Sonarr' },
];

const COLOR_BY_OPTIONS = [
  { key: 'bitrate', label: 'Bitrate' },
  { key: 'quality', label: 'Quality' },
  { key: 'codec', label: 'Codec' },
  { key: 'resolution', label: 'Resolution' },
];

function LibraryToolbar({
  sourceFilter,
  colorBy,
  minSizeFilter,
  selectedCount,
  onSourceFilterChange,
  onColorByChange,
  onMinSizeFilterChange,
  onScanNow,
  onBulkAction,
}: LibraryToolbarProps) {
  const handleMinSizeChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const value = parseFloat(event.target.value);
      onMinSizeFilterChange(isNaN(value) ? 0 : value);
    },
    [onMinSizeFilterChange]
  );

  const handleColorByChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      onColorByChange(event.target.value);
    },
    [onColorByChange]
  );

  const handleBulkAction = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      if (event.target.value) {
        onBulkAction(event.target.value);
        event.target.value = '';
      }
    },
    [onBulkAction]
  );

  return (
    <div className={styles.toolbar}>
      {/* Source Filter */}
      <div className={styles.filterGroup}>
        {SOURCE_OPTIONS.map((option) => (
          <button
            key={option.key}
            className={
              sourceFilter === option.key
                ? styles.filterButtonActive
                : styles.filterButton
            }
            onClick={() => onSourceFilterChange(option.key)}
          >
            {option.label}
          </button>
        ))}
      </div>

      {/* Color By */}
      <div className={styles.colorByGroup}>
        <span className={styles.colorByLabel}>Color by:</span>
        <select
          className={styles.colorBySelect}
          value={colorBy}
          onChange={handleColorByChange}
        >
          {COLOR_BY_OPTIONS.map((option) => (
            <option key={option.key} value={option.key}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      {/* Min Size Filter */}
      <div className={styles.minSizeGroup}>
        <span className={styles.minSizeLabel}>Min size (GB):</span>
        <input
          className={styles.minSizeInput}
          type="number"
          min={0}
          step={1}
          value={minSizeFilter || ''}
          placeholder="0"
          onChange={handleMinSizeChange}
        />
      </div>

      <div className={styles.spacer} />

      {/* Scan Now */}
      <button className={styles.scanButton} onClick={onScanNow}>
        Scan Now
      </button>

      {/* Bulk Actions */}
      {selectedCount > 0 && (
        <select
          className={styles.bulkButton}
          defaultValue=""
          onChange={handleBulkAction}
        >
          <option value="" disabled>
            Bulk Actions ({selectedCount})
          </option>
          <option value="delete">Delete Selected</option>
          <option value="search">Search & Replace</option>
          <option value="qualitySwap">Quality Swap</option>
        </select>
      )}
    </div>
  );
}

export default LibraryToolbar;
