import React, { useCallback } from 'react';
import { HierarchyRectangularNode } from 'd3-hierarchy';
import { TreemapItem, TreemapNode, ColorMode } from './treemapTypes';
import { getColor } from './treemapColors';
import styles from './TreemapBlock.css';

interface TreemapBlockProps {
  node: HierarchyRectangularNode<TreemapNode>;
  colorMode: ColorMode;
  onHover: (item: TreemapItem | null, x: number, y: number) => void;
  onClick: (item: TreemapItem) => void;
}

const MIN_WIDTH_FOR_LABEL = 40;
const MIN_HEIGHT_FOR_LABEL = 18;

function TreemapBlock({ node, colorMode, onHover, onClick }: TreemapBlockProps) {
  const { x0, y0, x1, y1, data } = node;
  const width = x1 - x0;
  const height = y1 - y0;
  const item = data.data;

  const backgroundColor = item
    ? getColor(item, colorMode)
    : '#555';

  const showLabel = width >= MIN_WIDTH_FOR_LABEL && height >= MIN_HEIGHT_FOR_LABEL;

  const handleMouseEnter = useCallback(
    (event: React.MouseEvent) => {
      if (item) {
        onHover(item, event.clientX, event.clientY);
      }
    },
    [item, onHover]
  );

  const handleMouseMove = useCallback(
    (event: React.MouseEvent) => {
      if (item) {
        onHover(item, event.clientX, event.clientY);
      }
    },
    [item, onHover]
  );

  const handleMouseLeave = useCallback(() => {
    onHover(null, 0, 0);
  }, [onHover]);

  const handleClick = useCallback(() => {
    if (item) {
      onClick(item);
    }
  }, [item, onClick]);

  if (width < 1 || height < 1) {
    return null;
  }

  return (
    <div
      className={styles.block}
      style={{
        left: x0,
        top: y0,
        width,
        height,
        backgroundImage: item?.posterUrl ? `url(${item.posterUrl})` : undefined,
        backgroundSize: 'cover',
        backgroundPosition: 'center top',
        backgroundColor: item?.posterUrl ? undefined : backgroundColor,
      }}
      onMouseEnter={handleMouseEnter}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      onClick={handleClick}
    >
      <div className={styles.colorOverlay} style={{ backgroundColor }} />
      {showLabel ? (
        <div className={styles.blockLabel}>
          {data.name}
        </div>
      ) : null}
    </div>
  );
}

export default React.memo(TreemapBlock);
