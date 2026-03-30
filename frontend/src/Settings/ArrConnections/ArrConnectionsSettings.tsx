import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import styles from './ArrConnectionsSettings.css';

interface ArrConnectionConfig {
  radarrUrl: string;
  radarrApiKey: string;
  radarrEnabled: boolean;
  sonarrUrl: string;
  sonarrApiKey: string;
  sonarrEnabled: boolean;
}

const DEFAULT_CONFIG: ArrConnectionConfig = {
  radarrUrl: 'http://localhost:7878',
  radarrApiKey: '',
  radarrEnabled: false,
  sonarrUrl: 'http://localhost:8989',
  sonarrApiKey: '',
  sonarrEnabled: false,
};

type TestStatus = 'idle' | 'testing' | 'success' | 'failure';

interface SectionTestState {
  status: TestStatus;
  message: string;
}

function ArrConnectionsSettings() {
  const [config, setConfig] = useState<ArrConnectionConfig>(DEFAULT_CONFIG);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [showRadarrApiKey, setShowRadarrApiKey] = useState(false);
  const [showSonarrApiKey, setShowSonarrApiKey] = useState(false);
  const [radarrTest, setRadarrTest] = useState<SectionTestState>({
    status: 'idle',
    message: '',
  });
  const [sonarrTest, setSonarrTest] = useState<SectionTestState>({
    status: 'idle',
    message: '',
  });

  useEffect(() => {
    fetch('/api/v3/config/arrconnection')
      .then((res) => res.json())
      .then((data: ArrConnectionConfig) => {
        setConfig(data);
        setIsLoading(false);
      })
      .catch(() => {
        // Use defaults if the endpoint is not available yet
        setIsLoading(false);
      });
  }, []);

  const handleChange = useCallback(
    (field: keyof ArrConnectionConfig, value: string | boolean) => {
      setConfig((prev) => ({ ...prev, [field]: value }));
    },
    []
  );

  const handleTestConnection = useCallback(
    (type: 'radarr' | 'sonarr') => {
      const setTest = type === 'radarr' ? setRadarrTest : setSonarrTest;

      setTest({ status: 'testing', message: 'Testing connection...' });

      const body =
        type === 'radarr'
          ? {
              type: 'radarr',
              url: config.radarrUrl,
              apiKey: config.radarrApiKey,
            }
          : {
              type: 'sonarr',
              url: config.sonarrUrl,
              apiKey: config.sonarrApiKey,
            };

      fetch('/api/v3/config/arrconnection/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
        .then((res) => {
          if (res.ok) {
            setTest({ status: 'success', message: 'Connection successful!' });
          } else {
            res.text().then((text) => {
              setTest({
                status: 'failure',
                message: text || 'Connection failed.',
              });
            });
          }
        })
        .catch((err: Error) => {
          setTest({
            status: 'failure',
            message: err.message || 'Connection failed.',
          });
        });
    },
    [config]
  );

  const handleSave = useCallback(() => {
    setIsSaving(true);

    fetch('/api/v3/config/arrconnection', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    })
      .then(() => {
        setIsSaving(false);
      })
      .catch(() => {
        setIsSaving(false);
      });
  }, [config]);

  if (isLoading) {
    return (
      <PageContent title="Arr Connections">
        <PageContentBody>
          <div className={styles.loading}>Loading settings...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  return (
    <PageContent title="Arr Connections">
      <PageContentBody>
        {/* Radarr Section */}
        <div className={styles.section}>
          <div className={styles.sectionHeader}>Radarr</div>

          <div className={styles.formGroup}>
            <div className={styles.label}>URL</div>
            <div className={styles.inputWrapper}>
              <input
                className={styles.textInput}
                type="text"
                value={config.radarrUrl}
                placeholder="http://localhost:7878"
                onChange={(e) => handleChange('radarrUrl', e.target.value)}
              />
            </div>
          </div>

          <div className={styles.formGroup}>
            <div className={styles.label}>API Key</div>
            <div className={styles.inputWrapper}>
              <div className={styles.passwordWrapper}>
                <input
                  className={styles.textInput}
                  type={showRadarrApiKey ? 'text' : 'password'}
                  value={config.radarrApiKey}
                  placeholder="Enter Radarr API key"
                  onChange={(e) =>
                    handleChange('radarrApiKey', e.target.value)
                  }
                />
                <button
                  className={styles.togglePasswordButton}
                  type="button"
                  onClick={() => setShowRadarrApiKey((v) => !v)}
                >
                  {showRadarrApiKey ? 'Hide' : 'Show'}
                </button>
              </div>
            </div>
          </div>

          <div className={styles.formGroup}>
            <div className={styles.label}>Enabled</div>
            <div className={styles.inputWrapper}>
              <label className={styles.checkboxLabel}>
                <input
                  type="checkbox"
                  checked={config.radarrEnabled}
                  onChange={(e) =>
                    handleChange('radarrEnabled', e.target.checked)
                  }
                />
                Enable Radarr integration
              </label>
            </div>
          </div>

          <div className={styles.buttonRow}>
            <button
              className={styles.testButton}
              type="button"
              disabled={radarrTest.status === 'testing'}
              onClick={() => handleTestConnection('radarr')}
            >
              {radarrTest.status === 'testing'
                ? 'Testing...'
                : 'Test Connection'}
            </button>
          </div>

          {radarrTest.status === 'success' && (
            <div className={styles.testSuccess}>{radarrTest.message}</div>
          )}
          {radarrTest.status === 'failure' && (
            <div className={styles.testFailure}>{radarrTest.message}</div>
          )}
        </div>

        {/* Sonarr Section */}
        <div className={styles.section}>
          <div className={styles.sectionHeader}>Sonarr</div>

          <div className={styles.formGroup}>
            <div className={styles.label}>URL</div>
            <div className={styles.inputWrapper}>
              <input
                className={styles.textInput}
                type="text"
                value={config.sonarrUrl}
                placeholder="http://localhost:8989"
                onChange={(e) => handleChange('sonarrUrl', e.target.value)}
              />
            </div>
          </div>

          <div className={styles.formGroup}>
            <div className={styles.label}>API Key</div>
            <div className={styles.inputWrapper}>
              <div className={styles.passwordWrapper}>
                <input
                  className={styles.textInput}
                  type={showSonarrApiKey ? 'text' : 'password'}
                  value={config.sonarrApiKey}
                  placeholder="Enter Sonarr API key"
                  onChange={(e) =>
                    handleChange('sonarrApiKey', e.target.value)
                  }
                />
                <button
                  className={styles.togglePasswordButton}
                  type="button"
                  onClick={() => setShowSonarrApiKey((v) => !v)}
                >
                  {showSonarrApiKey ? 'Hide' : 'Show'}
                </button>
              </div>
            </div>
          </div>

          <div className={styles.formGroup}>
            <div className={styles.label}>Enabled</div>
            <div className={styles.inputWrapper}>
              <label className={styles.checkboxLabel}>
                <input
                  type="checkbox"
                  checked={config.sonarrEnabled}
                  onChange={(e) =>
                    handleChange('sonarrEnabled', e.target.checked)
                  }
                />
                Enable Sonarr integration
              </label>
            </div>
          </div>

          <div className={styles.buttonRow}>
            <button
              className={styles.testButton}
              type="button"
              disabled={sonarrTest.status === 'testing'}
              onClick={() => handleTestConnection('sonarr')}
            >
              {sonarrTest.status === 'testing'
                ? 'Testing...'
                : 'Test Connection'}
            </button>
          </div>

          {sonarrTest.status === 'success' && (
            <div className={styles.testSuccess}>{sonarrTest.message}</div>
          )}
          {sonarrTest.status === 'failure' && (
            <div className={styles.testFailure}>{sonarrTest.message}</div>
          )}
        </div>

        {/* Global Save */}
        <div className={styles.buttonRow}>
          <button
            className={styles.saveButton}
            type="button"
            disabled={isSaving}
            onClick={handleSave}
          >
            {isSaving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default ArrConnectionsSettings;
