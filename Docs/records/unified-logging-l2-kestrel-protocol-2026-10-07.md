# L2 Kestrel HTTP/2、HTTP/3 与 HTTPS 来源治理

日期：2026-10-07（Asia/Shanghai）。项目所有者确认[实施方案](./unified-logging-l2-kestrel-protocol-plan-2026-10-07.md)后完成适配。承接[连接 / 坏请求治理](./unified-logging-l2-kestrel-connections-2026-10-07.md)，生产候选开关关闭，L2 尚未整体完成。

## 变更与事件

扩展现有 `HttpServerFrameworkLogSink`，只增加以下三个精确来源：`Microsoft.AspNetCore.Server.Kestrel.Http2`、`Microsoft.AspNetCore.Server.Kestrel.Http3`、`Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware`。沿用上下文合并后、输出前的接线，覆盖旧链代码 / 配置 WriteTo / AuditTo 与候选输出；没有增加新输出链或前缀匹配。

按来源 + ID + Name 匹配 36 个已知事件，另有 3 个未知编码。保留原时间、等级、类型化 trace/span、固定来源与受控 failureKind，不复制原模板、异常详情、连接 / 流 ID、请求头、证书身份 / 路径或任意 scope，不修改原 LogEvent。官方固定版本源码与实际生成身份依据见已确认方案。

| 来源后缀 | ID / Name | 事件编码 | 框架等级 |
| --- | --- | --- | --- |
| Http2 | 29 / `Http2ConnectionError` | `http.http2_connection_error` | Debug |
| Http2 | 30 / `Http2StreamError` | `http.http2_stream_error` | Debug |
| Http2 | 31 / `HPackDecodingError` | `http.http2_headers_decode_failed` | Debug |
| Http2 | 35 / `Http2StreamResetAbort` | `http.http2_stream_reset` | Debug |
| Http2 | 36 / `Http2ConnectionClosing` | `http.http2_connection_closing` | Debug |
| Http2 | 37 / `Http2FrameReceived` | `http.http2_frame_received` | Trace |
| Http2 | 38 / `HPackEncodingError` | `http.http2_headers_encode_failed` | Information |
| Http2 | 40 / `Http2MaxConcurrentStreamsReached` | `http.http2_stream_limit_reached` | Debug |
| Http2 | 48 / `Http2ConnectionClosed` | `http.http2_connection_closed` | Debug |
| Http2 | 49 / `Http2FrameSending` | `http.http2_frame_sending` | Trace |
| Http2 | 60 / `Http2QueueOperationsExceeded` | `http.http2_output_queue_exceeded` | Critical |
| Http2 | 61 / `Http2UnexpectedDataRemaining` | `http.http2_output_data_remaining` | Critical |
| Http2 | 62 / `Http2ConnectionQueueProcessingCompleted` | `http.http2_output_queue_completed` | Debug |
| Http2 | 63 / `Http2UnexpectedConnectionQueueError` | `http.http2_output_queue_failed` | Critical |
| Http2 | 64 / `Http2TooManyEnhanceYourCalms` | `http.http2_reset_limit_exceeded` | Debug |
| Http2 | 65 / `Http2FlowControlQueueOperationsExceeded` | `http.http2_flow_queue_exceeded` | Debug |
| Http2 | 66 / `Http2FlowControlQueueMaximumTooLow` | `http.http2_flow_queue_adjusted` | Debug |
| Http3 | 42 / `Http3ConnectionError` | `http.http3_connection_error` | Debug |
| Http3 | 43 / `Http3ConnectionClosing` | `http.http3_connection_closing` | Debug |
| Http3 | 44 / `Http3ConnectionClosed` | `http.http3_connection_closed` | Debug |
| Http3 | 45 / `Http3StreamAbort` | `http.http3_stream_aborted` | Debug |
| Http3 | 46 / `Http3FrameReceived` | `http.http3_frame_received` | Trace |
| Http3 | 47 / `Http3FrameSending` | `http.http3_frame_sending` | Trace |
| Http3 | 50 / `Http3OutboundControlStreamError` | `http.http3_control_stream_failed` | Debug |
| Http3 | 51 / `QPackDecodingError` | `http.http3_headers_decode_failed` | Debug |
| Http3 | 52 / `QPackEncodingError` | `http.http3_headers_encode_failed` | Information |
| Http3 | 53 / `Http3GoAwayHighestOpenedStreamId` | `http.http3_goaway` | Debug |
| Https | 1 / `AuthenticationFailed` | `http.tls_handshake_failed` | Debug |
| Https | 2 / `AuthenticationTimedOut` | `http.tls_handshake_timed_out` | Debug |
| Https | 3 / `HttpsConnectionEstablished` | `http.tls_established` | Debug |
| Https | 4 / `Http2DefaultCiphersInsufficient` | `http.tls_protocol_fallback` | Information |
| Https | 5 / `LocateCertWithPrivateKey` | `http.tls_certificate_search` | Debug |
| Https | 6 / `FoundCertWithPrivateKey` | `http.tls_certificate_found` | Debug |
| Https | 7 / `FailToLocateCertificate` | `http.tls_certificate_search_failed` | Debug |
| Https | 8 / `FailToOpenStore` | `http.tls_certificate_store_failed` | Debug |
| Https | 9 / `NoSubjectAlternativeName` | `http.tls_certificate_missing_san` | Information |
| Http2 | 未知 / 不匹配身份 | `http.http2_unclassified` | 保留输入等级 |
| Http3 | 未知 / 不匹配身份 | `http.http3_unclassified` | 保留输入等级 |
| Https | 未知 / 不匹配身份 | `http.tls_unclassified` | 保留输入等级 |

