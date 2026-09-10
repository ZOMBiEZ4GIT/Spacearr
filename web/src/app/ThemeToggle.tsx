import { useEffect, useState } from 'react';

type Theme = 'dark' | 'light' | 'system';
function read(): Theme { try { return (localStorage.getItem('spacearr.theme') as Theme) || 'system'; } catch { return 'system'; } }
function apply(t: Theme) { if (t === 'system') delete document.documentElement.dataset.theme; else document.documentElement.dataset.theme = t; }

export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(read);
  useEffect(() => {
    apply(theme);
    try {
      if (theme === 'system') localStorage.removeItem('spacearr.theme');
      else localStorage.setItem('spacearr.theme', theme);
    } catch { /* ignore */ }
  }, [theme]);
  return (
    <label className="field" style={{ gridAutoFlow: 'column', alignItems: 'center', gap: 8 }}>
      <span className="muted">Theme</span>
      <select id="theme-select" value={theme} onChange={(e) => setTheme(e.target.value as Theme)}>
        <option value="system">System</option><option value="dark">Dark</option><option value="light">Light</option>
      </select>
    </label>
  );
}
