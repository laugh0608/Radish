import { buildPublicForumPath } from '../public/forumRouteState.ts';
import { buildPublicProfilePath } from '../public/profileRouteState.ts';
import type { PublicRouteSourceState } from '../public/publicRouteNavigation.ts';
import { buildMessagesPath } from '../messages/messagesRouteState.ts';
import { buildShopOrderReturnPath, buildShopOrdersReturnPath } from '../services/authReturnPath.ts';
import { parseChatNotificationNavigation } from './chatNavigation.ts';
import { parseForumNotificationNavigation } from './forumNavigation.ts';

type NotificationLongLike = string | number;

export interface NotificationNavigationSource {
  type?: string | null;
  businessType?: string | null;
  businessId?: NotificationLongLike | null;
  triggerId?: NotificationLongLike | null;
  extData?: string | null;
}

export interface NotificationWebNavigationTarget {
  href: string;
  sourceState?: PublicRouteSourceState;
  surface: 'web' | 'desktop';
}

const notificationsSourceRoute = {
  app: 'notifications',
  route: { kind: 'index' }
} as const;

function normalizePositiveIntegerString(value: unknown): string | null {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? String(value) : null;
  }

  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  return /^[1-9]\d*$/.test(normalized) ? normalized : null;
}

export function resolveWebNotificationNavigation(
  notification: NotificationNavigationSource
): NotificationWebNavigationTarget | null {
  const chatNavigation = parseChatNotificationNavigation(notification.extData);
  if (chatNavigation) {
    return {
      surface: 'web',
      href: buildMessagesPath(chatNavigation)
    };
  }

  const forumNavigation = parseForumNotificationNavigation(notification.extData);
  if (forumNavigation) {
    return {
      surface: 'web',
      href: buildPublicForumPath({
        kind: 'detail',
        postId: forumNavigation.postId ?? forumNavigation.postPublicId!,
        ...(forumNavigation.postPublicId ? { postPublicId: forumNavigation.postPublicId } : {}),
        ...(forumNavigation.commentId ? { commentId: forumNavigation.commentId } : {})
      }),
      sourceState: {
        forumDetailSourceRoute: notificationsSourceRoute
      }
    };
  }

  const businessType = notification.businessType?.trim();
  if (businessType === 'Post') {
    const postId = normalizePositiveIntegerString(notification.businessId);
    return postId
      ? {
          surface: 'web',
          href: buildPublicForumPath({ kind: 'detail', postId }),
          sourceState: {
            forumDetailSourceRoute: notificationsSourceRoute
          }
        }
      : null;
  }

  if (businessType === 'User' || notification.type === 'follow') {
    const userId = normalizePositiveIntegerString(
      notification.type === 'follow'
        ? (notification.triggerId ?? notification.businessId)
        : (notification.businessId ?? notification.triggerId)
    );

    return userId
      ? {
          surface: 'web',
          href: buildPublicProfilePath({
            kind: 'detail',
            userId,
            tab: 'posts',
            page: 1
          }),
          sourceState: {
            profileSourceRoute: notificationsSourceRoute
          }
        }
      : null;
  }

  if (businessType === 'Order') {
    const orderId = normalizePositiveIntegerString(notification.businessId);
    return {
      surface: 'web',
      href: orderId ? buildShopOrderReturnPath(orderId) ?? buildShopOrdersReturnPath() : buildShopOrdersReturnPath()
    };
  }

  if (businessType === 'Comment') {
    return {
      surface: 'web',
      href: '/forum'
    };
  }

  if (
    notification.type === 'reply'
    || notification.type === 'mention'
    || notification.type === 'like'
  ) {
    return {
      surface: 'web',
      href: '/forum'
    };
  }

  return null;
}
