import { useStatus } from '../api/hooks';
import s from './settings.module.css';

export default function ToolStatus({ ffprobePath, mediainfoPath, onChange }: { ffprobePath: string; mediainfoPath: string; onChange: (k: 'ffprobePath' | 'mediainfoPath', v: string) => void }) {
  const status = useStatus();
  const t = status.data?.tools;
  return (
    <div className={s.grid}>
      <p><strong>ffprobe</strong> <span className={t?.ffprobe ? s.ok : s.bad}>{t?.ffprobe ? 'found' : 'not found — scans will read sizes only'}</span></p>
      <div className="field"><label htmlFor="tool-ffprobe">ffprobe path (optional)</label><input id="tool-ffprobe" value={ffprobePath} onChange={(e) => onChange('ffprobePath', e.target.value)} placeholder="Leave blank to search PATH" /></div>
      <p><strong>mediainfo</strong> <span className={t?.mediainfo ? s.ok : 'muted'}>{t?.mediainfo ? 'found' : 'not found (optional)'}</span></p>
      <div className="field"><label htmlFor="tool-mediainfo">mediainfo path (optional)</label><input id="tool-mediainfo" value={mediainfoPath} onChange={(e) => onChange('mediainfoPath', e.target.value)} /></div>
    </div>
  );
}
