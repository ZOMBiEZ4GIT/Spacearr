import { useState } from 'react';
import { useDuplicates, useInstances } from '../api/hooks';
import type { DuplicateGroup, LibraryItem } from '../api/types';
import { formatBitrate, formatBytes } from '../lib/format';
import { heatColor } from '../lib/heat';
import ActionDialog from '../actions/ActionDialog';
import { useLibraryParams } from '../library/useLibraryParams';
import s from './duplicates.module.css';

export default function DuplicatesPage() {
  const { params, set } = useLibraryParams();
  const instances = useInstances();
  const dups = useDuplicates({ instanceId: params.instanceId ?? undefined, kind: params.kind ?? undefined });
  const [queue, setQueue] = useState<number[]>([]);
  const [total, setTotal] = useState(0);
  const keep = (g: DuplicateGroup, keepId: number) => { const others = g.members.filter((m) => m.itemId !== keepId && m.itemId > 0).map((m) => m.itemId); setQueue(others); setTotal(others.length); };
  const wasted = dups.data?.reduce((a, g) => a + g.wastedBytes, 0) ?? 0;
  return (
    <div className={s.page}>
      <div className={s.head}>
        <div><h1 style={{ margin: 0 }}>Duplicates</h1><span className="muted">{dups.data ? `${dups.data.length} groups · ` : ''}<span className={s.waste}>{formatBytes(wasted)}</span> wasted</span></div>
        <div style={{ display: 'flex', gap: 8 }}>
          <select aria-label="Connection" value={params.instanceId ?? ''} onChange={(e) => set({ instanceId: e.target.value ? Number(e.target.value) : null })}><option value="">All connections</option>{instances.data?.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}</select>
          <select aria-label="Kind" value={params.kind ?? ''} onChange={(e) => set({ kind: (e.target.value || null) as typeof params.kind })}><option value="">Movies and TV</option><option value="movie">Movies</option><option value="episode">TV</option></select>
        </div>
      </div>
      {dups.data?.length === 0 && <p className="muted">No duplicates found across your connections.</p>}
      {dups.data?.map((g) => (
        <section key={g.key} className={`card ${s.group}`}>
          <div className={s.groupHead}><strong>{g.title}</strong><span className={s.waste}>{formatBytes(g.wastedBytes)} wasted</span></div>
          {g.members.map((m) => <Member key={m.fileId} m={m} onKeep={() => keep(g, m.itemId)} />)}
        </section>
      ))}
      {queue.length > 0 && (
        <ActionDialog key={queue[0]} kind="delete" itemId={queue[0]} onClose={() => setQueue([])} onDone={() => setQueue((q) => q.slice(1))} />
      )}
      {queue.length > 0 && <div className="muted" style={{ position: 'fixed', bottom: 12, right: 12, zIndex: 21 }}>Deleting {total - queue.length + 1} of {total}</div>}
    </div>
  );
}

function Member({ m, onKeep }: { m: LibraryItem; onKeep: () => void }) {
  return (
    <div className={s.member}>
      <span className="chip">{m.instanceName}</span>
      <div><div>{[m.qualityName, m.resolution, m.videoCodec?.toUpperCase(), m.hdrFormat].filter(Boolean).join(' · ')} <span className={s.meta}>· {formatBitrate(m.videoBitrateBps)}</span></div><div className={s.path}>{m.path}</div></div>
      <div className="mono">{m.heat >= 0 && <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: 2, background: heatColor(m.heat), marginRight: 6 }} />}{formatBytes(m.sizeBytes)}</div>
      <button className="btn" onClick={onKeep} disabled={m.itemId === 0}>Keep this</button>
    </div>
  );
}
