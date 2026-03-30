import React, { useMemo, useState, useCallback } from 'react';
import useMeasure from 'react-use-measure';
import { TreemapItem, ColorMode, TooltipState } from './treemapTypes';
import { buildTreemapData, computeTreemapLayout, getLeafNodes, getGroupNodes } from './treemapData';
import TreemapBlock from './TreemapBlock';
import TreemapTooltip from './TreemapTooltip';
import TreemapBreadcrumb from './TreemapBreadcrumb';
import styles from './Treemap.css';

interface TreemapProps {
  items: TreemapItem[];
  colorMode: ColorMode;
  onItemClick?: (item: TreemapItem) => void;
}

function Treemap({ items, colorMode, onItemClick }: TreemapProps) {
  const [measureRef, bounds] = useMeasure();
  const [currentGroup, setCurrentGroup] = useState<string | null>(null);
  const [currentSubGroup, setCurrentSubGroup] = useState<string | null>(null);
  const [tooltip, setTooltip] = useState<TooltipState>({
    item: null,
    x: 0,
    y: 0,
    visible: false,
  });

  const { width, height } = bounds;

  const layout = useMemo(() => {
    if (!items.length || width < 1 || height < 1) {
      return null;
    }

    const rootData = buildTreemapData(items, currentGroup, currentSubGroup);

    return computeTreemapLayout(rootData, width, height);
  }, [items, width, height, currentGroup, currentSubGroup]);

  const leafNodes = useMemo(() => {
    if (!layout) {
      return [];
    }

    return getLeafNodes(layout);
  }, [layout]);

  const groupNodes = useMemo(() => {
    if (!layout || (currentGroup && currentSubGroup)) {
      return [];
    }

    return getGroupNodes(layout);
  }, [layout, currentGroup, currentSubGroup]);

  const handleHover = useCallback(
    (item: TreemapItem | null, x: number, y: number) => {
      setTooltip({
        item,
        x,
        y,
        visible: item !== null,
      });
    },
    []
  );

  const handleItemClick = useCallback(
    (item: TreemapItem) => {
      if (onItemClick) {
        onItemClick(item);
      }
    },
    [onItemClick]
  );

  const handleGroupClick = useCallback(
    (groupName: string) => {
      if (currentGroup) {
        // Already drilled into a series, now drilling into a season
        setCurrentSubGroup(groupName);
      } else {
        setCurrentGroup(groupName);
      }
    },
    [currentGroup]
  );

  const handleBreadcrumbNavigate = useCallback(
    (group: string | null, subGroup?: string | null) => {
      if (group === null) {
        // Navigate to root
        setCurrentGroup(null);
        setCurrentSubGroup(null);
      } else if (subGroup === undefined || subGroup === null) {
        // Navigate to series level
        setCurrentGroup(group);
        setCurrentSubGroup(null);
      } else {
        setCurrentGroup(group);
        setCurrentSubGroup(subGroup);
      }
    },
    []
  );

  return (
    <div className={styles.container}>
      <TreemapBreadcrumb
        currentGroup={currentGroup}
        currentSubGroup={currentSubGroup}
        onNavigate={handleBreadcrumbNavigate}
      />

      <div
        className={styles.treemap}
        ref={measureRef}
      >
        {!items.length ? (
          <div className={styles.empty}>No items to display</div>
        ) : null}

        {leafNodes.map((node) => {
          const item = node.data.data;
          const key = item ? item.id : node.data.name;

          return (
            <TreemapBlock
              key={key}
              node={node}
              colorMode={colorMode}
              onHover={handleHover}
              onClick={handleItemClick}
            />
          );
        })}

        {groupNodes.map((node) => (
          <div
            key={`group-${node.data.name}`}
            className={styles.groupOverlay}
            style={{
              left: node.x0,
              top: node.y0,
              width: node.x1 - node.x0,
              height: node.y1 - node.y0,
            }}
          >
            <div className={styles.groupName}>{node.data.name}</div>
            <div
              className={styles.groupClickArea}
              onClick={() => handleGroupClick(node.data.name)}
              title={`Drill into ${node.data.name}`}
            />
          </div>
        ))}
      </div>

      <TreemapTooltip
        item={tooltip.item}
        x={tooltip.x}
        y={tooltip.y}
        visible={tooltip.visible}
      />
    </div>
  );
}

export default Treemap;
