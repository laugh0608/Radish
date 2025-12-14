# API 客户端使用指南

## 概述

`@radish/ui` 提供了统一的 API 客户端，封装了常见的 HTTP 请求操作，并与后端的 `MessageModel` 响应格式完美配合。

## 核心特性

- ✅ 统一的 API 响应格式（对应后端 MessageModel）
- ✅ 自动处理认证 token
- ✅ 请求超时控制
- ✅ 拦截器支持（请求/响应/错误）
- ✅ 完整的 TypeScript 类型支持
- ✅ 便捷的 GET/POST/PUT/DELETE 方法

## 快速开始

### 1. 配置 API 客户端

在应用入口或 API 模块中配置：

```typescript
import { configureApiClient } from '@radish/ui';

configureApiClient({
  // 基础 URL
  baseUrl: 'https://localhost:5000',

  // 请求超时时间（毫秒）
  timeout: 30000,

  // 获取 token 的函数
  getToken: () => {
    if (typeof window !== 'undefined') {
      return window.localStorage.getItem('access_token');
    }
    return null;
  },

  // 请求拦截器（可选）
  onRequest: (url, options) => {
    console.log('Request:', url, options);
  },

  // 响应拦截器（可选）
  onResponse: (response) => {
    console.log('Response:', response.status);
  },

  // 错误拦截器（可选）
  onError: (error) => {
    console.error('Error:', error);
  },
});
```

### 2. 发起请求

```typescript
import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';

// GET 请求
const result = await apiGet<User[]>('/api/v1/Users', { withAuth: true });

// POST 请求
const result = await apiPost<User>('/api/v1/Users', {
  username: 'admin',
  email: 'admin@example.com',
}, { withAuth: true });

// PUT 请求
const result = await apiPut<User>('/api/v1/Users/1', {
  email: 'newemail@example.com',
}, { withAuth: true });

// DELETE 请求
const result = await apiDelete<void>('/api/v1/Users/1', { withAuth: true });
```

### 3. 处理响应

所有便捷方法返回 `ParsedApiResponse<T>` 类型：

```typescript
interface ParsedApiResponse<T> {
  ok: boolean;         // 请求是否成功
  data?: T;           // 响应数据
  message?: string;   // 错误消息
  code?: string;      // 错误码
  statusCode?: number;// HTTP 状态码
}
```

**示例：**

```typescript
const result = await apiGet<User[]>('/api/v1/Users', { withAuth: true });

if (result.ok && result.data) {
  // 成功
  console.log('Users:', result.data);
} else {
  // 失败
  console.error('Error:', result.message);
}
```

## API 类型定义

### ApiResponse<T>

对应后端的 `MessageModel<T>` 结构：

```typescript
interface ApiResponse<T> {
  isSuccess: boolean;         // 操作是否成功
  statusCode: number;         // HTTP 状态码
  messageInfo: string;        // 消息内容
  messageInfoDev?: string;    // 开发环境消息
  responseData?: T;           // 响应数据
  code?: string;              // 错误码
  messageKey?: string;        // 国际化消息键
}
```

### PagedResponse<T>

分页数据结构：

```typescript
interface PagedResponse<T> {
  page: number;       // 当前页码
  pageSize: number;   // 每页数量
  dataCount: number;  // 总数据量
  pageCount: number;  // 总页数
  data: T[];          // 数据列表
}
```

### ApiRequestOptions

请求配置选项：

```typescript
interface ApiRequestOptions extends RequestInit {
  withAuth?: boolean;   // 是否携带认证信息
  baseUrl?: string;     // 基础 URL（覆盖全局配置）
  timeout?: number;     // 请求超时时间（覆盖全局配置）
}
```

## 完整示例

### Console 应用示例

```typescript
// src/api/clients.ts
import {
  configureApiClient,
  configureErrorHandling,
  apiGet,
  apiPost,
  apiPut,
  apiDelete,
  message,
} from '@radish/ui';
import type {
  PagedResponse,
  ParsedApiResponse,
} from '@radish/ui';
import type { OidcClient } from '../types/oidc';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl,
  timeout: 30000,
  getToken: () => {
    if (typeof window !== 'undefined') {
      return window.localStorage.getItem('access_token');
    }
    return null;
  },
});

// 配置错误处理
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => {
    message.error(msg);
  },
});

// 定义 API
export const clientApi = {
  /**
   * 获取客户端列表
   */
  async getClients(params?: {
    page?: number;
    pageSize?: number;
    keyword?: string;
  }): Promise<ParsedApiResponse<PagedResponse<OidcClient>>> {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.keyword) query.set('keyword', params.keyword);

    return apiGet<PagedResponse<OidcClient>>(
      `/api/v1/Client/GetClients?${query}`,
      { withAuth: true }
    );
  },

  /**
   * 创建客户端
   */
  async createClient(data: CreateClientRequest) {
    return apiPost<{ clientId: string; clientSecret: string }>(
      '/api/v1/Client/CreateClient',
      data,
      { withAuth: true }
    );
  },

  /**
   * 更新客户端
   */
  async updateClient(id: string, data: Partial<OidcClient>) {
    return apiPut<string>(
      `/api/v1/Client/UpdateClient/${id}`,
      data,
      { withAuth: true }
    );
  },

  /**
   * 删除客户端
   */
  async deleteClient(id: string) {
    return apiDelete<string>(
      `/api/v1/Client/DeleteClient/${id}`,
      { withAuth: true }
    );
  },
};
```

### 组件中使用

