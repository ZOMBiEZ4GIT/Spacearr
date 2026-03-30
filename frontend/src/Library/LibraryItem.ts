export interface LibraryItem {
  id: number;
  title: string;
  path: string;
  size: number;
  bitrate: number;
  quality: string;
  codec: string;
  resolution: string;
  duration: number;
  container: string;
  source: 'radarr' | 'sonarr';
  year?: number;
  qualityProfile?: string;
  monitored?: boolean;
  seriesTitle?: string;
  seasonNumber?: number;
  episodeNumber?: number;
  estimatedSavings?: number;
  suggestedQuality?: string;
  posterUrl?: string;
}

export function formatSize(bytes: number): string {
  if (bytes === 0) {
    return '0 B';
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const size = bytes / Math.pow(1024, i);

  return `${size.toFixed(i > 1 ? 1 : 0)} ${units[i]}`;
}

export function formatBitrate(kbps: number): string {
  const mbps = kbps / 1000;

  if (mbps >= 10) {
    return `${mbps.toFixed(0)} Mbps`;
  }

  return `${mbps.toFixed(1)} Mbps`;
}

export function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  return `${minutes}m`;
}

export function getBitrateLevel(kbps: number): 'low' | 'mediumLow' | 'medium' | 'mediumHigh' | 'high' {
  const mbps = kbps / 1000;

  if (mbps < 10) {
    return 'low';
  }

  if (mbps < 20) {
    return 'mediumLow';
  }

  if (mbps < 40) {
    return 'medium';
  }

  if (mbps < 60) {
    return 'mediumHigh';
  }

  return 'high';
}

