# 通知系统前端集成指南

> **文档版本**：v1.1
> **创建日期**：2026-01-06
> **最后更新**：2026-01-07
> **关联文档**：[通知系统总体规划](/guide/notification-realtime)

本文档提供通知系统前端集成的完整指南，包括 SignalR 连接管理、状态管理、组件实现和最佳实践。

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

**文件位置**：`radish.client/src/shared/services/notificationHub.ts`

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

**文件位置**：`radish.client/src/stores/notificationStore.ts`

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

**文件位置**：`radish.client/src/shared/api/notification.ts`

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

**文件位置**：`radish.client/src/shared/hooks/useNotificationHub.ts`

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

**文件位置**：`radish.client/src/shared/hooks/useNotificationToast.ts`

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

**文件位置**：`radish.client/src/components/notification/NotificationBadge.tsx`

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

**文件位置**：`radish.client/src/components/notification/NotificationCenter.tsx`

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
  };

  const handleNotificationClick = (notificationId: number, link?: string | null) => {
    // 标记已读
    markAsRead([notificationId]);

    // 跳转
    if (link) {
      setIsOpen(false);
      window.location.href = link;
    }
  };

  const handleMarkAllAsRead = () => {
    markAllAsRead();
  };

  return (
    <div className={styles.container} ref={containerRef}>
      <NotificationBadge onClick={handleToggle} />

      {isOpen && (
        <div className={styles.dropdown}>
          <div className={styles.header}>
            <h3 className={styles.title}>通知</h3>
            {unreadCount > 0 && (
              <button
                className={styles.markAllRead}
                onClick={handleMarkAllAsRead}
              >
                全部已读
              </button>
            )}
          </div>

          <NotificationList
            notifications={notifications}
            isLoading={isLoading}
            hasMore={hasMore}
            onLoadMore={loadMore}
            onNotificationClick={handleNotificationClick}
          />

          <div className={styles.footer}>
            <a href="/notifications" className={styles.viewAll}>
              查看全部通知
            </a>
          </div>
        </div>
      )}
    </div>
  );
};
```

### 6.3 NotificationList 组件

**文件位置**：`radish.client/src/components/notification/NotificationList.tsx`

```tsx
import { FC, useRef, useCallback } from 'react';
import type { NotificationVo } from '@/types/notification';
import { NotificationItem } from './NotificationItem';
import styles from './NotificationList.module.css';

interface NotificationListProps {
  notifications: NotificationVo[];
  isLoading: boolean;
  hasMore: boolean;
  onLoadMore: () => void;
  onNotificationClick: (id: number, link?: string | null) => void;
}

export const NotificationList: FC<NotificationListProps> = ({
  notifications,
  isLoading,
  hasMore,
  onLoadMore,
  onNotificationClick
}) => {
  const observerRef = useRef<IntersectionObserver | null>(null);

  // 无限滚动
  const lastItemRef = useCallback(
    (node: HTMLDivElement | null) => {
      if (isLoading) return;

      if (observerRef.current) {
        observerRef.current.disconnect();
      }

      observerRef.current = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting && hasMore) {
          onLoadMore();
        }
      });

      if (node) {
        observerRef.current.observe(node);
      }
    },
    [isLoading, hasMore, onLoadMore]
  );

  if (notifications.length === 0 && !isLoading) {
    return (
      <div className={styles.empty}>
        <p>暂无通知</p>
      </div>
    );
  }

  return (
    <div className={styles.list}>
      {notifications.map((notification, index) => (
        <div
          key={notification.id}
          ref={index === notifications.length - 1 ? lastItemRef : null}
        >
          <NotificationItem
            notification={notification}
            onClick={() => onNotificationClick(notification.id, notification.link)}
          />
        </div>
      ))}

      {isLoading && (
        <div className={styles.loading}>
          <span>加载中...</span>
        </div>
      )}
    </div>
  );
};
```

### 6.4 NotificationItem 组件

**文件位置**：`radish.client/src/components/notification/NotificationItem.tsx`

```tsx
import { FC } from 'react';
import type { NotificationVo } from '@/types/notification';
import { formatRelativeTime } from '@/shared/utils/date';
import styles from './NotificationItem.module.css';

