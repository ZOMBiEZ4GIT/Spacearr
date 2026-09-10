import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { TreeLeaf, TreeNode } from '../api/types';
import { categoryColor, heatColor, supportsOklch } from '../lib/heat';
import { formatBytes } from '../lib/format';
import { hitTest, layoutTree, resolvePath, type LayoutRect } from './layout';
import { paintBase, paintOverlay, type PaintOptions, type PaintTheme } from './renderer';
import { PosterCache } from './posters';
import Tooltip, { type TooltipData } from './Tooltip';
import Breadcrumb from './Breadcrumb';
import s from './Treemap.module.css';

interface Props {
  tree: TreeNode | undefined;
  colorBy: string;
  heatMode?: string;
  selectedItemId: number | null;
  showPosters?: boolean;
  onSelect: (leaf: TreeLeaf | null, name: string) => void;
  onZoom?: (node: TreeNode, path: TreeNode[]) => void;
  onHover?: (leaf: TreeLeaf | null, name: string, rect: LayoutRect | null) => void;
}

const ZOOM_MS = 250;
const reduceMotion = () => typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
const cssVar = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

function readTheme(): PaintTheme & { fontFamily: string; monoFamily: string } {
  return {
    surface: cssVar('--surface') || '#fff',
    surface2: cssVar('--surface-2') || '#eee',
    ink: cssVar('--ink') || '#000',
    muted: cssVar('--muted') || '#666',
    accent: cssVar('--accent') || '#b07410',
    line: cssVar('--line') || '#ccc',
    fontFamily: cssVar('--font-ui') || 'sans-serif',
    monoFamily: cssVar('--font-mono') || 'monospace',
  };
}

/** Reads the CSS custom properties once, and again whenever the theme flips. */
function useThemeVars() {
  const [vars, setVars] = useState(readTheme);
  useEffect(() => {
    const update = () => setVars(readTheme());
    update();
    const mo = new MutationObserver(update);
    mo.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'class', 'style'] });
    const mq = matchMedia('(prefers-color-scheme: dark)');
    mq.addEventListener('change', update);
    return () => { mo.disconnect(); mq.removeEventListener('change', update); };
  }, []);
  return vars;
}

