import {
  apiDelete,
  apiGet,
  apiPost,
  apiPut,
  configureApiClient,
  createApiResponseError,
  type ParsedApiResponse,
  type ChannelMessageSearchPageVo,
  type SearchChannelMessagesDto,
  type ChatMessageReactionMutationVo,
  type ChatMessageReactionStateVo,
  type ChatMessagePinMutationVo,
  type ChatMessagePinStateVo,
  type SetChatMessagePinDto,
  type SetChatMessageReactionDto,
} from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type {
  ChannelMemberVo,
  ChannelMessageVo,
  ChannelMessageWindowVo,
  ChannelVo,
  ChatChannelListView,
  DirectConversationVo,
  EntityIdValue,
  SendChannelMessageRequest,
} from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

const CHAT_READ_TIMEOUT_MS = 30_000;

function createChatApiError<T>(response: ParsedApiResponse<T>, fallbackMessage: string): Error {
  return createApiResponseError(
    response.messageKey ? response : { ...response, message: undefined },
    fallbackMessage
  );
}

export interface ChannelHistoryQuery {
  beforeMessageId?: EntityIdValue;
  afterMessageId?: EntityIdValue;
  pageSize?: number;
}

export async function getChannelList(view: ChatChannelListView = 'active'): Promise<ChannelVo[]> {
  const response = await apiGet<ChannelVo[]>(`/api/v1/Channel/GetList?view=${view}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载频道列表失败');
  }

  return response.data;
}

export async function getChannelDetail(channelId: EntityIdValue): Promise<ChannelVo> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const response = await apiGet<ChannelVo>(`/api/v1/Channel/GetDetail/${normalizedChannelId}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载频道详情失败');
  }

  return response.data;
}

export async function getOrCreateDirectConversation(targetUserId: EntityIdValue): Promise<DirectConversationVo> {
  const normalizedTargetUserId = normalizeEntityId(targetUserId);
  if (!normalizedTargetUserId) {
    throw new Error('目标用户 Id 无效');
  }

  const response = await apiPost<DirectConversationVo>('/api/v1/DirectConversation/GetOrCreate', {
    targetUserId: normalizedTargetUserId,
  }, { withAuth: true });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '创建私聊失败');
  }

  return response.data;
}

async function mutateDirectConversation(
  action: 'Accept' | 'Decline' | 'Block' | 'Unblock',
  channelId: EntityIdValue
): Promise<DirectConversationVo> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const response = await apiPost<DirectConversationVo>(
    `/api/v1/DirectConversation/${action}/${normalizedChannelId}`,
    undefined,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '更新私聊状态失败');
  }

  return response.data;
}

export const acceptDirectConversation = (channelId: EntityIdValue) => mutateDirectConversation('Accept', channelId);
export const declineDirectConversation = (channelId: EntityIdValue) => mutateDirectConversation('Decline', channelId);
export const blockDirectConversation = (channelId: EntityIdValue) => mutateDirectConversation('Block', channelId);
export const unblockDirectConversation = (channelId: EntityIdValue) => mutateDirectConversation('Unblock', channelId);

export async function setDirectConversationArchived(
  channelId: EntityIdValue,
  archived: boolean
): Promise<DirectConversationVo> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const response = await apiPut<DirectConversationVo>(
    `/api/v1/DirectConversation/SetArchived/${normalizedChannelId}`,
    { archived },
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '更新归档状态失败');
  }

  return response.data;
}

export async function getChannelHistory(
  channelId: EntityIdValue,
  query: ChannelHistoryQuery = {}
): Promise<ChannelMessageVo[]> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const normalizedBeforeMessageId = normalizeEntityId(query.beforeMessageId);
  const normalizedAfterMessageId = normalizeEntityId(query.afterMessageId);
  if (normalizedBeforeMessageId && normalizedAfterMessageId) {
    throw new Error('beforeMessageId 和 afterMessageId 不能同时传入');
  }

  const params = new URLSearchParams();
  params.set('channelId', normalizedChannelId);
  params.set('pageSize', String(query.pageSize ?? 50));
  if (normalizedBeforeMessageId && normalizedBeforeMessageId !== '0' && !normalizedBeforeMessageId.startsWith('-')) {
    params.set('beforeMessageId', normalizedBeforeMessageId);
  }
  if (normalizedAfterMessageId && normalizedAfterMessageId !== '0' && !normalizedAfterMessageId.startsWith('-')) {
    params.set('afterMessageId', normalizedAfterMessageId);
  }

  const response = await apiGet<ChannelMessageVo[]>(`/api/v1/ChannelMessage/GetHistory?${params.toString()}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载历史消息失败');
  }

  return response.data;
}

export async function getChannelMessageWindow(
  channelId: EntityIdValue,
  messageId: EntityIdValue,
  beforeCount: number = 25,
  afterCount: number = 25
): Promise<ChannelMessageWindowVo> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const normalizedMessageId = normalizeEntityId(messageId);
  if (!normalizedMessageId || normalizedMessageId === '0' || normalizedMessageId.startsWith('-')) {
    throw new Error('消息 Id 无效');
  }

  const params = new URLSearchParams();
  params.set('channelId', normalizedChannelId);
  params.set('messageId', normalizedMessageId);
  params.set('beforeCount', String(beforeCount));
  params.set('afterCount', String(afterCount));

  const response = await apiGet<ChannelMessageWindowVo>(`/api/v1/ChannelMessage/GetMessageWindow?${params.toString()}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载目标消息窗口失败');
  }

  return response.data;
}

