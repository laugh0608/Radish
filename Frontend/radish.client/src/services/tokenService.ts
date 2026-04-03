/**
 * Token 管理服务
 * 负责 Token 的存储、刷新和过期检测
 */
import { log } from '@/utils/logger';
import { getAuthBaseUrl } from '@/config/env';
import {
  classifyTokenRefreshFailure,
  dispatchAuthTokenExpired,
  shouldInvalidateSessionAfterRefreshFailure,
  TokenRefreshFailureReason,
  type TokenRefreshFailureReason as TokenRefreshFailureReasonValue,
} from './authSession';
import {
  getRolesFromTokenPayload,
  getUserIdentityFromTokenPayload,
  type AccessTokenIdentity,
  type JwtPayload,
} from './tokenClaims';

const CLIENT_ID = 'radish-client';

interface TokenInfo {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  token_type: string;
}

type ParsedJwtPayload = JwtPayload & {
  exp?: number;
};

class TokenRefreshError extends Error {
  readonly reason: TokenRefreshFailureReasonValue;
  readonly status?: number;

  constructor(message: string, reason: TokenRefreshFailureReasonValue, status?: number) {
    super(message);
    this.name = 'TokenRefreshError';
    this.reason = reason;
    this.status = status;
  }
}

class TokenService {
  private static instance: TokenService;
  private refreshPromise: Promise<string> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly TOKEN_KEY = 'radish_client_access_token';
  private readonly REFRESH_TOKEN_KEY = 'radish_client_refresh_token';
  private readonly TOKEN_EXPIRES_KEY = 'radish_client_token_expires_at';
  private readonly TOKEN_REFRESH_AT_KEY = 'radish_client_token_refresh_at';
  private readonly CURRENT_USER_CACHE_KEY = 'cached_user_info';
  private readonly LEGACY_TOKEN_KEY = 'access_token';
  private readonly LEGACY_REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly LEGACY_TOKEN_EXPIRES_KEY = 'token_expires_at';
  private readonly LEGACY_TOKEN_REFRESH_AT_KEY = 'token_refresh_at';
  private readonly MIN_REFRESH_BUFFER_SECONDS = 30;
  private readonly MAX_REFRESH_BUFFER_SECONDS = 300;
  private readonly MIN_CHECK_INTERVAL_MS = 15 * 1000;
  private readonly MAX_CHECK_INTERVAL_MS = 2 * 60 * 1000;

  private constructor() {
    this.migrateLegacyTokenStorage();
  }

  static getInstance(): TokenService {
    if (!TokenService.instance) {
      TokenService.instance = new TokenService();
    }
    return TokenService.instance;
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  getTokenExpiresAt(): number | null {
    const expiresAt = localStorage.getItem(this.TOKEN_EXPIRES_KEY);
    return expiresAt ? parseInt(expiresAt, 10) : null;
  }

  getTokenRefreshAt(): number | null {
    const refreshAt = localStorage.getItem(this.TOKEN_REFRESH_AT_KEY);
    return refreshAt ? parseInt(refreshAt, 10) : null;
  }

  getRolesFromAccessToken(token?: string | null): string[] {
    const targetToken = token ?? this.getAccessToken();
    if (!targetToken) {
      return [];
    }

    const payload = this.parseJwt(targetToken);
    if (!payload) {
      return [];
    }

    return getRolesFromTokenPayload(payload);
  }

  getUserIdentityFromAccessToken(token?: string | null): AccessTokenIdentity | null {
    const targetToken = token ?? this.getAccessToken();
    if (!targetToken) {
      return null;
    }

    const payload = this.parseJwt(targetToken);
    if (!payload) {
      return null;
    }

    return getUserIdentityFromTokenPayload(payload);
  }

  getCurrentUserCacheScope(token?: string | null): string | null {
    const targetToken = token ?? this.getAccessToken();
    if (!targetToken) {
      return null;
    }

    const identity = this.getUserIdentityFromAccessToken(targetToken);
    if (!identity) {
      return null;
    }

    return `v2:${identity.userId}:${identity.tenantId}:${this.createTokenFingerprint(targetToken)}`;
  }

  setTokenInfo(tokenInfo: TokenInfo): void {
    localStorage.setItem(this.TOKEN_KEY, tokenInfo.access_token);

    if (tokenInfo.refresh_token) {
      localStorage.setItem(this.REFRESH_TOKEN_KEY, tokenInfo.refresh_token);
    }

    // 计算过期时间与刷新时间（短 Token 使用动态提前量）
    const refreshBufferSeconds = this.getRefreshBufferSeconds(tokenInfo.expires_in);
    const expiresAt = Date.now() + tokenInfo.expires_in * 1000;
    const refreshAt = Date.now() + Math.max(tokenInfo.expires_in - refreshBufferSeconds, 0) * 1000;
    localStorage.setItem(this.TOKEN_EXPIRES_KEY, expiresAt.toString());
    localStorage.setItem(this.TOKEN_REFRESH_AT_KEY, refreshAt.toString());

    log.debug('TokenService', 'Token 信息已更新', {
      expires_in: tokenInfo.expires_in,
      expires_at: new Date(expiresAt).toISOString(),
      refresh_at: new Date(refreshAt).toISOString(),
    });

    if (this.refreshTimer) {
      this.startAutoRefresh();
    }
  }

  setTokenInfoFromJwt(accessToken: string, refreshToken?: string): void {
    const expiresIn = this.getExpiresInFromJwt(accessToken);
    this.setTokenInfo({
      access_token: accessToken,
      refresh_token: refreshToken,
      expires_in: expiresIn,
      token_type: 'Bearer',
    });
  }

  clearTokens(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRES_KEY);
    localStorage.removeItem(this.TOKEN_REFRESH_AT_KEY);
    localStorage.removeItem(this.CURRENT_USER_CACHE_KEY);
    this.refreshPromise = null;
    this.stopAutoRefresh(); // 清除 Token 时停止自动刷新
    log.debug('TokenService', 'Token 信息已清除');
  }

