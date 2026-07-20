import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  NotificationCategory,
  NotificationInboxGroupVo,
  UpdateNotificationPreferenceDto,
} from '@radish/http';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import { NotificationCenter } from '@/apps/notification/NotificationCenter';
import { WebStateSlot } from '@/components/web-shell';
import { getApiBaseUrl } from '@/config/env';
import { resolveConsoleExternalUrl } from '@/desktop/externalAppUrl';
import {
  buildNotificationPreferenceUpdates,
  getNotificationCategoryDefinition,
  getUnreadCategoryCount,
  notificationCategoryDefinitions,
} from '@/notifications/notificationInbox';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { rememberPublicRouteSourceTransfer } from '@/public/publicRouteNavigation';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { buildNotificationsReturnPath } from '@/services/authReturnPath';
import { notificationInboxSync } from '@/services/notificationInboxSync';
import { useAuthStore } from '@/stores/authStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import type { NotificationWebNavigationTarget } from '@/utils/notificationNavigation';
import styles from './NotificationsApp.module.css';

export const NotificationsApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const userId = useUserStore((state) => state.userId);
  const summary = useNotificationStore((state) => state.summary);
  const unreadCount = useNotificationStore((state) => state.unreadCount);
  const unreadOccurrenceCount = useNotificationStore((state) => state.unreadOccurrenceCount);
  const connectionState = useNotificationStore((state) => state.connectionState);
  const preferences = useNotificationStore((state) => state.preferences);
  const preferencesLoading = useNotificationStore((state) => state.preferencesLoading);
  const preferencesSaving = useNotificationStore((state) => state.preferencesSaving);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [preferenceError, setPreferenceError] = useState<string | null>(null);
  const [preferenceDraft, setPreferenceDraft] = useState<UpdateNotificationPreferenceDto[]>([]);

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
    if (`${window.location.pathname}${window.location.search}${window.location.hash}` !== '/notifications') {
      window.history.replaceState(window.history.state, '', '/notifications');
    }
  }, [t]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({ returnPath: buildNotificationsReturnPath() });
  }, [authReady, loggedIn, redirecting]);

  const loadPreferences = useCallback(async () => {
    setPreferenceError(null);
    try {
      await notificationInboxSync.loadPreferences();
    } catch (error) {
      setPreferenceError(t('notification.preferences.loadFailed'));
      log.warn('NotificationsApp', '通知偏好加载失败', error);
    }
  }, [t]);

  useEffect(() => {
    if (loggedIn) {
      void loadPreferences();
    }
  }, [loadPreferences, loggedIn]);

  useEffect(() => {
    setPreferenceDraft(buildNotificationPreferenceUpdates(preferences));
  }, [preferences]);

  const updatePreference = useCallback((
    category: NotificationCategory,
    field: 'inAppEnabled' | 'realtimePreviewEnabled',
    enabled: boolean,
  ) => {
    setPreferenceDraft((current) => current.map((item) => (
      item.category === category ? { ...item, [field]: enabled } : item
    )));
  }, []);

  const savePreferences = useCallback(async () => {
    setPreferenceError(null);
    try {
      await notificationInboxSync.updatePreferences(preferenceDraft);
      toast.success(t('notification.preferences.saved'));
    } catch (error) {
      setPreferenceError(t('notification.preferences.saveFailed'));
      log.warn('NotificationsApp', '通知偏好保存失败', error);
      toast.error(t('notification.preferences.saveFailed'));
    }
  }, [preferenceDraft, t]);

  const handleNavigateTarget = useCallback((
    _group: NotificationInboxGroupVo,
    target: NotificationWebNavigationTarget,
  ) => {
    if (target.surface === 'web' && target.sourceState) {
      rememberPublicRouteSourceTransfer(target.href, target.sourceState);
    }
    if (target.surface === 'web') {
      window.history.pushState({}, '', target.href);
      window.dispatchEvent(new PopStateEvent('popstate', { state: {} }));
    } else {
      window.location.href = resolveConsoleExternalUrl(target.href);
    }
    return true;
  }, []);

  const connectionLabel = connectionState === 'connected'
    ? t('notification.web.connected')
    : t(connectionState === 'connecting' || connectionState === 'reconnecting'
      ? 'notification.web.connecting'
      : 'notification.web.disconnected');
  const preferencesDirty = JSON.stringify(preferenceDraft) !== JSON.stringify(
    buildNotificationPreferenceUpdates(preferences),
  );

  let content;
  if (!authReady || redirecting || !loggedIn) {
    content = (
      <WebStateSlot
        tone={!authReady ? 'loading' : 'auth'}
        title={!authReady ? t('notification.web.loading') : t('notification.web.redirecting')}
        description={t('notification.web.authHint')}
      />
    );
  } else {
    content = (
      <div className={styles.contentGrid}>
        <section className={styles.summaryPanel} aria-labelledby="notifications-heading">
          <div className={styles.summaryHeader}>
            <span className={styles.kicker}>{t('notification.web.kicker')}</span>
            <h1 id="notifications-heading">{t('notification.web.heading')}</h1>
            <p>{t('notification.web.description')}</p>
          </div>
          <div className={styles.summaryCards} aria-label={t('notification.web.summaryLabel')}>
            <div className={styles.summaryCard}>
              <Icon icon="mdi:bell-badge-outline" size={23} />
              <strong>{unreadCount}</strong>
              <span>{t('notification.web.unreadMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <Icon icon="mdi:layers-triple-outline" size={23} />
              <strong>{unreadOccurrenceCount}</strong>
              <span>{t('notification.web.occurrenceMetric')}</span>
            </div>
            <div className={styles.summaryCard}>
              <Icon icon="mdi:sync-circle" size={23} />
              <strong>{summary?.voRevision ?? '—'}</strong>
              <span>{t('notification.web.revisionMetric')}</span>
            </div>
          </div>
        </section>

        <div className={styles.notificationWorkspace}>
          <div className={styles.centerShell}>
            <NotificationCenter headingLevel="h2" onNavigateTarget={handleNavigateTarget} />
          </div>

          <aside className={styles.notificationRail} aria-label={t('notification.web.railLabel')}>
            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.web.categorySummary')}</span>
                <strong>{connectionLabel}</strong>
              </div>
              <div className={styles.categorySummary}>
                {notificationCategoryDefinitions.map((definition) => (
                  <div key={definition.category}>
                    <span>
                      <Icon icon={definition.icon} size={17} />
                      {t(definition.labelKey)}
                    </span>
                    <strong>{getUnreadCategoryCount(summary, definition.category)}</strong>
                  </div>
                ))}
              </div>
              <p className={styles.connectionHint}>
                {connectionState === 'connected'
                  ? t('notification.web.connectionSyncedHint')
                  : t('notification.web.connectionRecoveringHint')}
              </p>
            </section>

            <section className={styles.railCard}>
              <div className={styles.railTitleRow}>
                <span>{t('notification.preferences.title')}</span>
                {preferencesLoading && <strong>{t('notification.shared.loading')}</strong>}
              </div>
              <p className={styles.preferenceHint}>{t('notification.preferences.description')}</p>
              {preferenceError && (
                <div className={styles.preferenceError} role="alert">
                  <span>{preferenceError}</span>
                  <button onClick={() => void loadPreferences()} type="button">{t('notification.retry')}</button>
                </div>
              )}
              <div className={styles.preferences}>
                {preferences.map((preference) => {
                  const draft = preferenceDraft.find((item) => item.category === preference.voCategory);
                  const definition = getNotificationCategoryDefinition(preference.voCategory);
                  return (
                    <fieldset key={preference.voCategory}>
                      <legend>
                        <Icon icon={definition.icon} size={17} />
                        {t(definition.labelKey)}
                      </legend>
                      <label>
                        <span>{t('notification.preferences.inApp')}</span>
                        <input
                          checked={draft?.inAppEnabled ?? preference.voInAppEnabled}
                          disabled={!preference.voCanDisableInApp}
                          onChange={(event) => updatePreference(preference.voCategory, 'inAppEnabled', event.target.checked)}
                          type="checkbox"
                        />
                      </label>
                      <label>
                        <span>{t('notification.preferences.realtime')}</span>
                        <input
                          checked={draft?.realtimePreviewEnabled ?? preference.voRealtimePreviewEnabled}
                          disabled={!preference.voCanDisableRealtimePreview}
                          onChange={(event) => updatePreference(preference.voCategory, 'realtimePreviewEnabled', event.target.checked)}
                          type="checkbox"
                        />
                      </label>
                    </fieldset>
                  );
                })}
              </div>
              <button
                className={styles.savePreferences}
                disabled={!preferencesDirty || preferencesSaving || preferences.length === 0}
                onClick={() => void savePreferences()}
                type="button"
              >
                {preferencesSaving ? t('notification.preferences.saving') : t('notification.preferences.save')}
              </button>
            </section>
          </aside>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="notifications"
        brandMark="萝"
        brandName={t('notification.title')}
        brandSubline={t('notification.web.shellSubline')}
        onBrandClick={() => { window.location.href = '/notifications'; }}
      />
      <main className={styles.main}>{content}</main>
    </div>
  );
};
