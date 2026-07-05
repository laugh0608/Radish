import type { NotificationItemData } from '@radish/ui/notification';
import type { UserNotificationVo } from '../api/notification.ts';
import type { NotificationItem, NotificationType } from '../stores/notificationStore.ts';
import {
  resolveWebNotificationNavigation,
  type NotificationWebNavigationTarget,
} from '../utils/notificationNavigation.ts';

export const NOTIFICATION_QUEUE_GROUP_ITEM_LIMIT = 3;

export type NotificationActionScope =
  | 'all'
  | 'posts'
  | 'comments'
  | 'answers'
  | 'messages'
  | 'orders'
  | 'docs'
  | 'follow'
  | 'pet'
  | 'experience'
  | 'governance'
  | 'likes'
  | 'system';

export type NotificationPreview = NotificationItemData & {
  target: NotificationWebNavigationTarget | null;
};

export interface NotificationScopeDefinition {
  key: NotificationActionScope;
  labelKey: string;
  icon: string;
}

export interface NotificationActionGroup {
  scope: NotificationActionScope;
  definition: NotificationScopeDefinition;
  items: NotificationPreview[];
  totalCount: number;
  unreadCount: number;
  routedCount: number;
  manualCount: number;
}

export interface BuildNotificationActionGroupsOptions {
  itemLimitPerGroup?: number;
  scopeOrder?: NotificationActionScope[];
}

const scopeDefinitions: Record<NotificationActionScope, NotificationScopeDefinition> = {
  all: {
    key: 'all',
    labelKey: 'notification.web.scope.all',
    icon: 'mdi:bell-outline',
  },
  posts: {
    key: 'posts',
    labelKey: 'notification.web.scope.posts',
    icon: 'mdi:post-outline',
  },
  comments: {
    key: 'comments',
    labelKey: 'notification.web.scope.comments',
    icon: 'mdi:comment-text-outline',
  },
  answers: {
    key: 'answers',
    labelKey: 'notification.web.scope.answers',
    icon: 'mdi:comment-question-outline',
  },
  messages: {
    key: 'messages',
    labelKey: 'notification.web.scope.messages',
    icon: 'mdi:message-text-outline',
  },
  orders: {
    key: 'orders',
    labelKey: 'notification.web.scope.orders',
    icon: 'mdi:receipt-text-outline',
  },
  docs: {
    key: 'docs',
    labelKey: 'notification.web.scope.docs',
    icon: 'mdi:file-document-outline',
  },
  follow: {
    key: 'follow',
    labelKey: 'notification.web.scope.follow',
    icon: 'mdi:account-plus-outline',
  },
  pet: {
    key: 'pet',
    labelKey: 'notification.web.scope.pet',
    icon: 'mdi:sprout-outline',
  },
  experience: {
    key: 'experience',
    labelKey: 'notification.web.scope.experience',
    icon: 'mdi:chart-timeline-variant-shimmer',
  },
  governance: {
    key: 'governance',
    labelKey: 'notification.web.scope.governance',
    icon: 'mdi:shield-check-outline',
  },
  likes: {
    key: 'likes',
    labelKey: 'notification.web.scope.likes',
    icon: 'mdi:heart-outline',
  },
  system: {
    key: 'system',
    labelKey: 'notification.web.scope.system',
    icon: 'mdi:bell-outline',
  },
};

const defaultQueueScopeOrder: NotificationActionScope[] = [
  'comments',
  'answers',
  'messages',
  'follow',
  'governance',
  'orders',
  'docs',
  'posts',
  'pet',
  'experience',
  'likes',
  'system',
];

const keywordMap: Record<Exclude<NotificationActionScope, 'all' | 'comments' | 'likes' | 'system'>, string[]> = {
  posts: ['post', 'thread', 'forum', '帖子', '发帖', '主题'],
  answers: ['answer', 'question', 'accepted', '问答', '回答', '采纳', '最佳答案'],
  messages: ['message', 'chat', 'channel', '私信', '消息', '聊天', '频道', '会话'],
  orders: ['order', 'purchase', 'inventory', 'benefit', '订单', '交易', '购买', '背包', '权益'],
  docs: ['doc', 'wiki', 'document', '文档', '知识库', '修订'],
  follow: ['follow', 'follower', '关注', '粉丝', '关系'],
  pet: ['pet', 'care', '宠物', '照护', '喂食', '清洁'],
  experience: ['experience', 'level', 'growth', '经验', '等级', '成长'],
  governance: ['moderation', 'audit', 'report', 'appeal', 'ban', '治理', '审核', '举报', '申诉', '封禁'],
};

