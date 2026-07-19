import { type ChangeEvent, type ClipboardEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelMessageSearchItemVo } from '@radish/http';
import { toast } from '@radish/ui/toast';
import { Icon } from '@radish/ui/icon';
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
  persistChannelDraft,
  resolveAttachmentAssetUrl,
  resolveMediaUrl,
  type MentionContext,
  type MessageNavigationTarget,
  type PendingImageDraft,
} from './chatApp.helpers';
import { ChatChannelSidebar } from './ChatChannelSidebar';
import { ChatComposerStatus } from './ChatComposerStatus';
import { ChatConversationHeader } from './ChatConversationHeader';
import { ChatMemberPanel } from './ChatMemberPanel';
import { ChatMentionMenu } from './ChatMentionMenu';
import { ChatMessageContent } from './ChatMessageContent';
import { ChatMessageList } from './ChatMessageList';
import { ChatMessageSearchPanel } from './ChatMessageSearchPanel';
import { canSendConversationAttachment, resolveConversationNoticeKey } from './chatConversationPresentation';
import { useChatConversationWorkspace } from './useChatConversationWorkspace';
import { useChatMessageNavigation } from './useChatMessageNavigation';
import styles from './ChatApp.module.css';
import searchStyles from './ChatSearchControls.module.css';

const PAGE_SIZE = 50;
const MEMBER_REFRESH_INTERVAL_MS = 15_000;

function isCompactChatViewport(): boolean {
  return typeof window !== 'undefined' && window.innerWidth <= 720;
}

export interface ChatAppProfileNavigationTarget {
  userId: EntityIdValue;
  publicId?: string | null;
  userName?: string | null;
  avatarUrl?: string | null;
}

interface ChatAppProps {
  onOpenUserProfile?: (target: ChatAppProfileNavigationTarget) => void;
  onOpenFocusedChannel?: (channelId: string) => void;
  onOpenMessageResult?: (target: { channelId: string; messageId: string }) => void;
  onBackToConversationList?: () => void;
  searchRestoreRevision?: number;
  searchHideRevision?: number;
  onSearchVisibilityChange?: (visible: boolean) => void;
}

