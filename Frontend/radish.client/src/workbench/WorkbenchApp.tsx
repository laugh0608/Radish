import { useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getChannelList } from '@/api/chat';
import { notificationApi } from '@/api/notification';
import { readDraftMap, type ChannelDraft } from '@/apps/chat/chatApp.helpers';
import { getApiBaseUrl } from '@/config/env';
import { buildMessagesPath } from '@/messages/messagesRouteState';
import { resolveConsoleExternalUrl } from '@/desktop/externalAppUrl';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { useAuthStore } from '@/stores/authStore';
import { useChatStore } from '@/stores/chatStore';
import { useNotificationStore, type NotificationItem } from '@/stores/notificationStore';
import { useUserStore } from '@/stores/userStore';
import type { ChannelVo } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import {
  getNotificationActionScope,
  resolveNotificationPreview,
  toNotificationStoreItem,
} from '@/notifications/notificationActionQueue';
import styles from './WorkbenchApp.module.css';

type WorkbenchAccess = 'public' | 'private' | 'admin' | 'legacy';

interface WorkbenchLink {
  labelKey: string;
  href: string;
}

interface WorkbenchItem {
  titleKey: string;
  descriptionKey: string;
  icon: string;
  access: WorkbenchAccess;
  href: string;
  links: WorkbenchLink[];
  crossApp?: boolean;
}

interface WorkbenchGroup {
  titleKey: string;
  descriptionKey: string;
  items: WorkbenchItem[];
}

interface WorkbenchQueueItem {
  id: string;
  title: string;
  description: string;
  href: string;
  icon: string;
  meta: string;
  tone?: 'attention' | 'neutral';
}

interface WorkbenchRailItem {
  label: string;
  value: string;
}

interface WorkbenchActivityState {
  loading: boolean;
  notificationError: boolean;
  messageError: boolean;
  chatDraftCount: number;
  hasForumDraft: boolean;
}

type Translate = (key: string, options?: Record<string, unknown>) => string;

const FORUM_POST_DRAFT_STORAGE_KEY = 'forum_post_draft';
const consoleWorkbenchUrl = resolveConsoleExternalUrl('/workbench');

