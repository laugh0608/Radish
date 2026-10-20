# L2 Kestrel 连接与坏请求日志核对及实施方案

日期：2026-10-07（Asia/Shanghai）。状态：项目所有者已确认并完成实施，结果见[本批治理记录](./unified-logging-l2-kestrel-connections-2026-10-07.md)。下文保留实施前核对与方案，不作为当前待办。承接[外层 HTTP 三来源治理](./unified-logging-l2-outer-http-2026-10-07.md)，核对阶段没有修改运行时代码或启动项目服务。

## 核对结论

下一批限定两个精确来源：`Microsoft.AspNetCore.Server.Kestrel.Connections` 与 `Microsoft.AspNetCore.Server.Kestrel.BadRequests`。它们仍在上一批适配之外；同属 Kestrel 不代表可以按前缀合并处理。

### 事件身份与等级

下表依据 ASP.NET Core 10.0.8 官方源码和本机已安装框架的 `LoggerMessageAttribute`，不是按日志文本猜测。ID 与 Name 必须共同匹配。

| 来源 | ID / Name | 原等级与含义 |
| --- | --- | --- |
| Connections | 1 ConnectionStart；2 ConnectionStop；39 ConnectionAccepted | Debug，连接过程 |
| Connections | 4 ConnectionPause；5 ConnectionResume；9 ConnectionKeepAlive；10 ConnectionDisconnect | Debug，连接状态 |
| Connections | 16 NotAllConnectionsClosedGracefully；21 NotAllConnectionsAborted | Debug，关闭阶段诊断，不升级为 Error |
| Connections | 24 ConnectionRejected | Warning，并发连接限制拒绝 |
| Connections | 34 ApplicationAbortedConnection | Information，应用主动终止连接 |
| BadRequests | 17 ConnectionBadRequest | Debug，坏请求异常及其 Message |
| BadRequests | 20 RequestProcessingError | Debug，请求处理异常结束 |
| BadRequests | 27 RequestBodyMinimumDataRateNotSatisfied；28 ResponseMinimumDataRateNotSatisfied | Debug，请求 / 响应最低速率边界 |
| BadRequests | 54 PossibleInvalidHttpVersionDetected | Debug，疑似 HTTP 版本不匹配 |

来源：[Connections 生成方法](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.Connections.cs)、[BadRequests 生成方法](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.BadRequests.cs)。BadRequests 的五个生成方法均为 Debug；已检查的 [HTTP/1 实现](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Http/Http1Connection.cs)没有原始请求字节 Trace 日志，本批不虚构此类已知事件。

`ConnectionBadRequest` 在调用 ILogger 前读取 `ex.Message`；适配器可以清理输出，不能阻止框架此前读取 Message。不可读取异常测试应放在 sink 边界，不能用它替代真实解析器异常回归。`PossibleInvalidHttpVersionDetected` 还受主类别 Debug 启用检查约束；不为补日志改变框架过滤条件。

### 连接错误不一定属于 Connections

[KestrelTrace](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.cs)将自身 ILogger 调用转交主类别。[连接委托执行](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelConnectionOfT.cs)捕获异常时使用主类别 `0 / Name=null / Error`；[连接完成回调](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelConnection.cs)失败也使用同一身份，并继续后续回调。

这些事件已由上一批主类别的 `http.server_unclassified` 安全摘要覆盖。不能仅按 ID 0 把它们命名为某一种连接故障，也不读取原始文本来判断职责；本批只补真实分支回归。连接委托收尾依次执行完成回调、记录停止、释放 transport，再从连接管理器移除；保留现有顺序，不新增异常消费或全局去重。

### 拒绝请求与拒绝连接不同

[HttpProtocol.SetBadRequestState](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Http/HttpProtocol.cs)记录坏请求，保存拒绝异常，在响应尚未开始时设置其状态码，并关闭 keep-alive；DiagnosticSource 的坏请求通知是另一通道。解析失败可以发生在应用 / Hosting 请求上下文建立前，不能要求每个坏请求都有 Hosting 起止或请求级 Activity。

