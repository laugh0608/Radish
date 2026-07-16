import { type ChangeEvent, type ClipboardEvent, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
  isSupportedAttachmentImageMimeType,
} from '@radish/ui';
import { uploadImage } from '@/api/attachment';
import type { ContentReportTargetType } from '@/api/contentModeration';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import {
  getChannelHistory,
  getChannelList,
  getChannelMessageWindow,
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
import { parseChatWindowParams } from '@/utils/chatNavigation';
import { copyToClipboard } from '@/utils/clipboard';
import { log } from '@/utils/logger';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { ContentReportModal } from '@/components/ContentReportModal';
import type { ChannelMemberVo, ChannelMessageVo, EntityIdValue, SendChannelMessageRequest } from '@/types/chat';
import {
  areEntityIdsEqual,
  isPersistedEntityId,
  isTemporaryEntityId,
  normalizeEntityId,
} from '@/types/chat';
import {
  buildAvatarStyle,
  buildAvatarText,
  buildClientRequestId,
  clearChannelDraft,
  findMentionContext,
  getConnectionHint,
  getEntityKey,
  getErrorMessage,
  getFallbackUserName,
  getReplyTargetMessageId,
  loadChannelDraft,
  MENTION_PATTERN,
  persistChannelDraft,
  resolveAttachmentAssetUrl,
  resolveMediaUrl,
  type MentionContext,
  type MessageFocusTarget,
  type MessageNavigationTarget,
  type PendingImageDraft,
} from './chatApp.helpers';
import { ChatChannelSidebar } from './ChatChannelSidebar';
import { ChatComposerStatus } from './ChatComposerStatus';
import { ChatMemberPanel } from './ChatMemberPanel';
import { ChatMessageList } from './ChatMessageList';
import styles from './ChatApp.module.css';

const PAGE_SIZE = 50;
const MEMBER_REFRESH_INTERVAL_MS = 15_000;
const MESSAGE_HIGHLIGHT_DURATION_MS = 2_600;

function isCompactChatViewport(): boolean {
  return typeof window !== 'undefined' && window.innerWidth <= 720;
}

export interface ChatAppProfileNavigationTarget {
  userId: EntityIdValue;
  userName?: string | null;
  avatarUrl?: string | null;
}

interface ChatAppProps {
  onOpenUserProfile?: (target: ChatAppProfileNavigationTarget) => void;
}

export const ChatApp = ({ onOpenUserProfile }: ChatAppProps = {}) => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const currentWindow = useCurrentWindow();
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
    appendChannelMessages,
    addMessage,
    removeMessage,
    recallMessage,
  } = useChatStore();
  const currentUserId = useUserStore((state) => state.userId);
  const currentUserDisplayName = useUserStore((state) => state.displayName);
  const currentUserDisplayHandle = useUserStore((state) => state.displayHandle);
  const currentUserName = useUserStore((state) => state.userName);
  const currentUserAvatarUrl = useUserStore((state) => state.avatarUrl);
  const windowParams = useMemo(() => parseChatWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const hasRoutedChannel = !!windowParams.channelId;

  const [loadingChannels, setLoadingChannels] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadProgress, setImageUploadProgress] = useState(0);
  const [messageInput, setMessageInput] = useState('');
  const [replyTarget, setReplyTarget] = useState<ChannelMessageVo | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingImageDraft | null>(null);
  const [hasMoreOlderHistory, setHasMoreOlderHistory] = useState<Record<string, boolean>>({});
  const [hasMoreNewerHistory, setHasMoreNewerHistory] = useState<Record<string, boolean>>({});
  const [typingAt, setTypingAt] = useState(0);
  const [mentionContext, setMentionContext] = useState<MentionContext | null>(null);
  const [mentionOptions, setMentionOptions] = useState<UserMentionOption[]>([]);
  const [mentionLoading, setMentionLoading] = useState(false);
  const [mentionSelectedIndex, setMentionSelectedIndex] = useState(0);
  const [onlineMembers, setOnlineMembers] = useState<ChannelMemberVo[]>([]);
  const [memberPanelCollapsed, setMemberPanelCollapsed] = useState(true);
  const [reportTarget, setReportTarget] = useState<{ targetType: ContentReportTargetType; targetId: number } | null>(null);
  const [messageNavigationTarget, setMessageNavigationTarget] = useState<MessageNavigationTarget | null>(null);
  const [messageFocusTarget, setMessageFocusTarget] = useState<MessageFocusTarget | null>(null);
  const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);
  const [isCompactViewport, setIsCompactViewport] = useState(() => isCompactChatViewport());

  const messageScrollRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const messageElementMapRef = useRef(new Map<string, HTMLDivElement>());
  const previousConnectionStateRef = useRef(connectionState);
  const previousChannelIdRef = useRef<EntityIdValue | null>(null);
  const activeChannelIdRef = useRef<EntityIdValue | null>(null);
  const loadedDraftChannelRef = useRef<EntityIdValue | null>(null);
  const handledWindowNavigationRef = useRef<string | null>(null);
  const handledMessageWindowLoadRef = useRef<string | null>(null);
  const windowChannelIdRef = useRef<string | undefined>(windowParams.channelId);
  const messageNavigationTargetRef = useRef<MessageNavigationTarget | null>(null);
  const messageHighlightTimerRef = useRef<number | null>(null);
  const tempMessageIdRef = useRef(-1);
  const composerStateRef = useRef<{ messageInput: string; replyTarget: ChannelMessageVo | null; pendingImage: PendingImageDraft | null }>({
    messageInput: '',
    replyTarget: null,
    pendingImage: null,
  });

  const currentUserIdKey = useMemo(() => getEntityKey(currentUserId), [currentUserId]);
  const currentUserNameValue = useMemo(
    () => currentUserDisplayHandle?.trim()
      || currentUserDisplayName?.trim()
      || currentUserName?.trim()
      || t('common.unknownUser'),
    [currentUserDisplayHandle, currentUserDisplayName, currentUserName, t]
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
  const activeChannelKey = useMemo(() => getEntityKey(activeChannelId), [activeChannelId]);

  const typingUsers = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return (typingMap[getEntityKey(activeChannelId)] || []).filter((user) => !areEntityIdsEqual(user.userId, currentUserId));
  }, [activeChannelId, currentUserId, typingMap]);

  const connectionHint = useMemo(() => getConnectionHint(connectionState, t), [connectionState, t]);

  const isMentionOpen = mentionContext !== null;
  const composerPlaceholder = activeChannelId
    ? t(isCompactViewport ? 'chat.inputPlaceholderMobile' : 'chat.inputPlaceholder')
    : t('chat.inputSelectChannel');

  useEffect(() => {
    const handleResize = () => {
      setIsCompactViewport(isCompactChatViewport());
    };

    handleResize();
    window.addEventListener('resize', handleResize);
    return () => {
      window.removeEventListener('resize', handleResize);
    };
  }, []);

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

  const clearMessageHighlight = useCallback(() => {
    if (messageHighlightTimerRef.current !== null) {
      window.clearTimeout(messageHighlightTimerRef.current);
      messageHighlightTimerRef.current = null;
    }

    setHighlightedMessageId(null);
  }, []);

  const highlightMessage = useCallback((messageId: string) => {
    clearMessageHighlight();
    setHighlightedMessageId(messageId);
    messageHighlightTimerRef.current = window.setTimeout(() => {
      setHighlightedMessageId((current) => (current === messageId ? null : current));
      messageHighlightTimerRef.current = null;
    }, MESSAGE_HIGHLIGHT_DURATION_MS);
  }, [clearMessageHighlight]);

  const setMessageElementRef = useCallback((messageId: string, element: HTMLDivElement | null) => {
    if (!messageId) {
      return;
    }

    if (element) {
      messageElementMapRef.current.set(messageId, element);
      return;
    }

    messageElementMapRef.current.delete(messageId);
  }, []);

  const updateHistoryAvailability = useCallback((
    channelId: EntityIdValue,
    hasMoreBefore: boolean,
    hasMoreAfter: boolean
  ) => {
    const channelKey = getEntityKey(channelId);
    if (!channelKey) {
      return;
    }

    setHasMoreOlderHistory((prev) => ({
      ...prev,
      [channelKey]: hasMoreBefore,
    }));
    setHasMoreNewerHistory((prev) => ({
      ...prev,
      [channelKey]: hasMoreAfter,
    }));
  }, []);

  const handleOpenUserProfile = useCallback((targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => {
    const targetUserIdKey = getEntityKey(targetUserId);
    if (!targetUserIdKey) {
      return;
    }

    if (onOpenUserProfile) {
      onOpenUserProfile({
        userId: targetUserId,
        userName: targetUserName ?? null,
        avatarUrl: avatarUrl ?? null,
      });
      return;
    }

    if (targetUserIdKey === currentUserIdKey) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName?.trim() || getFallbackUserName(targetUserIdKey, t),
      avatarUrl: avatarUrl ?? null,
    });
  }, [currentUserIdKey, onOpenUserProfile, openApp, t]);

  const renderAvatarButton = useCallback((
    targetUserId: EntityIdValue,
    targetUserName: string | null | undefined,
    avatarUrl: string | null | undefined,
    className?: string
  ) => {
    const targetUserIdKey = getEntityKey(targetUserId);
    const normalizedName = targetUserName?.trim() || getFallbackUserName(targetUserIdKey, t);
    const resolvedAvatarUrl = resolveMediaUrl(apiBaseUrl, avatarUrl);
    const canOpenProfile = !!targetUserIdKey && targetUserIdKey !== '0' && !targetUserIdKey.startsWith('-');

    return (
      <button
        type="button"
        className={`${styles.avatarButton} ${className ?? ''}`.trim()}
        onClick={() => {
          if (canOpenProfile) {
            handleOpenUserProfile(targetUserId, normalizedName, avatarUrl);
          }
        }}
        disabled={!canOpenProfile}
        title={canOpenProfile ? t('chat.viewProfile', { name: normalizedName }) : t('chat.userUnavailable')}
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
  }, [apiBaseUrl, handleOpenUserProfile, t]);

  const renderAvatarVisual = useCallback((
    targetUserName: string | null | undefined,
    avatarUrl: string | null | undefined,
    className?: string
  ) => {
    const normalizedName = targetUserName?.trim() || t('common.unknownUser');
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
  }, [apiBaseUrl, t]);

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
      const mentionName = match.groups?.name ?? match[1] ?? t('common.unknownUser');
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
  }, [handleOpenUserProfile, t]);

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

    const optionDisplayName = resolveVisibleUserDisplayName(option, getFallbackUserName(targetId, t));
    const targetName = resolveVisibleUserHandle(option, optionDisplayName) || optionDisplayName;
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
  }, [closeMentionDropdown, mentionContext, messageInput, t]);

  const loadChannels = useCallback(async () => {
    setLoadingChannels(true);
    try {
      const data = await getChannelList();
      setChannels(data);

      if (data.length === 0) {
        setActiveChannel(null);
        return;
      }

      const targetWindowChannelId = windowChannelIdRef.current;
      const currentActiveChannelId = activeChannelIdRef.current;
      const nextActiveChannelId = (
        targetWindowChannelId && data.some((channel) => areEntityIdsEqual(channel.voId, targetWindowChannelId))
          ? targetWindowChannelId
          : currentActiveChannelId && data.some((channel) => areEntityIdsEqual(channel.voId, currentActiveChannelId))
            ? normalizeEntityId(currentActiveChannelId)
            : normalizeEntityId(data[0].voId)
      );

      if (nextActiveChannelId) {
        setActiveChannel(nextActiveChannelId);
      }
    } catch (error) {
      log.error('ChatApp', '加载频道列表失败:', error);
      toast.error(t('chat.loadChannelsFailed'));
    } finally {
      setLoadingChannels(false);
    }
  }, [setChannels, setActiveChannel, t]);

  const loadInitialHistory = useCallback(async (channelId: EntityIdValue) => {
    const channelKey = getEntityKey(channelId);
    if (!channelKey) {
      return;
    }

    setLoadingHistory(true);

    try {
      const history = await getChannelHistory(channelId, { pageSize: PAGE_SIZE });
      setChannelMessages(channelId, history);
      updateHistoryAvailability(channelId, history.length >= PAGE_SIZE, false);

      requestAnimationFrame(() => {
        scrollToBottom();
      });

      await chatHub.markChannelAsRead(channelId);
    } catch (error) {
      log.error('ChatApp', '加载历史消息失败:', error);
      toast.error(t('chat.loadHistoryFailed'));
    } finally {
      setLoadingHistory(false);
    }
  }, [scrollToBottom, setChannelMessages, t, updateHistoryAvailability]);

  const loadMessageWindow = useCallback(async (target: MessageNavigationTarget) => {
    const channelId = target.channelId;
    if (!channelId) {
      return;
    }

    let shouldFallbackToLatest = false;
    setLoadingHistory(true);

    try {
      const windowData = await getChannelMessageWindow(channelId, target.messageId);
      setChannelMessages(channelId, windowData.voMessages);
      updateHistoryAvailability(channelId, windowData.voHasMoreBefore, windowData.voHasMoreAfter);
      setMessageFocusTarget({
        channelId,
        messageId: getEntityKey(windowData.voAnchorMessageId),
        signature: target.signature,
      });
      if (messageNavigationTargetRef.current?.signature === target.signature) {
        messageNavigationTargetRef.current = null;
      }
      setMessageNavigationTarget((current) => (current?.signature === target.signature ? null : current));

      if (!windowData.voHasMoreAfter) {
        await chatHub.markChannelAsRead(channelId);
      }
    } catch (error) {
      shouldFallbackToLatest = true;
      if (messageNavigationTargetRef.current?.signature === target.signature) {
        messageNavigationTargetRef.current = null;
      }
      setMessageNavigationTarget((current) => (current?.signature === target.signature ? null : current));
      log.error('ChatApp', '加载目标消息窗口失败:', error);
      toast.error(error instanceof Error ? error.message : t('chat.messageNavigationNotFound'));
    } finally {
      setLoadingHistory(false);
    }

    if (shouldFallbackToLatest) {
      await loadInitialHistory(channelId);
    }
  }, [loadInitialHistory, setChannelMessages, t, updateHistoryAvailability]);

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

  const loadOlderHistory = useCallback(async () => {
    if (!activeChannelId || !activeChannelKey || loadingHistory || !hasMoreOlderHistory[activeChannelKey]) {
      return 'skipped' as const;
    }

    const oldestMessage = activeMessages[0];
    const beforeMessageId = oldestMessage?.voId;
    if (!isPersistedEntityId(beforeMessageId)) {
      setHasMoreOlderHistory((prev) => ({
        ...prev,
        [activeChannelKey]: false,
      }));
      return 'exhausted' as const;
    }

    setLoadingHistory(true);
    try {
      const older = await getChannelHistory(activeChannelId, {
        beforeMessageId,
        pageSize: PAGE_SIZE,
      });
      prependChannelMessages(activeChannelId, older);
      setHasMoreOlderHistory((prev) => ({
        ...prev,
        [activeChannelKey]: older.length >= PAGE_SIZE,
      }));
      return older.length > 0 ? ('loaded' as const) : ('exhausted' as const);
    } catch (error) {
      log.error('ChatApp', '加载更多历史消息失败:', error);
      toast.error(t('chat.loadMoreHistoryFailed'));
      return 'failed' as const;
    } finally {
      setLoadingHistory(false);
    }
  }, [activeChannelId, activeChannelKey, activeMessages, hasMoreOlderHistory, loadingHistory, prependChannelMessages, t]);

  const loadNewerHistory = useCallback(async (options?: { scrollToBottomWhenDone?: boolean }) => {
    if (!activeChannelId || !activeChannelKey || loadingHistory || !hasMoreNewerHistory[activeChannelKey]) {
      return 'skipped' as const;
    }

    const newestMessage = activeMessages[activeMessages.length - 1];
    const afterMessageId = newestMessage?.voId;
    if (!isPersistedEntityId(afterMessageId)) {
      setHasMoreNewerHistory((prev) => ({
        ...prev,
        [activeChannelKey]: false,
      }));
      return 'exhausted' as const;
    }

    setLoadingHistory(true);
    try {
      const newer = await getChannelHistory(activeChannelId, {
        afterMessageId,
        pageSize: PAGE_SIZE,
      });
      appendChannelMessages(activeChannelId, newer);
      const hasMoreAfter = newer.length >= PAGE_SIZE;
      setHasMoreNewerHistory((prev) => ({
        ...prev,
        [activeChannelKey]: hasMoreAfter,
      }));

      if (options?.scrollToBottomWhenDone !== false) {
        requestAnimationFrame(() => {
          scrollToBottom();
        });
      }

      if (!hasMoreAfter) {
        await chatHub.markChannelAsRead(activeChannelId);
      }

      return newer.length > 0 ? ('loaded' as const) : ('exhausted' as const);
    } catch (error) {
      log.error('ChatApp', '加载较新消息失败:', error);
      toast.error(t('chat.loadNewerHistoryFailed'));
      return 'failed' as const;
    } finally {
      setLoadingHistory(false);
    }
  }, [
    activeChannelId,
    activeChannelKey,
    activeMessages,
    appendChannelMessages,
    hasMoreNewerHistory,
    loadingHistory,
    scrollToBottom,
    t,
  ]);

  const handleOpenReport = useCallback((targetType: ContentReportTargetType, targetId: number) => {
    if (!currentUserIdKey) {
      toast.error(t('report.loginRequired'));
      return;
    }

    if (targetId <= 0) {
      return;
    }

    setReportTarget({ targetType, targetId });
  }, [currentUserIdKey, t]);

  const resetComposer = useCallback((channelId: EntityIdValue) => {
    setMessageInput('');
    setReplyTarget(null);
    setPendingImage(null);
    closeMentionDropdown();
    clearChannelDraft(currentUserIdKey, channelId);
  }, [closeMentionDropdown, currentUserIdKey]);

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
  }): ChannelMessageVo => {
    const fallbackImageUrl = params.imageUrl ?? resolveAttachmentAssetUrl(params.attachmentId, 'original');
    const fallbackThumbnailUrl = params.imageThumbnailUrl
      ?? resolveAttachmentAssetUrl(params.attachmentId, 'thumbnail')
      ?? fallbackImageUrl;

    return {
    voId: params.tempMessageId,
    voClientRequestId: params.clientRequestId,
    voChannelId: params.channelId,
    voUserId: currentUserIdKey,
    voUserName: currentUserNameValue,
    voUserAvatarUrl: currentUserAvatarUrlValue,
    voType: params.type,
    voContent: params.content ?? null,
    voReplyToId: params.replyTo ? params.replyTo.voId : null,
    voReplyTo: params.replyTo,
    voAttachmentId: params.attachmentId ?? null,
    voImageUrl: fallbackImageUrl ?? null,
    voImageThumbnailUrl: fallbackThumbnailUrl ?? null,
    voIsRecalled: false,
    voCreateTime: new Date().toISOString(),
    voLocalStatus: 'sending',
    voLocalError: null,
    };
  }, [currentUserAvatarUrlValue, currentUserIdKey, currentUserNameValue]);

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
      const errorMessage = getErrorMessage(error, options?.failureFallbackMessage || t('chat.sendFailed'));
      log.error('ChatApp', '发送消息失败:', error);
      addMessage({
        ...optimisticMessage,
        voLocalStatus: 'failed',
        voLocalError: errorMessage,
      });
      toast.error(errorMessage);
    }
  }, [addMessage, scrollToBottom, t]);

  const handleSendMessage = useCallback(() => {
    if (!activeChannelId || uploadingImage) {
      return;
    }

    const content = messageInput.trim();
    const pendingImageSnapshot = pendingImage;
    if (!content && !pendingImageSnapshot) {
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
      type: pendingImageSnapshot ? 2 : 1,
      content: content || undefined,
      replyTo: replyTargetSnapshot,
      attachmentId: pendingImageSnapshot?.attachmentId,
      imageUrl: pendingImageSnapshot?.imageUrl,
      imageThumbnailUrl: pendingImageSnapshot?.imageThumbnailUrl || pendingImageSnapshot?.imageUrl,
    });

    resetComposer(activeChannelId);

    void sendOptimisticMessage(
      optimisticMessage,
      {
        channelId: activeChannelId,
        type: pendingImageSnapshot ? 2 : 1,
        content: content || undefined,
        replyToId: replyTargetId ?? undefined,
        attachmentId: pendingImageSnapshot?.attachmentId,
      },
      {
        failureFallbackMessage: pendingImageSnapshot ? t('chat.imageSendFailed') : t('chat.sendFailed'),
      }
    );
  }, [
    activeChannelId,
    createOptimisticMessage,
    createTempMessageId,
    messageInput,
    pendingImage,
    replyTarget,
    resetComposer,
    sendOptimisticMessage,
    t,
    uploadingImage,
  ]);

  const uploadImageToPendingDraft = useCallback(async (
    file: File,
    targetChannelId: EntityIdValue,
    draftContentSnapshot: string,
    draftReplyTargetSnapshot: ChannelMessageVo | null
  ) => {
    if (!targetChannelId || uploadingImage) {
      return;
    }

    if (!isSupportedAttachmentImageFile(file)) {
      toast.error(t('chat.imageSendFailed'));
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

      const nextPendingImage: PendingImageDraft = {
        attachmentId: attachment.voId,
        imageUrl: resolveAttachmentAssetUrl(attachment.voId, 'original') || attachment.voUrl,
        imageThumbnailUrl: (
          resolveAttachmentAssetUrl(attachment.voId, 'thumbnail')
          || resolveAttachmentAssetUrl(attachment.voId, 'original')
          || attachment.voThumbnailUrl
          || attachment.voUrl
        ),
        fileName: attachment.voOriginalName || file.name,
      };

      setImageUploadProgress(100);

      if (areEntityIdsEqual(activeChannelIdRef.current, targetChannelId)) {
        setPendingImage(nextPendingImage);
      } else {
        persistChannelDraft(
          currentUserIdKey,
          targetChannelId,
          draftContentSnapshot,
          draftReplyTargetSnapshot,
          nextPendingImage
        );
      }
    } catch (error) {
      log.error('ChatApp', '上传聊天室图片失败:', error);
      toast.error(getErrorMessage(error, t('chat.imageSendFailed')));
    } finally {
      setUploadingImage(false);
      setTimeout(() => setImageUploadProgress(0), 400);
    }
  }, [currentUserIdKey, t, uploadingImage]);

  const handleImageSelected = useCallback(async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !activeChannelId) {
      return;
    }

    try {
      await uploadImageToPendingDraft(file, activeChannelId, messageInput, replyTarget);
    } finally {
      event.target.value = '';
    }
  }, [activeChannelId, messageInput, replyTarget, uploadImageToPendingDraft]);

  const handleComposerPaste = useCallback((event: ClipboardEvent<HTMLTextAreaElement>) => {
    if (!activeChannelId || uploadingImage) {
      return;
    }

    const clipboardItems = Array.from(event.clipboardData.items);
    const imageItem = clipboardItems.find((item) => isSupportedAttachmentImageMimeType(item.type));
    const pastedImage = imageItem?.getAsFile();

    if (!pastedImage) {
      return;
    }

    event.preventDefault();
    void uploadImageToPendingDraft(pastedImage, activeChannelId, messageInput, replyTarget);
  }, [activeChannelId, messageInput, replyTarget, uploadImageToPendingDraft, uploadingImage]);

  const handleRemovePendingImage = useCallback(() => {
    setPendingImage(null);
  }, []);

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
      },
      {
        failureFallbackMessage: message.voType === 2 ? t('chat.imageSendFailed') : t('chat.sendFailed'),
      }
    );
  }, [sendOptimisticMessage, t]);

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

  const handleCopyFailedMessageDiagnostics = useCallback(async (message: ChannelMessageVo) => {
    const diagnosticLines = [
      'Radish chat delivery diagnostic',
      `channelId: ${getEntityKey(message.voChannelId) || 'unknown'}`,
      `messageId: ${getEntityKey(message.voId) || 'unknown'}`,
      `clientRequestId: ${message.voClientRequestId || 'none'}`,
      `status: ${message.voLocalStatus ?? 'sent'}`,
      `type: ${message.voType}`,
      `createdAt: ${message.voCreateTime || 'unknown'}`,
      `error: ${message.voLocalError || t('chat.sendFailed')}`,
      `hasContent: ${message.voContent?.trim() ? 'yes' : 'no'}`,
      `hasAttachment: ${isPersistedEntityId(message.voAttachmentId) ? 'yes' : 'no'}`,
      `path: ${window.location.pathname}${window.location.search}${window.location.hash}`,
    ];

    try {
      await copyToClipboard(diagnosticLines.join('\n'));
      toast.success(t('chat.diagnosticsCopied'));
    } catch (error) {
      log.warn('ChatApp', '复制聊天发送失败诊断失败', error);
      toast.error(t('chat.diagnosticsCopyFailed'));
    }
  }, [t]);

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
      toast.success(t('chat.messageRecalled'));
    } catch (error) {
      log.error('ChatApp', '撤回消息失败:', error);
      toast.error(error instanceof Error ? error.message : t('chat.recallFailed'));
    }
  }, [activeChannelId, recallMessage, replyTarget, t]);

  const handleScroll = useCallback(async () => {
    const scrollEl = messageScrollRef.current;
    if (!scrollEl || !activeChannelId || !activeChannelKey) {
      return;
    }

    if (scrollEl.scrollTop <= 24 && !loadingHistory) {
      await loadOlderHistory();
    }

    const distanceToBottom = scrollEl.scrollHeight - (scrollEl.scrollTop + scrollEl.clientHeight);
    if (distanceToBottom <= 24 && !hasMoreNewerHistory[activeChannelKey]) {
      await chatHub.markChannelAsRead(activeChannelId);
    }
  }, [activeChannelId, activeChannelKey, hasMoreNewerHistory, loadOlderHistory, loadingHistory]);

  useEffect(() => {
    composerStateRef.current = {
      messageInput,
      replyTarget,
      pendingImage,
    };
  }, [messageInput, pendingImage, replyTarget]);

  useEffect(() => {
    messageNavigationTargetRef.current = messageNavigationTarget;
  }, [messageNavigationTarget]);

  useEffect(() => (
    () => {
      if (messageHighlightTimerRef.current !== null) {
        window.clearTimeout(messageHighlightTimerRef.current);
      }
    }
  ), []);

  useEffect(() => {
    activeChannelIdRef.current = activeChannelId;
  }, [activeChannelId]);

  useEffect(() => {
    windowChannelIdRef.current = windowParams.channelId;
  }, [windowParams.channelId]);

  useEffect(() => {
    void loadChannels();
  }, [loadChannels]);

  useEffect(() => {
    if (!windowParams.channelId) {
      messageNavigationTargetRef.current = null;
      setMessageNavigationTarget(null);
      setMessageFocusTarget(null);
      return;
    }

    if (channels.length === 0) {
      return;
    }

    if (!channels.some((channel) => areEntityIdsEqual(channel.voId, windowParams.channelId))) {
      messageNavigationTargetRef.current = null;
      setMessageNavigationTarget(null);
      setMessageFocusTarget(null);
      return;
    }

    const navigationSignature = `${windowParams.channelId}:${windowParams.messageId ?? 'none'}:${windowParams.navigationKey ?? 'initial'}`;
    if (handledWindowNavigationRef.current === navigationSignature) {
      return;
    }

    handledWindowNavigationRef.current = navigationSignature;
    clearMessageHighlight();
    setMessageFocusTarget(null);

    const nextNavigationTarget = windowParams.messageId
      ? {
          channelId: windowParams.channelId,
          messageId: windowParams.messageId,
          signature: navigationSignature,
        }
      : null;
    messageNavigationTargetRef.current = nextNavigationTarget;
    setMessageNavigationTarget(nextNavigationTarget);

    if (!areEntityIdsEqual(activeChannelId, windowParams.channelId)) {
      setActiveChannel(windowParams.channelId);
    }
  }, [activeChannelId, channels, clearMessageHighlight, setActiveChannel, windowParams.channelId, windowParams.messageId, windowParams.navigationKey]);

  useEffect(() => {
    void chatHub.start();
  }, []);

  useEffect(() => {
    const previousChannelId = previousChannelIdRef.current;
    if (previousChannelId && !areEntityIdsEqual(previousChannelId, activeChannelId)) {
      persistChannelDraft(
        currentUserIdKey,
        previousChannelId,
        composerStateRef.current.messageInput,
        composerStateRef.current.replyTarget,
        composerStateRef.current.pendingImage
      );
    }

    previousChannelIdRef.current = activeChannelId;

    if (!activeChannelId) {
      setMessageInput('');
      setReplyTarget(null);
      setPendingImage(null);
      setOnlineMembers([]);
      loadedDraftChannelRef.current = null;
      setMessageFocusTarget(null);
      closeMentionDropdown();
      clearMessageHighlight();
      return;
    }

    const draft = loadChannelDraft(currentUserIdKey, activeChannelId);
    setMessageInput(draft?.content ?? '');
    setReplyTarget(draft?.replyTarget ?? null);
    setPendingImage(draft?.pendingImage ?? null);
    closeMentionDropdown();
    loadedDraftChannelRef.current = activeChannelId;

    void chatHub.joinChannel(activeChannelId);
    const pendingNavigationTarget = messageNavigationTargetRef.current;
    if (pendingNavigationTarget && areEntityIdsEqual(pendingNavigationTarget.channelId, activeChannelId)) {
      handledMessageWindowLoadRef.current = pendingNavigationTarget.signature;
      void loadMessageWindow(pendingNavigationTarget);
    } else {
      void loadInitialHistory(activeChannelId);
    }

    const initialMemberTimer = window.setTimeout(() => {
      void loadOnlineMembers(activeChannelId);
    }, 240);

    return () => {
      window.clearTimeout(initialMemberTimer);
      void chatHub.leaveChannel(activeChannelId);
    };
  }, [activeChannelId, clearMessageHighlight, closeMentionDropdown, currentUserIdKey, loadInitialHistory, loadMessageWindow, loadOnlineMembers]);

  useEffect(() => {
    if (!activeChannelId || !areEntityIdsEqual(loadedDraftChannelRef.current, activeChannelId)) {
      return;
    }

    persistChannelDraft(currentUserIdKey, activeChannelId, messageInput, replyTarget, pendingImage);
  }, [activeChannelId, currentUserIdKey, messageInput, pendingImage, replyTarget]);

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

    if (messageNavigationTarget && areEntityIdsEqual(messageNavigationTarget.channelId, activeChannelId)) {
      return;
    }
    if (messageFocusTarget && areEntityIdsEqual(messageFocusTarget.channelId, activeChannelId)) {
      return;
    }

    if (activeChannelKey && hasMoreNewerHistory[activeChannelKey]) {
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
  }, [activeChannelId, activeChannelKey, activeMessages, hasMoreNewerHistory, messageFocusTarget, messageNavigationTarget, scrollToBottom]);

  useEffect(() => {
    if (!messageNavigationTarget || !activeChannelId || loadingHistory) {
      return;
    }

    if (!areEntityIdsEqual(activeChannelId, messageNavigationTarget.channelId)) {
      return;
    }

    if (handledMessageWindowLoadRef.current === messageNavigationTarget.signature) {
      return;
    }

    handledMessageWindowLoadRef.current = messageNavigationTarget.signature;
    void loadMessageWindow(messageNavigationTarget);
  }, [activeChannelId, loadMessageWindow, loadingHistory, messageNavigationTarget]);

  useEffect(() => {
    if (!messageFocusTarget || !activeChannelId) {
      return;
    }

    if (!areEntityIdsEqual(activeChannelId, messageFocusTarget.channelId)) {
      return;
    }

    const targetElement = messageElementMapRef.current.get(messageFocusTarget.messageId);
    if (!targetElement) {
      return;
    }

    setMessageFocusTarget((current) => (current?.signature === messageFocusTarget.signature ? null : current));
    requestAnimationFrame(() => {
      targetElement.scrollIntoView({
        block: 'center',
        behavior: 'smooth',
      });
      highlightMessage(messageFocusTarget.messageId);
    });
  }, [activeChannelId, activeMessages, highlightMessage, messageFocusTarget]);

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
      updateHistoryAvailability(activeChannelId, false, false);
      const pendingNavigationTarget = messageNavigationTargetRef.current;
      if (pendingNavigationTarget && areEntityIdsEqual(pendingNavigationTarget.channelId, activeChannelId)) {
        handledMessageWindowLoadRef.current = pendingNavigationTarget.signature;
        void loadMessageWindow(pendingNavigationTarget);
      } else {
        void loadInitialHistory(activeChannelId);
      }
      void loadOnlineMembers(activeChannelId);
    }

    previousConnectionStateRef.current = connectionState;
  }, [activeChannelId, connectionState, loadInitialHistory, loadMessageWindow, loadOnlineMembers, setChannelMessages, updateHistoryAvailability]);

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
    }, 120);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [mentionContext, t]);

  const pendingImagePreviewUrl = resolveMediaUrl(
    apiBaseUrl,
    pendingImage?.imageThumbnailUrl || pendingImage?.imageUrl
  );
  const hasPendingImage = !!pendingImage;
  const hasComposerContent = !!messageInput.trim() || hasPendingImage;

  return (
    <div className={`${styles.chatApp} ${hasRoutedChannel ? styles.chatAppFocused : ''}`}>
      <ChatChannelSidebar
        channels={channels}
        activeChannelId={activeChannelId}
        loadingChannels={loadingChannels}
        onSelectChannel={setActiveChannel}
      />

      <section className={styles.main}>
        <header className={styles.mainHeader}>
          <div className={styles.headerMain}>
            {hasRoutedChannel && (
              <a className={styles.mobileBackLink} href="/messages">
                <Icon icon="mdi:chevron-left" size={18} />
                <span>{t('chat.backToConversations')}</span>
              </a>
            )}
            <div>
              <div className={styles.channelTitle}>
                {activeChannel ? `${activeChannel.voIconEmoji || '#'} ${activeChannel.voName}` : t('chat.selectChannel')}
              </div>
              {activeChannel?.voDescription && (
                <div className={styles.channelDescription}>{activeChannel.voDescription}</div>
              )}
            </div>

          </div>

          {activeChannelId !== null && connectionHint && (
            <div className={styles.connectionBanner}>
              <strong>{connectionHint}</strong>
              <span>{t('chat.connection.recoveryHint')}</span>
            </div>
          )}
        </header>

        <div className={styles.contentArea}>
          <div className={styles.messageColumn}>
            <ChatMessageList
              activeChannelId={activeChannelId}
              activeChannelKey={activeChannelKey}
              messages={activeMessages}
              loadingHistory={loadingHistory}
              highlightedMessageId={highlightedMessageId}
              currentUserIdKey={currentUserIdKey}
              apiBaseUrl={apiBaseUrl}
              hasMoreNewerHistory={hasMoreNewerHistory}
              messageScrollRef={messageScrollRef}
              setMessageElementRef={setMessageElementRef}
              renderAvatarButton={renderAvatarButton}
              renderMessageContent={renderMessageContent}
              onScroll={() => { void handleScroll(); }}
              onOpenUserProfile={handleOpenUserProfile}
              onReply={setReplyTarget}
              onRecall={(messageId) => { void handleRecall(messageId); }}
              onOpenReport={handleOpenReport}
              onRetryMessage={handleRetryMessage}
              onCopyFailedMessageDiagnostics={handleCopyFailedMessageDiagnostics}
              onDismissFailedMessage={handleDismissFailedMessage}
              onLoadNewerHistory={() => { void loadNewerHistory({ scrollToBottomWhenDone: true }); }}
            />

            <footer className={styles.inputArea}>
              <ChatComposerStatus
                typingUsers={typingUsers}
                replyTarget={replyTarget}
                pendingImage={pendingImage}
                pendingImagePreviewUrl={pendingImagePreviewUrl}
                activeChannelId={activeChannelId}
                hasComposerContent={hasComposerContent}
                uploadingImage={uploadingImage}
                imageUploadProgress={imageUploadProgress}
                onOpenUserProfile={handleOpenUserProfile}
                onCancelReply={() => setReplyTarget(null)}
                onRemovePendingImage={handleRemovePendingImage}
              />

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
                    onPaste={handleComposerPaste}
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
                    placeholder={composerPlaceholder}
                    disabled={!activeChannelId}
                  />

                  {isMentionOpen && (
                    <div className={styles.mentionDropdown}>
                      <div className={styles.mentionHeader}>
                        <span className={styles.mentionTitle}>{t('chat.mentionTitle')}</span>
                        <span className={styles.mentionHint}>{t('chat.mentionHint')}</span>
                      </div>
                      {!mentionContext?.keyword.trim() ? (
                        <div className={styles.mentionState}>{t('chat.mentionTypeToSearch')}</div>
                      ) : mentionLoading ? (
                        <div className={styles.mentionState}>{t('chat.mentionSearching')}</div>
                      ) : mentionOptions.length === 0 ? (
                        <div className={styles.mentionState}>{t('chat.mentionNotFound')}</div>
                      ) : (
                        mentionOptions.map((option, index) => {
                          const optionId = normalizeEntityId(option.voId) ?? `mention-${index}`;
                          const optionDisplayName = resolveVisibleUserDisplayName(option, getFallbackUserName(optionId, t));
                          const optionName = resolveVisibleUserHandle(option, optionDisplayName) || optionDisplayName;
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
                              <span className={styles.mentionMeta}>
                                <span className={styles.mentionLabel}>@{optionName}</span>
                                {optionDisplayName !== optionName ? (
                                  <span className={styles.mentionDisplayName}>{optionDisplayName}</span>
                                ) : null}
                              </span>
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
                    accept={attachmentImageAccept}
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
                    {uploadingImage ? t('chat.uploading') : t('chat.image')}
                  </button>
                  <button
                    className={styles.sendButton}
                    type="button"
                    disabled={!activeChannelId || uploadingImage || !hasComposerContent}
                    onClick={() => {
                      void handleSendMessage();
                    }}
                  >
                    {t('chat.send')}
                  </button>
                </div>
              </div>
            </footer>
          </div>

          {activeChannelId !== null && (
            <ChatMemberPanel
              collapsed={memberPanelCollapsed}
              members={onlineMembers}
              loading={loadingMembers}
              onToggleCollapsed={() => setMemberPanelCollapsed((current) => !current)}
              onOpenUserProfile={handleOpenUserProfile}
              renderAvatarVisual={renderAvatarVisual}
            />
          )}
        </div>
      </section>

      {reportTarget && (
        <ContentReportModal
          isOpen={!!reportTarget}
          targetType={reportTarget.targetType}
          targetId={reportTarget.targetId}
          onClose={() => setReportTarget(null)}
        />
      )}
    </div>
  );
};
