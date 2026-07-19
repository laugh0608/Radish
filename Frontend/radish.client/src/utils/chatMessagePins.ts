import type { ChatMessagePinStateVo } from '@radish/http';

type PinStateSource = 'authoritative' | 'broadcast';

function normalizeRevision(revision: string): string {
  const normalized = revision.trim().replace(/^0+(?=\d)/, '');
  return /^\d+$/.test(normalized) ? normalized : '0';
}

export function compareChatPinRevisions(left: string, right: string): number {
  const normalizedLeft = normalizeRevision(left);
  const normalizedRight = normalizeRevision(right);
  if (normalizedLeft.length !== normalizedRight.length) {
    return normalizedLeft.length - normalizedRight.length;
  }

  return normalizedLeft.localeCompare(normalizedRight);
}

export function mergeChatPinState(
  current: ChatMessagePinStateVo | undefined,
  incoming: ChatMessagePinStateVo,
  source: PinStateSource
): ChatMessagePinStateVo {
  if (!current) {
    return incoming;
  }

  const revisionComparison = compareChatPinRevisions(incoming.voRevision, current.voRevision);
  if (revisionComparison < 0 || (revisionComparison === 0 && source === 'broadcast')) {
    return current;
  }

  return incoming;
}
