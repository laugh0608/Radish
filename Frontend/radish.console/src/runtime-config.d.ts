interface RadishRuntimeConfig {
  apiBaseUrl?: string;
  authBaseUrl?: string;
  signalrHubUrl?: string;
  authServerUrl?: string;
  enableMock?: boolean;
  debug?: boolean;
  tokenAutoRefreshDebug?: boolean;
  features?: {
    darkMode?: boolean;
    i18n?: boolean;
    themeSwitch?: boolean;
    globalSearch?: boolean;
  };
}

interface Window {
  __RADISH_RUNTIME_CONFIG__?: RadishRuntimeConfig;
}
