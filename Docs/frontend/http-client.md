# `@radish/http` HTTP 客户端指南

> 当前版本：`26.7.1`
>
> 位置：`Frontend/radish.http/`

`@radish/http` 是 client、Console 和共享业务模块唯一正式 HTTP 客户端。它负责请求配置、认证、超时、响应解析、结构化错误、语言头与 Token 刷新；不依赖 React、i18next 实例或任一宿主 UI。

共享包当前仍为旧调用保留默认读取 `localStorage.access_token` 的兼容 `getToken`；正式宿主必须在初始化时注入自己的 `tokenService`，新增代码不得把该默认值当成认证存储契约。

## 1. 模块边界

- 正式调用直接从 `@radish/http` 导入。
- `@radish/ui` 只为旧调用保留兼容 re-export，新代码不得继续从 UI 包导入 HTTP API。
- 页面和业务模块不得再创建平行 fetch / axios 封装。
- 上传进度等必须使用 `XMLHttpRequest` 的场景，也要通过 `getApiClientConfig()` 读取统一 base URL、timeout、token、语言和消息翻译配置，并复用结构化响应解析。Console 附件适配器已遵循该边界；client 附件适配器当前仍单独从 `getApiBaseUrl()` 拼 URL，后续应在附件维护批次收敛，不能据此扩散第二套配置来源。
- 宿主环境变量通过各自 `env.ts` 读取，不在共享包中直接访问 `import.meta.env`。

## 2. 宿主初始化

client 和 Console 在应用入口各配置一次：

```ts
import { configureApiClient } from '@radish/http';
import i18n from './i18n';

configureApiClient({
  baseUrl: apiBaseUrl,
  timeout: 30_000,
  getToken: () => tokenService.getAccessToken(),
  getLanguage: () => i18n.resolvedLanguage ?? i18n.language,
  translateMessage: (key, args) => (
    i18n.exists(key)
      ? i18n.t(key, Object.fromEntries((args ?? []).map((value, index) => [index, value])))
      : undefined
  ),
});
```

配置项：

| 字段 | 作用 |
| --- | --- |
| `baseUrl` | 相对 API 路径的统一前缀 |
| `timeout` | 请求超时毫秒数，默认 `30000` |
| `getToken` | 在 `withAuth=true` 时提供 Bearer Token |
| `getLanguage` | 提供当前请求语言；客户端在未显式覆盖时写入 `Accept-Language` |
| `translateMessage` | 根据服务端 `MessageKey` 与可选位置参数获取宿主本地翻译 |
| `onRequest / onResponse / onError` | 宿主级观测和错误接线；不能在此改写业务成功语义 |

调用方显式传入的 `Accept`、`Accept-Language`、`Content-Type` 等 Header 优先，统一客户端不会覆盖。

## 3. 请求方法

```ts
import {
  apiDelete,
  apiGet,
  apiPost,
  apiPut,
  type PagedResponse,
} from '@radish/http';

const products = await apiGet<PagedResponse<Product>>(
  '/api/v1/Shop/GetProducts?pageIndex=1&pageSize=20',
);

const created = await apiPost<Product>(
  '/api/v1/Shop/CreateProduct',
  payload,
  { withAuth: true },
);

const updated = await apiPut<Product>(
  '/api/v1/Shop/UpdateProduct',
  payload,
  { withAuth: true },
);

const removed = await apiDelete<boolean>(
  `/api/v1/Shop/DeleteProduct/${encodeURIComponent(productId)}`,
  { withAuth: true },
);
```

`apiPost / apiPut` 默认补 `Content-Type: application/json` 并序列化普通对象 payload。文件上传等非 JSON 请求使用 `apiFetch` 并显式提供 body 与 Header。

## 4. 响应契约

后端 `MessageModel<T>` 对应：

```ts
interface ApiResponse<T> {
  isSuccess: boolean;
  statusCode: number;
  messageInfo: string;
  messageInfoDev?: string;
  responseData?: T;
  code?: string;
  messageKey?: string;
  messageArguments?: unknown[];
  traceId?: string;
}
```

统一方法返回：

```ts
interface ParsedApiResponse<T> {
  ok: boolean;
  data?: T;
  message?: string;
  messageInfo?: string;
  messageKey?: string;
  messageArguments?: unknown[];
  code?: string;
  statusCode?: number;
  httpStatus?: number;
  traceId?: string;
}
```

