# L2 transport 与协议解析日志（2026-10-11）

## 授权与范围

项目所有者先要求提交早期消费点批次（`b265d8b5`），再只读核对剩余框架来源；确认方案后，批准实施 9 个来源、99 个公共编码、四模式回归和文档。约束为不安装依赖、不启动项目服务、不绑定网络端口，保持日志等级、响应与异常传播。

本批扩展既有 `HttpServerFrameworkLogSink` 和 `SignalRFrameworkLogSink`，不新增包装层、业务 catch、重试、全请求去重或运行开关。准确现行契约见[第 58 节](../features/unified-logging-contract.md#58-l2-socket--quic-transporthttpconnections-与协议解析)。

| 精确来源 | 已知事件 | 未知摘要 | 适配器 |
| --- | ---: | ---: | --- |
| Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets | 7 | 1 | HttpServer |
| Microsoft.AspNetCore.Server.Kestrel.Transport.Quic | 24 | 1 | HttpServer |
| Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionDispatcher | 18 | 1 | SignalR |
| Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionManager | 9 | 1 | SignalR |
| Microsoft.AspNetCore.Http.Connections.Internal.HttpConnectionContext | 9 | 1 | SignalR |
| Microsoft.AspNetCore.Http.Connections.Internal.Transports.WebSocketsTransport | 15 | 1 | SignalR |
| Microsoft.AspNetCore.Http.Connections.Internal.Transports.LongPollingTransport | 5 | 1 | SignalR |
| Microsoft.AspNetCore.Http.Connections.Internal.Transports.ServerSentEventsTransport | 1 | 1 | SignalR |
| Microsoft.AspNetCore.SignalR.Internal.DefaultHubProtocolResolver | 2 | 1 | SignalR |
| 合计 | 90 | 9 | 99 个公共编码 |

## 来源身份与输出边界

- 使用来源 + EventId + EventName 精确匹配，不用前缀、方法名或消息文本推断。dispatcher ID 12 的 Name 为框架原拼写 `TerminatingConection`；manager 不填补已移除 ID 6 / 8；WebSockets 不填补 ID 13。三个 HTTP transport 的实现类型含 `Server`，日志类别不含 `Server`。
- Socket `Start` / 关闭 token 回调的直接 Error，以及 QUIC 关闭 Warning / 启动与关闭回调 Error 使用 `0 / Name=null`，均归属各来源的未知安全摘要，原 Warning / Error 保持。安全摘要不细分无法由身份区分的模板。
- 适配发生在根上下文合并后，覆盖旧链代码 / 配置 WriteTo / AuditTo 及候选输出。只保留时间、原等级、类型化 trace/span、固定来源、Diagnostic、事件码及受控 failureKind；不带连接 ID、用户名、协议名 / 实现类型、endpoint、原文、异常对象或 scope。
- 原 Trace / Debug / Information 仍为诊断，不提升严重度；候选输出沿用 Info 归一及 Development diagnostics 门禁。Warning / Error / Critical 不依赖诊断开关。用户配置的最低等级、reload、sink fanout / disposal 与 AuditTo 抛错语义沿用既有链路。
- 未匹配来源传递同一 LogEvent 实例，包括服务端 Socket 来源的 `.Client` 邻居。`System.Net`、EventSource、Metrics 不被误称为本批 Serilog 治理范围。

## 行为与四模式证据

测试使用旧 / 候选 × Development / Production，逐项检查以下边界：

1. 对已安装 ASP.NET Core 10.0.8 的全部 90 个 LoggerMessage 方法读取元数据，并调用真实日志生成器；同时校验每个类型的完整生成器清单，框架升级增删事件或变更身份时测试失败。两个诊断开关值、配置 WriteTo / AuditTo、未知事件与直接零事件、错误身份、不可读异常 / payload、时间与 trace/span 均有断言。
2. 真实 HttpConnectionDispatcher 协商保留成功 JSON、版本上限 1、连接 token 与注册关系；非整数 / 低版本返回带 error 的 JSON，状态仍为 200，不创建连接。缺失连接 404；不支持的 transport 404、transport 变更 400、活动 SSE 再连接 409。DELETE 非 LongPolling 为 400，LongPolling 为 202 并完成移除。
3. 请求体 I/O 失败按框架消费为 400；写入过程 InvalidOperationException 为 404；写锁均释放，日志保留 Debug。真实 EnsureConnectionStateAsync 的用户名变更产生安全 Warning 并更新 User，真实 manager Scan 的认证过期触发原 close requested token。
4. 真实 context / manager 清理：已完成应用任务与失败 transport 任务进入等待 transport 分支；IOException / 内含 IOException 的 WebSocketException 为 Trace reset，其他异常为 Error dispose_failed，连接均移除。只给已完成应用任务设置错误并不能证明 manager 消费，该错误可能已通过 pipe 交给 transport。
5. 真实 LongPolling 使用内存 Pipe / HTTP：原载荷 200、完成或替换 204、超时空 200、请求取消维持原响应；非取消写入失败记录 Error，设置 500，并重抛同一异常。真实 context 的 send timeout 取消 SendingToken，LongPolling 设置 204 后重抛原取消异常，不生成 Error。
6. 真实 SSE 保留事件格式 / headers、安静取消；I/O 原异常向上传播。真实 WebSocketsServerTransport 执行 ProcessRequest / ProcessSocket，抽象 WebSocket 返回正常关闭或提前断开，保持正常关闭结果与 Debug 诊断，验证释放。
7. 真实 DefaultHubProtocolResolver 保持大小写不敏感、同名最后注册生效；未知或禁用协议安静返回 null，null 参数继续抛 ArgumentNullException。

HttpConnectionManager 仅构造并显式调用方法，没有触发 ApplicationStarted 或启动扫描循环；清理调用 CloseConnections 释放定时器和连接。超时证据通过显式传入未来 ticks 触发原 TryCancelSend，不修改运行时超时规则，也不依赖墙钟 sleep。

## 验证结果

- 本批定向测试 **142 / 142 通过**，包含全部 90 个生成器身份以及四模式输出 / 行为测试；在沙盒内运行，无端口。
- HTTP 错误边界、Kestrel / HTTPS / 证书、SignalR Core、运行日志适配与共享策略扩展回归 **661 / 661 通过**，包含本批 142 项；无失败、跳过或未运行，耗时约 31 秒。
- `dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`：**0 warning / 0 error**。
- `npm run check:logging-contract`：**27 / 27 通过**；`npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` 通过。仅保留 logging.md / records/index.md 的既有软篇幅提醒。

所有构建使用 `--no-restore`。测试直接执行已有 xUnit v3 进程内入口，没有使用需本机通信端口的 VSTest。扩展回归的两次沙盒尝试停留在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException`，精确结束测试进程后按最小范围提权重跑；同一用例在沙盒外约 0.09 秒通过，完整回归通过。前两次未完成运行不计为通过，未为此修改项目运行行为。提权后的测试仍仅使用内存 transport，没有项目服务启动或网络 bind。

可复现入口（从仓库根目录开始，运行器工作目录为测试输出目录）：

```bash
dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false
cd Radish.Api.Tests/bin/Debug/net10.0
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 -class 'Radish.Api.Tests.ErrorHandling.*' -class 'Radish.Api.Tests.SignalRFramework*' -class 'Radish.Api.Tests.RuntimeLog*'
```

上述扩展回归沿用既有内存 HTTP/1 / HTTP/2 / HTTP/3、SslStream 及临时证书用例；这些用例的 BindAsync 是测试内存工厂，未注册系统 Socket / QUIC listener。它们不能补足本批 Socket / QUIC 的原生连接证据。

## 验证限制与后续

- Socket / QUIC 证据仅含安装框架元数据、真实日志生成器和安全输出；没有创建 Socket、执行 bind、accept、真实读写 / reset / close、TLS / QUIC 握手或平台回调。当前机器 `QuicListener.IsSupported / QuicConnection.IsSupported` 均为 false，不能将生成器调用写成原生 QUIC 验收。
- Http.Connections 使用真实框架对象和内存依赖，WebSocket 是抽象类测试实现；不代表真实网络、代理、浏览器、Hub 项目宿主或生产验收。Reconnect 回调失败、后台扫描异常、WebSocket 发送 / 关闭异常与稀有超时等未触发分支仅有生成器 / 身份 / 输出证据。HttpConnectionContext 的 ID 6 / 7 / 8 在本版本保留定义但审计未发现调用，不宣称实际触发。
- transport 在调用日志前读取原因的 Message 等行为不由 sink 改写。日志适配也不吞掉外层输出失败；LongPolling 等已记录本层错误后向上传播，后续 API / 框架可能记录另一个完成阶段，不承诺跨阶段只输出一次。
- `Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Client` 当前没有发现产品使用，暂不纳入；其余框架来源与非 Serilog 通道继续只读盘点后确认。生产候选开关仍为 false，L2 尚未整体关闭。

## 框架依据

身份以本机安装的 ASP.NET Core 10.0.8 程序集为准，行为对照同版本官方源码：

- [SocketsLog](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Servers/Kestrel/Transport.Sockets/src/Internal/SocketsLog.cs)、[SocketConnection](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Servers/Kestrel/Transport.Sockets/src/Internal/SocketConnection.cs)。
- [QuicLog](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Servers/Kestrel/Transport.Quic/src/Internal/QuicLog.cs)、[QuicConnectionContext](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Servers/Kestrel/Transport.Quic/src/Internal/QuicConnectionContext.cs)、[QuicStreamContext](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Servers/Kestrel/Transport.Quic/src/Internal/QuicStreamContext.cs)。
- [HttpConnectionDispatcher](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/HttpConnectionDispatcher.cs)、[HttpConnectionManager](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/HttpConnectionManager.cs)、[HttpConnectionContext](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/HttpConnectionContext.cs)。
- [LongPollingServerTransport](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/Transports/LongPollingServerTransport.cs)、[ServerSentEventsServerTransport](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/Transports/ServerSentEventsServerTransport.cs)、[WebSocketsServerTransport](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/common/Http.Connections/src/Internal/Transports/WebSocketsServerTransport.cs)。
- [DefaultHubProtocolResolver](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/SignalR/server/Core/src/Internal/DefaultHubProtocolResolver.cs)。

## 已知事件编码清单

以下清单保留原等级。每组另有同前缀 `unclassified` 摘要，适用于未知、畸形或无法精确识别的事件。

### Sockets

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 6 / ConnectionReadFin | Debug | http.socket_read_fin |
| 7 / ConnectionWriteFin | Debug | http.socket_write_fin |
| 8 / ConnectionWriteRst | Debug | http.socket_write_reset |
| 14 / ConnectionError | Debug | http.socket_connection_error |
| 19 / ConnectionReset | Debug | http.socket_connection_reset |
| 4 / ConnectionPause | Debug | http.socket_paused |
| 5 / ConnectionResume | Debug | http.socket_resumed |

### Quic

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / AcceptedConnection | Debug | http.quic_connection_accepted |
| 2 / AcceptedStream | Debug | http.quic_stream_accepted |
| 3 / ConnectedStream | Debug | http.quic_stream_connected |
| 4 / ConnectionError | Debug | http.quic_connection_error |
| 5 / ConnectionAborted | Debug | http.quic_connection_aborted |
| 6 / ConnectionAbort | Debug | http.quic_connection_abort |
| 7 / StreamError | Debug | http.quic_stream_error |
| 8 / StreamPause | Debug | http.quic_stream_paused |
| 9 / StreamResume | Debug | http.quic_stream_resumed |
| 10 / StreamShutdownWrite | Debug | http.quic_stream_write_shutdown |
| 11 / StreamAbortedRead | Debug | http.quic_stream_read_aborted |
| 12 / StreamAbortedWrite | Debug | http.quic_stream_write_aborted |
| 13 / StreamAbort | Debug | http.quic_stream_abort |
| 14 / StreamAbortRead | Debug | http.quic_stream_read_abort |
| 15 / StreamAbortWrite | Debug | http.quic_stream_write_abort |
| 16 / StreamPooled | Trace | http.quic_stream_pooled |
| 17 / StreamReused | Trace | http.quic_stream_reused |
| 18 / ConnectionListenerCertificateNotSpecified | Warning | http.quic_certificate_missing |
| 19 / ConnectionListenerApplicationProtocolsNotSpecified | Warning | http.quic_protocols_missing |
| 20 / ConnectionListenerStarting | Debug | http.quic_listener_starting |
| 21 / ConnectionListenerAborted | Debug | http.quic_listener_aborted |
| 22 / StreamTimeoutRead | Debug | http.quic_stream_read_timeout |
| 23 / StreamTimeoutWrite | Debug | http.quic_stream_write_timeout |
| 24 / ConnectionListenerAcceptConnectionFailed | Debug | http.quic_listener_accept_failed |

### HttpConnectionContext

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / DisposingConnection | Trace | signalr.http_context_disposing |
| 2 / WaitingForApplication | Trace | signalr.http_context_waiting_application |
| 3 / ApplicationComplete | Trace | signalr.http_context_application_complete |
| 4 / WaitingForTransport | Trace | signalr.http_context_waiting_transport |
| 5 / TransportComplete | Trace | signalr.http_context_transport_complete |
| 6 / ShuttingDownTransportAndApplication | Trace | signalr.http_context_shutting_down |
| 7 / WaitingForTransportAndApplication | Trace | signalr.http_context_waiting_completion |
| 8 / TransportAndApplicationComplete | Trace | signalr.http_context_complete |
| 9 / TransportSendTimeout | Trace | signalr.http_context_send_timeout |

### HttpConnectionDispatcher

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / ConnectionDisposed | Debug | signalr.http_dispatcher_disposed |
| 2 / ConnectionAlreadyActive | Debug | signalr.http_dispatcher_already_active |
| 3 / PollCanceled | Trace | signalr.http_dispatcher_poll_canceled |
| 4 / EstablishedConnection | Debug | signalr.http_dispatcher_established |
| 5 / ResumingConnection | Debug | signalr.http_dispatcher_resuming |
| 6 / ReceivedBytes | Trace | signalr.http_dispatcher_received |
| 7 / TransportNotSupported | Debug | signalr.http_dispatcher_transport_unsupported |
| 8 / CannotChangeTransport | Debug | signalr.http_dispatcher_transport_change_rejected |
| 9 / PostNotAllowedForWebSockets | Debug | signalr.http_dispatcher_websocket_post_rejected |
| 10 / NegotiationRequest | Debug | signalr.http_dispatcher_negotiating |
| 11 / ReceivedDeleteRequestForUnsupportedTransport | Trace | signalr.http_dispatcher_delete_unsupported |
| 12 / TerminatingConection | Trace | signalr.http_dispatcher_terminating |
| 13 / ConnectionDisposedWhileWriteInProgress | Debug | signalr.http_dispatcher_disposed_during_write |
| 14 / FailedToReadHttpRequestBody | Debug | signalr.http_dispatcher_request_read_failed |
| 15 / NegotiateProtocolVersionMismatch | Debug | signalr.http_dispatcher_negotiate_version_mismatch |
| 16 / InvalidNegotiateProtocolVersion | Debug | signalr.http_dispatcher_negotiate_version_invalid |
| 17 / UserNameChanged | Warning | signalr.http_dispatcher_user_changed |
| 18 / NotifyOnReconnectError | Debug | signalr.http_dispatcher_reconnect_failed |

### HttpConnectionManager

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / CreatedNewConnection | Debug | signalr.http_manager_created |
| 2 / RemovedConnection | Debug | signalr.http_manager_removed |
| 3 / FailedDispose | Error | signalr.http_manager_dispose_failed |
| 5 / ConnectionTimedOut | Trace | signalr.http_manager_timed_out |
| 4 / ConnectionReset | Trace | signalr.http_manager_reset |
| 7 / ScanningConnectionsFailed | Error | signalr.http_manager_scan_failed |
| 9 / HeartBeatStarted | Trace | signalr.http_manager_heartbeat_started |
| 10 / HeartBeatEnded | Trace | signalr.http_manager_heartbeat_ended |
| 11 / AuthenticationExpired | Debug | signalr.http_manager_authentication_expired |

### LongPollingTransport

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / LongPolling204 | Debug | signalr.long_polling_completed |
| 2 / PollTimedOut | Debug | signalr.long_polling_timed_out |
| 3 / LongPollingWritingMessage | Trace | signalr.long_polling_writing |
| 4 / LongPollingDisconnected | Debug | signalr.long_polling_disconnected |
| 5 / LongPollingTerminated | Error | signalr.long_polling_terminated |

### ServerSentEventsTransport

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / SSEWritingMessage | Trace | signalr.sse_writing |

### WebSocketsTransport

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / SocketOpened | Debug | signalr.websocket_opened |
| 2 / SocketClosed | Debug | signalr.websocket_closed |
| 3 / ClientClosed | Debug | signalr.websocket_client_closed |
| 4 / WaitingForSend | Debug | signalr.websocket_waiting_send |
| 5 / FailedSending | Debug | signalr.websocket_send_failed |
| 6 / FinishedSending | Debug | signalr.websocket_send_finished |
| 7 / WaitingForClose | Debug | signalr.websocket_waiting_close |
| 8 / CloseTimedOut | Debug | signalr.websocket_close_timeout |
| 9 / MessageReceived | Trace | signalr.websocket_received |
| 10 / MessageToApplication | Trace | signalr.websocket_forwarded |
| 11 / SendPayload | Trace | signalr.websocket_sending |
| 12 / ErrorWritingFrame | Debug | signalr.websocket_write_failed |
| 14 / ClosedPrematurely | Debug | signalr.websocket_closed_prematurely |
| 15 / ClosingWebSocketFailed | Debug | signalr.websocket_close_failed |
| 16 / SendErrored | Debug | signalr.websocket_send_errored |

### DefaultHubProtocolResolver

| ID / Name | 原等级 | 公共编码 |
| --- | --- | --- |
| 1 / RegisteredSignalRProtocol | Debug | signalr.protocol_registered |
| 2 / FoundImplementationForProtocol | Debug | signalr.protocol_resolved |
