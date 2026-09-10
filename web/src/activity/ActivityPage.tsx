import { useState } from 'react';
import { useActionLog, useCancelJob, useJobs, useStartEnrich, useStartScan } from '../api/hooks';
import { useJobProgress } from '../api/events';
import type { Job } from '../api/types';
import { formatBytes } from '../lib/format';
import { relative } from '../app/ScanPill';
import s from '../settings/settings.module.css';

const chip = (status: string) => <span className="chip" style={{ color: status === 'failed' ? 'var(--danger)' : status === 'succeeded' ? 'var(--good)' : undefined }}>{status}</span>;
const duration = (j: Job) => j.startedAt && j.finishedAt ? `${Math.max(1, Math.round((new Date(j.finishedAt).getTime() - new Date(j.startedAt).getTime()) / 1000))}s` : '—';

export default function ActivityPage() {
  const [page, setPage] = useState(1);
  const jobs = useJobs(page);
  const log = useActionLog(1);
  const scan = useStartScan();
  const enrich = useStartEnrich();
  const cancel = useCancelJob();
  return (
    <div className={s.page} style={{ maxWidth: 1100 }}>
      <div className={s.cardHead}><h1>Activity</h1><div className={s.row}><button className="btn" onClick={() => enrich.mutate()}>Match titles only</button><button className="btn btn-primary" onClick={() => scan.mutate()}>Scan now</button></div></div>
      <section className={`card ${s.grid}`}>
        <h2>Jobs</h2>
        <table className={s.table}><thead><tr><th>Type</th><th>Trigger</th><th>Status</th><th>Started</th><th>Took</th><th>Summary</th><th /></tr></thead>
          <tbody>{jobs.data?.items.map((j) => <JobRow key={j.id} j={j} onCancel={() => cancel.mutate(j.id)} />)}</tbody></table>
        <div className={s.row}><button className="btn" disabled={page === 1} onClick={() => setPage(page - 1)}>Newer</button><button className="btn" disabled={!jobs.data || page * jobs.data.pageSize >= jobs.data.total} onClick={() => setPage(page + 1)}>Older</button></div>
      </section>
      <section className={`card ${s.grid}`}>
        <h2>Actions</h2>
        {log.data?.items.length === 0 && <p className="muted">No actions yet. Actions you confirm from the library appear here.</p>}
        {log.data && log.data.items.length > 0 && (
          <table className={s.table}><thead><tr><th>When</th><th>Action</th><th>Title</th><th>Freed</th><th>Quality</th><th>Outcome</th><th>Detail</th></tr></thead>
            <tbody>{log.data.items.map((a) => (
              <tr key={a.id}><td className="muted">{relative(a.at)}</td><td>{a.type}</td><td>{a.title}</td><td className={s.mono}>{formatBytes(a.sizeBytesBefore)}</td>
                <td>{a.qualityBefore ?? '—'}{a.qualityAfter ? ` → ${a.qualityAfter}` : ''}</td><td>{chip(a.outcome)}</td><td className="muted">{a.detail}</td></tr>))}</tbody></table>
        )}
      </section>
    </div>
  );
}

function JobRow({ j, onCancel }: { j: Job; onCancel: () => void }) {
  const live = useJobProgress(j.id);
  const p = live?.kind === 'progress' ? live : j.progress;
  const sm = j.summary;
  const summary = sm ? [sm.filesSeen ? `${sm.filesSeen.toLocaleString()} files` : null, sm.filesProbed ? `${sm.filesProbed.toLocaleString()} read` : null, sm.filesAdded ? `${sm.filesAdded} added` : null, sm.filesRemoved ? `${sm.filesRemoved} removed` : null, sm.itemsMatched ? `${sm.itemsMatched.toLocaleString()} matched` : null, sm.itemsUnmatched ? `${sm.itemsUnmatched} unmatched` : null].filter(Boolean).join(' · ') : '';
  return (
    <tr>
      <td>{j.type}</td><td className="muted">{j.trigger}</td><td>{chip(j.status)}</td><td className="muted">{j.startedAt ? relative(j.startedAt) : '—'}</td><td className={s.mono}>{duration(j)}</td>
      <td>{j.status === 'running' && p ? `${p.phase ?? ''} ${p.done.toLocaleString()} / ${p.total.toLocaleString()}` : summary}{j.error && <div className={s.bad}>{j.error}</div>}{sm?.errors.map((e, i) => <div key={i} className={s.warn}>{e}</div>)}</td>
      <td>{j.status === 'running' && <button className="btn" onClick={onCancel}>Cancel</button>}</td>
    </tr>
  );
}
