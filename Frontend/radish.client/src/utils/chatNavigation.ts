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
  channelId: string;
  messageId?: string;
}

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
