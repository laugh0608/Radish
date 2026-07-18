# 日志敏感数据与查询凭据保护

> 最后更新：2026-07-18（Asia/Shanghai）
>
> 本页说明应用日志、网关日志与审计日志中的敏感数据边界；日志输出、存储、查询和归档仍以 [日志系统](/guide/logging) 为准，前端规则见 [前端日志与敏感字段脱敏](/guide/frontend-logging)。

## 基本原则

- 日志只记录诊断所需的最少上下文，不记录访问令牌、刷新令牌、授权头、Cookie、支付凭据、密码或完整敏感请求体。
- 结构化属性优先记录稳定标识、状态码、阶段、耗时和有限错误摘要；不要为了排障解构完整用户、认证响应或第三方载荷。
- 自动脱敏是纵深保护，不能作为主动记录凭据的理由。调用侧、审计中间件与日志 sink 均应保持自己的最小暴露边界。
- 业务正文、私聊内容和附件不是普通诊断字段。只有明确的治理或审计场景才能按对应权限与快照契约记录必要内容。

## 查询参数凭据统一脱敏

所有调用 `AddSerilogSetup()` 的宿主都会注册 `SensitiveQueryStringLogEnricher`。它在日志事件进入控制台、文件或数据库 sink 前遍历字符串标量属性，并对 URL / 查询字符串中的下列参数名做大小写不敏感替换：

- `access_token`
- `refresh_token`
- `id_token`
- `client_secret`
- OAuth `code`

例如 SignalR 握手属性：

```text
/hub/chat?id=connection-1&access_token=secret-token&mode=websocket
```

写入日志时会变为：

```text
/hub/chat?id=connection-1&access_token=[REDACTED]&mode=websocket
```

规则不依赖属性名，因此可以覆盖 `RequestPath`、`RequestUri`、`QueryString`、YARP 转发 URI 等常见结构化属性；非敏感查询参数保持原值，便于诊断路由问题。

## 覆盖边界

当前实现只处理日志事件中的字符串标量属性，并识别以 `?` 或 `&` 开始的标准查询参数。下列内容不会因为该 Enricher 自动变得安全：

- 对象解构、集合或嵌套 JSON 中的凭据；
- Authorization header、Cookie、请求体、响应体和异常对象内部文本；
- URL 编码后藏在自定义字段中的整段载荷；
- 新增但尚未登记的凭据参数名；
- 前端控制台、浏览器网络面板或第三方采集 SDK 自行产生的日志。

因此，审计中间件仍须按请求体敏感字段规则裁剪；业务日志仍禁止记录完整认证对象、握手 URL 或密钥。异常来自第三方库时，应先确认 message 与 data 中是否可能携带凭据，再决定是否原样记录。

## 代码与验证入口

- 全局注册：`Radish.Extension.Log/SerilogSetup.cs`
- 查询参数脱敏：`Radish.Extension.Log/SensitiveQueryStringLogEnricher.cs`
- 后端回归：`Radish.Api.Tests/SensitiveQueryStringLogEnricherTests.cs`
- 前端统一日志：`Frontend/radish.client/src/utils/logger.ts`
- 前端敏感字段规则：[前端日志与敏感字段脱敏](/guide/frontend-logging)

新增凭据参数名或改变日志属性结构时，应同步覆盖：

1. 参数名大小写与多参数组合；
2. `RequestPath / RequestUri / QueryString` 等不同属性名；
3. 非敏感参数保持原值；
4. Gateway、Auth、API 中至少一个真实产生该属性的入口；
5. 不把测试 token、真实账号信息或完整握手 URL 写入固定文档和测试输出。

## 开发检查清单

- 日志模板是否只含必要 ID、阶段、状态和耗时；
- `{@Object}` 是否可能展开认证、用户隐私、正文或附件内容；
- URL 是否含 query credential，且宿主是否通过 `AddSerilogSetup()` 初始化；
- 请求体 / 响应体是否有独立的字段白名单与长度限制；
- 错误复制或用户可见诊断是否排除了 token、成员列表、完整正文和后端堆栈；
- 新增敏感字段后，后端 Enricher、审计裁剪、前端 logger 与测试是否同步。
