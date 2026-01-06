import * as signalR from '@microsoft/signalr';
import { useNotificationStore, type NotificationItem } from '@/stores/notificationStore';

const defaultApiBase = 'http://localhost:5100'; // 临时直连 API，绕过 Gateway

function getHubUrl(): string {
  // 临时直连 API 进行测试
  return `${defaultApiBase}/hub/notification`;
}

function getAccessToken(): string | null {
  if (typeof window === 'undefined') return null;
  return window.localStorage.getItem('access_token');
}

/** SignalR Hub 连接管理器 */
class NotificationHubService {
  private connection: signalR.HubConnection | null = null;
  private isStarting = false;
  private retryCount = 0;
  private maxRetries = 5;
  private startRequestId = 0;

  /** 获取当前连接实例 */
  getConnection(): signalR.HubConnection | null {
    return this.connection;
  }

  /** 启动连接 */
  async start(): Promise<void> {
    // 已连接或正在连接中，跳过
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }
    if (this.isStarting) {
      return;
    }

    const token = getAccessToken();
    if (!token) {
      console.warn('[NotificationHub] 未找到 access_token，跳过连接');
      return;
    }

    const requestId = ++this.startRequestId;

    this.isStarting = true;
    const store = useNotificationStore.getState();
    store.setConnectionState('connecting');

    try {
      // 如果已有连接实例，先停止（避免并发/残留连接）
      if (this.connection) {
        try {
          await this.connection.stop();
        } catch {
          // 忽略 stop 失败（比如 StrictMode 导致 start/stop 竞态）
        }
      }

      // 创建新连接
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(getHubUrl(), {
          accessTokenFactory: () => token,
          transport: signalR.HttpTransportType.WebSockets,
          skipNegotiation: true
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 重连策略：0s, 2s, 5s, 10s, 30s, 然后每30s重试
            const delays = [0, 2000, 5000, 10000, 30000];
            return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // 注册事件处理器
      this.registerEventHandlers();

      // 启动连接
      await this.connection.start();

      // 如果 start 过程中外部调用了 stop()（例如 React StrictMode effect cleanup），则直接断开并退出
      if (requestId !== this.startRequestId) {
        try {
          await this.connection.stop();
        } catch {
          // ignore
        }
        store.setConnectionState('disconnected');
        return;
      }

      console.log('[NotificationHub] 连接成功');
      store.setConnectionState('connected');
      this.retryCount = 0;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);

      // React StrictMode / 生命周期竞态：start 尚未完成就 stop() 会触发 AbortError
      if (message.includes('Failed to start the HttpConnection before stop() was called')) {
        console.warn('[NotificationHub] 连接启动被取消（start/stop 竞态）');
        store.setConnectionState('disconnected');
        return;
      }

      console.error('[NotificationHub] 连接失败:', error);
      store.setConnectionState('disconnected');

      // 重试逻辑
      if (this.retryCount < this.maxRetries) {
        this.retryCount++;
        const delay = Math.min(1000 * Math.pow(2, this.retryCount), 30000);
        console.log(`[NotificationHub] ${delay}ms 后重试 (${this.retryCount}/${this.maxRetries})`);
        setTimeout(() => this.start(), delay);
      }
    } finally {
      this.isStarting = false;
    }
  }

  /** 停止连接 */
  async stop(): Promise<void> {
    // 标记：后续任何正在进行的 start() 都视为过期
    this.startRequestId++;

    if (this.connection) {
      try {
        await this.connection.stop();
        console.log('[NotificationHub] 连接已断开');
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (!message.includes('Failed to start the HttpConnection before stop() was called')) {
          console.error('[NotificationHub] 断开连接失败:', error);
        }
      }
      this.connection = null;
    }
    const store = useNotificationStore.getState();
    store.setConnectionState('disconnected');
  }

  /** 注册服务端事件处理器 */
  private registerEventHandlers(): void {
    if (!this.connection) return;

    // 连接状态变化
    this.connection.onreconnecting(() => {
      console.log('[NotificationHub] 正在重连...');
      useNotificationStore.getState().setConnectionState('reconnecting');
    });

    this.connection.onreconnected(() => {
      console.log('[NotificationHub] 重连成功');
      useNotificationStore.getState().setConnectionState('connected');
    });

    this.connection.onclose((error) => {
      console.log('[NotificationHub] 连接关闭', error);
      useNotificationStore.getState().setConnectionState('disconnected');
    });

    // 服务端推送事件
    this.connection.on('UnreadCountChanged', (data: { unreadCount: number }) => {
      console.log('[NotificationHub] 未读数更新:', data.unreadCount);
      useNotificationStore.getState().setUnreadCount(data.unreadCount);
    });

    this.connection.on('NewNotification', (notification: NotificationItem) => {
      console.log('[NotificationHub] 新通知:', notification);
      useNotificationStore.getState().addNotification(notification);
    });

    this.connection.on('NotificationRead', (data: { notificationIds: number[] }) => {
      console.log('[NotificationHub] 通知已读（其他端）:', data.notificationIds);
      useNotificationStore.getState().markAsRead(data.notificationIds);
    });

    this.connection.on('AllNotificationsRead', () => {
      console.log('[NotificationHub] 全部已读（其他端）');
      useNotificationStore.getState().markAllAsRead();
    });
  }

  /** 客户端调用：标记通知已读 */
  async markAsRead(notificationId: number): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      console.warn('[NotificationHub] 未连接，无法标记已读');
      return;
    }

    try {
      await this.connection.invoke('MarkAsRead', notificationId);
      // 本地也更新状态
      useNotificationStore.getState().markAsRead([notificationId]);
    } catch (error) {
      console.error('[NotificationHub] 标记已读失败:', error);
    }
  }

  /** 客户端调用：标记全部已读 */
  async markAllAsRead(): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      console.warn('[NotificationHub] 未连接，无法标记全部已读');
      return;
    }

    try {
      await this.connection.invoke('MarkAllAsRead');
      // 本地也更新状态
      useNotificationStore.getState().markAllAsRead();
    } catch (error) {
      console.error('[NotificationHub] 标记全部已读失败:', error);
    }
  }
}

/** 单例导出 */
export const notificationHub = new NotificationHubService();

/** React Hook: 在组件中使用 */
export function useNotificationHub() {
  return {
    start: () => notificationHub.start(),
    stop: () => notificationHub.stop(),
    markAsRead: (id: number) => notificationHub.markAsRead(id),
    markAllAsRead: () => notificationHub.markAllAsRead()
  };
}
