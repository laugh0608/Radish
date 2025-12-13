import { useState, useEffect, useMemo } from 'react';
import { useUserStore } from '@/stores/userStore';
import i18n from '@/i18n';
import type { ApiResponse } from '@/api/client';
import styles from './StatusBar.module.css';

/**
 * 状态栏组件
 *
 * 显示系统信息、用户信息和时间
 */
const defaultApiBase = 'https://localhost:5000';

export const StatusBar = () => {
  const { userName, userId, isAuthenticated, clearUser, setUser } = useUserStore();
  const [time, setTime] = useState(new Date());

  const apiBaseUrl = useMemo(() => {
    const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
    return (configured ?? defaultApiBase).replace(/\/$/, '');
  }, []);

  const loggedIn = isAuthenticated();

  interface CurrentUser {
    userId: number;
    userName: string;
    tenantId: number;
  }

  interface ApiFetchOptions extends RequestInit {
    withAuth?: boolean;
  }

  function apiFetch<T = unknown>(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
    const { withAuth, headers, ...rest } = options;

    const finalHeaders: HeadersInit = {
      Accept: 'application/json',
      'Accept-Language': i18n.language || 'zh',
      ...headers
    };

    if (withAuth && typeof window !== 'undefined') {
      const token = window.localStorage.getItem('access_token');
      if (token) {
        (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
      }
    }

    return fetch(input, {
      ...rest,
      headers: finalHeaders
    });
  }

  const handleLoginClick = () => {
    if (typeof window === 'undefined') return;

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
  };

  const handleLogoutClick = () => {
    if (typeof window === 'undefined') return;

    window.localStorage.removeItem('access_token');
    window.localStorage.removeItem('refresh_token');
    clearUser();
    window.location.replace('/');
  };

  // 从 API 恢复当前登录用户到 WebOS 全局状态
  const hydrateCurrentUser = async () => {
    if (typeof window === 'undefined') return;
    const token = window.localStorage.getItem('access_token');
    if (!token) {
      return;
    }

    const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;

    try {
      const response = await apiFetch<ApiResponse<CurrentUser>>(requestUrl, { withAuth: true });
      const json = await response.json();

      if (!json.isSuccess || !json.responseData) {
        throw new Error(json.messageInfo || '获取当前用户失败');
      }

      setUser({
        userId: json.responseData.userId,
        userName: json.responseData.userName,
        tenantId: json.responseData.tenantId,
        roles: ['User']
      });
    } catch {
      clearUser();
    }
  };

  useEffect(() => {
    const timer = setInterval(() => {
      setTime(new Date());
    }, 1000);

    // 如果本地已经有 access_token，则尝试从 API 恢复当前用户信息
    if (typeof window !== 'undefined') {
      void hydrateCurrentUser();
    }

    return () => clearInterval(timer);
  }, []);

  return (
    <div className={styles.statusBar}>
      <div className={styles.left}>
        <span className={styles.brand}>Radish OS</span>
        {loggedIn && userName && (
          <span className={styles.user}>
            👤 {userName} (ID: {userId})
          </span>
        )}
      </div>
      <div className={styles.right}>
        <button
          type="button"
          className={styles.authButton}
          onClick={loggedIn ? handleLogoutClick : handleLoginClick}
        >
          {loggedIn ? '退出登录' : '登录'}
        </button>
        <span className={styles.time}>
          {time.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
        </span>
      </div>
    </div>
  );
};
