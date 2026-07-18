import type {
  NotificationCategory,
  NotificationInboxChangedVo,
  NotificationInboxSummaryVo,
  UpdateNotificationPreferenceDto,
} from '@radish/http';
import { notificationApi } from '@/api/notification';
import {
  compareNotificationRevisions,
  isNotificationCursorExpiredError,
} from '@/notifications/notificationInbox';
import { useNotificationStore } from '@/stores/notificationStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';

const BROADCAST_CHANNEL_NAME = 'radish-notification-inbox';
const STORAGE_EVENT_KEY = 'radish_notification_inbox_revision';

interface NotificationRevisionBroadcast {
  userId: string;
  revision: string;
  sentAt: number;
}

let summaryRequest: Promise<NotificationInboxSummaryVo> | null = null;
let summaryRequestUserId: string | null = null;
let firstPageRequest: Promise<void> | null = null;
let firstPageRequestKey: string | null = null;
let broadcastChannel: BroadcastChannel | null = null;
let listenersReady = false;

function currentUserId(): string {
  return useUserStore.getState().userId.trim();
}

function isRevisionBroadcast(value: unknown): value is NotificationRevisionBroadcast {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<NotificationRevisionBroadcast>;
  return typeof candidate.userId === 'string'
    && typeof candidate.revision === 'string'
    && typeof candidate.sentAt === 'number';
}

function handleRevisionBroadcast(message: NotificationRevisionBroadcast): void {
  const userId = currentUserId();
  if (!userId || message.userId !== userId) {
    return;
  }

  const store = useNotificationStore.getState();
  const knownRevision = store.pendingRevision ?? store.summary?.voRevision ?? '0';
  if (compareNotificationRevisions(message.revision, knownRevision) <= 0) {
    return;
  }

  void notificationInboxSync.reconcile({ refreshListWhenChanged: true }).catch((error) => {
    log.warn('NotificationInboxSync', '跨标签通知状态对账失败', error);
  });
}

function ensureListeners(): void {
  if (listenersReady || typeof window === 'undefined') {
    return;
  }

  listenersReady = true;
  if (typeof BroadcastChannel !== 'undefined') {
    broadcastChannel = new BroadcastChannel(BROADCAST_CHANNEL_NAME);
    broadcastChannel.addEventListener('message', (event: MessageEvent<unknown>) => {
      if (isRevisionBroadcast(event.data)) {
        handleRevisionBroadcast(event.data);
      }
    });
  } else {
    window.addEventListener('storage', (event) => {
      if (event.key !== STORAGE_EVENT_KEY || !event.newValue) {
        return;
      }

      try {
        const message: unknown = JSON.parse(event.newValue);
        if (isRevisionBroadcast(message)) {
          handleRevisionBroadcast(message);
        }
      } catch (error) {
        log.warn('NotificationInboxSync', '忽略无效的跨标签通知 revision', error);
      }
    });
  }

  const reconcileAfterResume = () => {
    if (document.visibilityState === 'hidden' || !currentUserId()) {
      return;
    }

    void notificationInboxSync.reconcile({ refreshListWhenChanged: true }).catch((error) => {
      log.warn('NotificationInboxSync', '恢复前台后的通知对账失败', error);
    });
  };
  window.addEventListener('focus', reconcileAfterResume);
  window.addEventListener('online', reconcileAfterResume);
  document.addEventListener('visibilitychange', reconcileAfterResume);
}

function publishRevision(summary: NotificationInboxSummaryVo, userId: string): void {
  if (!userId || currentUserId() !== userId || typeof window === 'undefined') {
    return;
  }

  ensureListeners();
  const message: NotificationRevisionBroadcast = {
    userId,
    revision: summary.voRevision,
    sentAt: Date.now(),
  };
  if (broadcastChannel) {
    broadcastChannel.postMessage(message);
    return;
  }

  try {
    window.localStorage.setItem(STORAGE_EVENT_KEY, JSON.stringify(message));
    window.localStorage.removeItem(STORAGE_EVENT_KEY);
  } catch (error) {
    log.warn('NotificationInboxSync', '无法广播通知 revision', error);
  }
}

