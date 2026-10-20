# L2 HTTP 异常处理中间件边界核对与实施方案

日期：2026-10-07（Asia/Shanghai）。状态：项目所有者已确认并完成实施，结果见 [HTTP 异常处理中间件治理记录](./unified-logging-l2-http-exception-2026-10-07.md)。下文保留实施前核对与方案，不作为最新待办。

## 当前事实

本批承接 [SignalR Core 三类来源治理](./unified-logging-l2-signalr-core-2026-10-07.md)。核对范围为 `ApiExceptionHandler`、`UseApiExceptionHandler`、旧 / 候选共用日志出口、API / Gateway 管道，以及既有 API 内存回归。

框架依据为本机同版本 ASP.NET Core 10.0.8 的 [ExceptionHandlerMiddlewareImpl](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs) 与 [DiagnosticsTelemetry](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Middleware/Diagnostics/src/DiagnosticsTelemetry.cs)。本轮没有新增运行态测试；现有测试已覆盖部分传播路径，但旧链捕获器仍直接输出框架原始事件，不能视为完成治理。

| 条件 | 当前处理 / 日志 | 下一批责任边界 |
| --- | --- | --- |
| API 业务 4xx，错误响应写入完成 | 保留 MessageModel，框架诊断被抑制，自有日志安静 | 保持 |
| API 普通异常 / 业务 5xx，错误响应写入完成 | `http.failed` 在写入前生成；框架诊断被抑制 | 将该事件后移到写入任务完成后，仍只保留一个自有失败事件 |
| API 错误响应构造或写入失败 | 可能先有 `http.failed`，框架再记录处理失败及原异常，最终重抛原异常 | 自有事件不提前生成；分别保留框架处理失败和原始请求异常的安全摘要 |
| 响应已经开始 | 框架记录不能处理的 Warning 与原异常 Error，重抛原异常 | 保留状态、正文、头与原异常；安全化这两个不同含义的事件 |
| 原异常是 IOException / OperationCanceledException，且 RequestAborted 已取消 | 在 HasStarted 判断之前按取消处理；未开始时设 499，已开始时不改状态；不重抛 | 保留原取消语义及诊断等级，不升级为普通 Error |
| 非 API / `/api/health` 等路径失败 | 自定义 handler 返回 false，委托重抛；框架会先清理响应、默认设 500，再记录处理失败及原异常 | 保留现状并安全化输出；不声称这些请求绕过了异常中间件，也不把委托拒绝处理误称为响应写入失败 |
| Gateway Production `/Error` 重执行完成 | 使用同一框架来源；即使已处理，默认诊断仍可能调用 `UnhandledException` 日志方法 | 框架事件使用中性命名，不能根据这个方法名断言请求一定未处理 |

错误响应写入任务完成只表示服务端该次写入调用完成，不证明客户端已接收。`SuppressDiagnosticsCallback` 不能覆盖响应已开始、处理委托抛出等路径，也不能通过扩大抑制来代替日志治理。

## 建议实施范围

1. **API 自有事件时机**：将 `ApiExceptionHandler` 中两处 `WriteFailure` 移到各自 `ApiErrorResultFactory.WriteAsync` 成功返回后；4xx 继续安静。响应正文、状态映射、参数归一化、correlation header、404 完成选项及异常传播保持原契约。
2. **受限框架适配**：复用上一批的输出前包装位置，增加仅匹配 `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` 的适配。以来源 + 当前运行时 EventId / Name 匹配，不解析原始 Message；重建安全事件，保留等级、可用 trace/span、固定来源与受控 failureKind。
3. **中性事件语义**：框架异常报告、异常处理委托失败、响应已开始和请求取消分别使用受控事件。框架异常报告不等同于 API 自有 `http.failed`，也不等同于最终未处理；具体编码和 EventId 对照在实现时通过运行时元数据及真实管道回归固定。
4. **保留未知故障可见性**：目标来源未知 Warning / Error 仍保留安全摘要；低级别事件继续受原有最低等级 / diagnostics 控制。不复制异常、URL、header、用户输入或任意 scope，不按请求 / 异常实例全局去重。
5. **明确共用影响**：框架来源适配同时作用于 API、Gateway 及其他接入共用日志出口的宿主；仅 API 调整自有日志时机。旧 Console / File / Database、配置 WriteTo / AuditTo 与候选入口都须经过适配，上一批 SignalR 行为保持。

不替换框架中间件，不改变非 API 路由范围，不新增 catch 消费异常。非 API 当前清理响应及将同一原异常记录在两个框架阶段的行为明确保留；若后续要让协议请求完全绕过该中间件，需要单独确认响应变化及 Kestrel 等外层所有权。

## 验证矩阵

- 四模式：旧 / 候选 × Development / Production；显式检查候选 mode 与 diagnostics，不用最低级别过滤掩盖泄漏。
- 通过真实 `UseApiExceptionHandler` 内存管道覆盖原 400 / 403 / 404 / 409 / 429 / 500 / 503、普通异常 500 和普通非异常 404；验证完整 MessageModel 与日志次数。
- 错误响应构造失败、写入前失败、部分写入后失败、写入过程中取消：验证没有提前产生 `http.failed`，各框架阶段安全输出，最终仍为原始请求异常。写入阻塞期间先检查日志尚未生成，完成后才检查自有事件。
- 已开始响应：验证原正文 / 状态 / header 不变，原异常继续传播。取消分支交叉覆盖 IOException / OperationCanceledException、RequestAborted 是否取消、响应是否开始；普通异常加取消 token 不应被错误归入 499。
- 非 API / health：验证不构造 MessageModel、原异常实例继续传播，同时如实断言现有响应清理和状态行为。
- Gateway 样式的 `UseExceptionHandler("/Error")` 重执行管道：分别验证处理完成和错误页自身失败，检查路径恢复、原输出、事件语义与失败传播。使用内存管道，不启动 Gateway。
- 覆盖异常 / 模板 / scope 敏感哨兵、不可读取 Message 的异常、未知框架事件、配置 sink 和非目标来源；复跑 API 错误契约、404、生成入口、SignalR 与日志策略相关回归。
- 检查公共日志契约、Docs、文本卫生及 `git diff --check`；不安装依赖、不启动服务 / 容器、不访问生产。

## 保留范围

Kestrel、Hosting、DeveloperExceptionPage、Http.Connections、协议 resolver 及其他来源继续独立治理；没有宣称一次请求只有一个日志事件。DiagnosticListener、Activity / metrics 等非本项目 Serilog 输出通道不由这个适配器接管。生产候选开关继续关闭，L2 整体未关闭。
