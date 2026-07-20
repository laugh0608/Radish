# 前端错误处理指南

> 本页描述 client、Console、`@radish/http` 与共享 UI 的当前错误处理职责。

错误处理的目标不是把所有异常统一弹成一句 Toast，而是保留稳定控制流、用户操作上下文和可追踪诊断信息。

## 1. 错误分层

| 类型 | 识别依据 | 处理位置 |
| --- | --- | --- |
| 表单校验 | 本地规则或服务端 validation error | 字段 / 表单附近 |
| 业务失败 | HTTP status + `Code / MessageKey / MessageArguments / MessageInfo` | 当前业务页面或操作弹层 |
| 认证失效 | `401`、Token 刷新结果、会话事件 | 宿主认证边界 |
| 无权限 | `403` 或稳定权限 `Code` | 页面状态槽或禁用动作说明 |
| not-found / gone | `404 / 410` 或稳定结构化错误 | 页面 not-found 状态 |
| conflict | `409` 或稳定业务 `Code` | 保留输入，提示刷新 / 重试 / 调整请求 |
| 网络 / 超时 | fetch reject、`AbortError` | 可重试页面状态或操作反馈 |
| 未处理异常 | Error Boundary / 全局异常边界 | 安全通用错误 + 诊断入口 |

禁止根据中英文消息文本判断错误类型。

## 2. API 调用模式

不抛错的页面调用：

```ts
const response = await apiGet<UserVo>(path, { withAuth: true });
if (!response.ok || !response.data) {
  setLoadError(response.message ?? t('error.common.loadFailed'));
  return;
}

setUser(response.data);
```

需要跨 helper 抛错时：

```ts
import { createApiResponseError } from '@radish/http';

const response = await apiGet<UserVo>(path, { withAuth: true });
if (!response.ok || !response.data) {
  throw createApiResponseError(response, t('error.user.loadFailed'));
}

return response.data;
```

`ApiResponseError` 保留：

- `code`
- `messageKey`
- `messageArguments`
- `messageInfo`
- `statusCode`
- `httpStatus`
- `traceId`

不要把它提前转换成普通 `Error`。

## 3. 控制流与用户文案

控制流只读取：

1. 真实 `httpStatus`；
2. 稳定业务 `Code`；
3. 明确数据状态；
4. 兼容期才读取响应体 `statusCode`。

用户文案按以下顺序选择：

```text
失败响应存在前端 MessageKey 翻译（使用可选 MessageArguments 格式化）
  -> 服务端安全 MessageInfo
  -> 当前操作的本地通用 fallback
```

`MessageInfo` 可以随语言和文案调整，不能成为 switch、正则或重试策略的输入。

`MessageArguments` 只承载服务端规范化后的短位置参数，宿主可用于词元插值，但不得据此决定权限、重试、not-found 或其他业务流程。

## 4. 页面反馈方式

- 首次加载失败：使用页面状态槽，保留标题、返回路径和重试动作。
- 列表局部刷新失败：保留旧数据并明确刷新失败，不清空成“无数据”。
- 表单提交失败：保留输入、焦点和选择项；字段错误贴近字段，整体错误放在动作区附近。
- 删除、撤销、购买、权益切换等高风险动作：错误留在确认上下文中，不以静默刷新掩盖失败。
- 后台表格错误：保留筛选、分页和来源返回，不把管理人员送回首页。
- Toast 只用于短暂、无需持续阅读的反馈；长说明、TraceId 和恢复步骤使用页面状态或弹层。

## 5. not-found 与 conflict

```ts
import { isApiResponseNotFoundError } from '@radish/http';

try {
  const data = await loadResource();
  setData(data);
} catch (error) {
  if (isApiResponseNotFoundError(error)) {
    setNotFound(true);
    return;
  }

  setLoadError(toUserMessage(error));
}
```

