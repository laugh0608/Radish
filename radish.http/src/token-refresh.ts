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
  /** Token 刷新端点 URL */
  refreshEndpoint: string;
  /** 客户端 ID（可选，默认 'radish-client'） */
  clientId?: string;
  /** 获取 refresh_token 的函数 */
  getRefreshToken: () => string | null;
  /** Token 刷新成功回调 */
  onTokenRefreshed: (accessToken: string, refreshToken?: string) => void;
  /** Token 刷新失败回调 */
  onRefreshFailed: () => void;
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

  const clientId = refreshConfig.clientId || 'radish-client';

  const body = new URLSearchParams();
  body.set('grant_type', 'refresh_token');
  body.set('client_id', clientId);
  body.set('refresh_token', refreshToken);

  const response = await fetch(refreshConfig.refreshEndpoint, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body,
  });

  if (!response.ok) {
    throw new Error(`Token refresh failed: HTTP ${response.status}`);
  }

  const tokenSet = (await response.json()) as {
    access_token?: string;
    refresh_token?: string;
  };

  if (!tokenSet.access_token) {
    throw new Error('No access_token in refresh response');
  }

  // 调用成功回调保存新的 token
  refreshConfig.onTokenRefreshed(tokenSet.access_token, tokenSet.refresh_token);

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
      // 刷新失败，调用失败回调
      if (refreshConfig) {
        refreshConfig.onRefreshFailed();
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
