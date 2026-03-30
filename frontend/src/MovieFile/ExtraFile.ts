import ModelBase from 'App/ModelBase';

export interface ExtraFile extends ModelBase {
  movieId: number;
  movieFileId: number;
  relativePath: string;
  extension: string;
  type: string;
}
