import { useEffect, useRef } from 'react';
import { ToastContainer, LevelUpModal } from '@radish/ui';
import { notificationHub } from '@/services/notificationHub';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { Desktop } from './Desktop';
import { Dock } from './Dock';
import { WindowManager } from './WindowManager';
import styles from './Shell.module.css';

/**
 * Desktop Shell - 桌面外壳
 *
 * WebOS 的主容器，包含桌面、窗口管理器和 Dock（含灵动岛）
 */
export const Shell = () => {
  // 使用 ref 防止 React StrictMode 双重挂载导致重复连接
  const hasStartedRef = useRef(false);

  // 升级事件监听
  const { levelUpData, showModal, handleClose } = useLevelUpListener();

  useEffect(() => {
    const token = window.localStorage.getItem('access_token');

    // 防止 React StrictMode 导致重复启动连接
    // 注意：这里是 WebSocket 连接的唯一启动点，其他组件不应再启动连接
    if (token && !hasStartedRef.current) {
      hasStartedRef.current = true;
      void notificationHub.start();
    } else if (!token) {
      hasStartedRef.current = false;
      void notificationHub.stop();
    }

    // cleanup 函数：仅在组件真正卸载时执行
    return () => {
      // 延迟执行 stop，给 StrictMode 的第二次 mount 一个机会
      // 如果是 StrictMode 导致的卸载，会在几毫秒内重新挂载，此时不应 stop
      setTimeout(() => {
        // 再次检查：如果组件已重新挂载，hasStartedRef 会是 true，不执行 stop
        // 只有真正卸载时（用户登出、路由切换），才会执行 stop
        if (!hasStartedRef.current) {
          void notificationHub.stop();
        }
      }, 100);
    };
  }, []);

  return (
    <div className={styles.shell}>
      <Desktop />
      <WindowManager />
      <Dock />
      <ToastContainer />

      {/* 升级动画弹窗 */}
      {levelUpData && (
        <LevelUpModal
          isOpen={showModal}
          data={levelUpData}
          onClose={handleClose}
        />
      )}
    </div>
  );
};
