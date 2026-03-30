import React from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';
import SettingsToolbar from './SettingsToolbar';
import styles from './Settings.css';

function Settings() {
  return (
    <PageContent title={translate('Settings')}>
      <SettingsToolbar hasPendingChanges={false} />

      <PageContentBody>
        <Link className={styles.link} to="/settings/connections">
          Connections
        </Link>

        <div className={styles.summary}>
          Configure Radarr and Sonarr connections
        </div>

        <Link className={styles.link} to="/settings/connect">
          {translate('Connect')}
        </Link>

        <div className={styles.summary}>
          {translate('ConnectSettingsSummary')}
        </div>

        <Link className={styles.link} to="/settings/rules">
          Rules
        </Link>

        <div className={styles.summary}>
          Configure automated rules for space management, quality enforcement, and file cleanup.
        </div>

        <Link className={styles.link} to="/settings/tags">
          {translate('Tags')}
        </Link>

        <div className={styles.summary}>{translate('TagsSettingsSummary')}</div>

        <Link className={styles.link} to="/settings/general">
          {translate('General')}
        </Link>

        <div className={styles.summary}>
          {translate('GeneralSettingsSummary')}
        </div>

        <Link className={styles.link} to="/settings/ui">
          {translate('Ui')}
        </Link>

        <div className={styles.summary}>{translate('UiSettingsSummary')}</div>
      </PageContentBody>
    </PageContent>
  );
}

export default Settings;
