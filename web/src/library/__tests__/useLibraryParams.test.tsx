import { renderHook, act } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { useLibraryParams } from '../useLibraryParams';

describe('useLibraryParams', () => {
  it('reads defaults and writes to the url', () => {
    const { result } = renderHook(() => useLibraryParams(), { wrapper: ({ children }) => <MemoryRouter initialEntries={['/library?colorBy=codec&minBytes=5000000000']}>{children}</MemoryRouter> });
    expect(result.current.params.colorBy).toBe('codec');
    expect(result.current.params.minBytes).toBe(5_000_000_000);
    expect(result.current.params.sort).toBe('size');
    act(() => result.current.set({ sort: 'heat', order: 'asc', sel: 42 }));
    expect(result.current.params.sort).toBe('heat');
    expect(result.current.params.sel).toBe(42);
    act(() => result.current.set({ sel: null }));
    expect(result.current.params.sel).toBeNull();
  });
});
