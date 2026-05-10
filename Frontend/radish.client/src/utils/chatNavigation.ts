import {
  areEntityIdsEqual,
  compareEntityIds,
  isPersistedEntityId,
  type ChannelMessageVo,
  type EntityIdValue,
} from '../types/chat.ts';

export interface ChatNavigationTarget {
  channelId: string;
  messageId?: string;
}

export interface ChatWindowParams {
  channelId?: string;
  messageId?: string;
  navigationKey?: string;
}

export interface ChatAppParamTarget {
  channelId: string | number;
  messageId?: string | number;
}

export type ChatMessageNavigationAction = 'found' | 'load-more' | 'not-found';

function normalizePositiveIntegerString(value: unknown): string | undefined {
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || value <= 0) {
      return undefined;
    }

    return String(value);
  }

  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = value.trim();
  if (!/^\d+$/.test(trimmed)) {
    return undefined;
  }

  const normalized = trimmed.replace(/^0+/, '');
  return normalized || undefined;
}

export function buildChatAppParams(target: ChatAppParamTarget): Record<string, unknown> {
  const channelId = normalizePositiveIntegerString(target.channelId);
  if (!channelId) {
    return {};
  }

  const messageId = normalizePositiveIntegerString(target.messageId);
  return messageId
    ? { channelId, messageId }
    : { channelId };
}

export function parseChatWindowParams(appParams?: Record<string, unknown> | null): ChatWindowParams {
  if (!appParams) {
    return {};
  }

  const channelId = normalizePositiveIntegerString(appParams.channelId);
  if (!channelId) {
    return {};
  }

  const messageId = normalizePositiveIntegerString(appParams.messageId);
  const rawNavigationKey = appParams.__navigationKey;
  const navigationKey = rawNavigationKey == null ? undefined : String(rawNavigationKey);

  return {
    channelId,
    messageId,
    navigationKey,
  };
}

export function parseChatNotificationNavigation(extData?: string | null): ChatNavigationTarget | null {
  const normalized = extData?.trim();
  if (!normalized) {
    return null;
  }

  try {
    const parsed = JSON.parse(normalized) as Record<string, unknown> | string;
    if (typeof parsed === 'string') {
      return null;
    }

    const app = typeof parsed.app === 'string' ? parsed.app.trim().toLowerCase() : '';
    if (app && app !== 'chat') {
      return null;
    }

    const channelId = normalizePositiveIntegerString(parsed.channelId);
    if (!channelId) {
      return null;
    }

    const messageId = normalizePositiveIntegerString(parsed.messageId);
    return messageId
      ? { channelId, messageId }
      : { channelId };
  } catch {
    return null;
  }
}

export function resolveChatMessageNavigationAction(
  messages: ChannelMessageVo[],
  targetMessageId: EntityIdValue,
  hasMoreHistory: boolean
): ChatMessageNavigationAction {
  if (messages.some((message) => areEntityIdsEqual(message.voId, targetMessageId))) {
    return 'found';
  }

  const oldestMessageId = messages[0]?.voId;
  if (hasMoreHistory && isPersistedEntityId(oldestMessageId) && compareEntityIds(oldestMessageId, targetMessageId) > 0) {
    return 'load-more';
  }

  return 'not-found';
}
