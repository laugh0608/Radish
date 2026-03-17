import { type ChangeEvent, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from '@radish/ui/toast';
import { uploadImage } from '@/api/attachment';
import {
  getChannelHistory,
  getChannelList,
  getChannelOnlineMembers,
  recallChannelMessage,
  sendChannelMessage,
} from '@/api/chat';
import { searchUsersForMention, type UserMentionOption } from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { chatHub } from '@/services/chatHub';
import { useChatStore } from '@/stores/chatStore';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import type { ChannelMemberVo, ChannelMessageVo, EntityIdValue, SendChannelMessageRequest } from '@/types/chat';
import {
  areEntityIdsEqual,
  isPersistedEntityId,
  isTemporaryEntityId,
  normalizeEntityId,
} from '@/types/chat';
import styles from './ChatApp.module.css';

const PAGE_SIZE = 50;
const MEMBER_REFRESH_INTERVAL_MS = 15_000;
const CHAT_DRAFT_STORAGE_KEY = 'radish.chat.drafts.v1';
const MENTION_PATTERN = /@\[(?<name>[^\]]+)\]\((?<id>\d+)\)/g;

interface MentionContext {
  start: number;
  end: number;
  keyword: string;
}

interface ChannelDraft {
  content: string;
  replyTarget: ChannelMessageVo | null;
}

type ChannelDraftMap = Record<string, ChannelDraft>;

function toNumericId(value: EntityIdValue | null | undefined): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function getEntityKey(value: EntityIdValue | null | undefined): string {
  return normalizeEntityId(value) ?? '';
}

function formatTime(time: string): string {
  const date = new Date(time);
  if (Number.isNaN(date.getTime())) {
    return '--:--';
  }

  return date.toLocaleTimeString('zh-CN', {
    hour: '2-digit',
    minute: '2-digit',
  });
}

function resolveMediaUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  if (!url) {
    return null;
  }

  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  if (url.startsWith('/')) {
    return `${apiBaseUrl}${url}`;
  }

  return `${apiBaseUrl}/${url}`;
}

function buildAvatarText(name: string): string {
  const source = name.trim();
  if (!source) {
    return '?';
  }

  return source.charAt(0).toUpperCase();
}

