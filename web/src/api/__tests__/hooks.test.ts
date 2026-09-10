import { describe, expect, it } from 'vitest';
import { isTerminalJobStatus, libraryHasMore, LIBRARY_PAGE_SIZE } from '../hooks';
import type { JobStatus, Page, LibraryItem } from '../types';

describe('isTerminalJobStatus', () => {
  // This is the predicate useJob's refetchInterval is built on (`terminal(q.state.data) ? false :
  // 2000`), and what ActionDialog/ScanPill use to decide whether a polled job can be trusted over
  // a possibly-stale SSE entry - it must draw the line in exactly the right place: Queued/Running
  // keep polling, everything else stops.
  it.each<JobStatus>(['queued', 'running'])('is not terminal for %s', (status) => {
    expect(isTerminalJobStatus(status)).toBe(false);
  });

  it.each<JobStatus>(['succeeded', 'failed', 'cancelled'])('is terminal for %s', (status) => {
    expect(isTerminalJobStatus(status)).toBe(true);
  });

  it('is not terminal for null or undefined (no job loaded yet)', () => {
    expect(isTerminalJobStatus(null)).toBe(false);
    expect(isTerminalJobStatus(undefined)).toBe(false);
  });
});

const page = (items: number, total: number): Page<LibraryItem> => ({
  items: Array.from({ length: items }, () => ({} as LibraryItem)),
  total, page: 1, pageSize: LIBRARY_PAGE_SIZE,
});

describe('libraryHasMore', () => {
  it('is false with no pages loaded yet', () => {
    expect(libraryHasMore([])).toBe(false);
  });

  it('is true when a full page was returned and more rows remain past the clamp', () => {
    // The bug this replaces: pageSize clamps at 1000 server-side, so an ever-growing single
    // request eventually returns a short page forever. Real paging must keep offering more past
    // that point as long as full-sized pages keep coming back and total says there's more.
    expect(libraryHasMore([page(LIBRARY_PAGE_SIZE, 1500)])).toBe(true);
    expect(libraryHasMore([page(LIBRARY_PAGE_SIZE, 1500), page(LIBRARY_PAGE_SIZE, 1500)])).toBe(true);
  });

  it('is false once every row has been loaded', () => {
    expect(libraryHasMore([page(LIBRARY_PAGE_SIZE, 1500), page(LIBRARY_PAGE_SIZE, 1500), page(500, 1500)])).toBe(false);
  });

  it('is false when the last page came back short, even if it disagrees with total', () => {
    // A short page is always the last one - even if `total` changed between requests (rows
    // added/removed mid-session), a page smaller than the requested size must never be followed
    // by another request.
    expect(libraryHasMore([page(200, 5000)])).toBe(false);
  });
});
