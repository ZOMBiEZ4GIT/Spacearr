import type { LayoutRect } from './layout';
import type { TreeLeaf } from '../api/types';
import type { PosterCache } from './posters';
import { formatBytes } from '../lib/format';

export interface PaintTheme { surface: string; surface2: string; ink: string; muted: string; accent: string; line: string }
export interface PaintOptions {
  dpr: number; colorFor: (leaf: TreeLeaf) => string; hovered: LayoutRect | null; focused: LayoutRect | null; selectedItemId: number | null;
  posters: PosterCache; showPosters: boolean; fontFamily: string; monoFamily: string;
  theme: PaintTheme;
}

const SCRIM = 34;

function ellipsis(ctx: CanvasRenderingContext2D, text: string, max: number): string {
  if (max <= 0) return '';
  if (ctx.measureText(text).width <= max) return text;
  let lo = 0, hi = text.length;
  while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (ctx.measureText(text.slice(0, mid) + '…').width <= max) lo = mid; else hi = mid - 1; }
  return lo === 0 ? '' : text.slice(0, lo) + '…';
}

export function paintBase(ctx: CanvasRenderingContext2D, rects: LayoutRect[], o: PaintOptions) {
  ctx.save();
  ctx.setTransform(o.dpr, 0, 0, o.dpr, 0, 0);
  ctx.clearRect(0, 0, ctx.canvas.width / o.dpr, ctx.canvas.height / o.dpr);
  for (const r of rects) {
    const w = r.x1 - r.x0, h = r.y1 - r.y0;
    if (w <= 0 || h <= 0) continue;
    if (r.isGroup) {
      // Nested groups sit on a darker plate so the hierarchy reads at a glance.
      ctx.fillStyle = r.depth >= 2 ? o.theme.surface2 : o.theme.surface;
      ctx.fillRect(r.x0, r.y0, w, h);
      ctx.strokeStyle = o.theme.line; ctx.lineWidth = 1; ctx.strokeRect(r.x0 + 0.5, r.y0 + 0.5, w - 1, h - 1);
      if (h >= 16 && w >= 30) {
        const indent = r.depth >= 2 ? 10 : 4;
        ctx.fillStyle = o.theme.ink; ctx.font = `600 12px ${o.fontFamily}`; ctx.textBaseline = 'middle';
        ctx.fillText(ellipsis(ctx, `${r.node.name}  ${formatBytes(r.node.bytes)}`, w - indent - 4), r.x0 + indent, r.y0 + 9);
      }
      continue;
    }
    const leaf = r.node.leaf;
    if (!leaf) continue;
    const color = o.colorFor(leaf);
    const poster = o.showPosters && w >= 96 && h >= 96 && leaf.itemId > 0 ? o.posters.get(leaf.itemId) : null;
    if (poster) {
      const scale = Math.max(w / poster.naturalWidth, h / poster.naturalHeight);
      const dw = poster.naturalWidth * scale, dh = poster.naturalHeight * scale;
      ctx.save(); ctx.beginPath(); ctx.rect(r.x0, r.y0, w, h); ctx.clip();
      ctx.drawImage(poster, r.x0 + (w - dw) / 2, r.y0 + (h - dh) / 2, dw, dh);
      ctx.globalAlpha = 0.62; ctx.fillStyle = color; ctx.fillRect(r.x0, r.y0, w, h); ctx.restore();
    } else {
      ctx.fillStyle = color; ctx.fillRect(r.x0, r.y0, w, h);
    }
    // Unmatched file (no library item behind it): subtle dotted border.
    if (leaf.itemId === 0 && leaf.fileId !== 0) {
      ctx.save(); ctx.setLineDash([3, 3]); ctx.strokeStyle = 'rgba(255,255,255,0.5)'; ctx.lineWidth = 1;
      ctx.strokeRect(r.x0 + 1.5, r.y0 + 1.5, w - 3, h - 3); ctx.restore();
    }
    // Unknown heat (probe error): diagonal hatch. Folded "Other (n files)" leaves are exempt.
    if (leaf.heat < 0 && leaf.fileId !== 0) {
      ctx.save(); ctx.beginPath(); ctx.rect(r.x0, r.y0, w, h); ctx.clip();
      ctx.strokeStyle = 'rgba(0,0,0,0.35)'; ctx.lineWidth = 1;
      for (let d = -h; d < w; d += 8) { ctx.beginPath(); ctx.moveTo(r.x0 + d, r.y0); ctx.lineTo(r.x0 + d + h, r.y1); ctx.stroke(); }
      ctx.restore();
    }
    if (w >= 64 && h >= 34) {
      // A gradient scrim rather than a flat bar, so the heat colour still reads under the label.
      const scrim = ctx.createLinearGradient(0, r.y1 - SCRIM, 0, r.y1);
      scrim.addColorStop(0, 'rgba(0,0,0,0)');
      scrim.addColorStop(1, 'rgba(0,0,0,0.72)');
      ctx.fillStyle = scrim; ctx.fillRect(r.x0, r.y1 - SCRIM, w, SCRIM);
      ctx.textBaseline = 'alphabetic';
      ctx.save();
      ctx.shadowColor = 'rgba(0,0,0,0.6)'; ctx.shadowBlur = 1; ctx.shadowOffsetY = 1;
      ctx.fillStyle = '#fff'; ctx.font = `600 12px ${o.fontFamily}`;
      ctx.fillText(ellipsis(ctx, r.node.name, w - 10), r.x0 + 5, r.y1 - 16);
      ctx.restore();
      ctx.font = `10.5px ${o.monoFamily}`; ctx.fillStyle = 'rgba(255,255,255,0.7)';
      ctx.fillText(formatBytes(r.node.bytes), r.x0 + 5, r.y1 - 4);
    }
  }
  ctx.restore();
}

export function paintOverlay(ctx: CanvasRenderingContext2D, rects: LayoutRect[], o: PaintOptions) {
  ctx.save();
  ctx.setTransform(o.dpr, 0, 0, o.dpr, 0, 0);
  ctx.clearRect(0, 0, ctx.canvas.width / o.dpr, ctx.canvas.height / o.dpr);
  const outline = (r: LayoutRect, color: string) => { ctx.strokeStyle = color; ctx.lineWidth = 2; ctx.strokeRect(r.x0 + 1, r.y0 + 1, r.x1 - r.x0 - 2, r.y1 - r.y0 - 2); };
  if (o.selectedItemId != null) { const sel = rects.find((r) => !r.isGroup && r.node.leaf?.itemId === o.selectedItemId); if (sel) outline(sel, o.theme.accent); }
  if (o.hovered) outline(o.hovered, o.theme.ink);
  if (o.focused) { ctx.setLineDash([4, 3]); outline(o.focused, o.theme.accent); ctx.setLineDash([]); }
  ctx.restore();
}