// Mock data for development
export const MOCK_LIBRARY_ITEMS: LibraryItem[] = [
  {
    id: 1,
    title: 'The Matrix',
    path: '/movies/The Matrix (1999)/The.Matrix.1999.2160p.UHD.BluRay.x265.mkv',
    size: 84_200_000_000,
    bitrate: 45_000,
    quality: 'Bluray-2160p',
    codec: 'x265',
    resolution: '2160p',
    duration: 8160,
    container: 'mkv',
    source: 'radarr',
    year: 1999,
    qualityProfile: 'Ultra-HD',
    monitored: true,
    estimatedSavings: 42_000_000_000,
    suggestedQuality: 'Bluray-1080p',
    posterUrl: 'https://image.tmdb.org/t/p/w300/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg',
  },
  {
    id: 2,
    title: 'Breaking Bad - S01E01',
    path: '/tv/Breaking Bad/Season 01/Breaking.Bad.S01E01.1080p.BluRay.x264.mkv',
    size: 5_200_000_000,
    bitrate: 8_500,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 3480,
    container: 'mkv',
    source: 'sonarr',
    year: 2008,
    qualityProfile: 'HD-1080p',
    monitored: true,
    seriesTitle: 'Breaking Bad',
    seasonNumber: 1,
    episodeNumber: 1,
  },
  {
    id: 3,
    title: 'Inception',
    path: '/movies/Inception (2010)/Inception.2010.2160p.UHD.BluRay.REMUX.mkv',
    size: 78_400_000_000,
    bitrate: 62_000,
    quality: 'Remux-2160p',
    codec: 'HEVC',
    resolution: '2160p',
    duration: 8880,
    container: 'mkv',
    source: 'radarr',
    year: 2010,
    qualityProfile: 'Ultra-HD',
    monitored: true,
    estimatedSavings: 55_000_000_000,
    suggestedQuality: 'Bluray-2160p',
    posterUrl: 'https://image.tmdb.org/t/p/w300/oYuLEt3zVCKq57qu2F8dT7NIa6f.jpg',
  },
  {
    id: 4,
    title: 'The Office - S02E05',
    path: '/tv/The Office/Season 02/The.Office.S02E05.720p.BluRay.x264.mkv',
    size: 1_800_000_000,
    bitrate: 5_200,
    quality: 'Bluray-720p',
    codec: 'x264',
    resolution: '720p',
    duration: 1320,
    container: 'mkv',
    source: 'sonarr',
    year: 2005,
    qualityProfile: 'HD-720p',
    monitored: true,
    seriesTitle: 'The Office',
    seasonNumber: 2,
    episodeNumber: 5,
  },
  {
    id: 5,
    title: 'Dune',
    path: '/movies/Dune (2021)/Dune.2021.2160p.WEB-DL.DDP5.1.Atmos.DV.x265.mkv',
    size: 22_600_000_000,
    bitrate: 18_500,
    quality: 'WEBDL-2160p',
    codec: 'x265',
    resolution: '2160p',
    duration: 9360,
    container: 'mkv',
    source: 'radarr',
    year: 2021,
    qualityProfile: 'Ultra-HD',
    monitored: true,
    posterUrl: 'https://image.tmdb.org/t/p/w300/d5NXSklXo0qyIYkgV94XAgMIckC.jpg',
  },
  {
    id: 6,
    title: 'Game of Thrones - S08E03',
    path: '/tv/Game of Thrones/Season 08/Game.of.Thrones.S08E03.1080p.BluRay.x264.mkv',
    size: 12_400_000_000,
    bitrate: 22_000,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 4920,
    container: 'mkv',
    source: 'sonarr',
    year: 2019,
    qualityProfile: 'HD-1080p',
    monitored: false,
    seriesTitle: 'Game of Thrones',
    seasonNumber: 8,
    episodeNumber: 3,
  },
  {
    id: 7,
    title: 'Interstellar',
    path: '/movies/Interstellar (2014)/Interstellar.2014.2160p.UHD.BluRay.REMUX.mkv',
    size: 85_600_000_000,
    bitrate: 58_000,
    quality: 'Remux-2160p',
    codec: 'HEVC',
    resolution: '2160p',
    duration: 10140,
    container: 'mkv',
    source: 'radarr',
    year: 2014,
    qualityProfile: 'Ultra-HD',
    monitored: true,
    estimatedSavings: 60_000_000_000,
    suggestedQuality: 'Bluray-2160p',
    posterUrl: 'https://image.tmdb.org/t/p/w300/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg',
  },
  {
    id: 8,
    title: 'Stranger Things - S04E07',
    path: '/tv/Stranger Things/Season 04/Stranger.Things.S04E07.1080p.WEB-DL.x264.mkv',
    size: 4_100_000_000,
    bitrate: 6_800,
    quality: 'WEBDL-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 4500,
    container: 'mkv',
    source: 'sonarr',
    year: 2022,
    qualityProfile: 'HD-1080p',
    monitored: true,
    seriesTitle: 'Stranger Things',
    seasonNumber: 4,
    episodeNumber: 7,
  },
  {
    id: 9,
    title: 'Game of Thrones - S08E04',
    path: '/tv/Game of Thrones/Season 08/Game.of.Thrones.S08E04.1080p.BluRay.x264.mkv',
    size: 10_800_000_000,
    bitrate: 20_000,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 4680,
    container: 'mkv',
    source: 'sonarr',
    year: 2019,
    qualityProfile: 'HD-1080p',
    monitored: false,
    seriesTitle: 'Game of Thrones',
    seasonNumber: 8,
    episodeNumber: 4,
  },
  {
    id: 10,
    title: 'Game of Thrones - S07E07',
    path: '/tv/Game of Thrones/Season 07/Game.of.Thrones.S07E07.1080p.BluRay.x264.mkv',
    size: 9_200_000_000,
    bitrate: 18_000,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 4800,
    container: 'mkv',
    source: 'sonarr',
    year: 2017,
    qualityProfile: 'HD-1080p',
    monitored: true,
    seriesTitle: 'Game of Thrones',
    seasonNumber: 7,
    episodeNumber: 7,
  },
  {
    id: 11,
    title: 'Breaking Bad - S01E02',
    path: '/tv/Breaking Bad/Season 01/Breaking.Bad.S01E02.1080p.BluRay.x264.mkv',
    size: 4_800_000_000,
    bitrate: 8_200,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 2880,
    container: 'mkv',
    source: 'sonarr',
    year: 2008,
    qualityProfile: 'HD-1080p',
    monitored: true,
    seriesTitle: 'Breaking Bad',
    seasonNumber: 1,
    episodeNumber: 2,
  },
  {
    id: 12,
    title: 'Breaking Bad - S05E16',
    path: '/tv/Breaking Bad/Season 05/Breaking.Bad.S05E16.1080p.BluRay.x264.mkv',
    size: 5_600_000_000,
    bitrate: 9_000,
    quality: 'Bluray-1080p',
    codec: 'x264',
    resolution: '1080p',
    duration: 3360,
    container: 'mkv',
    source: 'sonarr',
    year: 2013,
    qualityProfile: 'HD-1080p',
    monitored: true,
    seriesTitle: 'Breaking Bad',
    seasonNumber: 5,
    episodeNumber: 16,
  },
];
