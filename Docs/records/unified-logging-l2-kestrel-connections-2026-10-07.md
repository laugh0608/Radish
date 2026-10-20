# L2 Kestrel 连接与坏请求来源治理

日期：2026-10-07（Asia/Shanghai）。项目所有者确认[实施方案](./unified-logging-l2-kestrel-connections-plan-2026-10-07.md)后完成适配。承接[外层 HTTP 三来源治理](./unified-logging-l2-outer-http-2026-10-07.md)，生产候选开关仍关闭，L2 未整体完成。

## 变更与事件

扩展现有 `HttpServerFrameworkLogSink`，仅增加 `Microsoft.AspNetCore.Server.Kestrel.Connections` 与 `Microsoft.AspNetCore.Server.Kestrel.BadRequests` 两个精确来源。旧链代码 / 配置 WriteTo / AuditTo 和候选链沿用已建立的接线，在上下文合并后、输出前重建安全事件；未增加前缀匹配或新输出链。

保留输入事件时间、等级、类型化 trace/span、固定来源与受控 failureKind；不复制原模板、异常原文、连接 ID、请求内容或任意 scope，也不修改传入的 LogEvent。事件身份以来源 + EventId + Name 匹配，不能用消息文本推断。

| 来源后缀 | ID / Name | 事件编码 | 框架原等级 |
| --- | --- | --- | --- |
| Connections | 1 / `ConnectionStart` | `http.connection_started` | Debug |
| Connections | 2 / `ConnectionStop` | `http.connection_stopped` | Debug |
| Connections | 4 / `ConnectionPause` | `http.connection_paused` | Debug |
| Connections | 5 / `ConnectionResume` | `http.connection_resumed` | Debug |
| Connections | 9 / `ConnectionKeepAlive` | `http.connection_keep_alive` | Debug |
| Connections | 10 / `ConnectionDisconnect` | `http.connection_disconnected` | Debug |
| Connections | 16 / `NotAllConnectionsClosedGracefully` | `http.connection_close_incomplete` | Debug |
| Connections | 21 / `NotAllConnectionsAborted` | `http.connection_abort_incomplete` | Debug |
| Connections | 24 / `ConnectionRejected` | `http.connection_rejected` | Warning |
| Connections | 34 / `ApplicationAbortedConnection` | `http.connection_application_aborted` | Information |
| Connections | 39 / `ConnectionAccepted` | `http.connection_accepted` | Debug |
| BadRequests | 17 / `ConnectionBadRequest` | `http.bad_request` | Debug |
| BadRequests | 20 / `RequestProcessingError` | `http.request_processing_ended` | Debug |
| BadRequests | 27 / `RequestBodyMinimumDataRateNotSatisfied` | `http.request_rate_insufficient` | Debug |
| BadRequests | 28 / `ResponseMinimumDataRateNotSatisfied` | `http.response_rate_insufficient` | Debug |
| BadRequests | 54 / `PossibleInvalidHttpVersionDetected` | `http.protocol_mismatch` | Debug |
| Connections | 未知 / 不匹配身份 | `http.connection_unclassified` | 保留输入等级 |
| BadRequests | 未知 / 不匹配身份 | `http.bad_request_unclassified` | 保留输入等级 |

共新增 18 个公共事件编码。Warning / Error / Critical 不因 diagnostics 关闭而丢弃。低级别事件仍遵循已有策略：旧链保持原等级，候选 Development 启用 diagnostics 时将 Debug 按公共规范输出为 Info + diagnostic，候选 Production 过滤过程诊断；适配器不改变最低等级或诊断开关。

## 行为与所有权

