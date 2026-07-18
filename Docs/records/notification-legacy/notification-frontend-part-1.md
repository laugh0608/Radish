# 通知系统前端集成指南

> **文档版本**：v1.3
> **创建日期**：2026-01-06
> **最后更新**：2026-04-01
> **关联文档**：[通知系统总体规划](/guide/notification-realtime)

> **文档状态**：历史前端集成参考。本文的大段代码、安装步骤、目录和 Smoke 口径不再代表当前实现；正式 Web 通知工作区的后续分类、偏好、聚合、目标和 revision 对账统一以 [F4-B 通知中心深化与通知治理](/features/notification-center-deepening) 为准。当前代码事实仍应直接查看文中列出的仓库文件。

本文档提供通知系统前端集成的完整指南，包括 SignalR 连接管理、状态管理、组件实现和最佳实践。

> 注意：本文以下大段代码片段为早期设计 / 方案草稿，当前仓库的真实实现已经前移到新的目录结构。
>
> 当前请优先以这些文件为准：
>
> - `Frontend/radish.client/src/services/notificationHub.ts`
> - `Frontend/radish.client/src/stores/notificationStore.ts`
> - `Frontend/radish.client/src/apps/notification/NotificationApp.tsx`
> - `Frontend/radish.ui/src/components/Notification/`
>
> 当前前端通知主线已具备：
>
> - Shell 层统一建立 SignalR 连接
> - Dock / Store 未读数同步
> - 通知应用内的列表、已读、全部已读、删除、基础跳转
> - 共享 `Toast` 的自动消失与剩余时间进度条
> - 通知触发者头像优先使用服务端返回值，缺失时再按 `triggerId` 拉取公开资料兜底，避免非聊天类通知长期显示默认占位头像
>
> 当前首版联调仍应重点关注：
>
> - 新通知接收后 Dock 未读数与通知应用列表是否一致
> - 从通知列表点击进入业务页时，已读状态与跳转链路是否同时成立
> - 删除未读通知后，通知应用、Dock 角标与服务端未读数是否一致
> - 多端同时在线时，`NotificationRead / AllNotificationsRead` 的同步是否稳定
> - 非聊天类通知在缺少 `voTriggerAvatar` 时，前端头像兜底是否仍能稳定回填到列表项
>
> Flutter Android 的最小通知回流边界见 [Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff)。

## 0. 当前最小人工验收顺序（首版 Smoke）

适用范围：通知中心、Dock 未读角标、实时推送、临时 Toast 预览。

前置条件：

- 用户 A 已登录 `radish.client`，且 Shell 中 `NotificationHub` 已连接成功。
- 如需验证多端同步，准备第二个同账号会话 A2。
- 用户 B 或后台触发入口可向用户 A 产生一条真实通知。

推荐执行顺序：

1. 触发一条新通知，确认桌面右上角出现实时 Toast 预览，Dock 未读角标同步增加。
2. 打开通知中心，确认新通知出现在列表顶部，标题、内容、时间和通知类型样式正确。
3. 优先验证一条非聊天类通知，确认触发者头像在服务端缺失头像字段时也能被前端兜底回填。
4. 点击该通知，确认会静默标记已读并跳转到对应业务页；返回通知中心后，该条通知状态应已更新，Dock 角标同步减少。
5. 再触发一条新通知后直接在通知中心执行删除，确认列表项消失，Dock 角标与服务端未读数保持一致。
6. 在第二个同账号会话 A2 中把通知标记已读或全部已读，确认当前会话能收到 `NotificationRead / AllNotificationsRead` 同步。

当前建议记录项：

- 是否成功收到实时 Toast 预览
- 未读角标、通知列表、服务端未读数是否三方一致
- 触发者头像是否正确展示，特别是非聊天类通知的头像兜底是否生效
- 跳转是否落到预期业务页
- 多端同步是否存在明显延迟或丢事件

## 1. 依赖安装

### 1.1 安装 SignalR 客户端

```bash
npm install @microsoft/signalr --workspace=radish.client
```

### 1.2 类型定义

