import { StatusBar } from './StatusBar';
import { Desktop } from './Desktop';
import { Dock } from './Dock';
import { WindowManager } from './WindowManager';
import styles from './Shell.module.css';

/**
 * Desktop Shell - 桌面外壳
 *
 * WebOS 的主容器，包含状态栏、桌面、窗口管理器和 Dock
 */
export const Shell = () => {
  return (
    <div className={styles.shell}>
      <StatusBar />
      <Desktop />
      <WindowManager />
      <Dock />
    </div>
  );
};
