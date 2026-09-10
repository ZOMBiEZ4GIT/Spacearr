const stops: [number, [number, number, number]][] = [
  [0, [56, 0.12, 155]], [0.4, [75, 0.15, 100]], [0.7, [68, 0.17, 55]], [1, [60, 0.19, 25]],
];
export const UNKNOWN = 'oklch(55% 0.01 200)';
const fmt = (l: number, c: number, h: number) => `oklch(${+l.toFixed(1)}% ${+c.toFixed(3)} ${+h.toFixed(1)})`;

export function heatColor(heat: number): string {
  if (!(heat >= 0)) return UNKNOWN;
  const t = Math.min(1, heat);
  for (let i = 1; i < stops.length; i++) {
    const [t0, a] = stops[i - 1]; const [t1, b] = stops[i];
    if (t <= t1) { const f = (t - t0) / (t1 - t0); return fmt(a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f); }
  }
  return fmt(...stops[stops.length - 1][1]);
}

const hues = [250, 30, 145, 320, 200, 60, 280, 10, 175, 95, 225, 340];
export function categoryColor(value: string | null | undefined): string {
  if (!value) return UNKNOWN;
  let h = 0; for (const ch of value) h = (h * 31 + ch.charCodeAt(0)) | 0;
  return `oklch(68% 0.13 ${hues[Math.abs(h) % hues.length]})`;
}

export const supportsOklch = typeof CSS !== 'undefined' && typeof CSS.supports === 'function' && CSS.supports('color', 'oklch(50% 0.1 100)');
