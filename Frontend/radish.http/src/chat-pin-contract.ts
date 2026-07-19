export interface ChatPinnedMessageVo {
  voId: string;
  voClientRequestId?: string | null;
  voChannelId: string;
  voUserId: string;
  voUserName: string;
  voUserAvatarAttachmentId?: string | null;
  voUserAvatarUrl?: string | null;
  voType: 1 | 2 | 3;
  voContent?: string | null;
  voReplyToId?: string | null;
  voReplyTo?: ChatPinnedMessageVo | null;
  voAttachmentId?: string | null;
  voImageUrl?: string | null;
  voImageThumbnailUrl?: string | null;
  voIsRecalled: boolean;
  voCreateTime: string;
}

export interface ChatMessagePinVo {
  voId: string;
  voMessageId: string;
  voMessage: ChatPinnedMessageVo;
  voPinnedByUserId: string;
  voPinnedByName: string;
  voPinnedAt: string;
}

export interface ChatMessagePinStateVo {
  voChannelId: string;
  voRevision: string;
  voItems: ChatMessagePinVo[];
}

export interface SetChatMessagePinDto {
  channelId: string;
  messageId: string;
  isPinned: boolean;
}

export interface ChatMessagePinMutationVo {
  voState: ChatMessagePinStateVo;
  voChanged: boolean;
}
