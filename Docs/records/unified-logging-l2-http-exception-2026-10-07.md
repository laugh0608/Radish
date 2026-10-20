# L2 HTTP 异常处理中间件安全输出

日期：2026-10-07（Asia/Shanghai）。本批按项目所有者确认的[边界核对与方案](./unified-logging-l2-http-boundary-plan-2026-10-07.md)实施，承接 SignalR Core 三类来源治理。

## 实现与所有权

- `ApiExceptionHandler` 的两处 `WriteFailure` 移至 `ApiErrorResultFactory.WriteAsync` 成功返回之后。普通异常 / 业务 5xx 成功写入后仍生成一次 `http.failed`；业务 4xx 继续安静。构造或写入失败不先声明本层处理完成，继续交给框架；这不证明客户端已经收到响应。
- `HttpExceptionFrameworkLogSink` 精确匹配 `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`。适配位于上下文合并后、旧链代码与配置 WriteTo / AuditTo、候选 RuntimeSerilogSink 之前，与原 SignalR 适配串联；相似前缀及其他来源原样传递。
- 重建事件，不渲染原模板、不读取异常 Message / ToString、不复制任意属性。保留原时间、等级、可用类型化 trace/span、固定 SourceContext、受控 EventCode / SourceCategory / Diagnostic 和存在异常时的 failureKind。
- 目标来源未知 Warning / Error 保留安全摘要。低级别事件继续服从最低等级与 diagnostics，不升级取消为 Error。配置 sink、审计异常传播、SQL 筛选与 SignalR 接线继续沿用上一批边界；不承诺自定义输出插件自身不会添加数据。

## 框架事件映射

| EventId / Name | 受控事件 | 原等级 | 含义 |
| --- | --- | --- | --- |
| 1 / UnhandledException | `http.framework_exception` | Error | 框架异常报告；Gateway 错误页重执行成功也可生成，不能断言最终未处理 |
| 2 / ResponseStarted | `http.response_started` | Warning | 响应已开始，异常处理器无法重写响应 |
| 3 / Exception | `http.exception_handler_failed` | Error | 异常处理委托自身抛错，包括非 API 委托拒绝处理后重抛 |
| 4 / RequestAborted | `http.request_aborted` | Debug | 原异常为取消 / IO 且请求 token 已取消；框架事件没有附带异常 |
| 其他目标事件 | `http.framework_unclassified` | 保留原等级 | 未识别事件仍安全可见 |

本机 ASP.NET Core 10.0.8 的 LoggerMessage 元数据与真实管道共同验证映射。`ResponseStartedErrorHandler`、`ErrorHandlerException`、`RequestAbortedException` 是方法名，不是对应事件 Name，不能直接混用。

## 保留的运行行为

- MessageModel 全字段、状态映射、参数规范化、correlation header、正常 404 与 `AllowStatusCode404Response=true` 保持。
- 响应已经开始时不改原正文 / 状态 / header，框架保留 Warning 与原异常 Error，并继续抛原异常。
- 原异常为 IOException / OperationCanceledException 且 RequestAborted 已取消时，框架先于 HasStarted 判断按取消处理；未开始设 499，已开始保留原状态，不重抛。普通异常即使 token 已取消仍按原失败路径传播。
- 错误响应构造失败、写入前失败、部分写入后失败与写入过程中取消，保留处理委托失败及原异常报告两个事件，最终重抛原始请求异常；不按一次请求强行合并不同阶段。
- 非 API / health 仍由框架先清理响应并设 500，然后自定义委托拒绝处理并重抛；不声称这些路径绕过中间件。该情况下两个框架阶段可能都引用同一原异常，未改变框架传播或做全局去重。
- Gateway 样式 `/Error` 重执行的路径恢复、状态、正文和原异常传播保持。共享适配适用于接入相同日志出口的宿主，但 API 自有事件时机调整只发生在 API。

## 验证与限制

- 新增 50 项测试，覆盖旧 / 候选 × Development / Production；真实 ASP.NET 异常中间件在内存执行，不替换 JSON 序列化、不监听端口。
- 写入屏障验证事件在写入任务完成前不存在；覆盖普通异常 / 503。故障矩阵覆盖普通异常 / 404 / 503 × 写入前 / 部分写入 / 写入时取消；检查原异常实例、原状态、部分正文、事件顺序和安全 failureKind。
- 构造失败使用 Message getter 抛错的 BusinessException；响应已开始及取消判断使用不可读取 Message / ToString 的异常。用不读取异常文本的实例断言，避免 xUnit Throws 读取 Message 干扰边界验证。
- 取消矩阵交叉覆盖 IO / 取消 / 其他异常、token 是否取消及 HasStarted；非 API 多路径检查现有清理与传播。Gateway 样式管道覆盖错误页完成和错误页写入前 / 部分输出后失败。
- 适配单元覆盖精确来源、EventId / Name 同时匹配、未知 Warning / Error、diagnostics 关闭、不可渲染 payload、时间 / 等级 / trace 保留、原事件不变，以及配置 AuditTo。实际 ConfigureLegacy 的配置 WriteTo 用于四模式管道；既有 SignalR 配置 reload、释放与审计 sink 抛错回归一并通过。
- 既有 API 404 完整响应与 400 / 403 / 409 / 429 / 500 / 503 回归已同步为新框架事件断言。API 错误契约、标签消费、生成入口、SignalR 和公共日志策略回归共同通过。
- HasStarted 使用受控 feature；Gateway 为相同 UseExceptionHandler 重执行机制的内存管道，不是完整网关进程验收。未执行真实网络中断、Kestrel、认证 / MVC、WebSocket、日志库入库或文件轮转验收。
- Kestrel、Hosting、DeveloperExceptionPage、Http.Connections、协议 resolver 等来源继续独立治理。DiagnosticListener、Activity / metrics 等非 Serilog 通道不在适配范围。L2 未整体完成，`RadishLogging.Enabled=false` 保持。

## 验证结果

- 定向 .NET 回归 **212 / 212** 通过，无跳过；构建使用 `TreatWarningsAsErrors=true`。
- Node 共用日志契约 **27 / 27** 通过；Docs、changed-only 文本卫生及 `git diff --check` 通过。
- 文档检查仅有日志手册与记录索引既有软篇幅提醒，未超过硬上限。
- 测试宿主所需本机通信在获准的沙盒外运行；未安装依赖、启动项目服务 / 容器、访问生产、推送或发布。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -p:TreatWarningsAsErrors=true --filter 'FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~SignalRFramework|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQuery' --verbosity minimal
npm run check:logging-contract
npm run check:docs
npm run check:repo-hygiene:changed
git diff --check
```
