import { type ChangeEvent, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from '@radish/ui/toast';
import { uploadImage } from '@/api/attachment';
import {
  getChannelHistory,
  getChannelList,
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
import type { ChannelMessageVo } from '@/types/chat';
import styles from './ChatApp.module.css';

const PAGE_SIZE = 50;
const MENTION_PATTERN = /@\[(?<name>[^\]]+)\]\((?<id>\d+)\)/g;

interface MentionContext {
  start: number;
  end: number;
  keyword: string;
}

function toNumericId(value: number | string | null | undefined): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
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
  } = useChatStore();
  const currentUserId = useUserStore((state) => state.userId);

  const [loadingChannels, setLoadingChannels] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [sending, setSending] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadProgress, setImageUploadProgress] = useState(0);
  const [messageInput, setMessageInput] = useState('');
  const [replyTarget, setReplyTarget] = useState<ChannelMessageVo | null>(null);
  const [hasMoreHistory, setHasMoreHistory] = useState<Record<number, boolean>>({});
  const [typingAt, setTypingAt] = useState(0);
  const [mentionContext, setMentionContext] = useState<MentionContext | null>(null);
  const [mentionOptions, setMentionOptions] = useState<UserMentionOption[]>([]);
  const [mentionLoading, setMentionLoading] = useState(false);
  const [mentionSelectedIndex, setMentionSelectedIndex] = useState(0);

  const messageScrollRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const previousConnectionStateRef = useRef(connectionState);

  const currentUserIdValue = useMemo(() => toNumericId(currentUserId), [currentUserId]);

  const activeChannel = useMemo(
    () => channels.find((channel) => toNumericId(channel.voId) === activeChannelId) || null,
    [channels, activeChannelId]
  );

  const activeMessages = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return messageMap[activeChannelId] || [];
  }, [activeChannelId, messageMap]);

  const typingUsers = useMemo(() => {
    if (!activeChannelId) {
      return [];
    }

    return (typingMap[activeChannelId] || []).filter((user) => user.userId !== currentUserIdValue);
  }, [activeChannelId, typingMap, currentUserIdValue]);

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

  const handleOpenUserProfile = useCallback((targetUserId: number, targetUserName?: string | null, avatarUrl?: string | null) => {
    if (!targetUserId) {
      return;
    }

    if (String(targetUserId) === String(currentUserIdValue)) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName?.trim() || `用户 ${targetUserId}`,
      avatarUrl: avatarUrl ?? null,
    });
  }, [currentUserIdValue, openApp]);

  const renderAvatarButton = useCallback((
    targetUserId: number,
    targetUserName: string | null | undefined,
    avatarUrl: string | null | undefined,
    titlePrefix: string
  ) => {
    const normalizedName = targetUserName?.trim() || `用户 ${targetUserId || '?'}`;
    const resolvedAvatarUrl = resolveMediaUrl(apiBaseUrl, avatarUrl);

    return (
      <button
        type="button"
        className={styles.avatarButton}
        onClick={() => {
          if (targetUserId > 0) {
            openApp('profile', {
              userId: targetUserId,
              userName: normalizedName,
              avatarUrl: avatarUrl ?? null,
            });
          }
        }}
        disabled={targetUserId <= 0}
        title={targetUserId > 0 ? `${titlePrefix}${normalizedName}` : '用户信息不可用'}
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
      const mentionUserId = toNumericId(match.groups?.id ?? match[2]);

      if (matchIndex > lastIndex) {
        nodes.push(content.slice(lastIndex, matchIndex));
      }

      nodes.push(
        <button
          key={`mention-${mentionUserId}-${keyIndex}`}
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

    const targetId = toNumericId(option.voId);
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
        const firstId = toNumericId(data[0].voId);
        if (firstId > 0) {
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

  const loadInitialHistory = useCallback(async (channelId: number) => {
    setLoadingHistory(true);

    try {
      const history = await getChannelHistory(channelId, undefined, PAGE_SIZE);
      setChannelMessages(channelId, history);
      setHasMoreHistory((prev) => ({
        ...prev,
        [channelId]: history.length >= PAGE_SIZE,
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

  const loadMoreHistory = useCallback(async () => {
    if (!activeChannelId || loadingHistory || !hasMoreHistory[activeChannelId]) {
      return;
    }

    const oldestMessage = activeMessages[0];
    const beforeMessageId = toNumericId(oldestMessage?.voId);
    if (beforeMessageId <= 0) {
      setHasMoreHistory((prev) => ({
        ...prev,
        [activeChannelId]: false,
      }));
      return;
    }

    setLoadingHistory(true);
    try {
      const older = await getChannelHistory(activeChannelId, beforeMessageId, PAGE_SIZE);
      prependChannelMessages(activeChannelId, older);
      setHasMoreHistory((prev) => ({
        ...prev,
        [activeChannelId]: older.length >= PAGE_SIZE,
      }));
    } catch (error) {
      log.error('ChatApp', '加载更多历史消息失败:', error);
      toast.error('加载更多历史消息失败');
    } finally {
      setLoadingHistory(false);
    }
  }, [activeChannelId, activeMessages, hasMoreHistory, loadingHistory, prependChannelMessages]);

  const handleSendMessage = useCallback(async () => {
    if (!activeChannelId || sending || uploadingImage) {
      return;
    }

    const content = messageInput.trim();
    if (!content) {
      return;
    }

    setSending(true);
    try {
      const sent = await sendChannelMessage({
        channelId: activeChannelId,
        type: 1,
        content,
        replyToId: replyTarget ? toNumericId(replyTarget.voId) : undefined,
      });

      addMessage(sent);
      setMessageInput('');
      setReplyTarget(null);
      closeMentionDropdown();

      requestAnimationFrame(() => {
        scrollToBottom();
      });

      await chatHub.markChannelAsRead(activeChannelId);
    } catch (error) {
      log.error('ChatApp', '发送消息失败:', error);
      toast.error(error instanceof Error ? error.message : '发送消息失败');
    } finally {
      setSending(false);
    }
  }, [activeChannelId, addMessage, closeMentionDropdown, messageInput, replyTarget, scrollToBottom, sending, uploadingImage]);

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

      const sent = await sendChannelMessage({
        channelId: activeChannelId,
        type: 2,
        content: messageInput.trim() || undefined,
        replyToId: replyTarget ? toNumericId(replyTarget.voId) : undefined,
        attachmentId: toNumericId(attachment.voId),
        imageUrl: attachment.voUrl,
        imageThumbnailUrl: attachment.voThumbnailUrl || attachment.voUrl,
      });

      addMessage(sent);
      setMessageInput('');
      setReplyTarget(null);
      closeMentionDropdown();
      setImageUploadProgress(100);

      requestAnimationFrame(() => {
        scrollToBottom();
      });

      await chatHub.markChannelAsRead(activeChannelId);
      toast.success('图片已发送');
    } catch (error) {
      log.error('ChatApp', '发送图片消息失败:', error);
      toast.error(error instanceof Error ? error.message : '发送图片消息失败');
    } finally {
      setUploadingImage(false);
      setTimeout(() => setImageUploadProgress(0), 400);
      event.target.value = '';
    }
  }, [activeChannelId, addMessage, closeMentionDropdown, messageInput, replyTarget, scrollToBottom, t, uploadingImage]);

  const handleRecall = useCallback(async (messageId: number) => {
    if (!messageId) {
      return;
    }

    try {
      await recallChannelMessage(messageId);
      if (replyTarget && toNumericId(replyTarget.voId) === messageId) {
        setReplyTarget(null);
      }
      toast.success('消息已撤回');
    } catch (error) {
      log.error('ChatApp', '撤回消息失败:', error);
      toast.error(error instanceof Error ? error.message : '撤回消息失败');
    }
  }, [replyTarget]);

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
    void loadChannels();
  }, [loadChannels]);

  useEffect(() => {
    void chatHub.start();
  }, []);

  useEffect(() => {
    if (!activeChannelId) {
      return;
    }

    setReplyTarget(null);
    closeMentionDropdown();
    void chatHub.joinChannel(activeChannelId);
    void loadInitialHistory(activeChannelId);

    return () => {
      void chatHub.leaveChannel(activeChannelId);
    };
  }, [activeChannelId, closeMentionDropdown, loadInitialHistory]);

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
    const previousConnectionState = previousConnectionStateRef.current;
    if (activeChannelId && previousConnectionState === 'reconnecting' && connectionState === 'connected') {
      setChannelMessages(activeChannelId, []);
      void loadInitialHistory(activeChannelId);
    }

    previousConnectionStateRef.current = connectionState;
  }, [activeChannelId, connectionState, loadInitialHistory, setChannelMessages]);

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
            const channelId = toNumericId(channel.voId);
            const isActive = activeChannelId === channelId;

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
          <div className={styles.channelTitle}>
            {activeChannel ? `${activeChannel.voIconEmoji || '#'} ${activeChannel.voName}` : '请选择频道'}
          </div>
          {activeChannel?.voDescription && (
            <div className={styles.channelDescription}>{activeChannel.voDescription}</div>
          )}
          {activeChannelId !== null && connectionHint && (
            <div className={styles.connectionBanner}>{connectionHint}</div>
          )}
        </header>

        <div className={styles.messageViewport} ref={messageScrollRef} onScroll={() => { void handleScroll(); }}>
          {activeChannelId === null ? (
            <div className={styles.placeholder}>请选择一个频道开始聊天</div>
          ) : activeMessages.length === 0 && loadingHistory ? (
            <div className={styles.placeholder}>正在加载历史消息...</div>
          ) : activeMessages.length === 0 ? (
            <div className={styles.placeholder}>还没有消息，发一条试试</div>
          ) : (
            activeMessages.map((message: ChannelMessageVo) => {
              const messageId = toNumericId(message.voId);
              const messageUserId = toNumericId(message.voUserId);
              const isMine = messageUserId > 0 && messageUserId === currentUserIdValue;
              const replyText = message.voReplyTo ? getMessagePreviewText(message.voReplyTo) : null;
              const messageImageUrl = resolveMediaUrl(apiBaseUrl, message.voImageUrl);

              return (
                <div key={messageId} className={`${styles.messageRow} ${isMine ? styles.mine : ''}`}>
                  {!isMine && renderAvatarButton(
                    messageUserId,
                    message.voUserName,
                    message.voUserAvatarUrl,
                    '查看 '
                  )}

                  <div className={styles.messageBody}>
                    <div className={styles.metaLine}>
                      <button
                        type="button"
                        className={styles.userNameButton}
                        onClick={() => handleOpenUserProfile(messageUserId, message.voUserName, message.voUserAvatarUrl)}
                        disabled={messageUserId <= 0}
                        title={messageUserId > 0 ? `查看 ${(message.voUserName || `用户 ${messageUserId}`).trim()} 的主页` : '用户信息不可用'}
                      >
                        <span className={styles.userName}>{message.voUserName || 'Unknown'}</span>
                      </button>
                      <span className={styles.time}>{formatTime(message.voCreateTime)}</span>
                      {!message.voIsRecalled && (
                        <button
                          type="button"
                          className={styles.replyButton}
                          onClick={() => setReplyTarget(message)}
                        >
                          回复
                        </button>
                      )}
                      {isMine && !message.voIsRecalled && (
                        <button
                          type="button"
                          className={styles.recallButton}
                          onClick={() => {
                            void handleRecall(messageId);
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
                      </div>
                    )}
                  </div>

                  {isMine && renderAvatarButton(
                    messageUserId,
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
                disabled={!activeChannelId || sending || uploadingImage}
              />

              {isMentionOpen && (
                <div className={styles.mentionDropdown}>
                  {mentionLoading ? (
                    <div className={styles.mentionEmpty}>搜索中...</div>
                  ) : mentionOptions.length === 0 ? (
                    <div className={styles.mentionEmpty}>未找到匹配的用户</div>
                  ) : (
                    mentionOptions.map((option, index) => {
                      const optionId = toNumericId(option.voId);
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
                disabled={!activeChannelId || sending || uploadingImage}
                onClick={() => imageInputRef.current?.click()}
              >
                {uploadingImage ? '上传中...' : '图片'}
              </button>
              <button
                className={styles.sendButton}
                type="button"
                disabled={!activeChannelId || sending || uploadingImage || !messageInput.trim()}
                onClick={() => {
                  void handleSendMessage();
                }}
              >
                {sending ? '发送中...' : '发送'}
              </button>
            </div>
          </div>
        </footer>
      </section>
    </div>
  );
};
