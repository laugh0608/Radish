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
   * API 基础 URL（统一走 Gateway）
   */
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000',

  /**
   * Auth Server URL（统一走 Gateway）
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
 * 获取 API 基础 URL
 *
 * 开发环境：根据当前端口智能选择
 * - 5000端口：使用当前origin (通过Gateway)
 * - 3100端口：使用配置的API URL (Gateway地址)
 * 生产环境：使用配置的API URL
 */
export function getApiBaseUrl(): string {
  if (typeof window === 'undefined') {
    return env.apiBaseUrl;
  }

  // 如果通过 Gateway 访问（5000端口），使用当前 origin
  if (window.location.port === '5000') {
    return window.location.origin;
  }

  // 否则使用配置的 API URL（开发环境下为 Gateway 地址）
  return env.apiBaseUrl;
}

/**
 * 获取 Auth Server 基础 URL
 *
 * 开发环境：根据当前端口智能选择
 * - 5000端口：使用当前origin (通过Gateway)
 * - 3100端口：使用配置的Auth URL (Gateway地址)
 * 生产环境：使用配置的Auth URL
 */
export function getAuthServerBaseUrl(): string {
  if (typeof window === 'undefined') {
    return env.authServerUrl;
  }

  // 如果通过 Gateway 访问（5000端口），使用当前 origin
  if (window.location.port === '5000') {
    return window.location.origin;
  }

  // 否则使用配置的认证服务器 URL（开发环境下为 Gateway 地址）
  return env.authServerUrl;
}

/**
 * 获取 redirect_uri
 *
 * 根据当前访问方式返回正确的回调地址
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
 *
 * 根据当前访问方式返回正确的登出后地址
 */
export function getPostLogoutRedirectUri(): string {
  if (typeof window === 'undefined') {
    return '';
  }

  const currentOrigin = window.location.origin;
  return `${currentOrigin}/console/`;
}

