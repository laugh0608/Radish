# 前端 API 客户端使用指南

本页保留为“前端 API 客户端”兼容入口。当前仓库的真实实现与长期口径都以 `@radish/http` 为准；`@radish/ui` 只负责 UI 组件、消息提示与部分前端展示工具。

更完整的类型与 API 说明见 [@radish/http 文档](./http-client.md)。

## 当前职责划分

- `@radish/http`
  - HTTP 请求封装
  - `configureApiClient`
  - `apiGet / apiPost / apiPut / apiDelete`
  - Token 刷新与认证续期
- `@radish/ui`
  - 组件库
  - `message` 等交互反馈
  - 前端展示辅助工具

## 快速开始

### 1. 配置客户端

```typescript
import { configureApiClient } from '@radish/http';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
  timeout: 30000,
  getToken: () => {
    if (typeof window !== 'undefined') {
      return window.localStorage.getItem('access_token');
    }

    return null;
  },
});
```

### 2. 发起请求

```typescript
import { apiGet, apiPost } from '@radish/http';

const products = await apiGet<PagedResponse<Product>>(
  '/api/v1/Shop/GetProducts?pageIndex=1&pageSize=20',
  { withAuth: true }
);

const created = await apiPost<Product>(
  '/api/v1/Shop/CreateProduct',
  payload,
  { withAuth: true }
);
```

### 3. 在组件里配合 `@radish/ui`

```typescript
import { message } from '@radish/ui';
import { apiDelete } from '@radish/http';

const result = await apiDelete(`/api/v1/Client/DeleteClient/${id}`, {
  withAuth: true,
});

if (result.ok) {
  message.success('删除成功');
} else {
  message.error(result.message || '删除失败');
}
```

## 401 自动续期

```typescript
import { configureTokenRefresh } from '@radish/http';

configureTokenRefresh({
  refreshEndpoint: `${authServerBaseUrl}/connect/token`,
  getRefreshToken: () => localStorage.getItem('refresh_token'),
  onTokenRefreshed: (accessToken, refreshToken) => {
    localStorage.setItem('access_token', accessToken);

    if (refreshToken) {
      localStorage.setItem('refresh_token', refreshToken);
    }
  },
  onRefreshFailed: () => {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    window.location.href = '/login';
  },
});
```

注意：

- 登录时必须申请 `offline_access` scope 才能拿到 `refresh_token`
- 统一通过 `apiGet / apiPost / apiPut / apiDelete` 触发 401 自动刷新并重试
- 不要在业务模块里自己包一层平行的 fetch/axios 客户端

## 特殊场景

- 需要上传进度时，可以临时使用 `XMLHttpRequest`
- 但必须先从 `@radish/http` 的 `getApiClientConfig()` 读取 `baseUrl` 与 token
- 其余请求仍然维持统一客户端，不要扩散成第二套 HTTP 规范

## 相关文档

- [@radish/http 文档](./http-client.md)
- [鉴权与授权指南](/guide/authentication)
- [API 说明索引](/guide/api-index)
