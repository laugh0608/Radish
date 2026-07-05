import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { ToastContainer } from '@radish/ui/toast';
import type { NotificationItemData } from '@radish/ui/notification';
import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { WebStateSlot } from '@/components/web-shell';
import { NotificationCenter } from '@/apps/notification/NotificationCenter';
import { getApiBaseUrl } from '@/config/env';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { redirectToLogin } from '@/services/auth';
import { buildNotificationsReturnPath } from '@/services/authReturnPath';
import { rememberPublicRouteSourceTransfer } from '@/public/publicRouteNavigation';
import { notificationHub } from '@/services/notificationHub';
import { useAuthStore } from '@/stores/authStore';
import { useNotificationStore, type NotificationItem } from '@/stores/notificationStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import {
  resolveWebNotificationNavigation,
  type NotificationWebNavigationTarget
} from '@/utils/notificationNavigation';
import styles from './NotificationsApp.module.css';

const NOTIFICATION_PREVIEW_LIMIT = 4;

type NotificationPreview = NotificationItemData & {
  target: NotificationWebNavigationTarget | null;
};

function toNotificationItemData(notification: NotificationItem): NotificationItemData {
  return {
    id: String(notification.id),
    type: notification.type,
    title: notification.title,
    content: notification.content,
    businessType: notification.businessType,
    businessId: notification.businessId == null ? null : String(notification.businessId),
    triggerId: notification.triggerId == null ? null : String(notification.triggerId),
    triggerName: notification.triggerName,
    triggerAvatar: notification.triggerAvatar,
    extData: notification.extData,
    isRead: notification.isRead,
    createdAt: notification.createdAt
  };
}

function includesBusinessKeyword(notification: NotificationItemData, keyword: string): boolean {
  const businessType = notification.businessType?.toLowerCase() ?? '';
  const title = notification.title.toLowerCase();
  const content = notification.content.toLowerCase();
  return businessType.includes(keyword) || title.includes(keyword) || content.includes(keyword);
}

function matchesOrderNotification(notification: NotificationItemData, target: NotificationWebNavigationTarget | null): boolean {
  return target?.href.startsWith('/shop/order') === true
    || includesBusinessKeyword(notification, 'order')
    || notification.title.includes('订单')
    || notification.content.includes('订单');
}

function matchesDocsNotification(notification: NotificationItemData, target: NotificationWebNavigationTarget | null): boolean {
  return target?.href.startsWith('/docs') === true
    || includesBusinessKeyword(notification, 'doc')
    || includesBusinessKeyword(notification, 'wiki')
    || notification.title.includes('文档')
    || notification.content.includes('文档');
}

function getNotificationKindLabelKey(notification: NotificationItemData, target: NotificationWebNavigationTarget | null): string {
  if (matchesOrderNotification(notification, target)) {
    return 'notification.web.scope.orders';
  }

  if (matchesDocsNotification(notification, target)) {
    return 'notification.web.scope.docs';
  }

  if (notification.type === 'reply' || notification.type === 'mention') {
    return 'notification.web.scope.comments';
  }

  if (notification.type === 'like') {
    return 'notification.web.scope.likes';
  }

  return 'notification.web.scope.system';
}

function getNotificationKindIcon(notification: NotificationItemData, target: NotificationWebNavigationTarget | null): string {
  if (matchesOrderNotification(notification, target)) {
    return 'mdi:receipt-text-outline';
  }

  if (matchesDocsNotification(notification, target)) {
    return 'mdi:file-document-outline';
  }

  if (notification.type === 'reply' || notification.type === 'mention') {
    return 'mdi:comment-text-outline';
  }

  if (notification.type === 'like') {
    return 'mdi:heart-outline';
  }

  return 'mdi:bell-outline';
}

function getTargetLabel(target: NotificationWebNavigationTarget | null): string {
  return target?.href ?? '';
}

