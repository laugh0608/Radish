# 前端 API 客户端使用指南

本页保留为“前端 API 客户端”兼容入口。当前仓库的真实实现与长期口径都以 `@radish/http` 为准；`@radish/ui` 只负责 UI 组件、消息提示与部分前端展示工具。

更完整的类型与 API 说明见 [@radish/http 文档](./http-client.md)。

## 当前职责划分

- `@radish/http`
  - HTTP 请求封装
  - `configureApiClient`
  - `apiGet / apiPost / apiPut / apiDelete`
  - Token 刷新与认证续期
  - `Accept-Language`、`MessageKey / MessageArguments` 翻译与结构化 `ApiResponseError`
- `@radish/ui`
  - 组件库
  - `message` 等交互反馈
  - 前端展示辅助工具

## 快速开始

### 1. 配置客户端

```typescript
import { configureApiClient } from '@radish/http';
import i18n from './i18n';
import { env } from './config/env';
import { tokenService } from './services/tokenService';

const apiBaseUrl = env.apiBaseUrl;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
  timeout: 30000,
  getToken: () => tokenService.getAccessToken(),
  getLanguage: () => i18n.resolvedLanguage ?? i18n.language,
  translateMessage: (key, args) => (
    i18n.exists(key)
      ? i18n.t(key, Object.fromEntries((args ?? []).map((value, index) => [index, value])))
      : undefined
  ),
});
```

### 2. 发起请求

```typescript
import { apiGet, apiPost, type PagedResponse } from '@radish/http';

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
import { apiDelete, createApiResponseError } from '@radish/http';
import { useTranslation } from 'react-i18next';

const { t } = useTranslation();
const result = await apiDelete(`/api/v1/Client/DeleteClient/${id}`, {
  withAuth: true,
});

if (result.ok) {
  message.success(t('common.deleteSuccess'));
} else {
  const error = createApiResponseError(result, t('common.deleteFailed'));
  message.error(error.message);
}
```

需要区分 not-found、conflict 或权限时，只读取真实 HTTP status、稳定 `Code` 或明确数据状态；不得匹配 `result.message` 的中英文文本。完整规则见[错误处理指南](/frontend/error-handling)。

## 401 自动续期

```typescript
import { configureTokenRefresh, TokenRefreshErrorType } from '@radish/http';

configureTokenRefresh({
  refreshEndpoint: `${authServerBaseUrl}/connect/token`,
  getRefreshToken: () => tokenService.getRefreshToken(),
  onTokenRefreshed: (accessToken, refreshToken) => {
    tokenService.setTokenInfoFromJwt(accessToken, refreshToken);
  },
  onRefreshFailed: (errorType) => {
    if (errorType === TokenRefreshErrorType.InvalidRefreshToken) {
      tokenService.clearTokens();
    }
  },
});
```

注意：

- 登录时必须申请 `offline_access` scope 才能拿到 `refresh_token`
- 只有宿主调用 `configureTokenRefresh` 后，带认证 Header 的请求才会在 `401` 时尝试刷新并重试一次；client 当前采用该模式
- Console 当前不配置 `@radish/http` 的 401 重放，而是在 `onRequest` 通过 `tokenService.getValidAccessToken()` 预刷新，并在最终 `401` 时由认证边界清理会话和回到登录页
- 当前 client 的认证恢复会重放原始 `withAuth` 请求，不区分 HTTP 方法；高风险非幂等写入必须具备服务端幂等语义或使用不参与该重放的专用适配器，不能依赖页面层重复重试
- 不要在业务模块里自己包一层平行的 fetch/axios 客户端

## 特殊场景

- 需要上传进度时，可以在统一适配器内使用 `XMLHttpRequest`，不得在页面复制实现
- 必须从 `@radish/http` 的 `getApiClientConfig()` 读取 base URL、timeout、token、语言与翻译器，并保留取消、超时、`MessageArguments`、HTTP status 与 `TraceId`
- 专用 XHR 上传不自动重试；其余请求继续使用统一客户端，不扩散成第二套 HTTP 规范

## 相关文档

- [@radish/http 文档](./http-client.md)
- [鉴权与授权指南](/guide/authentication)
- [API 说明索引](/guide/api-index)
