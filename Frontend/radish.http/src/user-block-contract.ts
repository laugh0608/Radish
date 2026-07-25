export interface UserBlockMutationRequest {
  targetUserPublicId: string;
  operationKey: string;
}

export interface DirectConversationBlockMutationRequest {
  operationKey: string;
}

export interface UserInteractionCapabilityVo {
  voCanFollow: boolean;
  voCanDirectMessage: boolean;
  voCanInteract: boolean;
  voInteractionUnavailable: boolean;
  voIsBlockedByCurrentUser: boolean;
}

export interface UserBlockMutationVo {
  voTargetUserPublicId: string;
  voRelationshipVersion: string;
  voChanged: boolean;
  voCapabilities: UserInteractionCapabilityVo;
}

export interface UserBlockListItemVo {
  voTargetUserPublicId: string;
  voTargetDisplayName: string;
  voTargetAvatarUrl?: string | null;
  voBlockedAtUtc: string;
  voCanUnblock: boolean;
}

export interface UserBlockPageVo {
  voItems: UserBlockListItemVo[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export interface UserInteractionChangedVo {
  voRelationshipVersion: string;
}
