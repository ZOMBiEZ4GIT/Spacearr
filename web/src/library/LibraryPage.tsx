import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useLibrary, useSettings, useStats, useTree } from '../api/hooks';
import type { LibraryItem, ProfileEstimate, TreeLeaf, TreeNode } from '../api/types';
import Treemap from '../treemap/Treemap';
import Toolbar from './Toolbar';
import LibraryTable from './LibraryTable';
import StatsRail from './StatsRail';
import DetailPanel from './DetailPanel';
import ActionDialog from '../actions/ActionDialog';
import { useLibraryParams } from './useLibraryParams';
import s from './library.module.css';

/** True when any leaf below `node` is the given item, so a zoom knows whether to keep the selection. */
function containsItem(node: TreeNode, itemId: number): boolean {
  if (node.leaf) return node.leaf.itemId === itemId;
  return (node.children ?? []).some((c) => containsItem(c, itemId));
}

/** Tracks a media query so the detail panel can be a column on wide screens and a drawer below that. */
function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => typeof matchMedia === 'function' && matchMedia(query).matches);
  useEffect(() => {
    if (typeof matchMedia !== 'function') return;
    const mq = matchMedia(query);
    const update = () => setMatches(mq.matches);
    update();
    mq.addEventListener('change', update);
    return () => mq.removeEventListener('change', update);
  }, [query]);
  return matches;
}

export default function LibraryPage() {
  const { params, set } = useLibraryParams();
  const settings = useSettings();
  const heatMode = params.heatMode ?? settings.data?.heatMode ?? 'relative';
  const [pages, setPages] = useState(1);
  const [action, setAction] = useState<{ kind: 'delete' | 'replace'; itemId: number; profile?: ProfileEstimate } | null>(null);
  // The row behind the current selection. Unmatched files have no id to put in the URL and no
  // detail endpoint, so their facts come from the list row itself.
  const [selectedRow, setSelectedRow] = useState<LibraryItem | null>(null);
  const common = { instanceId: params.instanceId ?? undefined, kind: params.kind ?? undefined, heatMode };
  const tree = useTree({ ...common, minBytes: params.minBytes, colorBy: params.colorBy });
  const stats = useStats(common);
  const list = useLibrary({ ...common, minBytes: params.minBytes, search: params.search || undefined, sort: params.sort, order: params.order, page: 1, pageSize: 500 * pages });
  const categories = useMemo(() => {
    const st = stats.data; if (!st) return [];
    return (params.colorBy === 'quality' ? st.byQuality : params.colorBy === 'codec' ? st.byCodec : params.colorBy === 'resolution' ? st.byResolution : params.colorBy === 'instance' ? st.byInstance : []).map((b) => b.name);
  }, [stats.data, params.colorBy]);

  // Any change of filter or ordering makes the accumulated pages meaningless: start again at one.
  useEffect(() => { setPages(1); }, [params.instanceId, params.kind, params.minBytes, params.search, params.sort, params.order, heatMode]);

  const select = (i: LibraryItem) => { setSelectedRow(i); set({ sel: i.itemId || null }); };
  const onTreeSelect = (leaf: TreeLeaf | null) => {
    // "Other (n files)" blocks carry itemId 0 and are not selectable.
    if (leaf && leaf.itemId === 0) return;
    setSelectedRow(null);
    set({ sel: leaf ? leaf.itemId : null });
  };
  const clearSelection = () => { setSelectedRow(null); set({ sel: null }); };
  const onZoom = (node: TreeNode) => {
    if (params.sel != null && !containsItem(node, params.sel)) clearSelection();
  };
  const sel = params.sel;
  const unmatched = sel == null && selectedRow?.itemId === 0 ? selectedRow : null;
  const detailId = sel ?? (unmatched ? 0 : null);
  const open = detailId !== null;
  // Below 1280px the panel is an overlay drawer, so it gets dialog semantics, a backdrop and focus.
  const drawer = useMediaQuery('(max-width: 1279px)');
  const asDialog = open && drawer;

  useEffect(() => {
    if (!asDialog) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape' && !e.defaultPrevented) clearSelection(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }); // eslint-disable-line react-hooks/exhaustive-deps

  const st = stats.data;
  return (
    <div className={`${s.frame} ${open ? s.withDetail : ''}`}>
      <div className={s.page}>
        <Toolbar params={params} set={set} heatMode={heatMode} categories={categories} />
        {/* Always rendered, so the rows below keep their place in the grid; empty unless the rail is hidden. */}
        <div className={s.notes}>
          {st && st.unmatchedFileCount > 0 && <div className={s.note}>{st.unmatchedFileCount.toLocaleString()} files not matched to any title. <Link to="/settings/connections">Check path mappings</Link>.</div>}
          {st && st.unreadableFileCount > 0 && <div className="muted">{st.unreadableFileCount.toLocaleString()} files could not be read by ffprobe (shown hatched).</div>}
        </div>
        <div className={s.treemap}><Treemap tree={tree.data} colorBy={params.colorBy} selectedItemId={sel} showPosters={params.posters} onSelect={onTreeSelect} onZoom={onZoom} /></div>
        <div className={s.lower}>
          <LibraryTable items={list.data?.items ?? []} total={list.data?.total ?? 0} selected={sel} selectedFileId={unmatched?.fileId ?? null} sort={params.sort} order={params.order}
            onSort={(c) => set({ sort: c, order: c === params.sort && params.order === 'desc' ? 'asc' : 'desc' })} onSelect={select} onMore={() => setPages((p) => p + 1)} />
          <StatsRail stats={stats.data} onSelect={select} />
        </div>
      </div>
      {asDialog && <button type="button" className={s.backdrop} aria-label="Close details" onClick={clearSelection} />}
      {open && (
        <div className={s.detailCol} role={asDialog ? 'dialog' : undefined} aria-modal={asDialog || undefined} aria-label={asDialog ? 'Details' : undefined}>
          <DetailPanel key={detailId} itemId={detailId} item={unmatched} autoFocus={asDialog} onClose={clearSelection} onAction={(kind, profile) => setAction({ kind, itemId: detailId, profile })} />
        </div>
      )}
      {action && <ActionDialog kind={action.kind} itemId={action.itemId} profile={action.profile} onClose={() => setAction(null)} onDone={() => { setAction(null); clearSelection(); }} />}
    </div>
  );
}
