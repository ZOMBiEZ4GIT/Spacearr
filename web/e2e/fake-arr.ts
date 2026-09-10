import { createServer } from 'node:http';
import { join } from 'node:path';
import { files, libraryDir } from './fixture';

const movies = files.map((f) => ({
  id: f.id, title: f.title, year: f.year, monitored: true, qualityProfileId: 5, tmdbId: 1000 + f.id, tags: [],
  images: [{ coverType: 'poster', url: `/MediaCover/${f.id}/poster.jpg` }],
  hasFile: true, movieFile: { id: 70 + f.id, movieId: f.id, path: join(libraryDir, f.rel).replace(/\\/g, '/'), size: 1, quality: { quality: { id: 1, name: f.id === 1 ? 'WEBDL-1080p' : 'Bluray-2160p' } } },
}));
const png1x1 = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64');

createServer((req, res) => {
  const url = new URL(req.url!, 'http://x');
  if (req.headers['x-api-key'] !== 'e2e') { res.writeHead(401); return res.end(); }
  const json = (b: unknown) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify(b)); };
  const p = url.pathname;
  if (p === '/api/v3/system/status') return json({ appName: 'Radarr', version: '6.3.0' });
  if (p === '/api/v3/qualityprofile') return json([{ id: 4, name: 'HD-1080p' }, { id: 5, name: 'Ultra-HD' }]);
  if (p === '/api/v3/tag') return json([]);
  if (p === '/api/v3/rootfolder') return json([{ path: libraryDir.replace(/\\/g, '/') }]);
  if (p === '/api/v3/movie' && req.method === 'GET') return json(movies);
  const m = p.match(/^\/api\/v3\/movie\/(\d+)$/);
  if (m) return json(movies.find((x) => x.id === Number(m[1])) ?? {});
  if (p.startsWith('/api/v3/moviefile/') && req.method === 'DELETE') return json({});
  if (p === '/api/v3/command') return json({ id: 1 });
  if (p.startsWith('/MediaCover/')) { res.writeHead(200, { 'content-type': 'image/png' }); return res.end(png1x1); }
  res.writeHead(404); res.end();
}).listen(7999);
