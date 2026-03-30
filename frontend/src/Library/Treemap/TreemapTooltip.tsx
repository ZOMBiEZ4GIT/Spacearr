import React from 'react';
import { TreemapItem } from './treemapTypes';
import styles from './TreemapTooltip.css';

interface TreemapTooltipProps {
  item: TreemapItem | null;
  x: number;
  y: number;
  visible: boolean;
}

function formatBytes(bytes: number): string {
  if (bytes === 0) {
    return '0 B';
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const k = 1024;
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  const value = bytes / Math.pow(k, i);

  return `${value.toFixed(i > 1 ? 2 : 0)} ${units[i]}`;
}

function formatBitrate(bps: number): string {
  const mbps = bps / 1_000_000;

  if (mbps < 1) {
    return `${(bps / 1_000).toFixed(0)} Kbps`;
  }

  return `${mbps.toFixed(1)} Mbps`;
}

function TreemapTooltip({ item, x, y, visible }: TreemapTooltipProps) {
  if (!visible || !item) {
    return null;
  }

  // Offset tooltip from cursor
  const offsetX = 12;
  const offsetY = 12;

  return (
    <div
      className={styles.tooltip}
      style={{
        left: x + offsetX,
        top: y + offsetY,
      }}
    >
      <div className={styles.title}>{item.title}</div>

      <div className={styles.row}>
        <span className={styles.label}>Size</span>
        <span className={styles.value}>{formatBytes(item.sizeBytes)}</span>
      </div>

      <div className={styles.row}>
        <span className={styles.label}>Bitrate</span>
        <span className={styles.value}>{formatBitrate(item.bitrateBps)}</span>
      </div>

      <div className={styles.row}>
        <span className={styles.label}>Quality</span>
        <span className={styles.value}>{item.qualityProfile}</span>
      </div>

      <div className={styles.row}>
        <span className={styles.label}>Codec</span>
        <span className={styles.value}>{item.codec}</span>
      </div>

      <div className={styles.row}>
        <span className={styles.label}>Resolution</span>
        <span className={styles.value}>{item.resolution}</span>
      </div>
    </div>
  );
}

export default TreemapTooltip;
