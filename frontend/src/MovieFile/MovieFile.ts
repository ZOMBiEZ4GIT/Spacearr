import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';

export interface MovieFile extends ModelBase {
  movieId: number;
  relativePath: string;
  path: string;
  size: number;
  dateAdded: string;
  sceneName?: string;
  releaseGroup?: string;
  languages: Language[];
  quality: QualityModel;
  mediaInfo?: Record<string, unknown>;
}
