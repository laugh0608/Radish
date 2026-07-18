import type { NotificationTargetVo } from '@radish/http';
import { buildMessagesPath } from '../messages/messagesRouteState.ts';
import { buildPublicDocsPath } from '../public/docsRouteState.ts';
import { buildPublicForumPath } from '../public/forumRouteState.ts';
import { buildPublicProfilePath } from '../public/profileRouteState.ts';
import type { PublicRouteSourceState } from '../public/publicRouteNavigation.ts';
import {
  buildShopInventoryReturnPath,
  buildShopOrderReturnPath,
} from '../services/authReturnPath.ts';

export interface NotificationWebNavigationTarget {
  href: string;
  sourceState?: PublicRouteSourceState;
  surface: 'web' | 'console';
}

const notificationsSourceRoute = {
  app: 'notifications',
  route: { kind: 'index' },
} as const;

function normalizePositiveIntegerString(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? '';
  return /^[1-9]\d*$/u.test(normalized) ? normalized : null;
}

function normalizeOpaqueIdentifier(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? '';
  return normalized.length > 0 ? normalized : null;
}

function buildGovernanceTarget(caseId: string): string {
  const search = new URLSearchParams({
    sourceReportId: caseId,
    backTo: '/notifications',
  });
  return `/console/moderation?${search.toString()}`;
}

export function resolveWebNotificationNavigation(
  target: NotificationTargetVo | null | undefined,
): NotificationWebNavigationTarget | null {
  if (!target || target.voKind === 'None') {
    return null;
  }

  switch (target.voKind) {
    case 'ForumPost': {
      const postPublicId = normalizeOpaqueIdentifier(target.voPostPublicId);
      const postId = normalizePositiveIntegerString(target.voPostId);
      const routeId = postPublicId ?? postId;
      if (!routeId) {
        return null;
      }

      const commentId = normalizePositiveIntegerString(target.voCommentId);
      return {
        surface: 'web',
        href: buildPublicForumPath({
          kind: 'detail',
          postId: routeId,
          ...(postPublicId ? { postPublicId } : {}),
          ...(commentId ? { commentId } : {}),
        }),
        sourceState: {
          forumDetailSourceRoute: notificationsSourceRoute,
        },
      };
    }
    case 'ChatConversation': {
      const channelId = normalizePositiveIntegerString(target.voChannelId);
      if (!channelId) {
        return null;
      }

      const messageId = normalizePositiveIntegerString(target.voMessageId);
      return {
        surface: 'web',
        href: buildMessagesPath({ channelId, ...(messageId ? { messageId } : {}) }),
      };
    }
    case 'UserProfile': {
      const userId = normalizeOpaqueIdentifier(target.voUserPublicId)
        ?? normalizePositiveIntegerString(target.voUserId);
      if (!userId) {
        return null;
      }

      return {
        surface: 'web',
        href: buildPublicProfilePath({ kind: 'detail', userId, tab: 'posts', page: 1 }),
        sourceState: {
          profileSourceRoute: notificationsSourceRoute,
        },
      };
    }
    case 'ShopOrder': {
      const orderId = normalizePositiveIntegerString(target.voOrderId);
      const href = orderId ? buildShopOrderReturnPath(orderId) : null;
      return href ? { surface: 'web', href } : null;
    }
    case 'Inventory':
      return { surface: 'web', href: buildShopInventoryReturnPath() };
    case 'Experience':
      return { surface: 'web', href: '/me/experience' };
    case 'DocsDocument': {
      const slug = normalizeOpaqueIdentifier(target.voDocumentSlug);
      return slug
        ? { surface: 'web', href: buildPublicDocsPath({ kind: 'detail', slug }) }
        : null;
    }
    case 'GovernanceCase': {
      const caseId = normalizePositiveIntegerString(target.voGovernanceCaseId);
      return caseId
        ? { surface: 'console', href: buildGovernanceTarget(caseId) }
        : null;
    }
  }

  return null;
}
