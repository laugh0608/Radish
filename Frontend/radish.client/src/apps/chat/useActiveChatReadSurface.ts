import { type RefObject, useCallback, useEffect, useMemo, useRef } from 'react';
import { ApiResponseError } from '@radish/http';
import { advanceChannelReadState } from '@/api/chat';
import type { WindowState } from '@/desktop/types';
import { useChatStore } from '@/stores/chatStore';
import { useWindowStore } from '@/stores/windowStore';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import {
  CHAT_READ_BOTTOM_THRESHOLD_PX,
  canAdvanceChatReadState,
  selectHigherReadTarget,
  selectHighestVisiblePersistedMessageId,
} from './chatReadReceipts';

interface UseActiveChatReadSurfaceOptions {
  accountKey: string;
  activeChannelId: EntityIdValue | null;
  currentWindow: WindowState | null;
  readSurfaceMode: 'page' | 'webos-window';
  messages: ChannelMessageVo[];
  messageScrollRef: RefObject<HTMLDivElement | null>;
  hasMoreNewerHistory: boolean;
  loadingHistory: boolean;
  navigatingHistory: boolean;
  connectionState: string;
}

interface PendingReadTarget {
  accountKey: string;
  channelId: string;
  messageId: string;
}

function shouldDiscardPendingRead(error: unknown): boolean {
  if (!(error instanceof ApiResponseError)) {
    return false;
  }

  const status = error.httpStatus ?? error.statusCode ?? 0;
  return status >= 400 && status < 500;
}

