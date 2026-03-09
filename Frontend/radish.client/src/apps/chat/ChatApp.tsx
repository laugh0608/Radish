import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { toast } from '@radish/ui/toast';
import {
  getChannelHistory,
  getChannelList,
  recallChannelMessage,
  sendChannelMessage,
} from '@/api/chat';
import { chatHub } from '@/services/chatHub';
import { useChatStore } from '@/stores/chatStore';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import type { ChannelMessageVo } from '@/types/chat';
import styles from './ChatApp.module.css';

const PAGE_SIZE = 50;

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

export const ChatApp = () => {
  const { openApp } = useWindowStore();
  const {
    channels,
    activeChannelId,
    messageMap,
    typingMap,
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
  const [messageInput, setMessageInput] = useState('');
  const [hasMoreHistory, setHasMoreHistory] = useState<Record<number, boolean>>({});
  const [typingAt, setTypingAt] = useState(0);

  const messageScrollRef = useRef<HTMLDivElement | null>(null);

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

    return (typingMap[activeChannelId] || []).filter((user) => user.userId !== currentUserId);
  }, [activeChannelId, typingMap, currentUserId]);

  const scrollToBottom = useCallback(() => {
    const scrollEl = messageScrollRef.current;
    if (!scrollEl) {
      return;
    }

    scrollEl.scrollTop = scrollEl.scrollHeight;
  }, []);

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
    if (!activeChannelId || sending) {
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
      });

      addMessage(sent);
      setMessageInput('');

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
  }, [activeChannelId, addMessage, messageInput, scrollToBottom, sending]);

  const handleRecall = useCallback(async (messageId: number) => {
    if (!messageId) {
      return;
    }

    try {
      await recallChannelMessage(messageId);
      toast.success('消息已撤回');
    } catch (error) {
      log.error('ChatApp', '撤回消息失败:', error);
      toast.error(error instanceof Error ? error.message : '撤回消息失败');
    }
  }, []);

  const handleOpenUserProfile = useCallback((targetUserId: number, targetUserName?: string | null, avatarUrl?: string | null) => {
    if (!targetUserId) {
      return;
    }

    if (String(targetUserId) === String(currentUserId ?? 0)) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName?.trim() || `用户 ${targetUserId}`,
      avatarUrl: avatarUrl ?? null,
    });
  }, [currentUserId, openApp]);

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

    void chatHub.joinChannel(activeChannelId);
    void loadInitialHistory(activeChannelId);

    return () => {
      void chatHub.leaveChannel(activeChannelId);
    };
  }, [activeChannelId, loadInitialHistory]);

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
              const isMine = messageUserId > 0 && messageUserId === currentUserId;

              return (
                <div key={messageId} className={`${styles.messageRow} ${isMine ? styles.mine : ''}`}>
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
                    <>
                      {message.voContent && <div className={styles.bubble}>{message.voContent}</div>}
                      {message.voType === 2 && message.voImageUrl && (
                        <img className={styles.imageMessage} src={message.voImageUrl} alt="图片消息" />
                      )}
                    </>
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

          <div className={styles.inputRow}>
            <textarea
              className={styles.input}
              value={messageInput}
              onChange={(event) => {
                setMessageInput(event.target.value);

                const now = Date.now();
                if (activeChannelId && now - typingAt >= 2000) {
                  setTypingAt(now);
                  void chatHub.startTyping(activeChannelId);
                }
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault();
                  void handleSendMessage();
                }
              }}
              placeholder={activeChannelId ? '输入消息，Enter 发送，Shift+Enter 换行' : '请先选择频道'}
              disabled={!activeChannelId || sending}
            />
            <button
              className={styles.sendButton}
              type="button"
              disabled={!activeChannelId || sending || !messageInput.trim()}
              onClick={() => {
                void handleSendMessage();
              }}
            >
              {sending ? '发送中...' : '发送'}
            </button>
          </div>
        </footer>
      </section>
    </div>
  );
};
