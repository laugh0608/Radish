# 错误处理指南

## 概述

`@radish/ui` 提供了统一的错误处理机制，帮助你优雅地处理各种错误情况，并向用户展示友好的错误消息。

## 核心特性

- ✅ 统一的错误处理接口
- ✅ 自动错误消息提示
- ✅ 可配置的错误处理器
- ✅ 网络错误识别
- ✅ HTTP 状态码错误映射
- ✅ 开发环境详细日志

## 快速开始

### 1. 配置错误处理

在应用入口配置：

```typescript
import { configureErrorHandling, message } from '@radish/ui';

configureErrorHandling({
  // 是否自动显示错误消息
  autoShowMessage: true,

  // 消息显示函数
  showMessage: (msg) => {
    message.error(msg);
  },

  // 自定义错误处理器（可选）
  onError: (error, context) => {
    // 错误上报
    console.error('Error occurred:', error, context);

    // 可以在这里添加错误上报服务
    // reportToSentry(error, context);
  },
});
```

### 2. 处理 API 错误

```typescript
import { handleApiError } from '@radish/ui';
import { clientApi } from './api/clients';

const result = await clientApi.getClients();

// 自动处理 API 错误
handleApiError(result);

if (result.ok && result.data) {
  // 成功逻辑
  console.log(result.data);
}
```

### 3. 处理通用错误

```typescript
import { handleError } from '@radish/ui';

try {
  await someOperation();
} catch (error) {
  handleError(error, { operation: 'someOperation' });
}
```

## 错误处理函数

### handleError

通用错误处理函数，支持多种错误类型：

```typescript
function handleError(error: unknown, context?: any): void
```

**参数：**
- `error` - 错误对象（Error、string 或其他类型）
- `context` - 错误上下文信息（可选）

**示例：**

```typescript
import { handleError } from '@radish/ui';

// 处理 Error 对象
try {
  throw new Error('Something went wrong');
} catch (error) {
  handleError(error);
}

// 处理字符串错误
handleError('操作失败');

// 带上下文信息
handleError(error, {
  userId: currentUser.id,
  operation: 'deleteUser',
});
```

### handleApiError

专门处理 API 响应错误：

```typescript
function handleApiError<T>(response: ParsedApiResponse<T>, context?: any): void
```

**参数：**
- `response` - API 响应对象
- `context` - 错误上下文信息（可选）

**示例：**

```typescript
import { handleApiError } from '@radish/ui';
import { userApi } from './api/users';

const handleDelete = async (userId: string) => {
  const result = await userApi.deleteUser(userId);

  // 如果失败，自动显示错误消息
  handleApiError(result, { userId });

  if (result.ok) {
    message.success('删除成功');
    await loadUsers();
  }
};
```

### handleNetworkError

处理网络相关错误：

```typescript
function handleNetworkError(error: unknown): void
```

**识别的错误类型：**
- `AbortError` → "请求超时"
- `Failed to fetch` → "网络连接失败，请检查您的网络"
- 其他网络错误

**示例：**

```typescript
import { handleNetworkError } from '@radish/ui';

try {
  const response = await fetch('/api/data');
} catch (error) {
  // 自动识别并显示友好的网络错误消息
  handleNetworkError(error);
}
```

### handleHttpError

处理 HTTP 状态码错误：

```typescript
function handleHttpError(statusCode: number, message?: string): void
```

**内置状态码映射：**
- `400` → "请求参数错误"
- `401` → "未授权，请重新登录"
- `403` → "拒绝访问"
- `404` → "请求的资源不存在"
- `408` → "请求超时"
- `500` → "服务器内部错误"
- `502` → "网关错误"
- `503` → "服务不可用"
- `504` → "网关超时"

**示例：**

```typescript
import { handleHttpError } from '@radish/ui';

const response = await fetch('/api/data');

if (!response.ok) {
  handleHttpError(response.status);
}
```

### withErrorHandling

错误处理装饰器，自动捕获异步函数的错误：