async function refreshCurrentFirstPage(showLoading: boolean): Promise<void> {
  ensureListeners();
  const initialStore = useNotificationStore.getState();
  const requestUserId = currentUserId();
  const category = initialStore.activeCategory ?? undefined;
  const onlyUnread = initialStore.onlyUnread;
  const requestKey = `${requestUserId}:${category ?? 'All'}:${onlyUnread ? 'unread' : 'all'}`;
  if (firstPageRequest) {
    if (firstPageRequestKey === requestKey) {
      return firstPageRequest;
    }

    try {
      await firstPageRequest;
    } catch {
      // 新筛选会发起自己的权威请求，旧请求错误已由旧请求写入状态。
    }
    return refreshCurrentFirstPage(showLoading);
  }

  if (showLoading) {
    initialStore.setLoadState('loading');
  }

  firstPageRequestKey = requestKey;
  firstPageRequest = (async () => {
    try {
      const page = await notificationApi.getInbox({ category, onlyUnread, pageSize: 20 });
      const current = useNotificationStore.getState();
      if (
        currentUserId() === requestUserId
        && (current.activeCategory ?? undefined) === category
        && current.onlyUnread === onlyUnread
      ) {
        current.applyInboxPage(page, false);
      }
    } catch (error) {
      const current = useNotificationStore.getState();
      if (
        currentUserId() === requestUserId
        && (current.activeCategory ?? undefined) === category
        && current.onlyUnread === onlyUnread
      ) {
        current.setLoadState('error', error instanceof Error ? error.message : String(error));
      }
      throw error;
    } finally {
      firstPageRequest = null;
      firstPageRequestKey = null;
    }
  })();

  return firstPageRequest;
}

