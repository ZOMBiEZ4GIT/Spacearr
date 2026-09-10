import { api, ApiError } from '../client';

describe('api client', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); });

  it('parses json and sends credentials', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ a: 1 }), { status: 200, headers: { 'content-type': 'application/json' } }));
    const r = await api.get<{ a: number }>('/api/v1/x');
    expect(r.a).toBe(1);
    expect(fetchMock.mock.calls[0][1].credentials).toBe('same-origin');
  });

  it('throws ApiError with server message and emits unauthorized on 401', async () => {
    const handler = vi.fn();
    window.addEventListener('spacearr:unauthorized', handler);
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ error: 'nope' }), { status: 401 }));
    await expect(api.get('/api/v1/x')).rejects.toMatchObject({ status: 401, message: 'nope' });
    expect(handler).toHaveBeenCalled();
    fetchMock.mockResolvedValue(new Response(undefined, { status: 204 }));
    await expect(api.post('/api/v1/y', { z: 1 })).resolves.toBeUndefined();
    expect(fetchMock.mock.calls[1][1].body).toBe('{"z":1}');
    expect(new ApiError(500, 'x').message).toBe('x');
  });
});
