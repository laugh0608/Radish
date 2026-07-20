import { useCallback, useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotificationCategory, NotificationInboxGroupVo } from '@radish/http';
import { formatLocalizedRelativeTime } from '@radish/ui';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import { getApiBaseUrl } from '@/config/env';
import {
  getNotificationCategoryDefinition,
  getNotificationTargetUnavailableKey,
  getUnreadCategoryCount,
  notificationCategoryDefinitions,
  parseNotificationCount,
} from '@/notifications/notificationInbox';
import { notificationInboxSync } from '@/services/notificationInboxSync';
import { useNotificationStore } from '@/stores/notificationStore';
import { resolveMediaUrl } from '@/utils/media';
import { log } from '@/utils/logger';
import {
  resolveWebNotificationNavigation,
  type NotificationWebNavigationTarget,
} from '@/utils/notificationNavigation';
import styles from './NotificationCenter.module.css';

interface NotificationCenterProps {
  headingLevel?: 'h1' | 'h2';
  onNavigateTarget?: (
    group: NotificationInboxGroupVo,
    target: NotificationWebNavigationTarget,
  ) => boolean;
}

export const NotificationCenter = ({
  headingLevel = 'h1',
  onNavigateTarget,
}: NotificationCenterProps) => {
  const { t, i18n } = useTranslation();
  const Heading = headingLevel;
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const {
    summary,
    groups,
    nextCursor,
    activeCategory,
    onlyUnread,
    connectionState,
    loadState,
    loadingMore,
    hasNewerRevision,
    setFilters,
  } = useNotificationStore();
  const initialShowLoading = useRef(groups.length === 0);

  const refresh = useCallback(async (showLoading = true) => {
    try {
      await notificationInboxSync.refreshFirstPage({ showLoading });
    } catch (error) {
      log.warn('NotificationCenter', '加载权威通知列表失败', error);
      toast.error(t('notification.loadFailed'));
    }
  }, [t]);

  useEffect(() => {
    void refresh(initialShowLoading.current);
  }, [refresh]);

  const changeFilters = useCallback((category: NotificationCategory | null, unread: boolean) => {
    setFilters(category, unread);
    void refresh(true);
  }, [refresh, setFilters]);

  const markGroupAsRead = useCallback(async (group: NotificationInboxGroupVo) => {
    if (group.voIsRead) {
      return;
    }

    try {
      await notificationInboxSync.markGroupsAsRead([group.voGroupId]);
      toast.success(t('notification.markReadSuccess'));
    } catch (error) {
      log.warn('NotificationCenter', '标记通知分组已读失败', error);
      toast.error(t('notification.markReadFailed'));
    }
  }, [t]);

  const markAllAsRead = useCallback(async () => {
    try {
      await notificationInboxSync.markAllAsRead(activeCategory ?? undefined);
      toast.success(t('notification.markAllReadSuccess'));
    } catch (error) {
      log.warn('NotificationCenter', '标记分类通知已读失败', error);
      toast.error(t('notification.markAllReadFailed'));
    }
  }, [activeCategory, t]);

  const deleteGroup = useCallback(async (group: NotificationInboxGroupVo) => {
    if (!window.confirm(t('notification.deleteConfirm'))) {
      return;
    }

    try {
      await notificationInboxSync.deleteGroup(group.voGroupId);
      toast.success(t('notification.deleteSuccess'));
    } catch (error) {
      log.warn('NotificationCenter', '删除通知分组失败', error);
      toast.error(t('notification.deleteFailed'));
    }
  }, [t]);

  const openTarget = useCallback((group: NotificationInboxGroupVo) => {
    const target = resolveWebNotificationNavigation(group.voTarget);
    if (!target) {
      return;
    }

    if (!group.voIsRead) {
      void notificationInboxSync.markGroupsAsRead([group.voGroupId]).catch((error) => {
        log.warn('NotificationCenter', '打开目标时标记通知分组已读失败', error);
      });
    }

    if (onNavigateTarget?.(group, target)) {
      return;
    }

    if (target.sourceState) {
      window.history.replaceState(target.sourceState, '', window.location.href);
    }
    window.location.href = target.href;
  }, [onNavigateTarget]);

  const loadMore = useCallback(async () => {
    try {
      const result = await notificationInboxSync.loadMore();
      if (result === 'cursor-expired') {
        toast.info(t('notification.cursorExpiredRecovered'));
      }
    } catch (error) {
      log.warn('NotificationCenter', '加载更多权威通知失败', error);
      toast.error(t('notification.loadMoreFailed'));
    }
  }, [t]);

  const reconcile = useCallback(async () => {
    try {
      await notificationInboxSync.reconcile({ refreshListWhenChanged: true });
    } catch (error) {
      log.warn('NotificationCenter', '手动恢复通知权威状态失败', error);
      toast.error(t('notification.loadFailed'));
    }
  }, [t]);

  const locale = i18n.resolvedLanguage ?? i18n.language;
  const currentUnread = activeCategory
    ? getUnreadCategoryCount(summary, activeCategory)
    : parseNotificationCount(summary?.voUnreadGroupCount);
  const isOffline = connectionState === 'disconnected' || connectionState === 'reconnecting';

  return (
    <section className={styles.center} aria-labelledby="notification-center-title">
      <header className={styles.header}>
        <div>
          <Heading id="notification-center-title">{t('notification.title')}</Heading>
          <p>{t('notification.inboxRevision', { revision: summary?.voRevision ?? '—' })}</p>
        </div>
        <button
          className={styles.markAllButton}
          disabled={currentUnread === 0}
          onClick={() => void markAllAsRead()}
          type="button"
        >
          <Icon icon="mdi:check-all" size={18} />
          {activeCategory ? t('notification.markCategoryRead') : t('notification.markAllRead')}
        </button>
      </header>

      <div className={styles.filters} aria-label={t('notification.categoryFilterLabel')}>
        <button
          aria-pressed={activeCategory === null}
          className={activeCategory === null ? styles.filterActive : styles.filter}
          onClick={() => changeFilters(null, onlyUnread)}
          type="button"
        >
          {t('notification.filter.all')}
          <span>{parseNotificationCount(summary?.voUnreadGroupCount)}</span>
        </button>
        {notificationCategoryDefinitions.map((definition) => (
          <button
            aria-pressed={activeCategory === definition.category}
            className={activeCategory === definition.category ? styles.filterActive : styles.filter}
            key={definition.category}
            onClick={() => changeFilters(definition.category, onlyUnread)}
            type="button"
          >
            {t(definition.labelKey)}
            <span>{getUnreadCategoryCount(summary, definition.category)}</span>
          </button>
        ))}
      </div>

      <div className={styles.controls}>
        <button
          aria-pressed={onlyUnread}
          className={onlyUnread ? styles.unreadToggleActive : styles.unreadToggle}
          onClick={() => changeFilters(activeCategory, !onlyUnread)}
          type="button"
        >
          <Icon icon="mdi:circle-medium" size={18} />
          {t('notification.filter.onlyUnread')}
        </button>
        <span aria-live="polite">{t('notification.resultCount', { count: groups.length })}</span>
      </div>

      {isOffline && (
        <div className={styles.notice} role="status">
          <Icon icon="mdi:cloud-off-outline" size={19} />
          <span>{t('notification.offlineHint')}</span>
          <button onClick={() => void reconcile()} type="button">
            {t('notification.retry')}
          </button>
        </div>
      )}

      {hasNewerRevision && (
        <button className={styles.newerPrompt} onClick={() => void refresh(false)} type="button">
          <Icon icon="mdi:arrow-up-circle-outline" size={20} />
          {t('notification.newerPrompt')}
        </button>
      )}

      <div className={styles.list} aria-busy={loadState === 'loading'}>
        {loadState === 'loading' && groups.length === 0 && (
          <div className={styles.state} role="status">
            <Icon icon="mdi:loading" size={28} className={styles.spin} />
            <strong>{t('notification.shared.loading')}</strong>
            <span>{t('notification.loadingHint')}</span>
          </div>
        )}

        {loadState === 'error' && groups.length === 0 && (
          <div className={styles.state} role="alert">
            <Icon icon="mdi:alert-circle-outline" size={30} />
            <strong>{t('notification.loadFailed')}</strong>
            <span>{t('notification.loadFailedHint')}</span>
            <button onClick={() => void refresh(true)} type="button">{t('notification.retry')}</button>
          </div>
        )}

        {loadState === 'ready' && groups.length === 0 && (
          <div className={styles.state}>
            <Icon icon="mdi:bell-sleep-outline" size={34} />
            <strong>{t('notification.shared.emptyTitle')}</strong>
            <span>{onlyUnread ? t('notification.emptyUnreadHint') : t('notification.shared.emptyHint')}</span>
          </div>
        )}

        {groups.map((group) => {
          const category = getNotificationCategoryDefinition(group.voCategory);
          const target = resolveWebNotificationNavigation(group.voTarget);
          const occurrenceCount = parseNotificationCount(group.voOccurrenceCount);
          const distinctTriggerCount = parseNotificationCount(group.voDistinctTriggerCount);
          const triggerAvatar = resolveMediaUrl(group.voTriggerAvatar, apiBaseUrl);
          return (
            <article className={`${styles.group} ${group.voIsRead ? '' : styles.groupUnread}`} key={group.voGroupId}>
              <div className={styles.groupIcon} aria-hidden="true">
                <Icon icon={category.icon} size={21} />
              </div>
              <div className={styles.groupBody}>
                <div className={styles.groupMeta}>
                  <span>{t(category.labelKey)}</span>
                  <time dateTime={group.voLastOccurredAtUtc}>
                    {formatLocalizedRelativeTime(group.voLastOccurredAtUtc, locale)}
                  </time>
                  {!group.voIsRead && <em>{t('notification.unreadDot')}</em>}
                </div>
                <h3>{group.voTitle}</h3>
                <p>{group.voContent || t('notification.web.emptyContent')}</p>
                <div className={styles.aggregation}>
                  {triggerAvatar ? <img alt="" src={triggerAvatar} /> : <Icon icon="mdi:account-circle-outline" size={24} />}
                  <span>
                    {group.voTriggerName || t('common.userFallback', { id: group.voTriggerId ?? '—' })}
                  </span>
                  {occurrenceCount > 1 && (
                    <strong>{t('notification.aggregationMeta', { count: occurrenceCount, triggers: distinctTriggerCount })}</strong>
                  )}
                </div>
                {!target && (
                  <div className={styles.unavailable} role="status">
                    <Icon icon="mdi:link-off" size={17} />
                    {t(getNotificationTargetUnavailableKey(group))}
                  </div>
                )}
              </div>
              <div className={styles.actions}>
                {target && (
                  <button className={styles.primaryAction} onClick={() => openTarget(group)} type="button">
                    {t('notification.openTarget')}
                  </button>
                )}
                {!group.voIsRead && (
                  <button onClick={() => void markGroupAsRead(group)} type="button">
                    {t('notification.shared.markAsRead')}
                  </button>
                )}
                <button onClick={() => void deleteGroup(group)} type="button">
                  {t('notification.shared.delete')}
                </button>
              </div>
            </article>
          );
        })}
      </div>

      {groups.length > 0 && (
        <footer className={styles.footer}>
          {nextCursor ? (
            <button disabled={loadingMore} onClick={() => void loadMore()} type="button">
              {loadingMore ? t('notification.shared.loadingMore') : t('notification.shared.loadMore')}
            </button>
          ) : (
            <span>{t('notification.shared.loadedAll')}</span>
          )}
        </footer>
      )}
    </section>
  );
};