```typescript
function withErrorHandling<T extends (...args: any[]) => Promise<any>>(
  fn: T,
  context?: any
): T
```

**示例：**

```typescript
import { withErrorHandling } from '@radish/ui';

// 原始函数
const loadData = async () => {
  const response = await fetch('/api/data');
  return response.json();
};

// 包装后自动处理错误
const loadDataWithErrorHandling = withErrorHandling(
  loadData,
  { operation: 'loadData' }
);

// 使用
await loadDataWithErrorHandling();
// 如果出错，会自动显示错误消息
```

## 完整示例

### Console 应用示例

```typescript
// src/main.tsx 或 src/api/index.ts
import { configureErrorHandling, message } from '@radish/ui';

// 配置全局错误处理
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => {
    message.error(msg);
  },
  onError: (error, context) => {
    // 开发环境：打印详细错误
    if (import.meta.env.DEV) {
      console.error('[Error]', error, context);
    }

    // 生产环境：上报错误
    if (import.meta.env.PROD) {
      // reportError(error, context);
    }
  },
});
```

### 组件中使用

```typescript
// src/pages/Users.tsx
import { useState, useEffect } from 'react';
import { message, handleError, handleApiError } from '@radish/ui';
import { userApi } from '../../api/users';
import type { User } from '../../types/user';

export const Users = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(false);

  // 加载用户列表
  const loadUsers = async () => {
    setLoading(true);
    try {
      const result = await userApi.getUsers();

      // 自动处理错误
      handleApiError(result, { operation: 'loadUsers' });

      if (result.ok && result.data) {
        setUsers(result.data);
      }
    } catch (error) {
      // 处理网络错误等异常情况
      handleError(error, { operation: 'loadUsers' });
    } finally {
      setLoading(false);
    }
  };

  // 删除用户
  const handleDelete = async (userId: string) => {
    try {
      const result = await userApi.deleteUser(userId);

      handleApiError(result, { operation: 'deleteUser', userId });

      if (result.ok) {
        message.success('删除成功');
        await loadUsers();
      }
    } catch (error) {
      handleError(error, { operation: 'deleteUser', userId });
    }
  };

  useEffect(() => {
    void loadUsers();
  }, []);

  // ...
};
```

### 自定义错误处理

```typescript
// src/utils/errorHandler.ts
import {
  configureErrorHandling,
  handleError as baseHandleError,
} from '@radish/ui';
import { message } from '@radish/ui';
import * as Sentry from '@sentry/react';

// 配置 Sentry
if (import.meta.env.PROD) {
  Sentry.init({
    dsn: import.meta.env.VITE_SENTRY_DSN,
  });
}

// 配置错误处理
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => {
    message.error(msg);
  },
  onError: (error, context) => {
    // 开发环境日志
    if (import.meta.env.DEV) {
      console.group('❌ Error Handler');
      console.error('Error:', error);
      console.log('Context:', context);
      console.groupEnd();
    }

    // 生产环境上报
    if (import.meta.env.PROD) {
      Sentry.captureException(error, {
        extra: context,
      });
    }
  },
});

// 导出自定义的错误处理函数
export function handleError(error: unknown, context?: any) {
  // 特殊错误处理逻辑
  if (error instanceof NetworkError) {
    message.warning('网络连接不稳定，请稍后重试');
    return;
  }

  // 默认处理
  baseHandleError(error, context);
}
```

## 高级用法

### 条件性错误提示

```typescript
import { handleApiError } from '@radish/ui';

const result = await userApi.getUsers();

// 只在特定情况下显示错误
if (!result.ok) {
  if (result.code === 'PERMISSION_DENIED') {
    message.warning('您没有权限查看用户列表');
  } else {
    handleApiError(result);
  }
}
```

### 错误重试机制

