# 日志敏感数据与查询凭据保护

> 最后更新：2026-10-11（Asia/Shanghai）
>
> 本页说明应用日志、网关日志与审计日志中的敏感数据边界；日志输出、存储、查询和归档仍以 [日志系统](/guide/logging) 为准，前端规则见 [前端日志与敏感字段脱敏](/guide/frontend-logging)。

## 基本原则

全项目重构见[统一日志专题方案](/features/unified-logging-governance-design)。`RadishLogging.Enabled=true` 的候选入口采用白名单事件重建，规则见[事件契约](/features/unified-logging-contract)；已治理的自有生成端同时作用于旧 / 候选链；已列明框架来源另在输出前安全重建，不能把旧链视为仅有查询参数替换。浏览器、审计与裸输出旁路尚未因候选入口自动完成治理。

- 日志只记录诊断所需的最少上下文，不记录访问令牌、刷新令牌、授权头、Cookie、支付凭据、密码或完整敏感请求体。
- 普通运行日志仅使用已登记事件、受控状态码 / 阶段 / 耗时与固定 failureKind，不因标识稳定就自动放行用户、连接或附件身份；不要为了排障解构完整用户、认证响应或第三方载荷。
- 自动脱敏是纵深保护，不能作为主动记录凭据的理由。调用侧、审计中间件与日志 sink 均应保持自己的最小暴露边界。
- 业务正文、私聊内容和附件不是普通诊断字段。只有明确的治理或审计场景才能按对应权限与快照契约记录必要内容。

## 查询参数凭据统一脱敏

API / Auth / Gateway 在 `RadishLogging.Enabled=false` 的旧 `AddSerilogSetup` 路径注册 `SensitiveQueryStringLogEnricher`；候选路径与旧 sinks 互斥，直接按统一契约省略任意消息、异常正文与未登记字段，不依赖此 Enricher。旧路径中，该 Enricher 在日志事件进入控制台、文件或数据库 sink 前遍历字符串标量属性，并对 URL / 查询字符串中的下列参数名做大小写不敏感替换：

- `access_token`
- `refresh_token`
- `id_token`
- `client_secret`
- OAuth `code`

例如单独经过查询参数 Enricher 的字符串属性：

```text
/hub/chat?id=connection-1&access_token=secret-token&mode=websocket
```

写入日志时会变为：

```text
/hub/chat?id=connection-1&access_token=[REDACTED]&mode=websocket
```

上述示例只说明 Enricher 的替换行为，不代表已治理 SignalR / HTTP 来源的最终输出；后者会省略整个请求路径等属性。规则不依赖属性名，因此可以覆盖 `RequestPath`、`RequestUri`、`QueryString`、YARP 转发 URI 等常见结构化属性；非敏感查询参数保持原值，便于诊断路由问题。

## 覆盖边界

该查询参数 Enricher 只处理日志事件中的字符串标量属性，并识别以 `?` 或 `&` 开始的标准查询参数。下列内容不会因为该 Enricher 自动变得安全：

- 对象解构、集合或嵌套 JSON 中的凭据；
- Authorization header、Cookie、请求体、响应体和异常对象内部文本；
- URL 编码后藏在自定义字段中的整段载荷；
- 新增但尚未登记的凭据参数名；
- 前端控制台、浏览器网络面板或第三方采集 SDK 自行产生的日志。

因此，审计中间件仍须按请求体敏感字段规则裁剪；业务日志仍禁止记录完整认证对象、握手 URL 或密钥。普通运行日志不得原样记录第三方异常的 Message、StackTrace 或 Data，应在最终消费边界生成登记事件与受控 failureKind；完整规则见[开发规范](../architecture/specifications.md#aop-与日志)。

## 已治理框架来源的输出边界

已治理来源以[事件契约第 51–66 节](../features/unified-logging-contract.md)逐项列明的精确类别为准（第 57 节为业务消费边界）：SignalR Core、HTTP 异常 / Hosting / Kestrel、证书监视与配置、服务端 Socket / QUIC、Http.Connections / 协议解析、YARP / 命名 HttpClient、认证授权、路由、宿主健康服务及 OpenIddict Server / Core。已知事件按来源、EventId / Name 识别，未知目标事件输出安全摘要，不通过原消息推断。

- 在 enrichment / LogContext 合并后重建事件，覆盖旧链代码 / 配置 WriteTo / AuditTo 与候选输出；保留时间、等级、类型化 trace/span、固定来源和受控 failureKind，移除原模板、异常对象与任意 scope。
- 宿主健康服务的一个来源和 OpenIddict Server / Core 的五个来源另有 ILogger → Serilog 桥接前保护：不枚举原 state、不调用原 formatter、不传入原 exception，避免消息绑定提前读取敏感对象。其他适配器不能据此宣称同样具备桥接前保护；最终 sink 仍负责移除合并后的任意 scope。
- 配置输出由 `ConfigureLegacy` 和实时 `SerilogConfigurationView` 分开装配，避免标准配置 sink 绕过适配；AuditTo 原有失败传播语义保留。自定义插件自行增添数据不由此保证。
- 非目标类别继续透传给后续输出。自有 Auth 日志、任意业务 scope、相邻框架类别与其他 OpenIddict 集成不因本批自动完成治理；候选公共策略仍会省略未登记载荷。
- 日志适配不改变开发异常页正文、HTTP / SignalR / OIDC 响应、认证规则、健康结果、manager 存储 / 缓存行为或证书验证。调用 ILogger 前读取属性的行为、BeginScope 自身的行为以及 IdentityModel 的独立日志接口 / EventSource、DiagnosticListener、Activity / metrics、Serilog SelfLog、裸输出等通道，不由本批整体保证。

## 代码与验证入口

- 全局注册：`Radish.Extension.Log/SerilogSetup.cs`
- 查询参数脱敏：`Radish.Extension.Log/SensitiveQueryStringLogEnricher.cs`
- 查询参数回归：`Radish.Api.Tests/SensitiveQueryStringLogEnricherTests.cs`
- 框架安全输出回归：`SignalRFrameworkSinkTests`、`HttpExceptionFrameworkSinkTests`、`OuterHttpFrameworkSinkTests`，早期真实路径见[10 月 7 日日终回顾](../records/day-end-doc-review-2026-10-07.md)；后续来源、健康 / OpenIddict 桥接保护与验证限制见[10 月 11 日日终回顾](../records/day-end-doc-review-2026-10-11.md)
- 前端统一日志：`Frontend/radish.client/src/utils/logger.ts`
- 前端敏感字段规则：[前端日志与敏感字段脱敏](/guide/frontend-logging)

新增凭据参数名或改变日志属性结构时，应同步覆盖：

1. 参数名大小写与多参数组合；
2. `RequestPath / RequestUri / QueryString` 等不同属性名；
3. 非敏感参数保持原值；
4. Gateway、Auth、API 中至少一个真实产生该属性的入口；
5. 不把测试 token、真实账号信息或完整握手 URL 写入固定文档和测试输出。

## 开发检查清单

- 日志模板和字段是否符合登记事件及类型约束，是否误放行用户 / 连接 / 附件身份；
- `{@Object}` 是否可能展开认证、用户隐私、正文或附件内容；
- URL 是否含 query credential，且宿主是否通过 `AddSerilogSetup()` 初始化；
- 请求体 / 响应体是否有独立的字段白名单与长度限制；
- 错误复制或用户可见诊断是否排除了 token、成员列表、完整正文和后端堆栈；
- 新增敏感字段后，后端 Enricher、审计裁剪、前端 logger 与测试是否同步。
