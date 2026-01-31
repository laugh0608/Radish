import { useState, useEffect, useMemo } from 'react';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { notificationHub } from '@/services/notificationHub';
import { redirectToLogin, logout, hasAccessToken } from '@/services/auth';
import { getAppById } from './AppRegistry';
import { Icon } from '@radish/ui';
import { CoinBalance } from './components/CoinBalance';
import { ExperienceDisplay } from './components/ExperienceDisplay';
import i18n from '@/i18n';
import type { ApiResponse } from '@radish/ui';
import { getApiBaseUrl } from '@/config/env';
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

  // 根据连接状态决定显示哪个未读数
  const unreadMessages = connectionState === 'connected' ? storeUnreadCount : pollingUnreadCount;

  // 统一通过 Gateway 访问
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);

  // 解析头像 URL（处理相对路径）
  const resolveAvatarUrl = (url: string | undefined): string | undefined => {
    if (!url) return undefined;
    if (/^https?:\/\//i.test(url)) return url;
    if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
    return `${apiBaseUrl}/${url}`;
  };

  const avatarSrc = resolveAvatarUrl(avatarThumbnailUrl || avatarUrl);

  // 显示在 Dock 中的应用（通知中心常驻 + 运行中的其他应用）
  const dockApps = useMemo(() => {
    const notificationApp = getAppById('notification');
    const notificationWindow = openWindows.find(w => w.appId === 'notification');

    // 其他运行中的应用
    const otherApps = openWindows
      .filter(window => window.appId !== 'notification')
      .map(window => ({
        window,
        app: getAppById(window.appId),
        isPinned: false
      }))
      .filter(item => item.app !== undefined);

    // 通知中心始终显示（固定在第一个位置）
    const notificationItem = notificationApp ? [{
      window: notificationWindow,
      app: notificationApp,
      isPinned: true // 标记为固定应用
    }] : [];

    return [...notificationItem, ...otherApps];
  }, [openWindows]);

  interface CurrentUser {
    voUserId: number;
    voUserName: string;
    voTenantId: number;
    voAvatarUrl?: string;
    voAvatarThumbnailUrl?: string;
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

  const handleLogoutClick = () => {
    clearUser();
    logout();
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
        userId: json.responseData.voUserId,
        userName: json.responseData.voUserName,
        tenantId: json.responseData.voTenantId,
        roles: ['User'],
        avatarUrl: json.responseData.voAvatarUrl,
        avatarThumbnailUrl: json.responseData.voAvatarThumbnailUrl
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

      // 注意：WebSocket 连接由 Shell.tsx 统一管理，此处不再启动
      // 如果用户已登录，获取未读数（作为降级数据）
      if (loggedIn) {
        // 初始化时获取一次未读数（作为降级数据）
        void fetchUnreadMessageCount();
      } else {
        // 用户未登录时，清空未读数
        setPollingUnreadCount(0);
      }

      // 降级轮询：仅在用户已登录且 SignalR 连接失败时使用（60秒间隔）
      // 注意：这里不能在依赖中使用 connectionState，否则会导致重启循环
      const pollingTimer = setInterval(() => {
        // 检查用户是否登录
        const token = window.localStorage.getItem('access_token');
        if (!token) {
          return;
        }
        
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
              {/* 萝卜币余额 */}
              {loggedIn && <CoinBalance />}
              {/* 经验值显示 */}
              {loggedIn && <ExperienceDisplay />}
            </div>

            {/* 中间：Dock 应用（通知中心常驻 + 运行中的应用） */}
            {dockApps.length > 0 && (
              <>
                <div className={styles.divider} />
                <div className={styles.appsSection}>
                  {dockApps.map(({ window, app, isPinned }) => {
                    const isNotification = app!.id === 'notification';
                    const isRunning = window !== undefined;
                    const isMinimized = window?.isMinimized ?? false;

                    return (
                      <button
                        key={app!.id}
                        className={`${styles.appIcon} ${
                          isRunning && !isMinimized ? styles.active :
                          isRunning && isMinimized ? styles.minimized :
                          isPinned ? styles.pinned : ''
                        }`}
                        onClick={() => {
                          // 如果是通知中心，未登录时先登录
                          if (isNotification && !loggedIn && !hasAccessToken()) {
                            redirectToLogin();
                            return;
                          }

                          // 如果窗口存在且最小化，恢复窗口
                          if (window && isMinimized) {
                            restoreWindow(window.id);
                          } else {
                            // 否则打开或聚焦应用
                            openApp(app!.id);
                          }
                        }}
                        title={app!.name}
                      >
                        <div style={{ position: 'relative' }}>
                          {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                            <Icon icon={app!.icon} size={40} />
                          ) : (
                            <span className={styles.emoji}>{app!.icon}</span>
                          )}
                          {/* 通知中心的未读数徽章 */}
                          {isNotification && unreadMessages > 0 && (
                            <div className={styles.notificationBadge}>{unreadMessages}</div>
                          )}
                        </div>
                        {/* 运行中指示器 */}
                        {isRunning && !isMinimized && <div className={styles.activeIndicator} />}
                      </button>
                    );
                  })}
                </div>
              </>
            )}

            {/* 右侧：时间和登录按钮 */}
            <div className={styles.divider} />
            <div className={styles.rightSection}>
              <div className={styles.time}>
                {time.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })}
              </div>
              <button
                type="button"
                className={`${styles.authButton} ${loggedIn ? styles.loggedIn : styles.loggedOut}`}
                onClick={loggedIn ? handleLogoutClick : redirectToLogin}
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

            {/* Dock 应用（最多显示4个） */}
            {dockApps.slice(0, 4).map(({ window, app }) => {
              const isNotification = app!.id === 'notification';
              const isRunning = window !== undefined;
              const isMinimized = window?.isMinimized ?? false;

              return (
                <div
                  key={app!.id}
                  className={styles.miniAppIcon}
                  style={{ position: 'relative', cursor: 'pointer' }}
                  onClick={() => {
                    // 如果是通知中心，未登录时先登录
                    if (isNotification && !loggedIn && !hasAccessToken()) {
                      redirectToLogin();
                      return;
                    }

                    // 如果窗口存在且最小化，恢复窗口
                    if (window && isMinimized) {
                      restoreWindow(window.id);
                    } else {
                      // 否则打开或聚焦应用
                      openApp(app!.id);
                    }
                  }}
                  title={app!.name}
                >
                  {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                    <Icon icon={app!.icon} size={16} />
                  ) : (
                    <span className={styles.miniEmoji}>{app!.icon}</span>
                  )}
                  {/* 通知中心的未读数徽章（迷你版） */}
                  {isNotification && unreadMessages > 0 && (
                    <div className={styles.miniNotificationBadge}>{unreadMessages}</div>
                  )}
                  {/* 运行中指示器 */}
                  {isRunning && !isMinimized && <div className={styles.miniActiveIndicator} />}
                </div>
              );
            })}

            {/* 更多应用指示器 */}
            {dockApps.length > 4 && (
              <div className={styles.moreIndicator}>+{dockApps.length - 4}</div>
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
