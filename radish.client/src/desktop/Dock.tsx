import { useWindowStore } from '@/stores/windowStore';
import { getAppById } from './AppRegistry';
import { Icon } from '@radish/ui';
import styles from './Dock.module.css';

/**
 * Dock 组件
 *
 * 显示运行中的应用
 */
export const Dock = () => {
  const { openWindows, openApp, restoreWindow } = useWindowStore();

  // 只显示打开的应用（包括最小化的）
  const runningApps = openWindows.map(window => ({
    window,
    app: getAppById(window.appId)
  })).filter(item => item.app !== undefined);

  if (runningApps.length === 0) {
    return null;
  }

  return (
    <div className={styles.dock}>
      <div className={styles.dockInner}>
        {runningApps.map(({ window, app }) => (
          <button
            key={window.id}
            className={`${styles.dockItem} ${window.isMinimized ? styles.minimized : styles.active}`}
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
              <Icon icon={app!.icon} size={32} />
            ) : (
              <span className={styles.emoji}>{app!.icon}</span>
            )}
            {!window.isMinimized && <div className={styles.indicator} />}
          </button>
        ))}
      </div>
    </div>
  );
};
