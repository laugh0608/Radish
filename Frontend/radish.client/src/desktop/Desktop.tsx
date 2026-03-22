import { useWindowStore } from '@/stores/windowStore';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { getVisibleApps } from './AppRegistry';
import { AppIcon } from '@/widgets/AppIcon';
import styles from './Desktop.module.css';

/**
 * 桌面组件
 *
 * 显示应用图标网格
 */
export const Desktop = () => {
  const { openApp } = useWindowStore();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const { roles, permissions } = useUserStore();

  // 根据用户角色过滤可见应用
  const visibleApps = getVisibleApps(isAuthenticated, roles, permissions);

  return (
    <div className={styles.desktop}>
      {visibleApps.map(app => (
        <AppIcon
          key={app.id}
          app={app}
          onDoubleClick={() => openApp(app.id)}
        />
      ))}
    </div>
  );
};
