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

export default function LibraryTable({ items, total, selected, selectedFileId, sort, order, onSort, onSelect, onMore }: { items: LibraryItem[]; total: number; selected: number | null; selectedFileId: number | null; sort: string; order: string; onSort: (c: LibraryUiParams['sort']) => void; onSelect: (i: LibraryItem) => void; onMore: () => void }) {
  const wrap = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [height, setHeight] = useState(600);
  useEffect(() => { const el = wrap.current; if (!el) return; const ro = new ResizeObserver(([e]) => setHeight(e.contentRect.height)); ro.observe(el); return () => ro.disconnect(); }, []);
  useEffect(() => {
    if (selected == null || !wrap.current) return;
    const idx = items.findIndex((i) => i.itemId === selected);
    if (idx < 0) return;
    const top = idx * ROW; const el = wrap.current;
    if (top < el.scrollTop || top + ROW > el.scrollTop + el.clientHeight) el.scrollTo({ top: top - el.clientHeight / 2 });
  }, [selected, items]);
  const start = Math.max(0, Math.floor(scrollTop / ROW) - 20);
  const end = Math.min(items.length, Math.ceil((scrollTop + height) / ROW) + 20);
  return (
    <div className={s.tableWrap} ref={wrap} onScroll={(e) => setScrollTop((e.target as HTMLDivElement).scrollTop)}>
      <table className={s.table} aria-rowcount={total}>
        <thead><tr>{cols.map(([key, label, cls]) => <th key={label} className={cls} onClick={() => key && onSort(key)} aria-sort={key === sort ? (order === 'asc' ? 'ascending' : 'descending') : undefined}>{label}{key === sort ? (order === 'asc' ? ' ↑' : ' ↓') : ''}</th>)}</tr></thead>
        <tbody>
          {start > 0 && <tr style={{ height: start * ROW }}><td colSpan={7} /></tr>}
          {items.slice(start, end).map((i) => (
            <tr key={`${i.itemId}-${i.fileId}`} aria-selected={i.itemId !== 0 ? i.itemId === selected : i.fileId === selectedFileId} onClick={() => onSelect(i)}>
              <td>{i.kind === 'episode' ? episodeLabel(i) : i.year ? `${i.title} (${i.year})` : i.title}
                <span className={s.sub}>{i.kind === 'episode' ? i.title : i.path.split(/[/\\]/).pop()}</span></td>
              <td className={s.num}>{formatBytes(i.sizeBytes)}</td>
              <td className={s.num}>{i.heat >= 0 ? <><span className={s.swatch} style={{ background: heatColor(i.heat) }} />{Math.round(i.heat * 100)}</> : '—'}</td>
              <td>{i.qualityName ?? '—'}</td>
              <td>{i.videoCodec?.toUpperCase() ?? '—'}{i.hdrFormat ? ` · ${i.hdrFormat}` : ''}</td>
              <td>{i.resolution ?? '—'}</td>
              <td>{i.instanceName}</td>
            </tr>
          ))}
          {end < items.length && <tr style={{ height: (items.length - end) * ROW }}><td colSpan={7} /></tr>}
        </tbody>
      </table>
      {items.length < total && <div style={{ padding: 10, textAlign: 'center' }}><span className="muted">Showing {items.length.toLocaleString()} of {total.toLocaleString()} · </span><button className="btn" onClick={onMore}>Load more</button></div>}
    </div>
  );
}
