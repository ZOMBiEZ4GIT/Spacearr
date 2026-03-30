import React from 'react';
import { formatBitrate, getBitrateLevel } from './LibraryItem';
import styles from './BitrateBadge.css';

interface BitrateBadgeProps {
  bitrate: number;
}

function BitrateBadge({ bitrate }: BitrateBadgeProps) {
  const level = getBitrateLevel(bitrate);

  return (
    <span className={styles[level]}>
      {formatBitrate(bitrate)}
    </span>
  );
}

export default BitrateBadge;