const notificationTypeMap: Record<string, NotificationType> = {
  System: 'system',
  CommentReply: 'reply',
  CommentReplied: 'reply',
  PostQuickReplied: 'reply',
  Mention: 'mention',
  Mentioned: 'mention',
  PostLiked: 'like',
  CommentLiked: 'like',
  Follow: 'follow',
  Followed: 'follow',
  LotteryWon: 'lottery',
};

function normalizeText(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? '';
}

function notificationHasKeyword(notification: NotificationItemData, keywords: string[]): boolean {
  const fields = [
    normalizeText(notification.type),
    normalizeText(notification.businessType),
    normalizeText(notification.title),
    normalizeText(notification.content),
    normalizeText(notification.extData),
  ];

  return keywords.some((keyword) => {
    const normalizedKeyword = keyword.toLowerCase();
    return fields.some((field) => field.includes(normalizedKeyword));
  });
}

function targetStartsWith(target: NotificationWebNavigationTarget | null, prefix: string): boolean {
  return target?.href.startsWith(prefix) === true;
}

function normalizedBusinessType(notification: NotificationItemData): string {
  return normalizeText(notification.businessType);
}

export function toNotificationItemData(notification: NotificationItem): NotificationItemData {
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
    createdAt: notification.createdAt,
  };
}

export function toNotificationStoreItem(userNotification: UserNotificationVo): NotificationItem | null {
  const notification = userNotification.voNotification;
  if (!notification) {
    return null;
  }

  return {
    id: String(userNotification.voId),
    notificationId: String(userNotification.voNotificationId),
    type: notificationTypeMap[notification.voType] ?? 'system',
    title: notification.voTitle,
    content: notification.voContent,
    businessType: notification.voBusinessType,
    businessId: notification.voBusinessId == null ? null : String(notification.voBusinessId),
    triggerId: notification.voTriggerId == null ? null : String(notification.voTriggerId),
    triggerName: notification.voTriggerName,
    triggerAvatar: notification.voTriggerAvatar,
    extData: notification.voExtData,
    isRead: userNotification.voIsRead,
    createdAt: notification.voCreateTime || userNotification.voCreateTime,
  };
}

export function resolveNotificationPreview(notification: NotificationItem): NotificationPreview {
  const item = toNotificationItemData(notification);
  return {
    ...item,
    target: resolveWebNotificationNavigation(item),
  };
}

export function matchesOrderNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  return targetStartsWith(target, '/shop/order')
    || targetStartsWith(target, '/shop/orders')
    || notificationHasKeyword(notification, keywordMap.orders);
}

export function matchesDocsNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  return targetStartsWith(target, '/docs')
    || notificationHasKeyword(notification, keywordMap.docs);
}

export function matchesMessageNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  return targetStartsWith(target, '/messages')
    || notificationHasKeyword(notification, keywordMap.messages);
}

export function matchesAnswerNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  const businessType = normalizedBusinessType(notification);

  return businessType.includes('answer')
    || businessType.includes('question')
    || (targetStartsWith(target, '/forum') && notificationHasKeyword(notification, keywordMap.answers))
    || notificationHasKeyword(notification, keywordMap.answers);
}

export function matchesPostNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  const businessType = normalizedBusinessType(notification);

  return businessType === 'post'
    || businessType.includes('forum')
    || targetStartsWith(target, '/forum/post')
    || notificationHasKeyword(notification, keywordMap.posts);
}

export function matchesCommentNotification(notification: NotificationItemData): boolean {
  const businessType = normalizedBusinessType(notification);

  return notification.type === 'reply'
    || notification.type === 'mention'
    || businessType === 'comment'
    || businessType.includes('comment');
}

