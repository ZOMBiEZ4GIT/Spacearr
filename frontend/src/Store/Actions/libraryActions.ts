import { createAction } from 'redux-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set, update } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'library';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: [],
  sortKey: 'size',
  sortDirection: sortDirections.DESCENDING,
  selectedFilterKey: 'all',
  sourceFilter: 'all',
  minSizeFilter: 0,
  colorBy: 'bitrate',
  selectedIds: [] as number[],

  stats: {
    isFetching: false,
    isPopulated: false,
    error: null,
    item: {
      totalSize: 0,
      fileCount: 0,
      qualityBreakdown: [] as { quality: string; size: number; count: number }[],
      quickWins: [] as { id: number; title: string; currentSize: number; estimatedSize: number; savings: number }[],
    },
  },

  columns: [
    {
      name: 'select',
      label: '',
      isVisible: true,
      isModifiable: false,
    },
    {
      name: 'title',
      label: 'Title',
      isSortable: true,
      isVisible: true,
    },
    {
      name: 'size',
      label: 'Size',
      isSortable: true,
      isVisible: true,
    },
    {
      name: 'bitrate',
      label: 'Bitrate',
      isSortable: true,
      isVisible: true,
    },
    {
      name: 'quality',
      label: 'Quality',
      isSortable: true,
      isVisible: true,
    },
    {
      name: 'source',
      label: 'Source',
      isSortable: true,
      isVisible: true,
    },
  ],

  filters: [
    {
      key: 'all',
      label: 'All',
      filters: [],
    },
    {
      key: 'radarr',
      label: 'Radarr',
      filters: [
        {
          key: 'source',
          value: 'radarr',
        },
      ],
    },
    {
      key: 'sonarr',
      label: 'Sonarr',
      filters: [
        {
          key: 'source',
          value: 'sonarr',
        },
      ],
    },
  ],
};

//
// Action Types

export const FETCH_LIBRARY = 'library/fetchLibrary';
export const FETCH_LIBRARY_STATS = 'library/fetchLibraryStats';
export const SET_LIBRARY_SORT = 'library/setLibrarySort';
export const SET_LIBRARY_FILTER = 'library/setLibraryFilter';
export const SET_LIBRARY_TABLE_OPTION = 'library/setLibraryTableOption';
export const SET_LIBRARY_SOURCE_FILTER = 'library/setLibrarySourceFilter';
export const SET_LIBRARY_MIN_SIZE_FILTER = 'library/setLibraryMinSizeFilter';
export const SET_LIBRARY_COLOR_BY = 'library/setLibraryColorBy';
export const SET_LIBRARY_SELECTED_IDS = 'library/setLibrarySelectedIds';
export const TRIGGER_SCAN = 'library/triggerScan';

//
// Action Creators

export const fetchLibrary = createThunk(FETCH_LIBRARY);
export const fetchLibraryStats = createThunk(FETCH_LIBRARY_STATS);
export const setLibrarySort = createAction(SET_LIBRARY_SORT);
export const setLibraryFilter = createAction(SET_LIBRARY_FILTER);
export const setLibraryTableOption = createAction(SET_LIBRARY_TABLE_OPTION);
export const setLibrarySourceFilter = createAction(SET_LIBRARY_SOURCE_FILTER);
export const setLibraryMinSizeFilter = createAction(SET_LIBRARY_MIN_SIZE_FILTER);
export const setLibraryColorBy = createAction(SET_LIBRARY_COLOR_BY);
export const setLibrarySelectedIds = createAction(SET_LIBRARY_SELECTED_IDS);
export const triggerScan = createThunk(TRIGGER_SCAN);

//
// Helpers

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_LIBRARY]: function (_getState: any, _payload: any, dispatch: any) {
    dispatch(set({ section, isFetching: true }));

    const { request, abortRequest } = createAjaxRequest({
      url: '/api/v3/library',
      traditional: true,
    });

    request.done((data: any) => {
      dispatch(
        update({
          section,
          data: Array.isArray(data) ? data : data.records || [],
        })
      );

      dispatch(
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
        })
      );
    });

    request.fail((xhr: any) => {
      dispatch(
        set({
          section,
          isFetching: false,
          isPopulated: false,
          error: xhr.aborted ? null : xhr,
        })
      );
    });

    return abortRequest;
  },

  [FETCH_LIBRARY_STATS]: function (_getState: any, _payload: any, dispatch: any) {
    dispatch(set({ section: `${section}.stats`, isFetching: true }));

    const { request, abortRequest } = createAjaxRequest({
      url: '/api/v3/library/stats',
      traditional: true,
    });

    request.done((data: any) => {
      dispatch(
        set({
          section: `${section}.stats`,
          isFetching: false,
          isPopulated: true,
          error: null,
          item: data,
        })
      );
    });

    request.fail((xhr: any) => {
      dispatch(
        set({
          section: `${section}.stats`,
          isFetching: false,
          isPopulated: false,
          error: xhr.aborted ? null : xhr,
        })
      );
    });

    return abortRequest;
  },

  [TRIGGER_SCAN]: function (_getState: any, payload: any, _dispatch: any) {
    const { request } = createAjaxRequest({
      url: '/api/v3/scan',
      method: 'POST',
      data: JSON.stringify(payload),
      dataType: 'json',
      contentType: 'application/json',
    });

    return request;
  },
});

//
// Reducers

export const reducers = createHandleActions(
  {
    [SET_LIBRARY_SORT]: function (state: any, { payload }: any) {
      return {
        ...state,
        sortKey: payload.sortKey,
        sortDirection: payload.sortDirection,
      };
    },

    [SET_LIBRARY_FILTER]: function (state: any, { payload }: any) {
      return {
        ...state,
        selectedFilterKey: payload.selectedFilterKey,
      };
    },

    [SET_LIBRARY_TABLE_OPTION]: createSetTableOptionReducer(section),

    [SET_LIBRARY_SOURCE_FILTER]: function (state: any, { payload }: any) {
      return {
        ...state,
        sourceFilter: payload.sourceFilter,
      };
    },

    [SET_LIBRARY_MIN_SIZE_FILTER]: function (state: any, { payload }: any) {
      return {
        ...state,
        minSizeFilter: payload.minSizeFilter,
      };
    },

    [SET_LIBRARY_COLOR_BY]: function (state: any, { payload }: any) {
      return {
        ...state,
        colorBy: payload.colorBy,
      };
    },

    [SET_LIBRARY_SELECTED_IDS]: function (state: any, { payload }: any) {
      return {
        ...state,
        selectedIds: payload.selectedIds,
      };
    },
  },
  defaultState,
  section
);
