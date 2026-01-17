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
   * Auth Server URL
   */
  authServerUrl: import.meta.env.VITE_AUTH_SERVER_URL || 'https://localhost:5000',

  /**
   * 是否启用调试模式
   */
  debug: import.meta.env.VITE_DEBUG === 'true',

  /**
   * 功能开关
   */
  features: {
    /**
     * 主题切换
     */
    themeSwitch: import.meta.env.VITE_FEATURE_THEME_SWITCH === 'true',

    /**
     * 全局搜索
     */
    globalSearch: import.meta.env.VITE_FEATURE_GLOBAL_SEARCH === 'true',
  },
} as const;

/**
 * 获取 Auth Server 基础 URL
 *
 * 根据当前访问方式动态返回正确的 Auth Server URL
 */
export function getAuthServerBaseUrl(): string {
  if (typeof window === 'undefined') {
    return env.authServerUrl;
  }

  const currentOrigin = window.location.origin;

  // 通过 Gateway 访问
  if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
    return currentOrigin;
  }

  // 直接访问 console 开发服务器
  if (currentOrigin === 'http://localhost:3100' || currentOrigin === 'https://localhost:3100') {
    return env.authServerUrl;
  }

  // 生产环境
  return currentOrigin;
}

/**
 * 获取 redirect_uri
 */
export function getRedirectUri(): string {
  if (typeof window === 'undefined') {
    return '';
  }

  const currentOrigin = window.location.origin;
  return `${currentOrigin}/console/callback`;
}

/**
 * 获取 post_logout_redirect_uri
 */
export function getPostLogoutRedirectUri(): string {
  if (typeof window === 'undefined') {
    return '';
  }

  const currentOrigin = window.location.origin;
  return `${currentOrigin}/console/`;
}
