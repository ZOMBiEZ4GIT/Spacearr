import { Link } from 'react-router-dom';
import type { LibraryStats, LibraryItem } from '../api/types';
import { formatBytes } from '../lib/format';
import { heatColor } from '../lib/heat';
import s from './library.module.css';

export default function StatsRail({ stats, onSelect }: { stats: LibraryStats | undefined; onSelect: (i: LibraryItem) => void }) {
  if (!stats) return <aside className={s.rail}><span className="muted">Loading…</span></aside>;
  const bars = (rows: { name: string; bytes: number }[]) => (
    <div className={s.bars}>{rows.slice(0, 6).map((r) => <div key={r.name} className={s.barRow}><span title={r.name} style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name}</span><span className={s.barTrack}><span className={s.barFill} style={{ width: `${stats.totalBytes ? (r.bytes / stats.totalBytes) * 100 : 0}%` }} /></span><span className={s.num}>{formatBytes(r.bytes)}</span></div>)}</div>
  );
  const maxBin = Math.max(1, ...stats.heatHistogram);
  return (
    <aside className={s.rail} aria-label="Library statistics">
      <div><div className={s.big}>{formatBytes(stats.totalBytes)}</div><div className="muted">{stats.fileCount.toLocaleString()} files · {stats.itemCount.toLocaleString()} titles</div></div>
      {stats.unmatchedFileCount > 0 && <div className={s.note}>{stats.unmatchedFileCount.toLocaleString()} files not matched to any title. <Link to="/settings/connections">Check path mappings</Link>.</div>}
      {stats.unreadableFileCount > 0 && <div className="muted">{stats.unreadableFileCount.toLocaleString()} files could not be read by ffprobe (shown hatched).</div>}
      {stats.byInstance.length > 1 && <section><h3>By connection</h3>{bars(stats.byInstance)}</section>}
      <section><h3>By quality</h3>{bars(stats.byQuality)}</section>
      <section><h3>Heat spread</h3><div className={s.hist} aria-label="Heat histogram">{stats.heatHistogram.map((n, i) => <span key={i} title={`${n} files`} style={{ height: `${(n / maxBin) * 100}%`, background: heatColor((i + 0.5) / 10) }} />)}</div></section>
      <section><h3>Largest</h3><div className={s.list}>{stats.largest.map((i) => <button key={i.fileId} onClick={() => onSelect(i)}><span>{i.kind === 'episode' ? `${i.seriesTitle} S${i.seasonNumber}` : i.title}</span><span className={s.num}>{formatBytes(i.sizeBytes)}</span></button>)}</div></section>
      <section><h3>Hottest</h3><div className={s.list}>{stats.hottest.map((i) => <button key={i.fileId} onClick={() => onSelect(i)}><span>{i.kind === 'episode' ? `${i.seriesTitle} S${i.seasonNumber}` : i.title}</span><span className={s.num}><span className={s.swatch} style={{ background: heatColor(i.heat) }} />{Math.round(i.heat * 100)}</span></button>)}</div></section>
    </aside>
  );
}
