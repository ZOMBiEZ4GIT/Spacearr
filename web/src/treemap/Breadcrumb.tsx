import type { TreeNode } from '../api/types';
import s from './Treemap.module.css';

export default function Breadcrumb({ path, onZoomTo }: { path: TreeNode[]; onZoomTo: (index: number) => void }) {
  return (
    <nav className={s.crumbs} aria-label="Treemap zoom">
      {path.map((n, i) => i === path.length - 1
        ? <span key={i} className={s.crumbCurrent}>{n.name}</span>
        : <span key={i}><button type="button" className={s.crumb} onClick={() => onZoomTo(i)}>{n.name}</button> ›</span>)}
    </nav>
  );
}