确保 `types/notification.ts` 文件已创建（参见 [API 文档](/guide/notification-api#6-typescript-类型定义)）。

---

## 2. SignalR 连接管理

### 2.1 NotificationHub 服务类

**文件位置**：`Frontend/radish.client/src/shared/services/notificationHub.ts`

```typescript
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import type {
  UnreadCountChangedPayload,
  NotificationReceivedPayload,
  NotificationReadPayload
} from '@/types/notification';

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';

export interface NotificationHubEvents {
  onUnreadCountChanged: (payload: UnreadCountChangedPayload) => void;
  onNotificationReceived: (payload: NotificationReceivedPayload) => void;
  onNotificationRead: (payload: NotificationReadPayload) => void;
  onAllNotificationsRead: () => void;
  onConnectionStateChanged: (state: ConnectionState) => void;
}

class NotificationHubService {
  private connection: HubConnection | null = null;
  private events: Partial<NotificationHubEvents> = {};
  private _connectionState: ConnectionState = 'Disconnected';

  /**
   * 获取当前连接状态
   */
  get connectionState(): ConnectionState {
    return this._connectionState;
  }

  /**
   * 检查是否已连接
   */
  get isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  /**
   * 初始化连接
   */
  async connect(accessTokenFactory: () => string | Promise<string>): Promise<void> {
    if (this.connection) {
      console.warn('[NotificationHub] 连接已存在，跳过初始化');
      return;
    }

    this.setConnectionState('Connecting');

    try {
      this.connection = new HubConnectionBuilder()
        .withUrl('/hub/notification', {
          accessTokenFactory
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 重连策略：0s, 2s, 5s, 10s, 30s, 然后每 30s 重试
            const delays = [0, 2000, 5000, 10000, 30000];
            return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
          }
        })
        .configureLogging(
          import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning
        )
        .build();

      this.setupEventHandlers();
      this.setupLifecycleHandlers();

      await this.connection.start();

      this.setConnectionState('Connected');
      console.log('[NotificationHub] 连接成功');
    } catch (error) {
      this.setConnectionState('Disconnected');
      console.error('[NotificationHub] 连接失败:', error);
      throw error;
    }
  }

  /**
   * 断开连接
   */
  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    try {
      await this.connection.stop();
      this.connection = null;
      this.setConnectionState('Disconnected');
      console.log('[NotificationHub] 连接已断开');
    } catch (error) {
      console.error('[NotificationHub] 断开连接失败:', error);
    }
  }

  /**
   * 注册事件监听
   */
  on<K extends keyof NotificationHubEvents>(
    event: K,
    callback: NotificationHubEvents[K]
  ): void {
    this.events[event] = callback;
  }

  /**
   * 移除事件监听
   */
  off<K extends keyof NotificationHubEvents>(event: K): void {
    delete this.events[event];
  }

  /**
   * 调用服务端方法：标记已读
   */
  async markAsRead(notificationId: number): Promise<void> {
    if (!this.isConnected) {
      throw new Error('NotificationHub 未连接');
    }

    await this.connection!.invoke('MarkAsRead', notificationId);
  }

  /**
   * 调用服务端方法：标记全部已读
   */
  async markAllAsRead(): Promise<void> {
    if (!this.isConnected) {
      throw new Error('NotificationHub 未连接');
    }

    await this.connection!.invoke('MarkAllAsRead');
  }

  private setupEventHandlers(): void {
    if (!this.connection) return;

    // 未读数变化
    this.connection.on('UnreadCountChanged', (payload: UnreadCountChangedPayload) => {
      console.log('[NotificationHub] UnreadCountChanged:', payload);
      this.events.onUnreadCountChanged?.(payload);
    });

    // 新通知到达
    this.connection.on('NotificationReceived', (payload: NotificationReceivedPayload) => {
      console.log('[NotificationHub] NotificationReceived:', payload);
      this.events.onNotificationReceived?.(payload);
    });

    // 其他端标记已读
    this.connection.on('NotificationRead', (payload: NotificationReadPayload) => {
      console.log('[NotificationHub] NotificationRead:', payload);
      this.events.onNotificationRead?.(payload);
    });

    // 其他端标记全部已读
    this.connection.on('AllNotificationsRead', () => {
      console.log('[NotificationHub] AllNotificationsRead');
      this.events.onAllNotificationsRead?.();
    });
  }

  private setupLifecycleHandlers(): void {
    if (!this.connection) return;

    this.connection.onreconnecting((error) => {
      console.log('[NotificationHub] 正在重连...', error);
      this.setConnectionState('Reconnecting');
    });

    this.connection.onreconnected((connectionId) => {
      console.log('[NotificationHub] 重连成功:', connectionId);
      this.setConnectionState('Connected');
    });

    this.connection.onclose((error) => {
      console.log('[NotificationHub] 连接关闭:', error);
      this.setConnectionState('Disconnected');
    });
  }

  private setConnectionState(state: ConnectionState): void {
    this._connectionState = state;
    this.events.onConnectionStateChanged?.(state);
  }
}

// 导出单例
export const notificationHub = new NotificationHubService();
```

---

## 3. 状态管理

### 3.1 Zustand Store

**文件位置**：`Frontend/radish.client/src/stores/notificationStore.ts`

```typescript
import { create } from 'zustand';
import type {
  NotificationVo,
  NotificationPriority
} from '@/types/notification';
import { notificationHub, ConnectionState } from '@/shared/services/notificationHub';
import { notificationApi } from '@/shared/api/notification';
import { toast } from '@radish/ui';

interface NotificationState {
  // 状态
  notifications: NotificationVo[];
  unreadCount: number;
  connectionState: ConnectionState;
  isLoading: boolean;
  hasMore: boolean;
  currentPage: number;

  // Actions
  initialize: (accessTokenFactory: () => string | Promise<string>) => Promise<void>;
  destroy: () => Promise<void>;
  fetchNotifications: (reset?: boolean) => Promise<void>;
  fetchUnreadCount: () => Promise<void>;
  markAsRead: (notificationIds: number[]) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  deleteNotifications: (notificationIds: number[]) => Promise<void>;

  // 内部方法
  _addNotification: (notification: NotificationVo) => void;
  _setUnreadCount: (count: number) => void;
  _setConnectionState: (state: ConnectionState) => void;
  _markNotificationsAsRead: (notificationIds: number[]) => void;
  _markAllNotificationsAsRead: () => void;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  // 初始状态
  notifications: [],
  unreadCount: 0,
  connectionState: 'Disconnected',
  isLoading: false,
  hasMore: true,
  currentPage: 1,

  /**
   * 初始化通知系统
   */
  initialize: async (accessTokenFactory) => {
    try {
      // 注册事件监听
      notificationHub.on('onUnreadCountChanged', (payload) => {
        get()._setUnreadCount(payload.unreadCount);
      });

      notificationHub.on('onNotificationReceived', (payload) => {
        // 添加新通知到列表
        get()._addNotification({
          ...payload,
          isRead: false,
          readAt: null,
          extData: null
        });

        // 显示 Toast 提示
        showNotificationToast(payload);
      });

      notificationHub.on('onNotificationRead', (payload) => {
        get()._markNotificationsAsRead(payload.notificationIds);
      });

      notificationHub.on('onAllNotificationsRead', () => {
        get()._markAllNotificationsAsRead();
      });

      notificationHub.on('onConnectionStateChanged', (state) => {
        get()._setConnectionState(state);
      });

      // 建立连接
      await notificationHub.connect(accessTokenFactory);

      // 连接成功后获取通知列表
      await get().fetchNotifications(true);
    } catch (error) {
      console.error('[NotificationStore] 初始化失败:', error);
      // 降级：使用轮询
      startFallbackPolling(get);
    }
  },

  /**
   * 销毁通知系统
   */
  destroy: async () => {
    notificationHub.off('onUnreadCountChanged');
    notificationHub.off('onNotificationReceived');
    notificationHub.off('onNotificationRead');
    notificationHub.off('onAllNotificationsRead');
    notificationHub.off('onConnectionStateChanged');

    await notificationHub.disconnect();

    set({
      notifications: [],
      unreadCount: 0,
      connectionState: 'Disconnected',
      isLoading: false,
      hasMore: true,
      currentPage: 1
    });
  },

  /**
   * 获取通知列表
   */
  fetchNotifications: async (reset = false) => {
    const state = get();
    if (state.isLoading) return;
    if (!reset && !state.hasMore) return;

    const page = reset ? 1 : state.currentPage;

    set({ isLoading: true });

    try {
      const response = await notificationApi.getList({
        pageIndex: page,
        pageSize: 20
      });

      if (response.isSuccess && response.responseData) {
        const { data, pageCount } = response.responseData;

        set({
          notifications: reset ? data : [...state.notifications, ...data],
          currentPage: page + 1,
          hasMore: page < pageCount,
          isLoading: false
        });
      }
    } catch (error) {
      console.error('[NotificationStore] 获取通知列表失败:', error);
      set({ isLoading: false });
    }
  },

  /**
   * 获取未读数量（兜底）
   */
  fetchUnreadCount: async () => {
    try {
      const response = await notificationApi.getUnreadCount();
      if (response.isSuccess && response.responseData) {
        set({ unreadCount: response.responseData.unreadCount });
      }
    } catch (error) {
      console.error('[NotificationStore] 获取未读数失败:', error);
    }
  },

  /**
   * 标记已读
   */
  markAsRead: async (notificationIds) => {
    // 乐观更新
    get()._markNotificationsAsRead(notificationIds);

    try {
      const response = await notificationApi.markAsRead({ notificationIds });
      if (response.isSuccess && response.responseData) {
        set({ unreadCount: response.responseData.unreadCount });
      }
    } catch (error) {
      console.error('[NotificationStore] 标记已读失败:', error);
      // 回滚（重新获取列表）
      await get().fetchNotifications(true);
    }
  },

  /**
   * 标记全部已读
   */
  markAllAsRead: async () => {
    // 乐观更新
    get()._markAllNotificationsAsRead();

    try {
      await notificationApi.markAllAsRead();
      set({ unreadCount: 0 });
    } catch (error) {
      console.error('[NotificationStore] 标记全部已读失败:', error);
      await get().fetchNotifications(true);
    }
  },

  /**
   * 删除通知
   */
  deleteNotifications: async (notificationIds) => {
    // 乐观更新
    set((state) => ({
      notifications: state.notifications.filter(
        (n) => !notificationIds.includes(n.id)
      )
    }));

    try {
      await notificationApi.deleteNotifications({ notificationIds });
    } catch (error) {
      console.error('[NotificationStore] 删除通知失败:', error);
      await get().fetchNotifications(true);
    }
  },

  // 内部方法
  _addNotification: (notification) => {
    set((state) => ({
      notifications: [notification, ...state.notifications],
      unreadCount: state.unreadCount + 1
    }));
  },

  _setUnreadCount: (count) => {
    set({ unreadCount: count });
  },

  _setConnectionState: (state) => {
    set({ connectionState: state });
  },

  _markNotificationsAsRead: (notificationIds) => {
    set((state) => ({
      notifications: state.notifications.map((n) =>
        notificationIds.includes(n.id)
          ? { ...n, isRead: true, readAt: new Date().toISOString() }
          : n
      ),
      unreadCount: Math.max(
        0,
        state.unreadCount - notificationIds.filter(
          (id) => state.notifications.find((n) => n.id === id && !n.isRead)
        ).length
      )
    }));
  },

  _markAllNotificationsAsRead: () => {
    set((state) => ({
      notifications: state.notifications.map((n) => ({
        ...n,
        isRead: true,
        readAt: n.readAt || new Date().toISOString()
      })),
      unreadCount: 0
    }));
  }
}));

/**
 * 显示通知 Toast
 */
function showNotificationToast(notification: {
  title: string;
  priority: number;
  link?: string | null;
}): void {
  const type = notification.priority >= 3 ? 'info' : 'default';

  toast({
    type,
    title: notification.title,
    duration: 5000,
    onClick: () => {
      if (notification.link) {
        // 跳转到通知关联页面
        window.location.href = notification.link;
      }
    }
  });
}

/**
 * 降级轮询
 */
let fallbackTimer: NodeJS.Timeout | null = null;

function startFallbackPolling(getState: () => NotificationState): void {
  if (fallbackTimer) return;

  console.log('[NotificationStore] 启动降级轮询模式');

  fallbackTimer = setInterval(() => {
    getState().fetchUnreadCount();
  }, 60000); // 每 60 秒轮询一次
}

export function stopFallbackPolling(): void {
  if (fallbackTimer) {
    clearInterval(fallbackTimer);
    fallbackTimer = null;
  }
}
```

---

## 4. API 客户端

### 4.1 Notification API

**文件位置**：`Frontend/radish.client/src/shared/api/notification.ts`

```typescript
import { request } from './request';
import type {
  NotificationListResponse,
  UnreadCountResponse,
  MarkAsReadResponse,
  DeleteNotificationResponse,
  NotificationSettingsResponse,
  NotificationVo
} from '@/types/notification';

const BASE_URL = '/api/v1/Notification';

export interface GetListParams {
  pageIndex?: number;
  pageSize?: number;
  isRead?: boolean;
  type?: string;
}

export interface MarkAsReadParams {
  notificationIds: number[];
}

export interface DeleteParams {
  notificationIds: number[];
}

export interface UpdateSettingsParams {
  settings: Array<{
    notificationType: string;
    isEnabled?: boolean;
    enableInApp?: boolean;
    enableEmail?: boolean;
    enableSound?: boolean;
  }>;
}

export const notificationApi = {
  /**
   * 获取未读数量
   */
  getUnreadCount: () =>
    request.get<UnreadCountResponse>(`${BASE_URL}/UnreadCount`),

  /**
   * 获取通知列表
   */
  getList: (params: GetListParams = {}) =>
    request.get<NotificationListResponse>(`${BASE_URL}/List`, { params }),

  /**
   * 获取通知详情
   */
  getDetail: (id: number) =>
    request.get<NotificationVo>(`${BASE_URL}/${id}`),

  /**
   * 标记已读
   */
  markAsRead: (data: MarkAsReadParams) =>
    request.put<MarkAsReadResponse>(`${BASE_URL}/MarkAsRead`, data),

  /**
   * 标记全部已读
   */
  markAllAsRead: (type?: string) =>
    request.put<MarkAsReadResponse>(
      `${BASE_URL}/MarkAllAsRead${type ? `?type=${type}` : ''}`
    ),

  /**
   * 删除通知
   */
  deleteNotifications: (data: DeleteParams) =>
    request.delete<DeleteNotificationResponse>(`${BASE_URL}/Delete`, { data }),

  /**
   * 获取通知设置
   */
  getSettings: () =>
    request.get<NotificationSettingsResponse>(`${BASE_URL}/Settings`),

  /**
   * 更新通知设置
   */
  updateSettings: (data: UpdateSettingsParams) =>
    request.put(`${BASE_URL}/Settings`, data)
};
```

---

## 5. React Hook

### 5.1 useNotificationHub Hook

**文件位置**：`Frontend/radish.client/src/shared/hooks/useNotificationHub.ts`

```typescript
import { useEffect, useCallback } from 'react';
import { useNotificationStore } from '@/stores/notificationStore';
import { useAuthStore } from '@/stores/authStore';

/**
 * 通知 Hub Hook
 *
 * 在需要使用通知功能的组件中调用，自动管理连接生命周期
 */
export function useNotificationHub() {
  const {
    notifications,
    unreadCount,
    connectionState,
    isLoading,
    hasMore,
    initialize,
    destroy,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    deleteNotifications
  } = useNotificationStore();

  const { getAccessToken, isAuthenticated } = useAuthStore();

  // 初始化连接
  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    initialize(getAccessToken);

    return () => {
      destroy();
    };
  }, [isAuthenticated, initialize, destroy, getAccessToken]);

  // 加载更多
  const loadMore = useCallback(() => {
    if (!isLoading && hasMore) {
      fetchNotifications(false);
    }
  }, [isLoading, hasMore, fetchNotifications]);

  // 刷新列表
  const refresh = useCallback(() => {
    fetchNotifications(true);
  }, [fetchNotifications]);

  return {
    // 状态
    notifications,
    unreadCount,
    connectionState,
    isLoading,
    hasMore,
    isConnected: connectionState === 'Connected',
    isReconnecting: connectionState === 'Reconnecting',

    // 方法
    loadMore,
    refresh,
    markAsRead,
    markAllAsRead,
    deleteNotifications
  };
}
```

### 5.2 useNotificationToast Hook

**文件位置**：`Frontend/radish.client/src/shared/hooks/useNotificationToast.ts`

```typescript
import { useEffect } from 'react';
import { notificationHub } from '@/shared/services/notificationHub';
import { toast } from '@radish/ui';
import type { NotificationReceivedPayload } from '@/types/notification';

/**
 * 通知 Toast Hook
 *
 * 在 Shell 层调用，用于显示新通知的 Toast 提示
 */
export function useNotificationToast() {
  useEffect(() => {
    const handleNotification = (payload: NotificationReceivedPayload) => {
      // 根据优先级决定 Toast 类型
      const toastType = payload.priority >= 3 ? 'info' : 'default';

      toast({
        type: toastType,
        title: payload.title,
        message: payload.content,
        duration: 5000,
        action: payload.link
          ? {
              label: '查看',
              onClick: () => {
                window.location.href = payload.link!;
              }
            }
          : undefined
      });
    };

    notificationHub.on('onNotificationReceived', handleNotification);

    return () => {
      notificationHub.off('onNotificationReceived');
    };
  }, []);
}
```

---

## 6. UI 组件

### 6.1 NotificationBadge 组件

**文件位置**：`Frontend/radish.client/src/components/notification/NotificationBadge.tsx`

```tsx
import { FC } from 'react';
import { useNotificationStore } from '@/stores/notificationStore';
import styles from './NotificationBadge.module.css';

interface NotificationBadgeProps {
  onClick?: () => void;
}

export const NotificationBadge: FC<NotificationBadgeProps> = ({ onClick }) => {
  const { unreadCount, connectionState } = useNotificationStore();

  const displayCount = unreadCount > 99 ? '99+' : unreadCount;

  return (
    <button className={styles.badge} onClick={onClick}>
      <svg
        className={styles.icon}
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
        />
      </svg>

      {/* 未读数 Badge */}
      {unreadCount > 0 && (
        <span className={styles.count}>{displayCount}</span>
      )}

      {/* 连接状态指示器 */}
      {connectionState !== 'Connected' && (
        <span
          className={`${styles.status} ${styles[connectionState.toLowerCase()]}`}
          title={
            connectionState === 'Reconnecting'
              ? '正在重连...'
              : connectionState === 'Connecting'
              ? '正在连接...'
              : '离线'
          }
        />
      )}
    </button>
  );
};
```

**样式文件**：`NotificationBadge.module.css`

```css
.badge {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  padding: 0;
  border: none;
  background: transparent;
  cursor: pointer;
  border-radius: 50%;
  transition: background-color 0.2s;
}

.badge:hover {
  background: rgba(255, 255, 255, 0.1);
}

.icon {
  width: 20px;
  height: 20px;
  color: #fff;
}

.count {
  position: absolute;
  top: -2px;
  right: -2px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  font-size: 10px;
  font-weight: 600;
  line-height: 16px;
  color: #fff;
  text-align: center;
  background: #ff4757;
  border-radius: 8px;
}

.status {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  border: 2px solid #1a1a2e;
}

.status.disconnected {
  background: #ff4757;
}

.status.connecting,
.status.reconnecting {
  background: #ffa502;
  animation: pulse 1s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
```

### 6.2 NotificationCenter 组件

**文件位置**：`Frontend/radish.client/src/components/notification/NotificationCenter.tsx`

```tsx
import { FC, useState, useRef, useEffect } from 'react';
import { useNotificationHub } from '@/shared/hooks/useNotificationHub';
import { NotificationBadge } from './NotificationBadge';
import { NotificationList } from './NotificationList';
import styles from './NotificationCenter.module.css';

export const NotificationCenter: FC = () => {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const {
    notifications,
    unreadCount,
    isLoading,
    hasMore,
    loadMore,
    markAsRead,
    markAllAsRead
  } = useNotificationHub();

  // 点击外部关闭
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const handleToggle = () => {
    setIsOpen(!isOpen);
