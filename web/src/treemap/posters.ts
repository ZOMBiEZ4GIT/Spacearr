/** Lazily loads poster images for treemap leaves, capped at `maxInflight` concurrent requests. */
export class PosterCache {
  private images = new Map<number, HTMLImageElement>();
  private failed = new Set<number>();
  private queued = new Set<number>();
  private queue: number[] = [];
  private inflight = 0;
  private onLoad: () => void;
  private maxInflight: number;

  constructor(onLoad: () => void, maxInflight = 6) {
    this.onLoad = onLoad;
    this.maxInflight = maxInflight;
  }

  /** Returns a decoded image, or null while it loads / if it will never load. */
  get(itemId: number): HTMLImageElement | null {
    if (itemId <= 0 || this.failed.has(itemId)) return null;
    const img = this.images.get(itemId);
    if (img) return img.complete && img.naturalWidth > 0 ? img : null;
    if (!this.queued.has(itemId)) { this.queued.add(itemId); this.queue.push(itemId); this.pump(); }
    return null;
  }

  private pump() {
    while (this.inflight < this.maxInflight && this.queue.length > 0) {
      const id = this.queue.shift()!;
      this.queued.delete(id);
      if (this.images.has(id) || this.failed.has(id)) continue;
      const img = new Image();
      this.images.set(id, img);
      this.inflight++;
      img.onload = () => { this.inflight--; this.onLoad(); this.pump(); };
      img.onerror = () => { this.inflight--; this.failed.add(id); this.images.delete(id); this.pump(); };
      img.src = `/api/v1/posters/${id}`;
    }
  }
}
