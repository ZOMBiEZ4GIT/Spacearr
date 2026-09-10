import { fireEvent, render, screen } from '@testing-library/react';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import LibraryTable from '../LibraryTable';
import type { LibraryItem } from '../../api/types';

const item = (itemId: number, title: string, extra: Partial<LibraryItem> = {}): LibraryItem => ({
  itemId, instanceId: 1, instanceName: 'Movies', instanceType: 'radarr', kind: 'movie', title, year: 1999,
  seriesId: null, seriesTitle: null, seasonNumber: null, episodes: null,
  qualityProfileName: 'HD-1080p', qualityProfileId: 4, qualityName: 'Bluray-1080p', monitored: true, tags: null, posterUrl: null,
  fileId: itemId * 10, path: `/media/${title}.mkv`, sizeBytes: 5e9, durationSeconds: 7200, width: 1920, height: 1080, frameRate: 24,
  videoCodec: 'h264', bitDepth: 8, hdrFormat: null, videoBitrateBps: 5e6, overallBitrateBps: 5e6, audioSummary: null, probeError: null,
  nbpp: 0.1, resolution: '1080p', tmdbId: 1, tvdbId: null, heat: 0.5, color: '#888', ...extra,
});

beforeAll(() => {
  class RO {
    constructor(private cb: ResizeObserverCallback) {}
    observe() { this.cb([{ contentRect: { width: 800, height: 600 } } as unknown as ResizeObserverEntry], this as unknown as ResizeObserver); }
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', RO);
  // jsdom implements no scrolling at all.
  HTMLElement.prototype.scrollTo = () => {};
});

const props = {
  items: [item(1, 'Alpha'), item(2, 'Beta')],
  total: 2, hasMore: false, selected: null, selectedFileId: null, sort: 'size', order: 'desc',
  onSort: () => {}, onSelect: () => {}, onMore: () => {},
};

describe('LibraryTable', () => {
  it('sorts from the keyboard: the header label is a real button', () => {
    const onSort = vi.fn();
    render(<LibraryTable {...props} onSort={onSort} />);
    const header = screen.getByRole('button', { name: /^Heat/ });
    fireEvent.click(header);
    expect(onSort).toHaveBeenCalledWith('heat');
    // Non-sortable columns stay plain text.
    expect(screen.queryByRole('button', { name: /^Codec/ })).toBeNull();
  });

  it('selects a row with Enter and with Space', () => {
    const onSelect = vi.fn();
    render(<LibraryTable {...props} onSelect={onSelect} />);
    const rows = screen.getAllByRole('row').filter((r) => r.tabIndex === 0);
    expect(rows).toHaveLength(2);
    fireEvent.keyDown(rows[0], { key: 'Enter' });
    fireEvent.keyDown(rows[1], { key: ' ' });
    expect(onSelect.mock.calls.map((c) => c[0].title)).toEqual(['Alpha', 'Beta']);
  });

  it('marks the selected row inside a grid, where aria-selected is valid', () => {
    render(<LibraryTable {...props} selected={2} />);
    expect(screen.getByRole('grid')).toBeTruthy();
    const selected = screen.getAllByRole('row').filter((r) => r.getAttribute('aria-selected') === 'true');
    expect(selected).toHaveLength(1);
    expect(selected[0].textContent).toContain('Beta');
  });

  // Important finding #2: "Load more" must reflect the infinite query's own hasNextPage, not a
  // comparison against `total` (which a short last page can disagree with once total is out of
  // date) - otherwise the button can be visible while calling onMore does nothing.
  it('shows Load more only when hasMore is true, regardless of how items compares to total', () => {
    const { rerender } = render(<LibraryTable {...props} total={500} hasMore={false} />);
    expect(screen.queryByRole('button', { name: 'Load more' })).toBeNull();
    rerender(<LibraryTable {...props} total={500} hasMore />);
    expect(screen.getByRole('button', { name: 'Load more' })).toBeInTheDocument();
  });

  it('calls onMore when Load more is clicked', () => {
    const onMore = vi.fn();
    render(<LibraryTable {...props} total={500} hasMore onMore={onMore} />);
    fireEvent.click(screen.getByRole('button', { name: 'Load more' }));
    expect(onMore).toHaveBeenCalledTimes(1);
  });
});
