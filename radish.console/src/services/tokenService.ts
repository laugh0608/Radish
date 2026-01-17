import { log } from '@/utils/logger';
import { getAuthServerBaseUrl } from '@/config/env';

/**
 * Token 信息接口
 */
interface TokenInfo {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  token_type: string;
}

/**
 * Token 刷新响应接口
 */
interface TokenRefreshResponse {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  token_type: string;
}

/**
 * Token 管理服务
 *
 * 负责 Token 的存储、刷新和过期检测
 */
class TokenService {
  private static instance: TokenService;
  private refreshPromise: Promise<string> | null = null;
  private readonly TOKEN_KEY = 'access_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly TOKEN_EXPIRES_KEY = 'token_expires_at';

  private constructor() {}

  static getInstance(): TokenService {
    if (!TokenService.instance) {
      TokenService.instance = new TokenService();
    }
    return TokenService.instance;
  }

  /**
   * 获取访问令牌
   */
  getAccessToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  /**
   * 获取刷新令牌
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  /**
   * 获取 Token 过期时间
   */
  getTokenExpiresAt(): number | null {
    const expiresAt = localStorage.getItem(this.TOKEN_EXPIRES_KEY);
    return expiresAt ? parseInt(expiresAt, 10) : null;
  }

  /**
   * 存储 Token 信息
   */
  setTokenInfo(tokenInfo: TokenInfo): void {
    localStorage.setItem(this.TOKEN_KEY, tokenInfo.access_token);

    if (tokenInfo.refresh_token) {
      localStorage.setItem(this.REFRESH_TOKEN_KEY, tokenInfo.refresh_token);
    }

    // 计算过期时间（提前 5 分钟刷新）
    const expiresAt = Date.now() + (tokenInfo.expires_in - 300) * 1000;
    localStorage.setItem(this.TOKEN_EXPIRES_KEY, expiresAt.toString());

    log.debug('TokenService', 'Token 信息已更新', {
      expires_in: tokenInfo.expires_in,
      expires_at: new Date(expiresAt).toISOString(),
    });
  }

  /**
   * 清除所有 Token 信息
   */
  clearTokens(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRES_KEY);
    this.refreshPromise = null;
    log.debug('TokenService', 'Token 信息已清除');
  }

  /**
   * 检查 Token 是否即将过期
   */
  isTokenExpiringSoon(): boolean {
    const expiresAt = this.getTokenExpiresAt();
    if (!expiresAt) {
      return true; // 没有过期时间信息，认为需要刷新
    }

    // 提前 5 分钟刷新
    return Date.now() >= expiresAt - 300000;
  }

  /**
   * 检查 Token 是否已过期
   */
  isTokenExpired(): boolean {
    const expiresAt = this.getTokenExpiresAt();
    if (!expiresAt) {
      return true;
    }

    return Date.now() >= expiresAt;
  }

  /**
   * 刷新访问令牌
   */
  async refreshAccessToken(): Promise<string> {
    // 如果已经有刷新请求在进行中，返回同一个 Promise
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('没有可用的刷新令牌');
    }

    this.refreshPromise = this.performTokenRefresh(refreshToken);

    try {
      const newAccessToken = await this.refreshPromise;
      return newAccessToken;
    } finally {
      this.refreshPromise = null;
    }
  }

  /**
   * 执行 Token 刷新请求
   */
  private async performTokenRefresh(refreshToken: string): Promise<string> {
    try {
      log.debug('TokenService', '开始刷新 Token');

      const authServerUrl = getAuthServerBaseUrl();
      const response = await fetch(`${authServerUrl}/connect/token`, {
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
        throw new Error(`Token 刷新失败: ${response.status} ${response.statusText}`);
      }

      const tokenData: TokenRefreshResponse = await response.json();

      // 更新 Token 信息
      this.setTokenInfo({
        access_token: tokenData.access_token,
        refresh_token: tokenData.refresh_token || refreshToken,
        expires_in: tokenData.expires_in,
        token_type: tokenData.token_type,
      });

      log.debug('TokenService', 'Token 刷新成功');
      return tokenData.access_token;
    } catch (error) {
      log.error('TokenService', 'Token 刷新失败', error);

      // 刷新失败，清除所有 Token
      this.clearTokens();

      throw error;
    }
  }

  /**
   * 获取有效的访问令牌（自动刷新）
   */
  async getValidAccessToken(): Promise<string | null> {
    const currentToken = this.getAccessToken();

    if (!currentToken) {
      return null;
    }

    // 如果 Token 即将过期，尝试刷新
    if (this.isTokenExpiringSoon()) {
      try {
        return await this.refreshAccessToken();
      } catch (error) {
        log.error('TokenService', '自动刷新 Token 失败', error);
        return null;
      }
    }

    return currentToken;
  }
}

/**
 * 导出单例实例
 */
export const tokenService = TokenService.getInstance();