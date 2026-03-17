export type ChatMessageStatus = 'sending' | 'sent' | 'failed';
export type EntityIdValue = string | number;
export type PersistedEntityId = string;

export function normalizeEntityId(value: EntityIdValue | null | undefined): string | null {
  if (typeof value === 'string') {
    const normalized = value.trim();
    return normalized ? normalized : null;
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value);
  }

  return null;
}

export function isTemporaryEntityId(value: EntityIdValue | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value < 0;
}

export function isPersistedEntityId(value: EntityIdValue | null | undefined): value is PersistedEntityId {
  const normalized = normalizeEntityId(value);
  return normalized !== null && normalized !== '0' && !normalized.startsWith('-');
}

export function areEntityIdsEqual(
  left: EntityIdValue | null | undefined,
  right: EntityIdValue | null | undefined
): boolean {
  const normalizedLeft = normalizeEntityId(left);
  const normalizedRight = normalizeEntityId(right);
  return normalizedLeft !== null && normalizedRight !== null && normalizedLeft === normalizedRight;
}

export function compareEntityIds(
  left: EntityIdValue | null | undefined,
  right: EntityIdValue | null | undefined
): number {
  const normalizedLeft = normalizeEntityId(left);
  const normalizedRight = normalizeEntityId(right);

  if (!normalizedLeft && !normalizedRight) {
    return 0;
  }

  if (!normalizedLeft) {
    return -1;
  }

  if (!normalizedRight) {
    return 1;
  }

  if (/^-?\d+$/.test(normalizedLeft) && /^-?\d+$/.test(normalizedRight)) {
    if (normalizedLeft.length !== normalizedRight.length) {
      return normalizedLeft.length - normalizedRight.length;
    }

    return normalizedLeft.localeCompare(normalizedRight);
  }

  return normalizedLeft.localeCompare(normalizedRight);
}

export interface ChannelMessageVo {
  voId: EntityIdValue;
  voClientRequestId?: string | null;
  voChannelId: EntityIdValue;
  voUserId: EntityIdValue;
  voUserName: string;
  voUserAvatarUrl?: string | null;
  voType: 1 | 2 | 3;
  voContent?: string | null;
  voReplyToId?: EntityIdValue | null;
  voReplyTo?: ChannelMessageVo | null;
  voAttachmentId?: EntityIdValue | null;
  voImageUrl?: string | null;
  voImageThumbnailUrl?: string | null;
  voIsRecalled: boolean;
  voCreateTime: string;
  voLocalStatus?: ChatMessageStatus;
  voLocalError?: string | null;
}

export interface ChannelVo {
  voId: EntityIdValue;
  voCategoryId?: EntityIdValue | null;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIconEmoji?: string | null;
  voType: 1 | 2 | 3;
  voSort: number;
  voUnreadCount: number;
  voHasMention: boolean;
  voLastMessage?: ChannelMessageVo | null;
}

export interface ChannelMemberVo {
  voUserId: EntityIdValue;
  voUserName: string;
  voUserAvatarUrl?: string | null;
  voIsOnline: boolean;
}

export interface SendChannelMessageRequest {
  clientRequestId?: string;
  channelId: EntityIdValue;
  type?: 1 | 2 | 3;
  content?: string;
  replyToId?: EntityIdValue;
  attachmentId?: EntityIdValue;
  imageUrl?: string;
  imageThumbnailUrl?: string;
}

export interface ChannelUnreadChangedPayload {
  channelId: EntityIdValue;
  unreadCount: number;
  hasMention: boolean;
}

export interface MessageRecalledPayload {
  channelId: EntityIdValue;
  messageId: EntityIdValue;
}

export interface UserTypingPayload {
  channelId: EntityIdValue;
  userId: EntityIdValue;
  userName: string;
}
