import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import RootFolderList from '../RootFolderList';

function wrap(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

describe('RootFolderList', () => {
  const fetchMock = vi.fn();
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); });

  it('shows the server error verbatim when Check is called with a relative path, and keeps Add folder disabled', async () => {
    fetchMock.mockImplementation((path: string) => {
      if (path.includes('/roots/validate')) {
        return Promise.resolve(jsonResponse({ error: 'Enter an absolute path, e.g. /media/movies.' }, 400));
      }
      if (path.includes('/roots')) return Promise.resolve(jsonResponse([]));
      return Promise.resolve(new Response('', { status: 404 }));
    });

    wrap(<RootFolderList />);
    fireEvent.change(screen.getByLabelText('Library folder as Spacearr sees it'), { target: { value: 'media/movies' } });
    fireEvent.click(screen.getByRole('button', { name: 'Check' }));

    await screen.findByText('Enter an absolute path, e.g. /media/movies.');
    expect(screen.getByRole('button', { name: 'Add folder' })).toBeDisabled();
  });
});
