import { useEffect, useMemo, useRef } from 'react';
import { LevelUpModal } from '@radish/ui/level-up-modal';
import { getApiBaseUrl } from '@/config/env';
import { notificationHub } from '@/services/notificationHub';
import { chatHub } from '@/services/chatHub';
import { useLevelUpListener } from '@/hooks/useLevelUpListener';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useChatStore } from '@/stores/chatStore';
import { useWindowStore } from '@/stores/windowStore';
import { tokenService } from '@/services/tokenService';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth } from '@/services/authBootstrap';
import { parseDesktopExternalEntry, stripDesktopExternalEntrySearch } from '@/utils/desktopEntryNavigation';
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
  const chatHubOwnerRef = useRef(Symbol('desktop-shell-chat'));
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);

  // 升级事件监听
  const { levelUpData, showModal, handleClose } = useLevelUpListener();

  // 从 authStore 读取认证状态
  const { isAuthenticated } = useAuthStore();
  const currentUser = useUserStore(state => state.userId);
  const clearUser = useUserStore(state => state.clearUser);
  const resetChatStore = useChatStore(state => state.reset);
  const openOrReuseApp = useWindowStore(state => state.openOrReuseApp);
  const handledDesktopEntrySignatureRef = useRef<string | null>(null);
  const loginRedirectEntrySignatureRef = useRef<string | null>(null);
  const desktopExternalEntry = useMemo(() => {
    if (typeof window === 'undefined') {
      return null;
    }

    return parseDesktopExternalEntry(window.location.pathname, window.location.search);
  }, []);

  // 监听认证状态变化，同步清除用户信息和通知
  useEffect(() => {
    const hasToken = !!tokenService.getAccessToken();
    if (!isAuthenticated && !hasToken && currentUser) {
      // 认证失效，清除用户信息
      clearUser();

      // 清除通知
      useNotificationStore.getState().reset();
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

  useEffect(() => {
    if (typeof window === 'undefined' || !desktopExternalEntry) {
      return;
    }

    if (desktopExternalEntry.requiresAuthenticatedSession && !isAuthenticated) {
      if (tokenService.getAccessToken()) {
        return;
      }

      if (loginRedirectEntrySignatureRef.current === desktopExternalEntry.signature) {
        return;
      }

      loginRedirectEntrySignatureRef.current = desktopExternalEntry.signature;
      redirectToLogin({
        returnPath: `${window.location.pathname}${window.location.search}${window.location.hash}`,
      });
      return;
    }

    if (handledDesktopEntrySignatureRef.current === desktopExternalEntry.signature) {
      return;
    }

    handledDesktopEntrySignatureRef.current = desktopExternalEntry.signature;
    openOrReuseApp(desktopExternalEntry.appId, desktopExternalEntry.appParams);

    const nextUrl = new URL(window.location.href);
    nextUrl.search = stripDesktopExternalEntrySearch(nextUrl.search);
    window.history.replaceState({}, '', `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`);
  }, [desktopExternalEntry, isAuthenticated, openOrReuseApp]);

  // 根据认证状态控制 SignalR 连接
  useEffect(() => {
    // StrictMode 可能重放旧 render 的 effect；执行时重新读取账号状态，避免旧 false 停掉新连接。
    const isCurrentlyAuthenticated = useAuthStore.getState().isAuthenticated;
    if (isCurrentlyAuthenticated) {
      void notificationHub.start();
    } else if (!tokenService.getAccessToken()) {
      // Token 仍在 hydration 时不停止账号级连接；真实登出会先清除 Token。
      void notificationHub.stop();
    }
  }, [isAuthenticated]);

  useEffect(() => {
    const owner = chatHubOwnerRef.current;
    if (!isAuthenticated) {
      void chatHub.release(owner);
      return;
    }

    void chatHub.acquire(owner);
    return () => {
      void chatHub.release(owner);
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
