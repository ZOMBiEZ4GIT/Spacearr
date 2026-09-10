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
import { chromium, type Browser } from '@playwright/test';
import { spawn, execFileSync, ChildProcess } from 'node:child_process';
import { existsSync, mkdirSync, statSync, rmSync, renameSync } from 'node:fs';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { startRadarrFake, startSonarrFake, type FakeMovie, type FakeSeries, type FakeProfile } from './fake-arr';

const __dirname = dirname(fileURLToPath(import.meta.url));
const webDir = join(__dirname, '..');
const repoRoot = join(webDir, '..');
const outDir = join(repoRoot, 'docs', 'screenshots');

const BACKEND_PORT = 8798;
const RADARR_1080P_PORT = 7996;
const RADARR_4K_PORT = 7995;
const SONARR_PORT = 7994;

// Kept in the system temp dir, not the repo, and deliberately never cleaned up - re-runs
// skip re-encoding video and re-rendering posters that already exist (see ensureFixtureFiles
// and ensurePosters below), which is the difference between a ~1s and a multi-minute re-run.
const fixtureDir = join(tmpdir(), 'spacearr-screenshot-fixture');
const posterDir = join(fixtureDir, 'posters');

// Three quality profiles, matched consistently against the actual resolution of the file
// each item carries below - a 1080p file is never assigned the Ultra-HD profile or vice
// versa. "Any" covers a series whose seasons genuinely span two resolutions.
const profiles: FakeProfile[] = [{ id: 4, name: 'HD-1080p' }, { id: 5, name: 'Ultra-HD' }, { id: 6, name: 'Any' }];
const HD_1080P = 4, ULTRA_HD = 5, ANY = 6;

interface FileSpec { rel: string; kbps: number; sizeMb: number; codec: 'h264' | 'hevc' }

function target(rel: string, kbps: number, sizeMb: number, codec: 'h264' | 'hevc'): FileSpec {
  return { rel, kbps, sizeMb, codec };
}

// Movies. "Harbour Lights (2022)" is deliberately listed under both Radarr instances with
// the same tmdbId, so it shows up as a duplicate - the normal "1080p + 4K" two-instance
// setup. Sizes span roughly 1 MB to 18 MB so the treemap has clearly large and small blocks.
const movies1080p: FileSpec[] = [
  target('movies/The Long Meridian (2019)/The Long Meridian.mp4', 2500, 6.0, 'h264'),
  target('movies/Harbour Lights (2022)/Harbour Lights.1080p.mp4', 4000, 9.5, 'h264'),
  target('movies/Second Winter (2016)/Second Winter.mp4', 1000, 1.1, 'h264'),
  target('movies/Quiet Machines (2021)/Quiet Machines.mp4', 6000, 17.5, 'h264'),
];
const movies4k: FileSpec[] = [
  target('movies-4k/Harbour Lights (2022)/Harbour Lights.2160p.mp4', 8000, 14.0, 'hevc'),
];

// Two series, two seasons of three episodes each, spanning the 800-6200 kbps range with a
// mix of codecs so the heat colour spans green through red. Northgate's two seasons are
// genuinely different resolutions (hence its "Any" profile below); The Ferrier Line stays
// 1080p throughout (hence "HD-1080p"), so profile and quality never disagree.
const northgateFiles: FileSpec[] = [
  target('tv/Northgate (2018)/Season 01/Northgate - S01E01.mp4', 900, 1.2, 'h264'),
  target('tv/Northgate (2018)/Season 01/Northgate - S01E02.mp4', 1800, 2.4, 'h264'),
  target('tv/Northgate (2018)/Season 01/Northgate - S01E03.mp4', 2600, 3.3, 'h264'),
  target('tv/Northgate (2018)/Season 02/Northgate - S02E01.mp4', 4000, 5.0, 'hevc'),
  target('tv/Northgate (2018)/Season 02/Northgate - S02E02.mp4', 5200, 6.4, 'hevc'),
  target('tv/Northgate (2018)/Season 02/Northgate - S02E03.mp4', 6200, 7.6, 'hevc'),
];
const northgateEpisodeTitles = ['Pilot', 'Salt and Iron', 'What the River Knew', 'Low Tide', 'The Long Silence', 'Harbour Bound'];
const ferrierFiles: FileSpec[] = [
  // 1.25 MB, not 1.0: FileDiscovery.MinSizeBytes (src/Spacearr/Scanning/FileDiscovery.cs)
  // skips any file under 1,000,000 bytes, and a 1.0 MB CBR target lands just under that.
  target('tv/The Ferrier Line (2020)/Season 01/The Ferrier Line - S01E01.mp4', 800, 1.25, 'h264'),
  target('tv/The Ferrier Line (2020)/Season 01/The Ferrier Line - S01E02.mp4', 1900, 2.5, 'h264'),
  target('tv/The Ferrier Line (2020)/Season 01/The Ferrier Line - S01E03.mp4', 2800, 3.6, 'h264'),
  target('tv/The Ferrier Line (2020)/Season 02/The Ferrier Line - S02E01.mp4', 3200, 4.0, 'hevc'),
  target('tv/The Ferrier Line (2020)/Season 02/The Ferrier Line - S02E02.mp4', 4200, 5.2, 'hevc'),
  target('tv/The Ferrier Line (2020)/Season 02/The Ferrier Line - S02E03.mp4', 5200, 6.4, 'hevc'),
];
const ferrierEpisodeTitles = ['Crossing', 'Static', 'The Weight of Water', 'Undertow', 'Cold Relay', 'Terminus'];

