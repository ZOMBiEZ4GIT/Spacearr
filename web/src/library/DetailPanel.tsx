import { useState } from 'react';
import { useInstances, useItem } from '../api/hooks';
import type { LibraryItem, ProfileEstimate } from '../api/types';
import { formatBitrate, formatBytes, formatDuration } from '../lib/format';
import { heatColor } from '../lib/heat';
import { episodeLabel } from './LibraryTable';
import s from './library.module.css';

/**
 * `itemId` 0 means the file is not matched to any title. `/api/v1/library/0` 404s, so the
 * caller hands the row it already has through `item` and the detail endpoint is never called.
 */
export default function DetailPanel({ itemId, item, onClose, onAction }: { itemId: number; item?: LibraryItem | null; onClose: () => void; onAction: (kind: 'delete' | 'replace', profile?: ProfileEstimate) => void }) {
  const detail = useItem(itemId > 0 ? itemId : undefined);
  const instances = useInstances();
  const [posterOk, setPosterOk] = useState(true);
  const it = itemId > 0 ? detail.data?.item : item ?? undefined;
  const profiles = detail.data?.profiles ?? [];
  if (itemId > 0 && detail.isLoading) return <div className={s.detail}><span className="muted">Loading…</span></div>;
  if (!it) return <div className={s.detail}><p className="muted">This title is no longer in the library.</p><button className="btn" onClick={onClose}>Close</button></div>;
  const inst = instances.data?.find((i) => i.id === it.instanceId);
  const title = it.itemId === 0 ? (it.path.split(/[/\\]/).pop() ?? it.path) : it.kind === 'episode' ? episodeLabel(it) : it.year ? `${it.title} (${it.year})` : it.title;
  const openHref = inst ? (inst.type === 'radarr' && it.tmdbId ? `${inst.baseUrl}/movie/${it.tmdbId}` : inst.baseUrl) : null;
  return (
    <div className={s.detail} role="region" aria-label="Details">
      <div style={{ display: 'flex', justifyContent: 'space-between' }}><strong>Details</strong><button className="btn" onClick={onClose} aria-label="Close details">✕</button></div>
      <div className={s.detailHead}>
        {posterOk && it.posterUrl && <img className={s.poster} src={`/api/v1/posters/${it.itemId}`} alt="" onError={() => setPosterOk(false)} />}
        <div><h2 style={{ margin: 0, fontSize: 'var(--fs-lg)' }}>{title}</h2>{it.kind === 'episode' && it.itemId !== 0 && <div className="muted">{it.title}</div>}<div className="muted">{it.instanceName}{it.tags ? ` · ${it.tags}` : ''}</div></div>
      </div>
      <div className={s.path}>{it.path}</div>
      <dl className={s.facts}>
        <dt>Size</dt><dd className="mono">{formatBytes(it.sizeBytes)}</dd>
        <dt>Duration</dt><dd>{formatDuration(it.durationSeconds)}</dd>
        <dt>Bitrate</dt><dd className="mono">{formatBitrate(it.videoBitrateBps)}{it.overallBitrateBps ? <span className="muted"> ({formatBitrate(it.overallBitrateBps)} overall)</span> : null}</dd>
        <dt>Heat</dt><dd>{it.heat >= 0 ? <><span className={s.swatch} style={{ background: heatColor(it.heat) }} />{Math.round(it.heat * 100)}<span className="muted"> · {it.nbpp?.toFixed(3)} bits per pixel per frame, x264-equivalent</span></> : <span className="muted">unreadable ({it.probeError ?? 'no video stream'})</span>}</dd>
        <dt>Video</dt><dd>{[it.resolution, it.videoCodec?.toUpperCase(), it.bitDepth ? `${it.bitDepth}-bit` : null, it.hdrFormat].filter(Boolean).join(' · ') || '—'}</dd>
        <dt>Audio</dt><dd>{it.audioSummary ?? '—'}</dd>
        <dt>Quality</dt><dd>{it.qualityName ?? '—'}</dd>
        <dt>Profile</dt><dd>{it.qualityProfileName ?? '—'}</dd>
        <dt>Monitored</dt><dd>{it.itemId === 0 ? '—' : it.monitored ? 'Yes' : 'No'}</dd>
      </dl>
      {it.itemId === 0 ? <p className="muted">Not matched to any title, so no actions are available. Check path mappings in Settings › Connections.</p> : (
        <>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {openHref && <a className="btn" href={openHref} target="_blank" rel="noreferrer">Open in {inst?.type === 'radarr' ? 'Radarr' : 'Sonarr'}</a>}
            <button className="btn btn-danger" onClick={() => onAction('delete')}>Delete…</button>
          </div>
          <section>
            <h3 style={{ margin: '4px 0 6px', fontSize: 'var(--fs-md)' }}>Replace with a smaller release</h3>
            {profiles.length === 0 && <p className="muted">No other quality profiles found on {it.instanceName}.</p>}
            <div className={s.profiles}>
              {profiles.map((p) => (
                <button key={p.id} className={s.profileRow} onClick={() => onAction('replace', p)}>
                  <span>{p.name}</span>
                  <span className={p.estimate.basis === 'unknown' ? 'muted' : s.save}>
                    {p.estimate.basis === 'unknown' ? 'no estimate' : `saves ~${formatBytes(p.estimate.savingsBytes)}`}
                    <span className="muted"> {p.estimate.basis === 'library' ? `(from ${p.estimate.samples} similar files)` : p.estimate.basis === 'table' ? '(rough estimate)' : ''}</span>
                  </span>
                </button>
              ))}
            </div>
            {it.instanceType === 'sonarr' && profiles.length > 0 && <p className="muted" style={{ fontSize: 'var(--fs-xs)' }}>Sonarr profiles apply to the whole series.</p>}
          </section>
        </>
      )}
    </div>
  );
}
