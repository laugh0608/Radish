import { create } from 'zustand';
import type {
  NotificationCategory,
  NotificationInboxChangedVo,
  NotificationInboxPageVo,
  NotificationInboxSummaryVo,
  NotificationPreferenceVo,
} from '@radish/http';
import {
  canApplyNotificationInboxPage,
  canApplyNotificationSummary,
  compareNotificationRevisions,
  getUnreadGroupCount,
  getUnreadOccurrenceCount,
  mergeNotificationGroups,
  parseNotificationCount,
} from '@/notifications/notificationInbox';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
export type NotificationLoadState = 'idle' | 'loading' | 'ready' | 'error';

interface NotificationStore {
  summary: NotificationInboxSummaryVo | null;
  groups: NotificationInboxPageVo['voItems'];
  nextCursor: string | null;
  listRevision: string | null;
  preferences: NotificationPreferenceVo[];
  activeCategory: NotificationCategory | null;
  onlyUnread: boolean;
  unreadCount: number;
  unreadOccurrenceCount: number;
  connectionState: ConnectionState;
  loadState: NotificationLoadState;
  loadingMore: boolean;
  preferencesLoading: boolean;
  preferencesSaving: boolean;
  errorMessage: string | null;
  pendingRevision: string | null;
  hasNewerRevision: boolean;
  applySummary: (summary: NotificationInboxSummaryVo) => boolean;
  applyInboxPage: (page: NotificationInboxPageVo, append: boolean) => boolean;
  noteInboxChanged: (change: NotificationInboxChangedVo) => boolean;
  setPreferences: (preferences: NotificationPreferenceVo[]) => void;
  setFilters: (category: NotificationCategory | null, onlyUnread: boolean) => void;
  setConnectionState: (state: ConnectionState) => void;
  setLoadState: (state: NotificationLoadState, errorMessage?: string | null) => void;
  setLoadingMore: (loading: boolean) => void;
  setPreferencesLoading: (loading: boolean) => void;
  setPreferencesSaving: (saving: boolean) => void;
  reset: () => void;
}

const initialState = {
  summary: null,
  groups: [],
  nextCursor: null,
  listRevision: null,
  preferences: [],
  activeCategory: null,
  onlyUnread: false,
  unreadCount: 0,
  unreadOccurrenceCount: 0,
  connectionState: 'disconnected' as ConnectionState,
  loadState: 'idle' as NotificationLoadState,
  loadingMore: false,
  preferencesLoading: false,
  preferencesSaving: false,
  errorMessage: null,
  pendingRevision: null,
  hasNewerRevision: false,
};

export const useNotificationStore = create<NotificationStore>((set, get) => ({
  ...initialState,

  applySummary: (summary) => {
    const current = get();
    if (!canApplyNotificationSummary(summary.voRevision, current.summary?.voRevision)) {
      return false;
    }

    const listIsBehind = current.listRevision !== null
      && compareNotificationRevisions(summary.voRevision, current.listRevision) > 0;
    const pendingResolved = current.pendingRevision === null
      || compareNotificationRevisions(summary.voRevision, current.pendingRevision) >= 0;

    set({
      summary,
      unreadCount: getUnreadGroupCount(summary),
      unreadOccurrenceCount: getUnreadOccurrenceCount(summary),
      pendingRevision: pendingResolved ? null : current.pendingRevision,
      hasNewerRevision: current.hasNewerRevision || listIsBehind,
    });
    return true;
  },

  applyInboxPage: (page, append) => {
    const current = get();
    if (!canApplyNotificationInboxPage(
      page.voSummary.voRevision,
      current.summary?.voRevision,
      current.listRevision,
      append,
    )) {
      return false;
    }

    set({
      summary: page.voSummary,
      groups: append ? mergeNotificationGroups(current.groups, page.voItems) : page.voItems,
      nextCursor: page.voNextCursor,
      listRevision: page.voSummary.voRevision,
      unreadCount: getUnreadGroupCount(page.voSummary),
      unreadOccurrenceCount: getUnreadOccurrenceCount(page.voSummary),
      pendingRevision: null,
      hasNewerRevision: false,
      loadState: 'ready',
      loadingMore: false,
      errorMessage: null,
    });
    return true;
  },

  noteInboxChanged: (change) => {
    const current = get();
    const knownRevision = current.pendingRevision ?? current.summary?.voRevision ?? '0';
    if (compareNotificationRevisions(change.voRevision, knownRevision) <= 0) {
      return false;
    }

    set({
      pendingRevision: change.voRevision,
      hasNewerRevision: current.listRevision !== null,
      unreadCount: parseNotificationCount(change.voUnreadGroupCount),
      unreadOccurrenceCount: parseNotificationCount(change.voUnreadOccurrenceCount),
    });
    return true;
  },

  setPreferences: (preferences) => set({ preferences }),
  setFilters: (activeCategory, onlyUnread) => set({ activeCategory, onlyUnread }),
  setConnectionState: (connectionState) => set({ connectionState }),
  setLoadState: (loadState, errorMessage = null) => set({ loadState, errorMessage }),
  setLoadingMore: (loadingMore) => set({ loadingMore }),
  setPreferencesLoading: (preferencesLoading) => set({ preferencesLoading }),
  setPreferencesSaving: (preferencesSaving) => set({ preferencesSaving }),
  reset: () => set({ ...initialState }),
}));
