import { useState } from 'react';
import type { TreeLeaf, TreeNode } from '../api/types';
import Treemap from './Treemap';
import Legend from './Legend';

/**
 * DEV-ONLY harness for the treemap. Not mounted in production builds
 * (see src/app/routes.tsx, guarded by import.meta.env.DEV).
 */

let nextId = 100;
const leaf = (name: string, gb: number, heat: number, extra: Partial<TreeLeaf> = {}): TreeNode => {
  const itemId = extra.itemId ?? nextId++;
  return {
    name,
    bytes: Math.round(gb * 1e9),
    children: null,
    leaf: {
      itemId,
      fileId: extra.fileId ?? itemId,
      heat,
      color: heat < 0 ? '#6B7280' : '#B07410',
      posterUrl: null,
      quality: extra.quality ?? 'Bluray-1080p',
      codec: extra.codec ?? 'h264',
      resolution: extra.resolution ?? '1080p',
      instanceId: extra.instanceId ?? 1,
      instanceName: extra.instanceName ?? 'Radarr',
      ...extra,
    },
  };
};

const group = (name: string, children: TreeNode[]): TreeNode => ({
  name,
  bytes: children.reduce((n, c) => n + c.bytes, 0),
  children,
  leaf: null,
});

const movies: TreeNode[] = [
  leaf('Blade Runner 2049 (2017)', 62, 0.82, { codec: 'h264', resolution: '2160p', quality: 'Remux-2160p' }),
  leaf('Dune (2021)', 48, 0.91, { resolution: '2160p', quality: 'Remux-2160p' }),
  leaf('Arrival (2016)', 31, 0.44, { codec: 'hevc' }),
  leaf('The Thing (1982)', 27, 0.66),
  leaf('Heat (1995)', 24, 0.55, { codec: 'hevc', resolution: '2160p' }),
  leaf('Interstellar (2014)', 22, 0.73, { resolution: '2160p' }),
  leaf('Alien (1979)', 19, -1, { quality: null, codec: null, resolution: null }),
  leaf('Whiplash (2014)', 17, 0.21, { codec: 'hevc' }),
  leaf('Sicario (2015)', 15, 0.36),
  leaf('Prisoners (2013)', 14, 0.62),
  leaf('The Prestige (2006)', 12, 0.18, { quality: 'WEBDL-1080p' }),
  leaf('Moon (2009)', 11, 0.05, { codec: 'hevc', quality: 'WEBDL-1080p' }),
  leaf('Ex Machina (2014)', 9.5, 0.48),
  leaf('Annihilation (2018)', 8.4, -1, { quality: null, codec: null, resolution: null }),
  leaf('Coherence (2013)', 7.1, 0.12, { quality: 'WEBDL-720p', resolution: '720p' }),
  leaf('Primer (2004)', 5.5, 0.3, { resolution: '720p', quality: 'WEBDL-720p' }),
  leaf('Upstream Color (2013)', 4.2, 0.7),
  leaf('The Fountain (2006)', 3.6, 0.58),
  leaf('Under the Skin (2013)', 3.1, 0.4),
  leaf('Solaris (1972)', 2.4, 0.26, { codec: 'mpeg2', quality: 'SDTV' }),
  leaf('Other (5 files)', 6.8, -1, { itemId: 0, fileId: 0, quality: null, codec: null, resolution: null }),
];

