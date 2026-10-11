# L2 宿主健康服务日志治理（2026-10-11）

本批按项目所有者确认范围，治理 1 个精确来源、6 个已知事件身份和 1 个未知摘要，共 7 个公共编码；同时增加 Serilog 桥接前的定向保护。保留健康检查、报告、响应和取消语义。生产 `RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 调用关系与版本

- API / Auth / Gateway 均通过 AddHealthChecks 使用 `DefaultHealthCheckService`。API 与 Auth 的 self 属于 minimal，issuer / 证书检查属于 extended；Gateway 的 api-service / auth-service 为 minimal、失败状态 Unhealthy，console-service 为 extended、失败状态 Degraded。
- 本机健康服务与健康中间件为 ASP.NET Core `10.0.8`，桥接为 `Serilog.Extensions.Logging` / `Serilog.Extensions.Hosting` `9.0.0`，Gateway 使用 `AspNetCore.HealthChecks.Uris` `9.0.0`。UriHealthCheck 自身不记录 ILogger 日志，它使用的三个命名 HttpClient 已纳入先前出站 HTTP 适配。
- 当前未找到产品注册 IHealthCheckPublisher；HealthCheckMiddleware 本身没有 ILogger 来源。不扩入 Publisher、OpenIddict、MVC / Auth 自有日志或非 Serilog 通道。
- 依据已安装程序集的真实 LoggerMessage 元数据、生成器与行为验证；工厂注册逻辑参照 [Serilog 9.0.0 服务注册源码](https://raw.githubusercontent.com/serilog/serilog-extensions-hosting/v9.0.0/src/Serilog.Extensions.Hosting/SerilogServiceCollectionExtensions.cs)，响应边界参照[健康中间件源码](https://source.dot.net/Microsoft.AspNetCore.Diagnostics.HealthChecks/HealthCheckMiddleware.cs.html)。

## 精确来源与编码

唯一来源为 `Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService`；不按类别前缀或模板文本猜测身份。

| ID / 实际 Name | 原等级 | 公共编码 |
| --- | --- | --- |
| `100 / HealthCheckProcessingBegin` | Debug | `health.run_started` |
| `101 / HealthCheckProcessingEnd` | Debug | `health.run_completed` |
| `102 / HealthCheckBegin` | Debug | `health.check_started` |
| `103 / HealthCheckEnd` | Healthy 为 Debug，Degraded 为 Warning，Unhealthy 为 Error | `health.check_completed` |
| `104 / HealthCheckError` | Error | `health.check_failed` |
| `105 / HealthCheckData` | Debug | `health.check_data` |
| 未知、错名或无效身份 | 保留输入等级 | `health.unknown` |

100–104 对应 7 个 LoggerMessage 方法，103 的三种结果共用同一身份；105 为自定义状态日志方法。测试调用全部 8 种真实发出形式，不把 105 误作 LoggerMessage 生成器。

## 桥接前保护与输出适配

审计实测发现，105 的 Data 在 Serilog 绑定过程中可能先调用值的 ToString；抛出异常的值会导致桥接丢弃事件并写 SelfLog。Data 中的 SourceContext 还可能覆盖真实类别，使精确来源 sink 无法命中。仅在 sink 中删字段不能解决这一边界。

- `HealthCheckLoggerFactory` 仅包装上述精确类别的 ILogger。Log 不读取原始 state 的 Count、索引或枚举，不调用原 formatter，也不向后传递异常对象；根据实际 EventId / Name 重建固定状态，异常仅按类型分类。
- 固定状态显式写入来源、编码、SourceCategory、Diagnostic 和 failureKind（无异常时显式 null），阻止同名 scope 值补入。未知事件传递固定未知 EventId，防止缺省身份被 scope 假冒。
- `AddSerilogSetup` 的旧链与候选分支均在 UseSerilog 注册后装饰原 singleton factory descriptor，保留其创建逻辑、provider 和 logger 所有权；其他类别、BeginScope、IsEnabled、AddProvider 原样委托，包装工厂只释放内部工厂一次。不支持的工厂注册形态明确失败，不静默降级为未保护日志。
- `HealthCheckLogSink` 在上下文合并后重建安全事件，覆盖旧链代码 / 配置 WriteTo、AuditTo 与候选输出。仅保留原时间、原等级、类型化 trace/span、固定身份 / 诊断属性，以及白名单 failureKind；移除名称、描述、Data、URL、scope 与异常原文。
- Id / Name 类型错误、重复身份字段和不匹配身份均成为未知摘要，相邻来源透传。Trace / Debug / Information 保持诊断语义；候选 Production 或 Diagnostics 关闭时抑制，Warning / Error / Critical 不因未知身份隐藏，旧链等级策略不变。

## 保持的行为与保护边界

- 报告的 HealthStatus、Description、Exception、Data、Tags 和聚合逻辑保持；日志中的 check_completed 只表示返回结果，不等于 Healthy。run_completed 只表示报告聚合结束，不等于 HTTP 写入完成。
- 健康服务消费检查异常时使用既有 FailureStatus 并发出 104；非调用方 OperationCanceledException 仍进入既有超时分支。测试注入该异常，不将其宣称为真实超时计时证据。普通检查的调用方取消继续传播，不伪造结果或聚合完成。
- UriHealthCheck 自行捕获 HTTP / IO / 取消异常并返回失败结果，包括检查运行中的调用方取消，因此健康服务走 103，保留这一既有差异。
- 工厂创建、scope 释放、异常 Message getter，以及 Data.Count / 枚举可能早于或晚于受保护 Log 调用。测试确认这些失败仍在原边界传播或转换为 FailureStatus；不宣称保护了所有前置求值。
- 保留 minimal / extended 选择、空检查集合、默认 Healthy / Degraded HTTP 200 和 Unhealthy HTTP 503，以及缓存头。结构化响应仍保留 description 与 exception.Message；本批不治理响应内容。响应写入失败 / 取消仍传播，报告完成日志不代表成功响应。

## 四模式验证

四模式为旧链 / 候选链 × Development / Production；生成器另外交叉 Diagnostics 开关，候选输出逐次核对实际 mode。

- 真实生成器、105 Data、全部编码的 WriteTo / AuditTo、未知严重事件、敏感 scope、保留字段冲突、不可格式化的 state / Data / exception、原时间 / 等级 / typed trace/span 与相邻来源透传。
- 真实 HealthCheckService 的三种结果、数据 / 异常对象身份、predicate / tags、并行聚合、空集合、消费失败、调用方与非调用方取消、工厂与释放失败、前置 Message / Data 失败和重复注册名。
- 真实 HealthCheckMiddleware 与 StructuredHealthCheckResponseWriter 的状态码、头、响应内容、选择过滤及流写入失败 / 取消；真实 Gateway 注册与 UriHealthCheck 使用内存 HttpMessageHandler，核对状态码 / IO 失败、取消折叠及分组语义。
- 共享宿主注册只执行 HostBuilder.Build 和 Dispose，不 Start；验证两分支均接入保护、候选 logger 继续由 session 拥有。独立工厂测试确认创建一次、释放一次和非目标委托。旧链共享初始化仍建立原有 SelfLog 文件，未改变其生产实现。
- 健康检查和响应测试使用内存替身，不访问 DNS、网络、数据库或系统证书库，不启动项目服务或绑定端口。测试包含 HostBuilder.Build，不将其描述为“没有构建宿主”。

验证结果：

| 检查 | 结果 |
| --- | --- |
| 本批健康服务定向测试 | 63 / 63，通过；四模式及共享注册验证包含在内 |
| ErrorHandling、SignalRFramework、RuntimeLog、JWT / 授权及既有 HealthChecks 回归 | 1192 / 1192；失败、跳过、未运行均为 0 |
| Node 公共日志契约 | 27 / 27 |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 0 警告、0 错误 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；日志指南与记录索引保留既有篇幅软提醒 |
| 公共策略差异核对 | 恰好新增上述 7 个 health 编码；既有事件和其他策略字段不变 |

扩大回归工作目录为 `Radish.Api.Tests/bin/Debug/net10.0`，使用已安装 xUnit v3 的进程内入口：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.Security.ApiJwtValidationPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthorizationBoundaryTests' \
  -class 'Radish.Api.Tests.HealthChecks.*'
```

定向测试在沙盒通过。扩大回归首次在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException` 停滞，55 秒超时后终止测试进程，未计为通过；经最小提权重跑同组测试，设置 90 秒超时清理，26.374 秒完成。没有使用会绑定通信端口的 VSTest 入口，未安装或还原依赖。

## 剩余边界

OpenIddict 下一批先确认来源与范围；非 Serilog 的 Activity / Meter / EventSource / DiagnosticListener / 独立 IIdentityLogger 后置。健康响应内容、Publisher 与真实探测 / 计时均不因本批完成。没有进行真实宿主运行、OIDC 登录、浏览器、生产采集、发布或部署验收。
