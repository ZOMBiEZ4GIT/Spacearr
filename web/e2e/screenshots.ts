/**
 * Generates the README/docs screenshots under docs/screenshots/.
 *
 * Builds a small library (movies across two Radarr instances - "1080p" and "4K" - so one
 * title duplicates across them, plus a Sonarr instance with two series) with ffmpeg, starts
 * three fake arr APIs (web/e2e/fake-arr.ts) and the real Spacearr backend against a temp
 * config dir, then drives the real UI with Playwright to capture:
 *   library-dark.png, detail.png, action-preview.png, duplicates.png, library-light.png
 *
 * Prerequisite: the web app and the backend must already be built, same as `npm run e2e`:
 *   cd web && npm run build && cd .. && dotnet build src/Spacearr -c Release -p:SkipWeb=true
 * Then: cd web && npm run screenshots
 */
import { chromium } from '@playwright/test';
import { spawn, execFileSync, ChildProcess } from 'node:child_process';
import { existsSync, mkdirSync, statSync } from 'node:fs';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { startRadarrFake, startSonarrFake, type FakeMovie, type FakeSeries } from './fake-arr';

const __dirname = dirname(fileURLToPath(import.meta.url));
const webDir = join(__dirname, '..');
const repoRoot = join(webDir, '..');
const outDir = join(repoRoot, 'docs', 'screenshots');

const BACKEND_PORT = 8798;
const RADARR_1080P_PORT = 7996;
const RADARR_4K_PORT = 7995;
const SONARR_PORT = 7994;

const fixtureDir = join(tmpdir(), 'spacearr-screenshot-fixture');

interface FileSpec { rel: string; kbps: number; sizeMb: number; codec: 'h264' | 'hevc' }

function target(rel: string, kbps: number, sizeMb: number, codec: 'h264' | 'hevc'): FileSpec {
  return { rel, kbps, sizeMb, codec };
}

// Movies. "Gamma (2022)" is deliberately listed under both Radarr instances with the same
// tmdbId, so it shows up as a duplicate - the normal "1080p + 4K" two-instance setup.
const movies1080p: FileSpec[] = [
  target('movies/Alpha (2020)/Alpha.mp4', 1200, 1.8, 'h264'),
  target('movies/Gamma (2022)/Gamma.1080p.mp4', 3500, 4.2, 'h264'),
];
const movies4k: FileSpec[] = [
  target('movies-4k/Gamma (2022)/Gamma.2160p.mp4', 6000, 5.8, 'hevc'),
];

// Two series, two seasons of three episodes each, spanning the 800-6000 kbps range with a
// mix of codecs so the heat colour spans green through red.
const deltaFiles: FileSpec[] = [
  target('tv/Delta (2019)/Season 01/Delta - S01E01.mp4', 800, 1.0, 'h264'),
  target('tv/Delta (2019)/Season 01/Delta - S01E02.mp4', 1500, 2.0, 'h264'),
  target('tv/Delta (2019)/Season 01/Delta - S01E03.mp4', 2200, 2.6, 'h264'),
  target('tv/Delta (2019)/Season 02/Delta - S02E01.mp4', 3000, 3.4, 'hevc'),
  target('tv/Delta (2019)/Season 02/Delta - S02E02.mp4', 4000, 4.4, 'hevc'),
  target('tv/Delta (2019)/Season 02/Delta - S02E03.mp4', 5000, 5.2, 'hevc'),
];
const epsilonFiles: FileSpec[] = [
  target('tv/Epsilon (2021)/Season 01/Epsilon - S01E01.mp4', 900, 1.2, 'h264'),
  target('tv/Epsilon (2021)/Season 01/Epsilon - S01E02.mp4', 1800, 2.3, 'h264'),
  target('tv/Epsilon (2021)/Season 01/Epsilon - S01E03.mp4', 2600, 3.0, 'h264'),
  target('tv/Epsilon (2021)/Season 02/Epsilon - S02E01.mp4', 3800, 4.0, 'hevc'),
  target('tv/Epsilon (2021)/Season 02/Epsilon - S02E02.mp4', 4800, 5.0, 'hevc'),
  target('tv/Epsilon (2021)/Season 02/Epsilon - S02E03.mp4', 6000, 6.0, 'hevc'),
];

