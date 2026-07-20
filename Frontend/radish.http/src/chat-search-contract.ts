export const ChatMessageSearchScopes = {
  CurrentChannel: 1,
  AllVisibleChannels: 2,
} as const;

export type ChatMessageSearchScope = (typeof ChatMessageSearchScopes)[keyof typeof ChatMessageSearchScopes];

export interface SearchChannelMessagesDto {
  scope: ChatMessageSearchScope;
  channelId?: string | null;
  keyword: string;
  fromUtc?: string | null;
  toUtc?: string | null;
  cursor?: string | null;
  pageSize?: number;
}

export interface ChannelMessageSearchItemVo {
  voChannelId: string;
  voMessageId: string;
  voChannelDisplayName: string;
  voChannelIcon?: string | null;
  voConversationKind: 'public' | 'group' | 'mutual' | 'stranger';
  voPeerUserId?: string | null;
  voPeerPublicId?: string | null;
  voPeerAvatarUrl?: string | null;
  voSenderUserId: string;
  voSenderDisplayName: string;
  voSenderAvatarUrl?: string | null;
  voSnippet: string;
  voCreateTime: string;
  voMessageType: 1 | 2;
}

export interface ChannelMessageSearchPageVo {
  voItems: ChannelMessageSearchItemVo[];
  voNextCursor?: string | null;
  voHasMore: boolean;
}
