import { log } from '@/utils/logger';
import { getAuthServerBaseUrl } from '@/config/env';
import { getUserNameFromTokenPayload, type JwtPayload } from './tokenClaims';

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

type SessionClearReason = 'idle_session_expired';

export interface TokenDebugInfo {
  hasAccessToken: boolean;
  accessTokenPreview: string | null;
  accessTokenLength: number;
  hasRefreshToken: boolean;
  refreshTokenPreview: string | null;
  refreshTokenLength: number;
  expiresAtTimestamp: number | null;
  expiresAtIso: string | null;
  remainingSeconds: number | null;
  isExpiringSoon: boolean;
  isExpired: boolean;
  lastActiveAtTimestamp: number | null;
  lastActiveAtIso: string | null;
  isIdleSessionExpired: boolean;
  hasRefreshInProgress: boolean;
}

const IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY = 'radish_console_session_last_active_at';
const IDLE_SESSION_REFRESH_PARAMETER = 'radish_last_active_at';
const DEFAULT_IDLE_SESSION_TIMEOUT_MS = 7 * 24 * 60 * 60 * 1000;
const DEFAULT_IDLE_SESSION_CLOCK_SKEW_MS = 60 * 1000;
const DEFAULT_ACTIVITY_WRITE_THROTTLE_MS = 60_000;

/**
 * Token 管理服务
 *
 * 负责 Token 的存储、刷新和过期检测
 */
class TokenService {
  private static instance: TokenService;
  private refreshPromise: Promise<string> | null = null;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private missingRefreshTokenWarned = false;
  private lastSessionClearReason: SessionClearReason | null = null;
  private readonly AUTO_REFRESH_CHECK_INTERVAL_MS = 30000;
  private readonly TOKEN_KEY = 'radish_console_access_token';
  private readonly REFRESH_TOKEN_KEY = 'radish_console_refresh_token';
  private readonly TOKEN_EXPIRES_KEY = 'radish_console_token_expires_at';

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
   * 从 Access Token 中解析用户名（用于接口兜底）
   */
  getUserNameFromAccessToken(token?: string | null): string | null {
    const targetToken = token ?? this.getAccessToken();
    if (!targetToken) {
      return null;
    }

    const payload = this.parseJwt(targetToken);
    if (!payload) {
      return null;
    }

    return getUserNameFromTokenPayload(payload);
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

  private maskToken(token: string | null): string | null {
    if (!token) {
      return null;
    }

    if (token.length <= 14) {
      return `${token.slice(0, 4)}...${token.slice(-4)}`;
    }

    return `${token.slice(0, 8)}...${token.slice(-6)}`;
  }

  private formatExpiresAt(expiresAt: number | null): string | null {
    if (!expiresAt || !Number.isFinite(expiresAt)) {
      return null;
    }

    return new Date(expiresAt).toISOString();
  }

  private formatEpochSeconds(epochSeconds: number | null): string | null {
    if (!epochSeconds || !Number.isFinite(epochSeconds)) {
      return null;
    }

    return new Date(epochSeconds * 1000).toISOString();
  }

  private getRemainingSeconds(expiresAt: number | null): number | null {
    if (!expiresAt || !Number.isFinite(expiresAt)) {
      return null;
    }

    return Math.max(0, Math.floor((expiresAt - Date.now()) / 1000));
  }

  getTokenDebugInfo(): TokenDebugInfo {
    const accessToken = this.getAccessToken();
    const refreshToken = this.getRefreshToken();
    const expiresAt = this.getTokenExpiresAt();
    const remainingSeconds = this.getRemainingSeconds(expiresAt);
    const lastActiveAt = this.getSessionLastActiveAt();

    return {
      hasAccessToken: !!accessToken,
      accessTokenPreview: this.maskToken(accessToken),
      accessTokenLength: accessToken?.length ?? 0,
      hasRefreshToken: !!refreshToken,
      refreshTokenPreview: this.maskToken(refreshToken),
      refreshTokenLength: refreshToken?.length ?? 0,
      expiresAtTimestamp: expiresAt,
      expiresAtIso: this.formatExpiresAt(expiresAt),
      remainingSeconds,
      isExpiringSoon: this.isTokenExpiringSoon(),
      isExpired: this.isTokenExpired(),
      lastActiveAtTimestamp: lastActiveAt,
      lastActiveAtIso: this.formatEpochSeconds(lastActiveAt),
      isIdleSessionExpired: this.isIdleSessionExpired(),
      hasRefreshInProgress: this.refreshPromise !== null,
    };
  }

  logTokenDebug(stage: string): void {
    log.debug('TokenService', `Token 状态快照 (${stage})`, this.getTokenDebugInfo());
  }

  getLastSessionClearReason(): SessionClearReason | null {
    return this.lastSessionClearReason;
  }

  /**
   * 存储 Token 信息
   */
  setTokenInfo(tokenInfo: TokenInfo): void {
    localStorage.setItem(this.TOKEN_KEY, tokenInfo.access_token);

    if (tokenInfo.refresh_token) {
      localStorage.setItem(this.REFRESH_TOKEN_KEY, tokenInfo.refresh_token);
    }

    // 存储真实过期时间，刷新窗口由 isTokenExpiringSoon 控制
    const expiresAt = Date.now() + Math.max(0, tokenInfo.expires_in) * 1000;
    localStorage.setItem(this.TOKEN_EXPIRES_KEY, expiresAt.toString());
    this.missingRefreshTokenWarned = false;
    this.lastSessionClearReason = null;

    log.debug('TokenService', 'Token 信息已更新', {
      expires_in: tokenInfo.expires_in,
      expires_at: new Date(expiresAt).toISOString(),
    });
    this.logTokenDebug('setTokenInfo');
  }

  /**
   * 清除所有 Token 信息
   */
  clearTokens(reason?: SessionClearReason): void {
    const beforeClear = this.getTokenDebugInfo();
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRES_KEY);
    this.clearSessionActivity();
    this.refreshPromise = null;
    this.missingRefreshTokenWarned = false;
    this.lastSessionClearReason = reason ?? null;
    this.stopAutoRefresh();
    log.debug('TokenService', 'Token 信息已清除', {
      before: beforeClear,
      after: this.getTokenDebugInfo(),
    });
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

  recordSessionActivity(force = false, nowMs = Date.now()): number | null {
    const currentSeconds = Math.floor(nowMs / 1000);
    const previousSeconds = this.getSessionLastActiveAt();

    if (!force && previousSeconds !== null && nowMs - previousSeconds * 1000 < DEFAULT_ACTIVITY_WRITE_THROTTLE_MS) {
      return previousSeconds;
    }

    localStorage.setItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY, String(currentSeconds));
    return currentSeconds;
  }

