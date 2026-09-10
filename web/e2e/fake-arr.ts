import { createServer, Server } from 'node:http';
import { join } from 'node:path';
import { existsSync, readFileSync } from 'node:fs';
import { files, libraryDir } from './fixture';

// A tiny 1x1 PNG served for every /MediaCover/* poster request that has no per-item
// poster file (the smoke test fixture below never sets `posterPath`); the screenshot
// fixture (web/e2e/screenshots.ts) sets `posterPath` on every item to a distinct,
// generated 300x450 JPEG instead so the treemap and detail panel render real-looking art.
const png1x1 = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64');
const fallbackPoster = { buf: png1x1, contentType: 'image/png' };

function loadPoster(posterPath?: string): { buf: Buffer; contentType: string } {
  if (!posterPath || !existsSync(posterPath)) return fallbackPoster;
  return { buf: readFileSync(posterPath), contentType: 'image/jpeg' };
}

function json(res: import('node:http').ServerResponse, body: unknown, status = 200) {
  res.writeHead(status, { 'content-type': 'application/json' });
  res.end(JSON.stringify(body));
}

export interface FakeProfile { id: number; name: string }
export const defaultProfiles: FakeProfile[] = [{ id: 4, name: 'HD-1080p' }, { id: 5, name: 'Ultra-HD' }];

export interface FakeMovie {
  id: number; title: string; year: number; tmdbId: number; qualityProfileId: number; qualityName: string;
  path: string; sizeBytes: number;
  /** Absolute path to a pre-rendered poster image; falls back to a 1x1 PNG when absent. */
  posterPath?: string;
}

export interface RadarrFixtureOptions {
  port: number; apiKey: string; movies: FakeMovie[]; rootFolder: string; profiles?: FakeProfile[];
}

/** Starts a Radarr-shaped fake API. Used directly (movies-only, port 7999) by the e2e smoke test,
 * and with a second, differently-configured instance by the screenshot generator. */
export function startRadarrFake(opts: RadarrFixtureOptions): Server {
  const profiles = opts.profiles ?? defaultProfiles;
  const posters = new Map(opts.movies.map((f) => [f.id, loadPoster(f.posterPath)]));
  const movies = opts.movies.map((f) => ({
    id: f.id, title: f.title, year: f.year, monitored: true, qualityProfileId: f.qualityProfileId, tmdbId: f.tmdbId, tags: [],
    images: [{ coverType: 'poster', url: `/MediaCover/${f.id}/poster.jpg` }],
    hasFile: true, movieFile: { id: 70 + f.id, movieId: f.id, path: f.path, size: f.sizeBytes, quality: { quality: { id: 1, name: f.qualityName } } },
  }));
  const server = createServer((req, res) => {
    const url = new URL(req.url!, 'http://x');
    if (req.headers['x-api-key'] !== opts.apiKey) { res.writeHead(401); return res.end(); }
    const p = url.pathname;
    if (p === '/api/v3/system/status') return json(res, { appName: 'Radarr', version: '6.3.0' });
    if (p === '/api/v3/qualityprofile') return json(res, profiles);
    if (p === '/api/v3/tag') return json(res, []);
    if (p === '/api/v3/rootfolder') return json(res, [{ path: opts.rootFolder }]);
    if (p === '/api/v3/movie' && req.method === 'GET') return json(res, movies);
    const m = p.match(/^\/api\/v3\/movie\/(\d+)$/);
    if (m && req.method === 'GET') return json(res, movies.find((x) => x.id === Number(m[1])) ?? {});
    if (m && req.method === 'PUT') return json(res, {});
    if (p.startsWith('/api/v3/moviefile/') && req.method === 'DELETE') return json(res, {});
    if (p === '/api/v3/command') return json(res, { id: 1 });
    const cover = p.match(/^\/MediaCover\/(\d+)\/poster\.jpg$/);
    if (cover) {
      const poster = posters.get(Number(cover[1])) ?? fallbackPoster;
      res.writeHead(200, { 'content-type': poster.contentType });
      return res.end(poster.buf);
    }
    if (p.startsWith('/MediaCover/')) { res.writeHead(200, { 'content-type': 'image/png' }); return res.end(png1x1); }
    res.writeHead(404); res.end();
  });
  server.listen(opts.port);
  return server;
}

export interface FakeEpisodeFile { id: number; seasonNumber: number; path: string; sizeBytes: number; qualityName: string }
export interface FakeEpisode { id: number; episodeNumber: number; seasonNumber: number; title: string; episodeFileId: number; monitored: boolean }
export interface FakeSeries {
  id: number; title: string; year: number; tvdbId: number; qualityProfileId: number; monitored: boolean;
  files: FakeEpisodeFile[]; episodes: FakeEpisode[];
  /** Absolute path to a pre-rendered poster image; falls back to a 1x1 PNG when absent. */
  posterPath?: string;
}