export function matchesFollowNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null = null,
): boolean {
  return notification.type === 'follow'
    || targetStartsWith(target, '/u/')
    || notificationHasKeyword(notification, keywordMap.follow);
}

export function matchesPetNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  return targetStartsWith(target, '/pet')
    || notificationHasKeyword(notification, keywordMap.pet);
}

export function matchesExperienceNotification(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): boolean {
  return targetStartsWith(target, '/me/experience')
    || notificationHasKeyword(notification, keywordMap.experience);
}

export function matchesGovernanceNotification(notification: NotificationItemData): boolean {
  return notificationHasKeyword(notification, keywordMap.governance);
}

export function getNotificationActionScope(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): NotificationActionScope {
  if (matchesMessageNotification(notification, target)) {
    return 'messages';
  }

  if (matchesOrderNotification(notification, target)) {
    return 'orders';
  }

  if (matchesDocsNotification(notification, target)) {
    return 'docs';
  }

  if (matchesPetNotification(notification, target)) {
    return 'pet';
  }

  if (matchesExperienceNotification(notification, target)) {
    return 'experience';
  }

  if (matchesGovernanceNotification(notification)) {
    return 'governance';
  }

  if (matchesAnswerNotification(notification, target)) {
    return 'answers';
  }

  if (matchesCommentNotification(notification)) {
    return 'comments';
  }

  if (matchesFollowNotification(notification, target)) {
    return 'follow';
  }

  if (notification.type === 'like') {
    return 'likes';
  }

  if (matchesPostNotification(notification, target)) {
    return 'posts';
  }

  return 'system';
}

export function getNotificationScopeDefinition(scope: NotificationActionScope): NotificationScopeDefinition {
  return scopeDefinitions[scope];
}

export function getNotificationKindLabelKey(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): string {
  return getNotificationScopeDefinition(getNotificationActionScope(notification, target)).labelKey;
}

export function getNotificationKindIcon(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): string {
  return getNotificationScopeDefinition(getNotificationActionScope(notification, target)).icon;
}

export function getTargetLabel(target: NotificationWebNavigationTarget | null): string {
  return target?.href ?? '';
}

export function getNotificationTargetHintKey(
  notification: NotificationItemData,
  target: NotificationWebNavigationTarget | null,
): string {
  if (target) {
    return 'notification.web.openTarget';
  }

  const scope = getNotificationActionScope(notification, target);
  if (scope === 'governance') {
    return 'notification.web.targetMissing.governance';
  }

  if (scope === 'system') {
    return 'notification.web.targetMissing.system';
  }

  const businessId = normalizeText(notification.businessId);
  if (businessId === '0' || businessId === '') {
    return 'notification.web.targetMissing.invalid';
  }

  return 'notification.web.targetMissing.default';
}

export function buildNotificationActionGroups(
  notifications: NotificationPreview[],
  options: BuildNotificationActionGroupsOptions = {},
): NotificationActionGroup[] {
  const itemLimitPerGroup = options.itemLimitPerGroup ?? NOTIFICATION_QUEUE_GROUP_ITEM_LIMIT;
  const scopeOrder = options.scopeOrder ?? defaultQueueScopeOrder;
  const groups = new Map<NotificationActionScope, NotificationPreview[]>();

  for (const notification of notifications) {
    const scope = getNotificationActionScope(notification, notification.target);
    const currentGroup = groups.get(scope) ?? [];
    currentGroup.push(notification);
    groups.set(scope, currentGroup);
  }

  return scopeOrder
    .map((scope) => {
      const items = groups.get(scope) ?? [];
      if (items.length === 0) {
        return null;
      }

      return {
        scope,
        definition: getNotificationScopeDefinition(scope),
        items: items.slice(0, itemLimitPerGroup),
        totalCount: items.length,
        unreadCount: items.filter((item) => !item.isRead).length,
        routedCount: items.filter((item) => item.target).length,
        manualCount: items.filter((item) => !item.target).length,
      };
    })
    .filter((group): group is NotificationActionGroup => group !== null);
}
