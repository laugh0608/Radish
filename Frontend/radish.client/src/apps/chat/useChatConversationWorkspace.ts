import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from '@radish/ui/toast';
import { getChannelDetail, getChannelList } from '@/api/chat';
import { useChatStore } from '@/stores/chatStore';
import type { ChannelVo, ChatChannelListView, DirectConversationAction } from '@/types/chat';
import { areEntityIdsEqual, normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';

function isCompactChatViewport(): boolean {
  return typeof window !== 'undefined' && window.innerWidth <= 720;
}

interface UseChatConversationWorkspaceOptions {
  routedChannelId?: string;
  onOpenFocusedChannel?: (channelId: string) => void;
}

export function useChatConversationWorkspace({
  routedChannelId,
  onOpenFocusedChannel,
}: UseChatConversationWorkspaceOptions) {
  const { t } = useTranslation();
  const channels = useChatStore((state) => state.channels);
  const activeChannelId = useChatStore((state) => state.activeChannelId);
  const conversationRevision = useChatStore((state) => state.conversationRevision);
  const setChannels = useChatStore((state) => state.setChannels);
  const setActiveChannel = useChatStore((state) => state.setActiveChannel);
  const [listView, setListView] = useState<ChatChannelListView>('active');
  const [loadingChannels, setLoadingChannels] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [routeUnavailable, setRouteUnavailable] = useState(false);
  const activeChannelIdRef = useRef(activeChannelId);
  const routedChannelResolvedRef = useRef<string | null>(null);
  const requestIdRef = useRef(0);
  const previousConversationRevisionRef = useRef(conversationRevision);

  useEffect(() => {
    activeChannelIdRef.current = activeChannelId;
  }, [activeChannelId]);

  useEffect(() => {
    routedChannelResolvedRef.current = null;
    setRouteUnavailable(false);
  }, [routedChannelId]);

  const reloadConversations = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    const normalizedRoutedChannelId = normalizeEntityId(routedChannelId);
    const shouldResolveRoute = Boolean(
      normalizedRoutedChannelId
      && routedChannelResolvedRef.current !== normalizedRoutedChannelId
    );

    setLoadingChannels(true);
    setListError(null);

    let data: ChannelVo[] = [];
    let listLoaded = false;
    try {
      data = await getChannelList(listView);
      listLoaded = true;

      if (shouldResolveRoute && normalizedRoutedChannelId) {
        let routedChannel = data.find((channel) => areEntityIdsEqual(channel.voId, normalizedRoutedChannelId));
        if (!routedChannel) {
          routedChannel = await getChannelDetail(normalizedRoutedChannelId);
          const routedView: ChatChannelListView = routedChannel.voIsArchived ? 'archived' : 'active';
          if (routedView !== listView) {
            setListView(routedView);
            return;
          }

          data = [...data, routedChannel];
        }

        routedChannelResolvedRef.current = normalizedRoutedChannelId;
      }

      if (requestId !== requestIdRef.current) {
        return;
      }

      setChannels(data);
      setRouteUnavailable(false);

      const currentActiveChannelId = activeChannelIdRef.current;
      const nextActiveChannelId = normalizedRoutedChannelId
        && data.some((channel) => areEntityIdsEqual(channel.voId, normalizedRoutedChannelId))
        ? normalizedRoutedChannelId
        : currentActiveChannelId
          && data.some((channel) => areEntityIdsEqual(channel.voId, currentActiveChannelId))
          ? normalizeEntityId(currentActiveChannelId)
          : !isCompactChatViewport()
            ? normalizeEntityId(data[0]?.voId)
            : null;
      setActiveChannel(nextActiveChannelId);
    } catch (error) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      const message = error instanceof Error ? error.message : t('chat.loadChannelsFailed');
      log.error('ChatApp', '加载会话列表或定位会话失败:', error);

      if (shouldResolveRoute) {
        routedChannelResolvedRef.current = normalizedRoutedChannelId;
        if (listLoaded) {
          setChannels(data);
        } else {
          setListError(message);
        }
        setRouteUnavailable(true);
        setActiveChannel(null);
      } else {
        setListError(message);
        toast.error(message);
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setLoadingChannels(false);
      }
    }
  }, [listView, routedChannelId, setActiveChannel, setChannels, t]);

  useEffect(() => {
    void reloadConversations();
  }, [reloadConversations]);

  useEffect(() => {
    if (previousConversationRevisionRef.current === conversationRevision) {
      return;
    }

    previousConversationRevisionRef.current = conversationRevision;
    void reloadConversations();
  }, [conversationRevision, reloadConversations]);

  const selectChannel = useCallback((channelId: string) => {
    if (isCompactChatViewport() && onOpenFocusedChannel) {
      onOpenFocusedChannel(channelId);
      return;
    }

    setActiveChannel(channelId);
  }, [onOpenFocusedChannel, setActiveChannel]);

  const changeListView = useCallback((nextView: ChatChannelListView) => {
    if (nextView === listView) {
      return;
    }

    routedChannelResolvedRef.current = normalizeEntityId(routedChannelId);
    setRouteUnavailable(false);
    setActiveChannel(null);
    setListView(nextView);
  }, [listView, routedChannelId, setActiveChannel]);

  const handleConversationChanged = useCallback(async (action: DirectConversationAction) => {
    if (action === 'archive' && listView !== 'archived') {
      setListView('archived');
      return;
    }

    if (action === 'unarchive' && listView !== 'active') {
      setListView('active');
      return;
    }

    await reloadConversations();
  }, [listView, reloadConversations]);

  return {
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
  };
}
