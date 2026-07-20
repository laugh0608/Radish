import {
  type ConsoleLongId,
  type ContentReportQueueItemVo,
  type UserModerationActionVo,
} from '@/api/moderationApi';
import { getApiBaseUrl } from '@/config/env';
import type { TFunction } from 'i18next';

export const getReviewStatusOptions = (t: TFunction) => [
  { label: t('moderation.reviewStatus.all'), value: -1 },
  { label: t('moderation.reviewStatus.pending'), value: 0 },
  { label: t('moderation.reviewStatus.approved'), value: 1 },
  { label: t('moderation.reviewStatus.rejected'), value: 2 },
];

export const getTargetTypeOptions = (t: TFunction) => [
  { label: t('moderation.target.post'), value: 'Post' },
  { label: t('moderation.target.comment'), value: 'Comment' },
  { label: t('moderation.target.quickReply'), value: 'PostQuickReply' },
  { label: t('moderation.target.chatMessage'), value: 'ChatMessage' },
  { label: t('moderation.target.product'), value: 'Product' },
];

export const getReasonTypeOptions = (t: TFunction) => [
  { label: t('moderation.reason.spam'), value: 'Spam' },
  { label: t('moderation.reason.abuse'), value: 'Abuse' },
  { label: t('moderation.reason.pornography'), value: 'Pornography' },
  { label: t('moderation.reason.illegal'), value: 'Illegal' },
  { label: t('moderation.reason.fraud'), value: 'Fraud' },
  { label: t('moderation.reason.other'), value: 'Other' },
];

export const getTargetNavigationStatusOptions = (t: TFunction) => [
  { label: t('moderation.navigation.ready'), value: 'Ready' },
  { label: t('moderation.navigation.fallback'), value: 'Fallback' },
  { label: t('moderation.navigation.unavailable'), value: 'Unavailable' },
  { label: t('moderation.navigation.unsupported'), value: 'Unsupported' },
];

export const getActionOptions = (t: TFunction) => [
  { label: t('moderation.action.none'), value: 0 },
  { label: t('moderation.action.mute'), value: 1 },
  { label: t('moderation.action.ban'), value: 2 },
];

export const MANUAL_ACTION_TYPE = {
  mute: 1,
  ban: 2,
  unmute: 3,
  unban: 4,
} as const;

export type ManualActionTypeValue = typeof MANUAL_ACTION_TYPE[keyof typeof MANUAL_ACTION_TYPE];

export const getManualActionOptions = (t: TFunction) => [
  { label: t('moderation.action.mute'), value: MANUAL_ACTION_TYPE.mute },
  { label: t('moderation.action.ban'), value: MANUAL_ACTION_TYPE.ban },
  { label: t('moderation.action.unmute'), value: MANUAL_ACTION_TYPE.unmute },
  { label: t('moderation.action.unban'), value: MANUAL_ACTION_TYPE.unban },
];

export const getActionLogActionTypeOptions = (t: TFunction) => [
  { label: t('moderation.action.mute'), value: 'Mute' },
  { label: t('moderation.action.ban'), value: 'Ban' },
  { label: t('moderation.action.unmute'), value: 'Unmute' },
  { label: t('moderation.action.unban'), value: 'Unban' },
];

export const getActionLogStatusOptions = (t: TFunction) => [
  { label: t('moderation.action.active'), value: 'active' },
  { label: t('moderation.action.inactive'), value: 'inactive' },
];

const TARGET_TYPE_KEY_MAP: Record<string, string> = {
  Post: 'moderation.target.post',
  Comment: 'moderation.target.comment',
  PostQuickReply: 'moderation.target.quickReply',
  ChatMessage: 'moderation.target.chatMessage',
  Product: 'moderation.target.product',
  Unknown: 'moderation.target.unknown',
};

