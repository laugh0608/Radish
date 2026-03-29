import { configureApiClient, configureTokenRefresh, TokenRefreshErrorType } from '@radish/http';
import { parseApiResponse, type ApiResponse } from '@radish/http';
import { getAuthBaseUrl } from '@/config/env';
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
  voRoleNames?: string[];
  voRoles?: string[];
  voPermissions?: string[];
  permissions?: string[];
}

export interface AuthBootstrapOptions {
  apiBaseUrl: string;
  onUserLoaded?: (user: CurrentUser) => void;
  onUserLoadFailed?: (error: Error) => void;
  useCache?: boolean;
}

const inFlightAuthHydrations = new Map<string, Promise<CurrentUser>>();

class CurrentUserRequestError extends Error {
  readonly status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = 'CurrentUserRequestError';
    this.status = status;
  }
}

function resolveUserRoles(user: CurrentUser, token?: string | null): string[] {
  const tokenRoles = tokenService.getRolesFromAccessToken(token);
  if (tokenRoles.length > 0) {
    return tokenRoles;
  }

  const userRoles = [
    ...(Array.isArray(user.voRoleNames) ? user.voRoleNames : []),
    ...(Array.isArray(user.voRoles) ? user.voRoles : []),
  ]
    .map((role) => role.trim())
    .filter(Boolean);

  if (userRoles.length > 0) {
    return userRoles;
  }

  return ['User'];
}

function setUserFromCurrentUser(user: CurrentUser, token?: string | null) {
  const { setUser } = useUserStore.getState();
  setUser({
    userId: typeof user.voUserId === 'string' ? parseInt(user.voUserId, 10) : user.voUserId,
    userName: user.voUserName,
    tenantId: typeof user.voTenantId === 'string' ? parseInt(user.voTenantId, 10) : user.voTenantId,
    roles: resolveUserRoles(user, token),
    permissions: [
      ...(Array.isArray(user.voPermissions) ? user.voPermissions : []),
      ...(Array.isArray(user.permissions) ? user.permissions : []),
    ]
      .map((permission) => permission.trim())
      .filter(Boolean),
    avatarUrl: user.voAvatarUrl,
    avatarThumbnailUrl: user.voAvatarThumbnailUrl
  });
}

function buildCurrentUserFromStore(): CurrentUser | null {
  const userStore = useUserStore.getState();
  if (!userStore.isAuthenticated()) {
    return null;
  }

  return {
    voUserId: userStore.userId,
    voUserName: userStore.userName,
    voTenantId: userStore.tenantId,
    voAvatarUrl: userStore.avatarUrl,
    voAvatarThumbnailUrl: userStore.avatarThumbnailUrl,
    voRoles: [...(userStore.roles || [])],
    voPermissions: [...(userStore.permissions || [])],
  };
}

function buildCurrentUserFromToken(token?: string | null): CurrentUser | null {
  const identity = tokenService.getUserIdentityFromAccessToken(token);
  if (!identity) {
    return null;
  }

  const currentStoreUser = useUserStore.getState();
  const sameUser = currentStoreUser.userId > 0 && currentStoreUser.userId === identity.userId;

  return {
    voUserId: identity.userId,
    voUserName: identity.userName,
    voTenantId: identity.tenantId,
    voAvatarUrl: sameUser ? currentStoreUser.avatarUrl : undefined,
    voAvatarThumbnailUrl: sameUser ? currentStoreUser.avatarThumbnailUrl : undefined,
    voRoles: identity.roles.length > 0 ? identity.roles : [...(sameUser ? currentStoreUser.roles || [] : [])],
    voPermissions: [...(sameUser ? currentStoreUser.permissions || [] : [])],
  };
}

function seedAuthFromToken(token?: string | null): CurrentUser | null {
  const tokenUser = buildCurrentUserFromToken(token);
  if (!tokenUser) {
    return null;
  }

  setUserFromCurrentUser(tokenUser, token);
  useAuthStore.getState().setAuthenticated(true);
  return tokenUser;
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
    throw new CurrentUserRequestError(`HTTP ${response.status} ${response.statusText}`, response.status);
  }

  const json = (await response.json()) as ApiResponse<CurrentUser>;
  const parsed = parseApiResponse(json);
  if (!parsed.ok || !parsed.data) {
    throw new CurrentUserRequestError(parsed.message || '用户信息解析失败', response.status);
  }

  return parsed.data;
}

function buildHydrationKey(apiBaseUrl: string, token: string): string {
  return `${apiBaseUrl}|${token}`;
}

