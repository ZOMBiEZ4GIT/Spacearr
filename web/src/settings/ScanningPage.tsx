import { useEffect, useState } from 'react';
import { useSaveSettings, useSettings } from '../api/hooks';
import type { AppSettings } from '../api/types';
import RootFolderList from './RootFolderList';
import ToolStatus from './ToolStatus';
import s from './settings.module.css';

export default function ScanningPage() {
  const settings = useSettings();
  const save = useSaveSettings();
  const [form, setForm] = useState<AppSettings | null>(null);
  useEffect(() => { if (settings.data && !form) setForm(settings.data); }, [settings.data, form]);
  if (!form) return <div className={s.page}><p className="muted">Loading…</p></div>;
  const set = <K extends keyof AppSettings>(k: K, v: AppSettings[K]) => setForm({ ...form, [k]: v });
  return (
    <div className={s.page}>
      <h1>Scanning</h1>
      <section className={`card ${s.grid}`}><h2>Library folders</h2><RootFolderList /></section>
      <section className={`card ${s.grid}`}>
        <h2>Schedule</h2>
        <div className="field"><label htmlFor="scan-interval">Scan every</label>
          <select id="scan-interval" value={form.scanIntervalHours} onChange={(e) => set('scanIntervalHours', Number(e.target.value))}>
            <option value={0}>Off (manual only)</option><option value={1}>1 hour</option><option value={3}>3 hours</option><option value={6}>6 hours</option><option value={12}>12 hours</option><option value={24}>24 hours</option>
          </select></div>
        <div className="field"><label htmlFor="scan-ext">File extensions</label><input id="scan-ext" value={form.extensions.join(', ')} onChange={(e) => set('extensions', e.target.value.split(',').map((x) => x.trim()).filter(Boolean))} /></div>
      </section>
      <section className={`card ${s.grid}`}>
        <h2>Heat</h2>
        <label className={s.row}><input type="radio" name="heat" checked={form.heatMode === 'relative'} onChange={() => set('heatMode', 'relative')} /> <span><strong>Relative</strong> <span className="muted">— colour by rank within your library. Always shows a spread, even if everything is a remux.</span></span></label>
        <label className={s.row}><input type="radio" name="heat" checked={form.heatMode === 'absolute'} onChange={() => set('heatMode', 'absolute')} /> <span><strong>Absolute</strong> <span className="muted">— colour by bits per pixel against fixed thresholds. Green means efficient regardless of neighbours.</span></span></label>
      </section>
      <section className={`card ${s.grid}`}><h2>Tools</h2><ToolStatus ffprobePath={form.ffprobePath ?? ''} mediainfoPath={form.mediainfoPath ?? ''} onChange={(k, v) => set(k, v || null)} /></section>
      <div className={s.row}>
        <button className="btn btn-primary" onClick={() => save.mutate(form)} disabled={save.isPending}>Save</button>
        {save.isSuccess && <span className={s.ok}>Saved</span>}
        {save.isError && <span className={s.bad} role="alert">{(save.error as Error).message}</span>}
      </div>
    </div>
  );
}
