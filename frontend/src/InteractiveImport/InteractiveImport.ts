import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';
import Movie from 'Movie/Movie';
import { QualityModel } from 'Quality/Quality';

interface InteractiveImport extends ModelBase {
  path: string;
  relativePath: string;
  name: string;
  size: number;
  movie?: Movie;
  quality: QualityModel;
  languages: Language[];
  rejections: string[];
}

export default InteractiveImport;
