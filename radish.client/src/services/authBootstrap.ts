import { configureTokenRefresh, TokenRefreshErrorType } from '@radish/http';
import { parseApiResponse, type ApiResponse } from '@radish/ui';
import { getAuthBaseUrl } from '@/config/env';
import { redirectToLogin } from '@/services/auth';
import { tokenService } from '@/services/tokenService';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import i18n from '@/i18n';
import { log } from '@/utils/logger';

export interface CurrentUser {
  voUserId: number;
  voUserName: string;
  voTenantId: number;
  voAvatarUrl?: string;
  voAvatarThumbnailUrl?: string;
}

export interface AuthBootstrapOptions {
  apiBaseUrl: string;
  onUserLoaded?: (user: CurrentUser) => void;
  onUserLoadFailed?: (error: Error) => void;
  useCache?: boolean;
}

function setUserFromCurrentUser(user: CurrentUser) {
  const { setUser } = useUserStore.getState();
  setUser({
    userId: typeof user.voUserId === 'string' ? parseInt(user.voUserId, 10) : user.voUserId,
    userName: user.voUserName,
    tenantId: typeof user.voTenantId === 'string' ? parseInt(user.voTenantId, 10) : user.voTenantId,
    roles: ['User'],
    avatarUrl: user.voAvatarUrl,
    avatarThumbnailUrl: user.voAvatarThumbnailUrl
  });
}

async function fetchCurrentUser(apiBaseUrl: string, token: string): Promise<CurrentUser> {
  const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;
  const response = await fetch(requestUrl, {
    headers: {
      Accept: 'application/json',
      'Accept-Language': i18n.language || 'zh',
      Authorization: `Bearer ${token}`
    }
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`);
  }

  const json = (await response.json()) as ApiResponse<CurrentUser>;
  const parsed = parseApiResponse(json);
  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '用户信息解析失败');
  }

  return parsed.data;
}

export async function hydrateAuthUser(options: AuthBootstrapOptions): Promise<CurrentUser | null> {
  if (typeof window === 'undefined') {
    return null;
  }

  const { apiBaseUrl, onUserLoaded, onUserLoadFailed, useCache = true } = options;
  const authStore = useAuthStore.getState();
  const userStore = useUserStore.getState();

  const token = tokenService.getAccessToken();
  if (!token) {
    authStore.setAuthenticated(false);
    return null;
  }

  if (useCache) {
    const cachedUserInfo = window.localStorage.getItem('cached_user_info');
    if (cachedUserInfo) {
      try {
        const userData = JSON.parse(cachedUserInfo) as CurrentUser;
        if (userData.voUserId && userData.voUserName) {
          setUserFromCurrentUser(userData);
          authStore.setAuthenticated(true);
          window.localStorage.removeItem('cached_user_info');
          onUserLoaded?.(userData);
          log.info('AuthBootstrap', '✅ 使用缓存用户信息完成初始化');
          return userData;
        }
      } catch (error) {
        log.error('AuthBootstrap', '缓存用户信息解析失败:', error);
      } finally {
        window.localStorage.removeItem('cached_user_info');
      }
    }
  }

  try {
    const userData = await fetchCurrentUser(apiBaseUrl, token);
    setUserFromCurrentUser(userData);
    authStore.setAuthenticated(true);
    onUserLoaded?.(userData);
    log.info('AuthBootstrap', '✅ 用户信息初始化完成');
    return userData;
  } catch (error) {
    const err = error instanceof Error ? error : new Error(String(error));
    userStore.clearUser();
    authStore.setAuthenticated(false);
    onUserLoadFailed?.(err);
    log.warn('AuthBootstrap', '用户信息初始化失败:', err.message);
    return null;
  }
}

export function bootstrapAuth(options: AuthBootstrapOptions): () => void {
  if (typeof window === 'undefined') {
    return () => {};
  }

  const authStore = useAuthStore.getState();
  const authServerBaseUrl = getAuthBaseUrl();

  configureTokenRefresh({
    refreshEndpoint: `${authServerBaseUrl}/connect/token`,
    getRefreshToken: () => {
      if (typeof window === 'undefined') return null;
      return window.localStorage.getItem('refresh_token');
    },
    onTokenRefreshed: (accessToken, refreshToken) => {
      if (typeof window === 'undefined') return;
      window.localStorage.setItem('access_token', accessToken);
      if (refreshToken) {
        window.localStorage.setItem('refresh_token', refreshToken);
      }
      log.info('AuthBootstrap', '✅ Token 自动刷新成功');
      authStore.setAuthenticated(true);
    },
    onRefreshFailed: (errorType, error) => {
      log.error('AuthBootstrap', '❌ Token 刷新失败:', errorType, error.message);
      if (errorType === TokenRefreshErrorType.InvalidRefreshToken) {
        authStore.logout();
        redirectToLogin();
      }
    }
  });

  const existingToken = tokenService.getAccessToken();
  if (existingToken) {
    authStore.setAuthenticated(true);
  }

  const handleTokenExpired = () => {
    log.warn('AuthBootstrap', '收到 Token 过期事件，执行登出');
    authStore.logout();
  };
  window.addEventListener('auth:token-expired', handleTokenExpired);

  tokenService.startAutoRefresh();
  void hydrateAuthUser(options);

  return () => {
    window.removeEventListener('auth:token-expired', handleTokenExpired);
    tokenService.stopAutoRefresh();
  };
}
