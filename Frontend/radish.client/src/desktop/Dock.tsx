import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { redirectToLogin, logout, hasAccessToken } from '@/services/auth';
import { getAppById } from './AppRegistry';
import type { AppDefinition } from './types';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import { useTheme } from '@/theme/useTheme';
import i18n from '@/i18n';
import type { ApiResponse } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';
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
  const { t } = useTranslation();
  const { openWindows, openApp, restoreWindow } = useWindowStore();
  const { userName, userId, avatarUrl, avatarThumbnailUrl, isAuthenticated, clearUser } = useUserStore();
  const { unreadCount: storeUnreadCount, connectionState } = useNotificationStore();
  const { currentTheme, cycleTheme } = useTheme();
  const [time, setTime] = useState(new Date());
  const [isExpanded, setIsExpanded] = useState(false); // 默认为灵动岛状态
  const [pollingUnreadCount, setPollingUnreadCount] = useState(0); // 轮询降级时的未读数
  const [avatarLoadError, setAvatarLoadError] = useState(false);
  const currentLanguage = i18n.resolvedLanguage?.startsWith('en') ? 'en' : 'zh';
  const timeLocale = currentLanguage === 'en' ? 'en-US' : 'zh-CN';
  const languageIcon = currentLanguage === 'en' ? 'mdi:format-letter-case' : 'mdi:translate-variant';
  const languageBadge = currentLanguage === 'en' ? 'EN' : 'ZH';
  const themeIcon = currentTheme.id === 'default' ? 'mdi:view-dashboard-outline' : 'mdi:landscape';
  const themeBadge = currentTheme.id === 'default' ? '简' : '风';

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

  const buildAvatarText = (name: string): string => {
    const source = name.trim();
    if (!source) return '?';
    return source.charAt(0).toUpperCase();
  };

  const buildAvatarStyle = (seed: string) => {
    let hash = 0;
    for (let i = 0; i < seed.length; i += 1) {
      hash = seed.charCodeAt(i) + ((hash << 5) - hash);
    }

    const hue = Math.abs(hash) % 360;
    return {
      backgroundColor: `hsl(${hue} 80% 92%)`,
      color: `hsl(${hue} 45% 30%)`
    };
  };

  const avatarSrc = resolveAvatarUrl(avatarThumbnailUrl || avatarUrl);
  const avatarImageSrc = avatarLoadError ? undefined : avatarSrc;
  const avatarSeed = userName?.trim() || 'User';
  const avatarFallbackText = buildAvatarText(avatarSeed);
  const avatarContainerStyle = {
    cursor: loggedIn ? 'pointer' : 'default',
    ...(loggedIn && !avatarImageSrc ? buildAvatarStyle(avatarSeed) : {})
  };

  useEffect(() => {
    setAvatarLoadError(false);
  }, [avatarSrc]);

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
      const token = tokenService.getAccessToken();
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
  const notifyLoginRequired = () => {
    toast.info(t('dock.loginRequired'));
  };
  const toggleLanguage = () => {
    void i18n.changeLanguage(currentLanguage === 'zh' ? 'en' : 'zh');
  };
  const getAppLabel = (app: AppDefinition) => app.nameKey ? t(app.nameKey) : app.name;

  // 获取未读通知数量（降级轮询时使用）
  const fetchUnreadNotificationCount = async () => {
    if (typeof window === 'undefined') return;
    const token = tokenService.getAccessToken();
    if (!token) {
      setPollingUnreadCount(0);
      return;
    }

    const requestUrl = `${apiBaseUrl}/api/v1/Notification/GetUnreadCount`;

    try {
      const response = await apiFetch(requestUrl, { withAuth: true });
      const json = await response.json() as ApiResponse<{ unreadCount: number }>;

      if (json.isSuccess && json.responseData) {
        setPollingUnreadCount(Math.max(0, json.responseData.unreadCount ?? 0));
      }
    } catch {
      // 静默失败，保持当前状态
    }
  };

  // 时间更新 + 降级轮询
  useEffect(() => {
    const timer = setInterval(() => {
      setTime(new Date());
    }, 1000);

    // 注意：用户信息由 App.tsx 统一管理，此处不再重复获取
    // 注意：WebSocket 连接由 Shell.tsx 统一管理，此处不再启动

    if (typeof window !== 'undefined' && loggedIn) {
      // 初始化时获取一次未读数（作为降级数据）
      void fetchUnreadNotificationCount();

      // 降级轮询：仅在用户已登录且 SignalR 连接失败时使用（60秒间隔）
      const pollingTimer = setInterval(() => {
        // 检查用户是否登录
        const token = tokenService.getAccessToken();
        if (!token) {
          return;
        }

        // 从 store 中实时读取 connectionState
        const state = useNotificationStore.getState().connectionState;
        if (state !== 'connected') {
          void fetchUnreadNotificationCount();
        }
      }, 60000);

      return () => {
        clearInterval(timer);
        clearInterval(pollingTimer);
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
    <div className={`${styles.dockContainer} ${isExpanded ? styles.dockContainerExpanded : styles.dockContainerCollapsed}`}>
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
              <div
                className={styles.avatar}
                onClick={() => loggedIn && openApp('profile')}
                style={avatarContainerStyle}
                title={loggedIn ? t('dock.openProfile') : undefined}
              >
                {loggedIn ? (
                  <>
                    {avatarImageSrc ? (
                      <img
                        src={avatarImageSrc}
                        alt={userName}
                        onError={() => setAvatarLoadError(true)}
                      />
                    ) : (
                      <span className={styles.avatarFallback}>{avatarFallbackText}</span>
                    )}
                    <div className={styles.statusDot} />
                  </>
                ) : (
                  <Icon icon="mdi:account-circle-outline" size={40} />
                )}
              </div>
              {loggedIn && userName && (
                <div
                  className={styles.userInfo}
                  onClick={() => openApp('profile')}
                  style={{ cursor: 'pointer' }}
                  title={t('dock.openProfile')}
                >
                  <div className={styles.userName}>{userName}</div>
                  <div className={styles.userId}>ID: {userId}</div>
                </div>
              )}
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
                            notifyLoginRequired();
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
                        title={getAppLabel(app!)}
                      >
                        <div style={{ position: 'relative' }}>
                          {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                            <Icon icon={app!.icon} size={32} />
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
                {time.toLocaleTimeString(timeLocale, { hour: '2-digit', minute: '2-digit' })}
              </div>
              <button
                type="button"
                className={styles.languageButton}
                onClick={toggleLanguage}
                title={`${t('lang.switch')}（${languageBadge}）`}
                aria-label={t('lang.switch')}
                data-state={currentLanguage}
              >
                <span className={styles.controlIconWrap}>
                  <Icon icon={languageIcon} size={20} />
                  <span className={`${styles.controlBadge} ${styles.languageBadge}`}>{languageBadge}</span>
                </span>
              </button>
              <button
                type="button"
                className={styles.themeButton}
                onClick={cycleTheme}
                title={`${t('theme.switch')}（${currentTheme.label}）`}
                aria-label={t('theme.switch')}
                data-state={currentTheme.id}
              >
                <span className={styles.controlIconWrap}>
                  <Icon icon={themeIcon} size={20} />
                  <span className={`${styles.controlBadge} ${styles.themeBadge}`}>{themeBadge}</span>
                </span>
              </button>
              <button
                type="button"
                className={`${styles.authButton} ${loggedIn ? styles.loggedIn : styles.loggedOut}`}
                onClick={loggedIn ? handleLogoutClick : redirectToLogin}
                title={loggedIn ? t('auth.logout') : t('auth.login')}
                aria-label={loggedIn ? t('auth.logout') : t('auth.login')}
              >
                <Icon icon={loggedIn ? 'mdi:logout' : 'mdi:login'} size={20} />
              </button>
            </div>
          </div>
        )}

        {/* 缩小状态（灵动岛） */}
        {!isExpanded && (
          <div className={styles.collapsedContent}>
            {/* 用户头像 */}
            <div className={styles.miniAvatar}>
              <Icon icon={loggedIn ? 'mdi:account-circle' : 'mdi:account-circle-outline'} size={16} />
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
                      notifyLoginRequired();
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
                  title={getAppLabel(app!)}
                >
                  {app!.icon.startsWith('mdi:') || app!.icon.startsWith('ic:') ? (
                    <Icon icon={app!.icon} size={14} />
                  ) : (
                    <span className={styles.miniEmoji}>{app!.icon}</span>
                  )}
                  {/* 通知中心的未读提示（红点） */}
                  {isNotification && unreadMessages > 0 && (
                    <div className={styles.miniNotificationDot} />
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
              {time.toLocaleTimeString(timeLocale, { hour: '2-digit', minute: '2-digit' })}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
