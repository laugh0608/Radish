import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import i18n from '@/i18n';
import { parseApiResponse, type ApiResponse } from '@/api/client';
import { useUserStore } from '@/stores/userStore';
import styles from './AuthTestApp.module.css';

interface CurrentUser {
  userId: number;
  userName: string;
  tenantId: number;
}

// 默认通过 Gateway 暴露的 API 入口
const defaultApiBase = 'https://localhost:5000';

/**
 * 认证测试应用
 *
 * 专注展示当前登录状态和用户信息
 */
export const AuthTestApp = () => {
  const { t } = useTranslation();

  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
  const [userError, setUserError] = useState<string>();

  const { roles, isAuthenticated } = useUserStore();

  const apiBaseUrl = useMemo(() => {
    const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
    return (configured ?? defaultApiBase).replace(/\/$/, '');
  }, []);

  const isBrowser = typeof window !== 'undefined';

  useEffect(() => {
    void populateCurrentUser();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiBaseUrl]);

  const loggedIn = isAuthenticated();

  return (
    <div className={styles.container}>
      <div className={styles.langButtons}>
        <button type="button" onClick={() => i18n.changeLanguage('zh')}>
          {t('lang.zh')}
        </button>
        <button type="button" onClick={() => i18n.changeLanguage('en')}>
          {t('lang.en')}
        </button>
      </div>

      <h1 className={styles.title}>{t('app.title')}</h1>
      <p className={styles.description}>{t('app.description')}</p>

      <section className={styles.section}>
        <h2>{t('auth.sectionTitle')}</h2>
        <div className={styles.authSection}>
          <div className={styles.buttonGroup}>
            <button type="button" onClick={() => handleLogin(apiBaseUrl)}>
              {loggedIn ? t('auth.relogin') : t('auth.login')}
            </button>
            {loggedIn && (
              <button type="button" onClick={() => handleLogout(apiBaseUrl)}>
                退出登录
              </button>
            )}
          </div>

          <div className={styles.userInfo}>
            <div className={styles.userInfoRow}>
              <span className={styles.userInfoLabel}>登录状态：</span>
              <span className={styles.userInfoValue}>{loggedIn ? '已登录' : '未登录'}</span>
            </div>

            {currentUser && (
              <>
                <div className={styles.userInfoRow}>
                  <span className={styles.userInfoLabel}>用户名：</span>
                  <span className={styles.userInfoValue}>{currentUser.userName}</span>
                </div>
                <div className={styles.userInfoRow}>
                  <span className={styles.userInfoLabel}>用户 ID：</span>
                  <span className={styles.userInfoValue}>{currentUser.userId}</span>
                </div>
                <div className={styles.userInfoRow}>
                  <span className={styles.userInfoLabel}>租户 ID：</span>
                  <span className={styles.userInfoValue}>{currentUser.tenantId}</span>
                </div>
              </>
            )}

            <div className={styles.userInfoRow}>
              <span className={styles.userInfoLabel}>角色：</span>
              <span className={styles.userInfoValue}>
                {roles && roles.length > 0 ? roles.join(', ') : '无（或未从服务端加载）'}
              </span>
            </div>

            {userError && (
              <div className={styles.error}>
                {t('auth.userInfoLoadFailedPrefix')}
                {userError}
              </div>
            )}

            {!currentUser && !userError && (
              <div className={styles.userInfoRow}>
                <span className={styles.userInfoLabel}>当前用户：</span>
                <span className={styles.userInfoValue}>{t('auth.notLoggedIn')}</span>
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  );

  async function populateCurrentUser() {
    if (!isBrowser) {
      return;
    }

    const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;
    try {
      const response = await apiFetch(requestUrl, { withAuth: true });

      const json = await response.json() as ApiResponse<CurrentUser>;
      const parsed = parseApiResponse(json, t);

      if (!parsed.ok || !parsed.data) {
        throw new Error(parsed.message || t('auth.userInfoLoadFailedPrefix'));
      }

      setCurrentUser(parsed.data);
      setUserError(undefined);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setUserError(message);
      setCurrentUser(null);
    }
  }
};

interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    'Accept-Language': i18n.language || 'zh',
    ...headers,
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders,
  });
}

function handleLogin(apiBaseUrl: string) {
  if (typeof window === 'undefined') {
    return;
  }

  const redirectUri = `${window.location.origin}/oidc/callback`;

  const authorizeUrl = new URL(`${apiBaseUrl}/connect/authorize`);
  authorizeUrl.searchParams.set('client_id', 'radish-client');
  authorizeUrl.searchParams.set('response_type', 'code');
  authorizeUrl.searchParams.set('redirect_uri', redirectUri);
  authorizeUrl.searchParams.set('scope', 'radish-api');

  const currentLanguage = i18n.language || 'zh';
  authorizeUrl.searchParams.set('culture', currentLanguage);
  authorizeUrl.searchParams.set('ui-culture', currentLanguage);

  window.location.href = authorizeUrl.toString();
}

function handleLogout(apiBaseUrl: string) {
  if (typeof window === 'undefined') {
    return;
  }

  // 清理本地保存的 Token
  window.localStorage.removeItem('access_token');
  window.localStorage.removeItem('refresh_token');

  const currentLanguage = i18n.language || 'zh';
  const logoutUrl = new URL(`${apiBaseUrl}/Account/Logout`);
  logoutUrl.searchParams.set('culture', currentLanguage);

  // 调用 Auth 的 Logout，并在完成后回到首页
  void fetch(logoutUrl.toString(), {
    method: 'POST',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'Accept-Language': currentLanguage
    }
  }).catch(() => {
    // 忽略登出接口错误
  }).finally(() => {
    window.location.replace('/');
  });
}
