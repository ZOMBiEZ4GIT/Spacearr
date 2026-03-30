import React, { useCallback } from 'react';
import styles from './TreemapBreadcrumb.css';

interface TreemapBreadcrumbProps {
  currentGroup: string | null;
  currentSubGroup?: string | null;
  onNavigate: (group: string | null, subGroup?: string | null) => void;
}

function TreemapBreadcrumb({ currentGroup, currentSubGroup, onNavigate }: TreemapBreadcrumbProps) {
  const handleRootClick = useCallback(() => {
    onNavigate(null);
  }, [onNavigate]);

  const handleGroupClick = useCallback(() => {
    onNavigate(currentGroup, null);
  }, [onNavigate, currentGroup]);

  return (
    <div className={styles.breadcrumb}>
      {currentGroup ? (
        <>
          <button
            className={styles.segment}
            onClick={handleRootClick}
            type="button"
          >
            All
          </button>
          <span className={styles.separator}>&rsaquo;</span>
          {currentSubGroup ? (
            <>
              <button
                className={styles.segment}
                onClick={handleGroupClick}
                type="button"
              >
                {currentGroup}
              </button>
              <span className={styles.separator}>&rsaquo;</span>
              <span className={styles.current}>{currentSubGroup}</span>
            </>
          ) : (
            <span className={styles.current}>{currentGroup}</span>
          )}
        </>
      ) : (
        <span className={styles.current}>All</span>
      )}
    </div>
  );
}

export default TreemapBreadcrumb;