对于 `409 Conflict`，页面应根据业务 `Code` 区分版本冲突、幂等键冲突、状态已变化等情况。默认行为是保留用户输入并提供明确恢复动作，不自动覆盖服务端新状态。

## 6. 认证与权限

- Token 有效性与续期由宿主认证边界统一治理，不由页面自己竞争刷新；client 配置 `@radish/http` 的 401 刷新，Console 当前在请求前通过 `tokenService` 续期。
- refresh token 确认失效后由宿主认证边界清理会话并保存安全回流上下文。
- `401` 不等于所有请求都立即跳登录；先遵循统一刷新结果。
- `403` 显示权限边界，不伪装成资源不存在，除非服务端安全策略明确如此定义。
- Console 按权限控制导航和按钮只是体验优化，服务端授权仍是安全边界。

## 7. 网络、超时与非 JSON

- fetch reject 和超时属于传输错误，不生成伪造业务 `Code`。
- 非 JSON 或无效 JSON 响应由 `parseHttpResponse` 保留真实 HTTP 状态并生成安全说明。
- 业务层重试必须由用户动作或明确的幂等策略驱动，页面不得自行重放写请求。
- 当前 client 配置 Token 刷新后，`@radish/http` 会对收到认证 `401` 的原始 `withAuth` 请求重放一次，且尚未按 HTTP 方法区分；关键非幂等写入必须具备服务端幂等键 / 去重契约或改用不参与该重放的专用适配器，这一认证恢复边界需要在相关业务批次持续审计。
- 上传、流式响应等特殊传输仍复用统一 base URL、认证和语言配置。

## 8. TraceId 与日志

- `TraceId` 用于复制诊断和服务端日志关联，不默认放进主错误标题。
- client 使用 `Frontend/radish.client/src/utils/logger.ts`，Console 使用 `Frontend/radish.console/src/utils/logger.ts`。
- 页面代码不得新增 `console.log/info/warn/error`。
- 日志不记录 access token、refresh token、支付口令、完整敏感请求体或未脱敏凭据。
- 用户可恢复错误的复制内容、显示层级和降级方式见[可恢复错误诊断规范](/frontend/recoverable-error-diagnostics)。

## 9. Error Boundary

Error Boundary 只处理渲染阶段未捕获异常，不能替代业务错误状态：

- 边界展示安全通用说明和恢复 / 返回动作。
- 开发诊断通过统一日志记录，生产 UI 不暴露堆栈。
- 可预期的加载失败、权限、not-found、conflict 应在页面状态模型内处理，不主动抛给 Error Boundary。

## 10. 共享组件边界

- `@radish/ui` 不读取宿主 i18n 或认证状态。
- 共享错误组件通过 `labels`、message 或 render props 接收用户文案。
- `@radish/http` 的 `configureErrorHandling / handleError / handleApiError / handleNetworkError / handleHttpError / withErrorHandling` 只提供底层回调接线；它们不替页面决定 Toast、路由或业务恢复策略。
- 新代码优先直接消费 `ParsedApiResponse` 或 `ApiResponseError`，避免把结构化错误压扁成只有字符串的全局回调。

## 11. 验证清单

- 同一 `Code / status` 在中英文消息不同的情况下走相同控制流。
- not-found、权限、conflict 不匹配消息文本。
- 表单和高风险操作失败后上下文仍保留。
- 非 JSON、网络错误、超时和 Token 刷新失败都有稳定状态。
- `MessageArguments` 能按位置格式化，异常对象、控制字符或超长参数会先被安全降级、规范化或截断，不以原始值进入 UI，也不参与控制流。
- TraceId 可复制但不泄漏内部异常。
- PC / mobile 下长英文错误和按钮组不溢出。

## 相关文档

- [`@radish/http` HTTP 客户端指南](/frontend/http-client)
- [国际化与多语言规范](/architecture/i18n)
- [可恢复错误诊断规范](/frontend/recoverable-error-diagnostics)
- [前端日志规范](/guide/frontend-logging)