const workbenchGroups: WorkbenchGroup[] = [
  {
    titleKey: 'workbench.group.public.title',
    descriptionKey: 'workbench.group.public.description',
    items: [
      {
        titleKey: 'workbench.item.discover.title',
        descriptionKey: 'workbench.item.discover.description',
        icon: 'mdi:compass-outline',
        access: 'public',
        href: '/discover',
        links: [
          { labelKey: 'workbench.link.open', href: '/discover' },
        ],
      },
      {
        titleKey: 'workbench.item.forum.title',
        descriptionKey: 'workbench.item.forum.description',
        icon: 'mdi:forum-outline',
        access: 'public',
        href: '/forum',
        links: [
          { labelKey: 'workbench.link.read', href: '/forum' },
          { labelKey: 'workbench.link.compose', href: '/forum/compose' },
        ],
      },
      {
        titleKey: 'workbench.item.docs.title',
        descriptionKey: 'workbench.item.docs.description',
        icon: 'mdi:file-document-outline',
        access: 'public',
        href: '/docs',
        links: [
          { labelKey: 'workbench.link.read', href: '/docs' },
          { labelKey: 'workbench.link.mine', href: '/docs/mine' },
          { labelKey: 'workbench.link.compose', href: '/docs/compose' },
        ],
      },
      {
        titleKey: 'workbench.item.legal.title',
        descriptionKey: 'workbench.item.legal.description',
        icon: 'mdi:shield-check-outline',
        access: 'public',
        href: '/legal',
        links: [
          { labelKey: 'workbench.link.readCommitments', href: '/legal' },
        ],
      },
      {
        titleKey: 'workbench.item.publicShop.title',
        descriptionKey: 'workbench.item.publicShop.description',
        icon: 'mdi:storefront-outline',
        access: 'public',
        href: '/shop',
        links: [
          { labelKey: 'workbench.link.browse', href: '/shop' },
          { labelKey: 'workbench.link.leaderboard', href: '/leaderboard' },
        ],
      },
      {
        titleKey: 'workbench.item.leaderboard.title',
        descriptionKey: 'workbench.item.leaderboard.description',
        icon: 'mdi:trophy-outline',
        access: 'public',
        href: '/leaderboard',
        links: [
          { labelKey: 'workbench.link.open', href: '/leaderboard' },
        ],
      },
    ],
  },
  {
    titleKey: 'workbench.group.private.title',
    descriptionKey: 'workbench.group.private.description',
    items: [
      {
        titleKey: 'workbench.item.circle.title',
        descriptionKey: 'workbench.item.circle.description',
        icon: 'mdi:account-group-outline',
        access: 'private',
        href: '/circle',
        links: [
          { labelKey: 'workbench.link.open', href: '/circle' },
        ],
      },
      {
        titleKey: 'workbench.item.me.title',
        descriptionKey: 'workbench.item.me.description',
        icon: 'mdi:account-circle-outline',
        access: 'private',
        href: '/me',
        links: [
          { labelKey: 'workbench.link.overview', href: '/me' },
          { labelKey: 'workbench.link.content', href: '/me/content' },
          { labelKey: 'workbench.link.history', href: '/me/history' },
          { labelKey: 'workbench.link.assets', href: '/me/assets' },
          { labelKey: 'workbench.link.attachments', href: '/me/attachments' },
          { labelKey: 'workbench.link.experience', href: '/me/experience' },
        ],
      },
      {
        titleKey: 'workbench.item.shop.title',
        descriptionKey: 'workbench.item.shop.description',
        icon: 'mdi:shopping-outline',
        access: 'private',
        href: '/shop',
        links: [
          { labelKey: 'workbench.link.browse', href: '/shop' },
          { labelKey: 'workbench.link.orders', href: '/shop/orders' },
          { labelKey: 'workbench.link.inventory', href: '/shop/inventory' },
        ],
      },
      {
        titleKey: 'workbench.item.messages.title',
        descriptionKey: 'workbench.item.messages.description',
        icon: 'mdi:message-text-outline',
        access: 'private',
        href: '/messages',
        links: [
          { labelKey: 'workbench.link.messages', href: '/messages' },
          { labelKey: 'workbench.link.notifications', href: '/notifications' },
        ],
      },
      {
        titleKey: 'workbench.item.pet.title',
        descriptionKey: 'workbench.item.pet.description',
        icon: 'mdi:sprout-outline',
        access: 'private',
        href: '/pet',
        links: [
          { labelKey: 'workbench.link.open', href: '/pet' },
        ],
      },
    ],
  },
  {
    titleKey: 'workbench.group.governance.title',
    descriptionKey: 'workbench.group.governance.description',
    items: [
      {
        titleKey: 'workbench.item.console.title',
        descriptionKey: 'workbench.item.console.description',
        icon: 'mdi:shield-crown-outline',
        access: 'admin',
        href: consoleWorkbenchUrl,
        crossApp: true,
        links: [
          { labelKey: 'workbench.link.open', href: consoleWorkbenchUrl },
        ],
      },
      {
        titleKey: 'workbench.item.desktop.title',
        descriptionKey: 'workbench.item.desktop.description',
        icon: 'mdi:view-dashboard-outline',
        access: 'legacy',
        href: '/desktop',
        links: [
          { labelKey: 'workbench.link.openDesktop', href: '/desktop' },
        ],
      },
    ],
  },
];