  /**
   * 检查 Token 是否即将过期（提前 5 分钟）
   */
  isTokenExpiringSoon(): boolean {
    const refreshAt = this.getTokenRefreshAt();
    if (refreshAt) {
      return Date.now() >= refreshAt;
    }

    // 兼容旧数据：若没有 refreshAt，退化为使用 token_expires_at 作为刷新时间
    const legacyRefreshAt = this.getTokenExpiresAt();
    if (legacyRefreshAt) {
      return Date.now() >= legacyRefreshAt;
    }

    // 没有过期时间信息，尝试从 JWT 解析
    const token = this.getAccessToken();
    if (token) {
      return this.shouldRefreshFromJwt(token);
    }
    return true;
  }

  /**
   * 检查 Token 是否已过期
   */
  isTokenExpired(): boolean {
    const expiresAt = this.getTokenExpiresAt();
    if (!expiresAt) {
      const token = this.getAccessToken();
      if (token) {
        return this.isJwtExpired(token);
      }
      return true;
    }

    return Date.now() >= expiresAt;
  }

  /**
   * 从 JWT 解析过期时间并判断是否需要刷新
   */
  private shouldRefreshFromJwt(token: string): boolean {
    try {
      const payload = this.parseJwt(token);
      if (!payload || !payload.exp) {
        return false;
      }

      const expirationTime = payload.exp * 1000;
      const currentTime = Date.now();
      const remainingSeconds = Math.max(0, Math.floor((expirationTime - currentTime) / 1000));
      const refreshBufferSeconds = this.getRefreshBufferSeconds(remainingSeconds);
      return currentTime >= expirationTime - refreshBufferSeconds * 1000;
    } catch {
      return false;
    }
  }

  /**
   * 从 JWT 解析过期时间并判断是否已过期
   */
  private isJwtExpired(token: string): boolean {
    try {
      const payload = this.parseJwt(token);
      if (!payload || !payload.exp) {
        return true;
      }

      return Date.now() >= payload.exp * 1000;
    } catch {
      return true;
    }
  }

  /**
   * 解析 JWT Token
   */
  private parseJwt(token: string): ParsedJwtPayload | null {
    try {
      const base64Url = token.split('.')[1];
      if (!base64Url) {
        return null;
      }

      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const paddedBase64 = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
      const jsonPayload = decodeURIComponent(
        atob(paddedBase64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch {
      return null;
    }
  }

  private createTokenFingerprint(token: string): string {
    let hash = 2166136261;
    for (let index = 0; index < token.length; index += 1) {
      hash ^= token.charCodeAt(index);
      hash = Math.imul(hash, 16777619);
    }

    return `${token.length}:${(hash >>> 0).toString(16)}`;
  }

  private getExpiresInFromJwt(token: string): number {
    const payload = this.parseJwt(token);
    if (!payload?.exp) {
      return 0;
    }

    const nowSeconds = Math.floor(Date.now() / 1000);
    return Math.max(0, payload.exp - nowSeconds);
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
      const error = new TokenRefreshError(
        '没有可用的刷新令牌',
        TokenRefreshFailureReason.MissingRefreshToken,
      );
      this.handleRefreshFailure(error);
      throw error;
    }

    this.refreshPromise = this.performTokenRefresh(refreshToken);

    try {
      const newAccessToken = await this.refreshPromise;
      return newAccessToken;
    } catch (error) {
      this.handleRefreshFailure(error);
      throw error;
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

      const authServerUrl = getAuthBaseUrl();
      const response = await fetch(`${authServerUrl}/connect/token`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: new URLSearchParams({
          grant_type: 'refresh_token',
          refresh_token: refreshToken,
          client_id: CLIENT_ID,
        }),
      });

      if (!response.ok) {
        throw new TokenRefreshError(
          `Token 刷新失败: ${response.status} ${response.statusText}`,
          classifyTokenRefreshFailure(response.status),
          response.status,
        );
      }

      const tokenData: TokenInfo = await response.json();

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
      if (error instanceof TokenRefreshError) {
        throw error;
      }

      const reason = classifyTokenRefreshFailure(undefined, error);
      const message = error instanceof Error ? error.message : String(error);
      throw new TokenRefreshError(`Token 刷新失败: ${message}`, reason);
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
        if (!this.isTokenExpired()) {
          log.warn('TokenService', '提前刷新失败，继续使用当前访问令牌', error);
          return currentToken;
        }

        log.error('TokenService', '自动刷新 Token 失败，当前访问令牌已不可用', error);
        return null;
      }
    }

    return currentToken;
  }

