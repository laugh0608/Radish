/**
 * Token 自动刷新模块
 *
 * 功能：
 * - 检测 401 未授权响应
 * - 自动使用 refresh_token 刷新 access_token
 * - 重试原始请求
 * - 防止并发刷新（多个请求同时失败时只刷新一次）
 */

/**
 * Token 刷新错误类型
 */
export enum TokenRefreshErrorType {
  /** 网络错误，可重试 */
  NetworkError = 'network_error',
  /** Refresh Token 无效，需重新登录 */
  InvalidRefreshToken = 'invalid_token',
  /** 服务器错误，可重试 */
  ServerError = 'server_error',
  /** 未知错误 */
  Unknown = 'unknown'
}

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
  onRefreshFailed: (errorType: TokenRefreshErrorType, error: Error) => void;
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
 * 判断错误类型
 */
function determineErrorType(error: unknown, status?: number): TokenRefreshErrorType {
  // 401/400 表示 Refresh Token 无效
  if (status === 401 || status === 400) {
    return TokenRefreshErrorType.InvalidRefreshToken;
  }

  // 5xx 表示服务器错误
  if (status && status >= 500) {
    return TokenRefreshErrorType.ServerError;
  }

  // 网络错误
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return TokenRefreshErrorType.NetworkError;
  }

  return TokenRefreshErrorType.Unknown;
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
    const error = new Error('No refresh token available');
    throw { error, errorType: TokenRefreshErrorType.InvalidRefreshToken };
  }

  const clientId = refreshConfig.clientId || 'radish-client';

  const body = new URLSearchParams();
  body.set('grant_type', 'refresh_token');
  body.set('client_id', clientId);
  body.set('refresh_token', refreshToken);

  try {
    const response = await fetch(refreshConfig.refreshEndpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body,
    });

    if (!response.ok) {
      const error = new Error(`Token refresh failed: HTTP ${response.status}`);
      const errorType = determineErrorType(error, response.status);
      throw { error, errorType };
    }

    const tokenSet = (await response.json()) as {
      access_token?: string;
      refresh_token?: string;
    };

    if (!tokenSet.access_token) {
      const error = new Error('No access_token in refresh response');
      throw { error, errorType: TokenRefreshErrorType.ServerError };
    }

    // 调用成功回调保存新的 token
    refreshConfig.onTokenRefreshed(tokenSet.access_token, tokenSet.refresh_token);

    return tokenSet.access_token;
  } catch (err: any) {
    // 如果已经是我们包装的错误，直接抛出
    if (err.error && err.errorType) {
      throw err;
    }

    // 否则判断错误类型
    const error = err instanceof Error ? err : new Error(String(err));
    const errorType = determineErrorType(error);
    throw { error, errorType };
  }
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
    .catch((err: any) => {
      // 刷新失败，调用失败回调
      if (refreshConfig) {
        const error = err.error || new Error(String(err));
        const errorType = err.errorType || TokenRefreshErrorType.Unknown;
        refreshConfig.onRefreshFailed(errorType, error);
      }
      throw err;
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
