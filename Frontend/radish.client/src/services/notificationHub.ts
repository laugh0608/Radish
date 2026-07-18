import * as signalR from '@microsoft/signalr';
import type { NotificationInboxChangedVo } from '@radish/http';
import { notificationInboxSync } from '@/services/notificationInboxSync';
import { useNotificationStore } from '@/stores/notificationStore';
import { useAuthStore } from '@/stores/authStore';
import { tokenService } from './tokenService';
import { log } from '@/utils/logger';
import { getSignalrHubUrl } from '@/config/env';

function getHubUrl(): string {
  // 使用统一的 SignalR Hub URL 配置
  return `${getSignalrHubUrl()}/hub/notification`;
}

/** SignalR Hub 连接管理器 */
class NotificationHubService {
  private connection: signalR.HubConnection | null = null;
  private isStarting = false;
  private retryCount = 0;
  private maxRetries = 5;
  private startRequestId = 0;
  private readonly serverTimeoutMs = 60_000;
  private readonly keepAliveIntervalMs = 15_000;

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

    // 必须在首次 await 前占有启动锁，避免多个壳层同时通过认证检查后互相 stop 正在协商的连接。
    this.isStarting = true;
    const store = useNotificationStore.getState();

    try {
      // 1. 检查认证状态
      const { isAuthenticated } = useAuthStore.getState();
      if (!isAuthenticated) {
        log.warn('[NotificationHub] 未登录，跳过连接');
        store.setConnectionState('disconnected');
        return;
      }

      // 2. 检查 Token 有效性
      const token = await tokenService.getValidAccessToken();
      if (!token) {
        log.warn('[NotificationHub] Token 无效，跳过连接');
        store.setConnectionState('disconnected');
        return;
      }

      const requestId = ++this.startRequestId;
      store.setConnectionState('connecting');

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
          accessTokenFactory: async () => await tokenService.getValidAccessToken() || '',
          // 移除 skipNegotiation，让 SignalR 先协商再升级到 WebSocket
          // 这样可以兼容 YARP 反向代理
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 重连策略：0s, 2s, 5s, 10s, 30s, 然后每30s重试
            const delays = [0, 2000, 5000, 10000, 30000];
            return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
          }
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      // 适度放宽服务端超时，降低弱网/代理抖动下的误断开概率
      this.connection.serverTimeoutInMilliseconds = this.serverTimeoutMs;
      this.connection.keepAliveIntervalInMilliseconds = this.keepAliveIntervalMs;

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

      log.debug('[NotificationHub] 连接成功');
      store.setConnectionState('connected');
      this.retryCount = 0;
      void notificationInboxSync.reconcile({ refreshListWhenChanged: true }).catch((error) => {
        log.warn('[NotificationHub] 连接成功后的权威状态对账失败', error);
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);

      // React StrictMode / 生命周期竞态：start 尚未完成就 stop() 会触发 AbortError
      const isLifecycleCancellation = message.includes('Failed to start the HttpConnection before stop() was called')
        || message.includes('connection was stopped during negotiation');
      if (isLifecycleCancellation) {
        log.warn('[NotificationHub] 连接启动被取消（start/stop 竞态）');
        store.setConnectionState('disconnected');
        if (useAuthStore.getState().isAuthenticated) {
          setTimeout(() => this.start(), 250);
        }
        return;
      }

      log.error('[NotificationHub] 连接失败:', error);
      store.setConnectionState('disconnected');

      // 重试逻辑
      if (this.retryCount < this.maxRetries) {
        this.retryCount++;
        const delay = Math.min(1000 * Math.pow(2, this.retryCount), 30000);
        log.debug(`[NotificationHub] ${delay}ms 后重试 (${this.retryCount}/${this.maxRetries})`);
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
        log.debug('[NotificationHub] 连接已断开');
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (!message.includes('Failed to start the HttpConnection before stop() was called')) {
          log.error('[NotificationHub] 断开连接失败:', error);
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
      log.debug('[NotificationHub] 正在重连...');
      useNotificationStore.getState().setConnectionState('reconnecting');
    });

    this.connection.onreconnected(() => {
      log.debug('[NotificationHub] 重连成功');
      useNotificationStore.getState().setConnectionState('connected');
      void notificationInboxSync.reconcile({ refreshListWhenChanged: true }).catch((error) => {
        log.warn('[NotificationHub] 重连后的权威状态对账失败', error);
      });
    });

    this.connection.onclose((error) => {
      log.debug('[NotificationHub] 连接关闭', error);
      useNotificationStore.getState().setConnectionState('disconnected');
    });

    this.connection.on('NotificationInboxChanged', (change: NotificationInboxChangedVo) => {
      log.debug('[NotificationHub] 收件箱 revision 更新:', change.voRevision);
      notificationInboxSync.handleInboxChanged(change);
    });

    // 旧 Hub 事件在 F4-B-D 完成前继续兼容，但只触发权威摘要对账，不消费本地计数。
    this.connection.on('UnreadCountChanged', () => {
      void notificationInboxSync.reconcile({ refreshListWhenChanged: true }).catch((error) => {
        log.warn('[NotificationHub] 旧未读事件的权威状态对账失败', error);
      });
    });
  }
}

/** 单例导出 */
export const notificationHub = new NotificationHubService();

/** React Hook: 在组件中使用 */
export function useNotificationHub() {
  return {
    start: () => notificationHub.start(),
    stop: () => notificationHub.stop()
  };
}
