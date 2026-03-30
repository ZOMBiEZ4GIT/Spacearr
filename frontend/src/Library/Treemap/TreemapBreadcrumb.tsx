import React, { useCallback } from 'react';
import styles from './TreemapBreadcrumb.css';

interface TreemapBreadcrumbProps {
  currentGroup: string | null;
  onNavigate: (group: string | null) => void;
}

function TreemapBreadcrumb({ currentGroup, onNavigate }: TreemapBreadcrumbProps) {
  const handleRootClick = useCallback(() => {
    onNavigate(null);
  }, [onNavigate]);

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
          <span className={styles.current}>{currentGroup}</span>
        </>
      ) : (
        <span className={styles.current}>All</span>
      )}
    </div>
  );
}

export default TreemapBreadcrumb;
