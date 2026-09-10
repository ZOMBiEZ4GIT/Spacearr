import { useState } from 'react';
import { useAddRoot, useDeleteRoot, useRoots, useUpdateRoot, useValidatePath } from '../api/hooks';
import type { ValidatePath } from '../api/types';
import { ApiError } from '../api/client';
import { relative } from '../app/ScanPill';
import s from './settings.module.css';

export default function RootFolderList() {
  const roots = useRoots();
  const add = useAddRoot();
  const update = useUpdateRoot();
  const del = useDeleteRoot();
  const validate = useValidatePath();
  const [path, setPath] = useState('');
  const [check, setCheck] = useState<ValidatePath | null>(null);
  const [error, setError] = useState<string | null>(null);

  const onCheck = async () => {
    setError(null);
    try { setCheck(await validate.mutateAsync(path)); }
    catch (err) { setError(err instanceof ApiError ? err.message : 'Could not check that folder.'); setCheck(null); }
  };
  const onAdd = async () => {
    setError(null);
    try { await add.mutateAsync(path); setPath(''); setCheck(null); }
    catch (err) { setError(err instanceof ApiError ? err.message : 'Could not add the folder.'); }
  };

  return (
    <div className={s.grid}>
      {roots.data && roots.data.length > 0 && (
        <table className={s.table}><thead><tr><th>Folder</th><th>Status</th><th>Last scan</th><th>Enabled</th><th /></tr></thead>
          <tbody>{roots.data.map((r) => (
            <tr key={r.id}>
              <td className={s.mono}>{r.path}</td>
              <td className={r.exists ? s.ok : s.bad}>{r.exists ? 'Visible' : 'Not found — check the volume mount'}</td>
              <td className="muted">{r.lastScanAt ? relative(r.lastScanAt) : 'never'}</td>
              <td><input id={`root-enabled-${r.id}`} type="checkbox" checked={r.enabled} onChange={(e) => update.mutate({ id: r.id, path: r.path, enabled: e.target.checked })} aria-label={`Enable ${r.path}`} /></td>
              <td><button className="btn" onClick={() => del.mutate(r.id)}>Remove</button></td>
            </tr>))}</tbody></table>
      )}
      <div className={s.row}>
        <div className="field" style={{ flex: 1 }}><label htmlFor="root-path">Library folder as Spacearr sees it</label>
          <input id="root-path" value={path} onChange={(e) => { setPath(e.target.value); setCheck(null); }} placeholder="/media/movies" /></div>
        <button className="btn" onClick={onCheck} disabled={!path || validate.isPending} style={{ alignSelf: 'end' }}>Check</button>
        <button className="btn btn-primary" onClick={onAdd} disabled={!check?.exists || add.isPending} style={{ alignSelf: 'end' }}>Add folder</button>
      </div>
      {check && (check.exists
        ? <p className={s.ok}>Found {check.mediaFileCountSample >= 200 ? '200+' : check.mediaFileCountSample} media files{check.sampleFiles[0] ? <>, e.g. <span className={s.mono}>{check.sampleFiles[0]}</span></> : ''}.</p>
        : <p className={s.bad}>Spacearr can't see that folder. Inside Docker, check the volume is mounted at that path.</p>)}
      {error && <p className="error" role="alert">{error}</p>}
    </div>
  );
}
