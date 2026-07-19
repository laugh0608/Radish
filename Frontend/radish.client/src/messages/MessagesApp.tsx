import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { WebStateSlot } from '@/components/web-shell';
import { ChatApp, type ChatAppProfileNavigationTarget } from '@/apps/chat/ChatApp';
import { CurrentWindowProvider } from '@/desktop/CurrentWindowContext';
import type { WindowState } from '@/desktop/types';
import { getApiBaseUrl } from '@/config/env';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildPublicProfilePath } from '@/public/profileRouteState';
import { resolvePublicUserRouteIdentifier } from '@/public/publicId';
import { rememberPublicRouteSourceTransfer, type PublicRouteDescriptor } from '@/public/publicRouteNavigation';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { buildMessagesReturnPath } from '@/services/authReturnPath';
import { chatHub } from '@/services/chatHub';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
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
  const [route, setRoute] = useState(parseInitialRoute);
  const [navigationRevision, setNavigationRevision] = useState(0);
  const [searchRestoreRevision, setSearchRestoreRevision] = useState(0);
  const [searchHideRevision, setSearchHideRevision] = useState(0);
  const [searchVisible, setSearchVisible] = useState(false);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const chatHubOwnerRef = useRef(Symbol('messages-page-chat'));

  const navigateToMessagesRoute = useCallback((
    nextRoute: ReturnType<typeof createDefaultMessagesRoute>,
    options: { restoreSearchOnBack?: boolean } = {}
  ) => {
    if (options.restoreSearchOnBack) {
      window.history.replaceState({
        ...window.history.state,
        __radishMessagesSearchRestore: true,
      }, '', `${window.location.pathname}${window.location.search}`);
    }

    const nextPath = buildMessagesPath(nextRoute);
    window.history.pushState(options.restoreSearchOnBack
      ? { __radishMessagesSearchTarget: true }
      : {}, '', nextPath);
    setRoute(nextRoute);
    setNavigationRevision((current) => current + 1);
  }, []);

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
          __navigationKey: `messages:${route.channelId}:${route.messageId ?? 'latest'}:${navigationRevision}`,
        }
      : undefined,
    zIndex: 0,
    isMinimized: false,
    isMaximized: true,
  }), [navigationRevision, route]);

  useEffect(() => {
    const handlePopState = (event: PopStateEvent) => {
      const nextRoute = parseMessagesRoute(window.location.pathname, window.location.search);
      if (!nextRoute) {
        return;
      }

      setRoute(nextRoute);
      setNavigationRevision((current) => current + 1);
      if ((event.state as { __radishMessagesSearchRestore?: unknown } | null)?.__radishMessagesSearchRestore === true) {
        setSearchRestoreRevision((current) => current + 1);
      } else if ((event.state as { __radishMessagesSearchTarget?: unknown } | null)?.__radishMessagesSearchTarget === true) {
        setSearchHideRevision((current) => current + 1);
      }
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

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
    const owner = chatHubOwnerRef.current;
    if (!loggedIn) {
      void chatHub.release(owner);
      return;
    }

    void chatHub.acquire(owner);
    return () => {
      void chatHub.release(owner);
    };
  }, [loggedIn]);

  const handleOpenUserProfile = useCallback((target: ChatAppProfileNavigationTarget) => {
    const targetUserId = resolvePublicUserRouteIdentifier({
      voUserId: String(target.userId),
      voPublicId: target.publicId,
    });
    if (!targetUserId) {
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
            <ChatApp
              readSurfaceMode="page"
              onOpenUserProfile={handleOpenUserProfile}
              onOpenFocusedChannel={(channelId) => {
                navigateToMessagesRoute({ channelId });
              }}
              onOpenMessageResult={(target) => {
                navigateToMessagesRoute(target, { restoreSearchOnBack: true });
              }}
              onBackToConversationList={() => {
                if ((window.history.state as { __radishMessagesSearchTarget?: unknown } | null)?.__radishMessagesSearchTarget === true) {
                  window.history.back();
                  return;
                }

                navigateToMessagesRoute(createDefaultMessagesRoute());
              }}
              searchRestoreRevision={searchRestoreRevision}
              searchHideRevision={searchHideRevision}
              onSearchVisibilityChange={setSearchVisible}
            />
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
        brandMark={t('messages.brandMark')}
        brandName={t('messages.title')}
        brandSubline={t('messages.shellSubline')}
        hideMobileNav={route.channelId !== undefined || searchVisible}
        onBrandClick={() => {
          window.location.href = buildMessagesPath();
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
    </div>
  );
};
