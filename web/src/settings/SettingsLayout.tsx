import { NavLink, Outlet } from 'react-router-dom';
import s from './settings.module.css';

export default function SettingsLayout() {
  const tabs = [['/settings/connections', 'Connections'], ['/settings/scanning', 'Scanning'], ['/settings/account', 'Account']];
  return (
    <div>
      <nav className={s.tabs} aria-label="Settings">{tabs.map(([to, label]) => <NavLink key={to} to={to} className={({ isActive }) => `${s.tab} ${isActive ? s.tabActive : ''}`}>{label}</NavLink>)}</nav>
      <Outlet />
    </div>
  );
}
