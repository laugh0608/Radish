# L2 外层 HTTP 框架来源核对与实施方案

日期：2026-10-07（Asia/Shanghai）。状态：项目所有者已确认并完成实施，结果见[外层 HTTP 三来源治理记录](./unified-logging-l2-outer-http-2026-10-07.md)。下文保留实施前核对与方案，不作为当前待办。

## 范围与事实

承接 [HTTP 异常处理中间件治理](./unified-logging-l2-http-exception-2026-10-07.md)。本次检查 API / Auth / Gateway 的管道、共享日志出口和本机 ASP.NET Core 10.0.8，区分以下三个精确来源：

| 来源 | 当前职责与发现 | 拟治理边界 |
| --- | --- | --- |
| `Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware` | 开发异常页报告原异常，再处理已开始响应或生成错误页；生成失败时再报告该阶段故障，最终传播原异常 | 安全化开发异常页日志，保留既有页面及阶段顺序 |
| `Microsoft.AspNetCore.Hosting.Diagnostics` | 请求开始 / 完成 / 到达管道末尾，以及部分宿主启动和配置事件；请求结束的 DiagnosticListener 异常通知不等于 ILogger Error | 区分请求诊断、启动故障与配置警告，安全化该来源输出 |
| `Microsoft.AspNetCore.Server.Kestrel` | 主来源包括应用异常、响应回调失败、请求取消及若干服务警告；连接、BadRequests、HTTP/2、HTTP/3 有各自独立类别 | 只适配主来源，保留后续传输类别边界 |

### 开发异常页并非只有显式注册

[WebApplicationBuilder 源码](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/DefaultBuilder/src/WebApplicationBuilder.cs)在 Development 的宿主组装阶段自动添加开发异常页。API / Auth 没有显式调用也会有外层包装；Gateway Development 另有显式注册。此前直接调用 `IApplicationBuilder.Build()` 的内存测试不包含这个宿主自动包装，不等于生产或开发完整宿主验证。

[开发异常页源码](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddlewareImpl.cs)保留以下顺序：IO / 取消异常且请求 token 已取消时按取消处理；其他异常先报告，再判断响应是否开始或生成页面。文本 / HTML / ProblemDetails 本身会读取异常与请求详情，这是现有开发响应功能；本批只治理日志，不把页面内容安全化或改变启用条件。

### Hosting 与 Kestrel 不承担同一种最终记录

[Hosting 请求诊断](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs)的请求开始 / 完成是 Information；异常还会进入 DiagnosticListener、EventSource 或 metrics。不能据此假定存在一个 Hosting ILogger 的请求异常 Error，也不能把正常未匹配路由的 RequestUnhandled 当成异常。

[GenericWebHostService](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Hosting/Hosting/src/GenericHost/GenericWebHostService.cs)使用同一 Hosting.Diagnostics 类别报告管道构建失败；配置捕获启动错误时可以构造错误页应用继续运行。此类事件应表达阶段故障，不能命名为进程必然退出。`Microsoft.Extensions.Hosting.Internal.Host` 是另一来源，现有 RuntimeProcess 对其 EventId 11 的启动失败移交保持不变。

[Kestrel HttpProtocol](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Http/HttpProtocol.cs)既记录应用调用失败，也记录 OnStarting、OnCompleted 和请求体收尾失败。OnCompleted 单个回调失败后继续处理其他回调；同一事件不能只解释为业务入口抛错。中间件重抛后外层记录可以与前一阶段同时存在，本批不做按请求或异常实例的全局去重。

[KestrelTrace 类别划分](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.cs)明确区分主来源与 `.BadRequests / .Connections / .Http2 / .Http3`；HTTPS / 证书等另有来源，不做前缀兜底适配。

## 元数据与探针证据

使用本机已安装的 .NET / ASP.NET Core 10.0.8 引用编译临时探针，无新增包。探针通过真实 WebApplicationBuilder 的 ConfigureApplication 组装外层，再由 HostingApplication 创建 / 执行 / 释放内存请求；没有调用宿主 Start / Run，也没有监听端口。

