import { hierarchy, treemap, treemapSquarify, type HierarchyRectangularNode } from 'd3-hierarchy';
import type { TreeNode } from '../api/types';

export interface LayoutRect { x0: number; y0: number; x1: number; y1: number; depth: number; node: TreeNode; parent: LayoutRect | null; isGroup: boolean; collapsed?: boolean }
export interface Padding { outer: number; top: number; inner: number }
const MIN_GROUP = 24;

export function layoutTree(root: TreeNode, width: number, height: number, padding: Padding = { outer: 2, top: 18, inner: 1.5 }): LayoutRect[] {
  const h = hierarchy<TreeNode>(root, (n) => n.children ?? undefined).sum((n) => (n.children ? 0 : n.bytes)).sort((a, b) => (b.value ?? 0) - (a.value ?? 0));
  treemap<TreeNode>().tile(treemapSquarify).size([width, height]).paddingOuter(padding.outer).paddingInner(padding.inner).paddingTop((d) => (d.children ? padding.top : 0))(h);
  const out: LayoutRect[] = [];
  const walk = (n: HierarchyRectangularNode<TreeNode>, parent: LayoutRect | null) => {
    const isGroup = !!n.children && n.depth > 0;
    if (n.depth > 0) {
      const rect: LayoutRect = { x0: n.x0, y0: n.y0, x1: n.x1, y1: n.y1, depth: n.depth, node: n.data, parent, isGroup };
      out.push(rect);
      if (isGroup && (n.x1 - n.x0 < MIN_GROUP || n.y1 - n.y0 < MIN_GROUP)) { rect.collapsed = true; return; }
      n.children?.forEach((c) => walk(c, rect));
    } else n.children?.forEach((c) => walk(c, null));
  };
  walk(h as HierarchyRectangularNode<TreeNode>, null);
  return out;
}

export function hitTest(rects: LayoutRect[], x: number, y: number): LayoutRect | null {
  let best: LayoutRect | null = null;
  for (const r of rects) {
    if (x < r.x0 || x > r.x1 || y < r.y0 || y > r.y1) continue;
    if (!best || r.depth > best.depth || (r.depth === best.depth && !r.isGroup)) best = r;
  }
  return best;
}

/**
 * Resolves a zoom path expressed as node names against a (possibly refreshed) tree,
 * stopping at the deepest prefix that still exists. Always returns at least the root.
 */
export function resolvePath(root: TreeNode, names: string[]): TreeNode[] {
  const out: TreeNode[] = [root];
  let cur = root;
  for (const name of names) {
    const next = cur.children?.find((c) => c.name === name && c.children);
    if (!next) break;
    out.push(next);
    cur = next;
  }
  return out;
}
