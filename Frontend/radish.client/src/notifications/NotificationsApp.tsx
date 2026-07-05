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
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import { resolveWebNotificationNavigation } from '@/utils/notificationNavigation';
import {
  getNotificationActionScope,
  getNotificationKindIcon,
  getNotificationKindLabelKey,
  getTargetLabel,
  matchesDocsNotification,
  matchesExperienceNotification,
  matchesFollowNotification,
  matchesGovernanceNotification,
  matchesMessageNotification,
  matchesOrderNotification,
  matchesPetNotification,
  NOTIFICATION_PREVIEW_LIMIT,
  resolveNotificationPreview,
  type NotificationPreview,
} from './notificationActionQueue';
import styles from './NotificationsApp.module.css';

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
    recentNotifications.map(resolveNotificationPreview)
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
    const messages = notificationPreviews.filter((item) => matchesMessageNotification(item, item.target)).length;
    const orders = notificationPreviews.filter((item) => matchesOrderNotification(item, item.target)).length;
    const docs = notificationPreviews.filter((item) => matchesDocsNotification(item, item.target)).length;
    const follow = notificationPreviews.filter((item) => matchesFollowNotification(item, item.target)).length;
    const pet = notificationPreviews.filter((item) => matchesPetNotification(item, item.target)).length;
    const experience = notificationPreviews.filter((item) => matchesExperienceNotification(item, item.target)).length;
    const governance = notificationPreviews.filter((item) => matchesGovernanceNotification(item)).length;
    const systems = notificationPreviews.filter((item) => getNotificationActionScope(item, item.target) === 'system').length;
    return [
      { key: 'all', label: t('notification.web.scope.all'), count: notificationPreviews.length },
      { key: 'comments', label: t('notification.web.scope.comments'), count: comments },
      { key: 'messages', label: t('notification.web.scope.messages'), count: messages },
      { key: 'orders', label: t('notification.web.scope.orders'), count: orders },
      { key: 'docs', label: t('notification.web.scope.docs'), count: docs },
      { key: 'follow', label: t('notification.web.scope.follow'), count: follow },
      { key: 'pet', label: t('notification.web.scope.pet'), count: pet },
      { key: 'experience', label: t('notification.web.scope.experience'), count: experience },
      { key: 'governance', label: t('notification.web.scope.governance'), count: governance },
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
