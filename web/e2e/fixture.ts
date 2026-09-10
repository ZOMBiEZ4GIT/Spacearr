import { execFileSync } from 'node:child_process';
import { mkdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

export const libraryDir = join(tmpdir(), 'spacearr-e2e-library');
export const files = [
  { title: 'Alpha', year: 2020, id: 1, rel: 'Alpha (2020)/Alpha.mp4', kbps: 1200 },
  { title: 'Bravo', year: 2021, id: 2, rel: 'Bravo (2021)/Bravo.mp4', kbps: 4000 },
];

export function ensureFixture(): boolean {
  try {
    execFileSync('ffmpeg', ['-version'], { stdio: 'ignore' });
  } catch (err) {
    // In CI (Plan 4), a missing ffmpeg must fail the run, not report green with zero golden-path
    // coverage via a silent test.skip. Left as a soft skip everywhere else (e.g. local dev
    // without ffmpeg installed) so default behaviour is unchanged.
    if (process.env.SPACEARR_E2E_REQUIRE_FFMPEG === '1') {
      throw new Error(`SPACEARR_E2E_REQUIRE_FFMPEG=1 but ffmpeg is not on PATH: ${err instanceof Error ? err.message : String(err)}`);
    }
    return false;
  }
  for (const f of files) {
    const path = join(libraryDir, f.rel);
    if (existsSync(path)) continue;
    mkdirSync(join(libraryDir, f.rel.split('/')[0]), { recursive: true });
    // nal-hrd=cbr forces libx264 to pad up to the target bitrate; without it a low-complexity
    // synthetic pattern like testsrc undershoots -b:v by 4-5x and the file never clears
    // FileDiscovery.MinSizeBytes (1 MB).
    execFileSync('ffmpeg', ['-y', '-f', 'lavfi', '-i', 'testsrc=size=320x240:rate=25', '-t', '8', '-c:v', 'libx264', '-b:v', `${f.kbps}k`, '-minrate', `${f.kbps}k`, '-maxrate', `${f.kbps}k`, '-bufsize', `${f.kbps * 2}k`, '-x264-params', 'nal-hrd=cbr:force-cfr=1', path], { stdio: 'ignore' });
  }
  return true;
}
