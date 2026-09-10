import { fireEvent, render, screen } from '@testing-library/react';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import Treemap from '../Treemap';
import { layoutTree } from '../layout';
import type { TreeNode } from '../../api/types';

const W = 400, H = 300;

const leaf = (name: string, bytes: number, itemId: number): TreeNode => ({
  name, bytes, children: null,
  leaf: { itemId, fileId: itemId, heat: 0.5, color: '#888', posterUrl: null, quality: null, codec: null, resolution: null, instanceId: 1, instanceName: 'R' },
});
const makeTree = (bigBytes: number): TreeNode => ({
  name: 'Library', bytes: 60 + bigBytes, leaf: null,
  children: [
    leaf('Big', bigBytes, 1),
    { name: 'Show', bytes: 60, leaf: null, children: [{ name: 'Season 1', bytes: 60, leaf: null, children: [leaf('E1', 40, 2), leaf('E2', 20, 3)] }] },
  ],
});

beforeAll(() => {
  // jsdom has neither ResizeObserver nor a canvas 2D context; the component tolerates a null context.
  class RO {
    constructor(private cb: ResizeObserverCallback) {}
    observe() { this.cb([{ contentRect: { width: W, height: H } } as unknown as ResizeObserverEntry], this as unknown as ResizeObserver); }
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', RO);
  vi.stubGlobal('matchMedia', (query: string) => ({ matches: false, media: query, onchange: null, addEventListener() {}, removeEventListener() {}, addListener() {}, removeListener() {}, dispatchEvent: () => false }));
  HTMLCanvasElement.prototype.getContext = () => null;
});

const canvasOf = () => document.querySelector('canvas[role="img"]')!;
/** Clicks the centre of a rect from the same layout the component computes. */
const clickNode = (tree: TreeNode, name: string, header = false) => {
  const r = layoutTree(tree, W, H).find((x) => x.node.name === name)!;
  const [x, y] = header ? [r.x0 + 5, r.y0 + 5] : [(r.x0 + r.x1) / 2, (r.y0 + r.y1) / 2];
  fireEvent.click(canvasOf(), { clientX: x, clientY: y });
};

describe('Treemap', () => {
  it('shows the empty state only for a tree that is really empty', () => {
    const { rerender } = render(<Treemap tree={undefined} colorBy="heat" selectedItemId={null} onSelect={() => {}} />);
    expect(screen.queryByText(/Nothing scanned yet/)).toBeNull();

    rerender(<Treemap tree={{ name: 'Library', bytes: 0, children: [], leaf: null }} colorBy="heat" selectedItemId={null} onSelect={() => {}} />);
    expect(screen.getByText(/Nothing scanned yet/)).toBeTruthy();
  });

  it('zooming reports through onZoom and never clears the selection', () => {
    const tree = makeTree(300);
    const onSelect = vi.fn();
    const onZoom = vi.fn();
    render(<Treemap tree={tree} colorBy="heat" selectedItemId={1} onSelect={onSelect} onZoom={onZoom} />);

    clickNode(tree, 'Show', true);
    expect(onSelect).not.toHaveBeenCalled();
    expect(onZoom).toHaveBeenCalledTimes(1);
    expect(onZoom.mock.calls[0][1].map((n: TreeNode) => n.name)).toEqual(['Library', 'Show']);
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Treemap of Show');
  });

  it('keeps the zoom when the tree is replaced by a refetch', () => {
    const tree = makeTree(300);
    const { rerender } = render(<Treemap tree={tree} colorBy="heat" selectedItemId={null} onSelect={() => {}} />);
    clickNode(tree, 'Show', true);
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Treemap of Show');

    // A refetch: same shape, brand new objects, different sizes.
    rerender(<Treemap tree={makeTree(500)} colorBy="heat" selectedItemId={null} onSelect={() => {}} />);
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Treemap of Show');
    expect(screen.getByRole('navigation', { name: 'Treemap zoom' }).textContent).toContain('Library');
  });

  it('Escape zooms out one level', () => {
    const tree = makeTree(300);
    render(<Treemap tree={tree} colorBy="heat" selectedItemId={null} onSelect={() => {}} />);
    clickNode(tree, 'Show', true);
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Treemap of Show');
    fireEvent.keyDown(canvasOf(), { key: 'Escape' });
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Treemap of Library');
  });

  it('fires onHover only when the hovered rect changes', () => {
    const tree = makeTree(300);
    const onHover = vi.fn();
    render(<Treemap tree={tree} colorBy="heat" selectedItemId={null} onSelect={() => {}} onHover={onHover} />);
    const big = layoutTree(tree, W, H).find((r) => r.node.name === 'Big')!;
    const cx = (big.x0 + big.x1) / 2, cy = (big.y0 + big.y1) / 2;
    fireEvent.mouseMove(canvasOf(), { clientX: cx, clientY: cy });
    fireEvent.mouseMove(canvasOf(), { clientX: cx + 1, clientY: cy + 1 });
    fireEvent.mouseMove(canvasOf(), { clientX: cx + 2, clientY: cy - 1 });
    expect(onHover).toHaveBeenCalledTimes(1);
    expect(onHover.mock.calls[0][1]).toBe('Big');
  });
});