async function hydrateAuthUserCore(
  apiBaseUrl: string,
  token: string,
  useCache: boolean,
): Promise<CurrentUser> {
  const authStore = useAuthStore.getState();
  const userStore = useUserStore.getState();

  if (useCache) {
    const cachedUserInfo = window.localStorage.getItem('cached_user_info');
    if (cachedUserInfo) {
      try {
        const userData = JSON.parse(cachedUserInfo) as CurrentUser;
        if (userData.voUserId && userData.voUserName) {
          setUserFromCurrentUser(userData, token);
          authStore.setAuthenticated(true);
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
    window.localStorage.setItem('cached_user_info', JSON.stringify(userData));
    setUserFromCurrentUser(userData, token);
    authStore.setAuthenticated(true);
    log.info('AuthBootstrap', '✅ 用户信息初始化完成');
    return userData;
  } catch (error) {
    const err = error instanceof Error ? error : new Error(String(error));
    if (error instanceof CurrentUserRequestError && (error.status === 401 || error.status === 403)) {
      userStore.clearUser();
      authStore.setAuthenticated(false);
      log.warn('AuthBootstrap', '用户信息初始化失败，访问令牌已失效:', err.message);
      throw err;
    }

    const currentStoreUser = buildCurrentUserFromStore();
    if (currentStoreUser) {
      authStore.setAuthenticated(true);
      log.warn('AuthBootstrap', '用户信息初始化失败，保留现有登录态:', err.message);
      return currentStoreUser;
    }

    const tokenUser = seedAuthFromToken(token);
    if (tokenUser) {
      log.warn('AuthBootstrap', '用户信息初始化失败，已回退到 Token 基础登录态:', err.message);
      return tokenUser;
    }

    userStore.clearUser();
    authStore.setAuthenticated(false);
    log.warn('AuthBootstrap', '用户信息初始化失败且无法回退基础登录态:', err.message);
    throw err;
  }
}

export async function hydrateAuthUser(options: AuthBootstrapOptions): Promise<CurrentUser | null> {
  if (typeof window === 'undefined') {
    return null;
  }

  const { apiBaseUrl, onUserLoaded, onUserLoadFailed, useCache = true } = options;
  const authStore = useAuthStore.getState();

  const token = await tokenService.getValidAccessToken();
  if (!token) {
    authStore.setAuthenticated(false);
    return null;
  }

  seedAuthFromToken(token);

  const hydrationKey = buildHydrationKey(apiBaseUrl, token);
  let hydrationPromise = inFlightAuthHydrations.get(hydrationKey);

  if (!hydrationPromise) {
    hydrationPromise = hydrateAuthUserCore(apiBaseUrl, token, useCache).finally(() => {
      const currentPromise = inFlightAuthHydrations.get(hydrationKey);
      if (currentPromise === hydrationPromise) {
        inFlightAuthHydrations.delete(hydrationKey);
      }
    });
    inFlightAuthHydrations.set(hydrationKey, hydrationPromise);
  } else {
    log.debug('AuthBootstrap', '复用进行中的用户信息初始化请求');
  }

  try {
    const userData = await hydrationPromise;
    onUserLoaded?.(userData);
    return userData;
  } catch (error) {
    const err = error instanceof Error ? error : new Error(String(error));
    onUserLoadFailed?.(err);
    return null;
  }
}

export function bootstrapAuth(options: AuthBootstrapOptions): () => void {
  if (typeof window === 'undefined') {
    return () => {};
  }

  const authStore = useAuthStore.getState();
  const authServerBaseUrl = getAuthBaseUrl();

  configureApiClient({
    getToken: () => tokenService.getAccessToken(),
  });

  configureTokenRefresh({
    refreshEndpoint: `${authServerBaseUrl}/connect/token`,
    getRefreshToken: () => tokenService.getRefreshToken(),
    onTokenRefreshed: (accessToken, refreshToken) => {
      if (typeof window === 'undefined') return;
      tokenService.setTokenInfoFromJwt(accessToken, refreshToken);
      seedAuthFromToken(accessToken);
      log.info('AuthBootstrap', '✅ Token 自动刷新成功');
      authStore.setAuthenticated(true);
    },
    onRefreshFailed: (errorType, error) => {
      log.error('AuthBootstrap', '❌ Token 刷新失败:', errorType, error.message);
      if (errorType === TokenRefreshErrorType.InvalidRefreshToken) {
        authStore.logout();
      }
    }
  });

  const existingToken = tokenService.getAccessToken();
  if (existingToken) {
    const seededUser = seedAuthFromToken(existingToken);
    if (!seededUser) {
      authStore.setAuthenticated(true);
    }
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
