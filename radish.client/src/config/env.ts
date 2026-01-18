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
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000',

  /**
   * SignalR Hub URL
   */
  signalrHubUrl: import.meta.env.VITE_SIGNALR_HUB_URL || 'https://localhost:5000',

  /**
   * 是否启用 Mock 数据
   */
  enableMock: import.meta.env.VITE_ENABLE_MOCK === 'true',

  /**
   * 是否启用调试模式
   */
  debug: import.meta.env.VITE_DEBUG === 'true',

  /**
   * 功能开关
   */
  features: {
    /**
     * 暗色模式
     */
    darkMode: import.meta.env.VITE_FEATURE_DARK_MODE === 'true',

    /**
     * 国际化
     */
    i18n: import.meta.env.VITE_FEATURE_I18N === 'true',
  },
} as const;

/**
 * 获取 API 基础 URL
 *
 * 兼容旧的硬编码方式
 */
export function getApiBaseUrl(): string {
  return env.apiBaseUrl.replace(/\/$/, '');
}