export const ChatApp = ({
  onOpenUserProfile,
  onOpenFocusedChannel,
  onOpenMessageResult,
  onBackToConversationList,
  searchRestoreRevision = 0,
  searchHideRevision = 0,
  onSearchVisibilityChange,
}: ChatAppProps = {}) => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const currentWindow = useCurrentWindow();
  const { openApp } = useWindowStore();
  const {
    messageMap,
    recalledMessageIds,
    typingMap,
    connectionState,
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
  const {
    channels,
    activeChannelId,
    listView,
    loadingChannels,
    listError,
    routeUnavailable,
    selectChannel,
    changeListView,
    reloadConversations,
    handleConversationChanged,
  } = useChatConversationWorkspace({
    routedChannelId: windowParams.channelId,
    onOpenFocusedChannel,
  });

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
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchMounted, setSearchMounted] = useState(false);
  const [reportTarget, setReportTarget] = useState<{ targetType: ContentReportTargetType; targetId: number } | null>(null);
  const [isCompactViewport, setIsCompactViewport] = useState(() => isCompactChatViewport());

  const messageScrollRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const previousConnectionStateRef = useRef(connectionState);
  const previousChannelIdRef = useRef<EntityIdValue | null>(null);
  const activeChannelIdRef = useRef<EntityIdValue | null>(null);
  const loadedDraftChannelRef = useRef<EntityIdValue | null>(null);
  const handledMessageWindowLoadRef = useRef<string | null>(null);
  const handledSearchHideRevisionRef = useRef(0);
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
  const canSendInActiveChannel = Boolean(activeChannel?.voCanSend);
  const canSendAttachment = canSendConversationAttachment(activeChannel);
  const conversationNoticeKey = resolveConversationNoticeKey(activeChannel);
  const isDirectRequestFirstMessage = activeChannel?.voDirectRequestStatus === 'pending' && activeChannel.voCanSend;

  const activeMessages = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return messageMap[getEntityKey(activeChannelId)] || [];
  }, [activeChannelId, messageMap]);
  const activeChannelKey = useMemo(() => getEntityKey(activeChannelId), [activeChannelId]);
  const {
    navigationTarget: messageNavigationTarget,
    navigationTargetRef: messageNavigationTargetRef,
    focusTarget: messageFocusTarget,
    highlightedMessageId,
    targetUnavailable: messageTargetUnavailable,
    setMessageElementRef,
    clearMessageHighlight,
    clearNavigation: clearMessageNavigation,
    completeMessageWindow,
    failMessageWindow,
    navigateToMessage,
  } = useChatMessageNavigation({
    routedChannelId: windowParams.channelId,
    routedMessageId: windowParams.messageId,
    navigationKey: windowParams.navigationKey,
    channels,
    activeChannelId,
    activeMessages,
    setActiveChannel,
  });

  const typingUsers = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return (typingMap[getEntityKey(activeChannelId)] || []).filter((user) => !areEntityIdsEqual(user.userId, currentUserId));
  }, [activeChannelId, currentUserId, typingMap]);

  const connectionHint = useMemo(() => getConnectionHint(connectionState, t), [connectionState, t]);

  const isMentionOpen = mentionContext !== null;
  const composerPlaceholder = !activeChannelId
    ? t('chat.inputSelectChannel')
    : !canSendInActiveChannel
      ? t(conversationNoticeKey || 'chat.inputConversationUnavailable')
      : isDirectRequestFirstMessage
        ? t('chat.inputDirectRequest')
        : t(isCompactViewport ? 'chat.inputPlaceholderMobile' : 'chat.inputPlaceholder');

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

  useEffect(() => {
    if (searchRestoreRevision > 0) {
      setMemberPanelCollapsed(true);
      setSearchMounted(true);
      setSearchOpen(true);
    }
  }, [searchRestoreRevision]);

  useEffect(() => {
    if (searchHideRevision <= handledSearchHideRevisionRef.current) {
      return;
    }

    handledSearchHideRevisionRef.current = searchHideRevision;
    if (isCompactViewport) {
      setSearchOpen(false);
    }
  }, [isCompactViewport, searchHideRevision]);

  useEffect(() => {
    onSearchVisibilityChange?.(searchOpen);
  }, [onSearchVisibilityChange, searchOpen]);

  useEffect(() => () => {
    onSearchVisibilityChange?.(false);
  }, [onSearchVisibilityChange]);

  const openMessageSearch = useCallback(() => {
    setMemberPanelCollapsed(true);
    setSearchMounted(true);
    setSearchOpen(true);
  }, []);

  const handleOpenSearchResult = useCallback((item: ChannelMessageSearchItemVo) => {
    const target = {
      channelId: item.voChannelId,
      messageId: item.voMessageId,
    };
    if (onOpenMessageResult) {
      onOpenMessageResult(target);
    } else {
      openApp('chat', {
        ...target,
        __navigationKey: `search:${target.channelId}:${target.messageId}:${Date.now()}`,
      });
      navigateToMessage(target.channelId, target.messageId);
    }

    if (isCompactViewport) {
      setSearchOpen(false);
    }
  }, [isCompactViewport, navigateToMessage, onOpenMessageResult, openApp]);

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

  const handleOpenUserProfile = useCallback((targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null, publicId?: string | null) => {
    const targetUserIdKey = getEntityKey(targetUserId);
    if (!targetUserIdKey) {
      return;
    }

    if (onOpenUserProfile) {
      onOpenUserProfile({ userId: targetUserId, publicId, userName: targetUserName ?? null, avatarUrl: avatarUrl ?? null });
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

  const renderMessageContent = useCallback((content: string | null | undefined) => (
    <ChatMessageContent content={content} onOpenUserProfile={handleOpenUserProfile} />
  ), [handleOpenUserProfile]);

  const updateMentionContext = useCallback((text: string, cursor: number) => {
    if (isDirectRequestFirstMessage) {
      closeMentionDropdown();
      return;
    }

    const context = findMentionContext(text, cursor);
    if (!context) {
      closeMentionDropdown();
      return;
    }

    setMentionContext(context);
  }, [closeMentionDropdown, isDirectRequestFirstMessage]);

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

    setLoadingHistory(true);

    try {
      const windowData = await getChannelMessageWindow(channelId, target.messageId);
      setChannelMessages(channelId, windowData.voMessages);
      updateHistoryAvailability(channelId, windowData.voHasMoreBefore, windowData.voHasMoreAfter);
      completeMessageWindow(target, getEntityKey(windowData.voAnchorMessageId));

      if (!windowData.voHasMoreAfter) {
        await chatHub.markChannelAsRead(channelId);
      }
    } catch (error) {
      failMessageWindow(target);
      log.error('ChatApp', '加载目标消息窗口失败:', error);
      toast.error(error instanceof Error ? error.message : t('chat.messageNavigationNotFound'));
    } finally {
      setLoadingHistory(false);
    }
  }, [completeMessageWindow, failMessageWindow, setChannelMessages, t, updateHistoryAvailability]);

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
      refreshConversationAfterSuccess?: boolean;
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

      if (options?.refreshConversationAfterSuccess) {
        await reloadConversations();
      }

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
  }, [addMessage, reloadConversations, scrollToBottom, t]);

  const handleSendMessage = useCallback(() => {
    if (!activeChannelId || !activeChannel?.voCanSend || uploadingImage) {
      return;
    }

    const content = messageInput.trim();
    const pendingImageSnapshot = pendingImage;
    if (!content && !pendingImageSnapshot) {
      return;
    }

    const replyTargetId = getReplyTargetMessageId(replyTarget);
    if (
      isDirectRequestFirstMessage
      && (pendingImageSnapshot || replyTargetId || /@\[[^\]]+\]\([^)]+\)/.test(content))
    ) {
      toast.error(t('chat.directRequestTextOnly'));
      return;
    }

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
        refreshConversationAfterSuccess: isDirectRequestFirstMessage,
      }
    );
  }, [
    activeChannel,
    activeChannelId,
    createOptimisticMessage,
    createTempMessageId,
    isDirectRequestFirstMessage,
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
    if (!file || !activeChannelId || !canSendAttachment) {
      return;
    }

    try {
      await uploadImageToPendingDraft(file, activeChannelId, messageInput, replyTarget);
    } finally {
      event.target.value = '';
    }
  }, [activeChannelId, canSendAttachment, messageInput, replyTarget, uploadImageToPendingDraft]);

  const handleComposerPaste = useCallback((event: ClipboardEvent<HTMLTextAreaElement>) => {
    if (!activeChannelId || !canSendAttachment || uploadingImage) {
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
  }, [activeChannelId, canSendAttachment, messageInput, replyTarget, uploadImageToPendingDraft, uploadingImage]);

  const handleRemovePendingImage = useCallback(() => {
    setPendingImage(null);
  }, []);

  const handleRetryMessage = useCallback((message: ChannelMessageVo) => {
    const channelId = message.voChannelId;
    const messageId = message.voId;
    if (!isPersistedEntityId(channelId) || !isTemporaryEntityId(messageId)) {
      return;
    }

    if (!activeChannel?.voCanSend) {
      toast.error(t(conversationNoticeKey || 'chat.inputConversationUnavailable'));
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
        refreshConversationAfterSuccess: isDirectRequestFirstMessage,
      }
    );
  }, [activeChannel, conversationNoticeKey, isDirectRequestFirstMessage, sendOptimisticMessage, t]);

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
    activeChannelIdRef.current = activeChannelId;
  }, [activeChannelId]);

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
      clearMessageNavigation();
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
  }, [activeChannelId, clearMessageHighlight, clearMessageNavigation, closeMentionDropdown, currentUserIdKey, loadInitialHistory, loadMessageWindow, loadOnlineMembers, messageNavigationTargetRef]);

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
      void chatHub.markChannelAsRead(activeChannelId);
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
  }, [activeChannelId, connectionState, loadInitialHistory, loadMessageWindow, loadOnlineMembers, messageNavigationTargetRef, setChannelMessages, updateHistoryAvailability]);

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
  const isConversationFocused = hasRoutedChannel
    || searchOpen
    || (isCompactViewport && Boolean(activeChannelId) && !onOpenFocusedChannel);

  return (
    <div className={`${styles.chatApp} ${isConversationFocused ? styles.chatAppFocused : ''}`}>
      <ChatChannelSidebar
        channels={channels}
        activeChannelId={activeChannelId}
        listView={listView}
        loadingChannels={loadingChannels}
        listError={listError}
        onSelectChannel={selectChannel}
        onChangeListView={changeListView}
        onOpenSearch={openMessageSearch}
      />

      <section className={`${styles.main} ${searchOpen && isCompactViewport ? searchStyles.mainSearchMode : ''}`}>
        {(!searchOpen || !isCompactViewport) && (
          <ChatConversationHeader
            activeChannel={activeChannel}
            showBackToList={hasRoutedChannel || (isCompactViewport && Boolean(activeChannelId))}
            onBackToList={hasRoutedChannel ? onBackToConversationList : () => setActiveChannel(null)}
            connectionHint={connectionHint}
            routeUnavailable={routeUnavailable}
            searchOpen={searchOpen}
            onOpenSearch={openMessageSearch}
            onOpenUserProfile={(target) => handleOpenUserProfile(target.userId, target.userName, target.avatarUrl, target.publicId)}
            onConversationChanged={handleConversationChanged}
          />
        )}

        <div className={`${styles.contentArea} ${searchOpen ? searchStyles.contentAreaSearch : ''}`}>
          {(!searchOpen || !isCompactViewport) && <div className={styles.messageColumn}>
            {messageTargetUnavailable && (
              <div className={searchStyles.messageTargetUnavailable} role="alert">
                <Icon icon="mdi:message-off-outline" size={20} />
                <span>
                  <strong>{t('chat.search.targetUnavailableTitle')}</strong>
                  {t('chat.search.targetUnavailableDescription')}
                </span>
              </div>
            )}
            <ChatMessageList
              activeChannelId={activeChannelId}
              activeChannelKey={activeChannelKey}
              messages={activeMessages}
              loadingHistory={loadingHistory}
              highlightedMessageId={highlightedMessageId}
              currentUserIdKey={currentUserIdKey}
              apiBaseUrl={apiBaseUrl}
              canSendMessages={canSendInActiveChannel}
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
              {activeChannelId && !canSendInActiveChannel && (
                <div className={styles.composerRestriction} role="status">
                  {t(conversationNoticeKey || 'chat.inputConversationUnavailable')}
                </div>
              )}
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
                    disabled={!activeChannelId || !canSendInActiveChannel}
                  />

                  {isMentionOpen && (
                    <ChatMentionMenu
                      keyword={mentionContext?.keyword ?? ''}
                      loading={mentionLoading}
                      options={mentionOptions}
                      selectedIndex={mentionSelectedIndex}
                      apiBaseUrl={apiBaseUrl}
                      onSelect={applyMentionSelection}
                      onSelectIndex={setMentionSelectedIndex}
                    />
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
                    disabled={!activeChannelId || !canSendAttachment || uploadingImage}
                    onClick={() => imageInputRef.current?.click()}
                  >
                    {uploadingImage ? t('chat.uploading') : t('chat.image')}
                  </button>
                  <button
                    className={styles.sendButton}
                    type="button"
                    disabled={!activeChannelId || !canSendInActiveChannel || uploadingImage || !hasComposerContent}
                    onClick={() => { void handleSendMessage(); }}
                  >
                    {t('chat.send')}
                  </button>
                </div>
              </div>
            </footer>
          </div>}

          {searchMounted && (
            <ChatMessageSearchPanel
              activeChannelId={activeChannelKey || null}
              accountKey={currentUserIdKey}
              recalledMessageIds={recalledMessageIds}
              hidden={!searchOpen}
              onClose={() => setSearchOpen(false)}
              onOpenResult={handleOpenSearchResult}
            />
          )}
          {!searchOpen && activeChannelId !== null && (
            <ChatMemberPanel
              collapsed={memberPanelCollapsed}
              members={onlineMembers}
              loading={loadingMembers}
              onToggleCollapsed={() => {
                setSearchOpen(false);
                setMemberPanelCollapsed((current) => !current);
              }}
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
