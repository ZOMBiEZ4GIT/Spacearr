import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import { stopEvents } from './events';
import type * as T from './types';

const qs = (p: object) => {
  const s = new URLSearchParams();
  for (const [k, v] of Object.entries(p)) if (v !== undefined && v !== null && v !== '') s.set(k, String(v));
  const str = s.toString();
  return str ? `?${str}` : '';
};

export const useStatus = () => useQuery({ queryKey: ['status'], queryFn: () => api.get<T.SystemStatus>('/api/v1/system/status') });
export const useMe = () => useQuery({ queryKey: ['me'], queryFn: () => api.get<T.Me>('/api/v1/auth/me'), retry: false });
export const useSettings = () => useQuery({ queryKey: ['settings'], queryFn: () => api.get<T.AppSettings>('/api/v1/settings') });
export const useInstances = () => useQuery({ queryKey: ['instances'], queryFn: () => api.get<T.Instance[]>('/api/v1/instances') });
export const useProfiles = (id?: number) => useQuery({ queryKey: ['profiles', id], queryFn: () => api.get<T.Profile[]>(`/api/v1/instances/${id}/profiles`), enabled: !!id });
export const useRoots = () => useQuery({ queryKey: ['roots'], queryFn: () => api.get<T.RootFolder[]>('/api/v1/roots') });
export const useJobs = (page = 1) => useQuery({ queryKey: ['jobs', page], queryFn: () => api.get<T.Page<T.Job>>(`/api/v1/jobs?page=${page}&pageSize=25`) });

// A job's outcome must never depend solely on an uninterrupted SSE connection: /api/v1/events
// has no replay, so a dropped connection (proxy idle timeout, sleeping laptop, a throttled
// background tab) can lose the `finished` event for good. Polling this alongside the SSE store
// gives every consumer a second, connection-independent way to learn a job reached a terminal
// status. Stops polling once the job is terminal so a finished job's dialog/pill don't keep
// hitting the API for ever.
const TERMINAL_JOB_STATUSES: readonly T.JobStatus[] = ['succeeded', 'failed', 'cancelled'];
export const isTerminalJobStatus = (status: T.JobStatus | null | undefined): boolean =>
  status != null && (TERMINAL_JOB_STATUSES as readonly string[]).includes(status);
export const useJob = (id: number | null) => useQuery({
  queryKey: ['job', id],
  queryFn: () => api.get<T.Job>(`/api/v1/jobs/${id}`),
  enabled: id != null,
  refetchInterval: (query) => (isTerminalJobStatus(query.state.data?.status) ? false : 2000),
  // The 2s refetchInterval above is already this query's own retry loop; TanStack's default
  // exponential-backoff retry on top of that would just add unpredictable extra delay before a
  // consumer (ActionDialog's 30s no-event fallback) can observe `isError`.
  retry: false,
});

export const useLibrary = (p: T.LibraryParams) => useQuery({ queryKey: ['library', p], queryFn: () => api.get<T.Page<T.LibraryItem>>(`/api/v1/library${qs(p)}`), placeholderData: (prev) => prev });

// The backend clamps pageSize to 1000 (LibraryEndpoints.cs), so requesting an ever-larger single
// page (the old `pageSize: 500 * pages` approach) silently stalls past 1000 rows: the third
// "Load more" click asks for 1500, gets 1000 back, and the button never grows the table again.
// A fixed page size with a real page number, accumulated here, has no such ceiling.
export const LIBRARY_PAGE_SIZE = 500;
/** True when the accumulated pages don't yet cover every row the server reports. Exported (rather
 * than inlined in getNextPageParam) so it has a plain-data unit test independent of react-query. */
export function libraryHasMore(pages: T.Page<T.LibraryItem>[]): boolean {
  if (pages.length === 0) return false;
  const last = pages[pages.length - 1];
  const loaded = pages.reduce((n, pg) => n + pg.items.length, 0);
  // A short page (fewer rows than requested) is always the last one, even if `total` disagrees
  // (e.g. it changed between requests) - never ask for a page after that.
  return last.items.length >= LIBRARY_PAGE_SIZE && loaded < last.total;
}
export const useLibraryPages = (p: Omit<T.LibraryParams, 'page' | 'pageSize'>) => useInfiniteQuery({
  queryKey: ['library', 'pages', p],
  queryFn: ({ pageParam }) => api.get<T.Page<T.LibraryItem>>(`/api/v1/library${qs({ ...p, page: pageParam, pageSize: LIBRARY_PAGE_SIZE })}`),
  initialPageParam: 1,
  getNextPageParam: (_last, allPages) => (libraryHasMore(allPages) ? allPages.length + 1 : undefined),
  placeholderData: (prev) => prev,
});
export const useTree = (p: T.TreeParams) => useQuery({ queryKey: ['tree', p], queryFn: () => api.get<T.TreeNode>(`/api/v1/library/tree${qs(p)}`), placeholderData: (prev) => prev });
export const useStats = (p: Pick<T.LibraryParams, 'instanceId' | 'kind' | 'heatMode'>) => useQuery({ queryKey: ['stats', p], queryFn: () => api.get<T.LibraryStats>(`/api/v1/library/stats${qs(p)}`), placeholderData: (prev) => prev });
export const useItem = (id?: number) => useQuery({ queryKey: ['item', id], queryFn: () => api.get<T.LibraryDetail>(`/api/v1/library/${id}`), enabled: !!id });
export const useDuplicates = (p: Pick<T.LibraryParams, 'instanceId' | 'kind'>) => useQuery({ queryKey: ['duplicates', p], queryFn: () => api.get<T.DuplicateGroup[]>(`/api/v1/duplicates${qs(p)}`) });
export const useActionLog = (page = 1) => useQuery({ queryKey: ['actionLog', page], queryFn: () => api.get<T.Page<T.ActionLogEntry>>(`/api/v1/actions/log?page=${page}&pageSize=50`) });

