export const PublicDiscoverItemKinds = {
  ChannelSummary: 1,
  MemberActivity: 2,
  HighlightedComment: 3,
  Post: 4,
  Question: 5,
} as const;

export type PublicDiscoverItemKind =
  typeof PublicDiscoverItemKinds[keyof typeof PublicDiscoverItemKinds];

export const PublicDiscoverTargetKinds = {
  Messages: 1,
  Docs: 2,
  ForumPost: 3,
} as const;

export type PublicDiscoverTargetKind =
  typeof PublicDiscoverTargetKinds[keyof typeof PublicDiscoverTargetKinds];

export const PublicDiscoverMetricKinds = {
  RecentReplies: 1,
  Likes: 2,
  Comments: 3,
  Answers: 4,
} as const;

export type PublicDiscoverMetricKind =
  typeof PublicDiscoverMetricKinds[keyof typeof PublicDiscoverMetricKinds];

export interface PublicDiscoverActorVo {
  voPublicId: string;
  voDisplayName: string;
  voAvatarThumbnailUrl?: string | null;
}

export interface PublicDiscoverTargetVo {
  voKind: PublicDiscoverTargetKind;
  voChannelId?: string | null;
  voDocumentSlug?: string | null;
  voPostPublicId?: string | null;
  voCommentId?: string | null;
  voRequiresAuthentication: boolean;
}

export interface PublicDiscoverMetricVo {
  voKind: PublicDiscoverMetricKind;
  voValue: string;
}

export interface PublicDiscoverItemVo {
  voKey: string;
  voKind: PublicDiscoverItemKind;
  voOccurredAtUtc: string;
  voTitle: string;
  voSummary: string;
  voActor?: PublicDiscoverActorVo | null;
  voTarget: PublicDiscoverTargetVo;
  voPrimaryMetric?: PublicDiscoverMetricVo | null;
}

export interface PublicDiscoverPulseVo {
  voWindowStartedAtUtc: string;
  voWindowEndedAtUtc: string;
  voDiscoverableChannelCount: string;
  voEligibleItemCount: string;
  voKnowledgeContributionCount: string;
}

export interface PublicDiscoverFeedVo {
  voItems: PublicDiscoverItemVo[];
  voPulse: PublicDiscoverPulseVo;
  voNextCursor?: string | null;
  voHasMore: boolean;
  voGeneratedAtUtc: string;
}

export interface GetPublicDiscoverFeedRequest {
  cursor?: string | null;
  pageSize?: number;
}