function buildAvatarStyle(seed: string) {
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

function normalizeMentionText(content: string | null | undefined): string {
  if (!content) {
    return '';
  }

  return content.replace(MENTION_PATTERN, (_, name: string) => `@${name}`);
}

function getMessagePreviewText(message: ChannelMessageVo): string {
  if (message.voIsRecalled) {
    return '[已撤回]';
  }

  const normalizedContent = normalizeMentionText(message.voContent).replace(/\s+/g, ' ').trim();
  if (normalizedContent) {
    return normalizedContent.length > 72 ? `${normalizedContent.slice(0, 72)}...` : normalizedContent;
  }

  if (message.voType === 2) {
    return '[图片消息]';
  }

  return '[消息]';
}

function getConnectionHint(connectionState: string): string | null {
  switch (connectionState) {
    case 'connecting':
      return '正在连接实时通道...';
    case 'reconnecting':
      return '实时连接已断开，正在自动恢复并补拉最新消息...';
    case 'disconnected':
      return '实时连接未建立，消息可能不会即时刷新。';
    default:
      return null;
  }
}

function findMentionContext(text: string, cursor: number): MentionContext | null {
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
  if (!keyword || /[\s()[\]{}]/.test(keyword)) {
    return null;
  }

  return {
    start: atIndex,
    end: cursor,
    keyword,
  };
}

function getDraftStorageKey(userId: number, channelId: EntityIdValue): string {
  return `${userId}:${getEntityKey(channelId)}`;
}

function readDraftMap(): ChannelDraftMap {
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

function writeDraftMap(nextMap: ChannelDraftMap): void {
  if (typeof window === 'undefined') {
    return;
  }

  if (Object.keys(nextMap).length === 0) {
    window.localStorage.removeItem(CHAT_DRAFT_STORAGE_KEY);
    return;
  }

  window.localStorage.setItem(CHAT_DRAFT_STORAGE_KEY, JSON.stringify(nextMap));
}

function loadChannelDraft(userId: number, channelId: EntityIdValue): ChannelDraft | null {
  if (userId <= 0 || !isPersistedEntityId(channelId)) {
    return null;
  }

  const draftMap = readDraftMap();
  const key = getDraftStorageKey(userId, channelId);
  return draftMap[key] ?? null;
}

function persistChannelDraft(
  userId: number,
  channelId: EntityIdValue,
  content: string,
  replyTarget: ChannelMessageVo | null
): void {
  if (userId <= 0 || !isPersistedEntityId(channelId)) {
    return;
  }

  const draftMap = readDraftMap();
  const key = getDraftStorageKey(userId, channelId);
  const normalizedContent = content.trim();

  if (!normalizedContent && !replyTarget) {
    delete draftMap[key];
    writeDraftMap(draftMap);
    return;
  }

  draftMap[key] = {
    content,
    replyTarget,
  };
  writeDraftMap(draftMap);
}

function clearChannelDraft(userId: number, channelId: EntityIdValue): void {
  persistChannelDraft(userId, channelId, '', null);
}

function buildClientRequestId(channelId: EntityIdValue): string {
  return `chat-${getEntityKey(channelId)}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function getErrorMessage(error: unknown, fallbackMessage: string): string {
  return error instanceof Error ? error.message : fallbackMessage;
}

function getReplyTargetMessageId(message: ChannelMessageVo | null | undefined): string | null {
  if (!message) {
    return null;
  }

  const messageStatus = message.voLocalStatus ?? 'sent';
  if (!isPersistedEntityId(message.voId) || messageStatus !== 'sent' || message.voIsRecalled) {
    return null;
  }

  return message.voId;
}

export const ChatApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const { openApp } = useWindowStore();
  const {
    channels,
    activeChannelId,
    messageMap,
    typingMap,
    connectionState,
    setChannels,
    setActiveChannel,
    setChannelMessages,
    prependChannelMessages,
    addMessage,
    removeMessage,
    recallMessage,
  } = useChatStore();
  const currentUserId = useUserStore((state) => state.userId);
  const currentUserName = useUserStore((state) => state.userName);
  const currentUserAvatarUrl = useUserStore((state) => state.avatarUrl);

  const [loadingChannels, setLoadingChannels] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadProgress, setImageUploadProgress] = useState(0);
  const [messageInput, setMessageInput] = useState('');
  const [replyTarget, setReplyTarget] = useState<ChannelMessageVo | null>(null);
  const [hasMoreHistory, setHasMoreHistory] = useState<Record<string, boolean>>({});
  const [typingAt, setTypingAt] = useState(0);
  const [mentionContext, setMentionContext] = useState<MentionContext | null>(null);
  const [mentionOptions, setMentionOptions] = useState<UserMentionOption[]>([]);
  const [mentionLoading, setMentionLoading] = useState(false);
  const [mentionSelectedIndex, setMentionSelectedIndex] = useState(0);
  const [onlineMembers, setOnlineMembers] = useState<ChannelMemberVo[]>([]);
  const [memberPanelCollapsed, setMemberPanelCollapsed] = useState(false);

  const messageScrollRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const previousConnectionStateRef = useRef(connectionState);
  const previousChannelIdRef = useRef<EntityIdValue | null>(null);
  const loadedDraftChannelRef = useRef<EntityIdValue | null>(null);
  const tempMessageIdRef = useRef(-1);
  const composerStateRef = useRef<{ messageInput: string; replyTarget: ChannelMessageVo | null }>({
    messageInput: '',
    replyTarget: null,
  });

  const currentUserIdValue = useMemo(() => toNumericId(currentUserId), [currentUserId]);
  const currentUserIdKey = useMemo(() => getEntityKey(currentUserId), [currentUserId]);
  const currentUserNameValue = useMemo(
    () => currentUserName?.trim() || 'Unknown',
    [currentUserName]
  );
  const currentUserAvatarUrlValue = useMemo(
    () => currentUserAvatarUrl?.trim() || null,
    [currentUserAvatarUrl]
  );

  const activeChannel = useMemo(
    () => channels.find((channel) => areEntityIdsEqual(channel.voId, activeChannelId)) || null,
    [channels, activeChannelId]
  );

  const activeMessages = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return messageMap[getEntityKey(activeChannelId)] || [];
  }, [activeChannelId, messageMap]);

  const typingUsers = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return (typingMap[getEntityKey(activeChannelId)] || []).filter((user) => !areEntityIdsEqual(user.userId, currentUserId));
  }, [activeChannelId, currentUserId, typingMap]);

  const connectionHint = useMemo(() => getConnectionHint(connectionState), [connectionState]);

  const isMentionOpen = useMemo(
    () => !!mentionContext && (mentionLoading || mentionOptions.length > 0 || mentionContext.keyword.length > 0),
    [mentionContext, mentionLoading, mentionOptions.length]
  );

  const scrollToBottom = useCallback(() => {
    const scrollEl = messageScrollRef.current;
    if (!scrollEl) {
      return;
    }

    scrollEl.scrollTop = scrollEl.scrollHeight;
  }, []);

  const closeMentionDropdown = useCallback(() => {
    setMentionContext(null);
    setMentionOptions([]);
    setMentionSelectedIndex(0);
    setMentionLoading(false);
  }, []);

  const handleOpenUserProfile = useCallback((targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => {
    const targetUserIdKey = getEntityKey(targetUserId);
    if (!targetUserIdKey) {
      return;
    }

    if (targetUserIdKey === currentUserIdKey) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName?.trim() || `用户 ${targetUserIdKey}`,
      avatarUrl: avatarUrl ?? null,
    });
  }, [currentUserIdKey, openApp]);

  const renderAvatarButton = useCallback((
    targetUserId: EntityIdValue,
    targetUserName: string | null | undefined,
    avatarUrl: string | null | undefined,
    titlePrefix: string,
    className?: string
  ) => {
    const targetUserIdKey = getEntityKey(targetUserId);
    const normalizedName = targetUserName?.trim() || `用户 ${targetUserIdKey || '?'}`;
    const resolvedAvatarUrl = resolveMediaUrl(apiBaseUrl, avatarUrl);
    const canOpenProfile = !!targetUserIdKey && targetUserIdKey !== '0' && !targetUserIdKey.startsWith('-');

    return (
      <button
        type="button"
        className={`${styles.avatarButton} ${className ?? ''}`.trim()}
        onClick={() => {
          if (canOpenProfile) {
            openApp('profile', {
              userId: targetUserId,
              userName: normalizedName,
              avatarUrl: avatarUrl ?? null,
            });
          }
        }}
        disabled={!canOpenProfile}
        title={canOpenProfile ? `${titlePrefix}${normalizedName}` : '用户信息不可用'}
      >
        {resolvedAvatarUrl ? (
          <img src={resolvedAvatarUrl} alt={normalizedName} className={styles.avatarImage} loading="lazy" />
        ) : (
          <span className={styles.avatarFallback} style={buildAvatarStyle(normalizedName)}>
            {buildAvatarText(normalizedName)}
          </span>
        )}
      </button>
    );
  }, [apiBaseUrl, openApp]);

  const renderAvatarVisual = useCallback((
    targetUserName: string | null | undefined,
    avatarUrl: string | null | undefined,
    className?: string
  ) => {
    const normalizedName = targetUserName?.trim() || '用户';
    const resolvedAvatarUrl = resolveMediaUrl(apiBaseUrl, avatarUrl);

    if (resolvedAvatarUrl) {
      return (
        <img
          src={resolvedAvatarUrl}
          alt={normalizedName}
          className={`${styles.avatarImage} ${className ?? ''}`.trim()}
          loading="lazy"
        />
      );
    }

    return (
      <span className={`${styles.avatarFallback} ${className ?? ''}`.trim()} style={buildAvatarStyle(normalizedName)}>
        {buildAvatarText(normalizedName)}
      </span>
    );
  }, [apiBaseUrl, openApp]);

  const renderMessageContent = useCallback((content: string | null | undefined): ReactNode => {
    if (!content) {
      return null;
    }

    const nodes: ReactNode[] = [];
    let lastIndex = 0;
    let keyIndex = 0;

    for (const match of content.matchAll(MENTION_PATTERN)) {
      const matchIndex = match.index ?? 0;
      const matchText = match[0];
      const mentionName = match.groups?.name ?? match[1] ?? '用户';
      const mentionUserId = normalizeEntityId(match.groups?.id ?? match[2]) ?? '';

      if (matchIndex > lastIndex) {
        nodes.push(content.slice(lastIndex, matchIndex));
      }

      nodes.push(
        <button
          key={`mention-${mentionUserId || keyIndex}-${keyIndex}`}
          type="button"
          className={styles.mentionChip}
          onClick={() => handleOpenUserProfile(mentionUserId, mentionName)}
        >
          @{mentionName}
        </button>
      );

      lastIndex = matchIndex + matchText.length;
      keyIndex += 1;
    }

    if (lastIndex < content.length) {
      nodes.push(content.slice(lastIndex));
    }

    return nodes;
  }, [handleOpenUserProfile]);

  const updateMentionContext = useCallback((text: string, cursor: number) => {
    const context = findMentionContext(text, cursor);
    if (!context) {
      closeMentionDropdown();
      return;
    }

    setMentionContext(context);
  }, [closeMentionDropdown]);

  const applyMentionSelection = useCallback((option: UserMentionOption) => {
    if (!mentionContext) {
      return;
    }

    const targetId = normalizeEntityId(option.voId);
    if (!targetId) {
      return;
    }

    const targetName = option.voUserName?.trim() || `用户${targetId}`;
    const mentionToken = `@[${targetName}](${targetId}) `;
    const nextValue = `${messageInput.slice(0, mentionContext.start)}${mentionToken}${messageInput.slice(mentionContext.end)}`;
    const nextCursor = mentionContext.start + mentionToken.length;

    setMessageInput(nextValue);
    closeMentionDropdown();

    requestAnimationFrame(() => {
      const textarea = textareaRef.current;
      if (!textarea) {
        return;
      }

      textarea.focus();
      textarea.setSelectionRange(nextCursor, nextCursor);
    });
  }, [closeMentionDropdown, mentionContext, messageInput]);

  const loadChannels = useCallback(async () => {
    setLoadingChannels(true);
    try {
      const data = await getChannelList();
      setChannels(data);

      if (data.length > 0) {
        const firstId = normalizeEntityId(data[0].voId);
        if (firstId) {
          setActiveChannel(firstId);
        }
      }
    } catch (error) {
      log.error('ChatApp', '加载频道列表失败:', error);
      toast.error('加载频道列表失败');
    } finally {
      setLoadingChannels(false);
    }
  }, [setChannels, setActiveChannel]);

  const loadInitialHistory = useCallback(async (channelId: EntityIdValue) => {
    const channelKey = getEntityKey(channelId);
    if (!channelKey) {
      return;
    }

    setLoadingHistory(true);

    try {
      const history = await getChannelHistory(channelId, undefined, PAGE_SIZE);
      setChannelMessages(channelId, history);
      setHasMoreHistory((prev) => ({
        ...prev,
        [channelKey]: history.length >= PAGE_SIZE,
      }));

      requestAnimationFrame(() => {
        scrollToBottom();
      });

      await chatHub.markChannelAsRead(channelId);
    } catch (error) {
      log.error('ChatApp', '加载历史消息失败:', error);
      toast.error('加载历史消息失败');
    } finally {
      setLoadingHistory(false);
    }
  }, [scrollToBottom, setChannelMessages]);

  const loadOnlineMembers = useCallback(async (channelId: EntityIdValue) => {
    if (!isPersistedEntityId(channelId)) {
      setOnlineMembers([]);
      return;
    }

    setLoadingMembers(true);
    try {
      const members = await getChannelOnlineMembers(channelId);
      setOnlineMembers(members);
    } catch (error) {
      log.error('ChatApp', '加载在线成员失败:', error);
      setOnlineMembers([]);
    } finally {
      setLoadingMembers(false);
    }
  }, []);

  const loadMoreHistory = useCallback(async () => {
    const activeChannelKey = getEntityKey(activeChannelId);
    if (!activeChannelId || !activeChannelKey || loadingHistory || !hasMoreHistory[activeChannelKey]) {
      return;
    }

    const oldestMessage = activeMessages[0];
    const beforeMessageId = oldestMessage?.voId;
    if (!isPersistedEntityId(beforeMessageId)) {
      setHasMoreHistory((prev) => ({
        ...prev,
        [activeChannelKey]: false,
      }));
      return;
    }

    setLoadingHistory(true);
    try {
      const older = await getChannelHistory(activeChannelId, beforeMessageId, PAGE_SIZE);
      prependChannelMessages(activeChannelId, older);
      setHasMoreHistory((prev) => ({
        ...prev,
        [activeChannelKey]: older.length >= PAGE_SIZE,
      }));
    } catch (error) {
      log.error('ChatApp', '加载更多历史消息失败:', error);
      toast.error('加载更多历史消息失败');
    } finally {
      setLoadingHistory(false);
    }
  }, [activeChannelId, activeMessages, hasMoreHistory, loadingHistory, prependChannelMessages]);

  const resetComposer = useCallback((channelId: EntityIdValue) => {
    setMessageInput('');
    setReplyTarget(null);
    closeMentionDropdown();
    clearChannelDraft(currentUserIdValue, channelId);
  }, [closeMentionDropdown, currentUserIdValue]);

  const createTempMessageId = useCallback(() => {
    const nextId = tempMessageIdRef.current;
    tempMessageIdRef.current -= 1;
    return nextId;
  }, []);

  const createOptimisticMessage = useCallback((params: {
    tempMessageId: number;
    clientRequestId: string;
    channelId: EntityIdValue;
    type: 1 | 2 | 3;
    content?: string;
    replyTo: ChannelMessageVo | null;
    attachmentId?: EntityIdValue;
    imageUrl?: string;
    imageThumbnailUrl?: string;
  }): ChannelMessageVo => ({
    voId: params.tempMessageId,
    voClientRequestId: params.clientRequestId,
    voChannelId: params.channelId,
    voUserId: currentUserIdKey || currentUserIdValue,
    voUserName: currentUserNameValue,
    voUserAvatarUrl: currentUserAvatarUrlValue,
    voType: params.type,
    voContent: params.content ?? null,
    voReplyToId: params.replyTo ? params.replyTo.voId : null,
    voReplyTo: params.replyTo,
    voAttachmentId: params.attachmentId ?? null,
    voImageUrl: params.imageUrl ?? null,
    voImageThumbnailUrl: params.imageThumbnailUrl ?? params.imageUrl ?? null,
    voIsRecalled: false,
    voCreateTime: new Date().toISOString(),
    voLocalStatus: 'sending',
    voLocalError: null,
  }), [currentUserAvatarUrlValue, currentUserIdKey, currentUserIdValue, currentUserNameValue]);

  const sendOptimisticMessage = useCallback(async (
    optimisticMessage: ChannelMessageVo,
    request: SendChannelMessageRequest,
    options?: {
      successToastMessage?: string;
      failureFallbackMessage?: string;
    }
  ) => {
    addMessage(optimisticMessage);

    requestAnimationFrame(() => {
      scrollToBottom();
    });

    try {
      const sent = await sendChannelMessage({
        ...request,
        clientRequestId: optimisticMessage.voClientRequestId || undefined,
      });

      addMessage({
        ...sent,
        voLocalStatus: 'sent',
        voLocalError: null,
      });

      await chatHub.markChannelAsRead(request.channelId);

      if (options?.successToastMessage) {
        toast.success(options.successToastMessage);
      }
    } catch (error) {
      const errorMessage = getErrorMessage(error, options?.failureFallbackMessage || '发送消息失败');
      log.error('ChatApp', '发送消息失败:', error);
      addMessage({
        ...optimisticMessage,
        voLocalStatus: 'failed',
        voLocalError: errorMessage,
      });
      toast.error(errorMessage);
    }
  }, [addMessage, scrollToBottom]);

  const handleSendMessage = useCallback(() => {
    if (!activeChannelId || uploadingImage) {
      return;
    }

    const content = messageInput.trim();
    if (!content) {
      return;
    }

    const replyTargetId = getReplyTargetMessageId(replyTarget);
    const replyTargetSnapshot = replyTargetId ? replyTarget : null;
    const tempMessageId = createTempMessageId();
    const clientRequestId = buildClientRequestId(activeChannelId);
    const optimisticMessage = createOptimisticMessage({
      tempMessageId,
      clientRequestId,
      channelId: activeChannelId,
      type: 1,
      content,
      replyTo: replyTargetSnapshot,
    });

    resetComposer(activeChannelId);

    void sendOptimisticMessage(
      optimisticMessage,
      {
        channelId: activeChannelId,
        type: 1,
        content,
        replyToId: replyTargetId ?? undefined,
      },
      {
        failureFallbackMessage: '发送消息失败',
      }
    );
  }, [activeChannelId, createOptimisticMessage, createTempMessageId, messageInput, replyTarget, resetComposer, sendOptimisticMessage, uploadingImage]);

  const handleImageSelected = useCallback(async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !activeChannelId || uploadingImage) {
      return;
    }

    setUploadingImage(true);
    setImageUploadProgress(0);

    try {
      const attachment = await uploadImage(
        {
          file,
          businessType: 'Chat',
          generateThumbnail: true,
          onProgress: setImageUploadProgress,
        },
        t
      );

      const content = messageInput.trim();
      const replyTargetId = getReplyTargetMessageId(replyTarget);
      const replyTargetSnapshot = replyTargetId ? replyTarget : null;
      const tempMessageId = createTempMessageId();
      const clientRequestId = buildClientRequestId(activeChannelId);
      const optimisticMessage = createOptimisticMessage({
        tempMessageId,
        clientRequestId,
        channelId: activeChannelId,
        type: 2,
        content: content || undefined,
        replyTo: replyTargetSnapshot,
        attachmentId: attachment.voId,
        imageUrl: attachment.voUrl,
        imageThumbnailUrl: attachment.voThumbnailUrl || attachment.voUrl,
      });

      resetComposer(activeChannelId);
      setImageUploadProgress(100);

      void sendOptimisticMessage(
        optimisticMessage,
        {
          channelId: activeChannelId,
          type: 2,
          content: content || undefined,
          replyToId: replyTargetId ?? undefined,
          attachmentId: attachment.voId,
          imageUrl: attachment.voUrl,
          imageThumbnailUrl: attachment.voThumbnailUrl || attachment.voUrl,
        },
        {
          successToastMessage: '图片已发送',
          failureFallbackMessage: '发送图片消息失败',
        }
      );
    } catch (error) {
      log.error('ChatApp', '发送图片消息失败:', error);
      toast.error(getErrorMessage(error, '发送图片消息失败'));
    } finally {
      setUploadingImage(false);
      setTimeout(() => setImageUploadProgress(0), 400);
      event.target.value = '';
    }
  }, [activeChannelId, createOptimisticMessage, createTempMessageId, messageInput, replyTarget, resetComposer, sendOptimisticMessage, t, uploadingImage]);

  const handleRetryMessage = useCallback((message: ChannelMessageVo) => {
    const channelId = message.voChannelId;
    const messageId = message.voId;
    if (!isPersistedEntityId(channelId) || !isTemporaryEntityId(messageId)) {
      return;
    }

    const clientRequestId = buildClientRequestId(channelId);
    const retryMessage: ChannelMessageVo = {
      ...message,
      voClientRequestId: clientRequestId,
      voLocalStatus: 'sending',
      voLocalError: null,
    };

    void sendOptimisticMessage(
      retryMessage,
      {
        channelId,
        type: message.voType,
        content: message.voContent?.trim() || undefined,
        replyToId: isPersistedEntityId(message.voReplyToId) ? message.voReplyToId : undefined,
        attachmentId: isPersistedEntityId(message.voAttachmentId) ? message.voAttachmentId : undefined,
        imageUrl: message.voImageUrl || undefined,
        imageThumbnailUrl: message.voImageThumbnailUrl || message.voImageUrl || undefined,
      },
      {
        successToastMessage: message.voType === 2 ? '图片已发送' : undefined,
        failureFallbackMessage: message.voType === 2 ? '发送图片消息失败' : '发送消息失败',
      }
    );
  }, [sendOptimisticMessage]);

  const handleDismissFailedMessage = useCallback((message: ChannelMessageVo) => {
    const channelId = message.voChannelId;
    const messageId = message.voId;
    if (!isPersistedEntityId(channelId) || !isTemporaryEntityId(messageId)) {
      return;
    }

    if (replyTarget && areEntityIdsEqual(replyTarget.voId, messageId)) {
      setReplyTarget(null);
    }

    removeMessage(channelId, messageId);
  }, [removeMessage, replyTarget]);

  const handleRecall = useCallback(async (messageId: EntityIdValue) => {
    if (!isPersistedEntityId(messageId)) {
      return;
    }

    try {
      await recallChannelMessage(messageId);
      if (activeChannelId) {
        recallMessage(activeChannelId, messageId);
      }
      if (replyTarget && areEntityIdsEqual(replyTarget.voId, messageId)) {
        setReplyTarget(null);
      }
      toast.success('消息已撤回');
    } catch (error) {
      log.error('ChatApp', '撤回消息失败:', error);
      toast.error(error instanceof Error ? error.message : '撤回消息失败');
    }
  }, [activeChannelId, recallMessage, replyTarget]);

  const handleScroll = useCallback(async () => {
    const scrollEl = messageScrollRef.current;
    if (!scrollEl || !activeChannelId) {
      return;
    }

    if (scrollEl.scrollTop <= 24 && !loadingHistory) {
      await loadMoreHistory();
    }

    const distanceToBottom = scrollEl.scrollHeight - (scrollEl.scrollTop + scrollEl.clientHeight);
    if (distanceToBottom <= 24) {
      await chatHub.markChannelAsRead(activeChannelId);
    }
  }, [activeChannelId, loadMoreHistory, loadingHistory]);

  useEffect(() => {
    composerStateRef.current = {
      messageInput,
      replyTarget,
    };
  }, [messageInput, replyTarget]);

  useEffect(() => {
    void loadChannels();
  }, [loadChannels]);

  useEffect(() => {
    void chatHub.start();
  }, []);

  useEffect(() => {
    const previousChannelId = previousChannelIdRef.current;
    if (previousChannelId && !areEntityIdsEqual(previousChannelId, activeChannelId)) {
      persistChannelDraft(
        currentUserIdValue,
        previousChannelId,
        composerStateRef.current.messageInput,
        composerStateRef.current.replyTarget
      );
    }

    previousChannelIdRef.current = activeChannelId;

    if (!activeChannelId) {
      setMessageInput('');
      setReplyTarget(null);
      setOnlineMembers([]);
      loadedDraftChannelRef.current = null;
      closeMentionDropdown();
      return;
    }

    const draft = loadChannelDraft(currentUserIdValue, activeChannelId);
    setMessageInput(draft?.content ?? '');
    setReplyTarget(draft?.replyTarget ?? null);
    closeMentionDropdown();
    loadedDraftChannelRef.current = activeChannelId;

    void chatHub.joinChannel(activeChannelId);
    void loadInitialHistory(activeChannelId);
    void loadOnlineMembers(activeChannelId);

    return () => {
      void chatHub.leaveChannel(activeChannelId);
    };
  }, [activeChannelId, closeMentionDropdown, currentUserIdValue, loadInitialHistory, loadOnlineMembers]);

  useEffect(() => {
    if (!activeChannelId || !areEntityIdsEqual(loadedDraftChannelRef.current, activeChannelId)) {
      return;
    }

    persistChannelDraft(currentUserIdValue, activeChannelId, messageInput, replyTarget);
  }, [activeChannelId, currentUserIdValue, messageInput, replyTarget]);

  useEffect(() => {
    if (!activeChannelId) {
      return;
    }

    const timer = window.setInterval(() => {
      void loadOnlineMembers(activeChannelId);
    }, MEMBER_REFRESH_INTERVAL_MS);

    return () => {
      window.clearInterval(timer);
    };
  }, [activeChannelId, loadOnlineMembers]);

  useEffect(() => {
    if (!activeChannelId || activeMessages.length === 0) {
      return;
    }

    const scrollEl = messageScrollRef.current;
    if (!scrollEl) {
      return;
    }

    const distanceToBottom = scrollEl.scrollHeight - (scrollEl.scrollTop + scrollEl.clientHeight);
    if (distanceToBottom <= 120) {
      requestAnimationFrame(() => {
        scrollToBottom();
      });
    }
  }, [activeChannelId, activeMessages, scrollToBottom]);

  useEffect(() => {
    if (!replyTarget) {
      return;
    }

    const replyTargetId = getReplyTargetMessageId(replyTarget);
    if (!replyTargetId) {
      setReplyTarget(null);
      return;
    }

    const latestReplyTarget = activeMessages.find((message) => areEntityIdsEqual(message.voId, replyTargetId));
    if (latestReplyTarget && latestReplyTarget !== replyTarget) {
      setReplyTarget(latestReplyTarget);
    }
  }, [activeMessages, replyTarget]);

  useEffect(() => {
    const previousConnectionState = previousConnectionStateRef.current;
    if (activeChannelId && previousConnectionState === 'reconnecting' && connectionState === 'connected') {
      setChannelMessages(activeChannelId, []);
      void loadInitialHistory(activeChannelId);
      void loadOnlineMembers(activeChannelId);
    }

    previousConnectionStateRef.current = connectionState;
  }, [activeChannelId, connectionState, loadInitialHistory, loadOnlineMembers, setChannelMessages]);

  useEffect(() => {
    if (!mentionContext || !mentionContext.keyword.trim()) {
      setMentionOptions([]);
      setMentionSelectedIndex(0);
      setMentionLoading(false);
      return;
    }

    let cancelled = false;
    setMentionLoading(true);

    const timer = window.setTimeout(async () => {
      try {
        const options = await searchUsersForMention(mentionContext.keyword, t, 8);
        if (!cancelled) {
          setMentionOptions(options);
          setMentionSelectedIndex(0);
        }
      } catch (error) {
        log.error('ChatApp', '搜索提及用户失败:', error);
        if (!cancelled) {
          setMentionOptions([]);
        }
      } finally {
        if (!cancelled) {
          setMentionLoading(false);
        }
      }
    }, 180);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [mentionContext, t]);

  return (
    <div className={styles.chatApp}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarHeader}>聊天室</div>
        {loadingChannels ? (
          <div className={styles.sidebarEmpty}>频道加载中...</div>
        ) : channels.length === 0 ? (
          <div className={styles.sidebarEmpty}>暂无可用频道</div>
        ) : (
          channels.map((channel) => {
            const channelId = normalizeEntityId(channel.voId);
            if (!channelId) {
              return null;
            }

            const isActive = areEntityIdsEqual(activeChannelId, channelId);

            return (
              <button
                key={channelId}
                className={`${styles.channelItem} ${isActive ? styles.channelItemActive : ''}`}
                onClick={() => setActiveChannel(channelId)}
                type="button"
              >
                <span className={styles.channelName}>
                  {channel.voIconEmoji || '#'} {channel.voName}
                </span>
                {channel.voUnreadCount > 0 && (
                  <span className={styles.unreadBadge}>{channel.voUnreadCount}</span>
                )}
              </button>
            );
          })
        )}
      </aside>

      <section className={styles.main}>
        <header className={styles.mainHeader}>
          <div className={styles.headerMain}>
            <div>
              <div className={styles.channelTitle}>
                {activeChannel ? `${activeChannel.voIconEmoji || '#'} ${activeChannel.voName}` : '请选择频道'}
              </div>
              {activeChannel?.voDescription && (
                <div className={styles.channelDescription}>{activeChannel.voDescription}</div>
              )}
            </div>

            {activeChannelId !== null && (
              <button
                type="button"
                className={styles.memberToggleButton}
                onClick={() => setMemberPanelCollapsed((current) => !current)}
              >
                {memberPanelCollapsed ? '展开成员' : '收起成员'}
              </button>
            )}
          </div>

          {activeChannelId !== null && connectionHint && (
            <div className={styles.connectionBanner}>{connectionHint}</div>
          )}
        </header>

        <div className={styles.contentArea}>
          <div className={styles.messageColumn}>
            <div className={styles.messageViewport} ref={messageScrollRef} onScroll={() => { void handleScroll(); }}>
            {activeChannelId === null ? (
              <div className={styles.placeholder}>请选择一个频道开始聊天</div>
            ) : activeMessages.length === 0 && loadingHistory ? (
              <div className={styles.placeholder}>正在加载历史消息...</div>
            ) : activeMessages.length === 0 ? (
              <div className={styles.placeholder}>还没有消息，发一条试试</div>
            ) : (
              activeMessages.map((message: ChannelMessageVo) => {
                const messageIdKey = getEntityKey(message.voId);
                const messageUserIdKey = getEntityKey(message.voUserId);
                const canOpenMessageUserProfile = !!messageUserIdKey && messageUserIdKey !== '0' && !messageUserIdKey.startsWith('-');
                const isMine = !!messageUserIdKey && messageUserIdKey === currentUserIdKey;
                const messageStatus = message.voLocalStatus ?? 'sent';
                const isSendingMessage = messageStatus === 'sending';
                const isFailedMessage = messageStatus === 'failed';
                const replyText = message.voReplyTo ? getMessagePreviewText(message.voReplyTo) : null;
                const messageImageUrl = resolveMediaUrl(apiBaseUrl, message.voImageUrl);

                return (
                  <div key={messageIdKey || message.voClientRequestId || message.voCreateTime} className={`${styles.messageRow} ${isMine ? styles.mine : ''}`}>
                    {!isMine && renderAvatarButton(
                      message.voUserId,
                      message.voUserName,
                      message.voUserAvatarUrl,
                      '查看 '
                    )}

                    <div className={styles.messageBody}>
                      <div className={styles.metaLine}>
                        <button
                          type="button"
                          className={styles.userNameButton}
                          onClick={() => handleOpenUserProfile(message.voUserId, message.voUserName, message.voUserAvatarUrl)}
                          disabled={!canOpenMessageUserProfile}
                          title={canOpenMessageUserProfile ? `查看 ${(message.voUserName || `用户 ${messageUserIdKey}`).trim()} 的主页` : '用户信息不可用'}
                        >
                          <span className={styles.userName}>{message.voUserName || 'Unknown'}</span>
                        </button>
                        <span className={styles.time}>{formatTime(message.voCreateTime)}</span>
                        {!message.voIsRecalled && messageStatus === 'sent' && (
                          <button
                            type="button"
                            className={styles.replyButton}
                            onClick={() => setReplyTarget(message)}
                          >
                            回复
                          </button>
                        )}
                        {isMine && !message.voIsRecalled && messageStatus === 'sent' && isPersistedEntityId(message.voId) && (
                          <button
                            type="button"
                            className={styles.recallButton}
                            onClick={() => {
                              void handleRecall(message.voId);
                            }}
                          >
                            撤回
                          </button>
                        )}
                      </div>

                      {message.voIsRecalled ? (
                        <div className={styles.recalled}>[已撤回]</div>
                      ) : (
                        <div className={styles.messageStack}>
                          {message.voReplyTo && (
                            <div className={styles.quotedMessage}>
                              <div className={styles.quotedAuthor}>
                                回复 {message.voReplyTo.voUserName || 'Unknown'}
                              </div>
                              <div className={styles.quotedText}>{replyText}</div>
                            </div>
                          )}
                          {message.voContent && <div className={styles.bubble}>{renderMessageContent(message.voContent)}</div>}
                          {message.voType === 2 && messageImageUrl && (
                            <img className={styles.imageMessage} src={messageImageUrl} alt="图片消息" loading="lazy" />
                          )}
                          {isMine && !message.voIsRecalled && messageStatus !== 'sent' && (
                            <div className={styles.deliveryState}>
                              <span className={`${styles.deliveryStateText} ${isFailedMessage ? styles.deliveryStateTextFailed : ''}`}>
                                {isSendingMessage ? '发送中...' : (message.voLocalError || '发送失败')}
                              </span>
                              {isFailedMessage && (
                                <>
                                  <button
                                    type="button"
                                    className={styles.deliveryActionButton}
                                    onClick={() => handleRetryMessage(message)}
                                  >
                                    重试
                                  </button>
                                  <button
                                    type="button"
                                    className={styles.deliveryActionButton}
                                    onClick={() => handleDismissFailedMessage(message)}
                                  >
                                    撤销
                                  </button>
                                </>
                              )}
                            </div>
                          )}
                        </div>
                      )}
                    </div>

                    {isMine && renderAvatarButton(
                      message.voUserId,
                      message.voUserName,
                      message.voUserAvatarUrl,
                      '查看 '
                    )}
                  </div>
                );
              })
            )}
            </div>

            <footer className={styles.inputArea}>
              {typingUsers.length > 0 && (
                <div className={styles.typingHint}>
                  {typingUsers.map((user, index) => (
                    <span key={`${user.userId}-${index}`}>
                      {index > 0 ? '、' : null}
                      <button
                        type="button"
                        className={styles.typingUserButton}
                        onClick={() => handleOpenUserProfile(user.userId, user.userName)}
                        title={`查看 ${user.userName} 的主页`}
                      >
                        {user.userName}
                      </button>
                    </span>
                  ))} 正在输入...
                </div>
              )}

              {replyTarget && (
                <div className={styles.replyPreview}>
                  <div className={styles.replyPreviewBody}>
                    <div className={styles.replyPreviewLabel}>
                      回复 {replyTarget.voUserName || 'Unknown'}
                    </div>
                    <div className={styles.replyPreviewText}>{getMessagePreviewText(replyTarget)}</div>
                  </div>
                  <button
                    type="button"
                    className={styles.replyCancelButton}
                    onClick={() => setReplyTarget(null)}
                  >
                    取消
                  </button>
                </div>
              )}

              {activeChannelId !== null && messageInput.trim() && (
                <div className={styles.draftHint}>当前频道草稿已自动保存</div>
              )}

              {uploadingImage && (
                <div className={styles.uploadStatus}>
                  图片上传中... {Math.max(0, Math.min(100, imageUploadProgress))}%
                </div>
              )}

              <div className={styles.inputRow}>
                <div className={styles.inputWrap}>
                  <textarea
                    ref={textareaRef}
                    className={styles.input}
                    value={messageInput}
                    onChange={(event) => {
                      const nextValue = event.target.value;
                      setMessageInput(nextValue);
                      updateMentionContext(nextValue, event.target.selectionStart);

                      const now = Date.now();
                      if (activeChannelId && now - typingAt >= 2000) {
                        setTypingAt(now);
                        void chatHub.startTyping(activeChannelId);
                      }
                    }}
                    onClick={(event) => {
                      updateMentionContext(messageInput, event.currentTarget.selectionStart);
                    }}
                    onKeyUp={(event) => {
                      updateMentionContext(messageInput, event.currentTarget.selectionStart);
                    }}
                    onKeyDown={(event) => {
                      if (isMentionOpen) {
                        if (event.key === 'ArrowDown') {
                          event.preventDefault();
                          setMentionSelectedIndex((prev) => (
                            mentionOptions.length > 0 ? (prev + 1) % mentionOptions.length : 0
                          ));
                          return;
                        }

                        if (event.key === 'ArrowUp') {
                          event.preventDefault();
                          setMentionSelectedIndex((prev) => (
                            mentionOptions.length > 0 ? (prev - 1 + mentionOptions.length) % mentionOptions.length : 0
                          ));
                          return;
                        }

                        if ((event.key === 'Enter' || event.key === 'Tab') && mentionOptions[mentionSelectedIndex]) {
                          event.preventDefault();
                          applyMentionSelection(mentionOptions[mentionSelectedIndex]);
                          return;
                        }

                        if (event.key === 'Escape') {
                          event.preventDefault();
                          closeMentionDropdown();
                          return;
                        }
                      }

                      if (event.key === 'Escape' && replyTarget) {
                        event.preventDefault();
                        setReplyTarget(null);
                        return;
                      }

                      if (event.key === 'Enter' && !event.shiftKey) {
                        event.preventDefault();
                        void handleSendMessage();
                      }
                    }}
                    placeholder={activeChannelId ? '输入消息，支持 @提及，Enter 发送，Shift+Enter 换行，Esc 取消回复' : '请先选择频道'}
                    disabled={!activeChannelId || uploadingImage}
                  />

                  {isMentionOpen && (
                    <div className={styles.mentionDropdown}>
                      {mentionLoading ? (
                        <div className={styles.mentionEmpty}>搜索中...</div>
                      ) : mentionOptions.length === 0 ? (
                        <div className={styles.mentionEmpty}>未找到匹配的用户</div>
                      ) : (
                        mentionOptions.map((option, index) => {
                          const optionId = normalizeEntityId(option.voId) ?? `mention-${index}`;
                          const optionName = option.voUserName?.trim() || `用户 ${optionId}`;
                          const optionAvatarUrl = resolveMediaUrl(apiBaseUrl, option.voAvatar);

                          return (
                            <button
                              key={optionId}
                              type="button"
                              className={`${styles.mentionOption} ${index === mentionSelectedIndex ? styles.mentionOptionActive : ''}`}
                              onMouseDown={(event) => {
                                event.preventDefault();
                                applyMentionSelection(option);
                              }}
                              onMouseEnter={() => setMentionSelectedIndex(index)}
                            >
                              {optionAvatarUrl ? (
                                <img src={optionAvatarUrl} alt={optionName} className={styles.mentionAvatar} loading="lazy" />
                              ) : (
                                <span className={styles.mentionAvatarFallback} style={buildAvatarStyle(optionName)}>
                                  {buildAvatarText(optionName)}
                                </span>
                              )}
                              <span className={styles.mentionLabel}>@{optionName}</span>
                            </button>
                          );
                        })
                      )}
                    </div>
                  )}
                </div>

                <div className={styles.actionColumn}>
                  <input
                    ref={imageInputRef}
                    type="file"
                    accept="image/*"
                    className={styles.hiddenFileInput}
                    onChange={(event) => {
                      void handleImageSelected(event);
                    }}
                  />
                  <button
                    className={styles.imageButton}
                    type="button"
                    disabled={!activeChannelId || uploadingImage}
                    onClick={() => imageInputRef.current?.click()}
                  >
                    {uploadingImage ? '上传中...' : '图片'}
                  </button>
                  <button
                    className={styles.sendButton}
                    type="button"
                    disabled={!activeChannelId || uploadingImage || !messageInput.trim()}
                    onClick={() => {
                      void handleSendMessage();
                    }}
                  >
                    发送
                  </button>
                </div>
              </div>
            </footer>
          </div>

          {activeChannelId !== null && (
            <aside className={`${styles.memberPanel} ${memberPanelCollapsed ? styles.memberPanelCollapsed : ''}`}>
              <div className={styles.memberPanelHeader}>
                <div className={styles.memberPanelTitle}>
                  成员
                  {!memberPanelCollapsed && <span className={styles.memberCount}>{onlineMembers.length}</span>}
                </div>
                <button
                  type="button"
                  className={styles.memberCollapseButton}
                  onClick={() => setMemberPanelCollapsed((current) => !current)}
                >
                  {memberPanelCollapsed ? '>' : '<'}
                </button>
              </div>

              {!memberPanelCollapsed && (
                <div className={styles.memberPanelBody}>
                  {loadingMembers ? (
                    <div className={styles.memberEmpty}>正在加载在线成员...</div>
                  ) : onlineMembers.length === 0 ? (
                    <div className={styles.memberEmpty}>当前暂无在线成员</div>
                  ) : (
                    onlineMembers.map((member) => {
                      const memberId = normalizeEntityId(member.voUserId) ?? member.voUserName ?? 'member';
                      const memberName = member.voUserName?.trim() || `用户 ${memberId}`;

                      return (
                        <button
                          key={memberId}
                          type="button"
                          className={styles.memberItem}
                          onClick={() => handleOpenUserProfile(member.voUserId, member.voUserName, member.voUserAvatarUrl)}
                        >
                          {renderAvatarVisual(member.voUserName, member.voUserAvatarUrl, styles.memberAvatar)}
                          <span className={styles.memberName}>{memberName}</span>
                        </button>
                      );
                    })
                  )}
                </div>
              )}
            </aside>
          )}
        </div>
      </section>
    </div>
  );
};