function useInvalidating<TArgs = void, TResult = void>(fn: (a: TArgs) => Promise<TResult>, keys: string[]) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => keys.forEach((k) => qc.invalidateQueries({ queryKey: [k] })) });
}
export const useLogin = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: { username: string; password: string }) => api.post('/api/v1/auth/login', b),
    onSuccess: () => {
      // Drop the cached error first so a successful login never renders a stale
      // isError from the pre-login /auth/me 401 while the refetch is in flight.
      qc.removeQueries({ queryKey: ['me'] });
      qc.invalidateQueries({ queryKey: ['me'] });
      qc.invalidateQueries({ queryKey: ['status'] });
    },
  });
};
export const useSetup = () => useInvalidating((b: { username: string; password: string }) => api.post<T.Me>('/api/v1/setup', b), ['status']);
export const useLogout = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/api/v1/auth/logout'),
    onSuccess: () => {
      stopEvents();
      qc.removeQueries({ queryKey: ['me'] });
    },
  });
};
export const useSaveSettings = () => useInvalidating((s: T.AppSettings) => api.put('/api/v1/settings', s), ['settings', 'status']);
export const useTestConnection = () => useMutation({ mutationFn: (b: { type: T.ArrType; baseUrl: string; apiKey: string }) => api.post<T.TestResult>('/api/v1/instances/test', b) });
export const useTestInstance = () => useMutation({ mutationFn: (id: number) => api.post<T.TestResult>(`/api/v1/instances/${id}/test`) });
export const useCreateInstance = () => useInvalidating((b: { type: T.ArrType; name: string; baseUrl: string; apiKey: string }) => api.post<T.Instance>('/api/v1/instances', b), ['instances']);
export const useUpdateInstance = () => useInvalidating(({ id, ...b }: { id: number; type: T.ArrType; name: string; baseUrl: string; enabled: boolean; apiKey: string }) => api.put(`/api/v1/instances/${id}`, b), ['instances']);
export const useDeleteInstance = () => useInvalidating((id: number) => api.del(`/api/v1/instances/${id}`), ['instances', 'library', 'tree', 'stats']);
export const useAddMapping = () => useInvalidating(({ id, ...b }: { id: number; remotePrefix: string; localPrefix: string }) => api.post<T.Mapping>(`/api/v1/instances/${id}/mappings`, b), ['instances']);
export const useDeleteMapping = () => useInvalidating(({ id, mappingId }: { id: number; mappingId: number }) => api.del(`/api/v1/instances/${id}/mappings/${mappingId}`), ['instances']);
export const useSuggestMappings = () => useMutation({ mutationFn: (id: number) => api.post<T.MappingSuggestion[]>(`/api/v1/instances/${id}/mappings/suggest`) });
export const useValidatePath = () => useMutation({ mutationFn: (path: string) => api.post<T.ValidatePath>('/api/v1/roots/validate', { path }) });
export const useAddRoot = () => useInvalidating((path: string) => api.post<T.RootFolder>('/api/v1/roots', { path }), ['roots']);
export const useUpdateRoot = () => useInvalidating(({ id, ...b }: { id: number; path: string; enabled: boolean }) => api.put(`/api/v1/roots/${id}`, b), ['roots']);
export const useDeleteRoot = () => useInvalidating((id: number) => api.del(`/api/v1/roots/${id}`), ['roots', 'library', 'tree', 'stats']);
export const useStartScan = () => useInvalidating(() => api.post<{ jobId: number }>('/api/v1/jobs/scan'), ['jobs']);
export const useStartEnrich = () => useInvalidating(() => api.post<{ jobId: number }>('/api/v1/jobs/enrich'), ['jobs']);
export const useCancelJob = () => useInvalidating((id: number) => api.post(`/api/v1/jobs/${id}/cancel`), ['jobs']);
export const usePreviewAction = () => useMutation({ mutationFn: (r: T.ActionRequest) => api.post<T.ActionPreview>('/api/v1/actions/preview', r) });
export const useExecuteAction = () => useInvalidating((r: T.ActionRequest) => api.post<{ jobId: number }>('/api/v1/actions/execute', r), ['jobs']);
export const useRegenerateApiKey = () => useInvalidating(() => api.post<{ apiKey: string }>('/api/v1/auth/apikey/regenerate'), ['me']);
export const useChangePassword = () => useMutation({ mutationFn: (b: { currentPassword: string; newPassword: string }) => api.post('/api/v1/auth/password', b) });
