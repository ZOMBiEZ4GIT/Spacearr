import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ActionDialog from '../ActionDialog';

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('ActionDialog', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); vi.useFakeTimers({ shouldAdvanceTime: true }); });
  afterEach(() => vi.useRealTimers());

  it('previews, shows steps and warning, then executes with the token', async () => {
    fetchMock.mockImplementation((url: string) => {
      if (url.endsWith('/actions/preview')) return Promise.resolve(json({ request: { type: 'replace', itemId: 7, targetProfileId: 4 }, title: 'Film (2020)', instanceName: 'Movies', instanceType: 'radarr', bytesFreedNow: 44_100_000_000, estimate: { estimatedBytes: 12e9, savingsBytes: 32.1e9, samples: 6, basis: 'library' }, warning: 'Careful.', steps: [{ description: 'Set quality profile to HD', method: 'PUT', path: '/x' }, { description: 'Delete the current file', method: 'DELETE', path: '/y' }, { description: 'Search', method: 'POST', path: '/z' }], confirmToken: 'tok', expiresAt: '2026-09-10T12:10:00Z' }));
      if (url.endsWith('/actions/execute')) return Promise.resolve(json({ jobId: 99 }, 202));
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
    // The mock itself must never throw an assertion (a synchronous throw inside a fetch mock
    // rejects the caller's promise and gets swallowed as a generic "Could not start the action"
    // UI error, silently passing the test) - so the real assertion lives out here instead,
    // against the recorded call.
    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([u]) => String(u).endsWith('/actions/execute'));
      expect(call).toBeTruthy();
      expect(JSON.parse((call as [string, RequestInit])[1].body as string).confirmToken).toBe('tok');
    });
  });

  it('does not call onClose on Escape once a job is running', async () => {
    fetchMock.mockImplementation((url: string) => {
      if (url.endsWith('/actions/preview')) return Promise.resolve(json({ request: { type: 'delete', itemId: 5 }, title: 'Movie (2019)', instanceName: 'Movies', instanceType: 'radarr', bytesFreedNow: 1_000_000_000, estimate: null, warning: null, steps: [{ description: 'Delete the file through Radarr', method: 'DELETE', path: '/y' }], confirmToken: 'tok2', expiresAt: '2026-09-10T12:10:00Z' }));
      if (url.endsWith('/actions/execute')) return Promise.resolve(json({ jobId: 42 }, 202));
      return Promise.resolve(json({}, 404));
    });
    const qc = new QueryClient();
    const onClose = vi.fn();
    render(<QueryClientProvider client={qc}><ActionDialog kind="delete" itemId={5} onClose={onClose} onDone={() => {}} /></QueryClientProvider>);
    await screen.findByText('Movie (2019)');
    const confirm = screen.getByRole('button', { name: /Delete/ });
    await waitFor(() => expect(confirm).toBeEnabled(), { timeout: 3000 });
    fireEvent.click(confirm);
    // Once the job exists (the "status" region is up), Escape must be inert - only Close, once
    // enabled by a finished job, may dismiss the dialog.
    await screen.findByRole('status');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).not.toHaveBeenCalled();
  });

  it('gates Escape and backdrop dismissal while the execute POST is in flight', async () => {
    let resolveExecute!: (r: Response) => void;
    const executePending = new Promise<Response>((resolve) => { resolveExecute = resolve; });
    fetchMock.mockImplementation((url: string) => {
      if (url.endsWith('/actions/preview')) return Promise.resolve(json({ request: { type: 'delete', itemId: 6 }, title: 'Show (2021)', instanceName: 'TV', instanceType: 'sonarr', bytesFreedNow: 2_000_000_000, estimate: null, warning: null, steps: [{ description: 'Delete the file through Sonarr', method: 'DELETE', path: '/y' }], confirmToken: 'tok3', expiresAt: '2026-09-10T12:10:00Z' }));
      if (url.endsWith('/actions/execute')) return executePending;
      return Promise.resolve(json({}, 404));
    });
    const qc = new QueryClient();
    const onClose = vi.fn();
    render(<QueryClientProvider client={qc}><ActionDialog kind="delete" itemId={6} onClose={onClose} onDone={() => {}} /></QueryClientProvider>);
    await screen.findByText('Show (2021)');
    const confirm = screen.getByRole('button', { name: /Delete/ });
    await waitFor(() => expect(confirm).toBeEnabled(), { timeout: 3000 });
    fireEvent.click(confirm);
    // No jobId yet (execute hasn't resolved), but the synchronous latch set inside confirm()
    // must already block both Escape and a backdrop click.
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).not.toHaveBeenCalled();
    const backdrop = screen.getByRole('dialog').parentElement as HTMLElement;
    fireEvent.mouseDown(backdrop, { target: backdrop });
    expect(onClose).not.toHaveBeenCalled();
    resolveExecute(json({ jobId: 43 }, 202));
    await screen.findByRole('status');
  });

  it('renders the subtitle and path when provided, to disambiguate identically titled copies', async () => {
    fetchMock.mockImplementation((url: string) => {
      if (url.endsWith('/actions/preview')) return Promise.resolve(json({ request: { type: 'delete', itemId: 8 }, title: 'Arrival (2016)', instanceName: 'Movies', instanceType: 'radarr', bytesFreedNow: 1_360_818, estimate: null, warning: null, steps: [{ description: 'Delete the file through Radarr', method: 'DELETE', path: '/y' }], confirmToken: 'tok4', expiresAt: '2026-09-10T12:10:00Z' }));
      return Promise.resolve(json({}, 404));
    });
    const qc = new QueryClient();
    render(<QueryClientProvider client={qc}>
      <ActionDialog kind="delete" itemId={8} subtitle="WEBDL-720p · 720p · H264 · 1 MB" path="/media/Arrival (2016)/Arrival.2016.720p.WEBDL.x264.mkv" onClose={() => {}} onDone={() => {}} />
    </QueryClientProvider>);
    // Rendered from props alone, independent of the async preview - visible even before
    // "Arrival (2016)" (the preview's own title, identical for both queued copies) resolves.
    expect(screen.getByText('WEBDL-720p · 720p · H264 · 1 MB')).toBeInTheDocument();
    expect(screen.getByText('/media/Arrival (2016)/Arrival.2016.720p.WEBDL.x264.mkv')).toBeInTheDocument();
    await screen.findByText('Arrival (2016)');
  });
});
