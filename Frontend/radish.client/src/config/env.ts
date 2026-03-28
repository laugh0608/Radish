type RuntimeConfig = NonNullable<Window['__RADISH_RUNTIME_CONFIG__']>;

const defaultPublicUrl = 'https://localhost:5000';

function getRuntimeConfig(): RuntimeConfig {
  if (typeof window === 'undefined' || !window.__RADISH_RUNTIME_CONFIG__) {
    return {};
  }

  return window.__RADISH_RUNTIME_CONFIG__;
}

function readRuntimeString(value: string | undefined, fallback: string): string {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : fallback;
}

function readRuntimeBoolean(value: boolean | undefined, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}

const runtimeConfig = getRuntimeConfig();

/**
 * 环境配置
 */
export const env = {
  /**
   * 是否为开发环境
   */
  isDev: import.meta.env.DEV,

  /**
   * 是否为生产环境
   */
  isProd: import.meta.env.PROD,

  /**
   * API 基础 URL
   */
  apiBaseUrl: readRuntimeString(
    runtimeConfig.apiBaseUrl,
    import.meta.env.VITE_API_BASE_URL || defaultPublicUrl,
  ),

  /**
   * 认证服务器 URL
   */
  authBaseUrl: readRuntimeString(
    runtimeConfig.authBaseUrl,
    import.meta.env.VITE_AUTH_BASE_URL || defaultPublicUrl,
  ),

  /**
   * SignalR Hub URL
   */
  signalrHubUrl: readRuntimeString(
    runtimeConfig.signalrHubUrl,
    import.meta.env.VITE_SIGNALR_HUB_URL || defaultPublicUrl,
  ),

  /**
   * 是否启用 Mock 数据
   */
  enableMock: readRuntimeBoolean(runtimeConfig.enableMock, import.meta.env.VITE_ENABLE_MOCK === 'true'),

  /**
   * 是否启用调试模式
   */
  debug: readRuntimeBoolean(runtimeConfig.debug, import.meta.env.VITE_DEBUG === 'true'),

  /**
   * 功能开关
   */
  features: {
    /**
     * 暗色模式
     */
    darkMode: readRuntimeBoolean(
      runtimeConfig.features?.darkMode,
      import.meta.env.VITE_FEATURE_DARK_MODE === 'true',
    ),

    /**
     * 国际化
     */
    i18n: readRuntimeBoolean(
      runtimeConfig.features?.i18n,
      import.meta.env.VITE_FEATURE_I18N === 'true',
    ),
  },

} as const;

/**
 * 获取 API 基础 URL
 *
 * 开发环境：根据当前端口智能选择
 * - 5000端口：使用当前origin (通过Gateway)
 * - 3000端口：使用配置的API URL (Gateway地址)
 * 生产环境：使用配置的API URL
 */
export function getApiBaseUrl(): string {
  if (typeof window === 'undefined') {
    return env.apiBaseUrl.replace(/\/$/, '');
  }

  // 如果通过 Gateway 访问（5000端口），使用当前 origin
  if (window.location.port === '5000') {
    return window.location.origin;
  }

  // 否则使用配置的 API URL（开发环境下为 Gateway 地址）
  return env.apiBaseUrl.replace(/\/$/, '');
}

/**
 * 获取 Auth Server 基础 URL
 *
 * 开发环境：根据当前端口智能选择
 * - 5000端口：使用当前origin (通过Gateway)
 * - 3000端口：使用配置的Auth URL (Gateway地址)
 * 生产环境：使用配置的Auth URL
 */
export function getAuthBaseUrl(): string {
  if (typeof window === 'undefined') {
    return env.authBaseUrl.replace(/\/$/, '');
  }

  // 如果通过 Gateway 访问（5000端口），使用当前 origin
  if (window.location.port === '5000') {
    return window.location.origin;
  }

  // 否则使用配置的认证服务器 URL（开发环境下为 Gateway 地址）
  return env.authBaseUrl.replace(/\/$/, '');
}

/**
 * 获取 SignalR Hub 基础 URL
 *
 * 开发环境：根据当前端口智能选择
 * - 5000端口：使用当前origin (通过Gateway)
 * - 3000端口：使用配置的SignalR URL (Gateway地址)
 * 生产环境：使用配置的SignalR URL
 */
export function getSignalrHubUrl(): string {
  if (typeof window === 'undefined') {
    return env.signalrHubUrl.replace(/\/$/, '');
  }

  // 如果通过 Gateway 访问（5000端口），使用当前 origin
  if (window.location.port === '5000') {
    return window.location.origin;
  }

  // 否则使用配置的 SignalR URL（开发环境下为 Gateway 地址）
  return env.signalrHubUrl.replace(/\/$/, '');
}