```typescript
import { handleError } from '@radish/ui';

async function fetchWithRetry(url: string, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        return response;
      }
    } catch (error) {
      if (i === maxRetries - 1) {
        // 最后一次失败才显示错误
        handleError(error, { url, attempt: i + 1 });
        throw error;
      }
      // 等待后重试
      await new Promise(resolve => setTimeout(resolve, 1000 * (i + 1)));
    }
  }
}
```

### 批量操作错误处理

```typescript
import { handleError, message } from '@radish/ui';

async function batchDelete(ids: string[]) {
  const errors: Array<{ id: string; error: string }> = [];

  for (const id of ids) {
    try {
      const result = await userApi.deleteUser(id);
      if (!result.ok) {
        errors.push({ id, error: result.message || '删除失败' });
      }
    } catch (error) {
      errors.push({ id, error: '网络错误' });
    }
  }

  if (errors.length === 0) {
    message.success('全部删除成功');
  } else if (errors.length === ids.length) {
    message.error('全部删除失败');
  } else {
    message.warning(`部分删除失败：${errors.length}/${ids.length}`);
    console.error('Failed deletions:', errors);
  }
}
```

## 最佳实践

### 1. 全局配置

**推荐：** 在应用入口配置一次

```typescript
// ✅ src/main.tsx
import { configureErrorHandling, message } from '@radish/ui';

configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => message.error(msg),
});
```

**不推荐：** 在每个组件中重复配置

```typescript
// ❌ 不要这样做
function MyComponent() {
  configureErrorHandling({ /* ... */ });
  // ...
}
```

### 2. 使用自动错误提示

**推荐：** 启用 `autoShowMessage`

```typescript
// ✅ 配置自动提示
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => message.error(msg),
});

// API 调用
const result = await userApi.getUsers();
// 错误消息自动显示，无需手动处理
```

**不推荐：** 每次都手动提示

```typescript
// ❌ 重复代码
const result = await userApi.getUsers();
if (!result.ok) {
  message.error(result.message || '请求失败');
}
```

### 3. 添加错误上下文

**推荐：** 提供错误上下文信息

```typescript
// ✅ 有助于调试
handleError(error, {
  userId: currentUser.id,
  operation: 'deleteUser',
  timestamp: Date.now(),
});
```

**不推荐：** 没有上下文信息

```typescript
// ❌ 难以调试
handleError(error);
```

### 4. 区分开发和生产环境

**推荐：** 根据环境调整行为

```typescript
// ✅ 区分环境
configureErrorHandling({
  onError: (error, context) => {
    if (import.meta.env.DEV) {
      console.error('[Dev Error]', error, context);
    } else {
      // reportToSentry(error, context);
    }
  },
});
```

## 常见问题

### Q: 如何禁用自动错误提示？

**A:** 设置 `autoShowMessage: false`：

```typescript
configureErrorHandling({
  autoShowMessage: false,
});
```

然后手动处理错误：

```typescript
const result = await userApi.getUsers();
if (!result.ok) {
  // 自定义错误处理
  console.error(result.message);
}
```

### Q: 如何自定义错误消息？

**A:** 在调用前检查错误类型：

```typescript
const result = await userApi.getUsers();

if (!result.ok) {
  if (result.code === 'PERMISSION_DENIED') {
    message.warning('您没有权限');
  } else {
    handleApiError(result);
  }
}
```

### Q: 如何处理 401 未授权错误？

**A:** 在 `onError` 中统一处理：

```typescript
configureErrorHandling({
  onError: (error, context) => {
    if (context?.statusCode === 401) {
      localStorage.removeItem('access_token');
      window.location.href = '/login';
    }
  },
});
```

### Q: 如何集成第三方错误监控（如 Sentry）？

**A:** 在 `onError` 回调中上报：

```typescript
import * as Sentry from '@sentry/react';

configureErrorHandling({
  onError: (error, context) => {
    if (import.meta.env.PROD) {
      Sentry.captureException(error, {
        extra: context,
      });
    }
  },
});
```

## 相关文档

- [API 客户端使用指南](./APIClientGuide.md)
- [UI 组件库概览](./UIComponentLibrary.md)
