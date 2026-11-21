import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/authStore';
import { resolveHubSession } from './hubSession';
import { WebHubReconnect, type HubCredential } from './webHubPolicy';
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

class CommentHubService {
  private connection: signalR.HubConnection | null = null;
  private credential: HubCredential | null = null;
  private isStarting = false;
  private readonly webReconnect = new WebHubReconnect();
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
    if ((this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) || this.isStarting) {
      return;
    }

    const requestId = ++this.startRequestId;
    this.isStarting = true;

    try {
      const session = await resolveHubSession('comment', useAuthStore.getState().isAuthenticated);
      if (!session || requestId !== this.startRequestId) return;

      const previous = this.connection;
      this.connection = null;
      if (previous) {
        try {
          await previous.stop();
        } catch {
          // ignore stale connection stop failure
        }
      }
      if (requestId !== this.startRequestId) return;

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(session.url, {
          accessTokenFactory: session.accessTokenFactory,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();
      this.connection = connection;
      this.credential = session.credential;

      this.connection.serverTimeoutInMilliseconds = 60_000;
      this.connection.keepAliveIntervalInMilliseconds = 15_000;

      this.registerEventHandlers(connection, session.credential === 'web');
      await connection.start();

      if (requestId !== this.startRequestId) {
        await connection.stop();
        return;
      }

      log.debug('CommentHub', '连接成功');
      this.webReconnect.reset();
    } catch (error) {
      if (requestId !== this.startRequestId) return;
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes('Failed to start the HttpConnection before stop() was called')) {
        log.warn('CommentHub', '连接失败，评论区将保留 REST 刷新:', error);
      }
      if (this.credential === 'web') this.scheduleWebReconnect();
    } finally {
      if (requestId === this.startRequestId) {
        this.isStarting = false;
        if (this.credential === 'anonymous') void this.syncSessionRoute();
      }
    }
  }

  async stop(): Promise<void> {
    this.startRequestId++;
    this.webReconnect.reset();
    this.isStarting = false;
    this.credential = null;

    const connection = this.connection;
    this.connection = null;
    if (!connection) {
      return;
    }

    try {
      await connection.stop();
      log.debug('CommentHub', '连接已断开');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes('Failed to start the HttpConnection before stop() was called')) {
        log.warn('CommentHub', '关闭连接失败:', error);
      }
    }
  }

  async syncSessionRoute(): Promise<void> {
    if (this.isStarting || this.credential === 'web' || this.joinedPosts.size === 0 ||
        tokenService.sessionMode !== 'cookie' || !tokenService.getAccessToken()) return;
    await this.restart();
  }

  async restart(): Promise<void> {
    const shouldRejoin = this.joinedPosts.size > 0;
    await this.stop();

    if (!shouldRejoin) {
      return;
    }

    await this.start();
    await this.rejoinPosts();
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

  private scheduleWebReconnect(): void {
    this.webReconnect.schedule(() => this.joinedPosts.size > 0 && tokenService.sessionMode === 'cookie' &&
      useAuthStore.getState().isAuthenticated, () => void this.start().then(() => this.rejoinPosts()));
  }

  private registerEventHandlers(connection: signalR.HubConnection, web: boolean): void {
    connection.onreconnected(() => {
      if (connection !== this.connection) return;
      this.webReconnect.reset();
      log.debug('CommentHub', '重连成功');
      void this.rejoinPosts();
    });

    connection.onclose((error) => {
      if (connection !== this.connection) return;
      if (error) {
        log.debug('CommentHub', '连接关闭:', error);
      }
      if (web) this.scheduleWebReconnect();
    });

    connection.on('CommentCreated', (payload: CommentRealtimeEvent) => this.emit('CommentCreated', payload));
    connection.on('CommentUpdated', (payload: CommentRealtimeEvent) => this.emit('CommentUpdated', payload));
    connection.on('CommentDeleted', (payload: CommentRealtimeEvent) => this.emit('CommentDeleted', payload));
    connection.on('CommentLikeChanged', (payload: CommentRealtimeEvent) => this.emit('CommentLikeChanged', payload));
    connection.on('CommentHighlightsChanged', (payload: CommentHighlightRealtimeEvent) => this.emit('CommentHighlightsChanged', payload));
    connection.on('CommentTyping', (payload: CommentTypingRealtimeEvent) => this.emit('CommentTyping', payload));
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
