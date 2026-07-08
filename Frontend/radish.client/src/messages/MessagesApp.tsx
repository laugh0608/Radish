import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToastContainer } from '@radish/ui/toast';
import { WebStateSlot } from '@/components/web-shell';
import { ChatApp, type ChatAppProfileNavigationTarget } from '@/apps/chat/ChatApp';
import { CurrentWindowProvider } from '@/desktop/CurrentWindowContext';
import type { WindowState } from '@/desktop/types';
import { getApiBaseUrl } from '@/config/env';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildPublicProfilePath } from '@/public/profileRouteState';
import { rememberPublicRouteSourceTransfer, type PublicRouteDescriptor } from '@/public/publicRouteNavigation';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { buildMessagesReturnPath } from '@/services/authReturnPath';
import { chatHub } from '@/services/chatHub';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import { buildMessagesPath, createDefaultMessagesRoute, parseMessagesRoute } from './messagesRouteState';
import styles from './MessagesApp.module.css';

function parseInitialRoute() {
  if (typeof window === 'undefined') {
    return createDefaultMessagesRoute();
  }

  return parseMessagesRoute(window.location.pathname, window.location.search) ?? createDefaultMessagesRoute();
}

export const MessagesApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const route = useMemo(() => parseInitialRoute(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const hasStartedHubRef = useRef(false);

  const routeDescriptor = useMemo<PublicRouteDescriptor>(() => ({
    app: 'messages',
    route,
  }), [route]);

  const virtualWindow = useMemo<WindowState>(() => ({
    id: 'messages-web-entry',
    appId: 'chat',
    appParams: route.channelId
      ? {
          channelId: route.channelId,
          ...(route.messageId ? { messageId: route.messageId } : {}),
          __navigationKey: `messages:${route.channelId}:${route.messageId ?? 'latest'}`,
        }
      : undefined,
    zIndex: 0,
    isMinimized: false,
    isMaximized: true,
  }), [route]);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('MessagesApp', '消息页登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    document.title = `${t('messages.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    const canonicalPath = buildMessagesPath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    const returnPath = buildMessagesReturnPath(route) ?? buildMessagesPath();
    setRedirecting(true);
    redirectToLogin({ returnPath });
  }, [authReady, loggedIn, redirecting, route]);

  useEffect(() => {
    if (loggedIn && !hasStartedHubRef.current) {
      hasStartedHubRef.current = true;
      void chatHub.start();
    } else if (!loggedIn && hasStartedHubRef.current) {
      hasStartedHubRef.current = false;
      void chatHub.stop();
    }

    return () => {
      hasStartedHubRef.current = false;
      setTimeout(() => {
        if (!hasStartedHubRef.current) {
          void chatHub.stop();
        }
      }, 100);
    };
  }, [loggedIn]);

  const handleOpenUserProfile = useCallback((target: ChatAppProfileNavigationTarget) => {
    const targetUserId = normalizeEntityId(target.userId);
    if (!targetUserId || !/^[1-9]\d*$/.test(targetUserId)) {
      return;
    }

    const href = buildPublicProfilePath({
      kind: 'detail',
      userId: targetUserId,
      tab: 'posts',
      page: 1,
    });

    rememberPublicRouteSourceTransfer(href, {
      profileSourceRoute: routeDescriptor,
    });
    window.location.href = href;
  }, [routeDescriptor]);

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="auth"
            icon="mdi:message-text-outline"
            title={t('messages.title')}
            description={t(redirecting ? 'messages.auth.redirecting' : 'messages.auth.loading')}
          />
        </section>
      );
    }

    return (
      <div className={styles.messagesWorkspace}>
        <section className={styles.chatShell} aria-label={t('messages.web.chatWorkspaceLabel')}>
          <CurrentWindowProvider value={virtualWindow}>
            <ChatApp onOpenUserProfile={handleOpenUserProfile} />
          </CurrentWindowProvider>
        </section>
      </div>
    );
  };

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="chat"
        brandMark="聊"
        brandName={t('messages.title')}
        brandSubline={t('messages.shellSubline')}
        hideMobileNav={route.channelId !== undefined}
        onBrandClick={() => {
          window.location.href = buildMessagesPath();
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
      <ToastContainer />
    </div>
  );
};
