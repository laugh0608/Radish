export type ChatReactionEmojiType = 'unicode' | 'sticker';

export interface ChatReactionSummaryVo {
  voEmojiType: ChatReactionEmojiType;
  voEmojiValue: string;
  voCount: number;
  voIsReacted: boolean;
  voThumbnailUrl?: string | null;
}

export interface ChatMessageReactionStateVo {
  voMessageId: string;
  voRevision: string;
  voItems: ChatReactionSummaryVo[];
}

export interface GetChatMessageReactionStatesDto {
  channelId: string;
  messageIds: string[];
}

export interface SetChatMessageReactionDto {
  channelId: string;
  messageId: string;
  emojiType: ChatReactionEmojiType;
  emojiValue: string;
  isActive: boolean;
  clientOperationId: string;
}

export interface ChatMessageReactionMutationVo {
  voState: ChatMessageReactionStateVo;
  voChanged: boolean;
  voReplayed: boolean;
}