interface NotificationItemProps {
  notification: NotificationVo;
  onClick: () => void;
}

export const NotificationItem: FC<NotificationItemProps> = ({
  notification,
  onClick
}) => {
  const {
    title,
    content,
    triggerName,
    triggerAvatar,
    isRead,
    createdAt
  } = notification;

  return (
    <div
      className={`${styles.item} ${!isRead ? styles.unread : ''}`}
      onClick={onClick}
    >
      <div className={styles.avatar}>
        {triggerAvatar ? (
          <img src={triggerAvatar} alt={triggerName || ''} />
        ) : (
          <div className={styles.defaultAvatar}>
            {triggerName?.[0] || '系'}
          </div>
        )}
      </div>

      <div className={styles.content}>
        <div className={styles.title}>{title}</div>
        <div className={styles.text}>{content}</div>
        <div className={styles.time}>{formatRelativeTime(createdAt)}</div>
      </div>

      {!isRead && <div className={styles.dot} />}
    </div>
  );
};
```

---

## 7. 集成到 Shell

### 7.1 StatusBar 集成

**文件位置**：`radish.client/src/desktop/StatusBar.tsx`

```tsx
import { FC } from 'react';
import { NotificationCenter } from '@/components/notification/NotificationCenter';
import { useNotificationToast } from '@/shared/hooks/useNotificationToast';
import styles from './StatusBar.module.css';

export const StatusBar: FC = () => {
  // 启用通知 Toast
  useNotificationToast();

  return (
    <div className={styles.statusBar}>
      <div className={styles.left}>
        {/* Logo、菜单等 */}
      </div>

      <div className={styles.right}>
        {/* 其他状态栏项目 */}

        {/* 通知中心 */}
        <NotificationCenter />

        {/* 用户菜单等 */}
      </div>
    </div>
  );
};
```

### 7.2 Shell 初始化

**文件位置**：`radish.client/src/desktop/Shell.tsx`

```tsx
import { FC, useEffect } from 'react';
import { useAuthStore } from '@/stores/authStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { StatusBar } from './StatusBar';
import { Desktop } from './Desktop';
import { Dock } from './Dock';

export const Shell: FC = () => {
  const { isAuthenticated, getAccessToken } = useAuthStore();
  const { initialize, destroy } = useNotificationStore();

  // 初始化通知系统
  useEffect(() => {
    if (isAuthenticated) {
      initialize(getAccessToken);
    }

    return () => {
      destroy();
    };
  }, [isAuthenticated, initialize, destroy, getAccessToken]);

  return (
    <div className="shell">
      <StatusBar />
      <Desktop />
      <Dock />
    </div>
  );
};
```

---

## 8. 降级策略

### 8.1 连接失败降级

当 SignalR 连接失败时，自动降级为低频轮询模式。

```typescript
// 在 notificationStore.ts 中已实现
function startFallbackPolling(getState: () => NotificationState): void {
  if (fallbackTimer) return;

  console.log('[NotificationStore] 启动降级轮询模式');

  // 每 60 秒轮询一次（比原来 30 秒更低频）
  fallbackTimer = setInterval(() => {
    getState().fetchUnreadCount();
  }, 60000);
}
```

### 8.2 UI 降级指示

在连接不可用时，显示离线状态指示。

```tsx
// NotificationBadge 组件中已实现
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
```

---

## 9. 测试指南

### 9.1 开发环境测试

```bash
# 1. 启动后端服务
dotnet run --project Radish.Api

# 2. 启动前端开发服务器
npm run dev --workspace=radish.client

