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
import { areEntityIdsEqual, normalizeEntityId, type ChannelVo } from '@/types/chat';
import { log } from '@/utils/logger';
import { buildMessagesPath, createDefaultMessagesRoute, parseMessagesRoute } from './messagesRouteState';
import styles from './MessagesApp.module.css';

const CHANNEL_PREVIEW_LIMIT = 4;

function parseInitialRoute() {
  if (typeof window === 'undefined') {
    return createDefaultMessagesRoute();
  }

  return parseMessagesRoute(window.location.pathname, window.location.search) ?? createDefaultMessagesRoute();
}

function getChannelTime(channel: ChannelVo): number {
  const latestTime = Date.parse(channel.voLastMessage?.voCreateTime ?? '');
  return Number.isNaN(latestTime) ? 0 : latestTime;
}

function getChannelPreview(channel: ChannelVo, fallback: string): string {
  const latestMessage = channel.voLastMessage;
  if (!latestMessage) {
    return channel.voDescription?.trim() || fallback;
  }

  if (latestMessage.voIsRecalled) {
    return fallback;
  }

  const content = latestMessage.voContent?.trim();
  if (content) {
    return content;
  }

  if (latestMessage.voImageUrl || latestMessage.voImageThumbnailUrl) {
    return fallback;
  }

  return channel.voDescription?.trim() || fallback;
}

function getConnectionLabelKey(connectionState: ReturnType<typeof useChatStore.getState>['connectionState']): string {
  if (connectionState === 'connected') {
    return 'messages.web.connectedMetricValue';
  }

  if (connectionState === 'connecting' || connectionState === 'reconnecting') {
    return 'messages.web.connectingMetricValue';
  }

  return 'messages.web.disconnectedMetricValue';
}