  clearSessionActivity(): void {
    localStorage.removeItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY);
  }

  isIdleSessionExpired(nowMs = Date.now()): boolean {
    if (!this.getAccessToken()) {
      return false;
    }

    const lastActiveAt = this.getSessionLastActiveAt();
    if (lastActiveAt === null) {
      return false;
    }

    return nowMs > lastActiveAt * 1000 + DEFAULT_IDLE_SESSION_TIMEOUT_MS + DEFAULT_IDLE_SESSION_CLOCK_SKEW_MS;
  }

  getRefreshRequestParameters(): Record<string, string> {
    const lastActiveAt = this.getSessionLastActiveAt();
    return lastActiveAt === null
      ? {}
      : { [IDLE_SESSION_REFRESH_PARAMETER]: String(lastActiveAt) };
  }

  startActivityTracking(onIdleExpired?: () => void): () => void {
    if (typeof window === 'undefined' || typeof document === 'undefined') {
      return () => {};
    }

    const handleActivity = () => {
      if (document.visibilityState === 'hidden') {
        return;
      }

      if (!this.getAccessToken()) {
        this.clearSessionActivity();
        return;
      }

      if (this.isIdleSessionExpired()) {
        onIdleExpired?.();
        return;
      }

      this.recordSessionActivity();
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        handleActivity();
      }
    };

    const passiveOptions: AddEventListenerOptions = { passive: true };
    window.addEventListener('focus', handleActivity);
    window.addEventListener('pageshow', handleActivity);
    window.addEventListener('pointerdown', handleActivity, passiveOptions);
    window.addEventListener('keydown', handleActivity);
    window.addEventListener('scroll', handleActivity, passiveOptions);
    window.addEventListener('touchstart', handleActivity, passiveOptions);
    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      window.removeEventListener('focus', handleActivity);
      window.removeEventListener('pageshow', handleActivity);
      window.removeEventListener('pointerdown', handleActivity);
      window.removeEventListener('keydown', handleActivity);
      window.removeEventListener('scroll', handleActivity);
      window.removeEventListener('touchstart', handleActivity);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }

  /**
   * 刷新访问令牌
   */
  async refreshAccessToken(): Promise<string> {
    // 如果已经有刷新请求在进行中，返回同一个 Promise
    if (this.refreshPromise) {
      log.debug('TokenService', '复用进行中的 Token 刷新请求');
      return this.refreshPromise;
    }

    if (this.isIdleSessionExpired()) {
      this.clearTokens('idle_session_expired');
      throw new Error('会话因长时间未使用已过期，请重新登录');
    }

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('没有可用的刷新令牌');
    }

    this.logTokenDebug('refreshAccessToken:start');
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
          ...this.getRefreshRequestParameters(),
        }),
      });

      if (!response.ok) {
        const details = await this.extractTokenRefreshErrorDetails(response);
        throw new Error(
          details
            ? `Token 刷新失败: ${response.status} ${response.statusText} (${details})`
            : `Token 刷新失败: ${response.status} ${response.statusText}`,
        );
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
      const clearReason = error instanceof Error && error.message.includes('session_idle_expired')
        ? 'idle_session_expired'
        : undefined;
      this.clearTokens(clearReason);

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

    if (this.isIdleSessionExpired()) {
      log.warn('TokenService', '会话因长时间未使用已过期，清理会话并触发重新登录', this.getTokenDebugInfo());
      this.clearTokens('idle_session_expired');
      return null;
    }

    // 如果 Token 即将过期，尝试刷新
    if (this.isTokenExpiringSoon()) {
      this.logTokenDebug('getValidAccessToken:expiringSoon');
      const refreshToken = this.getRefreshToken();
      if (!refreshToken) {
        // 某些会话不会下发 refresh_token：在 access_token 真过期前继续使用当前 token
        if (!this.isTokenExpired()) {
          if (!this.missingRefreshTokenWarned) {
            this.missingRefreshTokenWarned = true;
            log.warn(
              'TokenService',
              '未获取到 refresh_token，将在 access_token 过期前继续使用当前令牌',
              this.getTokenDebugInfo(),
            );
          }

          return currentToken;
        }

        log.warn(
          'TokenService',
          'access_token 已过期且无 refresh_token，清理会话并触发重新登录',
          this.getTokenDebugInfo(),
        );
        this.clearTokens();
        return null;
      }

      try {
        return await this.refreshAccessToken();
      } catch (error) {
        log.error('TokenService', '自动刷新 Token 失败', error);
        return null;
      }
    }

    return currentToken;
  }

  startAutoRefresh(): void {
    if (this.refreshTimer) {
      this.stopAutoRefresh();
    }

    log.debug('TokenService', '启动自动刷新定时器');

    const runCheck = async () => {
      if (!this.getAccessToken()) {
        return;
      }

      if (this.isIdleSessionExpired()) {
        log.warn('TokenService', '自动刷新检测到不活跃会话过期，停止自动刷新');
        this.clearTokens('idle_session_expired');
        return;
      }

      if (!this.isTokenExpiringSoon()) {
        return;
      }

      log.debug('TokenService', 'Token 即将过期，自动刷新');
      try {
        await this.refreshAccessToken();
        log.debug('TokenService', '自动刷新成功');
      } catch (error) {
        log.error('TokenService', '自动刷新失败', error);
      }
    };

    void runCheck();
    this.refreshTimer = setInterval(() => {
      void runCheck();
    }, this.AUTO_REFRESH_CHECK_INTERVAL_MS);
  }

  stopAutoRefresh(): void {
    if (!this.refreshTimer) {
      return;
    }

    clearInterval(this.refreshTimer);
    this.refreshTimer = null;
    log.debug('TokenService', '停止自动刷新定时器');
  }

  private parseJwt(token: string): JwtPayload | null {
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

  private getSessionLastActiveAt(): number | null {
    const value = localStorage.getItem(IDLE_SESSION_LAST_ACTIVE_STORAGE_KEY);
    if (!value) {
      return null;
    }

    const parsed = Number.parseInt(value.trim(), 10);
    return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
  }

  private async extractTokenRefreshErrorDetails(response: Response): Promise<string | null> {
    try {
      const text = await response.text();
      if (!text.trim()) {
        return null;
      }

      try {
        const parsed = JSON.parse(text) as { error?: string; error_description?: string };
        return [parsed.error, parsed.error_description].filter(Boolean).join(': ') || text;
      } catch {
        return text;
      }
    } catch {
      return null;
    }
  }
}

/**
 * 导出单例实例
 */
export const tokenService = TokenService.getInstance();
