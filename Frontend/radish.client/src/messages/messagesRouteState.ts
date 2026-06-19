import { buildChatAppParams } from '../utils/chatNavigation.ts';

export const MESSAGES_ENTRY_PATH = '/messages';

export interface MessagesRoute {
  channelId?: string;
  messageId?: string;
}

export function createDefaultMessagesRoute(): MessagesRoute {
  return {};
}

export function isMessagesPathname(pathname: string): boolean {
  return pathname === MESSAGES_ENTRY_PATH || pathname === `${MESSAGES_ENTRY_PATH}/`;
}

function normalizeRouteTarget(channelId: string | null | undefined, messageId: string | null | undefined): MessagesRoute {
  if (!channelId) {
    return createDefaultMessagesRoute();
  }

  const appParams = buildChatAppParams({
    channelId,
    ...(messageId ? { messageId } : {}),
  });

  if (typeof appParams.channelId !== 'string') {
    return createDefaultMessagesRoute();
  }

  return typeof appParams.messageId === 'string'
    ? { channelId: appParams.channelId, messageId: appParams.messageId }
    : { channelId: appParams.channelId };
}

export function parseMessagesRoute(pathname: string, search: string): MessagesRoute | null {
  if (!isMessagesPathname(pathname)) {
    return null;
  }

  const query = new URLSearchParams(search);
  return normalizeRouteTarget(query.get('channelId'), query.get('messageId'));
}

export function buildMessagesPath(route: MessagesRoute = createDefaultMessagesRoute()): string {
  const normalized = normalizeRouteTarget(route.channelId, route.messageId);
  if (!normalized.channelId) {
    return MESSAGES_ENTRY_PATH;
  }

  const query = new URLSearchParams({
    channelId: normalized.channelId,
  });
  if (normalized.messageId) {
    query.set('messageId', normalized.messageId);
  }

  return `${MESSAGES_ENTRY_PATH}?${query.toString()}`;
}
