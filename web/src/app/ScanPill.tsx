import { Link } from 'react-router-dom';
import { useRunningJob } from '../api/events';
import { useJobs } from '../api/hooks';

export default function ScanPill() {
  const running = useRunningJob();
  const jobs = useJobs(1);
  if (running) {
    const pct = running.total > 0 ? Math.round((running.done / running.total) * 100) : 0;
    const label = running.type === 'scan' ? (running.phase === 'probe' ? 'Reading files' : running.phase === 'enrich' ? 'Matching titles' : 'Scanning') : running.type === 'enrich' ? 'Matching titles' : 'Working';
    return (
      <Link to="/activity" className="scan-pill" aria-live="polite">
        <span>{label} <span className="mono">{running.done.toLocaleString()} / {running.total.toLocaleString()}</span></span>
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
