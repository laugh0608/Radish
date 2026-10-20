# L2 API 已处理 404 正常完成

日期：2026-10-07（Asia/Shanghai）。本批按项目所有者批准的 404 子项方案在 `dev` 实施，承接[事件契约第 50 节](../features/unified-logging-contract.md#50-l2-api-已处理-404-正常完成)。SignalR 框架来源不属于本批实施范围，生产候选开关保持关闭。

## 问题与修正

既有 [TagController 回归](./unified-logging-l2-tag-controller-2026-10-02.md)发现：业务异常 404 经 ApiExceptionHandler 写入 JSON 后，DefaultHttpContext 的内存响应仍未标记 HasStarted；框架默认不认可异常处理委托返回的 404，遂记录原异常并包装重抛。该路径发生在 SuppressDiagnosticsCallback 判断之前，旧 sink 会收到原异常，候选 sink 只能给出未分类安全摘要。

本机使用 ASP.NET Core 10.0.8。该版本[异常中间件源码](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs)及[官方选项说明](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.exceptionhandleroptions.allowstatuscode404response?view=aspnetcore-10.0)与复现一致。

`UseApiExceptionHandler` 现显式配置 `AllowStatusCode404Response=true`，声明业务 404 是合法处理结果。原处理范围、响应序列化、错误码与消息参数、关联 ID、5xx 日志及诊断抑制逻辑不变；不增加异常捕获层，不把路由未找到改为新的兜底响应。

## 验证矩阵

新增 `ApiNotFoundPipelineLoggingTests` 五组四模式用例（旧 / 候选 × Development / Production，共 20 个用例）；标签原 404 重抛用例改为校验正常完成、完整 JSON 与安静日志。

| 场景 | 结果 |
| --- | --- |
| 业务 400 / 403 / 404 / 409 / 429 | 正常完成，响应未开始标记下也不重抛；两种日志路径均安静 |
| 业务 500 / 503 | 正常完成，单条安全 `http.failed`，保留 statusCode / failureKind |
| 完整响应 | 状态、IsSuccess、消息、错误码、消息键、标量消息参数与 TraceId / `X-Correlation-ID` 保持；不支持的参数仍规范化为 `[unsupported]` |
| 普通 404 | API、Hub 外观路径与普通路径上的非异常响应保留原状态和正文，不附加业务错误信封 |
| 响应已开始 | 通过 IHttpResponseFeature 模拟开始状态；原异常实例继续传播，已有 206 状态及正文不被改写 |
| 响应写入失败 | 404 / 503 JSON 写入时注入 IOException，仍传播原 BusinessException，未把失败的响应误判成成功处理 |
| 非 API / health | `/hub/chat`、`/hub/comment`、`/connect/authorize`、`/api/health`、`/api/health/live`、`/healthz`、`/scalar` 保留原异常传播，不生成 MessageModel 正文 |
| 标签消费者 | Controller 仍原样传播 BusinessException；正式 API 内存管道完成其 404，保留自定义错误码 / 消息键，不再产生框架错误日志 |

已开始响应、响应写入失败与非 API 请求仍触达框架日志；旧路径的异常原文与候选 `runtime.unclassified` 未在本批治理。测试只确认其传播边界和候选内容裁剪，不宣称这些分支已满足全链路单次安全输出。

## 本地验证

```bash
dotnet test Radish.Api.Tests --no-restore --filter 'FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests' --logger 'console;verbosity=minimal' -p:TreatWarningsAsErrors=true
```

结果：70 通过，0 失败，0 跳过；编译启用警告视为错误。首次沙盒执行受 MSBuild 本地通信管道权限限制，提权后取得真实结果。

`npm run check:docs`、`npm run check:repo-hygiene:staged` 与 `git diff --check` 通过；卫生检查覆盖 9 个文件。logging 手册既有 768 行、records 索引增至 312 行触发建议篇幅提醒，均未达到硬上限，本批未扩大为文档拆分。证据为内存 ASP.NET Core 管道与现有标签 SQLite 回归，不代表真实 Kestrel、网络、MVC 全流程、WebSocket 或部署验收；未安装依赖、启动项目服务 / 容器、访问生产或切换日志链。

## 后续边界

继续核对 SignalR 最终处理，以及响应已开始、错误响应写入失败和非 API 传播时的框架日志归属。早期轻回应 / 评论 / 帖子消费点的响应再次失败仍独立待核对；本批不关闭 L2 整体。
