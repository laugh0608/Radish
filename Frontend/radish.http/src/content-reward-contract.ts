export type ContentRewardLongId = string | number;

export type ContentRewardTargetType = 'Post' | 'Comment';

export type ContentRewardReasonCode =
  | 'Helpful'
  | 'Insightful'
  | 'WellWritten'
  | 'Detailed'
  | 'Warm';

export const ContentRewardTargetTypes = {
  Post: 'Post',
  Comment: 'Comment',
} as const satisfies Record<string, ContentRewardTargetType>;

export const ContentRewardReasonCodes = {
  Helpful: 'Helpful',
  Insightful: 'Insightful',
  WellWritten: 'WellWritten',
  Detailed: 'Detailed',
  Warm: 'Warm',
} as const satisfies Record<string, ContentRewardReasonCode>;

export const ContentRewardErrorCode = {
  InvalidArgument: 'ContentReward.InvalidArgument',
  Unavailable: 'ContentReward.Unavailable',
  TargetUnavailable: 'ContentReward.TargetUnavailable',
  SelfNotAllowed: 'ContentReward.SelfNotAllowed',
  AlreadyRewarded: 'ContentReward.AlreadyRewarded',
  InsufficientBalance: 'ContentReward.InsufficientBalance',
  DailyLimitExceeded: 'ContentReward.DailyLimitExceeded',
  AccountUnavailable: 'ContentReward.AccountUnavailable',
  Processing: 'ContentReward.Processing',
  IdempotencyConflict: 'ContentReward.IdempotencyConflict',
  ReplayUnavailable: 'ContentReward.ReplayUnavailable',
  ConcurrentConflict: 'ContentReward.ConcurrentConflict',
  InteractionUnavailable: 'UserBlock.InteractionUnavailable',
  RelationshipTemporarilyUnavailable: 'UserBlock.RelationshipTemporarilyUnavailable',
} as const;

export type ContentRewardErrorCodeValue =
  typeof ContentRewardErrorCode[keyof typeof ContentRewardErrorCode];

export interface CreateContentRewardRequest {
  targetType: ContentRewardTargetType;
  targetId: ContentRewardLongId;
  reasonCode: ContentRewardReasonCode;
  idempotencyKey: string;
}

export interface ContentRewardTargetRequest {
  targetType: ContentRewardTargetType;
  targetId: ContentRewardLongId;
}

export interface GetContentRewardTargetStatesRequest {
  targets: ContentRewardTargetRequest[];
}

export interface ContentRewardMutationVo {
  voRewardId: ContentRewardLongId;
  voTargetType: ContentRewardTargetType;
  voTargetId: ContentRewardLongId;
  voReasonCode: ContentRewardReasonCode;
  voTotalCount: ContentRewardLongId;
  voViewerRewarded: boolean;
  voSenderAvailableBalance: ContentRewardLongId;
  voTransactionNo: string;
}

export interface ContentRewardTargetStateVo {
  voTargetType: ContentRewardTargetType;
  voTargetId: ContentRewardLongId;
  voTotalCount: ContentRewardLongId;
  voViewerRewarded: boolean;
  voCreateEnabled: boolean;
}

export interface ContentRewardRecordVo {
  voRewardId: ContentRewardLongId;
  voSenderPublicId?: string | null;
  voSenderDisplayName: string;
  voSenderAvatarUrl?: string | null;
  voReasonCode: ContentRewardReasonCode;
  voCreateTime: string;
}

export interface ContentRewardTargetPageVo extends ContentRewardTargetStateVo {
  voItems: ContentRewardRecordVo[];
  voPageIndex: number;
  voPageSize: number;
}
