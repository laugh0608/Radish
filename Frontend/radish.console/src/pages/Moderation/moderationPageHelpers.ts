import {
  type ConsoleLongId,
  type ContentReportQueueItemVo,
  type UserModerationActionVo,
} from '@/api/moderationApi';
import { getApiBaseUrl } from '@/config/env';

export const REVIEW_STATUS_OPTIONS = [
  { label: '全部状态', value: -1 },
  { label: '待审核', value: 0 },
  { label: '已通过', value: 1 },
  { label: '已驳回', value: 2 },
];

export const TARGET_TYPE_OPTIONS = [
  { label: '帖子', value: 'Post' },
  { label: '评论', value: 'Comment' },
  { label: '轻回应', value: 'PostQuickReply' },
  { label: '聊天消息', value: 'ChatMessage' },
  { label: '商品', value: 'Product' },
];

export const REASON_TYPE_OPTIONS = [
  { label: '垃圾内容', value: 'Spam' },
  { label: '辱骂骚扰', value: 'Abuse' },
  { label: '色情低俗', value: 'Pornography' },
  { label: '违法违规', value: 'Illegal' },
  { label: '欺诈误导', value: 'Fraud' },
  { label: '其他', value: 'Other' },
];

export const TARGET_NAVIGATION_STATUS_OPTIONS = [
  { label: '可回看', value: 'Ready' },
  { label: '已降级', value: 'Fallback' },
  { label: '已失效', value: 'Unavailable' },
  { label: '未接入回看', value: 'Unsupported' },
];

export const ACTION_OPTIONS = [
  { label: '不处罚', value: 0 },
  { label: '禁言', value: 1 },
  { label: '封禁', value: 2 },
];

export const MANUAL_ACTION_TYPE = {
  mute: 1,
  ban: 2,
  unmute: 3,
  unban: 4,
} as const;

export type ManualActionTypeValue = typeof MANUAL_ACTION_TYPE[keyof typeof MANUAL_ACTION_TYPE];

export const MANUAL_ACTION_OPTIONS = [
  { label: '禁言', value: MANUAL_ACTION_TYPE.mute },
  { label: '封禁', value: MANUAL_ACTION_TYPE.ban },
  { label: '解除禁言', value: MANUAL_ACTION_TYPE.unmute },
  { label: '解除封禁', value: MANUAL_ACTION_TYPE.unban },
];

export const ACTION_LOG_ACTION_TYPE_OPTIONS = [
  { label: '禁言', value: 'Mute' },
  { label: '封禁', value: 'Ban' },
  { label: '解除禁言', value: 'Unmute' },
  { label: '解除封禁', value: 'Unban' },
];

export const ACTION_LOG_STATUS_OPTIONS = [
  { label: '生效中', value: 'active' },
  { label: '已结束', value: 'inactive' },
];

const TARGET_TYPE_LABEL_MAP: Record<string, string> = {
  Post: '帖子',
  Comment: '评论',
  PostQuickReply: '轻回应',
  ChatMessage: '聊天消息',
  Product: '商品',
  Unknown: '未知目标',
};

const REASON_TYPE_LABEL_MAP: Record<string, string> = {
  Spam: '垃圾内容',
  Abuse: '辱骂骚扰',
  Pornography: '色情低俗',
  Illegal: '违法违规',
  Fraud: '欺诈误导',
  Other: '其他',
};

export type ActionLogActiveFilter = 'active' | 'inactive';

export interface ActionLogPreset {
  targetUserId?: string;
  sourceReportId?: string;
  actionType?: string;
  isActive?: ActionLogActiveFilter;
  keyword?: string;
  hint: string;
}

export interface QueuePreset {
  status?: number;
  targetType?: string;
  reasonType?: string;
  navigationStatus?: string;
  keyword?: string;
  hint: string;
}

export interface ManualActionPreset {
  targetUserId?: string;
  sourceReportId?: string;
  actionType?: ManualActionTypeValue;
  durationHours?: number | null;
  reason?: string;
  hint: string;
}

export interface ManualActiveModerationStatus {
  actionId: ConsoleLongId;
  actionType: 'Mute' | 'Ban';
  reason: string;
  sourceReportId?: ConsoleLongId | null;
  endTime?: string | null;
}

export interface ManualModerationStatusSnapshot {
  targetUserId: string;
  muteAction: ManualActiveModerationStatus | null;
  banAction: ManualActiveModerationStatus | null;
}

interface ModerationTargetNavigationInput {
  targetType: string | null | undefined;
  targetContentId?: ConsoleLongId | null;
  targetPostId?: ConsoleLongId | null;
  targetCommentId?: ConsoleLongId | null;
  targetChannelId?: ConsoleLongId | null;
  targetMessageId?: ConsoleLongId | null;
}

interface ModerationOpenTarget {
  label: string;
  url: string;
}

export interface ModerationTargetNavigationStateInput extends ModerationTargetNavigationInput {
  navigationStatus?: string | null;
  navigationMessage?: string | null;
}

