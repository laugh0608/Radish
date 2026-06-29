import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import styles from './NotificationsApp.module.css';

export const NotificationsApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const unreadCount = useNotificationStore(state => state.unreadCount);
  const recentNotifications = useNotificationStore(state => state.recentNotifications);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const hasStartedHubRef = useRef(false);

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
              <strong>{recentNotifications.length}</strong>
              <span>{t('notification.web.recentMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <span className={styles.summaryIcon}>
                <Icon icon="mdi:link-variant" size={22} />
              </span>
              <strong>{t('notification.web.routeMetricValue')}</strong>
              <span>{t('notification.web.routeMetric')}</span>
            </div>
          </div>
        </section>
        <section className={styles.centerShell}>
          <NotificationCenter onNavigateNotification={handleNavigateNotification} />
        </section>
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
