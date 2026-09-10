import { useEffect, useRef, useState } from 'react';
import { useInstances } from '../api/hooks';
import Legend from '../treemap/Legend';
import type { LibraryUiParams } from './useLibraryParams';
import s from './library.module.css';

const sizes: [number, string][] = [[0, 'Any size'], [1e9, '≥ 1 GB'], [5e9, '≥ 5 GB'], [10e9, '≥ 10 GB'], [20e9, '≥ 20 GB'], [40e9, '≥ 40 GB']];

export default function Toolbar({ params, set, heatMode, categories }: { params: LibraryUiParams; set: (p: Partial<LibraryUiParams>) => void; heatMode: 'relative' | 'absolute'; categories: string[] }) {
  const instances = useInstances();
  const [search, setSearch] = useState(params.search);
  // `set` is rebuilt on every search-param change, so the debounce reads it through a ref:
  // depending on it directly would restart the timer on every keystroke's own URL write.
  const setRef = useRef(set);
  useEffect(() => { setRef.current = set; });
  // The URL is the source of truth: adopt a search that changed elsewhere (a link, Back), but
  // never clobber what the user is mid-way through typing.
  const pushed = useRef(params.search);
  useEffect(() => {
    if (params.search === pushed.current) return;
    pushed.current = params.search;
    setSearch(params.search);
  }, [params.search]);
  useEffect(() => {
    if (search === params.search) return;
    const t = setTimeout(() => { pushed.current = search; setRef.current({ search }); }, 250);
    return () => clearTimeout(t);
  }, [search, params.search]);
  return (
    <div className={s.toolbar} role="toolbar" aria-label="Library filters">
      <select id="tb-instance" aria-label="Connection" value={params.instanceId ?? ''} onChange={(e) => set({ instanceId: e.target.value ? Number(e.target.value) : null })}>
        <option value="">All connections</option>{instances.data?.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
      </select>
      <div className={s.seg} role="group" aria-label="Kind">
        {([['', 'All'], ['movie', 'Movies'], ['episode', 'TV']] as const).map(([v, label]) => <button key={v} aria-pressed={(params.kind ?? '') === v} onClick={() => set({ kind: (v || null) as LibraryUiParams['kind'] })}>{label}</button>)}
      </div>
      <select id="tb-color" aria-label="Colour by" value={params.colorBy} onChange={(e) => set({ colorBy: e.target.value as LibraryUiParams['colorBy'] })}>
        <option value="heat">Colour: heat</option><option value="quality">Colour: quality</option><option value="codec">Colour: codec</option><option value="resolution">Colour: resolution</option><option value="duplicates">Colour: duplicates</option><option value="instance">Colour: connection</option>
      </select>
      {params.colorBy === 'heat' && (
        <div className={s.seg} role="group" aria-label="Heat mode">
          <button aria-pressed={heatMode === 'relative'} onClick={() => set({ heatMode: 'relative' })}>Relative</button>
          <button aria-pressed={heatMode === 'absolute'} onClick={() => set({ heatMode: 'absolute' })}>Absolute</button>
        </div>
      )}
      <select id="tb-size" aria-label="Minimum size" value={params.minBytes} onChange={(e) => set({ minBytes: Number(e.target.value) })}>{sizes.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
      <input id="tb-search" type="search" placeholder="Search titles" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search titles" />
      <label className={s.seg} style={{ padding: '4px 8px', gap: 6, alignItems: 'center' }}><input type="checkbox" checked={params.posters} onChange={(e) => set({ posters: e.target.checked })} /> Posters</label>
      <div className={s.spacer} />
      <div className={s.legendWrap}><Legend colorBy={params.colorBy} heatMode={heatMode} categories={categories} /></div>
    </div>
  );
}
