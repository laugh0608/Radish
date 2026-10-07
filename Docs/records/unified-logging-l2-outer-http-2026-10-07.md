# L2 外层 HTTP 三来源安全适配

日期：2026-10-07（Asia/Shanghai）。本批按项目所有者确认的[外层 HTTP 方案](./unified-logging-l2-outer-http-plan-2026-10-07.md)实施，承接 ExceptionHandlerMiddleware 治理。

## 实现与接线

- 新增 `HttpServerFrameworkLogSink`，精确匹配 `Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware`、`Microsoft.AspNetCore.Hosting.Diagnostics`、`Microsoft.AspNetCore.Server.Kestrel`，不使用来源前缀匹配。
- 在上下文合并后、所有旧链代码 / 配置 WriteTo / AuditTo 与候选 RuntimeSerilogSink 之前重建安全事件，与既有 HTTP 异常中间件及 SignalR 适配串联。
- 保留原时间、等级、可用类型化 trace/span、固定 SourceContext、受控事件元数据及存在异常时的 failureKind。不渲染原模板，不读取异常 Message / ToString，不复制请求字段、连接身份或任意 scope；原始 LogEvent 不被修改。
- 目标来源未知 Warning / Error / Critical 保持可见；低级别事件标记为诊断，继续受最低等级 / diagnostics 控制。原 Critical 在旧链保持 Serilog Fatal，在候选链按公共契约映射为 Error。
- 非目标来源继续透传，未改变响应、路由、开发异常页启用、原异常传播和回调流程。RuntimeProcess 对 Generic Host 启动失败的现有移交规则不变。

## 事件映射

以下 Name 为实际 EventId.Name，不能以方法名替代。Hosting 的缺失 / null Name 是有效契约；错误类型或其他字符串不按已知事件识别。

| 来源 | EventId / Name | 受控事件 | 原等级 |
| --- | --- | --- | --- |
| DeveloperExceptionPage | 1 / UnhandledException | `http.developer_exception` | Error |
| DeveloperExceptionPage | 2 / ResponseStarted | `http.developer_response_started` | Warning |
| DeveloperExceptionPage | 3 / DisplayErrorPageException | `http.developer_page_failed` | Error |
| DeveloperExceptionPage | 4 / RequestAborted | `http.developer_request_aborted` | Debug |
| Hosting.Diagnostics | 1 / null | `http.request_started` | Information |
| Hosting.Diagnostics | 2 / null | `http.request_finished` | Information |
| Hosting.Diagnostics | 16 / null | `http.request_unhandled` | Information |
| Hosting.Diagnostics | 6 / null | `http.host_pipeline_failed` | Critical |
| Hosting.Diagnostics | 11 / null | `http.hosting_startup_failed` | Critical |
| Hosting.Diagnostics | 15 / null | `http.host_binding_overridden` | Warning |
| Kestrel 主来源 | 13 / ApplicationError | `http.server_application_failed` | Error |
| Kestrel 主来源 | 22 / HeartbeatSlow | `http.server_heartbeat_slow` | Warning |
| Kestrel 主来源 | 23 / ApplicationNeverCompleted | `http.server_application_incomplete` | Critical |
| Kestrel 主来源 | 41 / InvalidResponseHeaderRemoved | `http.server_response_headers_removed` | Warning |
| Kestrel 主来源 | 64 / Http2DisabledWithHttp1AndNoTls、65 / Http3DisabledWithHttp1AndNoTls | `http.server_protocol_downgraded` | Warning |
| Kestrel 主来源 | 66 / RequestAborted | `http.server_request_aborted` | Debug |
| Kestrel 主来源 | 67 / RequestBodyDrainBodyReaderInvalidState | `http.server_body_drain_failed` | Error |

三个来源的未知事件分别为 `http.developer_unclassified`、`http.hosting_unclassified`、`http.server_unclassified`，不丢弃故障。共新增 20 个公共事件编码，没有新增属性 schema 或配置键。

## 真实管道与协议证据