export const MessagesApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const route = useMemo(() => parseInitialRoute(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const channels = useChatStore(state => state.channels);
  const activeChannelId = useChatStore(state => state.activeChannelId);
  const messageMap = useChatStore(state => state.messageMap);
  const typingMap = useChatStore(state => state.typingMap);
  const connectionState = useChatStore(state => state.connectionState);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const hasStartedHubRef = useRef(false);
  const activeChannelKey = normalizeEntityId(activeChannelId);
  const routeChannelKey = normalizeEntityId(route.channelId);
  const activeChannel = useMemo(() => {
    if (!activeChannelId) {
      return null;
    }

    return channels.find((channel) => areEntityIdsEqual(channel.voId, activeChannelId)) ?? null;
  }, [activeChannelId, channels]);
  const activeMessages = activeChannelKey ? (messageMap[activeChannelKey] ?? []) : [];
  const typingUsers = activeChannelKey ? (typingMap[activeChannelKey] ?? []) : [];
  const unreadTotal = useMemo(() => (
    channels.reduce((sum, channel) => sum + Math.max(0, channel.voUnreadCount), 0)
  ), [channels]);
  const mentionChannelCount = useMemo(() => (
    channels.filter((channel) => channel.voHasMention).length
  ), [channels]);
  const channelPreviews = useMemo(() => (
    [...channels]
      .sort((left, right) => {
        const mentionDiff = Number(right.voHasMention) - Number(left.voHasMention);
        if (mentionDiff !== 0) {
          return mentionDiff;
        }

        const unreadDiff = right.voUnreadCount - left.voUnreadCount;
        if (unreadDiff !== 0) {
          return unreadDiff;
        }

        const timeDiff = getChannelTime(right) - getChannelTime(left);
        if (timeDiff !== 0) {
          return timeDiff;
        }

        return left.voSort - right.voSort;
      })
      .slice(0, CHANNEL_PREVIEW_LIMIT)
  ), [channels]);
  const focusedRouteLabel = routeChannelKey
    ? t(route.messageId ? 'messages.web.focusedMessage' : 'messages.web.focusedChannel')
    : t('messages.web.pendingMetricValue');

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
              <strong>{focusedRouteLabel}</strong>
              <span>{t('messages.web.routeMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:access-point" size={22} />
              </span>
              <strong>{t(getConnectionLabelKey(connectionState))}</strong>
              <span>{t('messages.web.connectionMetric')}</span>
            </div>
          </div>
        </section>
        <div className={styles.messagesWorkspace}>
          <section className={styles.chatShell}>
            <CurrentWindowProvider value={virtualWindow}>
              <ChatApp onOpenUserProfile={handleOpenUserProfile} />
            </CurrentWindowProvider>
          </section>
          <aside className={styles.messageRail} aria-label={t('messages.web.railLabel')}>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('messages.web.contextTitle')}</span>
                <strong>{t(getConnectionLabelKey(connectionState))}</strong>
              </div>
              <div className={styles.activeChannelCard}>
                <span className={styles.activeChannelIcon}>
                  <Icon icon={activeChannel?.voHasMention ? 'mdi:at' : 'mdi:message-text-outline'} size={20} />
                </span>
                <div>
                  <strong>{activeChannel?.voName ?? t('messages.web.noActiveChannel')}</strong>
                  <span>{activeChannel ? getChannelPreview(activeChannel, t('messages.web.noMessagePreview')) : t('messages.web.noActiveHint')}</span>
                </div>
              </div>
              <div className={styles.railMetrics}>
                <div>
                  <strong>{unreadTotal}</strong>
                  <span>{t('messages.web.unreadMetric')}</span>
                </div>
                <div>
                  <strong>{mentionChannelCount}</strong>
                  <span>{t('messages.web.mentionMetric')}</span>
                </div>
              </div>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('messages.web.routeContextTitle')}</span>
                <strong>{route.messageId ? t('messages.web.messageAnchor') : t('messages.web.channelAnchor')}</strong>
              </div>
              <div className={styles.contextRows}>
                <div>
                  <span>{t('messages.web.activeMessages')}</span>
                  <strong>{activeMessages.length}</strong>
                </div>
                <div>
                  <span>{t('messages.web.typingUsers')}</span>
                  <strong>{typingUsers.length}</strong>
                </div>
                <div>
                  <span>{t('messages.web.routeTarget')}</span>
                  <strong>{routeChannelKey ? `#${routeChannelKey}` : t('messages.web.noRouteTarget')}</strong>
                </div>
              </div>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('messages.web.conversationQueue')}</span>
                <strong>{channelPreviews.length}</strong>
              </div>
              <div className={styles.channelQueue}>
                {channelPreviews.length > 0 ? channelPreviews.map((channel) => {
                  const channelKey = normalizeEntityId(channel.voId);
                  const href = channelKey ? buildMessagesPath({ channelId: channelKey }) : buildMessagesPath();
                  return (
                    <a className={styles.channelQueueItem} href={href} key={channelKey ?? channel.voName}>
                      <span className={styles.channelBadge}>
                        {channel.voHasMention ? <Icon icon="mdi:at" size={16} /> : channel.voIconEmoji || <Icon icon="mdi:forum-outline" size={16} />}
                      </span>
                      <span className={styles.channelQueueBody}>
                        <strong>{channel.voName}</strong>
                        <span>{getChannelPreview(channel, t('messages.web.noMessagePreview'))}</span>
                      </span>
                      {channel.voUnreadCount > 0 && (
                        <em>{channel.voUnreadCount}</em>
                      )}
                    </a>
                  );
                }) : (
                  <div className={styles.railEmpty}>
                    <Icon icon="mdi:message-check-outline" size={20} />
                    <span>{t('messages.web.noChannels')}</span>
                  </div>
                )}
              </div>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railActions}>
                <a href="/notifications">{t('messages.web.openNotifications')}</a>
                <a href={buildMessagesPath()}>{t('messages.web.openAllChannels')}</a>
              </div>
            </section>
          </aside>
        </div>
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