- Development 自动产生开发异常页 `1 / UnhandledException / Error`，请求完成为 500；Hosting 前后分别产生 `1 / Name=null / Information` 与 `2 / Name=null / Information`。
- Production 对照保留向调用方传播；Hosting 前后仍只有请求起止事件。反射调用同步失败表现为 TargetInvocationException 包装，不将其当作框架修改了业务异常。该探针没有 Kestrel，所以不以它的未提交响应状态推断实际服务器响应。
- 直接调用真实 KestrelTrace.ApplicationError，确认类别为 Kestrel 主来源，`13 / ApplicationError / Error`；这是日志生成方法证据，不是实际协议循环触发证据。
- 真实 HostingLoggerExtensions 生成的管道启动异常 `6 / Name=null / Critical`、Hosting startup assembly 异常 `11 / Name=null / Critical`、绑定配置覆盖 `15 / Name=null / Warning` 均已核对。Hosting 常量还包括 RequestUnhandled=16，实际分支回归留待实施。
- LoggerMessage 元数据核对开发页 `2 / ResponseStarted / Warning`、`3 / DisplayErrorPageException / Error`，共用 `4 / RequestAborted / Debug`。Kestrel 主来源的 ApplicationError=13、HeartbeatSlow=22、ApplicationNeverCompleted=23、InvalidResponseHeaderRemoved=41、Http2DisabledWithHttp1AndNoTls=64、Http3DisabledWithHttp1AndNoTls=65、RequestAborted=66、RequestBodyDrainBodyReaderInvalidState=67 已确认。
- 临时捕获器只输出来源、事件编号 / 名称、等级和是否附带异常；不输出异常原文或请求数据。沙盒内探针卡在 builder 创建，精确终止后获准在沙盒外完成；全部探针进程已结束。

这些结果是本轮核对证据，不代替下一批四模式适配回归或真实网络验收。

## 建议实施范围

1. 复用现有上下文合并后、sink 输出前的位置，增加三个精确来源的安全适配；覆盖旧代码 / 配置 WriteTo / AuditTo 与候选链。保持非目标来源透传、原等级 / 时间 / 类型化 trace、固定来源和受控 failureKind；不读取原始消息或复制任意 scope。
2. 按来源 + EventId + 实际 Name（Hosting 的 null 也须精确匹配）区分开发异常页、服务器应用 / 回调失败、取消、请求诊断、宿主阶段故障与已知服务警告。未知目标事件保留受控摘要；Warning / Error / Critical 不被丢弃，过程事件继续受 diagnostics / 最低等级约束。事件编码在实现时同步公共契约。
3. 保留自动 / 显式开发异常页、API / Gateway 错误处理器、请求响应和所有异常传播；不删除中间件，不修改开发页正文，不新增 catch 消费，不把同一请求所有日志合并为一个事件。
4. 保持 RuntimeProcess 当前启动失败移交规则，不按编号 11 跨类别扩大过滤；Hosting 阶段故障与进程最终失败可以分别保留。生产候选开关不变，不新增配置依赖。

## 验证矩阵

- 旧 / 候选 × Development / Production；开发页额外覆盖显式启用与宿主自动包装，Production 对照确认不会凭适配器启用开发页。
- 正常成功、普通未匹配 404、API 已处理错误、API / 非 API 向外传播、开发页正常文本 / HTML 与写入失败、响应已开始、IO / 取消 token 交叉矩阵。页面正文按现有契约断言，日志单独检查敏感哨兵。
- Hosting 使用真实请求生命周期，检查请求起止 / 未匹配事件、无额外请求 Error、原异常传播，以及 scope / Activity 的释放；启动类事件使用真实生成方法与元数据，不冒充完整启动故障验收。
- Kestrel 使用内存双向 Pipe / 可控 transport 的真实 HTTP/1 请求处理路径，覆盖应用抛错、OnStarting / OnCompleted 故障、后续回调继续和取消；不监听端口、不启动项目服务。可复用现有框架引用，测试内部边界按运行时版本约束，不新增测试包；若真实分支无法覆盖，明确留作缺口，不用手工 LogError 代替。
- 已知告警映射、未知事件、缺失 / 错误 EventName、不可读取异常与属性、原事件不变、审计传播、配置 sink、非目标 Kestrel 子类别和已有 HTTP / SignalR 接线回归。
- 执行定向 .NET（warnings as errors）、公共日志契约、Docs、文本卫生与 diff 检查，不安装依赖、不启动服务 / 容器、不访问生产。

保留后续范围：Kestrel 子类别及实际网络 / TLS、Http.Connections、协议 resolver、其他 Hosting / Lifetime 来源、非 Serilog 诊断通道，以及既有业务日志继承任意 scope 的全局治理。三个来源适配不等于所有请求日志已安全，L2 未整体完成。
