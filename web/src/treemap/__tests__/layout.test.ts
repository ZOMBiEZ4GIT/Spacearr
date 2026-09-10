import { layoutTree, hitTest, resolvePath } from '../layout';
import type { TreeNode } from '../../api/types';

const leaf = (name: string, bytes: number, itemId: number): TreeNode => ({ name, bytes, children: null, leaf: { itemId, fileId: itemId, heat: 0.5, color: '#888', posterUrl: null, quality: null, codec: null, resolution: null, instanceId: 1, instanceName: 'R' } });
const tree: TreeNode = { name: 'Library', bytes: 100, children: [
  leaf('Big', 60, 1),
  { name: 'Show', bytes: 40, leaf: null, children: [ { name: 'Season 1', bytes: 40, leaf: null, children: [leaf('E1', 30, 2), leaf('E2', 10, 3)] } ] },
], leaf: null };

describe('layout', () => {
  it('lays out leaves proportionally within bounds and in draw order', () => {
    const rects = layoutTree(tree, 400, 300);
    const leaves = rects.filter((r) => !r.isGroup);
    expect(leaves.map((r) => r.node.name).sort()).toEqual(['Big', 'E1', 'E2']);
    for (const r of rects) { expect(r.x0).toBeGreaterThanOrEqual(0); expect(r.x1).toBeLessThanOrEqual(400); expect(r.y1).toBeLessThanOrEqual(300); }
    const area = (r: { x0: number; y0: number; x1: number; y1: number }) => (r.x1 - r.x0) * (r.y1 - r.y0);
    const big = leaves.find((r) => r.node.name === 'Big')!; const e2 = leaves.find((r) => r.node.name === 'E2')!;
    expect(area(big) / area(e2)).toBeGreaterThan(4);
    const groupIndex = rects.findIndex((r) => r.node.name === 'Show'); const e1Index = rects.findIndex((r) => r.node.name === 'E1');
    expect(groupIndex).toBeLessThan(e1Index);
  });

  it('hit test returns the deepest leaf, else group', () => {
    const rects = layoutTree(tree, 400, 300);
    const e1 = rects.find((r) => r.node.name === 'E1')!;
    const hit = hitTest(rects, (e1.x0 + e1.x1) / 2, (e1.y0 + e1.y1) / 2);
    expect(hit?.node.name).toBe('E1');
    const show = rects.find((r) => r.node.name === 'Show')!;
    const header = hitTest(rects, show.x0 + 5, show.y0 + 5);
    expect(header?.node.name).toBe('Show');
    expect(hitTest(rects, -5, -5)).toBeNull();
  });

  it('collapses groups that are too small to subdivide', () => {
    const rects = layoutTree(tree, 60, 30);
    const show = rects.find((r) => r.node.name === 'Show')!;
    expect(show.collapsed).toBe(true);
    expect(rects.some((r) => r.node.name === 'E1')).toBe(false);
  });
});

describe('resolvePath', () => {
  it('resolves a name path to nodes', () => {
    const p = resolvePath(tree, ['Show', 'Season 1']);
    expect(p.map((n) => n.name)).toEqual(['Library', 'Show', 'Season 1']);
  });

  it('stops at the deepest resolvable prefix when the tree changed', () => {
    expect(resolvePath(tree, ['Show', 'Season 9']).map((n) => n.name)).toEqual(['Library', 'Show']);
    expect(resolvePath(tree, ['Gone']).map((n) => n.name)).toEqual(['Library']);
  });

  it('never descends into a leaf', () => {
    expect(resolvePath(tree, ['Big']).map((n) => n.name)).toEqual(['Library']);
  });
});