Warning / Error / Critical 保持可见；HTTP/2 输出队列的三个 Critical 在 diagnostics 关闭时仍输出。旧链保持原等级，候选 Fatal 按公共规范输出 Error；低级别仍遵循 diagnostics / 最低等级，候选 Development 的 Trace / Debug 规范为 Info + diagnostic，Production 过滤过程诊断。不把协议拒绝或 TLS 失败自动提升为 Error。

## 真实路径与行为

- **HTTP/2**：发送真实 preface / SETTINGS / HEADERS / PING。缺少 `:path` 返回 RST_STREAM(1)，随后同连接的新流正常返回 200；非法 PING 流 ID 返回 GOAWAY(1)，截断 HPACK 索引返回 GOAWAY(9)。HPACK 索引 0 在当前框架中走通用异常分支，返回 GOAWAY(2) 与主来源 `http.server_unclassified` Warning；按实际结果留痕，不改框架行为或按原文分类。
- **HTTP/2 与 HTTP/3 应用异常**：真实处理器保留 500；Development 由自动开发异常页消费，Production 由 Kestrel 主来源记录，并向 Hosting 的 DisposeContext 传原异常。正常请求 / 应用异常均验证请求释放与最终连接清理。
- **HTTP/3**：真实 KestrelServerImpl 经唯一内存多路复用 transport 处理 QPACK 请求 / 响应帧。控制流 DATA 被拒绝，连接错误码为 H3_FRAME_UNEXPECTED(0x105)；应用 Abort 为 H3_INTERNAL_ERROR(0x102)，只结束该流，同连接下一请求仍成功。测试夹具提供框架要求的持久流状态、方向、协议错误与 TLS 已建立 feature；这些 feature 不代表 QUIC 握手。
- **HTTPS**：真实 SslStream / HTTPS 中间件完成 TLS 1.2 握手与 HTTP/1 200；无效 TLS 字节不进入应用。OnAuthenticate 中的 OperationCanceledException 进入原 timed_out 分支，IO / AuthenticationException 进入 failed 分支，其他异常传播到主来源。握手后委托失败仍恢复原 transport，并由主来源报告；回调取消用例不等于真实计时器超时。
- 不修改响应、协议错误码、异常消费、连接 / 流生命周期、协议配置、证书验证或全局去重。证书包装方法在 ILogger 前读取 thumbprint 等值的既有行为，不属于输出适配器控制范围。

## 验证与清理

使用已安装 .NET SDK 10.0.300 / ASP.NET Core 10.0.8 与现有依赖；真实协议测试只初始化 Kestrel 测试引擎，不运行 WebApplication.Start / Run 或 API / Auth / Gateway 宿主。

- HTTP/2 / HTTPS 使用内存普通连接工厂；HTTP/3 显式移除普通 / 多路复用工厂，只注册内存 listener，并断言唯一注册。Loopback endpoint 交给内存 listener 保存，不执行系统网络 bind；没有 TCP / UDP / MsQuic。
- TLS 测试生成并释放临时自签证书 / RSA 密钥，测试客户端仅接受该证书；没有导入用户信任存储、访问真实私钥或更改产品校验规则。等待连接取消 / 处理任务结束并释放流、Pipe、SSL 和测试服务容器。
- 新增 36 个四模式协议用例、36 个运行时元数据用例、4 个遍历全部真实生成方法及 diagnostics 开关的四模式用例、42 个 sink 身份 / 安全用例及 3 个非目标透传用例，共 121 个。扩展现有未知事件、AuditTo 与 WriteTo 检查，覆盖不可读取异常 / 属性、原事件不变、等级 / 时间 / trace 和敏感 scope。
- HTTP/3 初次测试暴露夹具缺少 IPersistentStateFeature，修复后真实请求及中止路径通过；控制流 DATA 的预期按源码修正为 0x105。HTTP/2 的索引 0 与截断索引按两条实际路径验证，失败尝试不计成功证据。
- 定向 .NET 回归启用 `TreatWarningsAsErrors=true`、`--no-restore`：**500 / 500 通过**；公共 Node 日志契约 **27 / 27 通过**。MSBuild 在沙盒内被本地 IPC 权限阻止后，获准在沙盒外运行测试，未因此扩大产品运行范围。沙盒失败进程收到取消后已退出，最终确认无本批测试残留。

Docs、changed-only 文本卫生与 `git diff --check` 通过。日志指南 768 行与记录索引 332 行有既有篇幅软提醒，未达到硬上限。

定向命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -p:TreatWarningsAsErrors=true --filter 'FullyQualifiedName~ProtocolLoggingTests|FullyQualifiedName~KestrelProtocolGenerationTests|FullyQualifiedName~KestrelConnectionLoggingTests|FullyQualifiedName~OuterHttp|FullyQualifiedName~KestrelHttp|FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~SignalRFramework|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQuery'
```

## 证据边界与后续

HTTP/2 输出队列 Critical、HPACK / QPACK 编码失败、流控限制、HTTP/3 出站控制流故障 / QPACK 解码、平台协议回退及证书库诊断具有真实生成方法、元数据和安全映射证据；本批没有逐一触发其内部故障。没有验证真实计时器超时、ALPN 平台组合、证书文件监视、信任存储或 QUIC TLS。

三个来源完成不等于全部 HTTP / TLS 安全验收。下一步只读核对 CertificatePathWatcher 与配置加载来源，再确认后续实现范围；Socket / QUIC transport、Http.Connections、协议 resolver、其他框架来源和非 Serilog 通道继续治理。早期消费点与独立可靠性问题仍按当前规划处理，生产 `RadishLogging.Enabled=false` 保持。
