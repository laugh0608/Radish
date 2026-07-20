import { useCallback, useEffect, useRef, useState } from 'react';
import type { ChannelMessageVo, ChannelVo, EntityIdValue } from '@/types/chat';
import { areEntityIdsEqual } from '@/types/chat';
import type { MessageFocusTarget, MessageNavigationTarget } from './chatApp.helpers';

const MESSAGE_HIGHLIGHT_DURATION_MS = 2_600;

interface UseChatMessageNavigationOptions {
  routedChannelId?: string;
  routedMessageId?: string;
  navigationKey?: string;
  channels: ChannelVo[];
  activeChannelId: EntityIdValue | null;
  activeMessages: ChannelMessageVo[];
  setActiveChannel: (channelId: EntityIdValue | null) => void;
}

export function useChatMessageNavigation({
  routedChannelId,
  routedMessageId,
  navigationKey,
  channels,
  activeChannelId,
  activeMessages,
  setActiveChannel,
}: UseChatMessageNavigationOptions) {
  const [navigationTarget, setNavigationTarget] = useState<MessageNavigationTarget | null>(null);
  const [focusTarget, setFocusTarget] = useState<MessageFocusTarget | null>(null);
  const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);
  const [targetUnavailable, setTargetUnavailable] = useState(false);
  const navigationTargetRef = useRef<MessageNavigationTarget | null>(null);
  const handledWindowNavigationRef = useRef<string | null>(null);
  const messageElementMapRef = useRef(new Map<string, HTMLDivElement>());
  const highlightTimerRef = useRef<number | null>(null);

  const clearMessageHighlight = useCallback(() => {
    if (highlightTimerRef.current !== null) {
      window.clearTimeout(highlightTimerRef.current);
      highlightTimerRef.current = null;
    }
    setHighlightedMessageId(null);
  }, []);

  const highlightMessage = useCallback((messageId: string) => {
    clearMessageHighlight();
    setHighlightedMessageId(messageId);
    highlightTimerRef.current = window.setTimeout(() => {
      setHighlightedMessageId((current) => current === messageId ? null : current);
      highlightTimerRef.current = null;
    }, MESSAGE_HIGHLIGHT_DURATION_MS);
  }, [clearMessageHighlight]);

  const setMessageElementRef = useCallback((messageId: string, element: HTMLDivElement | null) => {
    if (!messageId) {
      return;
    }

    if (element) {
      messageElementMapRef.current.set(messageId, element);
    } else {
      messageElementMapRef.current.delete(messageId);
    }
  }, []);

  const clearNavigation = useCallback(() => {
    navigationTargetRef.current = null;
    setNavigationTarget(null);
    setFocusTarget(null);
    setTargetUnavailable(false);
    clearMessageHighlight();
  }, [clearMessageHighlight]);

  const completeMessageWindow = useCallback((target: MessageNavigationTarget, anchorMessageId: string) => {
    setTargetUnavailable(false);
    setFocusTarget({
      channelId: target.channelId,
      messageId: anchorMessageId,
      signature: target.signature,
    });
    if (navigationTargetRef.current?.signature === target.signature) {
      navigationTargetRef.current = null;
    }
    setNavigationTarget((current) => current?.signature === target.signature ? null : current);
  }, []);

  const failMessageWindow = useCallback((target: MessageNavigationTarget) => {
    if (navigationTargetRef.current?.signature === target.signature) {
      navigationTargetRef.current = null;
    }
    setNavigationTarget((current) => current?.signature === target.signature ? null : current);
    setFocusTarget(null);
    setTargetUnavailable(true);
  }, []);

  const navigateToMessage = useCallback((channelId: string, messageId: string) => {
    const signature = `${channelId}:${messageId}:search:${Date.now()}`;
    const target = { channelId, messageId, signature };
    setTargetUnavailable(false);
    clearMessageHighlight();
    setFocusTarget(null);
    navigationTargetRef.current = target;
    setNavigationTarget(target);
    if (!areEntityIdsEqual(activeChannelId, channelId)) {
      setActiveChannel(channelId);
    }
  }, [activeChannelId, clearMessageHighlight, setActiveChannel]);

  useEffect(() => {
    navigationTargetRef.current = navigationTarget;
  }, [navigationTarget]);

  useEffect(() => () => {
    if (highlightTimerRef.current !== null) {
      window.clearTimeout(highlightTimerRef.current);
    }
  }, []);

  useEffect(() => {
    if (!routedChannelId) {
      clearNavigation();
      return;
    }
    if (channels.length === 0) {
      return;
    }
    if (!channels.some((channel) => areEntityIdsEqual(channel.voId, routedChannelId))) {
      clearNavigation();
      return;
    }

    const signature = `${routedChannelId}:${routedMessageId ?? 'none'}:${navigationKey ?? 'initial'}`;
    if (handledWindowNavigationRef.current === signature) {
      return;
    }

    handledWindowNavigationRef.current = signature;
    clearMessageHighlight();
    setTargetUnavailable(false);
    setFocusTarget(null);
    const nextTarget = routedMessageId
      ? { channelId: routedChannelId, messageId: routedMessageId, signature }
      : null;
    navigationTargetRef.current = nextTarget;
    setNavigationTarget(nextTarget);
    if (!areEntityIdsEqual(activeChannelId, routedChannelId)) {
      setActiveChannel(routedChannelId);
    }
  }, [
    activeChannelId,
    channels,
    clearMessageHighlight,
    clearNavigation,
    navigationKey,
    routedChannelId,
    routedMessageId,
    setActiveChannel,
  ]);

  useEffect(() => {
    if (!focusTarget || !activeChannelId) {
      return;
    }
    if (!areEntityIdsEqual(activeChannelId, focusTarget.channelId)) {
      return;
    }

    const targetElement = messageElementMapRef.current.get(focusTarget.messageId);
    if (!targetElement) {
      return;
    }

    setFocusTarget((current) => current?.signature === focusTarget.signature ? null : current);
    requestAnimationFrame(() => {
      targetElement.scrollIntoView({ block: 'center', behavior: 'smooth' });
      highlightMessage(focusTarget.messageId);
    });
  }, [activeChannelId, activeMessages, focusTarget, highlightMessage]);

  return {
    navigationTarget,
    navigationTargetRef,
    focusTarget,
    highlightedMessageId,
    targetUnavailable,
    setMessageElementRef,
    clearMessageHighlight,
    clearNavigation,
    completeMessageWindow,
    failMessageWindow,
    navigateToMessage,
  };
}
