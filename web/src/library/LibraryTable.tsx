import { useEffect, useRef, useState } from 'react';
import type { LibraryItem } from '../api/types';
import { formatBytes } from '../lib/format';
import { heatColor } from '../lib/heat';
import type { LibraryUiParams } from './useLibraryParams';
import s from './library.module.css';

const ROW = 40;
const cols: [LibraryUiParams['sort'] | null, string, string][] = [['title', 'Title', ''], ['size', 'Size', s.num], ['heat', 'Heat', s.num], ['quality', 'Quality', ''], [null, 'Codec', ''], [null, 'Resolution', ''], [null, 'Connection', '']];

export const episodeLabel = (i: Pick<LibraryItem, 'seriesTitle' | 'seasonNumber' | 'episodes'>) =>
  `${i.seriesTitle} · S${String(i.seasonNumber).padStart(2, '0')}E${(i.episodes ?? '').split(',').map((e) => e.trim().padStart(2, '0')).join('-E')}`;

export default function LibraryTable({ items, total, hasMore, selected, selectedFileId, sort, order, onSort, onSelect, onMore }: { items: LibraryItem[]; total: number; hasMore: boolean; selected: number | null; selectedFileId: number | null; sort: string; order: string; onSort: (c: LibraryUiParams['sort']) => void; onSelect: (i: LibraryItem) => void; onMore: () => void }) {
  const wrap = useRef<HTMLDivElement>(null);
  const head = useRef<HTMLTableSectionElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [height, setHeight] = useState(600);
  // The header is sticky, so it covers the top `headH` pixels of the scroll port: rows under it
  // are not really visible, and both the window and scroll-into-view have to allow for that.
  const [headH, setHeadH] = useState(0);
  useEffect(() => {
    const el = wrap.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => { setHeight(e.contentRect.height); setHeadH(head.current?.offsetHeight ?? 0); });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  useEffect(() => {
    if (selected == null || !wrap.current) return;
    const idx = items.findIndex((i) => i.itemId === selected);
    if (idx < 0) return;
    const el = wrap.current;
    const top = headH + idx * ROW;
    if (top < el.scrollTop + headH || top + ROW > el.scrollTop + el.clientHeight) {
      el.scrollTo({ top: Math.max(0, idx * ROW - (el.clientHeight - headH) / 2) });
    }
  }, [selected, items, headH]);
  const view = Math.max(0, height - headH);
  const start = Math.max(0, Math.floor(scrollTop / ROW) - 20);
  const end = Math.min(items.length, Math.ceil((scrollTop + view) / ROW) + 20);
  const isSelected = (i: LibraryItem) => (i.itemId !== 0 ? i.itemId === selected : i.fileId === selectedFileId);
  return (
    <div className={s.tableWrap} ref={wrap} onScroll={(e) => setScrollTop((e.target as HTMLDivElement).scrollTop)}>
      <table className={s.table} role="grid" aria-rowcount={total}>
        <thead ref={head}>
          <tr role="row">
            {cols.map(([key, label, cls]) => (
              <th key={label} className={cls} role="columnheader" aria-sort={key === sort ? (order === 'asc' ? 'ascending' : 'descending') : undefined}>
                {key
                  ? <button type="button" onClick={() => onSort(key)}>{label}{key === sort ? (order === 'asc' ? ' ↑' : ' ↓') : ''}</button>
                  : <span>{label}</span>}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {start > 0 && <tr aria-hidden="true" style={{ height: start * ROW }}><td colSpan={7} /></tr>}
          {items.slice(start, end).map((i) => (
            <tr
              key={`${i.itemId}-${i.fileId}`}
              role="row"
              tabIndex={0}
              aria-selected={isSelected(i)}
              onClick={() => onSelect(i)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelect(i); } }}
            >
              <td role="gridcell">{i.kind === 'episode' ? episodeLabel(i) : i.year ? `${i.title} (${i.year})` : i.title}
                <span className={s.sub}>{i.kind === 'episode' ? i.title : i.path.split(/[/\\]/).pop()}</span></td>
              <td role="gridcell" className={s.num}>{formatBytes(i.sizeBytes)}</td>
              <td role="gridcell" className={s.num}>{i.heat >= 0 ? <><span className={s.swatch} style={{ background: heatColor(i.heat) }} />{Math.round(i.heat * 100)}</> : '—'}</td>
              <td role="gridcell">{i.qualityName ?? '—'}</td>
              <td role="gridcell">{i.videoCodec?.toUpperCase() ?? '—'}{i.hdrFormat ? ` · ${i.hdrFormat}` : ''}</td>
              <td role="gridcell">{i.resolution ?? '—'}</td>
              <td role="gridcell">{i.instanceName}</td>
            </tr>
          ))}
          {end < items.length && <tr aria-hidden="true" style={{ height: (items.length - end) * ROW }}><td colSpan={7} /></tr>}
        </tbody>
      </table>
      {/* Driven by the infinite query's own hasNextPage, not a comparison against `total` - a
          page shorter than requested is always the last one even if `total` disagrees (e.g. it
          changed between requests), and comparing lengths here could otherwise show a button
          that calls onMore for a page the query has already decided doesn't exist. */}
      {hasMore && <div style={{ padding: 10, textAlign: 'center' }}><span className="muted">Showing {items.length.toLocaleString()} of {total.toLocaleString()} · </span><button className="btn" onClick={onMore}>Load more</button></div>}
    </div>
  );
}
