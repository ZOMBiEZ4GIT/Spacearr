import React, { useCallback, useEffect } from 'react';
import { LibraryItem, formatSize, formatBitrate, formatDuration } from '../LibraryItem';
import BitrateBadge from '../BitrateBadge';
import SourceBadge from '../SourceBadge';
import styles from './DetailDrawer.css';

interface DetailDrawerProps {
  item: LibraryItem | null;
  onClose: () => void;
}

function DetailDrawer({ item, onClose }: DetailDrawerProps) {
  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    },
    [onClose]
  );

  useEffect(() => {
    if (item) {
      document.addEventListener('keydown', handleKeyDown);

      return () => {
        document.removeEventListener('keydown', handleKeyDown);
      };
    }

    return undefined;
  }, [item, handleKeyDown]);

  if (!item) {
    return null;
  }

  const hasSuggestion =
    item.estimatedSavings != null && item.estimatedSavings > 0;

  return (
    <>
      <div className={styles.overlay} onClick={onClose} />

      <div className={styles.drawer}>
        <div className={styles.header}>
          <div className={styles.headerTitle}>{item.title}</div>
          <button className={styles.closeButton} onClick={onClose}>
            &times;
          </button>
        </div>

        <div className={styles.body}>
          {/* File Info */}
          <div className={styles.infoSection}>
            <div className={styles.sectionTitle}>File Info</div>
            <div className={styles.infoGrid}>
              <span className={styles.infoLabel}>Path</span>
              <span className={styles.infoValue}>{item.path}</span>

              <span className={styles.infoLabel}>Size</span>
              <span className={styles.infoValue}>{formatSize(item.size)}</span>

              <span className={styles.infoLabel}>Bitrate</span>
              <span className={styles.infoValue}>
                <BitrateBadge bitrate={item.bitrate} />
              </span>

              <span className={styles.infoLabel}>Codec</span>
              <span className={styles.infoValue}>{item.codec}</span>

              <span className={styles.infoLabel}>Resolution</span>
              <span className={styles.infoValue}>{item.resolution}</span>

              <span className={styles.infoLabel}>Duration</span>
              <span className={styles.infoValue}>
                {formatDuration(item.duration)}
              </span>

              <span className={styles.infoLabel}>Container</span>
              <span className={styles.infoValue}>{item.container}</span>
            </div>
          </div>

          {/* ARR Info */}
          <div className={styles.infoSection}>
            <div className={styles.sectionTitle}>ARR Info</div>
            <div className={styles.infoGrid}>
              <span className={styles.infoLabel}>Title</span>
              <span className={styles.infoValue}>{item.title}</span>

              {item.year != null && (
                <>
                  <span className={styles.infoLabel}>Year</span>
                  <span className={styles.infoValue}>{item.year}</span>
                </>
              )}

              {item.qualityProfile && (
                <>
                  <span className={styles.infoLabel}>Profile</span>
                  <span className={styles.infoValue}>
                    {item.qualityProfile}
                  </span>
                </>
              )}

              <span className={styles.infoLabel}>Monitored</span>
              <span className={styles.infoValue}>
                {item.monitored ? 'Yes' : 'No'}
              </span>

              <span className={styles.infoLabel}>Source</span>
              <span className={styles.infoValue}>
                <SourceBadge source={item.source} />
              </span>
            </div>
          </div>

          {/* Space Saver Suggestion */}
          {hasSuggestion && (
            <div className={styles.suggestion}>
              <div className={styles.suggestionTitle}>Space Saver</div>
              <div className={styles.suggestionText}>
                Replacing with {item.suggestedQuality} could save approximately{' '}
                {formatSize(item.estimatedSavings!)}.
              </div>
            </div>
          )}

          {/* Actions */}
          <div className={styles.actions}>
            <button className={styles.actionButton}>Search & Replace</button>
            <button className={styles.actionButton}>Quality Swap</button>
            <button className={styles.deleteButton}>Delete File</button>
          </div>
        </div>
      </div>
    </>
  );
}

export default DetailDrawer;
