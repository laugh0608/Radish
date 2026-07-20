import type { ChannelVo } from '@/types/chat';
import { isPersistedEntityId } from '@/types/chat';

export function isDirectConversationChannel(channel: ChannelVo | null): boolean {
  return Boolean(
    channel
    && (channel.voConversationKind === 'mutual' || channel.voConversationKind === 'stranger')
    && isPersistedEntityId(channel.voPeerUserId)
  );
}

export function resolveConversationNoticeKey(channel: ChannelVo | null): string | null {
  if (!channel || !isDirectConversationChannel(channel)) {
    return null;
  }

  if (!channel.voIsPeerAvailable) {
    return 'chat.conversationNotice.peerUnavailable';
  }

  if (channel.voIsBlockedByCurrentUser || channel.voCanUnblock) {
    return 'chat.conversationNotice.blockedByMe';
  }

  if (channel.voDirectRequestStatus === 'declined') {
    return 'chat.conversationNotice.declined';
  }

  if (channel.voDirectRequestStatus === 'pending') {
    if (channel.voCanAccept) {
      return 'chat.conversationNotice.requestReceived';
    }

    return channel.voCanSend
      ? 'chat.conversationNotice.requestFirstMessage'
      : 'chat.conversationNotice.requestPending';
  }

  if (!channel.voCanSend) {
    return 'chat.conversationNotice.blockedByPeer';
  }

  if (channel.voIsArchived) {
    return 'chat.conversationNotice.archived';
  }

  return null;
}

export function canSendConversationAttachment(channel: ChannelVo | null): boolean {
  return Boolean(
    channel?.voCanSend
    && (!isDirectConversationChannel(channel) || channel.voDirectRequestStatus !== 'pending')
  );
}
