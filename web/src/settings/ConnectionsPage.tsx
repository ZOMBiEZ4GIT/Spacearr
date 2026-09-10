import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDeleteInstance, useInstances, useTestInstance } from '../api/hooks';
import type { Instance, TestResult } from '../api/types';
import { ApiError } from '../api/client';
import ConnectionForm from './ConnectionForm';
import MappingEditor from './MappingEditor';
import { relative } from '../app/ScanPill';
import s from './settings.module.css';

export default function ConnectionsPage() {
  const instances = useInstances();
  const [adding, setAdding] = useState(false);
  return (
    <div className={s.page}>
      <div className={s.cardHead}><h1>Connections</h1><button className="btn btn-primary" onClick={() => setAdding(true)}>Add connection</button></div>
      {adding && <ConnectionForm onSaved={() => setAdding(false)} onCancel={() => setAdding(false)} />}
      {instances.data?.length === 0 && !adding && <p className="muted">No connections yet. Add Radarr or Sonarr to label your files, or <Link to="/setup/wizard">run setup</Link>.</p>}
      {instances.data?.map((i) => <InstanceCard key={i.id} instance={i} />)}
      <p><Link to="/setup/wizard" className="muted">Run setup again</Link></p>
    </div>
  );
}

function InstanceCard({ instance }: { instance: Instance }) {
  const [editing, setEditing] = useState(false);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [test, setTest] = useState<TestResult | null>(null);
  const [testError, setTestError] = useState<string | null>(null);
  const del = useDeleteInstance();
  const run = useTestInstance();
  const onTest = async () => {
    setTestError(null);
    try { setTest(await run.mutateAsync(instance.id)); }
    catch (err) { setTestError(err instanceof ApiError ? err.message : 'Test failed.'); }
  };
  if (editing) return <ConnectionForm initial={instance} onSaved={() => setEditing(false)} onCancel={() => setEditing(false)} />;
  return (
    <section className={`card ${s.grid}`}>
      <div className={s.cardHead}>
        <div className={s.row}><span className={`${s.badge} ${instance.type === 'radarr' ? s.radarr : s.sonarr}`}>{instance.type === 'radarr' ? 'Radarr' : 'Sonarr'}</span><strong>{instance.name}</strong><span className={`muted ${s.mono}`}>{instance.baseUrl}</span></div>
        <div className={s.row}>
          <button className="btn" onClick={onTest} disabled={run.isPending}>Test</button>
          <button className="btn" onClick={() => setEditing(true)}>Edit</button>
          {!confirmRemove ? <button className="btn" onClick={() => setConfirmRemove(true)}>Remove</button>
            : <><span>Remove {instance.name}?</span><button className="btn btn-danger" onClick={() => del.mutate(instance.id)}>Yes, remove</button><button className="btn" onClick={() => setConfirmRemove(false)}>No</button></>}
        </div>
      </div>
      {test && <p className={test.ok ? s.ok : s.bad} role="status">{test.ok ? `Connected to ${test.appName} ${test.version}` : test.error}</p>}
      {testError && <p className={s.bad} role="alert">{testError}</p>}
      <p className="muted">{instance.lastSyncAt ? `Last synced ${relative(instance.lastSyncAt)}` : 'Not synced yet'}{instance.lastSyncError && <span className={s.bad}> · {instance.lastSyncError}</span>}</p>
      <MappingEditor instance={instance} />
    </section>
  );
}