# 3. 打开浏览器开发者工具，查看 Network 标签页
# 4. 登录后，应该能看到 WebSocket 连接建立
# 5. 在另一个标签页触发通知（如点赞帖子）
# 6. 观察第一个标签页的 Toast 提示和未读数更新
```

### 9.2 多端同步测试

```bash
# 1. 在两个浏览器窗口登录同一账号
# 2. 在窗口 A 中收到新通知
# 3. 在窗口 B 中标记该通知为已读
# 4. 观察窗口 A 的通知状态是否同步更新
```

### 9.3 降级测试

```bash
# 1. 在 Network 标签页中，将网络设置为 Offline
# 2. 观察连接状态指示器变为离线
# 3. 恢复网络
# 4. 观察自动重连并恢复未读数
```

---

## 10. 最佳实践

### 10.1 性能优化

1. **避免频繁重连**：使用指数退避重连策略
2. **乐观更新**：标记已读时先更新 UI，再调用 API
3. **虚拟列表**：通知列表数量较多时，使用虚拟滚动
4. **按需加载**：只在需要时加载通知详情

### 10.2 用户体验

1. **Toast 提示**：新通知到达时显示 Toast，可点击跳转
2. **声音提示**：高优先级通知可播放提示音（需用户授权）
3. **连接状态**：显示连接状态，让用户了解是否在线
4. **离线提示**：离线时显示"离线"标识，避免用户困惑

### 10.3 错误处理

1. **连接失败**：自动降级为轮询，不影响基本功能
2. **API 调用失败**：显示错误提示，支持重试
3. **乐观更新回滚**：API 调用失败时回滚本地状态

---

## 11. 常见问题

### 11.1 React StrictMode 导致连接失败

**问题描述**

在 React 18+ 开发模式下启用 `<StrictMode>` 时，SignalR 连接可能会失败，表现为：
- WebSocket HTTP 101 握手成功
- 但后端 `OnConnectedAsync` 方法未被调用
- 前端无法收到服务端推送的消息

**根本原因**

React StrictMode 在开发模式下会**故意双重挂载组件**以检测副作用：

```
Mount → 启动 SignalR 连接 → Unmount → cleanup 调用 stop() → Re-mount
              ↓
      WebSocket HTTP 101 成功，但 SignalR 协议握手被中断
```

当 `useEffect` 的 cleanup 函数立即调用 `notificationHub.stop()` 时，会在 SignalR 完成协议握手之前关闭连接，导致 Hub 生命周期方法未执行。

**解决方案**

使用 `useRef` 跟踪连接状态，并延迟 cleanup 执行：

```tsx
// radish.client/src/desktop/Shell.tsx
import { useEffect, useRef } from 'react';
import { notificationHub } from '@/services/notificationHub';

export const Shell = () => {
  // 使用 ref 防止 React StrictMode 双重挂载导致重复连接
  const hasStartedRef = useRef(false);

  useEffect(() => {
    const token = window.localStorage.getItem('access_token');

    // 防止 React StrictMode 导致重复启动连接
    if (token && !hasStartedRef.current) {
      hasStartedRef.current = true;
      void notificationHub.start();
    } else if (!token) {
      hasStartedRef.current = false;
      void notificationHub.stop();
    }

    // cleanup 函数：仅在组件真正卸载时执行
    return () => {
      // 延迟执行 stop，给 StrictMode 的第二次 mount 一个机会
      // 如果是 StrictMode 导致的卸载，会在几毫秒内重新挂载，此时不应 stop
      setTimeout(() => {
        // 再次检查：如果组件已重新挂载，hasStartedRef 会是 true，不执行 stop
        // 只有真正卸载时（用户登出、路由切换），才会执行 stop
        if (!hasStartedRef.current) {
          void notificationHub.stop();
        }
      }, 100);
    };
  }, []);

  return (
    // ...组件内容
  );
};
```

**关键技术点**

1. **useRef 防重复**：`hasStartedRef` 跟踪连接状态，避免 StrictMode 双重挂载时多次调用 `start()`
2. **延迟 cleanup**：`setTimeout(100ms)` 给 StrictMode 重新挂载的时间窗口
3. **智能判断**：cleanup 中再次检查 ref，只在组件真正卸载时执行 `stop()`

**验证方法**

启用 StrictMode 后，检查：
1. 后端日志出现 `[NotificationHub] 连接建立` 日志
2. 前端控制台显示 `[NotificationHub] 连接成功`
3. 前端能正常接收服务端推送的未读数更新

---

**文档版本**：v1.1
**状态**：P0 已实现，P1-P3 待实施
**关联文档**：
- [通知系统总体规划](/guide/notification-realtime)
- [通知系统实现细节](/guide/notification-implementation)
- [通知系统 API 文档](/guide/notification-api)
