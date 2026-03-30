import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { formatSize } from 'Library/LibraryItem';
import styles from './RecommendationsPage.css';

interface Recommendation {
  id: number;
  title: string;
  currentSize: number;
  estimatedSize: number;
  savings: number;
  currentQuality: string;
  suggestedQuality: string;
  source: 'radarr' | 'sonarr';
}

// Mock data for development
const MOCK_RECOMMENDATIONS: Recommendation[] = [
  {
    id: 1,
    title: 'The Matrix',
    currentSize: 84_200_000_000,
    estimatedSize: 42_200_000_000,
    savings: 42_000_000_000,
    currentQuality: 'Bluray-2160p',
    suggestedQuality: 'Bluray-1080p',
    source: 'radarr',
  },
  {
    id: 3,
    title: 'Inception',
    currentSize: 78_400_000_000,
    estimatedSize: 23_400_000_000,
    savings: 55_000_000_000,
    currentQuality: 'Remux-2160p',
    suggestedQuality: 'Bluray-2160p',
    source: 'radarr',
  },
  {
    id: 7,
    title: 'Interstellar',
    currentSize: 85_600_000_000,
    estimatedSize: 25_600_000_000,
    savings: 60_000_000_000,
    currentQuality: 'Remux-2160p',
    suggestedQuality: 'Bluray-2160p',
    source: 'radarr',
  },
];

function RecommendationsPage() {
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // GET /api/v3/recommendation
    fetch('/api/v3/recommendation')
      .then((res) => res.json())
      .then((data) => {
        setRecommendations(data);
        setIsLoading(false);
      })
      .catch(() => {
        // Fall back to mock data during development
        setRecommendations(MOCK_RECOMMENDATIONS);
        setIsLoading(false);
      });
  }, []);

  const handleDismiss = useCallback((id: number) => {
    setRecommendations((prev) => prev.filter((r) => r.id !== id));
  }, []);

  const handleDismissAll = useCallback(() => {
    setRecommendations([]);
  }, []);

  const handleSwap = useCallback((recommendation: Recommendation) => {
    // POST /api/v3/action/qualityswap
    fetch('/api/v3/action/qualityswap', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: recommendation.id,
        targetQuality: recommendation.suggestedQuality,
      }),
    })
      .then(() => {
        setRecommendations((prev) =>
          prev.filter((r) => r.id !== recommendation.id)
        );
      })
      .catch(() => {
        // Error handling would go here
      });
  }, []);

  if (isLoading) {
    return (
      <PageContent title="Recommendations">
        <PageContentBody>
          <div className={styles.loading}>Loading recommendations...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (recommendations.length === 0) {
    return (
      <PageContent title="Recommendations">
        <PageContentBody>
          <div className={styles.emptyState}>
            <div>No recommendations</div>
            <div className={styles.emptyHint}>
              Run a scan to generate suggestions.
            </div>
          </div>
        </PageContentBody>
      </PageContent>
    );
  }

  const totalSavings = recommendations.reduce((sum, r) => sum + r.savings, 0);

  return (
    <PageContent title="Recommendations">
      <PageContentBody>
        <div className={styles.toolbar}>
          <div className={styles.toolbarTitle}>
            {recommendations.length} recommendation
            {recommendations.length !== 1 ? 's' : ''} - potential savings:{' '}
            {formatSize(totalSavings)}
          </div>

          <button
            className={styles.dismissAllButton}
            onClick={handleDismissAll}
          >
            Dismiss All
          </button>
        </div>

        <div className={styles.cards}>
          {recommendations.map((rec) => (
            <div key={rec.id} className={styles.card}>
              <div className={styles.cardTitle}>{rec.title}</div>

              <div className={styles.suggestedQuality}>
                <span className={styles.qualityValue}>
                  {rec.currentQuality}
                </span>
                {' '}
                &rarr;{' '}
                <span className={styles.qualityValue}>
                  {rec.suggestedQuality}
                </span>
              </div>

              <div className={styles.sizeRow}>
                <span className={styles.currentSize}>
                  {formatSize(rec.currentSize)}
                </span>
                <span className={styles.arrow}>&rarr;</span>
                <span className={styles.estimatedSize}>
                  {formatSize(rec.estimatedSize)}
                </span>
              </div>

              <div className={styles.savings}>
                Save {formatSize(rec.savings)}
              </div>

              <div className={styles.cardActions}>
                <button
                  className={styles.swapButton}
                  onClick={() => handleSwap(rec)}
                >
                  Swap
                </button>

                <button
                  className={styles.dismissButton}
                  onClick={() => handleDismiss(rec.id)}
                >
                  Dismiss
                </button>
              </div>
            </div>
          ))}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default RecommendationsPage;
