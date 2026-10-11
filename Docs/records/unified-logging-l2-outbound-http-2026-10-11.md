# L2 YARP 与出站 HTTP 框架日志（2026-10-11）

本批按项目所有者确认的 15 个精确来源、75 个编码实施。当前契约见[事件契约第 59 节](../features/unified-logging-contract.md)；本文保存来源清单、隔离验证与真实网络限制。生产候选开关保持关闭，L2 尚未整体完成。

## 实际调用关系

- Gateway 的 `AddReverseProxy().LoadFromConfig(...)` / `MapReverseProxy()` 使用 YARP 2.2.0。转发客户端由 `ForwarderHttpClientFactory` 创建 `SocketsHttpHandler` / `HttpMessageInvoker`，不经 `IHttpClientFactory` 或 Kestrel Socket transport 的 `.Client` 工厂。
- `AddHttpClient<PublicHeadSnapshotClient>` 注册名称为 `PublicHeadSnapshotClient`；`GatewayHostHealthChecks.AddGatewayHostHealthChecks` 经 HealthChecks.Uris 9.0.0 为 `api-service`、`auth-service`、`console-service` 注册命名客户端。每个名称均有 `LogicalHandler` / `ClientHandler` 两个来源，使用已安装的 Microsoft.Extensions.Http 10.0.0.0 程序集，源码核对 .NET 10.0.8。
- 当前产品代码未引用 `SocketConnectionFactory` / `IConnectionFactory`；Socket `.Client` 继续后置。服务端 Socket 来源的既有治理不代表客户端来源已覆盖。
- 参考：[YARP 工厂源码](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Forwarder/ForwarderHttpClientFactory.cs)、[HttpClient 日志源码](https://github.com/dotnet/runtime/blob/v10.0.8/src/libraries/Microsoft.Extensions.Http/src/Logging/LogHelper.cs)、[健康检查命名注册源码](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/blob/2b47b154cb48f1cdf7a7708b3708e8187dc09502/src/HealthChecks.Uris/DependencyInjection/UrisHealthCheckBuilderExtensions.cs)。

## 精确来源与编码

以下名称均为完整 `SourceContext`，不进行前缀匹配。每个来源另有一个 `_unclassified` 编码，合计 60 个已知编码 + 15 个未知摘要。

| 精确来源 | 编码前缀 | 已知数 |
| --- | --- | ---: |
| `Yarp.ReverseProxy.Forwarder.HttpForwarder` | `proxy.forwarder_` | 8 |
| `Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory` | `proxy.client_` | 2 |
| `Yarp.ReverseProxy.Forwarder.ForwarderMiddleware` | `proxy.middleware_` | 2 |
| `Yarp.ReverseProxy.LoadBalancing.LoadBalancingMiddleware` | `proxy.balancing_` | 1 |
| `Yarp.ReverseProxy.Model.ProxyPipelineInitializerMiddleware` | `proxy.pipeline_` | 2 |
| `Yarp.ReverseProxy.Limits.LimitsMiddleware` | `proxy.limits_` | 1 |
| `Microsoft.Extensions.Http.DefaultHttpClientFactory` | `http.client_factory_` | 4 |
| `System.Net.Http.HttpClient.PublicHeadSnapshotClient.LogicalHandler` | `http.public_head_pipeline_` | 5 |
| `System.Net.Http.HttpClient.PublicHeadSnapshotClient.ClientHandler` | `http.public_head_handler_` | 5 |
| `System.Net.Http.HttpClient.api-service.LogicalHandler` | `http.api_health_pipeline_` | 5 |
| `System.Net.Http.HttpClient.api-service.ClientHandler` | `http.api_health_handler_` | 5 |
| `System.Net.Http.HttpClient.auth-service.LogicalHandler` | `http.auth_health_pipeline_` | 5 |
| `System.Net.Http.HttpClient.auth-service.ClientHandler` | `http.auth_health_handler_` | 5 |
| `System.Net.Http.HttpClient.console-service.LogicalHandler` | `http.console_health_pipeline_` | 5 |
| `System.Net.Http.HttpClient.console-service.ClientHandler` | `http.console_health_handler_` | 5 |

### YARP 与 DefaultHttpClientFactory

下面的编码后缀追加到对应来源的编码前缀；数字与 Name 共同匹配，框架升级后的未知或畸形身份只输出未知安全摘要。

| 来源简名 | ID / Name | 原等级 | 编码后缀 |
| --- | --- | --- | --- |
| HttpForwarder | 9 / Forwarding | Information | `forwarding` |
| HttpForwarder | 56 / ResponseReceived | Information | `response_received` |
| HttpForwarder | 48 / ForwardingError | Warning | `failed` |
| HttpForwarder | 66 / ForwardingRequestCancelled | Debug | `cancelled` |
| HttpForwarder | 59 / NotForwarding | Information | `skipped` |
| HttpForwarder | 61 / RetryingWebSocketDowngradeNoConnect | Information | `websocket_retry_no_connect` |
| HttpForwarder | 62 / RetryingWebSocketDowngradeNoHttp2 | Information | `websocket_retry_no_http2` |
| HttpForwarder | 63 / InvalidSecWebSocketKeyHeader | Warning | `websocket_key_invalid` |
| ForwarderHttpClientFactory | 45 / ClientCreated | Debug | `created` |
| ForwarderHttpClientFactory | 46 / ClientReused | Debug | `reused` |
| ForwarderMiddleware | 7 / NoAvailableDestinations | Warning | `no_destinations` |
| ForwarderMiddleware | 8 / MultipleDestinationsAvailable | Warning | `multiple_destinations` |
| LoadBalancingMiddleware | 7 / NoAvailableDestinations | Warning | `no_destinations` |
| ProxyPipelineInitializerMiddleware | 4 / NoClusterFound | Information | `cluster_missing` |
| ProxyPipelineInitializerMiddleware | 64 / TimeoutNotApplied | Error | `timeout_not_applied` |
| LimitsMiddleware | 60 / MaxRequestBodySizeSet | Debug | `body_size_set` |
| DefaultHttpClientFactory | 100 / CleanupCycleStart | Debug | `cleanup_started` |
| DefaultHttpClientFactory | 101 / CleanupCycleEnd | Debug | `cleanup_completed` |
| DefaultHttpClientFactory | 102 / CleanupItemFailed | Error | `cleanup_failed` |
| DefaultHttpClientFactory | 103 / HandlerExpired | Debug | `handler_expired` |

### 四个命名 HttpClient

每个名称各自应用下表，两个 handler 不共用编码。未列出的客户端名以及大小写不同或带子类别的来源原样通过。

| ID | LogicalHandler 的 Name | ClientHandler 的 Name | 原等级 | 编码后缀 |
| --- | --- | --- | --- | --- |
| 100 | RequestPipelineStart | RequestStart | Information | `started` |
| 101 | RequestPipelineEnd | RequestEnd | Information | `completed` |
| 102 | RequestPipelineRequestHeader | RequestHeader | Trace | `request_headers` |
| 103 | RequestPipelineResponseHeader | ResponseHeader | Trace | `response_headers` |
| 104 | RequestPipelineFailed | RequestFailed | Information | `failed` |

## 输出与行为契约

- `OutboundHttpFrameworkLogSink` 在上下文合并后重建目标事件，接入旧链代码 / 配置 WriteTo、配置 AuditTo 与候选输出。保留时间、原等级、类型化 trace/span、固定来源、`SourceCategory=http`、Diagnostic、事件码与受控 failureKind；删除原模板、URI、header、cluster / route 标识、原异常和任意 scope。
- Trace / Debug / Information 保持诊断属性；候选输出沿用 Info 归一和 Development diagnostics 开关。未知 Warning / Error / Critical 保持可见；不按异常文本推断事件，也不提升 `RequestFailed` 的 Information 等级。
- 适配器只重建日志，不修改代理、HttpClient、响应、取消、异常传播或生命周期。内外 handler 的两个失败记录、负载均衡与最终转发的两个无目标 Warning 各自保留，不承诺跨阶段单条日志。
- `HttpRequestException` 在真实两个 logging handler 中记录 Information 后原样重抛；取消与其他异常继续传播，不合成 failure 日志。HTTP 500 响应仍走正常完成事件，业务层另行处理响应语义。
- YARP 请求失败保留 502 与 `IForwarderErrorFeature` 的原异常；请求取消保留 Debug 和框架已有的 RequestAborted → 400、调用方 token → 502 区别；无目标保持 503。响应体读取失败与 trailer transform 直接抛出的边界保持不同。
- 工厂释放失败仍由框架消费为 Error，不重新入队；仍存活的 handler 延后释放。不会借日志治理修改框架清理策略。

## 四模式与无网络验证

旧 / 候选 × Development / Production 均验证：

1. 冻结全部 60 个已知身份，调用安装版本的真实 LoggerMessage delegate / LogHelper，核对 ID、Name、等级和完整生成器清单；覆盖 diagnostics true / false、配置 WriteTo / AuditTo、未知零事件 / 未来事件、畸形身份、不可读异常 / payload、trace/span 和非目标来源同实例通过。
2. 使用实际命名注册、真实 logging handler 和替代 primary handler。请求、响应与 scope 带敏感哨兵，测试显式关闭 header 默认脱敏，以证明安全输出不依赖框架默认设置。验证成功 / HTTP 500、两个 Information 失败事件及同一异常传播、取消 token 和其他异常传播。
3. 真实 `DefaultHttpClientFactory` 复用 handler、显式调用 expiry / cleanup 方法；活跃项留队，受控 weak-reference 变为不可达后执行释放，成功 / 失败均正确移出队列。验证不依赖 GC 或真实 timer 到期，测试最后释放清理 timer。
4. 真实 YARP forwarder 使用内存 HTTP context / stream 和替代 handler，验证响应状态 / header / body 原样转发、请求失败 502、取消 400 / 502、响应体错误 502 与原异常 feature、trailer transform 同一异常向上传播。
5. 真实负载均衡 / 转发中间件保留两个 Warning 和最终 503；initializer 缺 cluster 返回 503，未应用已要求的 timeout 则 Error 后抛出；limits 仅修改可写 body-size feature，继续调用下游。
6. 真实 YARP 工厂仅创建 / 复用 / 替换客户端实例，不调用其 Send；配置相同复用、配置改变新建。DI 只注册 / 解析依赖，不创建或启动项目宿主，不加载代理路由配置，不执行 URL 健康探测。

WebSocket 降级重试、无转发分支、多目标 fallback 等未逐一触发的行为，证据限于真实日志生成器与输出验证。真实网络连接、DNS、TLS、代理、HTTP/2 / HTTP/3、WebSocket 握手、连接池轮换与线上计时均未验收；内存行为测试不替代这些证据。既有 Socket / QUIC 验证限制继续有效。

## 验证结果

- 本批定向测试 **112 / 112 通过**，包括 60 个真实生成器身份、四模式输出与四模式内存行为；沙盒内执行。
- HTTP 错误边界、Kestrel / HTTPS / 证书、SignalR Core、运行日志适配与共享策略扩展回归 **773 / 773 通过**，包含本批 112 项；无失败、跳过或未运行，耗时约 30 秒。
- 扩展回归首次在沙盒内停在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException`，55 秒上限后仅停止本次测试进程，该次不计通过；最小范围获准提权后以相同进程内参数完整通过。未为此修改产品逻辑或跳过测试。
- `dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`：**0 warning / 0 error**。
- `npm run check:logging-contract`：**27 / 27 通过**；`npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` 通过。文档篇幅仅有既有 logging / records 索引软上限提醒，未超过硬上限。
- 注册表对照：精确增加 75 个事件，已有事件定义未改写；15 个来源的 60 个已知映射与 15 个未知摘要和注册表一致。

扩展回归命令（工作目录 `Radish.Api.Tests/bin/Debug/net10.0`）：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*'
```

进程内 xUnit 入口从测试输出目录执行，避免 VSTest 通信端口；不安装 / 恢复依赖、不启动项目服务、不绑定网络端口。所有 API 调用使用替代 primary handler，`example.invalid` 仅为 URI 值，不进行 DNS / 网络请求。

## 后续边界

YARP 配置 provider / manager / validator、条件性 SessionAffinity 与主动 / 被动健康检查、Socket transport `.Client`、其他命名 HttpClient、认证 / 路由等剩余框架来源以及 EventSource / Metrics 等非 Serilog 通道保留后续只读盘点与单独确认。本批不取得真实网络运行、发布部署或生产切换授权。
