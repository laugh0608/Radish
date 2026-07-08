import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { ToastContainer, toast } from '@radish/ui/toast';
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
import { copyToClipboard } from '@/utils/clipboard';
import { log } from '@/utils/logger';
import { resolveWebNotificationNavigation } from '@/utils/notificationNavigation';
import {
  buildNotificationActionGroups,
  getNotificationActionScope,
  getNotificationKindIcon,
  getNotificationKindLabelKey,
  getNotificationScopeDefinition,
  getNotificationTargetHintKey,
  getTargetLabel,
  resolveNotificationPreview,
  type NotificationActionScope,
  type NotificationPreview,
} from './notificationActionQueue';
import styles from './NotificationsApp.module.css';

const notificationScopeChipOrder: NotificationActionScope[] = [
  'all',
  'posts',
  'comments',
  'answers',
  'messages',
  'follow',
  'governance',
  'orders',
  'docs',
  'pet',
  'experience',
  'likes',
  'system',
];

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
  const queueSourcePreviews = useMemo(() => {
    const unreadItems = notificationPreviews.filter((item) => !item.isRead);
    return unreadItems.length > 0 ? unreadItems : notificationPreviews;
  }, [notificationPreviews]);
  const queueGroups = useMemo(() => (
    buildNotificationActionGroups(queueSourcePreviews)
  ), [queueSourcePreviews]);
  const scopeChips = useMemo(() => {
    const scopeCounts = new Map<NotificationActionScope, number>([
      ['all', notificationPreviews.length],
    ]);

    for (const item of notificationPreviews) {
      const scope = getNotificationActionScope(item, item.target);
      scopeCounts.set(scope, (scopeCounts.get(scope) ?? 0) + 1);
    }

    return notificationScopeChipOrder.map((scope) => {
      const definition = getNotificationScopeDefinition(scope);
      return {
        key: scope,
        label: t(definition.labelKey),
        count: scopeCounts.get(scope) ?? 0,
      };
    });
  }, [notificationPreviews, t]);
  const connectionLabel = connectionState === 'connected'
    ? t('notification.web.connected')
    : t(connectionState === 'connecting' || connectionState === 'reconnecting'
      ? 'notification.web.connecting'
      : 'notification.web.disconnected');
  const connectionHint = connectionState === 'connected'
    ? t('notification.web.connectionSyncedHint')
    : t(connectionState === 'connecting' || connectionState === 'reconnecting'
      ? 'notification.web.connectionRecoveringHint'
      : 'notification.web.connectionOfflineHint');

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
    if (!notification.target) {
      return;
    }

    event.preventDefault();
    handleNavigateNotification(notification);
  }, [handleNavigateNotification]);

  const handleCopyNotificationDiagnostics = useCallback(async (
    notification: NotificationPreview,
    targetText: string
  ) => {
    const diagnosticLines = [
      'Radish notification support context',
      `id: ${notification.id || 'unknown'}`,
      `type: ${notification.type || 'unknown'}`,
      `businessType: ${notification.businessType || 'unknown'}`,
      `businessId: ${notification.businessId || 'unknown'}`,
      `createdAt: ${notification.createdAt || 'unknown'}`,
      `isRead: ${notification.isRead ? 'yes' : 'no'}`,
      `target: ${notification.target?.href ?? 'missing'}`,
      `targetHint: ${targetText}`,
      `path: ${window.location.pathname}${window.location.search}${window.location.hash}`,
    ];

    try {
      await copyToClipboard(diagnosticLines.join('\n'));
      toast.success(t('notification.web.diagnosticsCopied'));
    } catch (error) {
      log.warn('NotificationsApp', '复制通知诊断上下文失败', error);
      toast.error(t('notification.web.diagnosticsCopyFailed'));
    }
  }, [t]);

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
          <section className={styles.centerShell} id="notification-center">
            <NotificationCenter headingLevel="h2" onNavigateNotification={handleNavigateNotification} />
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
              <p className={styles.connectionHint}>{connectionHint}</p>
            </section>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.web.queueTitle')}</span>
                <strong>{queueSourcePreviews.length}</strong>
              </div>
              <div className={styles.notificationQueue}>
                {queueGroups.length > 0 ? queueGroups.map((group) => (
                  <div className={styles.queueGroup} key={group.scope}>
                    <div className={styles.queueGroupHeader}>
                      <span className={styles.queueGroupTitle}>
                        <Icon icon={group.definition.icon} size={17} />
                        {t(group.definition.labelKey)}
                      </span>
                      <span className={styles.queueGroupMeta}>
                        {t('notification.web.queueGroupMeta', {
                          total: group.totalCount,
                          unread: group.unreadCount,
                          routed: group.routedCount,
                          manual: group.manualCount,
                        })}
                      </span>
                    </div>
                    <div className={styles.queueGroupList}>
                      {group.items.map((item) => {
                        const targetLabel = getTargetLabel(item.target);
                        const hasTarget = item.target !== null;
                        const targetHintKey = getNotificationTargetHintKey(item, item.target);
                        const targetText = hasTarget
                          ? t(targetHintKey, { target: targetLabel })
                          : t(targetHintKey);
                        const queueItemContent = (
                          <>
                            <span className={styles.queueIcon}>
                              <Icon icon={getNotificationKindIcon(item, item.target)} size={18} />
                            </span>
                            <span className={styles.queueBody}>
                              <strong>{item.title || t(getNotificationKindLabelKey(item, item.target))}</strong>
                              <span>{item.content || t('notification.web.emptyContent')}</span>
                              <em>{targetText}</em>
                            </span>
                            {!item.isRead && <span className={styles.queueUnread} aria-label={t('notification.web.unreadDot')} />}
                          </>
                        );

                        return hasTarget ? (
                          <a
                            className={styles.queueItem}
                            href={item.target?.href ?? '#notification-center'}
                            key={item.id}
                            onClick={(event) => handlePreviewTargetClick(event, item)}
                          >
                            {queueItemContent}
                            <span className={styles.queueAction}>
                              {t('notification.web.queueActionOpen')}
                            </span>
                          </a>
                        ) : (
                          <div className={`${styles.queueItem} ${styles.queueItemManual}`} key={item.id}>
                            {queueItemContent}
                            <button
                              type="button"
                              className={`${styles.queueAction} ${styles.queueActionButton}`}
                              onClick={() => {
                                void handleCopyNotificationDiagnostics(item, targetText);
                              }}
                            >
                              {t('notification.web.queueActionCopyDiagnostics')}
                            </button>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )) : (
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
                <span>{t('notification.web.targetMessages')}</span>
                <span>{t('notification.web.targetFollow')}</span>
                <span>{t('notification.web.targetGovernance')}</span>
                <span>{t('notification.web.targetOrder')}</span>
                <span>{t('notification.web.targetDocs')}</span>
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
        activeKey="notifications"
        brandMark="萝"
        brandName={t('notification.title')}
        brandSubline={t('notification.web.shellSubline')}
        onBrandClick={() => {
          window.location.href = '/notifications';
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
      <ToastContainer />
    </div>
  );
};
