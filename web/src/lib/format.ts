export function formatBytes(n: number): string {
  if (!Number.isFinite(n) || n <= 0) return '0 B';
  if (n < 1e6) return `${Math.round(n / 1e3)} kB`;
  if (n < 1e9) return `${Math.round(n / 1e6)} MB`;
  if (n < 1e12) return `${(n / 1e9).toFixed(1)} GB`;
  return `${(n / 1e12).toFixed(2)} TB`;
}
export function formatBitrate(bps?: number | null): string {
  if (bps == null || bps <= 0) return '—';
  return `${(bps / 1e6).toFixed(1)} Mbps`;
}
export function formatDuration(s?: number | null): string {
  if (s == null || s <= 0) return '—';
  if (s < 60) return `${Math.round(s)}s`;
  const h = Math.floor(s / 3600); const m = Math.round((s % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}