const allFiles = [...movies1080p, ...movies4k, ...deltaFiles, ...epsilonFiles];

function ensureFixtureFiles(): void {
  execFileSync('ffmpeg', ['-version'], { stdio: 'ignore' });
  for (const f of allFiles) {
    const path = join(fixtureDir, f.rel);
    if (existsSync(path) && statSync(path).size > 500_000) continue;
    mkdirSync(dirname(path), { recursive: true });
    const durationSec = Math.max(4, Math.round((f.sizeMb * 1_000_000 * 8) / (f.kbps * 1000)));
    const common = ['-y', '-f', 'lavfi', '-i', `testsrc=size=1280x720:rate=24`, '-t', String(durationSec)];
    if (f.codec === 'h264') {
      execFileSync('ffmpeg', [...common, '-c:v', 'libx264', '-b:v', `${f.kbps}k`, '-minrate', `${f.kbps}k`,
        '-maxrate', `${f.kbps}k`, '-bufsize', `${f.kbps * 2}k`, '-x264-params', 'nal-hrd=cbr:force-cfr=1', path], { stdio: 'ignore' });
    } else {
      execFileSync('ffmpeg', [...common, '-c:v', 'libx265', '-b:v', `${f.kbps}k`, '-minrate', `${f.kbps}k`,
        '-maxrate', `${f.kbps}k`, '-bufsize', `${f.kbps * 2}k`, '-x265-params', 'strict-cbr=1', path], { stdio: 'ignore' });
    }
  }
}

function fp(rel: string): string { return join(fixtureDir, rel).replace(/\\/g, '/'); }
function sizeOf(rel: string): number { return statSync(join(fixtureDir, rel)).size; }

function buildMovies1080p(): FakeMovie[] {
  const [alpha, gamma] = movies1080p;
  return [
    { id: 1, title: 'Alpha', year: 2020, tmdbId: 6001, qualityProfileId: 5, qualityName: 'WEBDL-1080p', path: fp(alpha.rel), sizeBytes: sizeOf(alpha.rel) },
    { id: 2, title: 'Gamma', year: 2022, tmdbId: 6002, qualityProfileId: 5, qualityName: 'Bluray-1080p', path: fp(gamma.rel), sizeBytes: sizeOf(gamma.rel) },
  ];
}
function buildMovies4k(): FakeMovie[] {
  const [gamma] = movies4k;
  return [
    { id: 1, title: 'Gamma', year: 2022, tmdbId: 6002, qualityProfileId: 5, qualityName: 'Bluray-2160p', path: fp(gamma.rel), sizeBytes: sizeOf(gamma.rel) },
  ];
}

function buildSeries(id: number, title: string, year: number, tvdbId: number, files: FileSpec[]): FakeSeries {
  // files: 3 per season, seasons in order (1, then 2)
  const seasonOf = (i: number) => (i < 3 ? 1 : 2);
  const epNumOf = (i: number) => (i % 3) + 1;
  const seriesFiles = files.map((f, i) => ({
    id: id * 100 + i + 1, seasonNumber: seasonOf(i), path: fp(f.rel), sizeBytes: sizeOf(f.rel),
    qualityName: f.codec === 'hevc' ? 'WEBDL-2160p' : 'HDTV-1080p',
  }));
  const episodes = files.map((_f, i) => ({
    id: id * 1000 + i + 1, episodeNumber: epNumOf(i), seasonNumber: seasonOf(i),
    title: `Episode ${epNumOf(i)}`, episodeFileId: seriesFiles[i].id, monitored: true,
  }));
  return { id, title, year, tvdbId, qualityProfileId: 5, monitored: true, files: seriesFiles, episodes };
}

async function waitForHttp(url: string, timeoutMs: number): Promise<void> {
  const start = Date.now();
  for (;;) {
    try { const res = await fetch(url); if (res.ok || res.status === 401) return; } catch { /* not up yet */ }
    if (Date.now() - start > timeoutMs) throw new Error(`Timed out waiting for ${url}`);
    await new Promise((r) => setTimeout(r, 500));
  }
}

