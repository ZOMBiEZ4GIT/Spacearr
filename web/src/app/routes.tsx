import { Suspense, lazy } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import Shell from './Shell';
import RequireAuth from '../auth/RequireAuth';
import SetupPage from '../auth/SetupPage';
import LoginPage from '../auth/LoginPage';
import FirstRunWizard from '../setup/FirstRunWizard';
import SettingsLayout from '../settings/SettingsLayout';
import ConnectionsPage from '../settings/ConnectionsPage';
import ScanningPage from '../settings/ScanningPage';
import AccountPage from '../settings/AccountPage';

// DEV-only treemap harness. Lazily imported so the sample data never reaches a production bundle.
const DevTreemapPage = import.meta.env.DEV ? lazy(() => import('../treemap/DevTreemapPage')) : null;

const Placeholder = ({ name }: { name: string }) => <div style={{ padding: 24 }}><h1>{name}</h1></div>;

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/setup" element={<SetupPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/setup/wizard" element={<FirstRunWizard />} />
        <Route element={<Shell />}>
          <Route path="/" element={<Navigate to="/library" replace />} />
          <Route path="/library" element={<Placeholder name="Library" />} />
          <Route path="/duplicates" element={<Placeholder name="Duplicates" />} />
          <Route path="/activity" element={<Placeholder name="Activity" />} />
          {DevTreemapPage && <Route path="/dev/treemap" element={<Suspense fallback={null}><DevTreemapPage /></Suspense>} />}
          <Route path="/settings" element={<SettingsLayout />}>
            <Route index element={<Navigate to="/settings/connections" replace />} />
            <Route path="connections" element={<ConnectionsPage />} />
            <Route path="scanning" element={<ScanningPage />} />
            <Route path="account" element={<AccountPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
