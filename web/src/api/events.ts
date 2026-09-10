import { useSyncExternalStore } from 'react';
import type { QueryClient } from '@tanstack/react-query';
import type { ProgressEvent } from './types';

type Listener = () => void;
const listeners = new Set<Listener>();
let progress: Record<number, ProgressEvent> = {};
let source: EventSource | null = null;
let backoff = 1000;
let stopped = true;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
// True while onerror's 401 probe is in flight: during that fetch neither `source` nor
// `reconnectTimer` is set, so without this a concurrent startEvents() would open a second
// EventSource that the stale flow's later reconnect would then orphan.
let probing = false;

function emit() { listeners.forEach((l) => l()); }

export function startEvents(qc: QueryClient) {
  stopped = false;
  // Guard the window between onerror nulling `source` and the reconnect timer firing:
  // if either a source is already open or a reconnect is already scheduled, do nothing.
  if (source || reconnectTimer || probing) return;
  const scheduleReconnect = () => {
    reconnectTimer = setTimeout(() => { reconnectTimer = null; connect(); }, backoff);
    backoff = Math.min(backoff * 2, 30_000);
  };
  const connect = () => {
    if (stopped) return;
    source?.close();
    source = new EventSource('/api/v1/events');
    source.addEventListener('progress', (e) => {
      const ev = JSON.parse((e as MessageEvent).data) as ProgressEvent;
      progress = { ...progress, [ev.jobId]: ev };
      emit();
    });
    source.addEventListener('finished', (e) => {
      const ev = JSON.parse((e as MessageEvent).data) as ProgressEvent;
      progress = { ...progress, [ev.jobId]: ev };
      emit();
      ['jobs', 'library', 'tree', 'stats', 'duplicates', 'instances', 'roots', 'actionLog', 'item'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }));
    });
    source.onopen = () => {
      backoff = 1000;
      // /api/v1/events has no replay: a (re)connect may have missed a job's `finished` event
      // entirely, so a fresh connection - not just the first one - must re-synchronise rather
      // than trust what's cached. Refetch the polled sources of truth...
      qc.invalidateQueries({ queryKey: ['jobs'] });
      qc.invalidateQueries({ queryKey: ['job'] });
      // ...and drop any progress-store entry that's still claiming to be in progress: it can
      // only be trusted while the connection that's updating it stays open, and this one just
      // (re)opened, so any such entry predates it and may already be stale (e.g. ScanPill
      // rendering "Scanning" for ever off a job that actually finished while disconnected).
      if (Object.values(progress).some((p) => p.kind === 'progress')) {
        progress = Object.fromEntries(Object.entries(progress).filter(([, p]) => p.kind === 'finished'));
        emit();
      }
    };
    source.onerror = () => {
      source?.close(); source = null;
      if (stopped) return;
      // EventSource surfaces a 401 as the same generic error as any transport blip, so a logged-out
      // tab would otherwise reconnect (and get another silent 401) for ever. Probe once with a real
      // fetch - a cheap, already-authenticated endpoint - before scheduling a reconnect: a confirmed
      // 401 means the session is gone, so dispatch the same 'spacearr:unauthorized' event client.ts
      // uses (the app's existing handler takes it from there) and stop, instead of looping.
      probing = true;
      fetch('/api/v1/auth/me', { credentials: 'same-origin' })
        .then((res) => {
          probing = false;
          if (stopped) return;
          if (res.status === 401) {
            window.dispatchEvent(new CustomEvent('spacearr:unauthorized', { detail: { path: '/api/v1/events' } }));
            stopEvents();
            return;
          }
          scheduleReconnect();
        })
        .catch(() => { probing = false; if (!stopped) scheduleReconnect(); });
    };
  };
  connect();
}

export function stopEvents() {
  stopped = true;
  probing = false;
  if (reconnectTimer) { clearTimeout(reconnectTimer); reconnectTimer = null; }
  source?.close(); source = null;
}

const subscribe = (l: Listener) => { listeners.add(l); return () => { listeners.delete(l); }; };
const getSnapshot = () => progress;

export function useJobProgress(jobId?: number): ProgressEvent | undefined {
  const all = useSyncExternalStore(subscribe, getSnapshot);
  return jobId === undefined ? undefined : all[jobId];
}

export function useRunningJob(): ProgressEvent | undefined {
  const all = useSyncExternalStore(subscribe, getSnapshot);
  return Object.values(all).filter((p) => p.kind === 'progress').sort((a, b) => b.jobId - a.jobId)[0];
}
