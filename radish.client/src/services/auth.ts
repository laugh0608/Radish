/**
 * OIDC 认证服务
 * 统一管理登录/退出逻辑
 */
import i18n from '@/i18n';
import { getAuthBaseUrl } from '@/config/env';

const CLIENT_ID = 'radish-client';

/**
 * 跳转到 OIDC 登录页面
 */
export function redirectToLogin(): void {
  if (typeof window === 'undefined') return;

  const redirectUri = `${window.location.origin}/oidc/callback`;
  const authServerBaseUrl = getAuthBaseUrl();

  const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
  authorizeUrl.searchParams.set('client_id', CLIENT_ID);
  authorizeUrl.searchParams.set('response_type', 'code');
  authorizeUrl.searchParams.set('redirect_uri', redirectUri);
  authorizeUrl.searchParams.set('scope', 'radish-api');

  // 传递当前语言设置
  const currentLanguage = i18n.language || 'zh';
  authorizeUrl.searchParams.set('culture', currentLanguage);
  authorizeUrl.searchParams.set('ui-culture', currentLanguage);

  window.location.href = authorizeUrl.toString();
}

/**
 * 执行 OIDC 登出
 * 清除本地 Token 并跳转到认证服务器的 endsession 端点
 */
export function logout(): void {
  if (typeof window === 'undefined') return;

  // 清理本地 Token
  window.localStorage.removeItem('access_token');
  window.localStorage.removeItem('refresh_token');
  window.localStorage.removeItem('cached_user_info');

  // 跳转到 OIDC endsession 端点
  const postLogoutRedirectUri = window.location.origin;
  const authServerBaseUrl = getAuthBaseUrl();

  const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
  logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
  logoutUrl.searchParams.set('client_id', CLIENT_ID);

  // 传递当前语言设置
  const currentLanguage = i18n.language || 'zh';
  logoutUrl.searchParams.set('culture', currentLanguage);

  window.location.href = logoutUrl.toString();
}

/**
 * 检查是否有有效的 access_token
 */
export function hasAccessToken(): boolean {
  if (typeof window === 'undefined') return false;
  try {
    return Boolean(window.localStorage.getItem('access_token'));
  } catch {
    return false;
  }
}
