interface Quality {
  id: number;
  name: string;
  source?: string;
  resolution?: number;
}

export interface QualityRevision {
  version: number;
  real: number;
  isRepack: boolean;
}

export interface QualityModel {
  quality: Quality;
  revision: QualityRevision;
}

export default Quality;
