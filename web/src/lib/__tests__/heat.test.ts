import { heatColor, categoryColor } from '../heat';

describe('heat colours', () => {
  it('ramps from green to red and handles unknown', () => {
    expect(heatColor(0)).toMatch(/^oklch\(56(\.0+)?% 0\.12 155\)$/);
    expect(heatColor(1)).toMatch(/^oklch\(60(\.0+)?% 0\.19 25\)$/);
    expect(heatColor(-1)).toBe('oklch(55% 0.01 200)');
    const mid = heatColor(0.55);
    expect(mid.startsWith('oklch(')).toBe(true);
    expect(mid).not.toBe(heatColor(0));
  });
  it('categorical is stable and distinct', () => {
    expect(categoryColor('hevc')).toBe(categoryColor('hevc'));
    expect(categoryColor('hevc')).not.toBe(categoryColor('h264'));
    expect(categoryColor(null)).toBe('oklch(55% 0.01 200)');
  });
});
