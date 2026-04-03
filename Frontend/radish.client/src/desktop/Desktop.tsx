
import { useWindowStore } from '@/stores/windowStore';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { hasAuthenticatedSession } from '@/services/authSession';
import { getVisibleApps, prefetchAppComponent } from './AppRegistry';
import { AppIcon } from '@/widgets/AppIcon';
import { ContextMenu } from '@radish/ui/context-menu';
import { Icon } from '@radish/ui/icon';
import { useTheme } from '@/theme/useTheme';
import styles from './Desktop.module.css';






/**
 * 桌面组件
 *
 * 显示应用图标网格
 */
export const Desktop = () => {
  
  const { openApp } = useWindowStore();
  const authAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const { userId, roles, permissions } = useUserStore();
  const { cycleTheme } = useTheme();
  const isAuthenticated = hasAuthenticatedSession(authAuthenticated, userId);

  // 根据桌面显示规则过滤可见应用
  const visibleApps = getVisibleApps(isAuthenticated, roles, permissions);

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

  return (
    <ContextMenu items={contextMenuItems}>
      <div className={styles.desktop}>
        {visibleApps.map(app => (
          <AppIcon
            key={app.id}
            app={app}
            onDoubleClick={() => openApp(app.id)}
            onPointerEnter={() => prefetchAppComponent(app.id)}
          />
        ))}
      </div>
    </ContextMenu>
  );
};
