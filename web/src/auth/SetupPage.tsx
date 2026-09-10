import { FormEvent, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useLogin, useSetup, useStatus } from '../api/hooks';
import { ApiError } from '../api/client';

export default function SetupPage() {
  const status = useStatus();
  const setup = useSetup();
  const login = useLogin();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  if (status.data?.setupComplete) return <Navigate to="/" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setError(null);
    try {
      await setup.mutateAsync({ username, password });
      await login.mutateAsync({ username, password });
      navigate('/setup/wizard', { replace: true });
    } catch (err) { setError(err instanceof ApiError ? err.message : 'Could not create the account.'); }
  };

  return (
    <main className="auth-page">
      <form className="card auth-card" onSubmit={submit}>
        <h1>Welcome to Spacearr</h1>
        <p className="muted">Create the admin account. Sign-in is required for every page and every API call.</p>
        <div className="field"><label htmlFor="setup-username">Username</label><input id="setup-username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" required /></div>
        <div className="field"><label htmlFor="setup-password">Password</label><input id="setup-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" minLength={10} required /><span className="hint">At least 10 characters.</span></div>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn btn-primary" disabled={setup.isPending || login.isPending}>Create account</button>
      </form>
    </main>
  );
}
