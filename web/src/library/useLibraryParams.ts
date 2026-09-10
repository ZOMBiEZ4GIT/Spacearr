import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { MediaKind } from '../api/types';

export interface LibraryUiParams {
  instanceId: number | null; kind: MediaKind | null; colorBy: 'heat' | 'quality' | 'codec' | 'resolution' | 'duplicates' | 'instance';
  heatMode: 'relative' | 'absolute' | null; minBytes: number; search: string; sort: 'size' | 'heat' | 'title' | 'quality'; order: 'asc' | 'desc'; sel: number | null; posters: boolean;
}

/** Keeps every toolbar control in the URL search params, so a library view is a shareable link. */
export function useLibraryParams() {
  const [sp, setSp] = useSearchParams();
  const params = useMemo<LibraryUiParams>(() => ({
    instanceId: sp.get('instanceId') ? Number(sp.get('instanceId')) : null,
    kind: (sp.get('kind') as MediaKind | null) || null,
    colorBy: (sp.get('colorBy') as LibraryUiParams['colorBy']) || 'heat',
    heatMode: (sp.get('heatMode') as LibraryUiParams['heatMode']) || null,
    minBytes: Number(sp.get('minBytes') || 0),
    search: sp.get('search') || '',
    sort: (sp.get('sort') as LibraryUiParams['sort']) || 'size',
    order: (sp.get('order') as LibraryUiParams['order']) || 'desc',
    sel: sp.get('sel') ? Number(sp.get('sel')) : null,
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