const allFiles = [...movies1080p, ...movies4k, ...northgateFiles, ...ferrierFiles];

// One poster per distinct title (the two Harbour Lights copies - 1080p and 4K - share one,
// since it's the same movie). hue is the base HSL hue for that item's gradient.
const posterSpecs: { slug: string; title: string; hue: number }[] = [
  { slug: 'long-meridian', title: 'The Long Meridian (2019)', hue: 205 },
  { slug: 'harbour-lights', title: 'Harbour Lights (2022)', hue: 18 },
  { slug: 'second-winter', title: 'Second Winter (2016)', hue: 192 },
  { slug: 'quiet-machines', title: 'Quiet Machines (2021)', hue: 265 },
  { slug: 'northgate', title: 'Northgate', hue: 95 },
  { slug: 'ferrier-line', title: 'The Ferrier Line', hue: 332 },
];
function posterFile(slug: string): string { return join(posterDir, `${slug}.jpg`); }

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
  const [meridian, harbour, winter, machines] = movies1080p;
  return [
    { id: 1, title: 'The Long Meridian', year: 2019, tmdbId: 6001, qualityProfileId: HD_1080P, qualityName: 'WEBDL-1080p', path: fp(meridian.rel), sizeBytes: sizeOf(meridian.rel), posterPath: posterFile('long-meridian') },
    { id: 2, title: 'Harbour Lights', year: 2022, tmdbId: 6002, qualityProfileId: HD_1080P, qualityName: 'Bluray-1080p', path: fp(harbour.rel), sizeBytes: sizeOf(harbour.rel), posterPath: posterFile('harbour-lights') },
    { id: 3, title: 'Second Winter', year: 2016, tmdbId: 6003, qualityProfileId: ANY, qualityName: 'WEBDL-1080p', path: fp(winter.rel), sizeBytes: sizeOf(winter.rel), posterPath: posterFile('second-winter') },
    { id: 4, title: 'Quiet Machines', year: 2021, tmdbId: 6004, qualityProfileId: HD_1080P, qualityName: 'Bluray-1080p', path: fp(machines.rel), sizeBytes: sizeOf(machines.rel), posterPath: posterFile('quiet-machines') },
  ];
}
function buildMovies4k(): FakeMovie[] {
  const [harbour4k] = movies4k;
  return [
    { id: 1, title: 'Harbour Lights', year: 2022, tmdbId: 6002, qualityProfileId: ULTRA_HD, qualityName: 'Bluray-2160p', path: fp(harbour4k.rel), sizeBytes: sizeOf(harbour4k.rel), posterPath: posterFile('harbour-lights') },
  ];
}

function buildSeries(
  id: number, title: string, year: number, tvdbId: number, qualityProfileId: number,
  files: FileSpec[], episodeTitles: string[], qualityNames: string[], posterSlug: string,
): FakeSeries {
  // files: 3 per season, seasons in order (1, then 2)
  const seasonOf = (i: number) => (i < 3 ? 1 : 2);
  const epNumOf = (i: number) => (i % 3) + 1;
  const seriesFiles = files.map((f, i) => ({
    id: id * 100 + i + 1, seasonNumber: seasonOf(i), path: fp(f.rel), sizeBytes: sizeOf(f.rel),
    qualityName: qualityNames[i],
  }));
  const episodes = files.map((_f, i) => ({
    id: id * 1000 + i + 1, episodeNumber: epNumOf(i), seasonNumber: seasonOf(i),
    title: episodeTitles[i], episodeFileId: seriesFiles[i].id, monitored: true,
  }));
  return { id, title, year, tvdbId, qualityProfileId, monitored: true, files: seriesFiles, episodes, posterPath: posterFile(posterSlug) };
}

/** Escapes text dropped into the poster HTML template below. */
function escapeHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** Renders one 300x450 poster per title (a two-tone gradient with the title over it) with
 * Playwright's Chromium, and caches it under fixtureDir/posters so re-runs skip the render. */
