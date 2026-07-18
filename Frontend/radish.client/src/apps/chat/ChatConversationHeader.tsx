import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  acceptDirectConversation,
  blockDirectConversation,
  declineDirectConversation,
  setDirectConversationArchived,
  unblockDirectConversation,
} from '@/api/chat';
import type { ChannelVo, DirectConversationAction, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { getErrorMessage } from './chatApp.helpers';
import { isDirectConversationChannel, resolveConversationNoticeKey } from './chatConversationPresentation';
import styles from './ChatApp.module.css';

interface ChatConversationHeaderProps {
  activeChannel: ChannelVo | null;
  showBackToList: boolean;
  onBackToList?: () => void;
  connectionHint: string | null;
  routeUnavailable: boolean;
  onOpenUserProfile: (target: {
    userId: EntityIdValue;
    publicId?: string | null;
    userName?: string | null;
    avatarUrl?: string | null;
  }) => void;
  onConversationChanged: (action: DirectConversationAction) => Promise<void>;
}

export function ChatConversationHeader({
  activeChannel,
  showBackToList,
  onBackToList,
  connectionHint,
  routeUnavailable,
  onOpenUserProfile,
  onConversationChanged,
}: ChatConversationHeaderProps) {
  const { t } = useTranslation();
  const [pendingAction, setPendingAction] = useState<DirectConversationAction | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const isDirect = isDirectConversationChannel(activeChannel);
  const noticeKey = resolveConversationNoticeKey(activeChannel);
  const peerUserId = normalizeEntityId(activeChannel?.voPeerUserId);
  const channelName = activeChannel?.voPeerDisplayName?.trim() || activeChannel?.voName;

  const performAction = async (action: DirectConversationAction) => {
    if (!activeChannel || pendingAction) {
      return;
    }

    setPendingAction(action);
    setActionError(null);
    try {
      switch (action) {
        case 'accept':
          await acceptDirectConversation(activeChannel.voId);
          break;
        case 'decline':
          await declineDirectConversation(activeChannel.voId);
          break;
        case 'block':
          await blockDirectConversation(activeChannel.voId);
          break;
        case 'unblock':
          await unblockDirectConversation(activeChannel.voId);
          break;
        case 'archive':
          await setDirectConversationArchived(activeChannel.voId, true);
          break;
        case 'unarchive':
          await setDirectConversationArchived(activeChannel.voId, false);
          break;
      }

      toast.success(t(`chat.action.${action}.success`));
      await onConversationChanged(action);
    } catch (error) {
      const message = getErrorMessage(error, t(`chat.action.${action}.failed`));
      setActionError(message);
      toast.error(message);
    } finally {
      setPendingAction(null);
    }
  };

  const openPeerProfile = () => {
    if (!activeChannel || !peerUserId) {
      return;
    }

    onOpenUserProfile({
      userId: peerUserId,
      publicId: activeChannel.voPeerPublicId,
      userName: activeChannel.voPeerDisplayName,
      avatarUrl: activeChannel.voPeerAvatarUrl,
    });
  };

  return (
    <header className={styles.mainHeader}>
      <div className={styles.headerMain}>
        <div className={styles.headerIdentity}>
          {showBackToList && (onBackToList ? (
            <button type="button" className={styles.mobileBackLink} onClick={onBackToList}>
              <Icon icon="mdi:chevron-left" size={18} />
              <span>{t('chat.backToConversations')}</span>
            </button>
          ) : (
            <a className={styles.mobileBackLink} href="/messages">
              <Icon icon="mdi:chevron-left" size={18} />
              <span>{t('chat.backToConversations')}</span>
            </a>
          ))}
          <div>
            <div className={styles.channelTitle}>
              {activeChannel ? `${activeChannel.voIconEmoji || '#'} ${channelName}` : t('chat.selectChannel')}
            </div>
            {activeChannel?.voDescription && (
              <div className={styles.channelDescription}>{activeChannel.voDescription}</div>
            )}
          </div>
        </div>

        {isDirect && activeChannel && (
          <div className={styles.conversationHeaderActions}>
            {peerUserId && activeChannel.voIsPeerAvailable && (
              <button type="button" className={styles.conversationProfileButton} onClick={openPeerProfile}>
                <Icon icon="mdi:account-outline" size={17} />
                <span>{t('chat.openPeerProfile')}</span>
              </button>
            )}
            <details className={styles.conversationMenu}>
              <summary aria-label={t('chat.conversationMenu.open')}>
                <Icon icon="mdi:dots-horizontal" size={20} />
              </summary>
              <div className={styles.conversationMenuPanel}>
                <button
                  type="button"
                  disabled={pendingAction !== null}
                  onClick={() => void performAction(activeChannel.voIsArchived ? 'unarchive' : 'archive')}
                >
                  <Icon icon={activeChannel.voIsArchived ? 'mdi:archive-arrow-up-outline' : 'mdi:archive-outline'} size={17} />
                  <span>{t(activeChannel.voIsArchived ? 'chat.action.unarchive' : 'chat.action.archive')}</span>
                </button>
                {activeChannel.voCanBlock && (
                  <button type="button" disabled={pendingAction !== null} onClick={() => void performAction('block')}>
                    <Icon icon="mdi:shield-off-outline" size={17} />
                    <span>{t('chat.action.block')}</span>
                  </button>
                )}
              </div>
            </details>
          </div>
        )}
      </div>

      {routeUnavailable && (
        <div className={styles.conversationNotice} data-tone="danger" role="alert">
          <strong>{t('chat.routeUnavailableTitle')}</strong>
          <span>{t('chat.routeUnavailableDescription')}</span>
        </div>
      )}

      {activeChannel?.voCanAccept || activeChannel?.voCanDecline ? (
        <div className={styles.conversationNotice} data-tone="request" role="status">
          <div>
            <strong>{t('chat.request.title')}</strong>
            <span>{t('chat.request.description')}</span>
          </div>
          <div className={styles.conversationNoticeActions}>
            {activeChannel.voCanDecline && (
              <button type="button" disabled={pendingAction !== null} onClick={() => void performAction('decline')}>
                {t(pendingAction === 'decline' ? 'chat.action.processing' : 'chat.action.decline')}
              </button>
            )}
            {activeChannel.voCanAccept && (
              <button type="button" data-primary="true" disabled={pendingAction !== null} onClick={() => void performAction('accept')}>
                {t(pendingAction === 'accept' ? 'chat.action.processing' : 'chat.action.accept')}
              </button>
            )}
          </div>
        </div>
      ) : noticeKey ? (
        <div className={styles.conversationNotice} data-tone="neutral" role="status">
          <span>{t(noticeKey)}</span>
          {activeChannel?.voCanUnblock && (
            <button type="button" disabled={pendingAction !== null} onClick={() => void performAction('unblock')}>
              {t(pendingAction === 'unblock' ? 'chat.action.processing' : 'chat.action.unblock')}
            </button>
          )}
        </div>
      ) : null}

      {actionError && <div className={styles.conversationActionError} role="alert">{actionError}</div>}

      {activeChannel && connectionHint && (
        <div className={styles.connectionBanner}>
          <strong>{connectionHint}</strong>
          <span>{t('chat.connection.recoveryHint')}</span>
        </div>
      )}
    </header>
  );
}
