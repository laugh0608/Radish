import { useEffect, useMemo, useRef } from 'react';
import { ToastContainer } from '@radish/ui/toast';
import { LevelUpModal } from '@radish/ui/level-up-modal';
import { getApiBaseUrl } from '@/config/env';
import { notificationHub } from '@/services/notificationHub';
import { chatHub } from '@/services/chatHub';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useChatStore } from '@/stores/chatStore';
import { tokenService } from '@/services/tokenService';
import { bootstrapAuth } from '@/services/authBootstrap';
import { prefetchAppComponent } from './AppRegistry';
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
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);

  // 升级事件监听
  const { levelUpData, showModal, handleClose } = useLevelUpListener();

  // 从 authStore 读取认证状态
  const { isAuthenticated } = useAuthStore();
  const currentUser = useUserStore(state => state.userId);
  const clearUser = useUserStore(state => state.clearUser);
  const resetChatStore = useChatStore(state => state.reset);

  // 监听认证状态变化，同步清除用户信息和通知
  useEffect(() => {
    const hasToken = !!tokenService.getAccessToken();
    if (!isAuthenticated && !hasToken && currentUser) {
      // 认证失效，清除用户信息
      clearUser();

      // 清除通知
      const notificationStore = useNotificationStore.getState();
      notificationStore.clearRecentNotifications();
      notificationStore.setUnreadCount(0);
      resetChatStore();
    }
  }, [isAuthenticated, currentUser, clearUser, resetChatStore]);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    return () => cleanup();
  }, [apiBaseUrl]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const preloadApps = () => {
      prefetchAppComponent('forum');
      if (isAuthenticated) {
        prefetchAppComponent('chat');
      }
    };

    if (typeof window.requestIdleCallback === 'function') {
      const idleHandle = window.requestIdleCallback(() => preloadApps(), { timeout: 1500 });
      return () => window.cancelIdleCallback(idleHandle);
    }

    const timer = window.setTimeout(preloadApps, 600);
    return () => window.clearTimeout(timer);
  }, [isAuthenticated]);

  // 根据认证状态控制 SignalR 连接
  useEffect(() => {
    // 防止 React StrictMode 导致重复启动连接
    if (isAuthenticated && !hasStartedRef.current) {
      hasStartedRef.current = true;
      void notificationHub.start();
      void chatHub.start();
    } else if (!isAuthenticated && hasStartedRef.current) {
      hasStartedRef.current = false;
      void notificationHub.stop();
      void chatHub.stop();
    }

    // cleanup 函数：仅在组件真正卸载时执行
    return () => {
      // 延迟执行 stop，给 StrictMode 的第二次 mount 一个机会
      setTimeout(() => {
        if (!hasStartedRef.current) {
          void notificationHub.stop();
          void chatHub.stop();
        }
      }, 100);
    };
  }, [isAuthenticated]);

  return (
    <div className={styles.shell}>
      <div className={styles.desktopLayer}>
        <Desktop />
      </div>
      <div className={styles.windowLayer}>
        <WindowManager />
      </div>
      <div className={styles.dockLayer}>
        <Dock />
      </div>
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
