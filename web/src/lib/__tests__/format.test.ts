import { formatBytes, formatBitrate, formatDuration } from '../format';

describe('format', () => {
  it('bytes', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(512_000_000)).toBe('512 MB');
    expect(formatBytes(1_400_000_000)).toBe('1.4 GB');
    expect(formatBytes(2_310_000_000_000)).toBe('2.31 TB');
  });
  it('bitrate', () => {
    expect(formatBitrate(8_000_000)).toBe('8.0 Mbps');
    expect(formatBitrate(null)).toBe('—');
  });
  it('duration', () => {
    expect(formatDuration(6120)).toBe('1h 42m');
    expect(formatDuration(2880)).toBe('48m');
    expect(formatDuration(undefined)).toBe('—');
  });
});
