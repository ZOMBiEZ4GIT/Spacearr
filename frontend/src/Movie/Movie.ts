import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';

export interface Image {
  coverType: string;
  url: string;
  remoteUrl?: string;
}

export interface RatingValues {
  value: number;
  votes: number;
  type?: string;
}

export interface Ratings {
  imdb: RatingValues;
  tmdb: RatingValues;
  metacritic: RatingValues;
  rottenTomatoes: RatingValues;
  trakt: RatingValues;
}

export type MovieStatus = 'tba' | 'announced' | 'inCinemas' | 'released' | 'deleted';

export type MovieAvailability = 'announced' | 'inCinemas' | 'released' | 'preDB';

export type MovieMonitor = 'movieOnly' | 'movieAndCollection' | 'none';

export interface AlternateTitle {
  sourceType: string;
  movieMetadataId: number;
  title: string;
  id: number;
}

interface Movie extends ModelBase {
  title: string;
  sortTitle: string;
  year: number;
  titleSlug: string;
  overview: string;
  monitored: boolean;
  status: MovieStatus;
  minimumAvailability: MovieAvailability;
  images: Image[];
  alternateTitles: AlternateTitle[];
  tmdbId: number;
  imdbId?: string;
  qualityProfileId: number;
  rootFolderPath: string;
  path: string;
  sizeOnDisk: number;
  hasFile: boolean;
  isAvailable: boolean;
  tags: number[];
  ratings: Ratings;
  certification?: string;
  genres: string[];
  studio?: string;
  added: string;
  runtime: number;
  inCinemas?: string;
  digitalRelease?: string;
  physicalRelease?: string;
  originalLanguage: Language;
  originalTitle?: string;
  collection?: { name: string; tmdbId: number };
  addOptions?: {
    monitor: MovieMonitor;
    searchForMovie: boolean;
  };
  movieFileId?: number;
  grabbed?: boolean;
  isSaving?: boolean;
}

export default Movie;
