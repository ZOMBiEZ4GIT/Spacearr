import { useMemo, useState } from 'react';
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

  return (
    <div className={`${s.page} ${detailId !== null ? s.withDetail : ''}`}>
      <Toolbar params={params} set={set} heatMode={heatMode} categories={categories} />
      <div className={s.treemap}><Treemap tree={tree.data} colorBy={params.colorBy} selectedItemId={sel} showPosters={params.posters} onSelect={onTreeSelect} onZoom={onZoom} /></div>
      <div className={s.lower}>
        <LibraryTable items={list.data?.items ?? []} total={list.data?.total ?? 0} selected={sel} selectedFileId={unmatched?.fileId ?? null} sort={params.sort} order={params.order}
          onSort={(c) => set({ sort: c, order: c === params.sort && params.order === 'desc' ? 'asc' : 'desc' })} onSelect={select} onMore={() => setPages((p) => p + 1)} />
        <StatsRail stats={stats.data} onSelect={select} />
      </div>
      {detailId !== null && <div className={s.detailCol}><DetailPanel itemId={detailId} item={unmatched} onClose={clearSelection} onAction={(kind, profile) => setAction({ kind, itemId: detailId, profile })} /></div>}
      {action && <ActionDialog kind={action.kind} itemId={action.itemId} profile={action.profile} onClose={() => setAction(null)} onDone={() => { setAction(null); clearSelection(); }} />}
    </div>
  );
}
