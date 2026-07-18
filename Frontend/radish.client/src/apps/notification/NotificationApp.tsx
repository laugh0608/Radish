import { useCallback } from 'react';
import type { NotificationInboxGroupVo } from '@radish/http';
import { resolveConsoleExternalUrl } from '@/desktop/externalAppUrl';
import { useWindowStore } from '@/stores/windowStore';
import { buildChatAppParams } from '@/utils/chatNavigation';
import { buildForumAppParams } from '@/utils/forumNavigation';
import type { NotificationWebNavigationTarget } from '@/utils/notificationNavigation';
import { NotificationCenter } from './NotificationCenter';

export const NotificationApp = () => {
  const openOrReuseApp = useWindowStore((state) => state.openOrReuseApp);

  const handleNavigateTarget = useCallback((
    group: NotificationInboxGroupVo,
    target: NotificationWebNavigationTarget,
  ) => {
    switch (group.voTarget.voKind) {
      case 'ForumPost': {
        const params = buildForumAppParams({
          postId: group.voTarget.voPostId ?? undefined,
          postPublicId: group.voTarget.voPostPublicId ?? undefined,
          commentId: group.voTarget.voCommentId ?? undefined,
        });
        if (Object.keys(params).length > 0) {
          openOrReuseApp('forum', params);
          return true;
        }
        break;
      }
      case 'ChatConversation': {
        const channelId = group.voTarget.voChannelId?.trim();
        if (channelId) {
          const params = buildChatAppParams({
            channelId,
            messageId: group.voTarget.voMessageId ?? undefined,
          });
          if (Object.keys(params).length > 0) {
            openOrReuseApp('chat', params);
            return true;
          }
        }
        break;
      }
      case 'ShopOrder': {
        const orderId = group.voTarget.voOrderId?.trim();
        if (orderId) {
          openOrReuseApp('shop', { orderId });
          return true;
        }
        break;
      }
      case 'Inventory':
        openOrReuseApp('shop', { initialView: 'inventory' });
        return true;
      default:
        break;
    }

    window.location.href = target.surface === 'console'
      ? resolveConsoleExternalUrl(target.href)
      : target.href;
    return true;
  }, [openOrReuseApp]);

  return <NotificationCenter onNavigateTarget={handleNavigateTarget} />;
};
