import { buildChatAppParams } from './chatNavigation.ts';
import { buildForumAppParams } from './forumNavigation.ts';

export interface DesktopExternalEntryTarget {
  appId: 'chat' | 'forum' | 'shop';
  appParams: Record<string, unknown>;
  requiresAuthenticatedSession: boolean;
  signature: string;
}

function normalizeDesktopPathname(pathname: string): string {
  if (!pathname) {
    return '';
  }

  return pathname.endsWith('/') && pathname !== '/' ? pathname.slice(0, -1) : pathname;
}

function normalizePositiveIntegerString(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const normalized = value.trim();
  return /^[1-9]\d*$/.test(normalized) ? normalized : null;
}

function parseChatDesktopEntry(query: URLSearchParams): DesktopExternalEntryTarget | null {
  const channelId = query.get('channelId');
  if (!channelId) {
    return null;
  }

  const messageId = query.get('messageId');
  const appParams = buildChatAppParams({
    channelId,
    ...(messageId ? { messageId } : {}),
  });
  if (!('channelId' in appParams) || typeof appParams.channelId !== 'string') {
    return null;
  }

  const normalizedMessageId = typeof appParams.messageId === 'string' ? appParams.messageId : undefined;
  return {
    appId: 'chat',
    appParams,
    requiresAuthenticatedSession: true,
    signature: `chat:${appParams.channelId}:${normalizedMessageId ?? 'none'}`,
  };
}

function parseForumDesktopEntry(query: URLSearchParams): DesktopExternalEntryTarget | null {
  const hasPostTarget = query.has('postId') || query.has('postPublicId') || query.has('commentId');
  if (!hasPostTarget) {
    return {
      appId: 'forum',
      appParams: {},
      requiresAuthenticatedSession: false,
      signature: 'forum:home',
    };
  }

  const appParams = buildForumAppParams({
    postId: query.get('postId') ?? undefined,
    postPublicId: query.get('postPublicId') ?? undefined,
    commentId: query.get('commentId') ?? undefined,
  });
  const postIdentifier = typeof appParams.postPublicId === 'string'
    ? appParams.postPublicId
    : typeof appParams.postId === 'string'
      ? appParams.postId
      : null;
  if (!postIdentifier) {
    return null;
  }

  const commentId = typeof appParams.commentId === 'string' ? appParams.commentId : undefined;
  return {
    appId: 'forum',
    appParams,
    requiresAuthenticatedSession: false,
    signature: `forum:${postIdentifier}:${commentId ?? 'none'}`,
  };
}

function parseShopDesktopEntry(query: URLSearchParams): DesktopExternalEntryTarget | null {
  const productId = normalizePositiveIntegerString(query.get('productId'));
  if (productId) {
    return {
      appId: 'shop',
      appParams: { productId },
      requiresAuthenticatedSession: false,
      signature: `shop:product:${productId}`,
    };
  }

  const orderId = normalizePositiveIntegerString(query.get('orderId'));
  if (orderId) {
    return {
      appId: 'shop',
      appParams: { orderId },
      requiresAuthenticatedSession: true,
      signature: `shop:order:${orderId}`,
    };
  }

  const view = query.get('view')?.trim().toLowerCase();
  if (view === 'orders' || view === 'inventory') {
    return {
      appId: 'shop',
      appParams: { initialView: view },
      requiresAuthenticatedSession: true,
      signature: `shop:${view}`,
    };
  }

  return null;
}

export function parseDesktopExternalEntry(pathname: string, search: string): DesktopExternalEntryTarget | null {
  if (normalizeDesktopPathname(pathname) !== '/desktop') {
    return null;
  }

  const query = new URLSearchParams(search);
  const appId = query.get('app')?.trim().toLowerCase();
  if (appId === 'chat') {
    return parseChatDesktopEntry(query);
  }

  if (appId === 'forum') {
    return parseForumDesktopEntry(query);
  }

  if (appId === 'shop') {
    return parseShopDesktopEntry(query);
  }

  return null;
}

export function stripDesktopExternalEntrySearch(search: string): string {
  const query = new URLSearchParams(search);
  query.delete('app');
  query.delete('channelId');
  query.delete('messageId');
  query.delete('postId');
  query.delete('postPublicId');
  query.delete('commentId');
  query.delete('productId');
  query.delete('orderId');
  query.delete('view');

  const nextSearch = query.toString();
  return nextSearch ? `?${nextSearch}` : '';
}
