import { FormEvent, useState } from 'react';
import { useCreateInstance, useTestConnection, useUpdateInstance } from '../api/hooks';
import type { ArrType, Instance, TestResult } from '../api/types';
import { ApiError } from '../api/client';
import s from './settings.module.css';

interface Props { initial?: Instance; onSaved: (i: Instance) => void; onCancel?: () => void }

export default function ConnectionForm({ initial, onSaved, onCancel }: Props) {
  const [type, setType] = useState<ArrType>(initial?.type ?? 'radarr');
  const [name, setName] = useState(initial?.name ?? '');
  const [baseUrl, setBaseUrl] = useState(initial?.baseUrl ?? '');
  const [apiKey, setApiKey] = useState('');
  const [tested, setTested] = useState<{ key: string; result: TestResult } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const test = useTestConnection();
  const create = useCreateInstance();
  const update = useUpdateInstance();
  const fingerprint = `${type}|${baseUrl}|${apiKey}`;
  const testedOk = tested?.key === fingerprint && tested.result.ok;
  const canSave = !!name && !!baseUrl && (initial ? true : testedOk);

  const runTest = async () => {
    setError(null);
    try { const result = await test.mutateAsync({ type, baseUrl, apiKey }); setTested({ key: fingerprint, result }); }
    catch (err) { setError(err instanceof ApiError ? err.message : 'Test failed.'); }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setError(null);
    try {
      if (initial) { await update.mutateAsync({ id: initial.id, type, name, baseUrl, enabled: initial.enabled, apiKey }); onSaved({ ...initial, type, name, baseUrl }); }
      else onSaved(await create.mutateAsync({ type, name, baseUrl, apiKey }));
    } catch (err) { setError(err instanceof ApiError ? err.message : 'Could not save.'); }
  };

  return (
    <form className={`card ${s.grid}`} onSubmit={submit}>
      <div className="field"><label htmlFor="conn-type">App</label>
        <select id="conn-type" value={type} onChange={(e) => setType(e.target.value as ArrType)}><option value="radarr">Radarr</option><option value="sonarr">Sonarr</option></select></div>
      <div className="field"><label htmlFor="conn-name">Name</label><input id="conn-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={type === 'radarr' ? 'Movies' : 'TV'} required /></div>
      <div className="field"><label htmlFor="conn-url">URL</label><input id="conn-url" value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder={type === 'radarr' ? 'http://radarr:7878' : 'http://sonarr:8989'} required />
        <span className="hint">Inside Docker use the container name, e.g. http://radarr:7878, not localhost.</span></div>
      <div className="field"><label htmlFor="conn-key">API key</label><input id="conn-key" value={apiKey} onChange={(e) => setApiKey(e.target.value)} autoComplete="off" />
        <span className="hint">{initial ? 'Leave blank to keep the current key.' : `Found in ${type === 'radarr' ? 'Radarr' : 'Sonarr'} under Settings > General > Security.`}</span></div>
      <div className={s.row}>
        <button type="button" className="btn" onClick={runTest} disabled={test.isPending || !baseUrl}>Test</button>
        {tested?.key === fingerprint && tested.result.ok && (
          <span className={s.ok}>Connected to {tested.result.appName} {tested.result.version} · {tested.result.rootFolders.length} root folder{tested.result.rootFolders.length === 1 ? '' : 's'} · {tested.result.profiles.length} quality profiles</span>
        )}
        {tested?.key === fingerprint && !tested.result.ok && <span className={s.bad} role="alert">{tested.result.error}</span>}
      </div>
      {tested?.key === fingerprint && tested.result.ok && tested.result.rootFolders.length > 0 && (
        <div className={s.row}>{tested.result.rootFolders.map((r) => <span key={r} className="chip">{r}</span>)}</div>
      )}
      {error && <p className="error" role="alert">{error}</p>}
      <div className={s.row}>
        <button className="btn btn-primary" disabled={!canSave || create.isPending || update.isPending}>Save</button>
        {onCancel && <button type="button" className="btn" onClick={onCancel}>Cancel</button>}
      </div>
    </form>
  );
}
