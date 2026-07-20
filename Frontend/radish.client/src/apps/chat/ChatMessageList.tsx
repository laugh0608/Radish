import { useMemo, type ReactNode, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  ChatMessageReactionStateVo,
  ChatReadReceiptMode,
  ChatReadReceiptSummaryItemVo,
} from '@radish/http';
import {
  ReactionBar,
  type ReactionTogglePayload,
} from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import type { ContentReportTargetType } from '@/api/contentModeration';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { isPersistedEntityId } from '@/types/chat';
import type { ChatConnectionState } from '@/stores/chatStore';
import {
  formatChatTime,
  getEntityKey,
  getFallbackUserName,
  getMessagePreviewText,
  resolveMediaUrl,
  toNumericId,
} from './chatApp.helpers';
import { resolveVisibleUserDisplayName } from '@/utils/userIdentityDisplay';
import { getIntlLocale } from '@/locales/language';
import { ChatProtectedImage } from './ChatProtectedImage';
import { createChatReactionBarLabels } from './chatReactionBarLabels';
import { ChatPinnedMessages } from './ChatPinnedMessages';
import { ChatReadReceiptIndicator } from './ChatReadReceiptIndicator';
import { useChatMessagePins } from './useChatMessagePins';
import styles from './ChatApp.module.css';

interface ChatMessageListProps {
  activeChannelId: EntityIdValue | null;
  activeChannelKey: string;
  messages: ChannelMessageVo[];
  loadingHistory: boolean;
  highlightedMessageId: string | null;
  currentUserIdKey: string;
  apiBaseUrl: string;
  canSendMessages: boolean;
  canReact: boolean;
  canPinMessages: boolean;
  connectionState: ChatConnectionState;
  messageTargetUnavailable: boolean;
  reactionStateMap: Record<string, ChatMessageReactionStateVo>;
  reactionLoading: boolean;
  reactionLoadError: string | null;
  readReceiptMode: ChatReadReceiptMode;
  readReceiptItemMap: Record<string, ChatReadReceiptSummaryItemVo>;
  directReadBoundaryMessageId: string | null;
  readReceiptLoadError: string | null;
  compact: boolean;
  stickerGroups: StickerPickerGroup[];
  hasMoreNewerHistory: Record<string, boolean>;
  messageScrollRef: RefObject<HTMLDivElement | null>;
  setMessageElementRef: (messageId: string, element: HTMLDivElement | null) => void;
  renderAvatarButton: (userId: EntityIdValue, userName?: string | null, avatarUrl?: string | null) => ReactNode;
  renderMessageContent: (content: string | null | undefined) => ReactNode;
  onScroll: () => void;
  onOpenUserProfile: (
    targetUserId: EntityIdValue,
    targetUserName?: string | null,
    avatarUrl?: string | null,
    publicId?: string | null
  ) => void;
  onReply: (message: ChannelMessageVo) => void;
  onRecall: (messageId: EntityIdValue) => void;
  onOpenReport: (targetType: ContentReportTargetType, targetId: number) => void;
  onRetryMessage: (message: ChannelMessageVo) => void;
  onCopyFailedMessageDiagnostics: (message: ChannelMessageVo) => void;
  onDismissFailedMessage: (message: ChannelMessageVo) => void;
  onToggleReaction: (messageId: EntityIdValue, payload: ReactionTogglePayload) => Promise<void>;
  onNavigateToMessage: (messageId: string) => void;
  onRetryReactionLoad: () => void;
  onRetryReadReceiptLoad: () => void;
  onLoadNewerHistory: () => void;
}