  /**
   * 启动自动刷新定时器
   */
  startAutoRefresh(): void {
    // 如果已经有定时器在运行，先停止
    if (this.refreshTimer) {
      this.stopAutoRefresh();
    }

    log.debug('TokenService', '启动自动刷新定时器');

    const runCheck = async () => {
      const token = this.getAccessToken();
      if (!token) {
        log.debug('TokenService', '没有 Token，停止自动刷新');
        this.stopAutoRefresh();
        return;
      }

      if (this.isTokenExpiringSoon()) {
        log.debug('TokenService', 'Token 即将过期，自动刷新');
        try {
          await this.refreshAccessToken();
          log.debug('TokenService', '自动刷新成功');
        } catch (error) {
          if (!this.getAccessToken()) {
            log.error('TokenService', '自动刷新失败，认证会话已失效', error);
            this.stopAutoRefresh();
            return;
          }

          log.warn('TokenService', '自动刷新失败，保留当前访问令牌并等待下一次重试', error);
        }
      }
    };

    const intervalMs = this.getCheckIntervalMs();
    void runCheck();
    this.refreshTimer = setInterval(runCheck, intervalMs);
  }

  /**
   * 停止自动刷新定时器
   */
  stopAutoRefresh(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
      log.debug('TokenService', '停止自动刷新定时器');
    }
  }

  private getRefreshBufferSeconds(expiresInSeconds: number): number {
    if (!Number.isFinite(expiresInSeconds) || expiresInSeconds <= 0) {
      return this.MIN_REFRESH_BUFFER_SECONDS;
    }

    const dynamicBuffer = Math.floor(expiresInSeconds * 0.2);
    return Math.min(
      this.MAX_REFRESH_BUFFER_SECONDS,
      Math.max(this.MIN_REFRESH_BUFFER_SECONDS, dynamicBuffer)
    );
  }

  private getCheckIntervalMs(): number {
    const now = Date.now();
    const refreshAt = this.getTokenRefreshAt();
    const expiresAt = this.getTokenExpiresAt();
    const targetAt = refreshAt ?? expiresAt;

    if (!targetAt) {
      return this.MAX_CHECK_INTERVAL_MS;
    }

    const remainingMs = targetAt - now;
    if (remainingMs <= 0) {
      return this.MIN_CHECK_INTERVAL_MS;
    }

    const interval = Math.floor(remainingMs / 2);
    return Math.min(this.MAX_CHECK_INTERVAL_MS, Math.max(this.MIN_CHECK_INTERVAL_MS, interval));
  }

  private migrateLegacyTokenStorage(): void {
    if (typeof window === 'undefined') {
      return;
    }

    this.migrateLegacyKey(this.LEGACY_TOKEN_KEY, this.TOKEN_KEY);
    this.migrateLegacyKey(this.LEGACY_REFRESH_TOKEN_KEY, this.REFRESH_TOKEN_KEY);
    this.migrateLegacyKey(this.LEGACY_TOKEN_EXPIRES_KEY, this.TOKEN_EXPIRES_KEY);
    this.migrateLegacyKey(this.LEGACY_TOKEN_REFRESH_AT_KEY, this.TOKEN_REFRESH_AT_KEY);
  }

  private migrateLegacyKey(legacyKey: string, targetKey: string): void {
    const targetValue = localStorage.getItem(targetKey);
    if (targetValue !== null) {
      return;
    }

    const legacyValue = localStorage.getItem(legacyKey);
    if (legacyValue !== null) {
      localStorage.setItem(targetKey, legacyValue);
    }
  }

  private handleRefreshFailure(error: unknown): void {
    const refreshError = error instanceof TokenRefreshError
      ? error
      : new TokenRefreshError(
          error instanceof Error ? error.message : String(error),
          classifyTokenRefreshFailure(undefined, error),
        );

    log.error('TokenService', 'Token 刷新失败', {
      reason: refreshError.reason,
      status: refreshError.status,
      message: refreshError.message,
    });

    if (!shouldInvalidateSessionAfterRefreshFailure(refreshError.reason, this.isTokenExpired())) {
      return;
    }

    this.clearTokens();
    dispatchAuthTokenExpired(refreshError.reason);
  }
}

export const tokenService = TokenService.getInstance();
