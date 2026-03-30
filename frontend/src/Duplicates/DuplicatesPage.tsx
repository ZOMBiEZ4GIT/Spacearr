import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { formatSize, formatBitrate } from 'Library/LibraryItem';
import styles from './DuplicatesPage.css';

interface DuplicateFile {
  id: number;
  path: string;
  size: number;
  quality: string;
  bitrate: number;
  codec: string;
}

interface DuplicateGroup {
  title: string;
  files: DuplicateFile[];
}

// Mock data for development
const MOCK_DUPLICATES: DuplicateGroup[] = [
  {
    title: 'The Matrix (1999)',
    files: [
      {
        id: 1,
        path: '/movies/The Matrix (1999)/The.Matrix.1999.2160p.UHD.BluRay.x265.mkv',
        size: 84_200_000_000,
        quality: 'Bluray-2160p',
        bitrate: 45_000,
        codec: 'x265',
      },
      {
        id: 10,
        path: '/movies/The Matrix (1999)/The.Matrix.1999.1080p.BluRay.x264.mkv',
        size: 12_400_000_000,
        quality: 'Bluray-1080p',
        bitrate: 12_000,
        codec: 'x264',
      },
    ],
  },
  {
    title: 'Breaking Bad - S01E01',
    files: [
      {
        id: 2,
        path: '/tv/Breaking Bad/Season 01/Breaking.Bad.S01E01.1080p.BluRay.x264.mkv',
        size: 5_200_000_000,
        quality: 'Bluray-1080p',
        bitrate: 8_500,
        codec: 'x264',
      },
      {
        id: 11,
        path: '/tv/Breaking Bad/Season 01/Breaking.Bad.S01E01.720p.WEB-DL.x264.mkv',
        size: 1_800_000_000,
        quality: 'WEBDL-720p',
        bitrate: 3_200,
        codec: 'x264',
      },
    ],
  },
];

function findSmallest(files: DuplicateFile[]): number {
  let smallest = files[0];

  for (const file of files) {
    if (file.size < smallest.size) {
      smallest = file;
    }
  }

  return smallest.id;
}

function findBestQuality(files: DuplicateFile[]): number {
  let best = files[0];

  for (const file of files) {
    if (file.bitrate > best.bitrate) {
      best = file;
    }
  }

  return best.id;
}

function DuplicatesPage() {
  const [groups, setGroups] = useState<DuplicateGroup[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [selectedFiles, setSelectedFiles] = useState<Record<string, number>>(
    {}
  );

  useEffect(() => {
    // GET /api/v3/duplicate
    fetch('/api/v3/duplicate')
      .then((res) => res.json())
      .then((data) => {
        setGroups(data);
        setIsLoading(false);

        // Expand all groups by default
        const expanded = new Set<string>();
        data.forEach((g: DuplicateGroup) => expanded.add(g.title));
        setExpandedGroups(expanded);
      })
      .catch(() => {
        // Fall back to mock data during development
        setGroups(MOCK_DUPLICATES);
        setIsLoading(false);

        const expanded = new Set<string>();
        MOCK_DUPLICATES.forEach((g) => expanded.add(g.title));
        setExpandedGroups(expanded);
      });
  }, []);

  const toggleGroup = useCallback((title: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);

      if (next.has(title)) {
        next.delete(title);
      } else {
        next.add(title);
      }

      return next;
    });
  }, []);

  const handleSelectFile = useCallback(
    (groupTitle: string, fileId: number) => {
      setSelectedFiles((prev) => ({
        ...prev,
        [groupTitle]: fileId,
      }));
    },
    []
  );

  const handleKeepBest = useCallback(
    (group: DuplicateGroup) => {
      const bestId = findBestQuality(group.files);
      setSelectedFiles((prev) => ({
        ...prev,
        [group.title]: bestId,
      }));
    },
    []
  );

  const handleKeepSmallest = useCallback(
    (group: DuplicateGroup) => {
      const smallestId = findSmallest(group.files);
      setSelectedFiles((prev) => ({
        ...prev,
        [group.title]: smallestId,
      }));
    },
    []
  );

  if (isLoading) {
    return (
      <PageContent title="Duplicates">
        <PageContentBody>
          <div className={styles.loading}>Loading duplicates...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (groups.length === 0) {
    return (
      <PageContent title="Duplicates">
        <PageContentBody>
          <div className={styles.emptyState}>No duplicates found.</div>
        </PageContentBody>
      </PageContent>
    );
  }

  return (
    <PageContent title="Duplicates">
      <PageContentBody>
        <div className={styles.groups}>
          {groups.map((group) => {
            const isExpanded = expandedGroups.has(group.title);
            const smallestId = findSmallest(group.files);
            const bestId = findBestQuality(group.files);

            return (
              <div key={group.title} className={styles.group}>
                <div
                  className={styles.groupHeader}
                  onClick={() => toggleGroup(group.title)}
                >
                  <div>
                    <span className={styles.groupTitle}>{group.title}</span>
                    <span className={styles.groupCount}>
                      {group.files.length}
                    </span>
                  </div>

                  <div className={styles.groupActions}>
                    <button
                      className={styles.keepBestButton}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleKeepBest(group);
                      }}
                    >
                      Keep Best
                    </button>

                    <button
                      className={styles.keepSmallestButton}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleKeepSmallest(group);
                      }}
                    >
                      Keep Smallest
                    </button>

                    <span
                      className={
                        isExpanded
                          ? styles.expandIconOpen
                          : styles.expandIcon
                      }
                    >
                      &#9660;
                    </span>
                  </div>
                </div>

                {isExpanded && (
                  <div className={styles.rows}>
                    <div className={styles.rowHeader}>
                      <div />
                      <div>Path</div>
                      <div style={{ textAlign: 'right' }}>Size</div>
                      <div>Quality</div>
                      <div style={{ textAlign: 'right' }}>Bitrate</div>
                      <div>Codec</div>
                    </div>

                    {group.files.map((file) => (
                      <div key={file.id} className={styles.row}>
                        <div className={styles.rowRadio}>
                          <input
                            type="radio"
                            name={`keep-${group.title}`}
                            checked={
                              selectedFiles[group.title] === file.id
                            }
                            onChange={() =>
                              handleSelectFile(group.title, file.id)
                            }
                          />
                        </div>

                        <div className={styles.rowPath}>{file.path}</div>

                        <div className={styles.rowSize}>
                          {formatSize(file.size)}
                          {file.id === smallestId && (
                            <span className={styles.smallestTag}>
                              Smallest
                            </span>
                          )}
                        </div>

                        <div className={styles.rowQuality}>
                          {file.quality}
                          {file.id === bestId && (
                            <span className={styles.bestTag}>Best</span>
                          )}
                        </div>

                        <div className={styles.rowBitrate}>
                          {formatBitrate(file.bitrate)}
                        </div>

                        <div className={styles.rowCodec}>{file.codec}</div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default DuplicatesPage;
