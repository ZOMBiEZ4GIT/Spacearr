import { hierarchy, treemap, treemapSquarify, HierarchyRectangularNode } from 'd3-hierarchy';
import { TreemapItem, TreemapNode } from './treemapTypes';

// Maximum number of leaf nodes before aggregation kicks in
const MAX_VISIBLE_ITEMS = 2000;
const OTHER_LABEL = 'Other (small items)';

function aggregateSmallItems(items: TreemapItem[]): TreemapItem[] {
  if (items.length <= MAX_VISIBLE_ITEMS) {
    return items;
  }

  // Sort by size descending, keep the largest items, aggregate the rest
  const sorted = [...items].sort((a, b) => b.sizeBytes - a.sizeBytes);
  const kept = sorted.slice(0, MAX_VISIBLE_ITEMS - 1);
  const aggregated = sorted.slice(MAX_VISIBLE_ITEMS - 1);

  const totalSize = aggregated.reduce((sum, item) => sum + item.sizeBytes, 0);
  const avgBitrate = aggregated.length > 0
    ? aggregated.reduce((sum, item) => sum + item.bitrateBps, 0) / aggregated.length
    : 0;

  const otherItem: TreemapItem = {
    id: -1,
    title: `${OTHER_LABEL} (${aggregated.length} items)`,
    sizeBytes: totalSize,
    bitrateBps: Math.round(avgBitrate),
    codec: 'Mixed',
    resolution: 'Mixed',
    qualityProfile: 'Mixed',
    source: 'Mixed',
    parentGroup: null,
  };

  return [...kept, otherItem];
}

// Build hierarchical data structure for d3.treemap
export function buildTreemapData(
  items: TreemapItem[],
  drillGroup: string | null = null
): TreemapNode {
  const processedItems = aggregateSmallItems(items);

  // Filter to drilled-in group if applicable
  const filteredItems = drillGroup
    ? processedItems.filter((item) => item.parentGroup === drillGroup)
    : processedItems;

  // Group items by parentGroup
  const groups = new Map<string, TreemapItem[]>();

  for (const item of filteredItems) {
    const groupKey = item.parentGroup || item.title;

    if (!groups.has(groupKey)) {
      groups.set(groupKey, []);
    }

    groups.get(groupKey)!.push(item);
  }

  // Build hierarchy: root -> groups -> items
  const children: TreemapNode[] = [];

  for (const [groupName, groupItems] of groups) {
    if (groupItems.length === 1 && !groupItems[0].parentGroup) {
      // Single standalone item (no group)
      children.push({
        name: groupItems[0].title,
        value: Math.max(groupItems[0].sizeBytes, 1),
        data: groupItems[0],
      });
    } else {
      // Group with children
      children.push({
        name: groupName,
        children: groupItems.map((item) => ({
          name: item.title,
          value: Math.max(item.sizeBytes, 1),
          data: item,
        })),
      });
    }
  }

  return {
    name: drillGroup || 'All',
    children,
  };
}

// Compute treemap layout
export function computeTreemapLayout(
  rootData: TreemapNode,
  width: number,
  height: number
): HierarchyRectangularNode<TreemapNode> {
  const root = hierarchy(rootData)
    .sum((d) => d.value || 0)
    .sort((a, b) => (b.value || 0) - (a.value || 0));

  return treemap<TreemapNode>()
    .size([width, height])
    .paddingInner(2)
    .paddingOuter(2)
    .paddingTop(14)
    .tile(treemapSquarify)
    (root);
}

// Get all leaf nodes (actual items) from a layout
export function getLeafNodes(
  root: HierarchyRectangularNode<TreemapNode>
): HierarchyRectangularNode<TreemapNode>[] {
  const leaves: HierarchyRectangularNode<TreemapNode>[] = [];

  root.each((node) => {
    if (!node.children || node.children.length === 0) {
      leaves.push(node);
    }
  });

  return leaves;
}

// Get group nodes (depth 1) from a layout
export function getGroupNodes(
  root: HierarchyRectangularNode<TreemapNode>
): HierarchyRectangularNode<TreemapNode>[] {
  const groups: HierarchyRectangularNode<TreemapNode>[] = [];

  root.each((node) => {
    if (node.depth === 1 && node.children && node.children.length > 0) {
      groups.push(node);
    }
  });

  return groups;
}
