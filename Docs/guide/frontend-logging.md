# 前端日志与敏感字段脱敏

> 状态：实施后维护
>
> 最后更新：2026-06-19（Asia/Shanghai）

本文说明 `radish.client`、`radish.console` 与 `@radish/http` 的前端日志入口、脱敏规则和后续维护要求。

## 统一入口

| workspace | 入口 |
| --- | --- |
| `radish.client` | `Frontend/radish.client/src/utils/logger.ts` |
| `radish.console` | `Frontend/radish.console/src/utils/logger.ts` |
| `@radish/http` | `Frontend/radish.http/src/logSanitizer.ts` 与 HTTP 客户端内部错误输出 |

业务代码禁止直接使用 `console.log/info/warn/error`。调试、错误和表格输出统一走 `log.debug / info / warn / error / table`，由统一 logger 负责脱敏。

## 脱敏规则

- 统一 logger 会递归脱敏普通对象、数组和 `Error` 对象的可枚举附加字段；循环引用输出为 `[Circular]`。
- 字段名大小写不敏感，并忽略 `_`、`-` 等分隔符。
- 当前覆盖 `paymentPassword`、`paymentPasscode`、`password`、`pwd`、`passcode`、`currentPassword`、`newPassword`、`confirmPassword`、`oldPassword`、`accessToken`、`refreshToken`、`idToken`、`token`、`secret`、`apiKey`、`api_key`。
- 日志中不得输出支付口令、登录密码、token、secret、api key 或完整认证响应。
- 幂等键只允许按“是否存在 / 操作类型”排障，不把它当作安全凭证或审计主键。

## 维护要求

- 新增前端日志工具或调整 `@radish/http` 错误输出时，必须补对应 workspace 的敏感字段脱敏测试。
- 新增高风险请求字段时，应同步扩展脱敏字段列表和测试用例。
- 安全边界见 [密码传输与请求签名临时评审](/guide/password-transport-and-request-signature)。
