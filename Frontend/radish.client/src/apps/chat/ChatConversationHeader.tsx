import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { toast } from '@radish/ui/toast';
import {
  acceptDirectConversation,
  declineDirectConversation,
  setDirectConversationArchived,
} from '@/api/chat';
import { blockUser, unblockUser } from '@/api/userBlock';
import {
  completeUserInteractionOperation,
  getStableUserInteractionOperationKey,
  publishUserInteractionChanged,
} from '@/services/userInteractionSync';
import type { ChannelVo, DirectConversationAction, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { resolveMediaUrl } from '@/utils/media';
import { getErrorMessage } from './chatApp.helpers';
import { isDirectConversationChannel, resolveConversationNoticeKey } from './chatConversationPresentation';
import styles from './ChatApp.module.css';
import searchStyles from './ChatSearchControls.module.css';

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
  searchOpen: boolean;
  onOpenSearch: () => void;
  compact: boolean;
  showMembersAction: boolean;
  memberPanelOpen: boolean;
  onToggleMembers: () => void;
}

export function ChatConversationHeader({
  activeChannel,
  showBackToList,
  onBackToList,
  connectionHint,
  routeUnavailable,
  onOpenUserProfile,
  onConversationChanged,
  searchOpen,
  onOpenSearch,
  compact,
  showMembersAction,
  memberPanelOpen,
  onToggleMembers,
}: ChatConversationHeaderProps) {
  const { t } = useTranslation();
  const [pendingAction, setPendingAction] = useState<DirectConversationAction | null>(null);
  const [confirmAction, setConfirmAction] = useState<'block' | 'unblock' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const isDirect = isDirectConversationChannel(activeChannel);
  const noticeKey = resolveConversationNoticeKey(activeChannel);
  const peerUserId = normalizeEntityId(activeChannel?.voPeerUserId);
  const channelName = activeChannel?.voPeerDisplayName?.trim() || activeChannel?.voName;
  const peerPublicId = activeChannel?.voPeerPublicId?.trim() ?? '';
  const channelAvatarUrl = resolveMediaUrl(activeChannel?.voPeerAvatarUrl);
  const conversationKindKey = activeChannel?.voConversationKind
    ? `chat.section.${activeChannel.voConversationKind}`
    : null;

  useEffect(() => {
    if (!confirmAction || compact) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && pendingAction === null) {
        setConfirmAction(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [compact, confirmAction, pendingAction]);

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
          if (!peerPublicId) {
            throw new Error(t('chat.action.block.missingTarget'));
          }
          {
            const operationKey = getStableUserInteractionOperationKey('block', peerPublicId);
            const result = await blockUser(peerPublicId, operationKey, t);
            completeUserInteractionOperation('block', peerPublicId);
            publishUserInteractionChanged({
              voRelationshipVersion: result.voRelationshipVersion,
            });
          }
          break;
        case 'unblock':
          if (!peerPublicId) {
            throw new Error(t('chat.action.unblock.missingTarget'));
          }
          {
            const operationKey = getStableUserInteractionOperationKey('unblock', peerPublicId);
            const result = await unblockUser(peerPublicId, operationKey, t);
            completeUserInteractionOperation('unblock', peerPublicId);
            publishUserInteractionChanged({
              voRelationshipVersion: result.voRelationshipVersion,
            });
          }
          break;
        case 'archive':
          await setDirectConversationArchived(activeChannel.voId, true);
          break;
        case 'unarchive':
          await setDirectConversationArchived(activeChannel.voId, false);
          break;
      }

      toast.success(t(`chat.action.${action}.success`));
      setConfirmAction(null);
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

  const confirmationTitle = confirmAction
    ? t(confirmAction === 'block' ? 'userBlock.confirm.blockTitle' : 'userBlock.confirm.unblockTitle', {
        name: channelName,
      })
    : '';
  const confirmationContent = confirmAction ? (
    <>
      <p className={styles.relationshipConfirmKicker}>{t('userBlock.confirm.kicker')}</p>
      <p>
        {t(confirmAction === 'block'
          ? 'userBlock.confirm.blockDescription'
          : 'userBlock.confirm.unblockDescription')}
      </p>
      <ul>
        <li>{t('userBlock.confirm.followImpact')}</li>
        <li>{t('userBlock.confirm.directImpact')}</li>
        <li>{t('userBlock.confirm.publicImpact')}</li>
      </ul>
      <div className={styles.relationshipConfirmActions}>
        <button type="button" autoFocus disabled={pendingAction !== null} onClick={() => setConfirmAction(null)}>
          {t('common.cancel')}
        </button>
        <button
          type="button"
          data-primary="true"
          disabled={pendingAction !== null}
          onClick={() => void performAction(confirmAction)}
        >
          {t(pendingAction ? 'chat.action.processing' : `chat.action.${confirmAction}`)}
        </button>
      </div>
    </>
  ) : null;

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
          <div className={styles.conversationIdentity}>
            {activeChannel && (
              <span className={styles.conversationAvatar} aria-hidden="true">
                {channelAvatarUrl ? (
                  <img src={channelAvatarUrl} alt="" loading="lazy" />
                ) : (
                  activeChannel.voIconEmoji || channelName?.charAt(0).toUpperCase() || '#'
                )}
              </span>
            )}
            <div className={styles.conversationIdentityCopy}>
              <div className={styles.channelTitle}>
                {activeChannel ? channelName : t('chat.selectChannel')}
              </div>
              <div className={styles.conversationMeta}>
                {conversationKindKey && <span>{t(conversationKindKey)}</span>}
                {activeChannel?.voDescription && <span>{activeChannel.voDescription}</span>}
              </div>
            </div>
          </div>
        </div>

        <div className={styles.conversationHeaderActions}>
          <button
            type="button"
            className={searchStyles.conversationSearchButton}
            data-chat-search-trigger="true"
            aria-label={t('chat.search.open')}
            aria-pressed={searchOpen}
            onClick={onOpenSearch}
          >
            <Icon icon="mdi:magnify" size={17} />
            <span>{t('chat.search.open')}</span>
          </button>
          {showMembersAction && (
            <button
              type="button"
              className={styles.conversationMembersButton}
              aria-pressed={memberPanelOpen}
              onClick={onToggleMembers}
            >
              <Icon icon="mdi:account-group-outline" size={17} />
              <span>{t('chat.onlineMembers')}</span>
            </button>
          )}
          {isDirect && activeChannel && (
            <>
              {peerUserId && activeChannel.voIsPeerAvailable && (
                <button
                  type="button"
                  className={styles.conversationProfileButton}
                  aria-label={t('chat.openPeerProfile')}
                  onClick={openPeerProfile}
                >
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
                  {activeChannel.voCanBlock && peerPublicId && (
                    <button type="button" disabled={pendingAction !== null} onClick={() => setConfirmAction('block')}>
                      <Icon icon="mdi:shield-off-outline" size={17} />
                      <span>{t('chat.action.block')}</span>
                    </button>
                  )}
                </div>
              </details>
            </>
          )}
        </div>
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
            {activeChannel.voCanBlock && peerPublicId && (
              <button type="button" disabled={pendingAction !== null} onClick={() => setConfirmAction('block')}>
                {t('chat.action.block')}
              </button>
            )}
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
          {activeChannel?.voCanUnblock && peerPublicId && (
            <button type="button" disabled={pendingAction !== null} onClick={() => setConfirmAction('unblock')}>
              {t(pendingAction === 'unblock' ? 'chat.action.processing' : 'chat.action.unblock')}
            </button>
          )}
        </div>
      ) : null}

      {actionError && <div className={styles.conversationActionError} role="alert">{actionError}</div>}

      {confirmAction && compact && (
        <BottomSheet
          isOpen
          onClose={() => setConfirmAction(null)}
          closeLabel={t('common.close')}
          title={confirmationTitle}
          height="min(68%, 560px)"
          bodyClassName={styles.relationshipConfirmSheetBody}
        >
          <div className={styles.relationshipConfirmSheet}>{confirmationContent}</div>
        </BottomSheet>
      )}

      {confirmAction && !compact && (
        <div className={styles.relationshipConfirmBackdrop} role="presentation">
          <section
            className={styles.relationshipConfirmDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="chat-relationship-confirm-title"
          >
            <h2 id="chat-relationship-confirm-title">{confirmationTitle}</h2>
            {confirmationContent}
          </section>
        </div>
      )}

      {activeChannel && connectionHint && (
        <div className={styles.connectionBanner}>
          <strong>{connectionHint}</strong>
          <span>{t('chat.connection.recoveryHint')}</span>
        </div>
      )}
    </header>
  );
}
