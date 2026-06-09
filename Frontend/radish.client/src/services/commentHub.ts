import * as signalR from '@microsoft/signalr';
import { getSignalrHubUrl } from '@/config/env';
import { tokenService } from './tokenService';
import { log } from '@/utils/logger';
import type { CommentNode } from '@/api/forum';
import type { LongId } from '@/api/user';

export interface CommentRealtimeEvent {
  voPostId: LongId;
  voCommentId: LongId;
  voParentCommentId?: LongId | null;
  voRootCommentId?: LongId | null;
  voComment?: CommentNode | null;
  voLikeCount?: number | null;
  voEventTime?: string;
}

export interface CommentHighlightRealtimeEvent {
  voPostId: LongId;
  voParentCommentId?: LongId | null;
  voHighlightType: number;
  voChanged: boolean;
  voCurrentCommentIds: LongId[];
  voEventTime?: string;
}

export interface CommentTypingRealtimeEvent {
  voPostId: LongId;
  voCommentId?: LongId | null;
  voUserId: LongId;
  voUserName: string;
  voEventTime?: string;
}

type CommentRealtimeEventMap = {
  CommentCreated: CommentRealtimeEvent;
  CommentUpdated: CommentRealtimeEvent;
  CommentDeleted: CommentRealtimeEvent;
  CommentLikeChanged: CommentRealtimeEvent;
  CommentHighlightsChanged: CommentHighlightRealtimeEvent;
  CommentTyping: CommentTypingRealtimeEvent;
};

type CommentRealtimeEventName = keyof CommentRealtimeEventMap;
type Listener<TEventName extends CommentRealtimeEventName> = (payload: CommentRealtimeEventMap[TEventName]) => void;

function getHubUrl(): string {
  return `${getSignalrHubUrl()}/hub/comment`;
}

class CommentHubService {
  private connection: signalR.HubConnection | null = null;
  private isStarting = false;
  private startRequestId = 0;
  private joinedPosts = new Map<string, LongId>();
  private listeners: {
    [K in CommentRealtimeEventName]: Set<Listener<K>>;
  } = {
    CommentCreated: new Set(),
    CommentUpdated: new Set(),
    CommentDeleted: new Set(),
    CommentLikeChanged: new Set(),
    CommentHighlightsChanged: new Set(),
    CommentTyping: new Set(),
  };

  async start(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected || this.isStarting) {
      return;
    }

    const requestId = ++this.startRequestId;
    this.isStarting = true;

    try {
      if (this.connection) {
        try {
          await this.connection.stop();
        } catch {
          // ignore stale connection stop failure
        }
      }

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(getHubUrl(), {
          accessTokenFactory: () => tokenService.getAccessToken() || '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.connection.serverTimeoutInMilliseconds = 60_000;
      this.connection.keepAliveIntervalInMilliseconds = 15_000;

      this.registerEventHandlers();
      await this.connection.start();

      if (requestId !== this.startRequestId) {
        await this.connection.stop();
        return;
      }

      log.debug('CommentHub', '连接成功');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes('Failed to start the HttpConnection before stop() was called')) {
        log.warn('CommentHub', '连接失败，评论区将保留 REST 刷新:', error);
      }
    } finally {
      this.isStarting = false;
    }
  }

  async joinPost(postId: LongId): Promise<void> {
    const postKey = String(postId);
    this.joinedPosts.set(postKey, postId);
    await this.ensureConnected();
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('JoinPost', postId);
    } catch (error) {
      log.warn('CommentHub', '加入帖子评论组失败:', error);
    }
  }

  async leavePost(postId: LongId): Promise<void> {
    const postKey = String(postId);
    this.joinedPosts.delete(postKey);
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('LeavePost', postId);
    } catch (error) {
      log.warn('CommentHub', '离开帖子评论组失败:', error);
    }
  }

  async startTyping(postId: LongId, commentId?: LongId | null): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('StartTyping', postId, commentId ?? null);
    } catch (error) {
      log.debug('CommentHub', '上报评论输入状态失败:', error);
    }
  }

  subscribe<TEventName extends CommentRealtimeEventName>(
    eventName: TEventName,
    listener: Listener<TEventName>
  ): () => void {
    this.listeners[eventName].add(listener);
    return () => {
      this.listeners[eventName].delete(listener);
    };
  }

  private async ensureConnected(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected || this.isStarting) {
      return;
    }

    await this.start();
  }

  private registerEventHandlers(): void {
    if (!this.connection) {
      return;
    }

    this.connection.onreconnected(() => {
      log.debug('CommentHub', '重连成功');
      void this.rejoinPosts();
    });

    this.connection.onclose((error) => {
      if (error) {
        log.debug('CommentHub', '连接关闭:', error);
      }
    });

    this.connection.on('CommentCreated', (payload: CommentRealtimeEvent) => this.emit('CommentCreated', payload));
    this.connection.on('CommentUpdated', (payload: CommentRealtimeEvent) => this.emit('CommentUpdated', payload));
    this.connection.on('CommentDeleted', (payload: CommentRealtimeEvent) => this.emit('CommentDeleted', payload));
    this.connection.on('CommentLikeChanged', (payload: CommentRealtimeEvent) => this.emit('CommentLikeChanged', payload));
    this.connection.on('CommentHighlightsChanged', (payload: CommentHighlightRealtimeEvent) => this.emit('CommentHighlightsChanged', payload));
    this.connection.on('CommentTyping', (payload: CommentTypingRealtimeEvent) => this.emit('CommentTyping', payload));
  }

  private async rejoinPosts(): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected || this.joinedPosts.size === 0) {
      return;
    }

    for (const postId of this.joinedPosts.values()) {
      try {
        await this.connection.invoke('JoinPost', postId);
      } catch (error) {
        log.warn('CommentHub', '重连后恢复帖子评论组失败:', error);
      }
    }
  }

  private emit<TEventName extends CommentRealtimeEventName>(
    eventName: TEventName,
    payload: CommentRealtimeEventMap[TEventName]
  ): void {
    for (const listener of this.listeners[eventName]) {
      listener(payload);
    }
  }
}

export const commentHub = new CommentHubService();
