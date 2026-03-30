import React from 'react';
import styles from './SourceBadge.css';

interface SourceBadgeProps {
  source: 'radarr' | 'sonarr';
}

function SourceBadge({ source }: SourceBadgeProps) {
  const label = source === 'radarr' ? 'Radarr' : 'Sonarr';

  return (
    <span className={styles[source]}>
      {label}
    </span>
  );
}

export default SourceBadge;
