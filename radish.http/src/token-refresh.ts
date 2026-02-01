/**
 * Token 自动刷新模块
 *
 * 功能：
 * - 检测 401 未授权响应
 * - 自动使用 refresh_token 刷新 access_token
 * - 重试原始请求
 * - 防止并发刷新（多个请求同时失败时只刷新一次）
 */

interface TokenRefreshConfig {
  /** 认证服务器基础 URL */
  authBaseUrl: string;
  /** 客户端 ID */
  clientId: string;
  /** 获取 refresh_token 的函数 */
  getRefreshToken: () => string | null;
  /** 保存新 token 的函数 */
  saveTokens: (accessToken: string, refreshToken?: string) => void;
  /** 清除 token 的函数（刷新失败时） */
  clearTokens: () => void;
  /** 重定向到登录页的函数 */
  redirectToLogin: () => void;
}

let refreshConfig: TokenRefreshConfig | null = null;
let isRefreshing = false;
let refreshPromise: Promise<string> | null = null;

/**
 * 配置 token 刷新
 */
export function configureTokenRefresh(config: TokenRefreshConfig) {
  refreshConfig = config;
}

/**
 * 获取当前配置
 */
export function getTokenRefreshConfig(): TokenRefreshConfig | null {
  return refreshConfig;
}

/**
 * 刷新 access_token
 */
async function refreshAccessToken(): Promise<string> {
  if (!refreshConfig) {
    throw new Error('Token refresh not configured');
  }

  const refreshToken = refreshConfig.getRefreshToken();
  if (!refreshToken) {
    throw new Error('No refresh token available');
  }

  const body = new URLSearchParams();
  body.set('grant_type', 'refresh_token');
  body.set('client_id', refreshConfig.clientId);
  body.set('refresh_token', refreshToken);

  const response = await fetch(`${refreshConfig.authBaseUrl}/connect/token`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body,
  });

  if (!response.ok) {
    throw new Error(`Token refresh failed: HTTP ${response.status}`);
  }

  const tokenSet = await response.json() as {
    access_token?: string;
    refresh_token?: string;
  };

  if (!tokenSet.access_token) {
    throw new Error('No access_token in refresh response');
  }

  // 保存新的 token
  refreshConfig.saveTokens(tokenSet.access_token, tokenSet.refresh_token);

  return tokenSet.access_token;
}

/**
 * 尝试刷新 token（带并发控制）
 *
 * 如果多个请求同时失败，只会执行一次刷新
 */
export async function tryRefreshToken(): Promise<string> {
  // 如果正在刷新，等待刷新完成
  if (isRefreshing && refreshPromise) {
    return refreshPromise;
  }

  // 开始刷新
  isRefreshing = true;
  refreshPromise = refreshAccessToken()
    .catch((error) => {
      // 刷新失败，清除 token 并重定向到登录页
      if (refreshConfig) {
        refreshConfig.clearTokens();
        refreshConfig.redirectToLogin();
      }
      throw error;
    })
    .finally(() => {
      // 刷新完成，重置状态
      isRefreshing = false;
      refreshPromise = null;
    });

  return refreshPromise;
}

/**
 * 检查响应是否需要刷新 token
 */
export function shouldRefreshToken(response: Response): boolean {
  return response.status === 401;
}

/**
 * 重置刷新状态（用于测试）
 */
export function resetRefreshState() {
  isRefreshing = false;
  refreshPromise = null;
}