```typescript
// src/pages/Applications.tsx
import { useEffect, useState } from 'react';
import { message } from '@radish/ui';
import { clientApi } from '../../api/clients';
import type { OidcClient } from '../../types/oidc';

export const Applications = () => {
  const [clients, setClients] = useState<OidcClient[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void loadClients();
  }, []);

  const loadClients = async () => {
    setLoading(true);
    try {
      const result = await clientApi.getClients({ page: 1, pageSize: 100 });

      if (result.ok && result.data) {
        setClients(result.data.data);
      } else {
        // 错误消息已自动显示（如果配置了 autoShowMessage）
        console.error('Load failed:', result.message);
      }
    } catch (error) {
      // 网络错误等异常情况
      message.error('加载失败');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    const result = await clientApi.deleteClient(id);

    if (result.ok) {
      message.success('删除成功');
      await loadClients();
    }
  };

  // ...
};
```

## 高级用法

### 使用原始 apiFetch

如果便捷方法不满足需求，可以使用底层的 `apiFetch`：

```typescript
import { apiFetch, parseApiResponse } from '@radish/ui';
import type { ApiResponse } from '@radish/ui';

const response = await apiFetch('/api/v1/Custom', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Custom-Header': 'value',
  },
  body: JSON.stringify({ data: 'custom' }),
  withAuth: true,
  timeout: 60000, // 自定义超时
});

const json = await response.json() as ApiResponse<MyType>;
const result = parseApiResponse(json);
```

### 动态修改配置

```typescript
import { configureApiClient, getApiClientConfig } from '@radish/ui';

// 获取当前配置
const currentConfig = getApiClientConfig();
console.log('Current baseUrl:', currentConfig.baseUrl);

// 修改配置（例如切换环境）
configureApiClient({
  baseUrl: 'https://api.production.com',
});
```

### 请求拦截器示例

```typescript
configureApiClient({
  baseUrl: 'https://localhost:5000',

  // 添加请求日志
  onRequest: (url, options) => {
    console.log(`[API Request] ${options.method || 'GET'} ${url}`);
    console.log('Headers:', options.headers);
  },

  // 检查响应状态
  onResponse: (response) => {
    if (!response.ok) {
      console.warn(`[API Warning] ${response.status} ${response.statusText}`);
    }
  },

  // 统一错误处理
  onError: (error) => {
    console.error('[API Error]', error);

    // 可以在这里添加错误上报逻辑
    if (import.meta.env.PROD) {
      // reportError(error);
    }
  },
});
```

## 最佳实践

### 1. 统一配置

**推荐：** 在应用入口配置一次

```typescript
// ✅ 在 src/api/index.ts 或 src/main.tsx 中配置
configureApiClient({ /* ... */ });
```

**不推荐：** 在每个 API 文件中重复配置

```typescript
// ❌ 不要这样做
// api/users.ts
configureApiClient({ /* ... */ });

// api/posts.ts
configureApiClient({ /* ... */ });
```

### 2. 类型安全

**推荐：** 使用泛型指定响应类型

```typescript
// ✅ 有类型提示
const result = await apiGet<User[]>('/api/v1/Users');
if (result.ok && result.data) {
  result.data.forEach(user => {
    console.log(user.name); // 有类型提示
  });
}
```

**不推荐：** 不指定类型

```typescript
// ❌ 没有类型提示
const result = await apiGet('/api/v1/Users');
```

### 3. 错误处理

**推荐：** 配合自动错误提示

```typescript
// ✅ 配置自动提示
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => message.error(msg),
});

// API 调用时无需手动处理错误消息
const result = await apiGet('/api/v1/Users');
if (result.ok) {
  // 处理成功情况
}
// 错误消息已自动显示
```

**不推荐：** 每次都手动处理

```typescript
// ❌ 重复代码
const result = await apiGet('/api/v1/Users');
if (!result.ok) {
  message.error(result.message || '请求失败');
}
```

### 4. 环境变量

**推荐：** 使用环境变量配置 baseUrl

```typescript
// ✅ 支持不同环境
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';

configureApiClient({
  baseUrl: apiBaseUrl,
});
```

**.env 文件：**

```env
# 开发环境
VITE_API_BASE_URL=https://localhost:5000

# 生产环境
VITE_API_BASE_URL=https://api.production.com
```

## 常见问题

### Q: 如何处理认证失败（401）？

**A:** 可以在错误拦截器中统一处理：

```typescript
configureApiClient({
  onError: (error) => {
    if (error.message.includes('401')) {
      // 清除 token
      localStorage.removeItem('access_token');
      // 跳转到登录页
      window.location.href = '/login';
    }
  },
});
```

### Q: 如何取消请求？

**A:** 使用 `AbortController`：

```typescript
const controller = new AbortController();

const result = await apiFetch('/api/v1/LongRequest', {
  signal: controller.signal,
});

// 取消请求
controller.abort();
```

### Q: 如何处理文件上传？

**A:** 使用 `FormData`：

```typescript
const formData = new FormData();
formData.append('file', file);

const response = await apiFetch('/api/v1/Upload', {
  method: 'POST',
  body: formData,
  withAuth: true,
  // 不要设置 Content-Type，浏览器会自动设置
});
```

### Q: 为什么响应没有数据？

**A:** 检查后端返回的字段名是否正确：

- `isSuccess` - 是否成功
- `responseData` - 响应数据（不是 `data`）
- `messageInfo` - 消息内容（不是 `message`）

## 相关文档

- [错误处理指南](./ErrorHandlingGuide.md)
- [UI 组件库概览](./UIComponentLibrary.md)
