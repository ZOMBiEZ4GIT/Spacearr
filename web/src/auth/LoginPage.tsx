import { FormEvent, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useLogin } from '../api/hooks';
import { ApiError } from '../api/client';

export default function LoginPage() {
  const login = useLogin();
  const navigate = useNavigate();
  const location = useLocation() as { state?: { from?: string } };
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setError(null);
    try { await login.mutateAsync({ username, password }); navigate(location.state?.from ?? '/', { replace: true }); }
    catch (err) { setError(err instanceof ApiError ? (err.status === 429 ? 'Too many attempts. Wait a minute and try again.' : 'Wrong username or password.') : 'Sign-in failed.'); }
  };
  return (
    <main className="auth-page">
      <form className="card auth-card" onSubmit={submit}>
        <h1>Spacearr</h1>
        <div className="field"><label htmlFor="login-username">Username</label><input id="login-username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" required /></div>
        <div className="field"><label htmlFor="login-password">Password</label><input id="login-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required /></div>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn btn-primary" disabled={login.isPending}>Sign in</button>
      </form>
    </main>
  );
}