export function useActiveChatReadSurface({
  accountKey,
  activeChannelId,
  currentWindow,
  readSurfaceMode,
  messages,
  messageScrollRef,
  hasMoreNewerHistory,
  loadingHistory,
  navigatingHistory,
  connectionState,
}: UseActiveChatReadSurfaceOptions) {
  const openWindows = useWindowStore((state) => state.openWindows);
  const updateUnread = useChatStore((state) => state.updateUnread);
  const pendingTargetsRef = useRef(new Map<string, PendingReadTarget>());
  const acknowledgedTargetsRef = useRef(new Map<string, string>());
  const inFlightChannelsRef = useRef(new Set<string>());
  const accountKeyRef = useRef(accountKey);
  const activeChannelKey = normalizeEntityId(activeChannelId);
  const activeChannelKeyRef = useRef(activeChannelKey);

  const windowForeground = useMemo(() => {
    if (readSurfaceMode === 'page') {
      return true;
    }

    if (!currentWindow) {
      return false;
    }

    const trackedWindow = openWindows.find((item) => item.id === currentWindow.id);
    if (!trackedWindow) {
      return false;
    }

    const foregroundWindow = openWindows
      .filter((item) => !item.isMinimized)
      .sort((left, right) => right.zIndex - left.zIndex)[0];
    return !trackedWindow.isMinimized && foregroundWindow?.id === trackedWindow.id;
  }, [currentWindow, openWindows, readSurfaceMode]);

  useEffect(() => {
    activeChannelKeyRef.current = activeChannelKey;
  }, [activeChannelKey]);

  useEffect(() => {
    const pendingTargets = pendingTargetsRef.current;
    const acknowledgedTargets = acknowledgedTargetsRef.current;
    if (accountKeyRef.current !== accountKey) {
      pendingTargets.clear();
      acknowledgedTargets.clear();
    }

    accountKeyRef.current = accountKey;
    return () => {
      accountKeyRef.current = '';
      pendingTargets.clear();
      acknowledgedTargets.clear();
    };
  }, [accountKey]);

  const flushPendingRead = useCallback(async (channelKey: string) => {
    if (inFlightChannelsRef.current.has(channelKey)) {
      return;
    }

    const pending = pendingTargetsRef.current.get(channelKey);
    if (!pending || pending.accountKey !== accountKeyRef.current || activeChannelKeyRef.current !== channelKey) {
      return;
    }

    const acknowledged = acknowledgedTargetsRef.current.get(channelKey) ?? null;
    const target = selectHigherReadTarget(acknowledged, pending.messageId);
    if (!target || target === acknowledged) {
      pendingTargetsRef.current.delete(channelKey);
      return;
    }

    pendingTargetsRef.current.delete(channelKey);
    inFlightChannelsRef.current.add(channelKey);
    const requestAccountKey = pending.accountKey;
    let failed = false;
    try {
      const state = await advanceChannelReadState(channelKey, target);
      if (requestAccountKey === accountKeyRef.current) {
        acknowledgedTargetsRef.current.set(channelKey, state.voLastReadMessageId);
        updateUnread({
          channelId: state.voChannelId,
          unreadCount: state.voUnreadCount,
          hasMention: state.voHasMention,
        });
      }
    } catch (error) {
      const discardPendingRead = shouldDiscardPendingRead(error);
      const accountStillActive = requestAccountKey === accountKeyRef.current;
      failed = accountStillActive;
      if (accountStillActive && !discardPendingRead) {
        const currentPending = pendingTargetsRef.current.get(channelKey);
        pendingTargetsRef.current.set(channelKey, {
          ...pending,
          messageId: selectHigherReadTarget(currentPending?.messageId ?? null, target) ?? target,
        });
      }
      if (accountStillActive) {
        log.warn(
          'ChatReadSurface',
          discardPendingRead
            ? '推进精确已读游标被服务端拒绝，已丢弃当前目标:'
            : '推进精确已读游标失败，保留当前会话内存重试目标:',
          error
        );
      }
    } finally {
      inFlightChannelsRef.current.delete(channelKey);
    }

    if (!failed && pendingTargetsRef.current.has(channelKey)) {
      await flushPendingRead(channelKey);
    }
  }, [updateUnread]);

  const evaluateReadSurface = useCallback(() => {
    const channelKey = activeChannelKeyRef.current;
    const scrollElement = messageScrollRef.current;
    if (!channelKey || !scrollElement || accountKeyRef.current.length === 0) {
      return;
    }

    const distanceToBottom = scrollElement.scrollHeight
      - (scrollElement.scrollTop + scrollElement.clientHeight);
    const canAdvance = canAdvanceChatReadState({
      documentVisible: document.visibilityState === 'visible',
      documentFocused: document.hasFocus(),
      windowForeground,
      atConversationTail: distanceToBottom <= CHAT_READ_BOTTOM_THRESHOLD_PX,
      hasMoreNewerHistory,
      loadingHistory,
      navigatingHistory,
    });
    if (!canAdvance) {
      return;
    }

    const viewportBounds = scrollElement.getBoundingClientRect();
    const visibleMessageIds = new Set<string>();
    scrollElement.querySelectorAll<HTMLElement>('[data-chat-message-id]').forEach((element) => {
      const bounds = element.getBoundingClientRect();
      if (bounds.bottom >= viewportBounds.top && bounds.top <= viewportBounds.bottom) {
        const messageId = element.dataset.chatMessageId?.trim();
        if (messageId) {
          visibleMessageIds.add(messageId);
        }
      }
    });
    const targetMessageId = selectHighestVisiblePersistedMessageId(messages, visibleMessageIds);
    if (!targetMessageId) {
      return;
    }

    const currentPending = pendingTargetsRef.current.get(channelKey);
    const nextTarget = selectHigherReadTarget(currentPending?.messageId ?? null, targetMessageId);
    if (!nextTarget) {
      return;
    }

    pendingTargetsRef.current.set(channelKey, {
      accountKey: accountKeyRef.current,
      channelId: channelKey,
      messageId: nextTarget,
    });
    void flushPendingRead(channelKey);
  }, [flushPendingRead, hasMoreNewerHistory, loadingHistory, messageScrollRef, messages, navigatingHistory, windowForeground]);

  useEffect(() => {
    const scheduleEvaluation = () => {
      window.requestAnimationFrame(evaluateReadSurface);
    };
    document.addEventListener('visibilitychange', scheduleEvaluation);
    window.addEventListener('focus', scheduleEvaluation);
    window.addEventListener('resize', scheduleEvaluation);
    return () => {
      document.removeEventListener('visibilitychange', scheduleEvaluation);
      window.removeEventListener('focus', scheduleEvaluation);
      window.removeEventListener('resize', scheduleEvaluation);
    };
  }, [evaluateReadSurface]);

  useEffect(() => {
    const frame = window.requestAnimationFrame(evaluateReadSurface);
    return () => window.cancelAnimationFrame(frame);
  }, [connectionState, evaluateReadSurface, messages]);

  return { evaluateReadSurface };
}
