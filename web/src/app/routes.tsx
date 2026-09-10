import { Navigate, Route, Routes } from 'react-router-dom';
import Shell from './Shell';
import RequireAuth from '../auth/RequireAuth';
import SetupPage from '../auth/SetupPage';
import LoginPage from '../auth/LoginPage';

const Placeholder = ({ name }: { name: string }) => <div style={{ padding: 24 }}><h1>{name}</h1></div>;

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/setup" element={<SetupPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/setup/wizard" element={<Placeholder name="First run" />} />
        <Route element={<Shell />}>
          <Route path="/" element={<Navigate to="/library" replace />} />
          <Route path="/library" element={<Placeholder name="Library" />} />
          <Route path="/duplicates" element={<Placeholder name="Duplicates" />} />
          <Route path="/activity" element={<Placeholder name="Activity" />} />
          <Route path="/settings/connections" element={<Placeholder name="Connections" />} />
          <Route path="/settings/scanning" element={<Placeholder name="Scanning" />} />
          <Route path="/settings/account" element={<Placeholder name="Account" />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
