import React from 'react';
import { Redirect, Route } from 'react-router-dom';
import NotFound from 'Components/NotFound';
import Switch from 'Components/Router/Switch';
import DuplicatesPage from 'Duplicates/DuplicatesPage';
import SpacearrHistoryPage from 'History/SpacearrHistoryPage';
import LibraryPage from 'Library/LibraryPage';
import RecommendationsPage from 'Recommendations/RecommendationsPage';
import GeneralSettingsConnector from 'Settings/General/GeneralSettingsConnector';
import NotificationSettings from 'Settings/Notifications/NotificationSettings';
import Settings from 'Settings/Settings';
import TagSettings from 'Settings/Tags/TagSettings';
import UISettingsConnector from 'Settings/UI/UISettingsConnector';
import BackupsConnector from 'System/Backup/BackupsConnector';
import LogsTableConnector from 'System/Events/LogsTableConnector';
import Logs from 'System/Logs/Logs';
import Status from 'System/Status/Status';
import Tasks from 'System/Tasks/Tasks';
import Updates from 'System/Updates/Updates';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

function RedirectWithUrlBase() {
  return <Redirect to={getPathWithUrlBase('/')} />;
}

function AppRoutes() {
  return (
    <Switch>
      {/*
        Library (default)
      */}

      <Route exact={true} path="/" component={LibraryPage} />

      {window.Spacearr.urlBase && (
        <Route
          exact={true}
          path="/"
          // eslint-disable-next-line @typescript-eslint/ban-ts-comment
          // @ts-ignore
          addUrlBase={false}
          render={RedirectWithUrlBase}
        />
      )}

      {/*
        Spacearr Pages
      */}

      <Route path="/recommendations" component={RecommendationsPage} />

      <Route path="/duplicates" component={DuplicatesPage} />

      <Route path="/history" component={SpacearrHistoryPage} />

      {/*
        Settings
      */}

      <Route exact={true} path="/settings" component={Settings} />

      <Route path="/settings/general" component={GeneralSettingsConnector} />

      <Route path="/settings/connect" component={NotificationSettings} />

      <Route path="/settings/tags" component={TagSettings} />

      <Route path="/settings/ui" component={UISettingsConnector} />

      {/*
        System
      */}

      <Route path="/system/status" component={Status} />

      <Route path="/system/tasks" component={Tasks} />

      <Route path="/system/backup" component={BackupsConnector} />

      <Route path="/system/updates" component={Updates} />

      <Route path="/system/events" component={LogsTableConnector} />

      <Route path="/system/logs/files" component={Logs} />

      {/*
        Not Found
      */}

      <Route path="*" component={NotFound} />
    </Switch>
  );
}

export default AppRoutes;
