import { Desktop } from './Desktop';
import { Dock } from './Dock';
import { WindowManager } from './WindowManager';
import styles from './Shell.module.css';

/**
 * Desktop Shell - 桌面外壳
 *
 * WebOS 的主容器，包含桌面、窗口管理器和增强版 Dock
 */
export const Shell = () => {
  return (
    <div className={styles.shell}>
      <Desktop />
      <WindowManager />
      <Dock />
    </div>
  );
};
