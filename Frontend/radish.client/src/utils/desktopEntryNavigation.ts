import { buildChatAppParams } from './chatNavigation.ts';

export interface DesktopExternalEntryTarget {
  appId: 'chat';
  appParams: Record<string, unknown>;
  signature: string;
}

function normalizeDesktopPathname(pathname: string): string {
  if (!pathname) {
    return '';
  }

  return pathname.endsWith('/') && pathname !== '/' ? pathname.slice(0, -1) : pathname;
}

export function parseDesktopExternalEntry(pathname: string, search: string): DesktopExternalEntryTarget | null {
  if (normalizeDesktopPathname(pathname) !== '/desktop') {
    return null;
  }

  const query = new URLSearchParams(search);
  const appId = query.get('app')?.trim().toLowerCase();
  if (appId !== 'chat') {
    return null;
  }

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
    signature: `chat:${appParams.channelId}:${normalizedMessageId ?? 'none'}`,
  };
}

export function stripDesktopExternalEntrySearch(search: string): string {
  const query = new URLSearchParams(search);
  query.delete('app');
  query.delete('channelId');
  query.delete('messageId');

  const nextSearch = query.toString();
  return nextSearch ? `?${nextSearch}` : '';
}
