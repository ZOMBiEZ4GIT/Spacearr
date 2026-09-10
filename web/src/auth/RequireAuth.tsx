import { useEffect, useRef } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useMe, useStatus } from '../api/hooks';

export default function RequireAuth() {
  const status = useStatus();
  const me = useMe();
  const qc = useQueryClient();
  const location = useLocation();
  const pathRef = useRef(location.pathname);
  useEffect(() => { pathRef.current = location.pathname; }, [location.pathname]);

  useEffect(() => {
    const onUnauthorized = (e: Event) => {
      const path = (e as CustomEvent<{ path?: string }>).detail?.path;
      // /auth/me 401ing is expected while signed out — it must not re-trigger itself.
      if (path === '/api/v1/auth/me') return;
      // Don't touch the cache while already on the auth routes; they own their own flow.
      if (pathRef.current === '/login' || pathRef.current === '/setup') return;
      qc.removeQueries({ queryKey: ['me'] });
    };
    window.addEventListener('spacearr:unauthorized', onUnauthorized);
    return () => window.removeEventListener('spacearr:unauthorized', onUnauthorized);
  }, [qc]);

  if (status.isLoading || (!me.data && me.isFetching)) return <p className="muted" style={{ padding: 24 }}>Loading…</p>;
  if (status.data && !status.data.setupComplete) return <Navigate to="/setup" replace />;
  if (!me.data && me.isError && !me.isFetching) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}
