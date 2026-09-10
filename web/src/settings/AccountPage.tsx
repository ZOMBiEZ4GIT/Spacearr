import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useChangePassword, useLogout, useMe, useRegenerateApiKey } from '../api/hooks';
import s from './settings.module.css';

export default function AccountPage() {
  const me = useMe();
  const regen = useRegenerateApiKey();
  const change = useChangePassword();
  const logout = useLogout();
  const navigate = useNavigate();
  const [show, setShow] = useState(false);
  const [current, setCurrent] = useState('');
  const [pw, setPw] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setError(null); setDone(false);
    try { await change.mutateAsync({ currentPassword: current, newPassword: pw }); setCurrent(''); setPw(''); setDone(true); }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not change the password.'); }
  };
  return (
    <div className={s.page}>
      <h1>Account</h1>
      <section className={`card ${s.grid}`}>
        <p>Signed in as <strong>{me.data?.username}</strong></p>
        <div className="field"><label htmlFor="api-key">API key</label>
          <div className={s.row}>
            <input id="api-key" readOnly value={show ? me.data?.apiKey ?? '' : '••••••••••••••••••••••••••••••••'} className={s.mono} style={{ flex: 1 }} />
            <button className="btn" onClick={() => setShow(!show)}>{show ? 'Hide' : 'Show'}</button>
            <button className="btn" onClick={() => navigator.clipboard.writeText(me.data?.apiKey ?? '')}>Copy</button>
            <button className="btn" onClick={() => regen.mutate()}>Regenerate</button>
          </div>
          <span className="hint">Send it as the X-Api-Key header for scripts.</span></div>
      </section>
      <form className={`card ${s.grid}`} onSubmit={submit}>
        <h2>Change password</h2>
        <div className="field"><label htmlFor="current-pw">Current password</label><input id="current-pw" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} required autoComplete="current-password" /></div>
        <div className="field"><label htmlFor="new-pw">New password</label><input id="new-pw" type="password" value={pw} onChange={(e) => setPw(e.target.value)} minLength={10} required autoComplete="new-password" /></div>
        <div className={s.row}><button className="btn btn-primary" disabled={change.isPending}>Change password</button>{done && <span className={s.ok}>Changed</span>}{error && <span className={s.bad} role="alert">{error}</span>}</div>
      </form>
      <div><button className="btn" onClick={async () => { await logout.mutateAsync(); navigate('/login'); }}>Sign out</button></div>
    </div>
  );
}
