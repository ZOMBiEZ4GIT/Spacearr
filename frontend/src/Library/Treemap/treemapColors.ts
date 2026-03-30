import { TreemapItem, ColorMode } from './treemapTypes';

// Bitrate heat: maps bitrate (bps) to a color
// Green -> Yellow -> Orange -> Red
export function getBitrateColor(bitrateBps: number): string {
  const mbps = bitrateBps / 1_000_000;

  if (mbps < 10) {
    return '#2d8a4e';
  }

  if (mbps < 20) {
    return '#6e8b3d';
  }

  if (mbps < 40) {
    return '#c4a43e';
  }

  if (mbps < 60) {
    return '#f5a623';
  }

  return '#e94560';
}

// Quality color: distinct color per quality profile
const QUALITY_COLORS: Record<string, string> = {
  'Bluray-2160p': '#7b68ee',
  'Bluray-1080p': '#4169e1',
  'Bluray-720p': '#6495ed',
  'WEBDL-2160p': '#9370db',
  'WEBDL-1080p': '#5b9bd5',
  'WEBDL-720p': '#87ceeb',
  'WEBRip-2160p': '#8a2be2',
  'WEBRip-1080p': '#6a5acd',
  'WEBRip-720p': '#7986cb',
  'HDTV-2160p': '#2e8b57',
  'HDTV-1080p': '#3cb371',
  'HDTV-720p': '#66cdaa',
  'DVD': '#cd853f',
  'SDTV': '#d2691e',
};

const DEFAULT_QUALITY_COLORS = [
  '#e06c75', '#e5c07b', '#98c379', '#61afef', '#c678dd',
  '#56b6c2', '#d19a66', '#be5046', '#abb2bf', '#528bff',
];

export function getQualityColor(quality: string): string {
  if (QUALITY_COLORS[quality]) {
    return QUALITY_COLORS[quality];
  }

  // Deterministic color from hash
  let hash = 0;

  for (let i = 0; i < quality.length; i++) {
    hash = quality.charCodeAt(i) + ((hash << 5) - hash);
  }

  return DEFAULT_QUALITY_COLORS[Math.abs(hash) % DEFAULT_QUALITY_COLORS.length];
}

// Codec color: distinct color per codec
const CODEC_COLORS: Record<string, string> = {
  'x265': '#2d8a4e',
  'h265': '#2d8a4e',
  'hevc': '#2d8a4e',
  'x264': '#4169e1',
  'h264': '#4169e1',
  'avc': '#4169e1',
  'av1': '#9370db',
  'vp9': '#cd853f',
  'mpeg4': '#d2691e',
  'mpeg2': '#8b4513',
  'xvid': '#a0522d',
  'divx': '#8b6914',
  'wmv': '#708090',
  'vc1': '#556b2f',
};

export function getCodecColor(codec: string): string {
  const normalized = codec.toLowerCase().replace(/[^a-z0-9]/g, '');

  for (const [key, color] of Object.entries(CODEC_COLORS)) {
    if (normalized.includes(key)) {
      return color;
    }
  }

  // Deterministic fallback
  let hash = 0;

  for (let i = 0; i < codec.length; i++) {
    hash = codec.charCodeAt(i) + ((hash << 5) - hash);
  }

  return DEFAULT_QUALITY_COLORS[Math.abs(hash) % DEFAULT_QUALITY_COLORS.length];
}

// Resolution color
const RESOLUTION_COLORS: Record<string, string> = {
  '2160p': '#7b68ee',
  '4k': '#7b68ee',
  '1080p': '#4169e1',
  '720p': '#3cb371',
  '576p': '#cd853f',
  '480p': '#d2691e',
};

export function getResolutionColor(resolution: string): string {
  const normalized = resolution.toLowerCase();

  for (const [key, color] of Object.entries(RESOLUTION_COLORS)) {
    if (normalized.includes(key)) {
      return color;
    }
  }

  return '#708090';
}

// Get color based on current mode
export function getColor(item: TreemapItem, mode: ColorMode): string {
  switch (mode) {
    case 'bitrate':
      return getBitrateColor(item.bitrateBps);
    case 'quality':
      return getQualityColor(item.qualityProfile);
    case 'codec':
      return getCodecColor(item.codec);
    case 'resolution':
      return getResolutionColor(item.resolution);
    default:
      return '#708090';
  }
}