export default function Treemap({ tree, colorBy, selectedItemId, showPosters = true, onSelect, onZoom, onHover }: Props) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const baseRef = useRef<HTMLCanvasElement>(null);
  const overlayRef = useRef<HTMLCanvasElement>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });
  // The zoom is stored as node names, not node objects, so it survives a refetch that replaces the tree.
  const [zoomNames, setZoomNames] = useState<string[]>([]);
  const [hovered, setHovered] = useState<LayoutRect | null>(null);
  const [focusIndex, setFocusIndex] = useState<number>(-1);
  const [tooltip, setTooltip] = useState<TooltipData | null>(null);
  const [posterTick, bump] = useState(0);
  const lastHitRef = useRef<LayoutRect | null>(null);
  const posters = useMemo(() => new PosterCache(() => bump((n) => n + 1)), []);
  const animRef = useRef<{ from: Map<TreeNode, LayoutRect>; start: number } | null>(null);
  const vars = useThemeVars();

  // A new tree keeps the zoom: re-resolve the deepest prefix of the name path that still exists.
  const path = useMemo<TreeNode[]>(() => (tree ? resolvePath(tree, zoomNames) : []), [tree, zoomNames]);
  const zoomRoot = path[path.length - 1];

  useEffect(() => {
    setFocusIndex(-1);
    setHovered(null);
    setTooltip(null);
    lastHitRef.current = null;
  }, [tree]);

  useEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    // Keep the fractional CSS size: the backing store is rounded, so the canvas is never stretched by more than a sub-pixel.
    const ro = new ResizeObserver(([e]) => setSize({ width: e.contentRect.width, height: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const rects = useMemo(
    () => (zoomRoot && size.width > 0 && size.height > 0 ? layoutTree(zoomRoot, size.width, size.height) : []),
    [zoomRoot, size],
  );
  const leaves = useMemo(() => rects.filter((r) => !r.isGroup), [rects]);
  /** Leaves in reading order, for the keyboard focus ring. */
  const navLeaves = useMemo(
    () => [...leaves].sort((a, b) => Math.round(a.y0 / 8) - Math.round(b.y0 / 8) || a.x0 - b.x0),
    [leaves],
  );

  const colorFor = useCallback((leaf: TreeLeaf) => {
    if (!supportsOklch) return leaf.color;
    switch (colorBy) {
      case 'quality': return categoryColor(leaf.quality);
      case 'codec': return categoryColor(leaf.codec);
      case 'resolution': return categoryColor(leaf.resolution);
      case 'instance': return categoryColor(leaf.instanceName);
      case 'duplicates': return leaf.color;
      default: return heatColor(leaf.heat);
    }
  }, [colorBy]);

  const dpr = typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1;

  // Options for the base canvas: deliberately free of hover/focus/selection so hovering never repaints it.
  // posterTick is not read here; it is a dependency so a finished poster load triggers a repaint.
  const baseOpts = useMemo<PaintOptions>(() => ({
    dpr,
    colorFor,
    hovered: null,
    focused: null,
    selectedItemId: null,
    posters,
    showPosters,
    fontFamily: vars.fontFamily,
    monoFamily: vars.monoFamily,
    theme: { surface: vars.surface, surface2: vars.surface2, ink: vars.ink, muted: vars.muted, accent: vars.accent, line: vars.line },
  }), [dpr, colorFor, posters, showPosters, vars, posterTick]); // eslint-disable-line react-hooks/exhaustive-deps

  const focused = focusIndex >= 0 ? navLeaves[focusIndex] ?? null : null;

  const overlayOpts = useMemo<PaintOptions>(
    () => ({ ...baseOpts, hovered, focused, selectedItemId }),
    [baseOpts, hovered, focused, selectedItemId],
  );

  // Base paint, with the zoom animation.
  useEffect(() => {
    const canvas = baseRef.current;
    if (!canvas || size.width === 0 || size.height === 0) return;
    const bw = Math.round(size.width * dpr), bh = Math.round(size.height * dpr);
    if (canvas.width !== bw || canvas.height !== bh) { canvas.width = bw; canvas.height = bh; }
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const anim = animRef.current;
    if (!anim || reduceMotion()) { animRef.current = null; paintBase(ctx, rects, baseOpts); return; }
    let raf = 0;
    let cancelled = false;
    const step = (now: number) => {
      if (cancelled) return;
      const t = Math.min(1, (now - anim.start) / ZOOM_MS);
      // The anim record is only cleared once it has actually finished, so a repaint mid-zoom resumes it.
      if (t >= 1) { animRef.current = null; paintBase(ctx, rects, baseOpts); return; }
      const e = 1 - Math.pow(1 - t, 3);
      const interpolated = rects.map((r) => {
        const f = anim.from.get(r.node);
        return f
          ? { ...r, x0: f.x0 + (r.x0 - f.x0) * e, y0: f.y0 + (r.y0 - f.y0) * e, x1: f.x1 + (r.x1 - f.x1) * e, y1: f.y1 + (r.y1 - f.y1) * e }
          : r;
      });
      paintBase(ctx, interpolated, baseOpts);
      raf = requestAnimationFrame(step);
    };
    raf = requestAnimationFrame(step);
    return () => { cancelled = true; cancelAnimationFrame(raf); };
  }, [rects, size, baseOpts, dpr]);

  // Overlay paint: hover, focus and selection only.
  useEffect(() => {
    const canvas = overlayRef.current;
    if (!canvas || size.width === 0 || size.height === 0) return;
    const w = Math.round(size.width * dpr);
    const h = Math.round(size.height * dpr);
    if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
    const ctx = canvas.getContext('2d');
    if (ctx) paintOverlay(ctx, rects, overlayOpts);
  }, [rects, size, overlayOpts, dpr]);

  const zoomTo = useCallback((names: string[]) => {
    const from = new Map<TreeNode, LayoutRect>();
    rects.forEach((r) => from.set(r.node, r));
    animRef.current = { from, start: performance.now() };
    setZoomNames(names);
    setHovered(null);
    setTooltip(null);
    setFocusIndex(-1);
    lastHitRef.current = null;
    // Zooming is navigation, not selection: the current selection is left alone.
    if (onZoom && tree) {
      const next = resolvePath(tree, names);
      onZoom(next[next.length - 1], next);
    }
  }, [rects, onZoom, tree]);

  const pointAt = (e: React.MouseEvent) => {
    const b = (e.currentTarget as HTMLElement).getBoundingClientRect();
    return { x: e.clientX - b.left, y: e.clientY - b.top };
  };

  const onMove = (e: React.MouseEvent) => {
    const { x, y } = pointAt(e);
    const hit = hitTest(rects, x, y);
    setHovered((prev) => (prev === hit ? prev : hit));
    setTooltip(hit ? { name: hit.node.name, bytes: hit.node.bytes, leaf: hit.node.leaf, x, y } : null);
    if (lastHitRef.current !== hit) {
      lastHitRef.current = hit;
      onHover?.(hit?.node.leaf ?? null, hit?.node.name ?? '', hit);
    }
  };

  const clearHover = () => {
    setHovered(null);
    setTooltip(null);
    if (lastHitRef.current !== null) {
      lastHitRef.current = null;
      onHover?.(null, '', null);
    }
  };

  const onClick = (e: React.MouseEvent) => {
    const { x, y } = pointAt(e);
    const hit = hitTest(rects, x, y);
    if (!hit) return;
    if (hit.isGroup) {
      // The hit may be several levels below the current zoom root (a nested group header is drawn
      // inside its parent), so walk the rect's ancestor chain rather than appending a single name.
      const branch: string[] = [];
      for (let r: LayoutRect | null = hit; r; r = r.parent) branch.unshift(r.node.name);
      zoomTo([...zoomNames.slice(0, path.length - 1), ...branch]);
      return;
    }
    onSelect(hit.node.leaf, hit.node.name);
  };

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape' && path.length > 1) { e.preventDefault(); zoomTo(zoomNames.slice(0, path.length - 2)); return; }
    if (navLeaves.length === 0) return;
    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
      e.preventDefault();
      setFocusIndex((i) => Math.min(navLeaves.length - 1, i + 1));
    } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
      e.preventDefault();
      setFocusIndex((i) => (i < 0 ? 0 : Math.max(0, i - 1)));
    } else if (e.key === 'Enter' && focusIndex >= 0) {
      e.preventDefault();
      const r = navLeaves[focusIndex];
      if (r) onSelect(r.node.leaf, r.node.name);
    }
  };

  // Only an actually-empty tree gets the empty state; while `tree` is undefined the page owns the loading UI.
  const empty = !!tree && (!tree.children || tree.children.length === 0);
  return (
    <div className={s.root}>
      {path.length > 1 && <Breadcrumb path={path} onZoomTo={(i) => zoomTo(zoomNames.slice(0, i))} />}
      <div className={s.wrap} ref={wrapRef}>
      <canvas ref={baseRef} className={s.canvas} aria-hidden="true" />
      <canvas
        ref={overlayRef}
        className={`${s.overlay} ${s.focusable}`}
        tabIndex={0}
        role="img"
        aria-label={zoomRoot ? `Treemap of ${zoomRoot.name}, ${formatBytes(zoomRoot.bytes)}, ${leaves.length} blocks. Use arrow keys to move, Enter to select, Escape to zoom out.` : 'Treemap'}
        style={{ pointerEvents: 'auto' }}
        onMouseMove={onMove}
        onMouseLeave={clearHover}
        onClick={onClick}
        onKeyDown={onKey}
      />
      {tooltip && <Tooltip data={tooltip} bounds={size} />}
      <div className={s.live} aria-live="polite">{focused ? `${focused.node.name}, ${formatBytes(focused.node.bytes)}` : ''}</div>
      {empty && <div className={s.empty}>Nothing scanned yet. Add a library folder and run a scan.</div>}
      </div>
    </div>
  );
}
