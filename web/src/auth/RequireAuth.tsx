import { useEffect } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useMe, useStatus } from '../api/hooks';

export default function RequireAuth() {
  const status = useStatus();
  const me = useMe();
  const qc = useQueryClient();
  const location = useLocation();
  useEffect(() => {
    // Re-run the query that spotted the 401 rather than navigating imperatively here:
    // during first-run (setup incomplete) the initial /auth/me call also 401s, and an
    // unconditional navigate('/login') would race ahead of the setupComplete check below
    // and send a fresh install to /login instead of /setup. Invalidating lets the
    // declarative checks below (which already order setup before login) decide.
    const onUnauthorized = () => { void qc.invalidateQueries({ queryKey: ['me'] }); };
    window.addEventListener('spacearr:unauthorized', onUnauthorized);
    return () => window.removeEventListener('spacearr:unauthorized', onUnauthorized);
  }, [qc]);
  if (status.isLoading || me.isLoading) return <p className="muted" style={{ padding: 24 }}>Loading…</p>;
  if (status.data && !status.data.setupComplete) return <Navigate to="/setup" replace />;
  if (me.isError) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}
