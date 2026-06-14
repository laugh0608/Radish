import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotificationItemData } from '@radish/ui/notification';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { buildChatAppParams, parseChatNotificationNavigation } from '@/utils/chatNavigation';
import { buildForumAppParams, parseForumNotificationNavigation } from '@/utils/forumNavigation';
import { isSameLongId, normalizePositiveLongIdKey } from '@/utils/longId';
import { NotificationCenter } from './NotificationCenter';

export const NotificationApp = () => {
  const { t } = useTranslation();
  const { openApp, openOrReuseApp } = useWindowStore();
  const currentUserId = useUserStore((state) => state.userId);

  const handleNavigateNotification = useCallback((notification: NotificationItemData) => {
    const businessType = notification.businessType?.trim();
    const chatNavigation = parseChatNotificationNavigation(notification.extData);
    const forumNavigation = parseForumNotificationNavigation(notification.extData);

    if (chatNavigation) {
      openOrReuseApp('chat', buildChatAppParams(chatNavigation));
      return true;
    }

    if (forumNavigation) {
      openOrReuseApp('forum', buildForumAppParams(forumNavigation));
      return true;
    }

    if (businessType === 'Post' && notification.businessId != null) {
      const forumParams = buildForumAppParams({ postId: notification.businessId });
      if ('postId' in forumParams) {
        openOrReuseApp('forum', forumParams);
        return true;
      }
    }

    if (businessType === 'Comment') {
      openOrReuseApp('forum');
      return true;
    }

    if (businessType === 'User' || notification.type === 'follow') {
      const targetUserId = normalizePositiveLongIdKey(
        notification.type === 'follow'
          ? (notification.triggerId ?? notification.businessId)
          : (notification.businessId ?? notification.triggerId)
      );

      if (targetUserId) {
        if (isSameLongId(targetUserId, currentUserId)) {
          openApp('profile');
        } else {
          openApp('profile', {
            userId: targetUserId,
            userName: notification.triggerName?.trim() || t('common.userFallback', { id: targetUserId }),
            avatarUrl: notification.triggerAvatar ?? null,
          });
        }
        return true;
      }
    }

    if (businessType === 'Order') {
      const targetOrderId = normalizePositiveLongIdKey(notification.businessId);
      if (targetOrderId) {
        openOrReuseApp('shop', { orderId: targetOrderId });
      } else {
        openApp('shop');
      }
      return true;
    }

    if (notification.type === 'reply' || notification.type === 'mention' || notification.type === 'like') {
      openOrReuseApp('forum');
      return true;
    }

    return false;
  }, [currentUserId, openApp, openOrReuseApp, t]);

  return <NotificationCenter onNavigateNotification={handleNavigateNotification} />;
};
