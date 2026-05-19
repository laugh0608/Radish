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
  __TAURI__?: {
    core?: {
      invoke?: <T>(command: string, args?: Record<string, unknown>) => Promise<T>;
    };
    event?: {
      listen?: <T>(
        event: string,
        handler: (event: { payload: T }) => void,
      ) => Promise<() => void>;
    };
  };
  Capacitor?: {
    isNativePlatform?: () => boolean;
  };
}