async function ensurePosters(browser: Browser): Promise<void> {
  mkdirSync(posterDir, { recursive: true });
  for (const { slug, title, hue } of posterSpecs) {
    const path = posterFile(slug);
    if (existsSync(path) && statSync(path).size > 1000) continue;
    const page = await browser.newPage({ viewport: { width: 300, height: 450 } });
    const hue2 = (hue + 45) % 360;
    // Title sits at the TOP of the poster, not the bottom: the treemap's own block
    // label (renderer.ts) is drawn bottom-left over every poster, so baking the
    // fixture's title into that same corner would double up with it visually.
    await page.setContent(`<!doctype html><html><body style="margin:0;width:300px;height:450px;
      background:linear-gradient(160deg, hsl(${hue} 62% 30%), hsl(${hue2} 68% 16%));
      display:flex;align-items:flex-start;box-sizing:border-box;padding:22px;
      font-family:-apple-system,Segoe UI,sans-serif;">
      <div style="color:#fff;font-size:27px;font-weight:700;line-height:1.28;
        text-shadow:0 2px 8px rgba(0,0,0,.65)">${escapeHtml(title)}</div>
      </body></html>`);
    await page.screenshot({ path, type: 'jpeg', quality: 82 });
    await page.close();
  }
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
  // ffmpeg with the same path as both -i and output is unsafe in general (it opens the input
  // for reading and the output for writing at once); write to a temp name in the same
  // directory, then rename over the original so a partial/failed re-encode never corrupts it.
  const tmp = `${path}.${process.pid}.tmp.png`;
  execFileSync('ffmpeg', ['-y', '-i', path, '-vf', 'scale=1440:-1', tmp], { stdio: 'ignore' });
  renameSync(tmp, path);
}

async function main() {
  mkdirSync(outDir, { recursive: true });
  console.log('Generating fixture media…');
  ensureFixtureFiles();

  console.log('Launching browser (also used to render fixture posters)…');
  const posterBrowser = await chromium.launch();
  console.log('Rendering fixture posters…');
  await ensurePosters(posterBrowser);

  console.log('Starting fake arr instances…');
  const radarr1080p = startRadarrFake({ port: RADARR_1080P_PORT, apiKey: 's-1080p-key', movies: buildMovies1080p(), rootFolder: fixtureDir.replace(/\\/g, '/'), profiles });
  const radarr4k = startRadarrFake({ port: RADARR_4K_PORT, apiKey: 's-4k-key', movies: buildMovies4k(), rootFolder: fixtureDir.replace(/\\/g, '/'), profiles });
  const sonarrTv = startSonarrFake({
    port: SONARR_PORT, apiKey: 's-tv-key', rootFolder: fixtureDir.replace(/\\/g, '/'), profiles,
    series: [
      buildSeries(1, 'Northgate', 2018, 7001, ANY, northgateFiles, northgateEpisodeTitles,
        ['HDTV-1080p', 'HDTV-1080p', 'HDTV-1080p', 'WEBDL-2160p', 'WEBDL-2160p', 'WEBDL-2160p'], 'northgate'),
      buildSeries(2, 'The Ferrier Line', 2020, 7002, HD_1080P, ferrierFiles, ferrierEpisodeTitles,
        ['HDTV-1080p', 'HDTV-1080p', 'HDTV-1080p', 'WEBDL-1080p', 'WEBDL-1080p', 'WEBDL-1080p'], 'ferrier-line'),
    ],
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
    void posterBrowser.close();
  };
  process.on('exit', cleanup);

  try {
    await waitForHttp(`http://localhost:${BACKEND_PORT}/api/v1/system/status`, 60_000);
    console.log('Backend is up. Reusing the poster-rendering browser…');

    const browser = posterBrowser;
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

    await page.getByRole('row', { name: /The Long Meridian/ }).click();
    await page.getByRole('region', { name: 'Details' }).getByRole('heading', { name: 'The Long Meridian (2019)' }).waitFor();
    await page.screenshot({ path: join(outDir, 'detail.png') });

    // The Long Meridian's own profile is HD-1080p; the "other profiles" list therefore
    // offers Ultra-HD (and Any) - clicking Ultra-HD previews upgrading this 1080p file.
    await page.getByRole('button', { name: /Ultra-HD/ }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('listitem').first().waitFor();
    await page.screenshot({ path: join(outDir, 'action-preview.png') });
    await dialog.getByRole('button', { name: 'Cancel' }).click();

    await page.getByRole('link', { name: 'Duplicates' }).click();
    await page.getByText('Harbour Lights (2022)').first().waitFor();
    await page.waitForTimeout(200);
    await page.screenshot({ path: join(outDir, 'duplicates.png') });

    await page.getByRole('link', { name: 'Library' }).click();
    await page.getByRole('img', { name: /Treemap of Library/ }).waitFor();
    await page.locator('#theme-select').selectOption('light');
    await page.waitForTimeout(300);
    await page.screenshot({ path: join(outDir, 'library-light.png') });

    await page.close();

    for (const name of ['library-dark.png', 'detail.png', 'action-preview.png', 'duplicates.png', 'library-light.png']) {
      await shrinkIfNeeded(join(outDir, name));
    }
    console.log('Screenshots written to', outDir);
  } finally {
    cleanup();
    // The backend's temp config dir (db, keys, logs) is unique per run and never reused,
    // unlike fixtureDir's media which is deliberately kept so re-runs skip re-encoding it.
    try { rmSync(configDir, { recursive: true, force: true }); } catch { /* best effort */ }
  }
}

main().catch((err) => { console.error(err); process.exitCode = 1; });
