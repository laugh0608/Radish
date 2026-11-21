/**
 * OIDC 认证服务
 * 统一管理登录/退出逻辑
 */
import i18n from '@/i18n';
import { createOidcAuthorizationUrl } from '@radish/http';
import { toast } from '@radish/ui/toast';
import { getAuthBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';
import { useAuthStore } from '@/stores/authStore';
import {
  getOidcRedirectUri,
  getPostLogoutRedirectUri,
  isTauriRuntime,
  openExternalUrl,
  prepareOidcRedirectUri,
  preparePostLogoutRedirectUri,
} from '@/platform/tauriBridge';
import { normalizeAuthReturnPath, rememberAuthReturnPath } from './authReturnPath';
import { browserWebSession } from './browserWebSession';
import { log } from '@/utils/logger';

const CLIENT_ID = 'radish-client';

interface RedirectToLoginOptions {
  returnPath?: string | null;
  onUnavailable?: () => void;
}

function navigateToWebAuthResult(value: string | null, allowedPath: string, allowHome = false): boolean {
  if (!value) return false;
  try {
    const url = new URL(value, window.location.origin);
    if (url.origin !== window.location.origin || (url.pathname !== allowedPath && !(allowHome && url.pathname === '/'))) return false;

    window.location.assign(url.href);
    return true;
  } catch {
    return false;
  }
}

/**
 * 跳转到 OIDC 登录页面
 */
export function redirectToLogin(options: RedirectToLoginOptions = {}): void {
  if (typeof window === 'undefined') return;

  const startLogin = async () => {
    const mode = await tokenService.resolveSessionMode();
    if (mode !== 'legacy') {
      const currentPath = window.location.pathname + window.location.search + window.location.hash;
      const returnPath = normalizeAuthReturnPath(options.returnPath ?? currentPath) ?? '/';
      const next = mode === 'cookie' ? await browserWebSession.login(returnPath) : null;
      if (!navigateToWebAuthResult(next, '/connect/authorize')) {
        useAuthStore.getState().setSessionRecovering(true);
        options.onUnavailable?.();
        toast.error(i18n.t('error.common.internal_error'));
      }
      return;
    }

    const redirectUri = isTauriRuntime() ? await prepareOidcRedirectUri() : getOidcRedirectUri();
    const authServerBaseUrl = getAuthBaseUrl();

    // 传递当前语言设置
    const currentLanguage = i18n.language || 'zh';
    const authorizeUrl = await createOidcAuthorizationUrl({
      clientId: CLIENT_ID,
      authServerBaseUrl,
      redirectUri,
      scope: 'openid profile offline_access radish-api',
      additionalParameters: {
        culture: currentLanguage,
        'ui-culture': currentLanguage,
      },
    });
    rememberAuthReturnPath(options.returnPath);

    if (isTauriRuntime()) {
      await openExternalUrl(authorizeUrl);
      return;
    }

    window.location.href = authorizeUrl;
  };

  void startLogin().catch((error: unknown) => {
    log.error('auth', '启动 OIDC 登录失败', error);
    if (tokenService.sessionMode !== 'legacy') useAuthStore.getState().setSessionRecovering(true);
    options.onUnavailable?.();
  });
}

/**
 * 执行 OIDC 登出
 * 清除本地 Token 并跳转到认证服务器的 endsession 端点
 */
export function logout(): void {
  if (typeof window === 'undefined') return;

  const startLogout = async () => {
    const mode = await tokenService.resolveSessionMode();
    if (mode !== 'legacy') {
      const next = mode === 'cookie' ? await browserWebSession.logout() : null;
      if (!navigateToWebAuthResult(next, '/connect/endsession', true)) {
        toast.error(i18n.t('error.common.internal_error'));
      }
      return;
    }

    // 清理本地 Token
    tokenService.clearTokens();

    // 跳转到 OIDC endsession 端点
    const postLogoutRedirectUri = isTauriRuntime()
      ? await preparePostLogoutRedirectUri()
      : getPostLogoutRedirectUri();
    const authServerBaseUrl = getAuthBaseUrl();

    const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
    logoutUrl.searchParams.set('client_id', CLIENT_ID);

    // 传递当前语言设置
    const currentLanguage = i18n.language || 'zh';
    logoutUrl.searchParams.set('culture', currentLanguage);

    if (isTauriRuntime()) {
      await openExternalUrl(logoutUrl.toString());
      return;
    }

    window.location.href = logoutUrl.toString();
  };

  void startLogout().catch((error: unknown) => {
    log.error('auth', '启动 OIDC 登出失败', error);
  });
}

/**
 * 检查是否有有效的 access_token
 */
export function hasAccessToken(): boolean {
  if (typeof window === 'undefined') return false;
  try {
    return Boolean(tokenService.getAccessToken());
  } catch {
    return false;
  }
}
