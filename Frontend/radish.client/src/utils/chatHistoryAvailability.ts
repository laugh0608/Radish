import type { ChannelMessageVo } from '../types/chat.ts';
import { isTemporaryEntityId } from '../types/chat.ts';

export function isChatChannelUnavailableError(error: unknown): boolean {
  if (!error || typeof error !== 'object') {
    return false;
  }

  const candidate = error as { code?: unknown; messageKey?: unknown };
  const code = typeof candidate.code === 'string' ? candidate.code.trim().toLowerCase() : '';
  const messageKey = typeof candidate.messageKey === 'string'
    ? candidate.messageKey.trim().toLowerCase()
    : '';

  return code === 'chat.channelunavailable'
    || messageKey === 'error.chat.channel_unavailable';
}

export function retainLocalChatMessages(messages: ChannelMessageVo[]): ChannelMessageVo[] {
  return messages.filter((message) => (
    isTemporaryEntityId(message.voId)
    || message.voLocalStatus === 'sending'
    || message.voLocalStatus === 'failed'
  ));
}