- `OuterHttpTestPipeline` 调用当前框架的宿主管道组装器，包含 Development 自动开发异常页；再通过真实 HostingApplication 执行请求生命周期。反射桥接只处理内部类型并解包 TargetInvocationException，测试检查业务原异常实例，不把反射包装当成服务器行为。
- 自动开发页在 Development 消费普通异常并生成原 500 页面；Production 对照继续向调用方传播，Hosting 只记录请求起止，不额外生成请求异常 Error。检查请求 scope 清理和父 Activity 恢复。
- 显式开发页保留原文本 / HTML 和 BadHttpRequestException 的 413；开发响应仍包含原有异常与请求详情，日志摘要的裁剪不施加于页面正文。显式与自动两层开发页在响应已开始时各自报告，保留原正文 / 状态 / header 及原异常。
- 开发页写入前 / 部分写入后失败保留原请求异常，分别记录原异常报告及页面生成故障。IO / 取消 / 其他异常 × token 是否取消 × HasStarted 的矩阵保持原 499、原已开始状态与诊断等级。
- 正常成功、普通未匹配 404、已处理 API 404 只有 Hosting 诊断；RequestUnhandled 表示到达应用末尾，不是 Error。
- `MemoryKestrelFixture` 初始化测试内 Kestrel 实例（StartAsync / StopAsync），只向其传入自有内存连接工厂，通过双向 Pipe 发送真实 HTTP/1 字节。endpoint 仅是描述符，不调用系统 bind；没有注册 socket transport、启动 Radish 项目宿主或网络监听。停止并释放测试实例，连接与 Pipe 随用随释放。
- 真实 HTTP/1 验证普通应用异常：Development 的开发页消费并返回原详情，Production 的 Kestrel 生成安全 500，原异常交给 Hosting DisposeContext；日志归属随原框架处理不同而不同。
- OnStarting 故障停止后续启动回调并生成 500；OnCompleted 故障保留 200 且继续后续完成回调。两者均由 Kestrel ApplicationError 记录，不能单按事件名判定最终响应失败。
- 已开始响应的 API 重抛保留 206、header 与部分正文，按环境保留 ExceptionHandlerMiddleware → DeveloperExceptionPage → Kestrel 的阶段事件；非 API 委托拒绝处理保留内部两个事件以及后续开发页 / Kestrel 归属。没有请求级去重。
- 主动中断后 IO 异常保持取消诊断，未生成应用失败 Error，未向客户端写入伪成功响应。

## 验证限制

- 本批新增 107 项测试。Hosting 三个启动 / 配置事件通过真实日志生成方法验证；Kestrel 心跳慢、应用未完成、无效响应头、协议降级及请求体收尾故障只具备映射和运行时元数据证据，不宣称触发过全部真实故障分支。
- 未运行真实 socket、TLS、HTTP/2 / HTTP/3、完整 Radish 宿主、认证 / MVC、生产部署、日志库入库或文件轮转。框架内部桥接及 LoggerMessage 断言对应本机 ASP.NET Core 10.0.8，升级框架时应按失败证据重新核对。
- Kestrel.Connections 等子类别在内存协议测试中仍由框架生成，安全断言明确只覆盖本批三个来源及此前 ExceptionHandlerMiddleware；不通过过滤掉这些类别来冒充整个连接链路已治理。
- 业务来源继承的 RequestPath / 任意 scope 尚未全局清理；其他 Hosting / Lifetime、HTTPS / 证书、Http.Connections、协议 resolver、DiagnosticListener / EventSource / Activity / metrics 等仍是独立范围。
- 精确来源透传、未知事件 / diagnostics、异常与 payload 不可读取、EventName 错误、原事件不变、审计输出及 Generic Host EventId 11 不与 Hosting.Diagnostics 11 混同均有回归。既有审计失败传播与配置 reload 回归一并通过。
- L2 未整体完成，生产 `RadishLogging.Enabled=false` 保持；没有安装依赖、启动项目服务 / 容器、推送或发布。

## 验证结果

- 新旧 HTTP、SignalR、公共适配及生成入口定向回归 **319 / 319** 通过，无跳过，构建 `TreatWarningsAsErrors=true`。
- Node 公共日志契约 **27 / 27** 通过；Docs、changed-only 文本卫生与 `git diff --check` 通过。
- 文本检查保留日志手册与记录索引的软篇幅提醒，未超过硬上限。
- 本机测试宿主与 WebApplication 构建的沙盒限制沿用已确认情况，获准在沙盒外运行定向测试；不因此增加项目启动或生产操作范围。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -p:TreatWarningsAsErrors=true --filter 'FullyQualifiedName~OuterHttp|FullyQualifiedName~KestrelHttp|FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~SignalRFramework|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQuery' --verbosity minimal
npm run check:logging-contract
npm run check:docs
npm run check:repo-hygiene:changed
git diff --check
```