export interface ModerationTargetDisplayInput extends ModerationTargetNavigationStateInput {
  snapshotTitle?: string | null;
  snapshotSummary?: string | null;
  snapshotIsPersisted?: boolean;
  targetUserId?: ConsoleLongId | null;
  targetUserName?: string | null;
  showTargetUser?: boolean;
}

export function getReasonTypeLabel(value: string | null | undefined): string {
  return value ? (REASON_TYPE_LABEL_MAP[value] ?? value) : '未分类';
}

export function getTargetTypeLabel(value: string | null | undefined): string {
  return value ? (TARGET_TYPE_LABEL_MAP[value] ?? value) : '未知目标';
}

export function toOptionalString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

export function toPositiveLongString(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

export function hasPositiveLongId(value: unknown): boolean {
  return toPositiveLongString(String(value ?? '')) !== undefined;
}

export function getActionTypeText(value: string | null | undefined): string {
  switch (value) {
    case 'Mute':
      return '禁言';
    case 'Ban':
      return '封禁';
    case 'Unmute':
      return '解除禁言';
    case 'Unban':
      return '解除封禁';
    default:
      return '治理动作';
  }
}

export function getManualActionTypeText(value: ManualActionTypeValue | null | undefined): string {
  switch (value) {
    case MANUAL_ACTION_TYPE.mute:
      return '禁言';
    case MANUAL_ACTION_TYPE.ban:
      return '封禁';
    case MANUAL_ACTION_TYPE.unmute:
      return '解除禁言';
    case MANUAL_ACTION_TYPE.unban:
      return '解除封禁';
    default:
      return '治理动作';
  }
}

function buildDesktopChatTargetUrl(channelId: ConsoleLongId, messageId: ConsoleLongId): string {
  const url = new URL('/desktop', getApiBaseUrl());
  url.searchParams.set('app', 'chat');
  url.searchParams.set('channelId', String(channelId));
  url.searchParams.set('messageId', String(messageId));
  return url.toString();
}

function buildPublicForumTargetUrl(postId: ConsoleLongId, commentId?: ConsoleLongId | null): string {
  const url = new URL(`/forum/post/${encodeURIComponent(String(postId))}`, getApiBaseUrl());
  if (hasPositiveLongId(commentId)) {
    url.searchParams.set('commentId', String(commentId));
  }

  return url.toString();
}

function buildPublicShopTargetUrl(productId: ConsoleLongId): string {
  return new URL(`/shop/product/${encodeURIComponent(String(productId))}`, getApiBaseUrl()).toString();
}

export function canOpenChatTarget(
  targetType: string | null | undefined,
  channelId: ConsoleLongId | null | undefined,
  messageId: ConsoleLongId | null | undefined
): boolean {
  return targetType === 'ChatMessage'
    && hasPositiveLongId(channelId)
    && hasPositiveLongId(messageId);
}

export function resolveNavigationStatusLabel(status: string | null | undefined): { color: string; label: string } {
  switch (status) {
    case 'Fallback':
      return { color: 'warning', label: '已降级' };
    case 'Unavailable':
      return { color: 'default', label: '已失效' };
    case 'Unsupported':
      return { color: 'default', label: '未接入回看' };
    default:
      return { color: 'success', label: '可回看' };
  }
}

export function buildQueueTargetDisplayInput(record: ContentReportQueueItemVo): ModerationTargetDisplayInput {
  return {
    targetType: record.voTargetType,
    targetContentId: record.voTargetContentId,
    targetPostId: record.voTargetPostId,
    targetCommentId: record.voTargetCommentId,
    targetChannelId: record.voTargetChannelId,
    targetMessageId: record.voTargetMessageId,
    navigationStatus: record.voTargetNavigationStatus,
    navigationMessage: record.voTargetNavigationMessage,
    snapshotTitle: record.voTargetSnapshotTitle,
    snapshotSummary: record.voTargetSnapshotSummary,
    snapshotIsPersisted: record.voTargetSnapshotIsPersisted,
    targetUserId: record.voTargetUserId,
    targetUserName: record.voTargetUserName,
  };
}

export function buildQueueTargetNavigationInput(record: ContentReportQueueItemVo): ModerationTargetNavigationStateInput {
  return {
    targetType: record.voTargetType,
    targetContentId: record.voTargetContentId,
    targetPostId: record.voTargetPostId,
    targetCommentId: record.voTargetCommentId,
    targetChannelId: record.voTargetChannelId,
    targetMessageId: record.voTargetMessageId,
    navigationStatus: record.voTargetNavigationStatus,
    navigationMessage: record.voTargetNavigationMessage,
  };
}

export function buildActionSourceTargetDisplayInput(record: UserModerationActionVo): ModerationTargetDisplayInput {
  return {
    targetType: record.voSourceReportTargetType,
    targetContentId: record.voSourceReportTargetContentId ?? null,
    targetPostId: record.voSourceReportTargetPostId,
    targetCommentId: record.voSourceReportTargetCommentId,
    targetChannelId: record.voSourceReportTargetChannelId,
    targetMessageId: record.voSourceReportTargetMessageId,
    navigationStatus: record.voSourceReportTargetNavigationStatus,
    navigationMessage: record.voSourceReportTargetNavigationMessage,
    snapshotTitle: record.voSourceReportTargetSnapshotTitle,
    snapshotSummary: record.voSourceReportTargetSnapshotSummary,
    snapshotIsPersisted: record.voSourceReportTargetSnapshotIsPersisted,
  };
}

export function buildActionSourceTargetNavigationInput(record: UserModerationActionVo): ModerationTargetNavigationStateInput {
  return {
    targetType: record.voSourceReportTargetType,
    targetContentId: record.voSourceReportTargetContentId,
    targetPostId: record.voSourceReportTargetPostId,
    targetCommentId: record.voSourceReportTargetCommentId,
    targetChannelId: record.voSourceReportTargetChannelId,
    targetMessageId: record.voSourceReportTargetMessageId,
    navigationStatus: record.voSourceReportTargetNavigationStatus,
    navigationMessage: record.voSourceReportTargetNavigationMessage,
  };
}

export function buildQueueManualActionReason(record: ContentReportQueueItemVo): string {
  const reasonLabel = getReasonTypeLabel(record.voReasonType);
  const reasonDetail = record.voReasonDetail?.trim();
  return reasonDetail
    ? `来源举报单 #${record.voReportId}：${reasonLabel}，${reasonDetail}`
    : `来源举报单 #${record.voReportId}：${reasonLabel}`;
}

export function buildActionLogManualActionReason(record: UserModerationActionVo, actionType?: ManualActionTypeValue): string {
  if (actionType === MANUAL_ACTION_TYPE.unmute || actionType === MANUAL_ACTION_TYPE.unban) {
    return `参考动作单 #${record.voActionId}，人工复核后${getManualActionTypeText(actionType)}`;
  }

  return `参考动作单 #${record.voActionId}，继续对用户 #${record.voTargetUserId} 执行人工治理`;
}

function pickLatestActiveModerationAction(
  actions: UserModerationActionVo[],
  actionType: 'Mute' | 'Ban'
): ManualActiveModerationStatus | null {
  const matchedAction = actions
    .filter(action => action.voIsActive && action.voActionType === actionType)
    .sort((left, right) => new Date(right.voStartTime).getTime() - new Date(left.voStartTime).getTime())[0];

  if (!matchedAction) {
    return null;
  }

  return {
    actionId: matchedAction.voActionId,
    actionType,
    reason: matchedAction.voReason,
    sourceReportId: matchedAction.voSourceReportId,
    endTime: matchedAction.voEndTime,
  };
}

export function buildManualModerationStatusSnapshot(
  targetUserId: string,
  actions: UserModerationActionVo[]
): ManualModerationStatusSnapshot {
  return {
    targetUserId,
    muteAction: pickLatestActiveModerationAction(actions, 'Mute'),
    banAction: pickLatestActiveModerationAction(actions, 'Ban'),
  };
}

export function resolveOpenTarget(input: ModerationTargetNavigationStateInput): ModerationOpenTarget | null {
  const navigationStatus = input.navigationStatus ?? 'Ready';
  if (navigationStatus === 'Unavailable' || navigationStatus === 'Unsupported') {
    return null;
  }

  const targetType = input.targetType ?? null;
  if (canOpenChatTarget(targetType, input.targetChannelId, input.targetMessageId)) {
    return {
      label: '打开聊天定位',
      url: buildDesktopChatTargetUrl(input.targetChannelId!, input.targetMessageId!),
    };
  }

  if (targetType === 'Post' && hasPositiveLongId(input.targetPostId)) {
    return {
      label: '打开帖子详情',
      url: buildPublicForumTargetUrl(input.targetPostId!),
    };
  }

  if (targetType === 'Comment' && hasPositiveLongId(input.targetPostId)) {
    const targetCommentId = input.targetCommentId ?? input.targetContentId;
    const isFallback = navigationStatus === 'Fallback';
    return {
      label: isFallback ? '打开所属帖子' : '打开评论定位',
      url: buildPublicForumTargetUrl(input.targetPostId!, isFallback ? undefined : targetCommentId),
    };
  }

  if (targetType === 'PostQuickReply' && hasPositiveLongId(input.targetPostId)) {
    return {
      label: '打开所属帖子',
      url: buildPublicForumTargetUrl(input.targetPostId!),
    };
  }

  if (targetType === 'Product' && hasPositiveLongId(input.targetContentId)) {
    return {
      label: '打开商品详情',
      url: buildPublicShopTargetUrl(input.targetContentId!),
    };
  }

  return null;
}

export function resolveMissingTargetMessage(targetType: string | null | undefined, navigationMessage?: string | null): string {
  if (navigationMessage && navigationMessage.trim().length > 0) {
    return navigationMessage;
  }

  switch (targetType) {
    case 'ChatMessage':
      return '当前举报项缺少聊天定位信息';
    case 'Post':
    case 'Comment':
    case 'PostQuickReply':
      return '当前举报项缺少论坛定位信息';
    case 'Product':
      return '当前举报项缺少商品定位信息';
    default:
      return '当前目标类型未接入直接回看，请结合创建时快照、举报原因和用户治理记录复核。';
  }
}