async function shrinkIfNeeded(path: string): Promise<void> {
  const { size } = statSync(path);
  if (size <= 400_000) return;
  // Downscale to 1440 wide and re-encode as PNG to bring a screenshot under the size budget.
  execFileSync('ffmpeg', ['-y', '-i', path, '-vf', 'scale=1440:-1', path], { stdio: 'ignore' });
}

async function main() {
  mkdirSync(outDir, { recursive: true });
  console.log('Generating fixture media…');
  ensureFixtureFiles();

  console.log('Starting fake arr instances…');
  const radarr1080p = startRadarrFake({ port: RADARR_1080P_PORT, apiKey: 's-1080p-key', movies: buildMovies1080p(), rootFolder: fixtureDir.replace(/\\/g, '/') });
  const radarr4k = startRadarrFake({ port: RADARR_4K_PORT, apiKey: 's-4k-key', movies: buildMovies4k(), rootFolder: fixtureDir.replace(/\\/g, '/') });
  const sonarrTv = startSonarrFake({
    port: SONARR_PORT, apiKey: 's-tv-key', rootFolder: fixtureDir.replace(/\\/g, '/'),
    series: [buildSeries(1, 'Delta', 2019, 7001, deltaFiles), buildSeries(2, 'Epsilon', 2021, 7002, epsilonFiles)],
  });

  const configDir = mkdtempSync(join(tmpdir(), 'spacearr-screenshots-'));
  console.log('Starting backend on port', BACKEND_PORT, 'config dir', configDir);
  const backend: ChildProcess = spawn('dotnet', ['run', '--project', '../src/Spacearr', '--no-build', '-c', 'Release'], {
    cwd: webDir,
    env: { ...process.env, SPACEARR_CONFIG_DIR: configDir, SPACEARR_PORT: String(BACKEND_PORT), ASPNETCORE_ENVIRONMENT: 'Production' },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  backend.stdout?.on('data', (d) => process.env.SPACEARR_SCREENSHOT_VERBOSE && process.stdout.write(`[backend] ${d}`));
  backend.stderr?.on('data', (d) => process.stderr.write(`[backend] ${d}`));

  const cleanup = () => {
    // `dotnet run` on Windows spawns the real Spacearr.dll host as a child of the process
    // this script starts; a plain backend.kill() only signals that wrapper and leaves the
    // host running. taskkill /t kills the whole tree; falls back to a plain kill elsewhere.
    if (backend.pid) {
      if (process.platform === 'win32') {
        try { execFileSync('taskkill', ['/pid', String(backend.pid), '/t', '/f'], { stdio: 'ignore' }); } catch { /* already gone */ }
      } else {
        backend.kill();
      }
    }
    radarr1080p.close(); radarr4k.close(); sonarrTv.close();
  };
  process.on('exit', cleanup);

  try {
    await waitForHttp(`http://localhost:${BACKEND_PORT}/api/v1/system/status`, 60_000);
    console.log('Backend is up. Launching browser…');

    const browser = await chromium.launch();
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    // spacearr.theme is per-viewer localStorage read by ThemeToggle; force dark up front so the
    // capture doesn't depend on the OS/browser's default colour scheme.
    await page.addInitScript(() => { try { localStorage.setItem('spacearr.theme', 'dark'); } catch { /* ignore */ } });

    await page.goto(`http://localhost:${BACKEND_PORT}/`);
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill('correct horse battery staple');
    await page.getByRole('button', { name: 'Create account' }).click();
    await page.waitForURL(/\/setup\/wizard$/);

    // Step 0: connect the first Radarr instance.
    await page.getByLabel('Name').fill('1080p');
    await page.getByLabel('URL').fill(`http://localhost:${RADARR_1080P_PORT}`);
    await page.getByLabel('API key').fill('s-1080p-key');
    await page.getByRole('button', { name: 'Test' }).click();
    await page.getByText(/Connected to Radarr/).waitFor();
    await page.getByRole('button', { name: 'Save' }).click();

    // Step 1: paths already match (fake arr reports the same absolute paths Spacearr scans).
    await page.getByRole('button', { name: 'Skip, paths match' }).click();

    // Step 2: add the one root folder that covers movies/ and tv/.
    await page.getByLabel('Library folder as Spacearr sees it').fill(fixtureDir);
    await page.getByRole('button', { name: 'Check' }).click();
    await page.getByText(/Found \d+/).waitFor();
    await page.getByRole('button', { name: 'Add folder' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Add the remaining two instances before the first scan, so one scan enriches everything.
    // Each InstanceCard already on the page carries its own MappingEditor <form>, so once an
    // instance exists `page.locator('form')` is no longer unique - the freshly-opened
    // ConnectionForm is always the first <form> in DOM order (it's rendered above the instance
    // list), so `.first()` pins it down for Name/URL/API key/Test/Save.
    await page.goto(`http://localhost:${BACKEND_PORT}/settings/connections`);
    await page.getByRole('button', { name: 'Add connection' }).click();
    const form4k = page.locator('form').first();
    await form4k.getByLabel('App').selectOption('radarr');
    await form4k.getByLabel('Name').fill('4K');
    await form4k.getByLabel('URL').fill(`http://localhost:${RADARR_4K_PORT}`);
    await form4k.getByLabel('API key').fill('s-4k-key');
    await form4k.getByRole('button', { name: 'Test' }).click();
    await form4k.getByText(/Connected to Radarr/).waitFor();
    await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/v1/instances') && r.request().method() === 'POST'),
      form4k.getByRole('button', { name: 'Save' }).click(),
    ]);

    await page.getByRole('button', { name: 'Add connection' }).click();
    const formTv = page.locator('form').first();
    await formTv.getByLabel('App').selectOption('sonarr');
    await formTv.getByLabel('Name').fill('TV');
    await formTv.getByLabel('URL').fill(`http://localhost:${SONARR_PORT}`);
    await formTv.getByLabel('API key').fill('s-tv-key');
    await formTv.getByRole('button', { name: 'Test' }).click();
    await formTv.getByText(/Connected to Sonarr/).waitFor();
    await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/v1/instances') && r.request().method() === 'POST'),
      formTv.getByRole('button', { name: 'Save' }).click(),
    ]);

    // Run the scan and wait for it to finish.
    await page.getByRole('button', { name: 'Scan now' }).click();
    await page.getByText(/Last scan/).waitFor({ timeout: 120_000 });

    await page.goto(`http://localhost:${BACKEND_PORT}/library`);
    await page.getByRole('img', { name: /Treemap of Library/ }).waitFor();
    await page.waitForTimeout(400); // let poster images/heat colours settle
    await page.screenshot({ path: join(outDir, 'library-dark.png') });

    await page.getByRole('row', { name: /Alpha/ }).click();
    await page.getByRole('region', { name: 'Details' }).getByRole('heading', { name: 'Alpha (2020)' }).waitFor();
    await page.screenshot({ path: join(outDir, 'detail.png') });

    await page.getByRole('button', { name: /HD-1080p/ }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('listitem').first().waitFor();
    await page.screenshot({ path: join(outDir, 'action-preview.png') });
    await dialog.getByRole('button', { name: 'Cancel' }).click();

    await page.getByRole('link', { name: 'Duplicates' }).click();
    await page.getByText('Gamma (2022)').first().waitFor();
    await page.waitForTimeout(200);
    await page.screenshot({ path: join(outDir, 'duplicates.png') });

    await page.getByRole('link', { name: 'Library' }).click();
    await page.getByRole('img', { name: /Treemap of Library/ }).waitFor();
    await page.locator('#theme-select').selectOption('light');
    await page.waitForTimeout(300);
    await page.screenshot({ path: join(outDir, 'library-light.png') });

    await browser.close();

    for (const name of ['library-dark.png', 'detail.png', 'action-preview.png', 'duplicates.png', 'library-light.png']) {
      await shrinkIfNeeded(join(outDir, name));
    }
    console.log('Screenshots written to', outDir);
  } finally {
    cleanup();
  }
}

main().catch((err) => { console.error(err); process.exitCode = 1; });
