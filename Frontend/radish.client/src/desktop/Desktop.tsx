import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useWindowStore } from '@/stores/windowStore';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { hasAuthenticatedSession } from '@/services/authSession';
import { getVisibleApps, prefetchAppComponent } from './AppRegistry';
import { AppIcon } from '@/widgets/AppIcon';
import { ContextMenu } from '@radish/ui/context-menu';
import { Icon } from '@radish/ui/icon';
import { useTheme } from '@/theme/useTheme';
import { DesktopResumePanel } from './components/DesktopResumePanel';
import {
  findRecentDesktopApp,
  readRecentDesktopApps,
  RECENT_DESKTOP_APPS_CHANGED_EVENT,
  type RecentDesktopAppItem,
} from '@/utils/desktopRecentApps';
import styles from './Desktop.module.css';

const RESUMABLE_DESKTOP_APP_IDS = new Set(['forum', 'document', 'shop']);

/**
 * 桌面组件
 *
 * 显示应用图标网格
 */
export const Desktop = () => {
  const { t } = useTranslation();
  const { openApp, openOrReuseApp } = useWindowStore();
  const authAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const { userId, roles, permissions } = useUserStore();
  const { cycleTheme } = useTheme();
  const [recentApps, setRecentApps] = useState<RecentDesktopAppItem[]>([]);
  const isAuthenticated = hasAuthenticatedSession(authAuthenticated, userId);

  // 根据桌面显示规则过滤可见应用
  const visibleApps = getVisibleApps(isAuthenticated, roles, permissions);

  useEffect(() => {
    const refreshRecentApps = () => {
      setRecentApps(readRecentDesktopApps());
    };

    refreshRecentApps();
    window.addEventListener(RECENT_DESKTOP_APPS_CHANGED_EVENT, refreshRecentApps);

    return () => {
      window.removeEventListener(RECENT_DESKTOP_APPS_CHANGED_EVENT, refreshRecentApps);
    };
  }, []);

  const contextMenuItems = [
    {
      id: 'refresh',
      label: '刷新桌面',
      icon: <Icon icon="mdi:refresh" />,
      onClick: () => window.location.reload()
    },
    {
      id: 'theme',
      label: '切换国风主题',
      icon: <Icon icon="mdi:palette-outline" />,
      onClick: () => cycleTheme()
    },
    { id: 'divider-1', label: '', divider: true },
    {
      id: 'about',
      label: '关于系统',
      icon: <Icon icon="mdi:information-outline" />,
      onClick: () => openApp('welcome')
    }
  ];

  const getRestorableRecentApp = (appId: string) => {
    if (!isAuthenticated || !RESUMABLE_DESKTOP_APP_IDS.has(appId)) {
      return undefined;
    }

    return recentApps.find((item) => item.appId === appId && item.appParams);
  };

  return (
    <ContextMenu items={contextMenuItems}>
      <div className={styles.desktop}>
        <div className={styles.iconGrid}>
          {visibleApps.map(app => {
            const recentApp = getRestorableRecentApp(app.id);
            const appName = app.nameKey ? t(app.nameKey) : app.name;

            return (
              <AppIcon
                key={app.id}
                app={app}
                resumeTitle={recentApp ? t('desktop.resume.openRecentAppTitle', { appName }) : undefined}
                onDoubleClick={() => openApp(app.id)}
                onPointerEnter={() => prefetchAppComponent(app.id)}
                onResumeClick={recentApp ? () => {
                  const currentRecentApp = findRecentDesktopApp(app.id);
                  openOrReuseApp(app.id, currentRecentApp?.appParams ?? recentApp.appParams);
                } : undefined}
              />
            );
          })}
        </div>
        {isAuthenticated ? <DesktopResumePanel /> : null}
      </div>
    </ContextMenu>
  );
};
