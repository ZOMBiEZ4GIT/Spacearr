import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ConnectionForm from '../ConnectionForm';

function wrap(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ConnectionForm', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); });

  it('disables Save until a test succeeds and shows server errors verbatim', async () => {
    wrap(<ConnectionForm onSaved={() => {}} />);
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Movies' } });
    fireEvent.change(screen.getByLabelText('URL'), { target: { value: 'http://radarr:7878' } });
    fireEvent.change(screen.getByLabelText('API key'), { target: { value: 'k' } });
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();

    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ ok: false, error: 'Unauthorized: check the API key', rootFolders: [], profiles: [] }), { status: 200, headers: { 'content-type': 'application/json' } }));
    fireEvent.click(screen.getByRole('button', { name: 'Test' }));
    await screen.findByText('Unauthorized: check the API key');
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();

    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ ok: true, error: null, version: '6.3.0', appName: 'Radarr', rootFolders: ['/data/movies'], profiles: [{ id: 1, name: 'Any' }] }), { status: 200, headers: { 'content-type': 'application/json' } }));
    fireEvent.click(screen.getByRole('button', { name: 'Test' }));
    await screen.findByText(/Radarr 6\.3\.0/);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled());
  });
});
