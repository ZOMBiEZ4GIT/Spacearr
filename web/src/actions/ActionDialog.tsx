import { useEffect, useRef, useState } from 'react';
import { useExecuteAction, usePreviewAction } from '../api/hooks';
import { useJobProgress } from '../api/events';
import type { ActionPreview, ProfileEstimate } from '../api/types';
import { ApiError } from '../api/client';
import { formatBytes } from '../lib/format';
import s from './actions.module.css';

interface Props { kind: 'delete' | 'replace'; itemId: number; profile?: ProfileEstimate; onClose: () => void; onDone: () => void }

export default function ActionDialog({ kind, itemId, profile, onClose, onDone }: Props) {
  const preview = usePreviewAction();
  const execute = useExecuteAction();
  const [unmonitor, setUnmonitor] = useState(false);
  const [data, setData] = useState<ActionPreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [armed, setArmed] = useState(false);
  const [jobId, setJobId] = useState<number | undefined>();
  const [repreviewing, setRepreviewing] = useState(false);
  const progress = useJobProgress(jobId);
  const dialogRef = useRef<HTMLDivElement>(null);
  const request = { type: kind, itemId, targetProfileId: profile?.id ?? null, unmonitor };

  const finished = progress?.kind === 'finished';

  // Kept current via effects below so the mount-only keydown listener and the confirm handler
  // never need `onClose`/`jobId` in their own deps - a stale-closure-free ref read instead.
  const onCloseRef = useRef(onClose);
  useEffect(() => { onCloseRef.current = onClose; }, [onClose]);
  const jobIdRef = useRef<number | undefined>(undefined);
  useEffect(() => { jobIdRef.current = jobId; }, [jobId]);

  // Guards state updates from in-flight async work (the initial preview, and the "Preview
  // first" retry inside confirm()) against firing after the dialog has unmounted.
  const mountedRef = useRef(true);
  useEffect(() => () => { mountedRef.current = false; }, []);

  // A single pending "arm the confirm button" timer, so a re-preview never stacks a second one
  // on top of the first and unmount always has exactly one timer to clear.
  const armTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const scheduleArm = () => {
    if (armTimerRef.current) clearTimeout(armTimerRef.current);
    armTimerRef.current = setTimeout(() => { if (mountedRef.current) setArmed(true); }, 1500);
  };
  useEffect(() => () => { if (armTimerRef.current) clearTimeout(armTimerRef.current); }, []);

  useEffect(() => {
    let cancelled = false;
    setData(null); setArmed(false); setError(null);
    preview.mutateAsync(request).then((p) => { if (!cancelled) { setData(p); scheduleArm(); } })
      .catch((err) => !cancelled && setError(err instanceof ApiError ? err.message : 'Preview failed.'));
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kind, itemId, profile?.id, unmonitor]);

  // Moves focus into the dialog once there is something to focus (the preview has resolved into
  // either the confirm form or an error message) rather than at mount, when only "Checking with
  // the arr app…" is on screen and no button/input exists yet. Deps are this component's own
  // state, not the `onClose` prop, so a parent re-render never re-runs this and steals focus back.
  useEffect(() => {
    if (!data && !error) return;
    const el = dialogRef.current;
    const target = el?.querySelector<HTMLElement>('button:not([disabled]), input') ?? el;
    target?.focus();
  }, [data, error, jobId, finished]);

  // Synchronous latch: React's own re-render (which disables the button) is not synchronous, so
  // without this a double-click can fire two executes before the first `disabled` update lands.
  // Declared before the mount-only effect below so Escape/backdrop can gate on it too, covering
  // the window between clicking confirm and the execute POST resolving into a jobId.
  const confirmLatchRef = useRef(false);

  // Mount-only: the focus trap and the Escape handler. Reads `onClose`/`jobId` through refs so
  // it never has to re-subscribe (LibraryPage passes inline callbacks that get a new identity on
  // every render). Escape and the backdrop click (below) are both gated on jobIdRef and
  // confirmLatchRef: once a job exists, or an execute POST is in flight, dismissal must go
  // through the Close button so a failure is never hidden mid-action.
  useEffect(() => {
    const el = dialogRef.current;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (jobIdRef.current !== undefined || confirmLatchRef.current) return;
        e.preventDefault();
        onCloseRef.current();
      }
      if (e.key === 'Tab' && el) {
        const f = Array.from(el.querySelectorAll<HTMLElement>('button:not([disabled]), input, a[href]'));
        if (f.length === 0) return;
        const first = f[0], last = f[f.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey); return () => document.removeEventListener('keydown', onKey);
  }, []);

  const confirm = async () => {
    if (!data || confirmLatchRef.current) return;
    confirmLatchRef.current = true;
    setError(null);
    try {
      const res = await execute.mutateAsync({ ...data.request, confirmToken: data.confirmToken });
      if (mountedRef.current) setJobId(res.jobId);
    } catch (err) {
      if (err instanceof ApiError && err.message.startsWith('Preview first')) {
        if (mountedRef.current) { setArmed(false); setData(null); setRepreviewing(true); }
        try {
          const p = await preview.mutateAsync(request);
          if (mountedRef.current) { setData(p); setRepreviewing(false); scheduleArm(); }
        } catch (err2) {
          if (mountedRef.current) { setRepreviewing(false); setError(err2 instanceof ApiError ? err2.message : 'Preview failed.'); }
        }
      } else if (mountedRef.current) {
        setError(err instanceof ApiError ? err.message : 'Could not start the action.');
      }
    } finally {
      confirmLatchRef.current = false;
    }
  };

  const label = data ? (kind === 'delete' ? `Delete ${formatBytes(data.bytesFreedNow)}` : `Replace, free ${formatBytes(data.bytesFreedNow)} now`) : kind === 'delete' ? 'Delete' : 'Replace';

  return (
    <div className={s.backdrop} onMouseDown={(e) => { if (jobIdRef.current !== undefined || confirmLatchRef.current) return; if (e.target === e.currentTarget) onClose(); }}>
      <div className={s.dialog} role="dialog" aria-modal="true" aria-labelledby="action-title" tabIndex={-1} ref={dialogRef}>
        <h2 id="action-title">{kind === 'delete' ? 'Delete file' : `Replace with ${profile?.name ?? 'a smaller release'}`}</h2>
        {!data && !error && <p className="muted">{repreviewing ? 'Preview expired, refreshing…' : 'Checking with the arr app…'}</p>}
        {error && <p className="error" role="alert">{error}</p>}
        {data && !jobId && (
          <>
            <div><strong>{data.title}</strong> <span className="muted">· {data.instanceName}</span></div>
            <div><span className="muted">Frees now</span> <span className={s.big}>{formatBytes(data.bytesFreedNow)}</span></div>
            {kind === 'replace' && data.estimate && data.estimate.basis !== 'unknown' && (
              <p>Expect roughly <span className="mono">{formatBytes(data.estimate.estimatedBytes)}</span> after replacement, saving <span className="mono">~{formatBytes(data.estimate.savingsBytes)}</span> <span className="muted">{data.estimate.basis === 'library' ? `(based on ${data.estimate.samples} similar files in your library)` : '(rough estimate; no similar files yet)'}</span>.</p>
            )}
            {kind === 'replace' && (!data.estimate || data.estimate.basis === 'unknown') && <p className="muted">No size estimate for this profile. The file is deleted first and a replacement is searched for.</p>}
            {data.warning && <div className={s.warn}>{data.warning}</div>}
            {kind === 'delete' && <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}><input id="action-unmonitor" type="checkbox" checked={unmonitor} onChange={(e) => setUnmonitor(e.target.checked)} /> Also unmonitor so it is not downloaded again</label>}
            <div><div className="muted" style={{ marginBottom: 4 }}>Spacearr will ask {data.instanceType === 'radarr' ? 'Radarr' : 'Sonarr'} to:</div>
              <ol className={s.steps}>{data.steps.map((st, i) => <li key={i}>{st.description}</li>)}</ol></div>
            <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>Spacearr never deletes from disk itself. The file is removed by the arr app, so it stays in sync.</p>
            <div className={s.foot}>
              <button className="btn" onClick={onClose}>Cancel</button>
              <button className={`btn ${kind === 'delete' ? 'btn-danger' : 'btn-primary'}`} onClick={confirm} disabled={!armed || execute.isPending}>{label}</button>
            </div>
          </>
        )}
        {jobId && (
          <>
            <p role="status" aria-live="polite">{finished ? (progress?.status === 'succeeded' ? 'Done.' : `Failed: ${progress?.detail ?? 'see Activity for details'}`) : `Working… ${progress?.detail ?? ''}`}</p>
            {!finished && <div className={s.progress}><span style={{ width: `${progress && progress.total ? (progress.done / progress.total) * 100 : 10}%` }} /></div>}
            <div className={s.foot}><button className="btn btn-primary" onClick={onDone} disabled={!finished}>Close</button></div>
          </>
        )}
      </div>
    </div>
  );
}
