import { Error } from './AppSectionState';
import CaptchaAppState from './CaptchaAppState';
import CommandAppState from './CommandAppState';
import CustomFiltersAppState from './CustomFiltersAppState';
import MessagesAppState from './MessagesAppState';
import OAuthAppState from './OAuthAppState';
import ParseAppState from './ParseAppState';
import PathsAppState from './PathsAppState';
import ProviderOptionsAppState from './ProviderOptionsAppState';
import RootFolderAppState from './RootFolderAppState';
import SettingsAppState from './SettingsAppState';
import SystemAppState from './SystemAppState';
import TagsAppState from './TagsAppState';

interface FilterBuilderPropOption {
  id: string;
  name: string;
}

export interface FilterBuilderProp<T> {
  name: string;
  label: string;
  type: string;
  valueType?: string;
  optionsSelector?: (items: T[]) => FilterBuilderPropOption[];
}

export interface PropertyFilter {
  key: string;
  value: boolean | string | number | string[] | number[];
  type: string;
}

export interface Filter {
  key: string;
  label: string | (() => string);
  filters: PropertyFilter[];
}

export interface CustomFilter {
  id: number;
  type: string;
  label: string;
  filters: PropertyFilter[];
}

export interface AppSectionState {
  isUpdated: boolean;
  isConnected: boolean;
  isDisconnected: boolean;
  isReconnecting: boolean;
  isSidebarVisible: boolean;
  version: string;
  prevVersion?: string;
  dimensions: {
    isSmallScreen: boolean;
    isLargeScreen: boolean;
    width: number;
    height: number;
  };
  translations: {
    error?: Error;
    isPopulated: boolean;
  };
  messages: MessagesAppState;
}

interface AppState {
  app: AppSectionState;
  captcha: CaptchaAppState;
  commands: CommandAppState;
  customFilters: CustomFiltersAppState;
  oAuth: OAuthAppState;
  parse: ParseAppState;
  paths: PathsAppState;
  providerOptions: ProviderOptionsAppState;
  rootFolders: RootFolderAppState;
  settings: SettingsAppState;
  system: SystemAppState;
  tags: TagsAppState;
}

export default AppState;
