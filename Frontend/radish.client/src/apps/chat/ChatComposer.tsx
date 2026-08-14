import {
  type ChangeEvent,
  type ClipboardEvent,
  type KeyboardEvent,
  type RefObject,
  useRef,
} from 'react';
import { useTranslation } from 'react-i18next';
import { attachmentImageAccept } from '@radish/ui';
import type { UserMentionOption } from '@/api/user';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import type { PendingImageDraft } from './chatApp.helpers';
import { ChatComposerStatus } from './ChatComposerStatus';
import { ChatMentionMenu } from './ChatMentionMenu';
import styles from './ChatComposer.module.css';

interface ChatComposerTypingUser {
  userId: EntityIdValue;
  userName: string;
}

interface ChatComposerProps {
  activeChannelId: EntityIdValue | null;
  canSend: boolean;
  canSendAttachment: boolean;
  conversationNoticeKey: string | null;
  typingUsers: ChatComposerTypingUser[];
  replyTarget: ChannelMessageVo | null;
  pendingImage: PendingImageDraft | null;
  pendingImagePreviewUrl: string | null;
  hasComposerContent: boolean;
  uploadingImage: boolean;
  imageUploadProgress: number;
  messageInput: string;
  placeholder: string;
  textareaRef: RefObject<HTMLTextAreaElement | null>;
  imageInputRef: RefObject<HTMLInputElement | null>;
  mentionOpen: boolean;
  mentionKeyword: string;
  mentionLoading: boolean;
  mentionOptions: UserMentionOption[];
  mentionSelectedIndex: number;
  apiBaseUrl: string;
  onMessageInputChange: (value: string, cursor: number) => void;
  onRefreshMentionContext: (cursor: number) => void;
  onSelectMention: (option: UserMentionOption) => void;
  onSelectMentionIndex: (index: number) => void;
  onCloseMention: () => void;
  onStartTyping: (channelId: EntityIdValue) => void;
  onPaste: (event: ClipboardEvent<HTMLTextAreaElement>) => void;
  onImageSelected: (event: ChangeEvent<HTMLInputElement>) => void;
  onSend: () => void;
  onOpenUserProfile: (targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => void;
  onCancelReply: () => void;
  onRemovePendingImage: () => void;
}

export const ChatComposer = ({
  activeChannelId,
  canSend,
  canSendAttachment,
  conversationNoticeKey,
  typingUsers,
  replyTarget,
  pendingImage,
  pendingImagePreviewUrl,
  hasComposerContent,
  uploadingImage,
  imageUploadProgress,
  messageInput,
  placeholder,
  textareaRef,
  imageInputRef,
  mentionOpen,
  mentionKeyword,
  mentionLoading,
  mentionOptions,
  mentionSelectedIndex,
  apiBaseUrl,
  onMessageInputChange,
  onRefreshMentionContext,
  onSelectMention,
  onSelectMentionIndex,
  onCloseMention,
  onStartTyping,
  onPaste,
  onImageSelected,
  onSend,
  onOpenUserProfile,
  onCancelReply,
  onRemovePendingImage,
}: ChatComposerProps) => {
  const { t } = useTranslation();
  const lastTypingAtRef = useRef(0);

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (mentionOpen) {
      if (event.key === 'ArrowDown') {
        event.preventDefault();
        onSelectMentionIndex(mentionOptions.length > 0
          ? (mentionSelectedIndex + 1) % mentionOptions.length
          : 0);
        return;
      }

      if (event.key === 'ArrowUp') {
        event.preventDefault();
        onSelectMentionIndex(mentionOptions.length > 0
          ? (mentionSelectedIndex - 1 + mentionOptions.length) % mentionOptions.length
          : 0);
        return;
      }

      if ((event.key === 'Enter' || event.key === 'Tab') && mentionOptions[mentionSelectedIndex]) {
        event.preventDefault();
        onSelectMention(mentionOptions[mentionSelectedIndex]);
        return;
      }

      if (event.key === 'Escape') {
        event.preventDefault();
        onCloseMention();
        return;
      }
    }

    if (event.key === 'Escape' && replyTarget) {
      event.preventDefault();
      onCancelReply();
      return;
    }

    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      onSend();
    }
  };

  return (
    <footer className={styles.inputArea}>
      {activeChannelId && !canSend && (
        <div className={styles.composerRestriction} role="status">
          {t(conversationNoticeKey || 'chat.inputConversationUnavailable')}
        </div>
      )}
      <ChatComposerStatus
        typingUsers={typingUsers}
        replyTarget={replyTarget}
        pendingImage={pendingImage}
        pendingImagePreviewUrl={pendingImagePreviewUrl}
        activeChannelId={activeChannelId}
        hasComposerContent={hasComposerContent}
        uploadingImage={uploadingImage}
        imageUploadProgress={imageUploadProgress}
        onOpenUserProfile={onOpenUserProfile}
        onCancelReply={onCancelReply}
        onRemovePendingImage={onRemovePendingImage}
      />

      <div className={styles.inputRow}>
        <div className={styles.inputWrap}>
          <textarea
            ref={textareaRef}
            className={styles.input}
            value={messageInput}
            onChange={(event) => {
              const nextValue = event.target.value;
              onMessageInputChange(nextValue, event.target.selectionStart);

              const now = Date.now();
              if (activeChannelId && now - lastTypingAtRef.current >= 2000) {
                lastTypingAtRef.current = now;
                onStartTyping(activeChannelId);
              }
            }}
            onClick={(event) => onRefreshMentionContext(event.currentTarget.selectionStart)}
            onKeyUp={(event) => onRefreshMentionContext(event.currentTarget.selectionStart)}
            onPaste={onPaste}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            disabled={!activeChannelId || !canSend}
          />

          {mentionOpen && (
            <ChatMentionMenu
              keyword={mentionKeyword}
              loading={mentionLoading}
              options={mentionOptions}
              selectedIndex={mentionSelectedIndex}
              apiBaseUrl={apiBaseUrl}
              onSelect={onSelectMention}
              onSelectIndex={onSelectMentionIndex}
            />
          )}
        </div>

        <div className={styles.actionColumn}>
          <input
            ref={imageInputRef}
            type="file"
            accept={attachmentImageAccept}
            className={styles.hiddenFileInput}
            onChange={onImageSelected}
          />
          <button
            className={styles.imageButton}
            type="button"
            disabled={!activeChannelId || !canSendAttachment || uploadingImage}
            onClick={() => imageInputRef.current?.click()}
          >
            {uploadingImage ? t('chat.uploading') : t('chat.image')}
          </button>
          <button
            className={styles.sendButton}
            type="button"
            disabled={!activeChannelId || !canSend || uploadingImage || !hasComposerContent}
            onClick={onSend}
          >
            {t('chat.send')}
          </button>
        </div>
      </div>
    </footer>
  );
};
