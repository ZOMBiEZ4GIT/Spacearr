import { Link } from 'react-router-dom';
import { useRunningJob } from '../api/events';
import { isTerminalJobStatus, useJobs } from '../api/hooks';

export default function ScanPill() {
  const running = useRunningJob();
  const jobs = useJobs(1);
  // The progress store alone can go stale: a `finished` event dropped by a disconnect (the SSE
  // stream has no replay) leaves a `kind: 'progress'` entry claiming a job is still running
  // forever. Trust "still scanning" only when the polled jobs list agrees the named job hasn't
  // already reached a terminal status - never rely on the store by itself.
  const jobEntry = running ? jobs.data?.items.find((j) => j.id === running.jobId) : undefined;
  const confirmedRunning = running && jobEntry && !isTerminalJobStatus(jobEntry.status) ? running : undefined;
  if (confirmedRunning) {
    const pct = confirmedRunning.total > 0 ? Math.round((confirmedRunning.done / confirmedRunning.total) * 100) : 0;
    const label = confirmedRunning.type === 'scan' ? (confirmedRunning.phase === 'probe' ? 'Reading files' : confirmedRunning.phase === 'enrich' ? 'Matching titles' : 'Scanning') : confirmedRunning.type === 'enrich' ? 'Matching titles' : 'Working';
    return (
      <Link to="/activity" className="scan-pill" aria-live="polite">
        <span>{label} <span className="mono">{confirmedRunning.done.toLocaleString()} / {confirmedRunning.total.toLocaleString()}</span></span>
        <span className="scan-bar"><span style={{ width: `${pct}%` }} /></span>
      </Link>
    );
  }
  const last = jobs.data?.items.find((j) => j.type === 'scan' && j.finishedAt);
  return <Link to="/activity" className="scan-pill muted">{last ? `Last scan ${relative(last.finishedAt!)}` : 'Not scanned yet'}</Link>;
}

export function relative(iso: string): string {
  const s = (Date.now() - new Date(iso).getTime()) / 1000;
  if (s < 60) return 'just now';
  if (s < 3600) return `${Math.round(s / 60)}m ago`;
  if (s < 86400) return `${Math.round(s / 3600)}h ago`;
  return `${Math.round(s / 86400)}d ago`;
}
