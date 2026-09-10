import { NavLink, Outlet } from 'react-router-dom';
import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { startEvents } from '../api/events';
import { useStartScan } from '../api/hooks';
import ScanPill from './ScanPill';
import ThemeToggle from './ThemeToggle';
import styles from './Shell.module.css';

const nav = [
  { to: '/library', label: 'Library', glyph: '▦' },
  { to: '/duplicates', label: 'Duplicates', glyph: '⧉' },
  { to: '/activity', label: 'Activity', glyph: '≡' },
  { to: '/settings/connections', label: 'Settings', glyph: '⚙' },
];

export default function Shell() {
  const qc = useQueryClient();
  const scan = useStartScan();
  useEffect(() => { startEvents(qc); }, [qc]);
  return (
    <div className={styles.shell}>
      <nav className={styles.rail} aria-label="Main">
        <div className={styles.brand}><span className={styles.mark} aria-hidden="true" /> <span className={styles.brandText}>Spacearr</span></div>
        {nav.map((n) => (
          <NavLink key={n.to} to={n.to} className={({ isActive }) => `${styles.navLink} ${isActive ? styles.active : ''}`}>
            <span aria-hidden="true" className={styles.glyph}>{n.glyph}</span><span className={styles.navText}>{n.label}</span>
          </NavLink>
        ))}
        <div className={styles.railFoot}><ThemeToggle /></div>
      </nav>
      <div className={styles.main}>
        <header className={styles.topbar}>
          <ScanPill />
          <button className="btn" onClick={() => scan.mutate()} disabled={scan.isPending}>Scan now</button>
        </header>
        <div className={styles.content}><Outlet /></div>
      </div>
    </div>
  );
}
