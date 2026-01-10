import { useState, useEffect, useMemo } from 'react';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { notificationHub } from '@/services/notificationHub';
import { getAppById } from './AppRegistry';
import { Icon } from '@radish/ui';
import i18n from '@/i18n';
import type { ApiResponse } from '@/api/client';
import styles from './Dock.module.css';

/**
 * 增强版 Dock 组件
 *
 * 功能：
 * - 显示用户信息、时间、登录状态
 * - 显示运行中的应用
 * - 10秒无操作自动缩小为"灵动岛"样式
 * - 状态指示器（小圆点）
 * - 实时未读数推送（SignalR）+ 降级轮询
 */
export const Dock = () => {
  const { openWindows, openApp, restoreWindow } = useWindowStore();
  const { userName, userId, avatarUrl, avatarThumbnailUrl, isAuthenticated, clearUser, setUser } = useUserStore();
  const { unreadCount: storeUnreadCount, connectionState } = useNotificationStore();
  const [time, setTime] = useState(new Date());
  const [isExpanded, setIsExpanded] = useState(false); // 默认为灵动岛状态
  const [pollingUnreadCount, setPollingUnreadCount] = useState(0); // 轮询降级时的未读数

  const loggedIn = isAuthenticated();
  const hasAccessToken = () => {
    if (typeof window === 'undefined') return false;
    try {
      return Boolean(window.localStorage.getItem('access_token'));
    } catch {
      return false;
    }
  };

  // 根据连接状态决定显示哪个未读数
  const unreadMessages = connectionState === 'connected' ? storeUnreadCount : pollingUnreadCount;

  // 统一通过 Gateway 访问
  const apiBaseUrl = useMemo(() => {
    if (typeof window !== 'undefined') {
      return window.location.origin;
    }
    return 'https://localhost:5000';
  }, []);

  // 解析头像 URL（处理相对路径）
  const resolveAvatarUrl = (url: string | undefined): string | undefined => {
    if (!url) return undefined;
    if (/^https?:\/\//i.test(url)) return url;
    if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
    return `${apiBaseUrl}/${url}`;
  };

  const avatarSrc = resolveAvatarUrl(avatarThumbnailUrl || avatarUrl);

  // 只显示打开的应用（包括最小化的）
  const runningApps = openWindows.map(window => ({
    window,
    app: getAppById(window.appId)
  })).filter(item => item.app !== undefined);

  interface CurrentUser {
    userId: number;
    userName: string;
    tenantId: number;
    avatarUrl?: string;
    avatarThumbnailUrl?: string;
  }

  interface ApiFetchOptions extends RequestInit {
    withAuth?: boolean;
  }

  function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
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

  const getAuthServerBaseUrl = (): string => {
    if (typeof window === 'undefined') {
      return 'https://localhost:5000';
    }

    const currentOrigin = window.location.origin;

    if (currentOrigin === 'https://localhost:5000' || currentOrigin === 'http://localhost:5000') {
      return currentOrigin;
    }

    if (currentOrigin === 'http://localhost:3000' || currentOrigin === 'https://localhost:3000') {
      return 'http://localhost:5200';
    }

    return currentOrigin;
  };

  const handleLoginClick = () => {
    if (typeof window === 'undefined') return;

    const redirectUri = `${window.location.origin}/oidc/callback`;
    const authServerBaseUrl = getAuthServerBaseUrl();
    const authorizeUrl = new URL(`${authServerBaseUrl}/connect/authorize`);
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

    const postLogoutRedirectUri = window.location.origin;
    const authServerBaseUrl = getAuthServerBaseUrl();

    const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
    logoutUrl.searchParams.set('client_id', 'radish-client');

    const currentLanguage = i18n.language || 'zh';
    logoutUrl.searchParams.set('culture', currentLanguage);

    window.location.href = logoutUrl.toString();
  };

  const handleOpenNotificationClick = () => {
    if (!loggedIn && !hasAccessToken()) {
      handleLoginClick();
      return;
    }
    openApp('notification');
  };

  const hydrateCurrentUser = async () => {
    if (typeof window === 'undefined') return;
    const token = window.localStorage.getItem('access_token');
    if (!token) {
      return;
    }

    const requestUrl = `${apiBaseUrl}/api/v1/User/GetUserByHttpContext`;

    try {
      const response = await apiFetch(requestUrl, { withAuth: true });
      const json = await response.json() as ApiResponse<CurrentUser>;

      if (!json.isSuccess || !json.responseData) {
        throw new Error(json.messageInfo || '获取当前用户失败');
      }

      setUser({
        userId: json.responseData.userId,
        userName: json.responseData.userName,
        tenantId: json.responseData.tenantId,
        roles: ['User'],
        avatarUrl: json.responseData.avatarUrl,
        avatarThumbnailUrl: json.responseData.avatarThumbnailUrl
      });
    } catch {
      clearUser();
    }
  };

  // 获取未读消息数量（降级轮询时使用）
  const fetchUnreadMessageCount = async () => {
    if (typeof window === 'undefined') return;
    const token = window.localStorage.getItem('access_token');
    if (!token) {
      setPollingUnreadCount(0);
      return;
    }

    const requestUrl = `${apiBaseUrl}/api/v1/User/GetUnreadMessageCount`;

    try {
      const response = await apiFetch(requestUrl, { withAuth: true });
      const json = await response.json() as ApiResponse<{ userId: number; unreadCount: number }>;

      if (json.isSuccess && json.responseData) {
        setPollingUnreadCount(json.responseData.unreadCount);
      }
    } catch {
      // 静默失败，保持当前状态
    }
  };

  // 时间更新 + SignalR 连接 + 降级轮询
  useEffect(() => {
    const timer = setInterval(() => {
      setTime(new Date());
    }, 1000);

    if (typeof window !== 'undefined') {
      void hydrateCurrentUser();

      // 如果用户已登录，启动 SignalR 连接
      if (loggedIn) {
        void notificationHub.start();
      }

      // 初始化时获取一次未读数（作为降级数据）
      void fetchUnreadMessageCount();

      // 降级轮询：仅在 SignalR 连接失败时使用（60秒间隔）
      // 注意：这里不能在依赖中使用 connectionState，否则会导致重启循环
      const pollingTimer = setInterval(() => {
        // 从 store 中实时读取 connectionState
        const state = useNotificationStore.getState().connectionState;
        if (state !== 'connected') {
          void fetchUnreadMessageCount();
        }
      }, 60000);

      return () => {
        clearInterval(timer);
        clearInterval(pollingTimer);
        // 组件卸载时停止 SignalR 连接
        void notificationHub.stop();
      };
    }

    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loggedIn]);

  // 鼠标移入展开，移出缩小
  const handleMouseEnter = () => {
    setIsExpanded(true);
  };

  const handleMouseLeave = () => {
    setIsExpanded(false);
  };

  return (
    <div className={styles.dockContainer}>
      <div
        className={`${styles.dock} ${isExpanded ? styles.expanded : styles.collapsed}`}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
      >
        {/* 展开状态 */}
        {isExpanded && (
          <div className={styles.expandedContent}>
            {/* 左侧：用户信息 */}
            <div className={styles.userSection}>
              <div className={styles.avatar}>
                {loggedIn ? (
                  <>
                    {avatarSrc ? (
                      <img
                        src={avatarSrc}
                        alt={userName}
                        style={{
                          width: '40px',
                          height: '40px',
                          borderRadius: '50%',
                          objectFit: 'cover'
                        }}
                      />
                    ) : (
                      <Icon icon="mdi:account-circle" size={40} />
                    )}
                    <div className={styles.statusDot} />
                  </>
                ) : (
                  <Icon icon="mdi:account-circle-outline" size={40} />
                )}
              </div>
              {loggedIn && userName && (
                <div className={styles.userInfo}>
                  <div className={styles.userName}>{userName}</div>
                  <div className={styles.userId}>ID: {userId}</div>
                </div>
              )}
            </div>

            {/* 中间：运行中的应用 */}
            {runningApps.length > 0 && (
              <>
                <div className={styles.divider} />
                <div className={styles.appsSection}>
                  {runningApps.map(({ window, app }) => (
                    <button
                      key={window.id}
                      className={`${styles.appIcon} ${window.isMinimized ? styles.minimized : styles.active}`}
                      onClick={() => {
                        if (window.isMinimized) {
                          restoreWindow(window.id);
                        } else {
                          openApp(app!.id);
                        }
                      }}
                      title={app!.name}
                    >
                      {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                        <Icon icon={app!.icon} size={40} />
                      ) : (
                        <span className={styles.emoji}>{app!.icon}</span>
                      )}
                      {!window.isMinimized && <div className={styles.activeIndicator} />}
                    </button>
                  ))}
                </div>
              </>
            )}

            {/* 右侧：时间和登录按钮 */}
            <div className={styles.divider} />
            <div className={styles.rightSection}>
              {/* 通知中心快捷入口 */}
              <button
                type="button"
                className={styles.notificationButton}
                onClick={handleOpenNotificationClick}
                title="通知中心"
              >
                🔔
                {unreadMessages > 0 && (
                  <div className={styles.notificationBadge}>{unreadMessages}</div>
                )}
              </button>
              <div className={styles.time}>
                {time.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })}
              </div>
              <button
                type="button"
                className={`${styles.authButton} ${loggedIn ? styles.loggedIn : styles.loggedOut}`}
                onClick={loggedIn ? handleLogoutClick : handleLoginClick}
                title={loggedIn ? '退出登录' : '登录'}
              >
                <Icon icon={loggedIn ? 'mdi:account-check' : 'mdi:login-variant'} size={28} />
                {loggedIn && <div className={styles.onlineIndicator} />}
              </button>
            </div>
          </div>
        )}

        {/* 缩小状态（灵动岛） */}
        {!isExpanded && (
          <div className={styles.collapsedContent}>
            {/* 用户头像 */}
            <div className={styles.miniAvatar}>
              <Icon icon={loggedIn ? 'mdi:account-circle' : 'mdi:account-circle-outline'} size={20} />
              {loggedIn && <div className={styles.statusDot} />}
            </div>

            {/* 通知中心快捷入口（灵动岛） */}
            <button
              type="button"
              className={styles.miniNotificationButton}
              onClick={handleOpenNotificationClick}
              title="通知中心"
            >
              🔔
              {unreadMessages > 0 && (
                <div className={styles.miniNotificationBadge}>{unreadMessages}</div>
              )}
            </button>

            {/* 运行中的应用（最多显示3个） */}
            {runningApps.slice(0, 3).map(({ window, app }) => (
              <div key={window.id} className={styles.miniAppIcon}>
                {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                  <Icon icon={app!.icon} size={16} />
                ) : (
                  <span className={styles.miniEmoji}>{app!.icon}</span>
                )}
                {!window.isMinimized && <div className={styles.miniActiveIndicator} />}
              </div>
            ))}

            {/* 更多应用指示器 */}
            {runningApps.length > 3 && (
              <div className={styles.moreIndicator}>+{runningApps.length - 3}</div>
            )}

            {/* 时间 */}
            <div className={styles.miniTime}>
              {time.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