const severance = group('Severance', [
  group('Season 1', [
    leaf('S01E01 Good News About Hell', 7.2, 0.68, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E02 Half Loop', 6.9, 0.64, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E03 In Perpetuity', 6.6, 0.71, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E04 The You You Are', 6.4, 0.59, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E05 The Grim Barbarity', 6.1, -1, { instanceName: 'Sonarr', instanceId: 2, quality: null, codec: null, resolution: null }),
    leaf('S01E06 Hide and Seek', 5.8, 0.52, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E07 Defiant Jazz', 5.4, 0.47, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S01E08 What Is a Living', 5.1, 0.5, { instanceName: 'Sonarr', instanceId: 2 }),
  ]),
  group('Season 2', [
    leaf('S02E01 Hello Ms. Cobel', 8.4, 0.88, { instanceName: 'Sonarr', instanceId: 2, resolution: '2160p', quality: 'WEBDL-2160p' }),
    leaf('S02E02 Goodbye Mrs. Selvig', 8.1, 0.84, { instanceName: 'Sonarr', instanceId: 2, resolution: '2160p', quality: 'WEBDL-2160p' }),
    leaf('S02E03 Who Is Alive?', 7.7, 0.79, { instanceName: 'Sonarr', instanceId: 2, resolution: '2160p', quality: 'WEBDL-2160p' }),
    leaf('S02E04 Woe’s Hollow', 7.3, 0.76, { instanceName: 'Sonarr', instanceId: 2, resolution: '2160p', quality: 'WEBDL-2160p' }),
  ]),
]);

const theBear = group('The Bear', [
  group('Season 1', [
    leaf('S01E01 System', 3.4, 0.33, { instanceName: 'Sonarr', instanceId: 2, codec: 'hevc' }),
    leaf('S01E02 Hands', 3.2, 0.29, { instanceName: 'Sonarr', instanceId: 2, codec: 'hevc' }),
    leaf('S01E03 Brigade', 3.1, 0.35, { instanceName: 'Sonarr', instanceId: 2, codec: 'hevc' }),
    leaf('S01E04 Dogs', 2.9, 0.24, { instanceName: 'Sonarr', instanceId: 2, codec: 'hevc' }),
    leaf('S01E05 Sheridan', 2.7, 0.31, { instanceName: 'Sonarr', instanceId: 2, codec: 'hevc' }),
  ]),
  group('Season 2', [
    leaf('S02E01 Beef', 4.1, 0.42, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S02E02 Pasta', 3.9, 0.39, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S02E03 Sundae', 3.8, 0.45, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S02E04 Honeydew', 3.6, 0.37, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S02E05 Pop', 3.4, 0.41, { instanceName: 'Sonarr', instanceId: 2 }),
    leaf('S02E06 Fishes', 5.2, 0.61, { instanceName: 'Sonarr', instanceId: 2 }),
  ]),
]);

const children = [...movies, severance, theBear];
const sampleTree: TreeNode = {
  name: 'Library',
  bytes: children.reduce((n, c) => n + c.bytes, 0),
  children,
  leaf: null,
};

const colorModes = ['heat', 'quality', 'codec', 'resolution', 'instance'];

export default function DevTreemapPage() {
  const [colorBy, setColorBy] = useState('heat');
  const [selected, setSelected] = useState<number | null>(null);
  const [label, setLabel] = useState('');
  const [posters, setPosters] = useState(false);
  const [zoom, setZoom] = useState('Library');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 8, padding: 12 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <strong>Dev treemap</strong>
        <select value={colorBy} onChange={(e) => setColorBy(e.target.value)} aria-label="Colour by">
          {colorModes.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
        <label style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
          <input type="checkbox" checked={posters} onChange={(e) => setPosters(e.target.checked)} /> posters
        </label>
        <Legend colorBy={colorBy} heatMode="relative" categories={['h264', 'hevc', 'mpeg2']} />
        <span className="muted" data-testid="dev-selection">{label || 'nothing selected'}</span>
        <span className="muted" data-testid="dev-zoom">zoom: {zoom}</span>
      </div>
      <div style={{ flex: 1, minHeight: 400, border: '1px solid var(--line)' }}>
        <Treemap
          tree={sampleTree}
          colorBy={colorBy}
          heatMode="relative"
          selectedItemId={selected}
          showPosters={posters}
          onSelect={(l, name) => { setSelected(l?.itemId ?? null); setLabel(l ? `selected: ${name}` : 'nothing selected'); }}
          onZoom={(node, p) => setZoom(p.map((n) => n.name).join(' / ') + (node ? '' : ''))}
        />
      </div>
    </div>
  );
}
