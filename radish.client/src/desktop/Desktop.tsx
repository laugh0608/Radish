import { useWindowStore } from '@/stores/windowStore';
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
  const { roles } = useUserStore();

  // 根据用户角色过滤可见应用
  const visibleApps = getVisibleApps(roles);

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
