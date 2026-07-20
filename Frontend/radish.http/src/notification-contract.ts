export type NotificationCategory =
  | 'Discussion'
  | 'Reaction'
  | 'Message'
  | 'Relationship'
  | 'Commerce'
  | 'Growth'
  | 'Governance'
  | 'Knowledge'
  | 'System';

export type NotificationTargetKind =
  | 'None'
  | 'ForumPost'
  | 'ChatConversation'
  | 'UserProfile'
  | 'ShopOrder'
  | 'Inventory'
  | 'Experience'
  | 'DocsDocument'
  | 'GovernanceCase';

export interface NotificationTargetVo {
  voKind: NotificationTargetKind;
  voPostId: string | null;
  voPostPublicId: string | null;
  voCommentId: string | null;
  voChannelId: string | null;
  voMessageId: string | null;
  voUserId: string | null;
  voUserPublicId: string | null;
  voOrderId: string | null;
  voBenefitId: string | null;
  voDocumentSlug: string | null;
  voGovernanceCaseId: string | null;
  voUnavailableReason: string | null;
}

export interface NotificationInboxSummaryVo {
  voRevision: string;
  voUnreadGroupCount: string;
  voUnreadOccurrenceCount: string;
  voUnreadGroupCountByCategory: Partial<Record<NotificationCategory, string>>;
  voLastChangedAtUtc: string;
}

export interface NotificationInboxGroupVo {
  voGroupId: string;
  voLatestNotificationId: string;
  voCategory: NotificationCategory;
  voKind: string;
  voTitle: string;
  voContent: string;
  voPriority: number;
  voOccurrenceCount: string;
  voUnreadOccurrenceCount: string;
  voDistinctTriggerCount: string;
  voIsRead: boolean;
  voFirstOccurredAtUtc: string;
  voLastOccurredAtUtc: string;
  voTriggerId: string | null;
  voTriggerName: string | null;
  voTriggerAvatar: string | null;
  voTarget: NotificationTargetVo;
}

export interface NotificationInboxPageVo {
  voItems: NotificationInboxGroupVo[];
  voNextCursor: string | null;
  voSummary: NotificationInboxSummaryVo;
}

export interface NotificationInboxMutationVo {
  voAffectedRows: number;
  voSummary: NotificationInboxSummaryVo;
}

export interface NotificationPreferenceVo {
  voCategory: NotificationCategory;
  voInAppEnabled: boolean;
  voRealtimePreviewEnabled: boolean;
  voCanDisableInApp: boolean;
  voCanDisableRealtimePreview: boolean;
  voSupportedKinds: string[];
}

export interface UpdateNotificationPreferenceDto {
  category: NotificationCategory;
  inAppEnabled: boolean;
  realtimePreviewEnabled: boolean;
}

export interface NotificationInboxChangedVo {
  voRevision: string;
  voUnreadGroupCount: string;
  voUnreadOccurrenceCount: string;
  voReason: 'Connected' | 'Created' | 'Read' | 'ReadAll' | 'Deleted' | string;
  voLatestGroupId: string | null;
  voRealtimePreviewAllowed: boolean;
}
