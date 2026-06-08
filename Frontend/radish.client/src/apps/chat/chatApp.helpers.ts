import type { ChannelMessageVo, EntityIdValue } from '../../types/chat.ts';
import {
  isPersistedEntityId,
  normalizeEntityId,
} from '../../types/chat.ts';
import { normalizeBrowserVisibleUrl } from '../../utils/browserVisibleUrl.ts';

export const CHAT_DRAFT_STORAGE_KEY = 'radish.chat.drafts.v1';
export const MENTION_PATTERN = /@\[(?<name>[^\]]+)\]\((?<id>\d+)\)/g;
type Translate = (key: string, options?: Record<string, unknown>) => string;

function defaultTranslate(key: string, options?: Record<string, unknown>): string {
  if (key === 'common.userFallback') {
    return options?.id ? `用户${options.id}` : '用户';
  }

  const fallbacks: Record<string, string> = {
    'chat.recalled': '消息已撤回',
    'chat.imageMessage': '图片消息',
    'chat.genericMessage': '消息',
    'chat.connection.connecting': '正在连接',
    'chat.connection.reconnecting': '正在重连',
    'chat.connection.disconnected': '连接已断开',
    'common.unknownUser': '未知用户',
  };

  return fallbacks[key] ?? key;
}

export interface MentionContext {
  start: number;
  end: number;
  keyword: string;
}

export interface PendingImageDraft {
  attachmentId: EntityIdValue;
  imageUrl: string;
  imageThumbnailUrl?: string | null;
  fileName?: string | null;
}

export interface ChannelDraft {
  content: string;
  replyTarget: ChannelMessageVo | null;
  pendingImage: PendingImageDraft | null;
}

export type ChannelDraftMap = Record<string, ChannelDraft>;

export interface MessageNavigationTarget {
  channelId: string;
  messageId: string;
  signature: string;
}

export interface MessageFocusTarget {
  channelId: string;
  messageId: string;
  signature: string;
}

export function toNumericId(value: EntityIdValue | null | undefined): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

export function getEntityKey(value: EntityIdValue | null | undefined): string {
  return normalizeEntityId(value) ?? '';
}

