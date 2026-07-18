import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/authStore';
import { useChatStore } from '@/stores/chatStore';
import { tokenService } from './tokenService';
import { log } from '@/utils/logger';
import { getSignalrHubUrl } from '@/config/env';
import type {
  ChannelMessageVo,
  ChannelUnreadChangedPayload,
  EntityIdValue,
  MessageRecalledPayload,
  UserTypingPayload,
} from '@/types/chat';
import { isPersistedEntityId, normalizeEntityId } from '@/types/chat';

function getHubUrl(): string {
  return `${getSignalrHubUrl()}/hub/chat`;
}

class ChatHubService {
  private connection: signalR.HubConnection | null = null;
  private connectionOwners = new Set<symbol>();
  private isConnectionDesired = false;
  private isStarting = false;
  private shouldRestartAfterStart = false;
  private startRequestId = 0;
  private typingTimers = new Map<string, number>();

  private async joinActiveChannelIfNeeded(): Promise<void> {
    const activeChannelId = useChatStore.getState().activeChannelId;
    if (!isPersistedEntityId(activeChannelId)) {
      return;
    }

    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('JoinChannel', activeChannelId);
    } catch (error) {
      log.warn('ChatHub', '自动加入当前频道失败:', error);
    }
  }

  getConnection(): signalR.HubConnection | null {
    return this.connection;
  }

  async acquire(owner: symbol): Promise<void> {
    this.connectionOwners.add(owner);
    await this.start();
  }

  async release(owner: symbol): Promise<void> {
    this.connectionOwners.delete(owner);
    if (this.connectionOwners.size === 0) {
      await this.stopConnection();
    }
  }

  async start(): Promise<void> {
    this.isConnectionDesired = true;

    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.isStarting) {
      this.shouldRestartAfterStart = true;
      return;
    }

    const { isAuthenticated } = useAuthStore.getState();
    if (!isAuthenticated) {
      this.isConnectionDesired = false;
      return;
    }

    const requestId = ++this.startRequestId;
    this.isStarting = true;
    this.shouldRestartAfterStart = false;
    useChatStore.getState().setConnectionState('connecting');
    let startingConnection: signalR.HubConnection | null = null;

    try {
      const token = await tokenService.getValidAccessToken();
      if (!token || requestId !== this.startRequestId || !this.isConnectionDesired) {
        return;
      }

      if (this.connection) {
        try {
          await this.connection.stop();
        } catch {
          // 忽略旧连接 stop 失败
        }
      }

      startingConnection = new signalR.HubConnectionBuilder()
        .withUrl(getHubUrl(), {
          accessTokenFactory: async () => await tokenService.getValidAccessToken() || '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();
      this.connection = startingConnection;

      startingConnection.serverTimeoutInMilliseconds = 60_000;
      startingConnection.keepAliveIntervalInMilliseconds = 15_000;

      this.registerEventHandlers();
      await startingConnection.start();

      if (requestId !== this.startRequestId || !this.isConnectionDesired) {
        await startingConnection.stop();
        if (this.connection === startingConnection) {
          this.connection = null;
        }
        useChatStore.getState().setConnectionState('disconnected');
        return;
      }

      useChatStore.getState().setConnectionState('connected');
      await this.joinActiveChannelIfNeeded();
      log.debug('ChatHub', '连接成功');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const wasLifecycleCancellation = requestId !== this.startRequestId ||
        !this.isConnectionDesired ||
        message.includes('Failed to start the HttpConnection before stop() was called') ||
        message.includes('stopped during negotiation') ||
        message.includes('stopped before the hub handshake could complete');

      if (this.connection === startingConnection) {
        this.connection = null;
      }

      if (wasLifecycleCancellation) {
        useChatStore.getState().setConnectionState('disconnected');
        return;
      }

      useChatStore.getState().setConnectionState('disconnected');
      log.error('ChatHub', '连接失败:', error);
    } finally {
      this.isStarting = false;
      const shouldRestart = this.isConnectionDesired &&
        this.connection?.state !== signalR.HubConnectionState.Connected &&
        (this.shouldRestartAfterStart || requestId !== this.startRequestId);
      this.shouldRestartAfterStart = false;

      if (shouldRestart) {
        queueMicrotask(() => void this.start());
      }
    }
  }

  async stop(): Promise<void> {
    this.connectionOwners.clear();
    await this.stopConnection();
  }

  private async stopConnection(): Promise<void> {
    this.isConnectionDesired = false;
    this.shouldRestartAfterStart = false;
    this.startRequestId++;

    for (const timer of this.typingTimers.values()) {
      window.clearTimeout(timer);
    }
    this.typingTimers.clear();

    const connection = this.connection;
    this.connection = null;
    if (connection) {
      try {
        await connection.stop();
      } catch (error) {
        log.warn('ChatHub', '关闭连接失败:', error);
      }
    }

    useChatStore.getState().setConnectionState('disconnected');
  }

  async joinChannel(channelId: EntityIdValue): Promise<void> {
    if (!isPersistedEntityId(channelId)) {
      return;
    }

    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('JoinChannel', channelId);
    } catch (error) {
      log.error('ChatHub', '加入频道失败:', error);
    }
  }

  async leaveChannel(channelId: EntityIdValue): Promise<void> {
    if (!isPersistedEntityId(channelId)) {
      return;
    }

    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('LeaveChannel', channelId);
    } catch (error) {
      log.warn('ChatHub', '离开频道失败:', error);
    }
  }

  async startTyping(channelId: EntityIdValue): Promise<void> {
    if (!isPersistedEntityId(channelId)) {
      return;
    }

    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('StartTyping', channelId);
    } catch (error) {
      log.warn('ChatHub', '上报输入中状态失败:', error);
    }
  }

  async markChannelAsRead(channelId: EntityIdValue): Promise<void> {
    if (!isPersistedEntityId(channelId)) {
      return;
    }

    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('MarkChannelAsRead', channelId);
    } catch (error) {
      log.warn('ChatHub', '标记频道已读失败:', error);
    }
  }

  private registerEventHandlers(): void {
    if (!this.connection) {
      return;
    }

    this.connection.onreconnecting(() => {
      useChatStore.getState().setConnectionState('reconnecting');
    });

    this.connection.onreconnected(async () => {
      useChatStore.getState().setConnectionState('connected');
      await this.joinActiveChannelIfNeeded();
    });

    this.connection.onclose(() => {
      useChatStore.getState().setConnectionState('disconnected');
    });

    this.connection.on('MessageReceived', (message: ChannelMessageVo) => {
      useChatStore.getState().addMessage(message);
    });

    this.connection.on('MessageRecalled', (payload: MessageRecalledPayload) => {
      useChatStore.getState().recallMessage(payload.channelId, payload.messageId);
    });

    this.connection.on('ChannelUnreadChanged', (payload: ChannelUnreadChangedPayload) => {
      useChatStore.getState().updateUnread(payload);
    });

    this.connection.on('ConversationStateChanged', () => {
      useChatStore.getState().notifyConversationStateChanged();
    });

    this.connection.on('UserTyping', (payload: UserTypingPayload) => {
      useChatStore.getState().setTypingUser(payload.channelId, payload.userId, payload.userName);

      const channelKey = normalizeEntityId(payload.channelId) ?? 'unknown-channel';
      const userKey = normalizeEntityId(payload.userId) ?? 'unknown-user';
      const timerKey = `${channelKey}:${userKey}`;
      const oldTimer = this.typingTimers.get(timerKey);
      if (oldTimer) {
        window.clearTimeout(oldTimer);
      }

      const timer = window.setTimeout(() => {
        useChatStore.getState().removeTypingUser(payload.channelId, payload.userId);
        this.typingTimers.delete(timerKey);
      }, 3000);
      this.typingTimers.set(timerKey, timer);
    });
  }
}

export const chatHub = new ChatHubService();
