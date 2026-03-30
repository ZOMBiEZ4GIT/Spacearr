export interface TreemapItem {
  id: number;
  title: string;
  sizeBytes: number;
  bitrateBps: number;
  codec: string;
  resolution: string;
  qualityProfile: string;
  source: string;
  parentGroup: string | null;
  posterUrl?: string;
}

export type ColorMode = 'bitrate' | 'quality' | 'codec' | 'resolution';

export interface TreemapNode {
  name: string;
  value?: number;
  children?: TreemapNode[];
  data?: TreemapItem;
}

export interface TreemapLayoutNode {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
  data: TreemapNode;
  depth: number;
  height: number;
  parent: TreemapLayoutNode | null;
  children?: TreemapLayoutNode[];
  value: number;
}

export interface TooltipState {
  item: TreemapItem | null;
  x: number;
  y: number;
  visible: boolean;
}

export interface ZoomState {
  path: string[];
  currentGroup: string | null;
}
