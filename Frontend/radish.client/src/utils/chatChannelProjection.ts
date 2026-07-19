import {
  areEntityIdsEqual,
  compareEntityIds,
  type ChannelMessageVo,
  type ChannelVo,
} from '../types/chat.ts';

function getMessageTimestamp(message: ChannelMessageVo): number {
  const timestamp = Date.parse(message.voCreateTime);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

export function mergeChannelLastMessage(
  channel: ChannelVo,
  incoming: ChannelMessageVo
): ChannelVo {
  if (!areEntityIdsEqual(channel.voId, incoming.voChannelId)) {
    return channel;
  }

  const current = channel.voLastMessage;
  if (!current) {
    return { ...channel, voLastMessage: incoming };
  }

  if (areEntityIdsEqual(current.voId, incoming.voId)) {
    return {
      ...channel,
      voLastMessage: {
        ...current,
        ...incoming,
      },
    };
  }

  const timestampDiff = getMessageTimestamp(incoming) - getMessageTimestamp(current);
  if (
    timestampDiff < 0
    || (timestampDiff === 0 && compareEntityIds(incoming.voId, current.voId) <= 0)
  ) {
    return channel;
  }

  return { ...channel, voLastMessage: incoming };
}