export async function searchChannelMessages(
  request: SearchChannelMessagesDto
): Promise<ChannelMessageSearchPageVo> {
  const response = await apiPost<ChannelMessageSearchPageVo>(
    '/api/v1/ChannelMessage/Search',
    request,
    {
      withAuth: true,
      timeout: CHAT_READ_TIMEOUT_MS,
    }
  );

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '搜索消息失败');
  }

  return response.data;
}

export async function getChatMessageReactionStates(
  channelId: EntityIdValue,
  messageIds: EntityIdValue[]
): Promise<ChatMessageReactionStateVo[]> {
  const normalizedChannelId = normalizeEntityId(channelId);
  const normalizedMessageIds = messageIds
    .map((messageId) => normalizeEntityId(messageId))
    .filter((messageId): messageId is string => Boolean(messageId && !messageId.startsWith('-')));
  if (!normalizedChannelId || normalizedMessageIds.length === 0 || normalizedMessageIds.length !== messageIds.length) {
    throw new Error('消息回应目标无效');
  }

  const response = await apiPost<ChatMessageReactionStateVo[]>(
    '/api/v1/ChannelMessageReaction/GetStates',
    { channelId: normalizedChannelId, messageIds: normalizedMessageIds },
    { withAuth: true, timeout: CHAT_READ_TIMEOUT_MS }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载消息回应失败');
  }

  return response.data;
}

export async function setChatMessageReaction(
  request: SetChatMessageReactionDto
): Promise<ChatMessageReactionMutationVo> {
  const response = await apiPost<ChatMessageReactionMutationVo>(
    '/api/v1/ChannelMessageReaction/Set',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '更新消息回应失败');
  }

  return response.data;
}

export async function getChatMessagePinState(
  channelId: EntityIdValue
): Promise<ChatMessagePinStateVo> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('消息置顶频道无效');
  }

  const response = await apiGet<ChatMessagePinStateVo>(
    `/api/v1/ChannelMessagePin/GetState?channelId=${encodeURIComponent(normalizedChannelId)}`,
    { withAuth: true, timeout: CHAT_READ_TIMEOUT_MS }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载消息置顶失败');
  }

  return response.data;
}

export async function setChatMessagePin(
  request: SetChatMessagePinDto
): Promise<ChatMessagePinMutationVo> {
  const response = await apiPost<ChatMessagePinMutationVo>(
    '/api/v1/ChannelMessagePin/Set',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createChatApiError(response, '更新消息置顶失败');
  }

  return response.data;
}

export async function sendChannelMessage(request: SendChannelMessageRequest): Promise<ChannelMessageVo> {
  const response = await apiPost<ChannelMessageVo>('/api/v1/ChannelMessage/Send', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '发送消息失败');
  }

  return response.data;
}

export async function recallChannelMessage(messageId: EntityIdValue): Promise<boolean> {
  const normalizedMessageId = normalizeEntityId(messageId);
  if (!normalizedMessageId) {
    throw new Error('消息 Id 无效');
  }

  const response = await apiDelete<boolean>(`/api/v1/ChannelMessage/Recall/${normalizedMessageId}`, { withAuth: true });

  if (!response.ok) {
    throw createChatApiError(response, '撤回消息失败');
  }

  return response.data ?? true;
}

export async function getChannelOnlineMembers(channelId: EntityIdValue): Promise<ChannelMemberVo[]> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const response = await apiGet<ChannelMemberVo[]>(`/api/v1/Channel/GetOnlineMembers/${normalizedChannelId}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw createChatApiError(response, '加载在线成员失败');
  }

  return response.data;
}
