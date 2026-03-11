export type ChatMessageStatus = 'sending' | 'sent' | 'failed';

export interface ChannelMessageVo {
  voId: number;
  voClientRequestId?: string | null;
  voChannelId: number;
  voUserId: number;
  voUserName: string;
  voUserAvatarUrl?: string | null;
  voType: 1 | 2 | 3;
  voContent?: string | null;
  voReplyToId?: number | null;
  voReplyTo?: ChannelMessageVo | null;
  voAttachmentId?: number | null;
  voImageUrl?: string | null;
  voImageThumbnailUrl?: string | null;
  voIsRecalled: boolean;
  voCreateTime: string;
  voLocalStatus?: ChatMessageStatus;
  voLocalError?: string | null;
}

export interface ChannelVo {
  voId: number;
  voCategoryId?: number | null;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIconEmoji?: string | null;
  voType: 1 | 2 | 3;
  voSort: number;
  voUnreadCount: number;
  voHasMention: boolean;
  voLastMessage?: ChannelMessageVo | null;
}

export interface ChannelMemberVo {
  voUserId: number;
  voUserName: string;
  voUserAvatarUrl?: string | null;
  voIsOnline: boolean;
}

export interface SendChannelMessageRequest {
  clientRequestId?: string;
  channelId: number;
  type?: 1 | 2 | 3;
  content?: string;
  replyToId?: number;
  attachmentId?: number;
  imageUrl?: string;
  imageThumbnailUrl?: string;
}

export interface ChannelUnreadChangedPayload {
  channelId: number;
  unreadCount: number;
  hasMention: boolean;
}

export interface MessageRecalledPayload {
  channelId: number;
  messageId: number;
}

export interface UserTypingPayload {
  channelId: number;
  userId: number;
  userName: string;
}
