import { useTranslation } from 'react-i18next';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { getMessagePreviewText, type PendingImageDraft } from './chatApp.helpers';
import { resolveVisibleUserDisplayName } from '@/utils/userIdentityDisplay';
import styles from './ChatApp.module.css';

interface ComposerTypingUser {
  userId: EntityIdValue;
  userName: string;
}

interface ChatComposerStatusProps {
  typingUsers: ComposerTypingUser[];
  replyTarget: ChannelMessageVo | null;
  pendingImage: PendingImageDraft | null;
  pendingImagePreviewUrl: string | null;
  activeChannelId: EntityIdValue | null;
  hasComposerContent: boolean;
  uploadingImage: boolean;
  imageUploadProgress: number;
  onOpenUserProfile: (targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => void;
  onCancelReply: () => void;
  onRemovePendingImage: () => void;
}

export const ChatComposerStatus = ({
  typingUsers,
  replyTarget,
  pendingImage,
  pendingImagePreviewUrl,
  activeChannelId,
  hasComposerContent,
  uploadingImage,
  imageUploadProgress,
  onOpenUserProfile,
  onCancelReply,
  onRemovePendingImage,
}: ChatComposerStatusProps) => {
  const { t } = useTranslation();
  const replyUserName = replyTarget
    ? resolveVisibleUserDisplayName({ voUserName: replyTarget.voUserName }, t('common.unknownUser'))
    : null;

  return (
    <>
      {typingUsers.length > 0 && (
        <div className={styles.typingHint}>
          {typingUsers.map((user, index) => (
            <span key={`${user.userId}-${index}`}>
              {index > 0 ? '、' : null}
              <button
                type="button"
                className={styles.typingUserButton}
                onClick={() => onOpenUserProfile(user.userId, user.userName)}
                title={t('chat.viewProfile', { name: user.userName })}
              >
                {user.userName}
              </button>
            </span>
          ))} {t('chat.typing')}
        </div>
      )}

      {replyTarget && (
        <div className={styles.replyPreview}>
          <div className={styles.replyPreviewBody}>
            <div className={styles.replyPreviewLabel}>
              {t('chat.replyTo', { name: replyUserName || t('common.unknownUser') })}
            </div>
            <div className={styles.replyPreviewText}>{getMessagePreviewText(replyTarget, t)}</div>
          </div>
          <button
            type="button"
            className={styles.replyCancelButton}
            onClick={onCancelReply}
          >
            {t('chat.replyCancel')}
          </button>
        </div>
      )}

      {pendingImage && (
        <div className={styles.pendingImagePreview}>
          {pendingImagePreviewUrl && (
            <img
              src={pendingImagePreviewUrl}
              alt={t('chat.pendingImage')}
              className={styles.pendingImageThumbnail}
              loading="lazy"
            />
          )}
          <div className={styles.pendingImageMeta}>
            <div className={styles.pendingImageLabel}>{t('chat.pendingImage')}</div>
            <div className={styles.pendingImageName}>
              {pendingImage.fileName?.trim() || t('chat.imageMessage')}
            </div>
          </div>
          <button
            type="button"
            className={styles.pendingImageRemoveButton}
            onClick={onRemovePendingImage}
          >
            {t('chat.pendingImageRemove')}
          </button>
        </div>
      )}

      {activeChannelId !== null && hasComposerContent && (
        <div className={styles.draftHint}>{t('chat.draftSaved')}</div>
      )}

      {uploadingImage && (
        <div className={styles.uploadStatus}>
          {t('chat.imageUploading', { progress: Math.max(0, Math.min(100, imageUploadProgress)) })}
        </div>
      )}
    </>
  );
};
