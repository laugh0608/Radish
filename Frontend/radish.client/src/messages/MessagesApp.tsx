import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToastContainer } from '@radish/ui/toast';
import { Icon } from '@radish/ui/icon';
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
import { useChatStore } from '@/stores/chatStore';
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
  const channels = useChatStore(state => state.channels);
  const activeChannelId = useChatStore(state => state.activeChannelId);
  const connectionState = useChatStore(state => state.connectionState);
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
      <div className={styles.contentGrid}>
        <section className={styles.summaryPanel} aria-label={t('messages.web.summaryLabel')}>
          <div className={styles.summaryHeader}>
            <span className={styles.kicker}>{t('messages.web.kicker')}</span>
            <h1>{t('messages.web.heading')}</h1>
            <p>{t('messages.web.description')}</p>
          </div>
          <div className={styles.summaryCards}>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:forum-outline" size={22} />
              </span>
              <strong>{channels.length}</strong>
              <span>{t('messages.web.channelsMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon={activeChannelId ? 'mdi:message-text-outline' : 'mdi:message-outline'} size={22} />
              </span>
              <strong>{activeChannelId ? t('messages.web.openMetricValue') : t('messages.web.pendingMetricValue')}</strong>
              <span>{t('messages.web.routeMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:access-point" size={22} />
              </span>
              <strong>{connectionState === 'connected' ? t('messages.web.connectedMetricValue') : t('messages.web.connectingMetricValue')}</strong>
              <span>{t('messages.web.connectionMetric')}</span>
            </div>
          </div>
        </section>
        <section className={styles.chatShell}>
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
        activeKey="messages"
        brandMark="聊"
        brandName={t('messages.title')}
        brandSubline={t('messages.shellSubline')}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
        onBrandClick={() => {
          window.location.href = buildMessagesPath();
        }}
        onNavigateToDiscover={() => {
          window.location.href = '/discover';
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
      <ToastContainer />
    </div>
  );
};
