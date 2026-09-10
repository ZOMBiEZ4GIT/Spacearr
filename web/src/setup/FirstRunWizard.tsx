import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useInstances, useStartScan } from '../api/hooks';
import { startEvents, useJobProgress } from '../api/events';
import type { Instance } from '../api/types';
import ConnectionForm from '../settings/ConnectionForm';
import MappingEditor from '../settings/MappingEditor';
import RootFolderList from '../settings/RootFolderList';
import s from '../settings/settings.module.css';

const steps = ['Connect', 'Map paths', 'Library folders', 'Scan'];

export default function FirstRunWizard() {
  const [step, setStep] = useState(0);
  const [instance, setInstance] = useState<Instance | null>(null);
  const instances = useInstances();
  const scan = useStartScan();
  const qc = useQueryClient();
  const [jobId, setJobId] = useState<number | undefined>();
  const progress = useJobProgress(jobId);
  const live = instances.data?.find((i) => i.id === instance?.id) ?? instance;
  // The wizard runs outside the app Shell (no nav chrome yet), which is normally what
  // opens the live-progress connection. Start it here too so step 4's progress line
  // and "Scan complete." work on the very first run, before Shell ever mounts.
  useEffect(() => { startEvents(qc); }, [qc]);

  return (
    <main className={s.page} style={{ margin: '0 auto', paddingTop: 32 }}>
      <h1>Set up Spacearr</h1>
      <ol className={s.stepper}>{steps.map((label, i) => <li key={label} className={i === step ? s.stepOn : ''}>{i + 1}. {label}</li>)}</ol>
      {step === 0 && <>
        <p className="muted">Connect Radarr or Sonarr so files get titles, posters and quality profiles. You can add more later.</p>
        <ConnectionForm onSaved={(i) => { setInstance(i); setStep(1); }} />
        <p><Link to="/library" className="muted">Skip for now</Link></p></>}
      {step === 1 && live && <>
        <p className="muted">Spacearr reads the disk itself, so it needs to know how {live.type === 'radarr' ? 'Radarr' : 'Sonarr'}'s paths map to yours. Add a library folder in the next step first if suggestions are empty.</p>
        <div className="card"><MappingEditor instance={live} /></div>
        <div className={s.row}><button className="btn btn-primary" onClick={() => setStep(2)}>Next</button><button className="btn" onClick={() => setStep(2)}>Skip, paths match</button></div></>}
      {step === 2 && <>
        <p className="muted">Which folders should Spacearr scan? Use the paths as Spacearr sees them.</p>
        <div className="card"><RootFolderList /></div>
        <div className={s.row}><button className="btn" onClick={() => setStep(1)}>Back to mappings</button><button className="btn btn-primary" onClick={() => setStep(3)}>Next</button></div></>}
      {step === 3 && <>
        <p className="muted">The first scan reads every file with ffprobe. Large libraries take a while; you can close this page and watch progress in Activity.</p>
        {!jobId && <button className="btn btn-primary" onClick={async () => setJobId((await scan.mutateAsync()).jobId)} disabled={scan.isPending}>Start scan</button>}
        <div aria-live="polite">
          {progress && progress.kind === 'progress' && <p>{progress.phase === 'probe' ? 'Reading files' : progress.phase === 'enrich' ? 'Matching titles' : 'Discovering'} <span className="mono">{progress.done.toLocaleString()} / {progress.total.toLocaleString()}</span></p>}
          {progress && progress.kind === 'finished' && <p className={progress.status === 'succeeded' ? s.ok : s.bad}>{progress.status === 'succeeded' ? 'Scan complete.' : `Scan ${progress.status}: ${progress.detail ?? ''}`}</p>}
        </div>
        <div className={s.row}><Link className="btn btn-primary" to="/library">Open library</Link></div>
        {jobId !== undefined && progress?.kind !== 'finished' && <p className="muted">The scan keeps running; the library fills in as it goes.</p>}</>}
    </main>
  );
}