export const NotificationsApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const unreadCount = useNotificationStore(state => state.unreadCount);
  const connectionState = useNotificationStore(state => state.connectionState);
  const recentNotifications = useNotificationStore(state => state.recentNotifications);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const hasStartedHubRef = useRef(false);
  const notificationPreviews = useMemo<NotificationPreview[]>(() => (
    recentNotifications.map((notification) => {
      const item = toNotificationItemData(notification);
      return {
        ...item,
        target: resolveWebNotificationNavigation(item)
      };
    })
  ), [recentNotifications]);
  const routedNotificationCount = useMemo(() => (
    notificationPreviews.filter((item) => item.target !== null).length
  ), [notificationPreviews]);
  const unsupportedNotificationCount = Math.max(0, notificationPreviews.length - routedNotificationCount);
  const unreadPreviewCount = useMemo(() => (
    notificationPreviews.filter((item) => !item.isRead).length
  ), [notificationPreviews]);
  const queuePreviews = useMemo(() => {
    const unreadItems = notificationPreviews.filter((item) => !item.isRead);
    const sourceItems = unreadItems.length > 0 ? unreadItems : notificationPreviews;
    return sourceItems.slice(0, NOTIFICATION_PREVIEW_LIMIT);
  }, [notificationPreviews]);
  const scopeChips = useMemo(() => {
    const comments = notificationPreviews.filter((item) => item.type === 'reply' || item.type === 'mention').length;
    const orders = notificationPreviews.filter((item) => matchesOrderNotification(item, item.target)).length;
    const docs = notificationPreviews.filter((item) => matchesDocsNotification(item, item.target)).length;
    const systems = notificationPreviews.filter((item) => item.type === 'system').length;
    return [
      { key: 'all', label: t('notification.web.scope.all'), count: notificationPreviews.length },
      { key: 'comments', label: t('notification.web.scope.comments'), count: comments },
      { key: 'orders', label: t('notification.web.scope.orders'), count: orders },
      { key: 'docs', label: t('notification.web.scope.docs'), count: docs },
      { key: 'system', label: t('notification.web.scope.system'), count: systems }
    ];
  }, [notificationPreviews, t]);
  const connectionLabel = connectionState === 'connected'
    ? t('notification.web.connected')
    : t(connectionState === 'connecting' || connectionState === 'reconnecting'
      ? 'notification.web.connecting'
      : 'notification.web.disconnected');

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('NotificationsApp', '通知页登录态初始化失败', error);
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
    document.title = `${t('notification.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    const canonicalPath = '/notifications';
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, []);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildNotificationsReturnPath()
    });
  }, [authReady, loggedIn, redirecting]);

  useEffect(() => {
    if (loggedIn && !hasStartedHubRef.current) {
      hasStartedHubRef.current = true;
      void notificationHub.start();
    } else if (!loggedIn && hasStartedHubRef.current) {
      hasStartedHubRef.current = false;
      void notificationHub.stop();
    }

    return () => {
      hasStartedHubRef.current = false;
      setTimeout(() => {
        if (!hasStartedHubRef.current) {
          void notificationHub.stop();
        }
      }, 100);
    };
  }, [loggedIn]);

  const handleNavigateNotification = useCallback((notification: NotificationItemData) => {
    const target = resolveWebNotificationNavigation(notification);
    if (!target || typeof window === 'undefined') {
      return false;
    }

    if (target.surface === 'web' && target.sourceState) {
      rememberPublicRouteSourceTransfer(target.href, target.sourceState);
    }

    window.location.href = target.href;
    return true;
  }, []);

  const handlePreviewTargetClick = useCallback((
    event: MouseEvent<HTMLAnchorElement>,
    notification: NotificationPreview
  ) => {
    event.preventDefault();
    handleNavigateNotification(notification);
  }, [handleNavigateNotification]);

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="auth"
            icon="mdi:bell-ring-outline"
            title={t('notification.title')}
            description={t(redirecting ? 'notification.web.redirecting' : 'notification.web.loading')}
          />
        </section>
      );
    }

    return (
      <div className={styles.contentGrid}>
        <section className={styles.summaryPanel} aria-label={t('notification.web.summaryLabel')}>
          <div className={styles.summaryHeader}>
            <span className={styles.kicker}>{t('notification.web.kicker')}</span>
            <h1>{t('notification.web.heading')}</h1>
            <p>{t('notification.web.description')}</p>
            <div className={styles.scopeChips} aria-label={t('notification.web.scopeLabel')}>
              {scopeChips.map((scope) => (
                <span className={styles.scopeChip} key={scope.key}>
                  <strong>{scope.count}</strong>
                  {scope.label}
                </span>
              ))}
            </div>
          </div>
          <div className={styles.summaryCards}>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:bell-badge-outline" size={22} />
              </span>
              <strong>{unreadCount}</strong>
              <span>{t('notification.web.unreadMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:history" size={22} />
              </span>
              <strong>{unreadPreviewCount}</strong>
              <span>{t('notification.web.recentMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:link-variant" size={22} />
              </span>
              <strong>{routedNotificationCount}</strong>
              <span>{t('notification.web.routeMetric')}</span>
            </div>
          </div>
        </section>
        <div className={styles.notificationWorkspace}>
          <section className={styles.centerShell}>
            <NotificationCenter onNavigateNotification={handleNavigateNotification} />
          </section>
          <aside className={styles.notificationRail} aria-label={t('notification.web.railLabel')}>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.web.railStatusTitle')}</span>
                <strong>{connectionLabel}</strong>
              </div>
              <div className={styles.railMetrics}>
                <div>
                  <strong>{routedNotificationCount}</strong>
                  <span>{t('notification.web.routedMetric')}</span>
                </div>
                <div>
                  <strong>{unsupportedNotificationCount}</strong>
                  <span>{t('notification.web.unsupportedMetric')}</span>
                </div>
              </div>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.web.queueTitle')}</span>
                <strong>{queuePreviews.length}</strong>
              </div>
              <div className={styles.notificationQueue}>
                {queuePreviews.length > 0 ? queuePreviews.map((item) => {
                  const targetLabel = getTargetLabel(item.target);
                  return (
                    <a
                      className={styles.queueItem}
                      href={item.target?.href ?? '/notifications'}
                      key={item.id}
                      onClick={(event) => handlePreviewTargetClick(event, item)}
                    >
                      <span className={styles.queueIcon}>
                        <Icon icon={getNotificationKindIcon(item, item.target)} size={18} />
                      </span>
                      <span className={styles.queueBody}>
                        <strong>{item.title || t(getNotificationKindLabelKey(item, item.target))}</strong>
                        <span>{item.content || t('notification.web.emptyContent')}</span>
                        <em>{targetLabel ? t('notification.web.openTarget', { target: targetLabel }) : t('notification.web.noTarget')}</em>
                      </span>
                      {!item.isRead && <span className={styles.queueUnread} aria-label={t('notification.web.unreadDot')} />}
                    </a>
                  );
                }) : (
                  <div className={styles.railEmpty}>
                    <Icon icon="mdi:check-circle-outline" size={20} />
                    <span>{t('notification.web.queueEmpty')}</span>
                  </div>
                )}
              </div>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.web.targetTitle')}</span>
              </div>
              <div className={styles.targetRules}>
                <span>{t('notification.web.targetForum')}</span>
                <span>{t('notification.web.targetOrder')}</span>
                <span>{t('notification.web.targetDocs')}</span>
                <span>{t('notification.web.targetMessages')}</span>
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
        brandMark="萝"
        brandName={t('notification.title')}
        brandSubline={t('notification.web.shellSubline')}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
        onBrandClick={() => {
          window.location.href = '/notifications';
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
