import { Tag } from '@radish/ui';
import { useTranslation } from 'react-i18next';
import {
  getTargetTypeLabel,
  resolveNavigationStatusLabel,
  type ModerationTargetDisplayInput,
} from './moderationPageHelpers';

interface StatusTagProps {
  value: string;
}

interface ModerationTargetDisplayProps {
  input: ModerationTargetDisplayInput;
}

interface TargetNavigationStateProps {
  status?: string | null;
  messageText?: string | null;
}

export function ReportStatusTag({ value }: StatusTagProps) {
  const { t } = useTranslation();
  switch (value) {
    case 'Approved':
      return <Tag color="success">{t('moderation.reviewStatus.approved')}</Tag>;
    case 'Rejected':
      return <Tag color="error">{t('moderation.reviewStatus.rejected')}</Tag>;
    default:
      return <Tag color="processing">{t('moderation.reviewStatus.pending')}</Tag>;
  }
}

export function ActionTypeTag({ value }: StatusTagProps) {
  const { t } = useTranslation();
  switch (value) {
    case 'Mute':
      return <Tag color="orange">{t('moderation.action.mute')}</Tag>;
    case 'Ban':
      return <Tag color="red">{t('moderation.action.ban')}</Tag>;
    case 'Unmute':
      return <Tag color="green">{t('moderation.action.unmute')}</Tag>;
    case 'Unban':
      return <Tag color="cyan">{t('moderation.action.unban')}</Tag>;
    default:
      return <Tag>{t('moderation.action.none')}</Tag>;
  }
}

function TargetNavigationState({ status, messageText }: TargetNavigationStateProps) {
  const { t } = useTranslation();
  const statusMeta = resolveNavigationStatusLabel(status, t);

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">{t('moderation.target.currentStatus')}</div>
      <div className="moderation-target__state">
        <Tag color={statusMeta.color}>{statusMeta.label}</Tag>
        {messageText ? <div className="moderation-target__state-message">{messageText}</div> : null}
      </div>
    </div>
  );
}

function SnapshotSection({ input }: ModerationTargetDisplayProps) {
  const { t } = useTranslation();
  const hasSnapshotTitle = !!input.snapshotTitle?.trim();
  const hasSnapshotSummary = !!input.snapshotSummary?.trim();

  if (!hasSnapshotTitle && !hasSnapshotSummary && !input.snapshotIsPersisted) {
    return null;
  }

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">
        {t(input.snapshotIsPersisted ? 'moderation.target.snapshot' : 'moderation.target.legacySummary')}
      </div>
      {hasSnapshotTitle ? <div className="moderation-target__snapshot-title">{input.snapshotTitle}</div> : null}
      {hasSnapshotSummary ? <div className="moderation-target__snapshot-summary">{input.snapshotSummary}</div> : null}
      {!hasSnapshotTitle && !hasSnapshotSummary ? <div className="moderation-target__empty">{t('moderation.target.noSummary')}</div> : null}
    </div>
  );
}

export function ModerationTargetDisplay({ input }: ModerationTargetDisplayProps) {
  const { t } = useTranslation();
  return (
    <div className="moderation-target">
      <div className="moderation-target__identity">{getTargetTypeLabel(input.targetType, t)} #{input.targetContentId ?? '-'}</div>
      {input.targetType === 'Comment' && input.targetPostId ? (
        <div className="moderation-target__meta">
          {t('moderation.target.postComment', { postId: input.targetPostId, commentId: input.targetCommentId ?? input.targetContentId })}
        </div>
      ) : null}
      {input.targetType === 'PostQuickReply' && input.targetPostId ? (
        <div className="moderation-target__meta">{t('moderation.target.parentPost', { postId: input.targetPostId })}</div>
      ) : null}
      {input.targetType === 'ChatMessage' && input.targetChannelId ? (
        <div className="moderation-target__meta">
          {t('moderation.target.channelMessage', { channelId: input.targetChannelId, messageId: input.targetMessageId ?? input.targetContentId })}
        </div>
      ) : null}
      <SnapshotSection input={input} />
      <TargetNavigationState status={input.navigationStatus} messageText={input.navigationMessage} />
      {input.showTargetUser ? (
        <div className="moderation-target__user">{input.targetUserName || t('moderation.target.user', { id: input.targetUserId ?? '-' })}</div>
      ) : null}
    </div>
  );
}