export interface SonarrFixtureOptions {
  port: number; apiKey: string; series: FakeSeries[]; rootFolder: string; profiles?: FakeProfile[];
}

/** Starts a Sonarr-shaped fake API covering exactly the endpoints SonarrClient reads:
 * system/status, series, episode?seriesId=, episodefile?seriesId=, qualityprofile, rootfolder. */
export function startSonarrFake(opts: SonarrFixtureOptions): Server {
  const profiles = opts.profiles ?? defaultProfiles;
  const posters = new Map(opts.series.map((s) => [s.id, loadPoster(s.posterPath)]));
  const series = opts.series.map((s) => ({
    id: s.id, title: s.title, year: s.year, tvdbId: s.tvdbId, imdbId: null, monitored: s.monitored, qualityProfileId: s.qualityProfileId, tags: [],
    images: [{ coverType: 'poster', url: `/MediaCover/${s.id}/poster.jpg` }],
  }));
  const server = createServer((req, res) => {
    const url = new URL(req.url!, 'http://x');
    if (req.headers['x-api-key'] !== opts.apiKey) { res.writeHead(401); return res.end(); }
    const p = url.pathname;
    if (p === '/api/v3/system/status') return json(res, { appName: 'Sonarr', version: '4.0.9' });
    if (p === '/api/v3/qualityprofile') return json(res, profiles);
    if (p === '/api/v3/tag') return json(res, []);
    if (p === '/api/v3/rootfolder') return json(res, [{ path: opts.rootFolder }]);
    if (p === '/api/v3/series' && req.method === 'GET' && !url.searchParams.has('seriesId')) return json(res, series);
    const sm = p.match(/^\/api\/v3\/series\/(\d+)$/);
    if (sm && req.method === 'GET') return json(res, series.find((x) => x.id === Number(sm[1])) ?? {});
    if (sm && req.method === 'PUT') return json(res, {});
    if (p === '/api/v3/episode') {
      const seriesId = Number(url.searchParams.get('seriesId'));
      const found = opts.series.find((s) => s.id === seriesId);
      return json(res, (found?.episodes ?? []).map((e) => ({
        id: e.id, seriesId, episodeNumber: e.episodeNumber, seasonNumber: e.seasonNumber, title: e.title,
        episodeFileId: e.episodeFileId, monitored: e.monitored, hasFile: e.episodeFileId > 0,
      })));
    }
    if (p === '/api/v3/episodefile') {
      const seriesId = Number(url.searchParams.get('seriesId'));
      const found = opts.series.find((s) => s.id === seriesId);
      return json(res, (found?.files ?? []).map((f) => ({
        id: f.id, seriesId, seasonNumber: f.seasonNumber, path: f.path, size: f.sizeBytes,
        quality: { quality: { id: 1, name: f.qualityName } },
      })));
    }
    if (p.startsWith('/api/v3/episodefile/') && req.method === 'DELETE') return json(res, {});
    const em = p.match(/^\/api\/v3\/episode\/(\d+)$/);
    if (em && req.method === 'PUT') return json(res, {});
    if (p === '/api/v3/command') return json(res, { id: 1 });
    const cover = p.match(/^\/MediaCover\/(\d+)\/poster\.jpg$/);
    if (cover) {
      const poster = posters.get(Number(cover[1])) ?? fallbackPoster;
      res.writeHead(200, { 'content-type': poster.contentType });
      return res.end(poster.buf);
    }
    if (p.startsWith('/MediaCover/')) { res.writeHead(200, { 'content-type': 'image/png' }); return res.end(png1x1); }
    res.writeHead(404); res.end();
  });
  server.listen(opts.port);
  return server;
}

// When run directly (as the e2e webServer does: `node --import tsx e2e/fake-arr.ts`), reproduce
// the original single-instance, movies-only fixture exactly, on the port the smoke test expects.
const isMain = process.argv[1] && (process.argv[1].endsWith('fake-arr.ts') || process.argv[1].endsWith('fake-arr.js'));
if (isMain) {
  const movies: FakeMovie[] = files.map((f) => ({
    id: f.id, title: f.title, year: f.year, tmdbId: 1000 + f.id, qualityProfileId: f.id === 1 ? 4 : 5,
    qualityName: f.id === 1 ? 'WEBDL-1080p' : 'Bluray-2160p', path: join(libraryDir, f.rel).replace(/\\/g, '/'), sizeBytes: 1,
  }));
  startRadarrFake({ port: 7999, apiKey: 'e2e', movies, rootFolder: libraryDir.replace(/\\/g, '/') });
}