const REASON_TYPE_KEY_MAP: Record<string, string> = {
  Spam: 'moderation.reason.spam',
  Abuse: 'moderation.reason.abuse',
  Pornography: 'moderation.reason.pornography',
  Illegal: 'moderation.reason.illegal',
  Fraud: 'moderation.reason.fraud',
  Other: 'moderation.reason.other',
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

export function getReasonTypeLabel(value: string | null | undefined, t: TFunction): string {
  return value ? t(REASON_TYPE_KEY_MAP[value] ?? value) : t('moderation.target.notCategorized');
}

export function getTargetTypeLabel(value: string | null | undefined, t: TFunction): string {
  return value ? t(TARGET_TYPE_KEY_MAP[value] ?? value) : t('moderation.target.unknown');
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

export function getActionTypeText(value: string | null | undefined, t: TFunction): string {
  switch (value) {
    case 'Mute':
      return t('moderation.action.mute');
    case 'Ban':
      return t('moderation.action.ban');
    case 'Unmute':
      return t('moderation.action.unmute');
    case 'Unban':
      return t('moderation.action.unban');
    default:
      return t('moderation.action.generic');
  }
}

export function getManualActionTypeText(value: ManualActionTypeValue | null | undefined, t: TFunction): string {
  switch (value) {
    case MANUAL_ACTION_TYPE.mute:
      return t('moderation.action.mute');
    case MANUAL_ACTION_TYPE.ban:
      return t('moderation.action.ban');
    case MANUAL_ACTION_TYPE.unmute:
      return t('moderation.action.unmute');
    case MANUAL_ACTION_TYPE.unban:
      return t('moderation.action.unban');
    default:
      return t('moderation.action.generic');
  }
}

function buildPublicMessagesTargetUrl(channelId: ConsoleLongId, messageId: ConsoleLongId): string {
  const url = new URL('/messages', getApiBaseUrl());
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

export function resolveNavigationStatusLabel(status: string | null | undefined, t: TFunction): { color: string; label: string } {
  switch (status) {
    case 'Fallback':
      return { color: 'warning', label: t('moderation.navigation.fallback') };
    case 'Unavailable':
      return { color: 'default', label: t('moderation.navigation.unavailable') };
    case 'Unsupported':
      return { color: 'default', label: t('moderation.navigation.unsupported') };
    default:
      return { color: 'success', label: t('moderation.navigation.ready') };
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

export function buildQueueManualActionReason(record: ContentReportQueueItemVo, t: TFunction): string {
  const reasonLabel = getReasonTypeLabel(record.voReasonType, t);
  const reasonDetail = record.voReasonDetail?.trim();
  return reasonDetail
    ? t('moderation.reason.fromReportWithDetail', { reportId: record.voReportId, reason: reasonLabel, detail: reasonDetail })
    : t('moderation.reason.fromReport', { reportId: record.voReportId, reason: reasonLabel });
}

export function buildActionLogManualActionReason(record: UserModerationActionVo, t: TFunction, actionType?: ManualActionTypeValue): string {
  if (actionType === MANUAL_ACTION_TYPE.unmute || actionType === MANUAL_ACTION_TYPE.unban) {
    return t('moderation.reason.fromActionUndo', {
      actionId: record.voActionId,
      action: getManualActionTypeText(actionType, t),
    });
  }

  return t('moderation.reason.fromAction', { actionId: record.voActionId, userId: record.voTargetUserId });
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

export function resolveOpenTarget(input: ModerationTargetNavigationStateInput, t: TFunction): ModerationOpenTarget | null {
  const navigationStatus = input.navigationStatus ?? 'Ready';
  if (navigationStatus === 'Unavailable' || navigationStatus === 'Unsupported') {
    return null;
  }

  const targetType = input.targetType ?? null;
  if (canOpenChatTarget(targetType, input.targetChannelId, input.targetMessageId)) {
    return {
      label: t('moderation.open.chat'),
      url: buildPublicMessagesTargetUrl(input.targetChannelId!, input.targetMessageId!),
    };
  }

  if (targetType === 'Post' && hasPositiveLongId(input.targetPostId)) {
    return {
      label: t('moderation.open.post'),
      url: buildPublicForumTargetUrl(input.targetPostId!),
    };
  }

  if (targetType === 'Comment' && hasPositiveLongId(input.targetPostId)) {
    const targetCommentId = input.targetCommentId ?? input.targetContentId;
    const isFallback = navigationStatus === 'Fallback';
    return {
      label: t(isFallback ? 'moderation.open.parentPost' : 'moderation.open.comment'),
      url: buildPublicForumTargetUrl(input.targetPostId!, isFallback ? undefined : targetCommentId),
    };
  }

  if (targetType === 'PostQuickReply' && hasPositiveLongId(input.targetPostId)) {
    return {
      label: t('moderation.open.parentPost'),
      url: buildPublicForumTargetUrl(input.targetPostId!),
    };
  }

  if (targetType === 'Product' && hasPositiveLongId(input.targetContentId)) {
    return {
      label: t('moderation.open.product'),
      url: buildPublicShopTargetUrl(input.targetContentId!),
    };
  }

  return null;
}

export function resolveMissingTargetMessage(
  targetType: string | null | undefined,
  navigationMessage: string | null | undefined,
  t: TFunction,
): string {
  if (navigationMessage && navigationMessage.trim().length > 0) {
    return navigationMessage;
  }

  switch (targetType) {
    case 'ChatMessage':
      return t('moderation.missing.chat');
    case 'Post':
    case 'Comment':
    case 'PostQuickReply':
      return t('moderation.missing.forum');
    case 'Product':
      return t('moderation.missing.product');
    default:
      return t('moderation.missing.unsupported');
  }
}
