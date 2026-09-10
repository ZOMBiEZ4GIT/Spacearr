import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { MediaKind } from '../api/types';

export interface LibraryUiParams {
  instanceId: number | null; kind: MediaKind | null; colorBy: 'heat' | 'quality' | 'codec' | 'resolution' | 'duplicates' | 'instance';
  heatMode: 'relative' | 'absolute' | null; minBytes: number; search: string; sort: 'size' | 'heat' | 'title' | 'quality'; order: 'asc' | 'desc'; sel: number | null; posters: boolean;
}

const KINDS: MediaKind[] = ['movie', 'episode'];
const COLOR_BY: LibraryUiParams['colorBy'][] = ['heat', 'quality', 'codec', 'resolution', 'duplicates', 'instance'];
const HEAT_MODES: NonNullable<LibraryUiParams['heatMode']>[] = ['relative', 'absolute'];
const SORTS: LibraryUiParams['sort'][] = ['size', 'heat', 'title', 'quality'];
const ORDERS: LibraryUiParams['order'][] = ['asc', 'desc'];

/**
 * Whitelists an enum-valued param. A hand-edited or stale URL must fall back to the default
 * rather than being forwarded to the API, which would 400 the whole page.
 */
function pick<T extends string>(raw: string | null, allowed: readonly T[], fallback: T): T;
function pick<T extends string>(raw: string | null, allowed: readonly T[], fallback: null): T | null;
function pick<T extends string>(raw: string | null, allowed: readonly T[], fallback: T | null): T | null {
  return allowed.includes(raw as T) ? (raw as T) : fallback;
}

/** A non-negative finite number, or the fallback: NaN or a negative page/size would break the API. */
const num = (raw: string | null, fallback: number) => {
  const n = Number(raw);
  return raw !== null && raw !== '' && Number.isFinite(n) && n >= 0 ? n : fallback;
};

/** Keeps every toolbar control in the URL search params, so a library view is a shareable link. */
export function useLibraryParams() {
  const [sp, setSp] = useSearchParams();
  const params = useMemo<LibraryUiParams>(() => ({
    instanceId: num(sp.get('instanceId'), 0) || null,
    kind: pick(sp.get('kind'), KINDS, null),
    colorBy: pick(sp.get('colorBy'), COLOR_BY, 'heat'),
    heatMode: pick(sp.get('heatMode'), HEAT_MODES, null),
    minBytes: num(sp.get('minBytes'), 0),
    search: sp.get('search') || '',
    sort: pick(sp.get('sort'), SORTS, 'size'),
    order: pick(sp.get('order'), ORDERS, 'desc'),
    sel: num(sp.get('sel'), 0) || null,
    posters: sp.get('posters') !== '0',
  }), [sp]);
  const set = useCallback((partial: Partial<LibraryUiParams>) => {
    const next = new URLSearchParams(sp);
    for (const [k, v] of Object.entries(partial)) {
      if (v === null || v === undefined || v === '' || (k === 'minBytes' && v === 0) || (k === 'posters' && v === true)) next.delete(k);
      else next.set(k, k === 'posters' ? (v ? '1' : '0') : String(v));
    }
    setSp(next, { replace: true });
  }, [sp, setSp]);
  return { params, set };
}