- HTTP/1 请求行 / 请求头解析失败以及缺少 Host 继续返回框架 400，不进入应用或 Hosting 请求生命周期；正文不增加日志详情，不制造请求 trace。DiagnosticSource 等其他通道不由此适配器接管。
- 实际连接限流以首条连接占用唯一配额、第二条连接被拒绝验证。拒绝继续释放连接而不产生 HTTP 响应，Warning 即使在候选 diagnostics 关闭时也可见；首条请求可正常完成。本批没有改变并发限制或统一成 API 429。
- 连续 keep-alive 请求保持两次应用调用和两次 Hosting 释放，最后释放一次 transport。输入 Pipe 异常结束保持无响应与 Debug 阶段诊断；应用主动终止连接保持 Information 诊断。
- 连接委托抛错及连接完成回调失败实际属于 Kestrel 主类别 `0 / Name=null / Error`，继续由已有 `http.server_unclassified` 适配。同步 / 异步完成回调失败后继续其他回调，再释放 transport；同一连接的不同故障阶段分别保留，不将 ID 0 改为特定业务事件。
- BadRequests 的 ConnectionBadRequest 生成方法仍在 ILogger 前读取异常 Message；本批裁剪输出，不声称阻止框架此前读取。不可读取异常与属性在 sink 测试边界验证；真实解析回归使用框架实际异常。

## 验证

使用本机 .NET SDK 10.0.300 / ASP.NET Core 10.0.8 和现有依赖。真实协议回归仅启动 Kestrel 测试引擎，唯一 transport 是内存 Channel / Pipe；未注册 socket transport，Loopback endpoint 仅交给内存 listener 保存，不执行系统 bind。没有启动 API / Auth / Gateway 项目宿主、服务容器或访问生产。

- 新增 24 个四模式测试用例：连续 keep-alive、三类解析拒绝、输入异常、真实并发拒绝、连接委托及同步 / 异步完成回调故障、部分诊断生成方法。
- 扩展已有应用主动取消测试，确认 Connections 的 Information 事件；扩展旧 / 候选输出安全检查至本批两来源。
- 新增 20 个 sink 身份 / 安全测试和 16 个运行时元数据测试；覆盖 16 个已知身份、错误 / 缺失 Name、未知编号、不可读取异常与属性、原事件不变、时间 / 等级 / trace。扩展未知事件四模式、非目标透传和配置 AuditTo 回归；真实协议用例同时经过配置 WriteTo。
- 内存 transport 修复测试生命周期竞态：独立管理 ConnectionClosed 取消任务，等待取消完成再释放 CTS，并复用同一释放任务；避免 DefaultConnectionContext 默认排队取消与测试提前释放相撞。这是测试实现修正，未修改产品 transport 或协议行为。
- 定向 .NET 回归启用 `TreatWarningsAsErrors=true`、`--no-restore`：**379 / 379 通过**，包括新增 60 个用例以及既有 HTTP / SignalR / Runtime policy、API 错误契约与消费边界回归。
- 公共 Node 日志契约 **27 / 27 通过**；Docs、changed-only 文本卫生与 `git diff --check` 通过。日志指南 / 记录索引已有篇幅软提醒，未达到硬上限。
- 沙盒复跑时 MSBuild 本地通信管道被拒绝，获准在沙盒外完成验证；取消后未退出的本次进程已精确清理。一次旧测试 transport 的异步取消竞态曾导致宿主崩溃，该次部分通过不计成功；修复后以上完整 379 项通过。

定向命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -p:TreatWarningsAsErrors=true --filter 'FullyQualifiedName~KestrelConnectionLoggingTests|FullyQualifiedName~OuterHttp|FullyQualifiedName~KestrelHttp|FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~SignalRFramework|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQuery'
```

## 证据边界与后续

连接暂停 / 恢复、关闭未完成 / 中止未完成、最低请求 / 响应速率和版本不匹配事件具有真实生成方法、映射与元数据证据；不声称已执行真实流控、超时或 HTTP/2 握手分支。内存协议回归不代表真实 TCP、TLS、HTTP/2 / HTTP/3 或生产验收。

后续继续核对 Kestrel HTTP/2 / HTTP/3 / TLS 来源，再确认适配范围；Http.Connections、协议 resolver、其他 Hosting / Lifetime 来源及业务任意 scope 全局治理保留后续。已列明早期消费点与独立可靠性问题仍按当前规划推进，不因本批日志收口改变响应、事务或重试规则。
