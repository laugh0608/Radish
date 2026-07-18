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

**文件位置**：`Frontend/radish.client/src/components/notification/NotificationList.tsx`

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

**文件位置**：`Frontend/radish.client/src/components/notification/NotificationItem.tsx`

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

**文件位置**：`Frontend/radish.client/src/desktop/StatusBar.tsx`

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

**文件位置**：`Frontend/radish.client/src/desktop/Shell.tsx`

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
// Frontend/radish.client/src/desktop/Shell.tsx
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

**文档版本**：v1.2
**状态**：当前实现可用，文中代码片段以历史方案参考为主
**关联文档**：
- [通知系统总体规划](/guide/notification-realtime)
- [通知系统实现细节](/guide/notification-implementation)
- [通知系统 API 文档](/guide/notification-api)
