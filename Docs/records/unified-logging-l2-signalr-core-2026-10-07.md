# L2 SignalR Core 框架日志适配

日期：2026-10-07（Asia/Shanghai）。本批承接[框架边界核对与已确认方案](./unified-logging-l2-framework-boundary-audit-2026-10-07.md)，治理三个明确的 SignalR Core 来源，不改变 Hub 或协议处理。

## 实现与所有权

- `SignalRFrameworkLogSink` 精确匹配 `Microsoft.AspNetCore.SignalR.HubConnectionHandler`、`Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher`、`Microsoft.AspNetCore.SignalR.HubConnectionContext`。实际 ILogger 类别不包含泛型 Hub 名；相似前缀与其他来源保持原行为。
- 在上下文合并之后重建安全 LogEvent，不渲染原模板、不读取异常 Message / ToString、不复制任意属性。保留原时间、等级、类型化 trace/span、固定 SourceContext，以及受控 EventCode、SourceCategory、Diagnostic 和存在异常时的 failureKind。
- 已知映射同时核对来源、EventId 和 Name；生命周期事件还限定 `OnConnectedAsync / OnDisconnectedAsync`。未知事件输出 `signalr.unclassified`；Warning / Error 不因映射遗漏被吞掉，低级别事件继续服从模式 / diagnostics / 最低等级。
- 原始事件不被修改或再次输出；Hub 自有断开事件仍按原时机记录。客户端带错误 Close 可以只通知 Hub 回调，不能以删除自有日志来实现连接级去重。
- 不改变授权、业务调用、分组 / presence 清理、Completion / Close 内容、详细错误开关、重连和原异常传播。协议中的既有错误文案不属于本批日志正文治理范围。

## 事件映射

| 来源 | 框架 EventId / Name | 受控事件 | 框架等级 |
| --- | --- | --- | --- |
| handler | 1 / ErrorDispatchingHubEvent，OnConnectedAsync | `signalr.connect_failed` | Error |
| handler | 1 / ErrorDispatchingHubEvent，OnDisconnectedAsync | `signalr.disconnect_failed` | Error |
| handler | 2 / ErrorProcessingRequest | `signalr.message_failed` | Debug |
| handler | 4 / ErrorSendingClose | `signalr.close_failed` | Debug |
| dispatcher | 8 / FailedInvokingHubMethod | `signalr.invocation_failed` | Error |
| dispatcher | 25 / FailedStreaming | `signalr.stream_failed` | Error |
| dispatcher | 3 / UnknownHubMethod、22 / InvalidHubParameters | `signalr.invocation_rejected` | Debug |
| connection context | 5 / HandshakeFailed | `signalr.handshake_failed` | Debug |
| connection context | 6 / FailedWritingMessage | `signalr.write_failed` | Error |

等级依据本机 ASP.NET Core 10.0.8 的 LoggerMessage 元数据，回归固定对应关系；未统一将框架 Debug 升为 Error。

## 双链接线

- 候选链在 `RuntimeSerilogSink` 之前接入；生产 `RadishLogging.Enabled=false` 保持，未切换部署链路。
- 旧链经 `ConfigureLegacy` 统一接入代码配置的 Console / File / Database 及标准 `Serilog:WriteTo`。根等级、来源 override、enrichment / filter 与输出配置通过实时视图分开装配，避免配置 sink 直接绕过适配。
- `AuditTo` 使用单独的安全包装，继续向调用方传播审计 sink 失败；Serilog 聚合异常的内部包装层次可能增加，内部原始故障仍保留。普通 WriteTo 的 sink 失败处理仍由 Serilog 负责。
- 下游固定 SourceContext 保持原值，非目标来源的 LogSource 等属性不变；不将 SQL 事件误当作 SignalR，也不改变原 SQL / 应用筛选条件。配置的输出插件仍负责自身行为，本批不将外部插件或重新添加原始数据的自定义子 logger 视为已审计代码。

## 验证与限制

- 四模式真实 `HubConnectionHandler`、dispatcher 与 JSON 协议通过内存双向 Pipe 验证：正常返回、普通异常 / HubException、未知方法、绑定失败、连接初始化失败、客户端带错误 Close、断开清理失败、无效消息、握手失败和输出写入失败。
- 检查客户端原结果、清理顺序、原异常实例、各阶段事件数量与敏感哨兵。写入失败后仍尝试发送 Close，两个独立写入故障分别记录；不按同一连接压成一条。
- `ErrorSendingClose` 与 `FailedStreaming` 本批只有映射单元及当前运行时元数据证据，不宣称已触发真实框架分支。没有运行真实 WebSocket、认证中间件、端口监听或服务器。
- 配置接线回归覆盖多个配置 sink、根属性清理、来源 override 的 reload、非目标事件与 SQL 属性保留、资源释放及审计 sink 失败传播；不替代旧日志库真实入库或文件轮转验收。
- `DefaultHubProtocolResolver` 的协议注册 / 选择日志不在这三个来源内，测试明确区分；`Http.Connections`、Kestrel、其他框架来源及 HTTP 未处理路径继续待治理。不能把本批称为整个 SignalR / 连接链路收口。

## 验证结果

- 新框架 / 适配测试与三个 Hub、公共日志策略相关回归 **152 / 152** 通过，构建使用 `TreatWarningsAsErrors=true`；其中本批新增 56 项。
- 生成入口及启动失败所有权既有回归另行 **18 / 18** 通过；两组均无跳过。
- Node 共用日志契约 **27 / 27** 通过；`check:docs`、changed-only 文本卫生及 `git diff --check` 通过。
- 文本检查仅提示既有日志手册、记录索引超过软篇幅建议，均未超过硬上限。本批未安装依赖、启动项目服务 / 容器、访问生产、推送或发布。
- 沙盒内 MSBuild / 测试运行器的本地通信受限，获准在沙盒外完成定向测试；不是新增项目运行态验收。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~SignalRFramework|FullyQualifiedName~ChatHubLoggingTests|FullyQualifiedName~NotificationHubLoggingTests|FullyQualifiedName~CommentHubLoggingTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQueryStringLogEnricherTests' --verbosity quiet -p:TreatWarningsAsErrors=true
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ProducerLoggingTests --verbosity quiet
npm run check:logging-contract
npm run check:docs
npm run check:repo-hygiene:changed
git diff --check
```
