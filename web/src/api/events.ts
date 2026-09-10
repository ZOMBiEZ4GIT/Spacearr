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

function emit() { listeners.forEach((l) => l()); }

export function startEvents(qc: QueryClient) {
  stopped = false;
  // Guard the window between onerror nulling `source` and the reconnect timer firing:
  // if either a source is already open or a reconnect is already scheduled, do nothing.
  if (source || reconnectTimer) return;
  const connect = () => {
    if (stopped) return;
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
    source.onopen = () => { backoff = 1000; };
    source.onerror = () => {
      source?.close(); source = null;
      if (stopped) return;
      reconnectTimer = setTimeout(() => { reconnectTimer = null; connect(); }, backoff);
      backoff = Math.min(backoff * 2, 30_000);
    };
  };
  connect();
}

export function stopEvents() {
  stopped = true;
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
