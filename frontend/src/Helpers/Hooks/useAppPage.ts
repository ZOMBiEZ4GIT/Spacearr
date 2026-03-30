import { useEffect, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import { fetchTranslations } from 'Store/Actions/appActions';
import {
  fetchUISettings,
} from 'Store/Actions/settingsActions';
import { fetchStatus } from 'Store/Actions/systemActions';
import { fetchTags } from 'Store/Actions/tagActions';

const createErrorsSelector = () =>
  createSelector(
    (state: AppState) => state.tags.error,
    (state: AppState) => state.settings.ui.error,
    (state: AppState) => state.system.status.error,
    (state: AppState) => state.app.translations.error,
    (
      tagsError,
      uiSettingsError,
      systemStatusError,
      translationsError
    ) => {
      const hasError = !!(
        tagsError ||
        uiSettingsError ||
        systemStatusError ||
        translationsError
      );

      return {
        hasError,
        errors: {
          tagsError,
          uiSettingsError,
          systemStatusError,
          translationsError,
        },
      };
    }
  );

const useAppPage = () => {
  const dispatch = useDispatch();

  const isPopulated = useSelector(
    (state: AppState) =>
      state.tags.isPopulated &&
      state.settings.ui.isPopulated &&
      state.system.status.isPopulated &&
      state.app.translations.isPopulated
  );

  const { hasError, errors } = useSelector(createErrorsSelector());

  const isLocalStorageSupported = useMemo(() => {
    const key = 'spacearrTest';

    try {
      localStorage.setItem(key, key);
      localStorage.removeItem(key);

      return true;
    } catch {
      return false;
    }
  }, []);

  useEffect(() => {
    dispatch(fetchTags());
    dispatch(fetchUISettings());
    dispatch(fetchStatus());
    dispatch(fetchTranslations());
  }, [dispatch]);

  return useMemo(() => {
    return { errors, hasError, isLocalStorageSupported, isPopulated };
  }, [errors, hasError, isLocalStorageSupported, isPopulated]);
};

export default useAppPage;
