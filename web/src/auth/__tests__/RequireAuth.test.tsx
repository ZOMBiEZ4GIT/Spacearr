import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import RequireAuth from '../RequireAuth';

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

function makeQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderApp(qc: QueryClient) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/setup" element={<div>Setup Page</div>} />
          <Route path="/login" element={<div>Login Page</div>} />
          <Route element={<RequireAuth />}>
            <Route path="/" element={<div>Protected Child</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('RequireAuth', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); });

  it('renders /setup when setup is incomplete', async () => {
    fetchMock.mockImplementation((path: string) => {
      if (path.includes('/system/status')) return Promise.resolve(jsonResponse({ version: '0.1.0', setupComplete: false, tools: { ffprobe: true, mediainfo: true } }));
      if (path.includes('/auth/me')) return Promise.resolve(jsonResponse({ error: 'unauthorized' }, 401));
      return Promise.resolve(new Response('', { status: 404 }));
    });
    renderApp(makeQueryClient());
    await waitFor(() => expect(screen.getByText('Setup Page')).toBeInTheDocument());
  });

  it('renders /login when setup is complete and /auth/me 401s', async () => {
    fetchMock.mockImplementation((path: string) => {
      if (path.includes('/system/status')) return Promise.resolve(jsonResponse({ version: '0.1.0', setupComplete: true, tools: { ffprobe: true, mediainfo: true } }));
      if (path.includes('/auth/me')) return Promise.resolve(jsonResponse({ error: 'unauthorized' }, 401));
      return Promise.resolve(new Response('', { status: 404 }));
    });
    renderApp(makeQueryClient());
    await waitFor(() => expect(screen.getByText('Login Page')).toBeInTheDocument());
  });

  it('renders the protected child when /auth/me succeeds', async () => {
    fetchMock.mockImplementation((path: string) => {
      if (path.includes('/system/status')) return Promise.resolve(jsonResponse({ version: '0.1.0', setupComplete: true, tools: { ffprobe: true, mediainfo: true } }));
      if (path.includes('/auth/me')) return Promise.resolve(jsonResponse({ username: 'roland', apiKey: 'k' }));
      return Promise.resolve(new Response('', { status: 404 }));
    });
    renderApp(makeQueryClient());
    await waitFor(() => expect(screen.getByText('Protected Child')).toBeInTheDocument());
  });

  it('recovers to the protected child after a login clears the cached /auth/me error, never re-rendering /login', async () => {
    let meCalls = 0;
    fetchMock.mockImplementation((path: string) => {
      if (path.includes('/system/status')) return Promise.resolve(jsonResponse({ version: '0.1.0', setupComplete: true, tools: { ffprobe: true, mediainfo: true } }));
      if (path.includes('/auth/me')) {
        meCalls += 1;
        return Promise.resolve(meCalls === 1 ? jsonResponse({ error: 'unauthorized' }, 401) : jsonResponse({ username: 'roland', apiKey: 'k' }));
      }
      return Promise.resolve(new Response('', { status: 404 }));
    });

    const qc = makeQueryClient();
    const first = renderApp(qc);
    await waitFor(() => expect(screen.getByText('Login Page')).toBeInTheDocument());

    // Simulate what useLogin's onSuccess does: drop the stale error before remounting.
    first.unmount();
    qc.removeQueries({ queryKey: ['me'] });
    renderApp(qc);

    await waitFor(() => expect(screen.getByText('Protected Child')).toBeInTheDocument());
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument();
    expect(meCalls).toBe(2);
  });
});