export const ChatMessageList = ({
  activeChannelId,
  activeChannelKey,
  messages,
  loadingHistory,
  highlightedMessageId,
  currentUserIdKey,
  apiBaseUrl,
  canSendMessages,
  canReact,
  canPinMessages,
  connectionState,
  messageTargetUnavailable,
  reactionStateMap,
  reactionLoading,
  reactionLoadError,
  readReceiptMode,
  readReceiptItemMap,
  directReadBoundaryMessageId,
  readReceiptLoadError,
  compact,
  stickerGroups,
  hasMoreNewerHistory,
  messageScrollRef,
  setMessageElementRef,
  renderAvatarButton,
  renderMessageContent,
  onScroll,
  onOpenUserProfile,
  onReply,
  onRecall,
  onOpenReport,
  onRetryMessage,
  onCopyFailedMessageDiagnostics,
  onDismissFailedMessage,
  onToggleReaction,
  onNavigateToMessage,
  onRetryReactionLoad,
  onRetryReadReceiptLoad,
  onLoadNewerHistory,
}: ChatMessageListProps) => {
  const { t, i18n } = useTranslation();
  const locale = getIntlLocale(i18n.resolvedLanguage ?? i18n.language);
  const reactionLabels = useMemo(() => createChatReactionBarLabels(t), [t]);
  const {
    state: pinState,
    pinnedMessageIds,
    loading: pinLoading,
    loadError: pinLoadError,
    pendingMessageId: pinPendingMessageId,
    retry: retryPinLoad,
    setPinned,
  } = useChatMessagePins({
    activeChannelId,
    connectionState,
    canPinMessages,
    refreshAfterTargetUnavailable: messageTargetUnavailable,
  });

  return (
    <>
      <div className={styles.pinRegion}>
        <ChatPinnedMessages
          state={pinState}
          loading={pinLoading}
          loadError={pinLoadError}
          canManage={canPinMessages}
          pendingMessageId={pinPendingMessageId}
          onRetry={retryPinLoad}
          onNavigate={onNavigateToMessage}
          onSetPinned={setPinned}
        />
      </div>
      <div className={styles.messageViewport} ref={messageScrollRef} onScroll={onScroll}>
      {activeChannelId !== null && reactionLoadError && (
        <div className={styles.reactionLoadError} role="alert">
          <span>{reactionLoadError}</span>
          <button type="button" onClick={onRetryReactionLoad}>
            {t('chat.reaction.retry')}
          </button>
        </div>
      )}
      {activeChannelId !== null && readReceiptLoadError && (
        <div className={styles.reactionLoadError} role="alert">
          <span>{readReceiptLoadError}</span>
          <button type="button" onClick={onRetryReadReceiptLoad}>
            {t('chat.receipt.retry')}
          </button>
        </div>
      )}
      {activeChannelId === null ? (
        <div className={styles.placeholder}>{t('chat.inputSelectChannel')}</div>
      ) : messages.length === 0 && loadingHistory ? (
        <div className={styles.placeholder}>{t('chat.loadingHistory')}</div>
      ) : messages.length === 0 ? (
        <div className={styles.placeholder}>{t('chat.noMessages')}</div>
      ) : (
        messages.map((message: ChannelMessageVo) => {
          const messageIdKey = getEntityKey(message.voId);
          const messageUserIdKey = getEntityKey(message.voUserId);
          const canOpenMessageUserProfile = !!messageUserIdKey && messageUserIdKey !== '0' && !messageUserIdKey.startsWith('-');
          const isHighlightedMessage = !!messageIdKey && messageIdKey === highlightedMessageId;
          const isMine = !!messageUserIdKey && messageUserIdKey === currentUserIdKey;
          const messageStatus = message.voLocalStatus ?? 'sent';
          const isSendingMessage = messageStatus === 'sending';
          const isFailedMessage = messageStatus === 'failed';
          const replyText = message.voReplyTo ? getMessagePreviewText(message.voReplyTo, t) : null;
          const reactionState = messageIdKey ? reactionStateMap[messageIdKey] : undefined;
          const isPinned = Boolean(messageIdKey && pinnedMessageIds.has(messageIdKey));
          const canManagePin = Boolean(
            canPinMessages
            && messageIdKey
            && isPersistedEntityId(message.voId)
            && !message.voIsRecalled
            && messageStatus === 'sent'
          );
          const canShowReactionBar = Boolean(
            messageIdKey
            && isPersistedEntityId(message.voId)
            && !message.voIsRecalled
            && messageStatus === 'sent'
            && (canReact || (reactionState?.voItems.length ?? 0) > 0)
          );
          const messageImageUrl = resolveMediaUrl(apiBaseUrl, message.voImageUrl);
          const canReportMessage = !isMine && !message.voIsRecalled && messageStatus === 'sent' && isPersistedEntityId(message.voId);
          const messageUserName = resolveVisibleUserDisplayName(
            { voUserName: message.voUserName },
            getFallbackUserName(messageUserIdKey, t)
          );
          const replyUserName = message.voReplyTo
            ? resolveVisibleUserDisplayName(
                { voUserName: message.voReplyTo.voUserName },
                t('common.unknownUser')
              )
            : null;

          return (
            <div
              key={messageIdKey || message.voClientRequestId || message.voCreateTime}
              ref={messageIdKey ? (element) => setMessageElementRef(messageIdKey, element) : undefined}
              data-chat-message-id={isPersistedEntityId(message.voId) ? messageIdKey : undefined}
              className={`${styles.messageRow} ${isMine ? styles.mine : ''} ${isHighlightedMessage ? styles.messageRowTargeted : ''}`.trim()}
            >
              <div className={styles.messageMain}>
                {renderAvatarButton(
                  message.voUserId,
                  messageUserName,
                  message.voUserAvatarUrl
                )}

                <div className={styles.messageContent}>
                  <div className={styles.metaLine}>
                    <button
                      type="button"
                      className={styles.userNameButton}
                      onClick={() => onOpenUserProfile(message.voUserId, messageUserName, message.voUserAvatarUrl)}
                      disabled={!canOpenMessageUserProfile}
                      title={canOpenMessageUserProfile
                        ? t('chat.viewProfile', { name: messageUserName })
                        : t('chat.userUnavailable')}
                    >
                      <span className={styles.userName}>{messageUserName}</span>
                    </button>
                    <span className={styles.time}>{formatChatTime(message.voCreateTime, locale)}</span>
                    {canSendMessages && !message.voIsRecalled && messageStatus === 'sent' && (
                      <button
                        type="button"
                        className={styles.replyButton}
                        onClick={() => onReply(message)}
                      >
                        {t('chat.reply')}
                      </button>
                    )}
                    {canManagePin && (
                      <button
                        type="button"
                        className={styles.pinButton}
                        onClick={() => { void setPinned(message.voId, !isPinned); }}
                        disabled={pinPendingMessageId === messageIdKey}
                      >
                        {pinPendingMessageId === messageIdKey
                          ? t('chat.pin.updating')
                          : t(isPinned ? 'chat.pin.unpin' : 'chat.pin.pin')}
                      </button>
                    )}
                    {isMine && !message.voIsRecalled && messageStatus === 'sent' && isPersistedEntityId(message.voId) && (
                      <button
                        type="button"
                        className={styles.recallButton}
                        onClick={() => onRecall(message.voId)}
                      >
                        {t('chat.recall')}
                      </button>
                    )}
                    {canReportMessage && (
                      <button
                        type="button"
                        className={styles.reportButton}
                        onClick={() => {
                          const messageId = toNumericId(message.voId);
                          if (messageId > 0) {
                            onOpenReport('ChatMessage', messageId);
                          }
                        }}
                      >
                        {t('report.action')}
                      </button>
                    )}
                  </div>

                  {message.voIsRecalled ? (
                    <div className={styles.recalled}>{t('chat.recalled')}</div>
                  ) : (
                    <div className={styles.messageStack}>
                      {message.voReplyTo && (
                        <div className={styles.quotedMessage}>
                          <div className={styles.quotedAuthor}>
                            {t('chat.replyTo', { name: replyUserName || t('common.unknownUser') })}
                          </div>
                          <div className={styles.quotedText}>{replyText}</div>
                        </div>
                      )}
                      {message.voContent && <div className={styles.bubble}>{renderMessageContent(message.voContent)}</div>}
                      {message.voType === 2 && messageImageUrl && (
                        <ChatProtectedImage
                          attachmentId={message.voAttachmentId}
                          fallbackUrl={messageImageUrl}
                          variant="thumbnail"
                          className={styles.imageMessage}
                          alt={t('chat.imageMessage')}
                        />
                      )}
                      {isMine && !message.voIsRecalled && messageStatus !== 'sent' && (
                        <div className={styles.deliveryState}>
                          <span className={`${styles.deliveryStateText} ${isFailedMessage ? styles.deliveryStateTextFailed : ''}`}>
                            {isSendingMessage ? t('chat.sending') : (message.voLocalError || t('chat.sendFailed'))}
                          </span>
                          {isFailedMessage && (
                            <>
                              <span className={styles.deliveryRecoveryHint}>{t('chat.failedRecoverableHint')}</span>
                              {canSendMessages && (
                                <button
                                  type="button"
                                  className={styles.deliveryActionButton}
                                  onClick={() => onRetryMessage(message)}
                                >
                                  {t('chat.retry')}
                                </button>
                              )}
                              <button
                                type="button"
                                className={styles.deliveryActionButton}
                                onClick={() => onCopyFailedMessageDiagnostics(message)}
                              >
                                {t('chat.copyDiagnostics')}
                              </button>
                              <button
                                type="button"
                                className={styles.deliveryActionButton}
                                onClick={() => onDismissFailedMessage(message)}
                              >
                                {t('chat.dismiss')}
                              </button>
                            </>
                          )}
                        </div>
                      )}
                      {canShowReactionBar && messageIdKey && (
                        <ReactionBar
                          targetType="ChatMessage"
                          targetId={messageIdKey}
                          items={reactionState?.voItems ?? []}
                          isLoggedIn
                          loading={reactionLoading}
                          readOnly={!canReact}
                          showAddReactionLabel
                          stickerGroups={stickerGroups}
                          className={styles.chatReactionBar}
                          labels={reactionLabels}
                          onToggle={(payload) => onToggleReaction(message.voId, payload)}
                        />
                      )}
                      {isMine && messageIdKey && isPersistedEntityId(message.voId) && !message.voIsRecalled && messageStatus === 'sent' && (
                        <ChatReadReceiptIndicator
                          apiBaseUrl={apiBaseUrl}
                          channelId={activeChannelId}
                          messageId={message.voId}
                          mode={readReceiptMode}
                          summary={readReceiptItemMap[messageIdKey]}
                          isDirectBoundary={messageIdKey === directReadBoundaryMessageId}
                          compact={compact}
                          onOpenUserProfile={onOpenUserProfile}
                        />
                      )}
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })
      )}
      {activeChannelId !== null && activeChannelKey && hasMoreNewerHistory[activeChannelKey] && (
        <div className={styles.loadNewerContainer}>
          <button
            type="button"
            className={styles.loadNewerButton}
            onClick={onLoadNewerHistory}
            disabled={loadingHistory}
          >
            {loadingHistory ? t('chat.loadingNewerHistory') : t('chat.loadNewerHistory')}
          </button>
        </div>
      )}
      </div>
    </>
  );
};
