import type { ReactNode, RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type { ContentReportTargetType } from '@/api/contentModeration';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { isPersistedEntityId } from '@/types/chat';
import {
  formatChatTime,
  getEntityKey,
  getFallbackUserName,
  getMessagePreviewText,
  resolveMediaUrl,
  toNumericId,
} from './chatApp.helpers';
import styles from './ChatApp.module.css';

interface ChatMessageListProps {
  activeChannelId: EntityIdValue | null;
  activeChannelKey: string;
  messages: ChannelMessageVo[];
  loadingHistory: boolean;
  highlightedMessageId: string | null;
  currentUserIdKey: string;
  apiBaseUrl: string;
  hasMoreNewerHistory: Record<string, boolean>;
  messageScrollRef: RefObject<HTMLDivElement | null>;
  setMessageElementRef: (messageId: string, element: HTMLDivElement | null) => void;
  renderAvatarButton: (userId: EntityIdValue, userName?: string | null, avatarUrl?: string | null) => ReactNode;
  renderMessageContent: (content: string | null | undefined) => ReactNode;
  onScroll: () => void;
  onOpenUserProfile: (targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => void;
  onReply: (message: ChannelMessageVo) => void;
  onRecall: (messageId: EntityIdValue) => void;
  onOpenReport: (targetType: ContentReportTargetType, targetId: number) => void;
  onRetryMessage: (message: ChannelMessageVo) => void;
  onDismissFailedMessage: (message: ChannelMessageVo) => void;
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
  onDismissFailedMessage,
  onLoadNewerHistory,
}: ChatMessageListProps) => {
  const { t } = useTranslation();

  return (
    <div className={styles.messageViewport} ref={messageScrollRef} onScroll={onScroll}>
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
          const messageImageUrl = resolveMediaUrl(apiBaseUrl, message.voImageUrl);
          const canReportMessage = !isMine && !message.voIsRecalled && messageStatus === 'sent' && isPersistedEntityId(message.voId);

          return (
            <div
              key={messageIdKey || message.voClientRequestId || message.voCreateTime}
              ref={messageIdKey ? (element) => setMessageElementRef(messageIdKey, element) : undefined}
              className={`${styles.messageRow} ${isMine ? styles.mine : ''} ${isHighlightedMessage ? styles.messageRowTargeted : ''}`.trim()}
            >
              <div className={styles.messageMain}>
                {renderAvatarButton(
                  message.voUserId,
                  message.voUserName,
                  message.voUserAvatarUrl
                )}

                <div className={styles.messageContent}>
                  <div className={styles.metaLine}>
                    <button
                      type="button"
                      className={styles.userNameButton}
                      onClick={() => onOpenUserProfile(message.voUserId, message.voUserName, message.voUserAvatarUrl)}
                      disabled={!canOpenMessageUserProfile}
                      title={canOpenMessageUserProfile
                        ? t('chat.viewProfile', { name: (message.voUserName || getFallbackUserName(messageUserIdKey, t)).trim() })
                        : t('chat.userUnavailable')}
                    >
                      <span className={styles.userName}>{message.voUserName || t('common.unknownUser')}</span>
                    </button>
                    <span className={styles.time}>{formatChatTime(message.voCreateTime)}</span>
                    {!message.voIsRecalled && messageStatus === 'sent' && (
                      <button
                        type="button"
                        className={styles.replyButton}
                        onClick={() => onReply(message)}
                      >
                        {t('chat.reply')}
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
                            {t('chat.replyTo', { name: message.voReplyTo.voUserName || t('common.unknownUser') })}
                          </div>
                          <div className={styles.quotedText}>{replyText}</div>
                        </div>
                      )}
                      {message.voContent && <div className={styles.bubble}>{renderMessageContent(message.voContent)}</div>}
                      {message.voType === 2 && messageImageUrl && (
                        <img className={styles.imageMessage} src={messageImageUrl} alt={t('chat.imageMessage')} loading="lazy" />
                      )}
                      {isMine && !message.voIsRecalled && messageStatus !== 'sent' && (
                        <div className={styles.deliveryState}>
                          <span className={`${styles.deliveryStateText} ${isFailedMessage ? styles.deliveryStateTextFailed : ''}`}>
                            {isSendingMessage ? t('chat.sending') : (message.voLocalError || t('chat.sendFailed'))}
                          </span>
                          {isFailedMessage && (
                            <>
                              <button
                                type="button"
                                className={styles.deliveryActionButton}
                                onClick={() => onRetryMessage(message)}
                              >
                                {t('chat.retry')}
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
  );
};
