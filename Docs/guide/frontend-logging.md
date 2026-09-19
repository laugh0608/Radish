# 前端日志与敏感字段脱敏

> 状态：实施后维护
>
> 最后更新：2026-09-19（Asia/Shanghai）

本文说明 `radish.client`、`radish.console` 与 `@radish/http` 的前端日志入口、脱敏规则和后续维护要求。

三级日志与公共实现收敛见[统一日志专题方案](/features/unified-logging-governance-design)。下文三个浏览器 workspace 的实现尚未替换，不自动上传浏览器日志；Node 容器宿主已先接入候选统一输出，见下节。

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

## Node 静态宿主

`Frontend/scripts/serve-static.mjs` 通过 `logging/runtime-output.mjs` 记录生命周期和请求失败，统一路径由 `RadishLogging__Enabled=true` 显式启用，默认关闭；配置、三级及开发诊断限制见[事件契约](/features/unified-logging-contract)。Node 运行适配与唯一 JSON 策略随 Frontend 镜像复制，浏览器包不使用这个服务器输出入口。

请求拒绝 / 失败调用点不再传路径、IP、转发头或异常原文；健康检查不逐次输出。候选路径序列化单行 JSON，失败进入有限安全应急摘要；旧终端路径只显示固定事件码。此变化不代表浏览器 logger、Native 或全部业务日志已完成重构。
