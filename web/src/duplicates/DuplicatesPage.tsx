import { useEffect, useRef, useState } from 'react';
import { useDuplicates, useInstances } from '../api/hooks';
import type { DuplicateGroup, LibraryItem } from '../api/types';
import { formatBitrate, formatBytes } from '../lib/format';
import { heatColor } from '../lib/heat';
import ActionDialog from '../actions/ActionDialog';
import { useLibraryParams } from '../library/useLibraryParams';
import s from './duplicates.module.css';

const subtitleFor = (m: LibraryItem) => [m.qualityName, m.resolution, m.videoCodec?.toUpperCase(), formatBytes(m.sizeBytes)].filter(Boolean).join(' · ');

export default function DuplicatesPage() {
  const { params, set } = useLibraryParams();
  const instances = useInstances();
  const dups = useDuplicates({ instanceId: params.instanceId ?? undefined, kind: params.kind ?? undefined });
  // Holds the full member, not just its id, so the queued ActionDialog can show a
  // disambiguating subtitle/path for the copy it's about to delete (two queued members can
  // share the group's title).
  const [queue, setQueue] = useState<LibraryItem[]>([]);
  const [total, setTotal] = useState(0);
  const [cancelledMsg, setCancelledMsg] = useState<string | null>(null);
  const cancelledTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => () => { if (cancelledTimerRef.current) clearTimeout(cancelledTimerRef.current); }, []);

  const dismissCancelled = () => {
    if (cancelledTimerRef.current) { clearTimeout(cancelledTimerRef.current); cancelledTimerRef.current = null; }
    setCancelledMsg(null);
  };
  const keep = (g: DuplicateGroup, keepId: number) => {
    dismissCancelled();
    const others = g.members.filter((m) => m.itemId !== keepId && m.itemId > 0);
    setQueue(others);
    setTotal(others.length);
  };
  // Cancelling (Escape/backdrop/Cancel, all routed through the dialog's onClose) mid-queue used
  // to silently drop the rest of the batch. `n` is the queue's length at the moment of cancel,
  // which still includes the item the open dialog was showing (its own onDone hasn't fired), so
  // it counts every copy left undeleted, not just the ones after it.
  const cancelQueue = () => {
    setQueue((q) => {
      if (q.length > 0) {
        const n = q.length;
        if (cancelledTimerRef.current) clearTimeout(cancelledTimerRef.current);
        setCancelledMsg(`Cancelled — ${n} cop${n === 1 ? 'y was' : 'ies were'} not deleted.`);
        cancelledTimerRef.current = setTimeout(() => setCancelledMsg(null), 10_000);
      }
      return [];
    });
  };
  const wasted = dups.data?.reduce((a, g) => a + g.wastedBytes, 0) ?? 0;
  return (
    <div className={s.page}>
      <div className={s.head}>
        <div><h1 style={{ margin: 0 }}>Duplicates</h1><span className="muted">{dups.data ? `${dups.data.length} group${dups.data.length === 1 ? '' : 's'} · ` : ''}<span className={s.waste}>{formatBytes(wasted)}</span> wasted</span></div>
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
        <ActionDialog key={queue[0].itemId} kind="delete" itemId={queue[0].itemId} subtitle={subtitleFor(queue[0])} path={queue[0].path} onClose={cancelQueue} onDone={() => setQueue((q) => q.slice(1))} />
      )}
      {queue.length > 0 && <div role="status" aria-live="polite" className="muted" style={{ position: 'fixed', bottom: 12, right: 12, zIndex: 21 }}>Deleting {total - queue.length + 1} of {total}</div>}
      {queue.length === 0 && cancelledMsg && (
        <div role="status" className="muted" style={{ position: 'fixed', bottom: 12, right: 12, zIndex: 21, display: 'flex', gap: 8, alignItems: 'center' }}>
          <span>{cancelledMsg}</span>
          <button className="btn" onClick={dismissCancelled}>Dismiss</button>
        </div>
      )}
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
