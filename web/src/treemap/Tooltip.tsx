import type { TreeLeaf } from '../api/types';
import { formatBytes } from '../lib/format';
import s from './Treemap.module.css';

export interface TooltipData { name: string; bytes: number; leaf: TreeLeaf | null; x: number; y: number }

const WIDTH = 290;
const HEIGHT = 110;

export default function Tooltip({ data, bounds }: { data: TooltipData; bounds: { width: number; height: number } }) {
  const left = Math.max(4, Math.min(data.x + 14, bounds.width - WIDTH));
  const top = Math.max(4, data.y + 14 > bounds.height - HEIGHT ? data.y - HEIGHT : data.y + 14);
  return (
    <div className={s.tooltip} style={{ left, top }} role="presentation">
      <strong>{data.name}</strong>
      <div className="mono">{formatBytes(data.bytes)}</div>
      {data.leaf && data.leaf.fileId !== 0 && (
        <div className="muted">
          {[data.leaf.quality, data.leaf.resolution, data.leaf.codec?.toUpperCase()].filter(Boolean).join(' · ')}
          {data.leaf.heat >= 0 ? ` · heat ${Math.round(data.leaf.heat * 100)}` : ' · unreadable'}
          {data.leaf.instanceName ? ` · ${data.leaf.instanceName}` : ''}
        </div>
      )}
    </div>
  );
}
