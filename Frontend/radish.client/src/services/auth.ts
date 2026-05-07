/**
 * OIDC 认证服务
 * 统一管理登录/退出逻辑
 */
import i18n from '@/i18n';
import { getAuthBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';
import {
  getOidcRedirectUri,
  getPostLogoutRedirectUri,
  isTauriRuntime,
  openExternalUrl,
  prepareOidcRedirectUri,
  preparePostLogoutRedirectUri,
} from '@/platform/tauriBridge';
import { log } from '@/utils/logger';

const CLIENT_ID = 'radish-client';

/**
 * 跳转到 OIDC 登录页面
 */
export function redirectToLogin(): void {
  if (typeof window === 'undefined') return;

  const startLogin = async () => {
    const redirectUri = isTauriRuntime() ? await prepareOidcRedirectUri() : getOidcRedirectUri();
    const authServerBaseUrl = getAuthBaseUrl();

    const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
    authorizeUrl.searchParams.set('client_id', CLIENT_ID);
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('redirect_uri', redirectUri);
    authorizeUrl.searchParams.set('scope', 'openid profile offline_access radish-api');

    // 传递当前语言设置
    const currentLanguage = i18n.language || 'zh';
    authorizeUrl.searchParams.set('culture', currentLanguage);
    authorizeUrl.searchParams.set('ui-culture', currentLanguage);

    if (isTauriRuntime()) {
      await openExternalUrl(authorizeUrl.toString());
      return;
    }

    window.location.href = authorizeUrl.toString();
  };

  void startLogin().catch((error: unknown) => {
    log.error('auth', '启动 OIDC 登录失败', error);
  });
}

/**
 * 执行 OIDC 登出
 * 清除本地 Token 并跳转到认证服务器的 endsession 端点
 */
export function logout(): void {
  if (typeof window === 'undefined') return;

  const startLogout = async () => {
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
