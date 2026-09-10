import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ActionDialog from '../ActionDialog';

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('ActionDialog', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); vi.useFakeTimers({ shouldAdvanceTime: true }); });
  afterEach(() => vi.useRealTimers());

  it('previews, shows steps and warning, then executes with the token', async () => {
    fetchMock.mockImplementation((url: string, init: RequestInit) => {
      if (url.endsWith('/actions/preview')) return Promise.resolve(json({ request: { type: 'replace', itemId: 7, targetProfileId: 4 }, title: 'Film (2020)', instanceName: 'Movies', instanceType: 'radarr', bytesFreedNow: 44_100_000_000, estimate: { estimatedBytes: 12e9, savingsBytes: 32.1e9, samples: 6, basis: 'library' }, warning: 'Careful.', steps: [{ description: 'Set quality profile to HD', method: 'PUT', path: '/x' }, { description: 'Delete the current file', method: 'DELETE', path: '/y' }, { description: 'Search', method: 'POST', path: '/z' }], confirmToken: 'tok', expiresAt: '2026-09-10T12:10:00Z' }));
      if (url.endsWith('/actions/execute')) { expect(JSON.parse(init.body as string).confirmToken).toBe('tok'); return Promise.resolve(json({ jobId: 99 }, 202)); }
      return Promise.resolve(json({}, 404));
    });
    const qc = new QueryClient();
    render(<QueryClientProvider client={qc}><ActionDialog kind="replace" itemId={7} profile={{ id: 4, name: 'HD', estimate: { estimatedBytes: 0, savingsBytes: 0, samples: 0, basis: 'table' } }} onClose={() => {}} onDone={() => {}} /></QueryClientProvider>);
    await screen.findByText('Film (2020)');
    expect(screen.getByText('Careful.')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    const confirm = screen.getByRole('button', { name: /Replace, free 44\.1 GB now/ });
    expect(confirm).toBeDisabled();
    await waitFor(() => expect(confirm).toBeEnabled(), { timeout: 3000 });
    fireEvent.click(confirm);
    await waitFor(() => expect(fetchMock.mock.calls.some(([u]) => String(u).endsWith('/actions/execute'))).toBe(true));
  });
});