export const notificationInboxSync = {
  async refreshSummary(): Promise<NotificationInboxSummaryVo> {
    ensureListeners();
    const requestUserId = currentUserId();
    if (summaryRequest) {
      if (summaryRequestUserId === requestUserId) {
        return summaryRequest;
      }

      try {
        await summaryRequest;
      } catch {
        // 新账号会发起自己的摘要请求，旧账号错误由旧调用方处理。
      }
      return notificationInboxSync.refreshSummary();
    }

    summaryRequestUserId = requestUserId;
    summaryRequest = (async () => {
      try {
        const summary = await notificationApi.getInboxSummary();
        if (currentUserId() === requestUserId) {
          useNotificationStore.getState().applySummary(summary);
        }
        return summary;
      } finally {
        summaryRequest = null;
        summaryRequestUserId = null;
      }
    })();

    return summaryRequest;
  },

  refreshFirstPage(options: { showLoading?: boolean } = {}): Promise<void> {
    return refreshCurrentFirstPage(options.showLoading ?? true);
  },

  async reconcile(options: { refreshListWhenChanged?: boolean } = {}): Promise<void> {
    const listRevision = useNotificationStore.getState().listRevision;
    const summary = await notificationInboxSync.refreshSummary();
    if (
      options.refreshListWhenChanged
      && listRevision !== null
      && compareNotificationRevisions(summary.voRevision, listRevision) > 0
    ) {
      await refreshCurrentFirstPage(false);
    }
  },

  async loadMore(): Promise<'loaded' | 'cursor-expired' | 'complete'> {
    const store = useNotificationStore.getState();
    if (!store.nextCursor || store.loadingMore) {
      return 'complete';
    }

    const requestUserId = currentUserId();
    const requestCategory = store.activeCategory;
    const requestOnlyUnread = store.onlyUnread;
    store.setLoadingMore(true);
    try {
      const page = await notificationApi.getInbox({
        category: requestCategory ?? undefined,
        onlyUnread: requestOnlyUnread,
        cursor: store.nextCursor,
        pageSize: 20,
      });
      const current = useNotificationStore.getState();
      if (
        currentUserId() !== requestUserId
        || current.activeCategory !== requestCategory
        || current.onlyUnread !== requestOnlyUnread
      ) {
        current.setLoadingMore(false);
        return 'complete';
      }
      if (!current.applyInboxPage(page, true)) {
        await refreshCurrentFirstPage(false);
        return 'cursor-expired';
      }
      return 'loaded';
    } catch (error) {
      if (isNotificationCursorExpiredError(error)) {
        await refreshCurrentFirstPage(false);
        return 'cursor-expired';
      }

      const current = useNotificationStore.getState();
      if (
        currentUserId() === requestUserId
        && current.activeCategory === requestCategory
        && current.onlyUnread === requestOnlyUnread
      ) {
        current.setLoadingMore(false);
      }
      throw error;
    }
  },

  async loadPreferences(): Promise<void> {
    const store = useNotificationStore.getState();
    const requestUserId = currentUserId();
    store.setPreferencesLoading(true);
    try {
      const preferences = await notificationApi.getPreferences();
      if (currentUserId() === requestUserId) {
        useNotificationStore.getState().setPreferences(preferences);
      }
    } finally {
      if (currentUserId() === requestUserId) {
        useNotificationStore.getState().setPreferencesLoading(false);
      }
    }
  },

  async updatePreferences(preferences: UpdateNotificationPreferenceDto[]): Promise<void> {
    const store = useNotificationStore.getState();
    const requestUserId = currentUserId();
    store.setPreferencesSaving(true);
    try {
      const updated = await notificationApi.updatePreferences(preferences);
      if (currentUserId() !== requestUserId) {
        return;
      }
      useNotificationStore.getState().setPreferences(updated);
      const summary = await notificationInboxSync.refreshSummary();
      publishRevision(summary, requestUserId);
    } finally {
      if (currentUserId() === requestUserId) {
        useNotificationStore.getState().setPreferencesSaving(false);
      }
    }
  },

  async markGroupsAsRead(groupIds: string[]): Promise<void> {
    const requestUserId = currentUserId();
    const mutation = await notificationApi.markInboxGroupsAsRead(groupIds);
    if (currentUserId() !== requestUserId) {
      return;
    }
    useNotificationStore.getState().applySummary(mutation.voSummary);
    publishRevision(mutation.voSummary, requestUserId);
    await refreshCurrentFirstPage(false);
  },

  async markAllAsRead(category?: NotificationCategory): Promise<void> {
    const requestUserId = currentUserId();
    const mutation = await notificationApi.markAllInboxAsRead(category);
    if (currentUserId() !== requestUserId) {
      return;
    }
    useNotificationStore.getState().applySummary(mutation.voSummary);
    publishRevision(mutation.voSummary, requestUserId);
    await refreshCurrentFirstPage(false);
  },

  async deleteGroup(groupId: string): Promise<void> {
    const requestUserId = currentUserId();
    const mutation = await notificationApi.deleteInboxGroup(groupId);
    if (currentUserId() !== requestUserId) {
      return;
    }
    useNotificationStore.getState().applySummary(mutation.voSummary);
    publishRevision(mutation.voSummary, requestUserId);
    await refreshCurrentFirstPage(false);
  },

  handleInboxChanged(change: NotificationInboxChangedVo): void {
    ensureListeners();
    if (!currentUserId()) {
      return;
    }
    if (!useNotificationStore.getState().noteInboxChanged(change)) {
      return;
    }

    const userId = currentUserId();
    if (userId && typeof window !== 'undefined') {
      const message: NotificationRevisionBroadcast = {
        userId,
        revision: change.voRevision,
        sentAt: Date.now(),
      };
      broadcastChannel?.postMessage(message);
    }

    void notificationInboxSync.reconcile({
      refreshListWhenChanged: change.voReason !== 'Created',
    }).catch((error) => {
      log.warn('NotificationInboxSync', '收到通知变化后权威状态对账失败', error);
    });
  },
};
