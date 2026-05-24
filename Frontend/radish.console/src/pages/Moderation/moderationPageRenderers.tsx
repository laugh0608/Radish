import { Tag } from '@radish/ui';
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
  switch (value) {
    case 'Approved':
      return <Tag color="success">已通过</Tag>;
    case 'Rejected':
      return <Tag color="error">已驳回</Tag>;
    default:
      return <Tag color="processing">待审核</Tag>;
  }
}

export function ActionTypeTag({ value }: StatusTagProps) {
  switch (value) {
    case 'Mute':
      return <Tag color="orange">禁言</Tag>;
    case 'Ban':
      return <Tag color="red">封禁</Tag>;
    case 'Unmute':
      return <Tag color="green">解除禁言</Tag>;
    case 'Unban':
      return <Tag color="cyan">解除封禁</Tag>;
    default:
      return <Tag>无动作</Tag>;
  }
}

function TargetNavigationState({ status, messageText }: TargetNavigationStateProps) {
  const statusMeta = resolveNavigationStatusLabel(status);

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">当前状态</div>
      <div className="moderation-target__state">
        <Tag color={statusMeta.color}>{statusMeta.label}</Tag>
        {messageText ? <div className="moderation-target__state-message">{messageText}</div> : null}
      </div>
    </div>
  );
}

function SnapshotSection({ input }: ModerationTargetDisplayProps) {
  const hasSnapshotTitle = !!input.snapshotTitle?.trim();
  const hasSnapshotSummary = !!input.snapshotSummary?.trim();

  if (!hasSnapshotTitle && !hasSnapshotSummary && !input.snapshotIsPersisted) {
    return null;
  }

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">
        {input.snapshotIsPersisted ? '创建时快照' : '目标摘要（旧数据兼容）'}
      </div>
      {hasSnapshotTitle ? <div className="moderation-target__snapshot-title">{input.snapshotTitle}</div> : null}
      {hasSnapshotSummary ? <div className="moderation-target__snapshot-summary">{input.snapshotSummary}</div> : null}
      {!hasSnapshotTitle && !hasSnapshotSummary ? <div className="moderation-target__empty">未保留文本摘要</div> : null}
    </div>
  );
}

export function ModerationTargetDisplay({ input }: ModerationTargetDisplayProps) {
  return (
    <div className="moderation-target">
      <div className="moderation-target__identity">{getTargetTypeLabel(input.targetType)} #{input.targetContentId ?? '-'}</div>
      {input.targetType === 'Comment' && input.targetPostId ? (
        <div className="moderation-target__meta">
          帖子 #{input.targetPostId} · 评论 #{input.targetCommentId ?? input.targetContentId}
        </div>
      ) : null}
      {input.targetType === 'PostQuickReply' && input.targetPostId ? (
        <div className="moderation-target__meta">所属帖子 #{input.targetPostId}</div>
      ) : null}
      {input.targetType === 'ChatMessage' && input.targetChannelId ? (
        <div className="moderation-target__meta">
          频道 #{input.targetChannelId} · 消息 #{input.targetMessageId ?? input.targetContentId}
        </div>
      ) : null}
      <SnapshotSection input={input} />
      <TargetNavigationState status={input.navigationStatus} messageText={input.navigationMessage} />
      {input.showTargetUser ? (
        <div className="moderation-target__user">{input.targetUserName || `用户 ${input.targetUserId}`}</div>
      ) : null}
    </div>
  );
}