const fallbackQueueTemplates = [
  {
    id: 'notifications',
    titleKey: 'workbench.continue.notifications.title',
    descriptionKey: 'workbench.continue.notifications.description',
    href: '/notifications',
    icon: 'mdi:bell-outline',
    metaKey: 'workbench.continue.notifications.meta',
  },
  {
    id: 'orders',
    titleKey: 'workbench.continue.orders.title',
    descriptionKey: 'workbench.continue.orders.description',
    href: '/shop/orders',
    icon: 'mdi:receipt-text-outline',
    metaKey: 'workbench.continue.orders.meta',
  },
  {
    id: 'author',
    titleKey: 'workbench.continue.author.title',
    descriptionKey: 'workbench.continue.author.description',
    href: '/docs/mine',
    icon: 'mdi:file-document-edit-outline',
    metaKey: 'workbench.continue.author.meta',
  },
  {
    id: 'pet',
    titleKey: 'workbench.continue.pet.title',
    descriptionKey: 'workbench.continue.pet.description',
    href: '/pet',
    icon: 'mdi:sprout-outline',
    metaKey: 'workbench.continue.pet.meta',
  },
] as const;

const accessClassNameMap: Record<WorkbenchAccess, string> = {
  public: styles.accessPublic,
  private: styles.accessPrivate,
  admin: styles.accessAdmin,
  legacy: styles.accessLegacy,
};

function hasMeaningfulChatDraft(draft: ChannelDraft | null | undefined): boolean {
  if (!draft) {
    return false;
  }

  return draft.content.trim().length > 0 || Boolean(draft.replyTarget || draft.pendingImage);
}

function countChatDrafts(userId: string): number {
  if (!userId.trim()) {
    return 0;
  }

  const prefix = `${userId.trim()}:`;
  return Object.entries(readDraftMap()).filter(([key, draft]) => (
    key.startsWith(prefix) && hasMeaningfulChatDraft(draft)
  )).length;
}

function hasMeaningfulForumDraft(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }

  const raw = window.localStorage.getItem(FORUM_POST_DRAFT_STORAGE_KEY);
  if (!raw || raw.trim() === '{}') {
    return false;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== 'object') {
      return raw.trim().length > 0;
    }

    return Object.values(parsed).some((value) => {
      if (typeof value === 'string') {
        return value.trim().length > 0;
      }

      return Array.isArray(value) && value.length > 0;
    });
  } catch {
    return raw.trim().length > 0;
  }
}

function buildFallbackQueue(t: Translate): WorkbenchQueueItem[] {
  return fallbackQueueTemplates.map((item) => ({
    id: item.id,
    title: t(item.titleKey),
    description: t(item.descriptionKey),
    href: item.href,
    icon: item.icon,
    meta: t(item.metaKey),
    tone: 'neutral',
  }));
}

function firstReadableChannelHref(channels: ChannelVo[]): string {
  const targetChannel = channels.find((channel) => channel.voHasMention)
    ?? channels.find((channel) => channel.voUnreadCount > 0)
    ?? channels[0];

  const channelId = normalizeEntityId(targetChannel?.voId);
  return channelId ? buildMessagesPath({ channelId }) : '/messages';
}

function appendUniqueQueueItem(items: WorkbenchQueueItem[], item: WorkbenchQueueItem): void {
  if (items.some((existing) => existing.id === item.id)) {
    return;
  }

  items.push(item);
}

