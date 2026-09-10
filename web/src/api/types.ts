export type ArrType = 'radarr' | 'sonarr';
export type MediaKind = 'movie' | 'episode';
export type JobType = 'scan' | 'enrich' | 'action';
export type JobStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';

export interface SystemStatus { version: string; setupComplete: boolean; tools: { ffprobe: boolean; mediainfo: boolean } }
export interface Me { username: string; apiKey: string }
export interface AppSettings { scanIntervalHours: number; extensions: string[]; ffprobePath: string | null; mediainfoPath: string | null; heatMode: 'relative' | 'absolute'; theme: 'dark' | 'light' }
export interface Mapping { id: number; remotePrefix: string; localPrefix: string }
export interface Instance { id: number; type: ArrType; name: string; baseUrl: string; enabled: boolean; apiKeySet: boolean; lastSyncAt: string | null; lastSyncError: string | null; mappings: Mapping[]; matched: number; unmatched: number }
export interface Profile { id: number; name: string }
export interface TestResult { ok: boolean; error: string | null; version: string | null; appName: string | null; rootFolders: string[]; profiles: Profile[] }
export interface MappingSuggestion { remotePrefix: string; localPrefix: string; confidence: 'high' | 'low' }
export interface RootFolder { id: number; path: string; enabled: boolean; lastScanAt: string | null; exists: boolean }
export interface ValidatePath { exists: boolean; sampleFiles: string[]; mediaFileCountSample: number }
export interface JobSummary { filesSeen: number; filesProbed: number; filesAdded: number; filesRemoved: number; itemsMatched: number; itemsUnmatched: number; errors: string[] }
export interface ProgressEvent { jobId: number; type: JobType; kind: 'progress' | 'finished'; phase: string | null; done: number; total: number; detail: string | null; status: JobStatus | null }
export interface Job { id: number; type: JobType; status: JobStatus; trigger: 'manual' | 'scheduled'; queuedAt: string; startedAt: string | null; finishedAt: string | null; summary: JobSummary | null; error: string | null; progress: ProgressEvent | null }
export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }
export interface LibraryItem {
  itemId: number; instanceId: number; instanceName: string; instanceType: ArrType; kind: MediaKind; title: string; year: number | null;
  seriesId: number | null; seriesTitle: string | null; seasonNumber: number | null; episodes: string | null;
  qualityProfileName: string | null; qualityProfileId: number | null; qualityName: string | null; monitored: boolean; tags: string | null; posterUrl: string | null;
  fileId: number; path: string; sizeBytes: number; durationSeconds: number | null; width: number | null; height: number | null; frameRate: number | null;
  videoCodec: string | null; bitDepth: number | null; hdrFormat: string | null; videoBitrateBps: number | null; overallBitrateBps: number | null; audioSummary: string | null; probeError: string | null;
  nbpp: number | null; resolution: string | null; tmdbId: number | null; tvdbId: number | null; heat: number; color: string;
}
export interface TreeLeaf { itemId: number; fileId: number; heat: number; color: string; posterUrl: string | null; quality: string | null; codec: string | null; resolution: string | null; instanceId: number; instanceName: string }
export interface TreeNode { name: string; bytes: number; children: TreeNode[] | null; leaf: TreeLeaf | null }
export interface Bucket { name: string; bytes: number; count: number }
export interface LibraryStats { totalBytes: number; fileCount: number; itemCount: number; unmatchedFileCount: number; unreadableFileCount: number; byInstance: (Bucket & { id: number; type: ArrType })[]; byQuality: Bucket[]; byCodec: Bucket[]; byResolution: Bucket[]; heatHistogram: number[]; largest: LibraryItem[]; hottest: LibraryItem[] }
export interface SavingsEstimate { estimatedBytes: number; savingsBytes: number; samples: number; basis: 'library' | 'table' | 'unknown' }
export interface ProfileEstimate { id: number; name: string; estimate: SavingsEstimate }
export interface LibraryDetail { item: LibraryItem; profiles: ProfileEstimate[] }
export interface DuplicateGroup { key: string; title: string; members: LibraryItem[]; wastedBytes: number; keepLargestId: number; keepSmallestId: number }
export interface ActionRequest { type: 'delete' | 'replace'; itemId: number; targetProfileId?: number | null; unmonitor?: boolean; confirmToken?: string | null }
export interface ActionStep { description: string; method: string; path: string }
export interface ActionPreview { request: ActionRequest; title: string; instanceName: string; instanceType: ArrType; bytesFreedNow: number; estimate: SavingsEstimate | null; warning: string | null; steps: ActionStep[]; confirmToken: string; expiresAt: string }
export interface ActionLogEntry { id: number; at: string; type: 'delete' | 'replace'; mediaItemId: number | null; arrInstanceId: number; title: string; path: string | null; sizeBytesBefore: number; qualityBefore: string | null; qualityAfter: string | null; outcome: 'succeeded' | 'failed'; detail: string | null }
export interface LibraryParams { instanceId?: number; kind?: MediaKind; minBytes?: number; search?: string; sort?: 'size' | 'heat' | 'title' | 'quality'; order?: 'asc' | 'desc'; page?: number; pageSize?: number; heatMode?: 'relative' | 'absolute' }
export interface TreeParams { instanceId?: number; kind?: MediaKind; minBytes?: number; colorBy?: 'heat' | 'quality' | 'codec' | 'resolution' | 'duplicates' | 'instance'; heatMode?: 'relative' | 'absolute' }
