import { useState } from 'react';
import { useAddMapping, useDeleteMapping, useSuggestMappings } from '../api/hooks';
import type { Instance, MappingSuggestion } from '../api/types';
import s from './settings.module.css';

export default function MappingEditor({ instance }: { instance: Instance }) {
  const add = useAddMapping();
  const del = useDeleteMapping();
  const suggest = useSuggestMappings();
  const [remote, setRemote] = useState('');
  const [local, setLocal] = useState('');
  const [suggestions, setSuggestions] = useState<MappingSuggestion[] | null>(null);
  const total = instance.matched + instance.unmatched;

  return (
    <div className={s.grid}>
      <p className={total === 0 ? 'muted' : instance.unmatched === 0 ? s.ok : s.warn}>
        {total === 0 ? 'Run a scan to see how many files match.' : `Matched ${instance.matched.toLocaleString()} of ${total.toLocaleString()} files${instance.unmatched > 0 ? `. ${instance.unmatched.toLocaleString()} unmatched — usually a path mapping is missing.` : '.'}`}
      </p>
      <p className="muted">A mapping translates the path {instance.type === 'radarr' ? 'Radarr' : 'Sonarr'} reports into the path Spacearr sees. Leave empty when both see the same paths.</p>
      {instance.mappings.length > 0 && (
        <table className={s.table}><thead><tr><th>{instance.type === 'radarr' ? 'Radarr' : 'Sonarr'} path</th><th>Spacearr path</th><th /></tr></thead>
          <tbody>{instance.mappings.map((m) => (
            <tr key={m.id}><td className={s.mono}>{m.remotePrefix}</td><td className={s.mono}>{m.localPrefix}</td>
              <td><button className="btn" onClick={() => del.mutate({ id: instance.id, mappingId: m.id })}>Remove</button></td></tr>))}</tbody></table>
      )}
      <div className={s.row}>
        <button className="btn" onClick={async () => setSuggestions(await suggest.mutateAsync(instance.id))} disabled={suggest.isPending}>Suggest mappings</button>
        {suggestions?.length === 0 && <span className="muted">No suggestions: the paths already match, or add a library folder first.</span>}
      </div>
      {suggestions && suggestions.length > 0 && (
        <table className={s.table}><tbody>{suggestions.map((sg) => (
          <tr key={sg.remotePrefix + sg.localPrefix}><td className={s.mono}>{sg.remotePrefix}</td><td className={s.mono}>{sg.localPrefix}</td>
            <td><span className="chip">{sg.confidence === 'high' ? 'likely' : 'possible'}</span></td>
            <td><button className="btn" onClick={() => add.mutate({ id: instance.id, remotePrefix: sg.remotePrefix, localPrefix: sg.localPrefix })}>Add</button></td></tr>))}</tbody></table>
      )}
      <form className={s.row} onSubmit={(e) => { e.preventDefault(); add.mutate({ id: instance.id, remotePrefix: remote, localPrefix: local }, { onSuccess: () => { setRemote(''); setLocal(''); } }); }}>
        <div className="field"><label htmlFor={`map-remote-${instance.id}`}>{instance.type === 'radarr' ? 'Radarr' : 'Sonarr'} path</label><input id={`map-remote-${instance.id}`} value={remote} onChange={(e) => setRemote(e.target.value)} placeholder="/data/movies" /></div>
        <div className="field"><label htmlFor={`map-local-${instance.id}`}>Spacearr path</label><input id={`map-local-${instance.id}`} value={local} onChange={(e) => setLocal(e.target.value)} placeholder="/media/movies" /></div>
        <button className="btn" disabled={!remote || !local || add.isPending} style={{ alignSelf: 'end' }}>Add mapping</button>
      </form>
    </div>
  );
}