[ConnectionLimitMiddleware](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Middleware/ConnectionLimitMiddleware.cs)在达到并发限制时记录 Warning、释放连接并返回，不生成 HTTP 429。日志治理必须保留这种差异，不能顺便统一为 API 错误响应。

## 本次证据与限制

- 用已安装 .NET SDK 10.0.300 / ASP.NET Core 10.0.8 编译临时只读探针，无新增依赖。核对 11 个 Connections 与 5 个 BadRequests 的 ID、Name、等级。
- 调用真实生成方法，确认连接开始 / 停止 / 拒绝 / 应用终止，以及五个 BadRequests 事件的实际类别、身份、等级和异常附件；直接 ILogger 调用确认 KestrelTrace 的主类别 `0 / null / Error` 转发。捕获器只输出元数据，不格式化消息或异常。
- 探针已正常退出；没有初始化 Kestrel 引擎、监听网络端口或启动 API / Auth / Gateway。本轮生成方法证据不等于真实协议、并发拒绝、速率超时或连接清理验证。
- 部分官方源码网页读取失败，改用获准的只读下载。没有安装工具、改变项目配置或访问生产。

## 建议实施范围

1. 在现有上下文合并后、sink 输出前增加上述两个精确来源的适配，覆盖旧链代码 / 配置 WriteTo / AuditTo 及候选链。保留时间、等级、类型化 trace 和固定来源；清理原消息、异常详情、连接标识、请求标识与任意 scope，只按现有契约提供受控事件编码和 failureKind。
2. 对 16 个已知身份建立明确映射，未知身份使用各来源的受控摘要。Warning / Error / Critical 保持可见，Debug / Information 继续受既有 diagnostics 与最低等级约束；不提升等级，不为请求级 trace 缺失制造标识。
3. 保留协议解析、错误状态码、连接限制、取消、回调、transport 释放及所有控制流；主类别 ID 0 继续使用既有未知事件摘要。HTTP/2 / HTTP/3 / TLS 类别、Socket transport、Http.Connections、协议 resolver 及非 Serilog 通道仍在后续范围。
4. 实现时同步公共事件契约、治理专题和当前入口；生产候选开关保持关闭，L2 不因两类完成而整体关闭。

## 验证方案

- 旧 / 候选 × Development / Production 四模式；复用真实 Kestrel HTTP/1 引擎与内存 Pipe transport，仅初始化测试引擎，不注册 socket transport、不执行系统 bind、不启动项目宿主。测试结束停止 / 释放引擎、连接与管道。
- 正常请求与连续 keep-alive、坏请求行 / 头与缺少 Host、请求输入异常结束、应用主动取消：对照响应、应用调用次数、连接释放及阶段事件；解析前拒绝不强求 Hosting 起止。
- 受控并发限制拒绝：确认 Warning 在 diagnostics 关闭时仍输出，应用未执行、连接被释放、没有新增 HTTP 响应。连接委托和同步 / 异步完成回调故障：确认主类别未知摘要、后续回调继续及原有释放顺序。
- 16 个事件的映射和运行时元数据、未知编号 / 错误 Name / 非目标来源、不可读取异常及属性、原 LogEvent 不变、时间 / 等级 / trace、配置 WriteTo / AuditTo 接线；适配后的目标输出不得含敏感哨兵。
- 速率超时、关闭超时等难以确定触发的分支至少验证真实生成方法与映射，未执行的协议分支明确列为缺口，不用手工 LogError 冒充分支覆盖；错误协议探测也不等于 HTTP/2 服务验收。
- 回归上一批 HTTP / SignalR 适配，执行定向 .NET warnings-as-errors、公共日志契约、Docs、文本卫生与 diff 检查。无包安装、项目服务 / 容器启动或生产操作。
