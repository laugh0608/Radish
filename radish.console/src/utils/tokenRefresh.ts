import { log } from '@/utils/logger';
import { getAuthServerBaseUrl } from '@/config/env';

/**
 * Token 刷新管理器
 */
class TokenRefreshManager {
  private refreshing = false;
  private refreshPromise: Promise<string | null> | null = null;

  /**
   * 刷新 Token
   */
  async refreshToken(): Promise<string | null> {
    // 如果正在刷新，返回现有的 Promise
    if (this.refreshing && this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshing = true;
    this.refreshPromise = this.doRefresh();

    try {
      const newToken = await this.refreshPromise;
      return newToken;
    } finally {
      this.refreshing = false;
      this.refreshPromise = null;
    }
  }

  /**
   * 执行刷新操作
   */
  private async doRefresh(): Promise<string | null> {
    try {
      const refreshToken = localStorage.getItem('refresh_token');
      if (!refreshToken) {
        throw new Error('No refresh token available');
      }

      // 获取 Auth Server 基础 URL
      const authServerBaseUrl = getAuthServerBaseUrl();

      // 调用 Token 端点
      const response = await fetch(`${authServerBaseUrl}/connect/token`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: new URLSearchParams({
          grant_type: 'refresh_token',
          refresh_token: refreshToken,
          client_id: 'radish-console',
        }),
      });

      if (!response.ok) {
        throw new Error('Token refresh failed');
      }

      const data = await response.json();

      // 保存新的 Token
      if (data.access_token) {
        localStorage.setItem('access_token', data.access_token);
      }
      if (data.refresh_token) {
        localStorage.setItem('refresh_token', data.refresh_token);
      }

      return data.access_token;
    } catch (error) {
      log.error('Failed to refresh token:', error);

      // 刷新失败，清除 Token 并跳转到登录页
      localStorage.removeItem('access_token');
      localStorage.removeItem('refresh_token');

      // 跳转到登录页
      if (typeof window !== 'undefined') {
        window.location.href = '/console/login';
      }

      return null;
    }
  }

  /**
   * 检查 Token 是否即将过期
   *
   * @param token JWT Token
   * @param thresholdSeconds 提前刷新的时间阈值（秒）
   * @returns 是否需要刷新
   */
  shouldRefreshToken(token: string, thresholdSeconds = 300): boolean {
    try {
      // 解析 JWT Token
      const payload = this.parseJwt(token);
      if (!payload || !payload.exp) {
        return false;
      }

      // 计算过期时间
      const expirationTime = payload.exp * 1000; // 转换为毫秒
      const currentTime = Date.now();
      const timeUntilExpiration = expirationTime - currentTime;

      // 如果距离过期时间小于阈值，需要刷新
      return timeUntilExpiration < thresholdSeconds * 1000;
    } catch (error) {
      log.error('Failed to parse token:', error);
      return false;
    }
  }

  /**
   * 解析 JWT Token
   */
  private parseJwt(token: string): any {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch (error) {
      return null;
    }
  }
}

/**
 * 全局 Token 刷新管理器实例
 */
export const tokenRefreshManager = new TokenRefreshManager();

/**
 * 自动刷新 Token 的拦截器
 *
 * 在 API 请求前检查 Token 是否即将过期，如果是则自动刷新
 */
export async function autoRefreshTokenInterceptor(): Promise<void> {
  const token = localStorage.getItem('access_token');
  if (!token) {
    return;
  }

  // 检查是否需要刷新（提前 5 分钟刷新）
  if (tokenRefreshManager.shouldRefreshToken(token, 300)) {
    await tokenRefreshManager.refreshToken();
  }
}