export function formatChatTime(time: string): string {
  const date = new Date(time);
  if (Number.isNaN(date.getTime())) {
    return '--:--';
  }

  return date.toLocaleTimeString('zh-CN', {
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function resolveMediaUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }

  if (/^https?:\/\//i.test(url)) {
    return normalizeBrowserVisibleUrl(url);
  }

  if (url.startsWith('/')) {
    return normalizeBrowserVisibleUrl(`${apiBaseUrl}${url}`);
  }

  return normalizeBrowserVisibleUrl(`${apiBaseUrl}/${url}`);
}

export function resolveAttachmentAssetUrl(
  attachmentId: EntityIdValue | null | undefined,
  variant: 'original' | 'thumbnail' = 'original'
): string | null {
  const normalizedAttachmentId = normalizeEntityId(attachmentId);
  if (!normalizedAttachmentId || normalizedAttachmentId === '0' || normalizedAttachmentId.startsWith('-')) {
    return null;
  }

  return variant === 'thumbnail'
    ? `/_assets/attachments/${normalizedAttachmentId}/thumbnail`
    : `/_assets/attachments/${normalizedAttachmentId}`;
}

export function buildAvatarText(name: string): string {
  const source = name.trim();
  if (!source) {
    return '?';
  }

  return source.charAt(0).toUpperCase();
}

export function buildAvatarStyle(seed: string) {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = seed.charCodeAt(index) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 78% 92%)`,
    color: `hsl(${hue} 42% 28%)`,
  };
}

export function normalizeMentionText(content: string | null | undefined): string {
  if (!content) {
    return '';
  }

  return content.replace(MENTION_PATTERN, (_, name: string) => `@${name}`);
}

export function getMessagePreviewText(message: ChannelMessageVo, t: Translate = defaultTranslate): string {
  if (message.voIsRecalled) {
    return t('chat.recalled');
  }

  const normalizedContent = normalizeMentionText(message.voContent).replace(/\s+/g, ' ').trim();
  if (normalizedContent) {
    return normalizedContent.length > 72 ? `${normalizedContent.slice(0, 72)}...` : normalizedContent;
  }

  if (message.voType === 2) {
    return t('chat.imageMessage');
  }

  return t('chat.genericMessage');
}

export function getConnectionHint(connectionState: string, t: Translate = defaultTranslate): string | null {
  switch (connectionState) {
    case 'connecting':
      return t('chat.connection.connecting');
    case 'reconnecting':
      return t('chat.connection.reconnecting');
    case 'disconnected':
      return t('chat.connection.disconnected');
    default:
      return null;
  }
}

export function findMentionContext(text: string, cursor: number): MentionContext | null {
  if (cursor <= 0) {
    return null;
  }

  const beforeCaret = text.slice(0, cursor);
  const atIndex = beforeCaret.lastIndexOf('@');
  if (atIndex < 0) {
    return null;
  }

  const prefixChar = atIndex > 0 ? beforeCaret[atIndex - 1] : ' ';
  if (!/\s|[([{>]/.test(prefixChar)) {
    return null;
  }

  const keyword = beforeCaret.slice(atIndex + 1);
  if (/[\s()[\]{}]/.test(keyword)) {
    return null;
  }

  return {
    start: atIndex,
    end: cursor,
    keyword,
  };
}

export function getDraftStorageKey(userId: EntityIdValue, channelId: EntityIdValue): string {
  return `${userId}:${getEntityKey(channelId)}`;
}

export function readDraftMap(): ChannelDraftMap {
  if (typeof window === 'undefined') {
    return {};
  }

  try {
    const raw = window.localStorage.getItem(CHAT_DRAFT_STORAGE_KEY);
    if (!raw) {
      return {};
    }

    const parsed = JSON.parse(raw) as ChannelDraftMap;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export function writeDraftMap(nextMap: ChannelDraftMap): void {
  if (typeof window === 'undefined') {
    return;
  }

  if (Object.keys(nextMap).length === 0) {
    window.localStorage.removeItem(CHAT_DRAFT_STORAGE_KEY);
    return;
  }

  window.localStorage.setItem(CHAT_DRAFT_STORAGE_KEY, JSON.stringify(nextMap));
}

export function loadChannelDraft(userId: EntityIdValue, channelId: EntityIdValue): ChannelDraft | null {
  if (!isPersistedEntityId(userId) || !isPersistedEntityId(channelId)) {
    return null;
  }

  const draftMap = readDraftMap();
  const key = getDraftStorageKey(userId, channelId);
  return draftMap[key] ?? null;
}

export function persistChannelDraft(
  userId: EntityIdValue,
  channelId: EntityIdValue,
  content: string,
  replyTarget: ChannelMessageVo | null,
  pendingImage: PendingImageDraft | null
): void {
  if (!isPersistedEntityId(userId) || !isPersistedEntityId(channelId)) {
    return;
  }

  const draftMap = readDraftMap();
  const key = getDraftStorageKey(userId, channelId);
  const normalizedContent = content.trim();

  if (!normalizedContent && !replyTarget && !pendingImage) {
    delete draftMap[key];
    writeDraftMap(draftMap);
    return;
  }

  draftMap[key] = {
    content,
    replyTarget,
    pendingImage,
  };
  writeDraftMap(draftMap);
}

export function clearChannelDraft(userId: EntityIdValue, channelId: EntityIdValue): void {
  persistChannelDraft(userId, channelId, '', null, null);
}

export function buildClientRequestId(channelId: EntityIdValue): string {
  return `chat-${getEntityKey(channelId)}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

export function getErrorMessage(error: unknown, fallbackMessage: string): string {
  return error instanceof Error ? error.message : fallbackMessage;
}

export function getFallbackUserName(userId?: EntityIdValue | null, t: Translate = defaultTranslate): string {
  const normalizedId = normalizeEntityId(userId);
  return normalizedId
    ? t('common.userFallback', { id: normalizedId })
    : t('common.unknownUser');
}

export function getReplyTargetMessageId(message: ChannelMessageVo | null | undefined): string | null {
  if (!message) {
    return null;
  }

  const messageStatus = message.voLocalStatus ?? 'sent';
  if (!isPersistedEntityId(message.voId) || messageStatus !== 'sent' || message.voIsRecalled) {
    return null;
  }

  return message.voId;
}
