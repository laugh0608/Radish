export const ChatReadReceiptModes = {
  none: 'none',
  privateGroup: 'private_group',
  direct: 'direct',
} as const;

export type ChatReadReceiptMode =
  (typeof ChatReadReceiptModes)[keyof typeof ChatReadReceiptModes];

export interface AdvanceChannelReadStateDto {
  channelId: string;
  readThroughMessageId: string;
}

export interface ChannelReadStateVo {
  voChannelId: string;
  voLastReadMessageId: string;
  voUnreadCount: number;
  voHasMention: boolean;
  voChanged: boolean;
}

export interface GetChatReadReceiptSummariesDto {
  channelId: string;
  messageIds: string[];
}

export interface ChatReadReceiptSummaryItemVo {
  voMessageId: string;
  voReadCount?: number | null;
  voPeerHasRead?: boolean | null;
}

export interface ChatReadReceiptSummariesVo {
  voChannelId: string;
  voMode: ChatReadReceiptMode;
  voItems: ChatReadReceiptSummaryItemVo[];
}

export interface ChatReadReceiptReaderVo {
  voUserId: string;
  voPublicId?: string | null;
  voPublicIndex?: string | null;
  voDisplayName: string;
  voAvatarAttachmentId?: string | null;
  voAvatarUrl?: string | null;
}

export interface GetChatReadReceiptReadersQuery {
  channelId: string;
  messageId: string;
  cursor?: string | null;
  pageSize?: number;
}

export interface ChatReadReceiptReaderPageVo {
  voChannelId: string;
  voMessageId: string;
  voItems: ChatReadReceiptReaderVo[];
  voNextCursor?: string | null;
  voHasMore: boolean;
}

export interface ReadReceiptsChangedVo {
  channelId: string;
}