export const WorkbenchApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const unreadCount = useNotificationStore(state => state.unreadCount);
  const recentNotifications = useNotificationStore(state => state.recentNotifications);
  const channels = useChatStore(state => state.channels);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [pendingCrossAppHref, setPendingCrossAppHref] = useState<string | null>(null);
  const [activityState, setActivityState] = useState<WorkbenchActivityState>({
    loading: false,
    notificationError: false,
    messageError: false,
    chatDraftCount: 0,
    hasForumDraft: false,
  });
  const handleCrossAppNavigate = (event: MouseEvent<HTMLAnchorElement>, href: string) => {
    if (
      event.button !== 0
      || event.metaKey
      || event.ctrlKey
      || event.shiftKey
      || event.altKey
    ) {
      return;
    }

    event.preventDefault();
    setPendingCrossAppHref(href);
    window.requestAnimationFrame(() => {
      window.location.assign(href);
    });
  };
  const notificationPreviews = useMemo(() => (
    recentNotifications.map(resolveNotificationPreview)
  ), [recentNotifications]);
  const unreadNotificationCount = Math.max(
    unreadCount,
    notificationPreviews.filter((item) => !item.isRead).length,
  );
  const hasActivitySyncIssue = activityState.notificationError || activityState.messageError;
  const activitySyncIssueHref = activityState.notificationError ? '/notifications' : '/messages';
  const notificationScopeCounts = useMemo(() => (
    notificationPreviews.reduce<Record<string, number>>((counts, item) => {
      const scope = getNotificationActionScope(item, item.target);
      counts[scope] = (counts[scope] ?? 0) + 1;
      return counts;
    }, {})
  ), [notificationPreviews]);
  const messageUnreadTotal = useMemo(() => (
    channels.reduce((total, channel) => total + Math.max(0, channel.voUnreadCount), 0)
  ), [channels]);
  const mentionChannelCount = useMemo(() => (
    channels.filter((channel) => channel.voHasMention).length
  ), [channels]);
  const queueItems = useMemo(() => {
    const items: WorkbenchQueueItem[] = [];
    const fallbackItems = buildFallbackQueue(t);
    const latestRoutedNotification = notificationPreviews.find((item) => item.target !== null);
    const orderCount = notificationScopeCounts.orders ?? 0;
    const petCount = notificationScopeCounts.pet ?? 0;
    const experienceCount = notificationScopeCounts.experience ?? 0;
    const followCount = notificationScopeCounts.follow ?? 0;
    const governanceCount = notificationScopeCounts.governance ?? 0;

    if (!authReady) {
      appendUniqueQueueItem(items, {
        id: 'activity-loading',
        title: t('workbench.continue.loading.title'),
        description: t('workbench.continue.loading.description'),
        href: '/notifications',
        icon: 'mdi:progress-clock',
        meta: t('workbench.continue.loading.meta'),
        tone: 'neutral',
      });
    }

    if (loggedIn && hasActivitySyncIssue) {
      appendUniqueQueueItem(items, {
        id: 'activity-sync-issue',
        title: t('workbench.continue.syncIssue.title'),
        description: t('workbench.continue.syncIssue.description'),
        href: activitySyncIssueHref,
        icon: 'mdi:cloud-alert-outline',
        meta: t('workbench.continue.syncIssue.meta'),
        tone: 'attention',
      });
    }

    if (loggedIn && unreadNotificationCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'notifications',
        title: t('workbench.continue.notifications.title'),
        description: t('workbench.continue.notifications.descriptionActive'),
        href: latestRoutedNotification?.target?.href ?? '/notifications',
        icon: 'mdi:bell-badge-outline',
        meta: t('workbench.continue.notifications.metaUnread', { count: unreadNotificationCount }),
        tone: 'attention',
      });
    }

    if (loggedIn && (messageUnreadTotal > 0 || mentionChannelCount > 0 || activityState.chatDraftCount > 0)) {
      appendUniqueQueueItem(items, {
        id: 'messages',
        title: t('workbench.continue.messages.title'),
        description: mentionChannelCount > 0
          ? t('workbench.continue.messages.descriptionMention')
          : t('workbench.continue.messages.description'),
        href: firstReadableChannelHref(channels),
        icon: 'mdi:message-text-outline',
        meta: mentionChannelCount > 0
          ? t('workbench.continue.messages.metaMention', { count: mentionChannelCount })
          : t('workbench.continue.messages.metaUnread', { count: messageUnreadTotal + activityState.chatDraftCount }),
        tone: 'attention',
      });
    }

    if (activityState.hasForumDraft) {
      appendUniqueQueueItem(items, {
        id: 'forum-draft',
        title: t('workbench.continue.draft.title'),
        description: t('workbench.continue.draft.description'),
        href: '/forum/compose',
        icon: 'mdi:pencil-outline',
        meta: t('workbench.continue.draft.meta'),
        tone: 'attention',
      });
    }

    if (loggedIn && orderCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'orders',
        title: t('workbench.continue.orders.title'),
        description: t('workbench.continue.orders.descriptionActive'),
        href: '/shop/orders',
        icon: 'mdi:receipt-text-outline',
        meta: t('workbench.continue.orders.metaCount', { count: orderCount }),
        tone: 'attention',
      });
    }

    if (loggedIn && followCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'circle',
        title: t('workbench.continue.circle.title'),
        description: t('workbench.continue.circle.description'),
        href: '/circle',
        icon: 'mdi:account-group-outline',
        meta: t('workbench.continue.circle.meta', { count: followCount }),
        tone: 'attention',
      });
    }

    if (loggedIn && petCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'pet',
        title: t('workbench.continue.pet.title'),
        description: t('workbench.continue.pet.descriptionActive'),
        href: '/pet',
        icon: 'mdi:sprout-outline',
        meta: t('workbench.continue.pet.metaCount', { count: petCount }),
        tone: 'attention',
      });
    }

    if (loggedIn && experienceCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'experience',
        title: t('workbench.continue.experience.title'),
        description: t('workbench.continue.experience.description'),
        href: '/me/experience',
        icon: 'mdi:chart-timeline-variant-shimmer',
        meta: t('workbench.continue.experience.meta', { count: experienceCount }),
        tone: 'attention',
      });
    }

    if (loggedIn && governanceCount > 0) {
      appendUniqueQueueItem(items, {
        id: 'governance',
        title: t('workbench.continue.governance.title'),
        description: t('workbench.continue.governance.description'),
        href: '/notifications',
        icon: 'mdi:shield-check-outline',
        meta: t('workbench.continue.governance.meta', { count: governanceCount }),
        tone: 'attention',
      });
    }

    for (const item of fallbackItems) {
      appendUniqueQueueItem(items, item);
      if (items.length >= 6) {
        break;
      }
    }

    return items.slice(0, 6);
  }, [
    activityState.chatDraftCount,
    activityState.hasForumDraft,
    activitySyncIssueHref,
    authReady,
    channels,
    hasActivitySyncIssue,
    loggedIn,
    mentionChannelCount,
    messageUnreadTotal,
    notificationPreviews,
    notificationScopeCounts,
    t,
    unreadNotificationCount,
  ]);
  const railItems = useMemo<WorkbenchRailItem[]>(() => {
    const syncState = activityState.loading
      ? t('workbench.rail.sync.loading')
      : activityState.notificationError || activityState.messageError
        ? t('workbench.rail.sync.partial')
        : t(loggedIn ? 'workbench.rail.sync.ready' : 'workbench.rail.sync.public');

    return [
      {
        label: t('workbench.rail.activity.label'),
        value: loggedIn
          ? t('workbench.rail.activity.value', { count: unreadNotificationCount })
          : t('workbench.rail.activity.publicValue'),
      },
      {
        label: t('workbench.rail.messages.label'),
        value: loggedIn
          ? t('workbench.rail.messages.value', { unread: messageUnreadTotal, mentions: mentionChannelCount })
          : t('workbench.rail.messages.publicValue'),
      },
      {
        label: t('workbench.rail.drafts.label'),
        value: t('workbench.rail.drafts.value', {
          count: activityState.chatDraftCount + (activityState.hasForumDraft ? 1 : 0),
        }),
      },
      {
        label: t('workbench.rail.sync.label'),
        value: syncState,
      },
    ];
  }, [
    activityState.chatDraftCount,
    activityState.hasForumDraft,
    activityState.loading,
    activityState.messageError,
    activityState.notificationError,
    loggedIn,
    mentionChannelCount,
    messageUnreadTotal,
    t,
    unreadNotificationCount,
  ]);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('WorkbenchApp', '工作台登录态初始化失败', error);
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
    document.title = `${t('workbench.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    const refreshLocalDrafts = () => {
      setActivityState((state) => ({
        ...state,
        chatDraftCount: countChatDrafts(userId),
        hasForumDraft: hasMeaningfulForumDraft(),
      }));
    };

    refreshLocalDrafts();
    window.addEventListener('focus', refreshLocalDrafts);
    window.addEventListener('storage', refreshLocalDrafts);
    return () => {
      window.removeEventListener('focus', refreshLocalDrafts);
      window.removeEventListener('storage', refreshLocalDrafts);
    };
  }, [userId]);

  useEffect(() => {
    if (!authReady || !loggedIn) {
      setActivityState((state) => ({
        ...state,
        loading: false,
        notificationError: false,
        messageError: false,
      }));
      return;
    }

    let cancelled = false;
    setActivityState((state) => ({
      ...state,
      loading: true,
      notificationError: false,
      messageError: false,
      chatDraftCount: countChatDrafts(userId),
      hasForumDraft: hasMeaningfulForumDraft(),
    }));

    Promise.allSettled([
      notificationApi.getUnreadCount(),
      notificationApi.getMyNotifications({ pageIndex: 1, pageSize: 12 }),
      getChannelList(),
    ]).then((results) => {
      if (cancelled) {
        return;
      }

      const [unreadResult, notificationResult, channelResult] = results;
      const nextState: Partial<WorkbenchActivityState> = {
        loading: false,
        notificationError: false,
        messageError: false,
        chatDraftCount: countChatDrafts(userId),
        hasForumDraft: hasMeaningfulForumDraft(),
      };

      if (unreadResult.status === 'fulfilled') {
        useNotificationStore.getState().setUnreadCount(unreadResult.value);
      } else {
        nextState.notificationError = true;
        log.warn('WorkbenchApp', '加载工作台未读通知数量失败', unreadResult.reason);
      }

      if (notificationResult.status === 'fulfilled' && notificationResult.value) {
        const notifications = notificationResult.value.data
          .map(toNotificationStoreItem)
          .filter((item): item is NotificationItem => item !== null);
        useNotificationStore.getState().setRecentNotifications(notifications);
      } else {
        nextState.notificationError = true;
        if (notificationResult.status === 'rejected') {
          log.warn('WorkbenchApp', '加载工作台近期通知失败', notificationResult.reason);
        }
      }

      if (channelResult.status === 'fulfilled') {
        useChatStore.getState().setChannels(channelResult.value);
      } else {
        nextState.messageError = true;
        log.warn('WorkbenchApp', '加载工作台频道列表失败', channelResult.reason);
      }

      setActivityState((state) => ({
        ...state,
        ...nextState,
      }));
    });

    return () => {
      cancelled = true;
    };
  }, [authReady, loggedIn, userId]);

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="more"
        brandMark="多"
        brandName={t('workbench.title')}
        brandSubline={t('workbench.shellSubline')}
        onBrandClick={() => {
          window.location.href = '/workbench';
        }}
      />

      <main className={styles.main}>
        <section className={styles.summary}>
          <div className={styles.summaryText}>
            <p className={styles.kicker}>{t('workbench.kicker')}</p>
            <h1>{t('workbench.heading')}</h1>
            <p>{t('workbench.description')}</p>
          </div>
          <div className={styles.summaryActions} aria-label={t('workbench.quickActionsLabel')}>
            <a className={styles.primaryAction} href="/me">
              <Icon icon="mdi:account-circle-outline" size={18} />
              <span>{t('workbench.quick.me')}</span>
            </a>
            <a className={styles.secondaryAction} href="/messages">
              <Icon icon="mdi:message-text-outline" size={18} />
              <span>{t('workbench.quick.messages')}</span>
            </a>
          </div>
        </section>

        <section className={styles.focusArea} aria-labelledby="workbench-focus-title">
          <div className={styles.queuePanel}>
            <div className={styles.panelHeader}>
              <p className={styles.panelKicker}>{t('workbench.continue.kicker')}</p>
              <h2 id="workbench-focus-title">{t('workbench.continue.title')}</h2>
              <p>{t('workbench.continue.description')}</p>
            </div>
            <div className={styles.queueList}>
              {queueItems.map((item) => (
                <a
                  className={`${styles.queueItem} ${item.tone === 'attention' ? styles.queueItemAttention : ''}`}
                  href={item.href}
                  key={item.id}
                >
                  <span className={styles.queueIcon}>
                    <Icon icon={item.icon} size={20} />
                  </span>
                  <span className={styles.queueCopy}>
                    <span className={styles.queueTitle}>{item.title}</span>
                    <span className={styles.queueDescription}>{item.description}</span>
                  </span>
                  <span className={`${styles.queueMeta} ${item.tone === 'attention' ? styles.queueMetaAttention : ''}`}>
                    {item.meta}
                  </span>
                </a>
              ))}
            </div>
          </div>

          <aside className={styles.railPanel} aria-label={t('workbench.rail.label')}>
            <div className={styles.railHeader}>
              <span className={styles.railIcon}>
                <Icon icon="mdi:map-marker-path" size={20} />
              </span>
              <div>
                <p className={styles.panelKicker}>{t('workbench.rail.kicker')}</p>
                <h2>{t('workbench.rail.title')}</h2>
              </div>
            </div>
            <div className={styles.railRows}>
              {railItems.map((item) => (
                <div className={styles.railRow} key={item.label}>
                  <span>{item.label}</span>
                  <strong>{item.value}</strong>
                </div>
              ))}
            </div>
            <div className={styles.railActions}>
              <a className={styles.railAction} href="/me/assets">
                <Icon icon="mdi:wallet-outline" size={18} />
                <span>{t('workbench.rail.assets')}</span>
              </a>
              <a className={styles.railAction} href="/docs/mine">
                <Icon icon="mdi:file-document-edit-outline" size={18} />
                <span>{t('workbench.rail.author')}</span>
              </a>
            </div>
          </aside>
        </section>

        <div className={styles.groups}>
          {workbenchGroups.map((group) => (
            <section className={styles.group} key={group.titleKey}>
              <div className={styles.groupHeader}>
                <h2>{t(group.titleKey)}</h2>
                <p>{t(group.descriptionKey)}</p>
              </div>
              <div className={styles.grid}>
                {group.items.map((item) => (
                  <article
                    className={`${styles.item} ${pendingCrossAppHref === item.href ? styles.itemCrossAppPending : ''}`}
                    key={item.titleKey}
                    aria-busy={pendingCrossAppHref === item.href}
                  >
                    <a
                      className={styles.itemMainLink}
                      href={item.href}
                      aria-label={t(item.titleKey)}
                      onClick={item.crossApp
                        ? (event) => handleCrossAppNavigate(event, item.href)
                        : undefined}
                    >
                      <span className={styles.itemIcon}>
                        <Icon icon={item.icon} size={22} />
                      </span>
                      <span className={styles.itemText}>
                        <span className={styles.itemTitleRow}>
                          <span className={styles.itemTitle}>{t(item.titleKey)}</span>
                          <span className={`${styles.accessBadge} ${accessClassNameMap[item.access]}`}>
                            {t(`workbench.access.${item.access}`)}
                          </span>
                        </span>
                        <span className={styles.itemDescription}>{t(item.descriptionKey)}</span>
                      </span>
                    </a>
                    <div className={styles.links} aria-label={t('workbench.linksLabel', { name: t(item.titleKey) })}>
                      {item.links.map((link) => (
                        <a
                          className={styles.linkChip}
                          href={link.href}
                          key={`${item.titleKey}:${link.href}`}
                          onClick={item.crossApp
                            ? (event) => handleCrossAppNavigate(event, link.href)
                            : undefined}
                        >
                          {pendingCrossAppHref === link.href
                            ? t('workbench.crossApp.pending')
                            : t(link.labelKey)}
                        </a>
                      ))}
                    </div>
                  </article>
                ))}
              </div>
            </section>
          ))}
        </div>
      </main>
    </div>
  );
};
