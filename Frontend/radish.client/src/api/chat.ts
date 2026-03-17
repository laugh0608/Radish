import { apiDelete, apiGet, apiPost, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type {
  ChannelMemberVo,
  ChannelMessageVo,
  ChannelVo,
  EntityIdValue,
  SendChannelMessageRequest,
} from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

const CHAT_READ_TIMEOUT_MS = 30_000;

export async function getChannelList(): Promise<ChannelVo[]> {
  const response = await apiGet<ChannelVo[]>('/api/v1/Channel/GetList', {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载频道列表失败');
  }

  return response.data;
}

export async function getChannelHistory(
  channelId: EntityIdValue,
  beforeMessageId?: EntityIdValue,
  pageSize: number = 50
): Promise<ChannelMessageVo[]> {
  const normalizedChannelId = normalizeEntityId(channelId);
  if (!normalizedChannelId) {
    throw new Error('频道 Id 无效');
  }

  const params = new URLSearchParams();
  params.set('channelId', normalizedChannelId);
  params.set('pageSize', String(pageSize));
  const normalizedBeforeMessageId = normalizeEntityId(beforeMessageId);
  if (normalizedBeforeMessageId && normalizedBeforeMessageId !== '0' && !normalizedBeforeMessageId.startsWith('-')) {
    params.set('beforeMessageId', normalizedBeforeMessageId);
  }

  const response = await apiGet<ChannelMessageVo[]>(`/api/v1/ChannelMessage/GetHistory?${params.toString()}`, {
    withAuth: true,
    timeout: CHAT_READ_TIMEOUT_MS,
  });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载历史消息失败');
  }

  return response.data;
}

export async function sendChannelMessage(request: SendChannelMessageRequest): Promise<ChannelMessageVo> {
  const response = await apiPost<ChannelMessageVo>('/api/v1/ChannelMessage/Send', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '发送消息失败');
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
    throw new Error(response.message || '撤回消息失败');
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
    throw new Error(response.message || '加载在线成员失败');
  }

  return response.data;
}