- `statusCode` 是响应体兼容状态，`httpStatus` 是真实 HTTP 状态；控制流优先使用真实 HTTP 状态和稳定 `Code`。
- `message` 是用户展示候选：失败响应且宿主存在 `MessageKey` 翻译时使用本地文案，否则保留安全 `MessageInfo`；成功响应不会由统一客户端再次本地化。
- `messageInfo` 保留服务端原始安全消息，用于诊断和缺键回退。
- `messageArguments` 是服务端规范化后的可选短位置参数，只传给 `translateMessage(key, args?)` 做展示格式化，不参与控制流。
- `traceId` 来自响应体或 `X-Correlation-ID`，用于显式诊断入口，不默认拼进主提示。
- `204` 解析为成功；非 JSON、无效 JSON 和非 `MessageModel` 响应保留真实 HTTP 状态并返回失败说明。

## 5. 结构化错误

业务 helper 需要抛错时使用 `ApiResponseError`，保留 `Code / MessageKey / MessageArguments / MessageInfo / status / TraceId`：

```ts
import {
  createApiResponseError,
  isApiResponseNotFoundError,
} from '@radish/http';

const response = await apiGet<DocumentVo>(path);
if (!response.ok || !response.data) {
  throw createApiResponseError(response, '无法读取文档');
}

return response.data;
```

页面可以在边界处识别稳定状态：

```ts
try {
  const document = await getPublicDocument(slug);
  setDocument(document);
} catch (error) {
  if (isApiResponseNotFoundError(error)) {
    setNotFound(true);
    return;
  }

  throw error;
}
```

禁止：

- 通过“商品不存在”“not found”等中英文字符串或正则决定业务分支；
- 抛出普通 `Error(response.message)` 后丢失 `Code / status / MessageKey / MessageArguments / TraceId`；
- 把 `MessageInfo` 当作稳定协议字段；
- 未处理异常时向 UI 暴露堆栈、SQL、路径或凭据。

## 6. 用户提示与诊断

请求方法只负责生成结构化结果，不直接决定 Toast、页面错误槽或弹窗。宿主根据交互层级展示：

- 行内校验：贴近字段，保留用户输入。
- 页面加载失败：使用页面状态槽，并提供重试。
- 操作失败：保留当前表单、选中对象和上下文。
- 可恢复异常：按[可恢复错误诊断规范](/frontend/recoverable-error-diagnostics)提供复制诊断入口。
- 日志：client / Console 使用各自 `log` 工具，不新增 `console.log/info/warn/error`。

## 7. 401 与 Token 刷新

```ts
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

- 只有带认证 Header 的请求在符合刷新条件时自动尝试刷新。
- 刷新成功后使用新 Token 重试原请求。
- 确认 refresh token 失效时触发会话过期处理；网络或服务端临时失败保留原响应交给调用方。
- 业务模块不得各自实现刷新锁或重复请求队列。

上述行为只在宿主调用 `configureTokenRefresh` 后生效。client 当前采用该模式；Console 当前在 `onRequest` 通过 `tokenService.getValidAccessToken()` 预刷新，并在最终 `401` 时清理会话和跳转登录，不启用本模块的 401 重放。

当前重试会原样重放任意收到认证 `401` 的 `withAuth` 请求，尚未按 HTTP 方法区分。业务页面不得再叠加自动重试；关键非幂等写入必须由服务端幂等 / 去重契约保护，或使用不参与该重放的专用传输适配器。

## 8. 原始请求与解析 helper

- `apiFetch`：需要自定义 body、Header 或读取原始 `Response` 时使用。
- `parseHttpResponse`：把真实 HTTP Response 解析为 `ParsedApiResponse<T>`。
- `parseApiResponse`：只解析已经反序列化的 `MessageModel<T>`。
- `parseApiResponseWithI18n`：兼容显式 translator 的旧 helper；正式统一方法已经使用宿主 `translateMessage`。
- `getApiClientConfig`：供上传等特殊传输只读获取 base URL、timeout、token、语言与翻译器，不用于页面修改全局状态；特殊传输仍需保留取消、超时和结构化错误契约。

## 9. 验证

修改 `@radish/http` 后至少执行：

```bash
npm run type-check --workspace=@radish/http
npm run test --workspace=@radish/http
```

涉及 client / Console 配置或公共调用方式时，再执行对应宿主 type-check、测试和 production build。运行态 smoke 只在成组功能准备验收并取得当轮启动授权后执行。

## 相关文档

- [前端 API 客户端兼容入口](/frontend/api-client)
- [错误处理指南](/frontend/error-handling)
- [国际化与多语言规范](/architecture/i18n)
- [可恢复错误诊断规范](/frontend/recoverable-error-diagnostics)
