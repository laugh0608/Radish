# L2 框架最终边界核对与下一批方案

日期：2026-10-07（Asia/Shanghai）。状态：本方案已获项目所有者确认；SignalR Core 子项的实施与验证见[批次记录](./unified-logging-l2-signalr-core-2026-10-07.md)。下文保留实施前的边界核对与方案。

## 核对范围与证据

- 仓库：三个 Hub、API 异常管道、Serilog 旧链与候选链及现有回归。
- 框架：与本机 `Microsoft.AspNetCore.App 10.0.8` 对齐的 [HubConnectionHandler](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/SignalR/server/Core/src/HubConnectionHandler.cs)、[DefaultHubDispatcher](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/SignalR/server/Core/src/Internal/DefaultHubDispatcher.cs) 和 [ExceptionHandlerMiddlewareImpl](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs)。
- 本记录描述源码行为；没有启动 HTTP / WebSocket 服务，没有新增框架调度运行证据。此前 Hub 方法级测试不替代框架调度测试。

## 最终边界

| 触发 | 处理与传播 | 日志所有权及缺口 |
| --- | --- | --- |
| Hub 方法抛出异常 | dispatcher 捕获并构造 invocation 错误响应；普通方法失败不进入 API 异常处理器 | 框架 `FailedInvokingHubMethod` 持有异常对象与方法名；单加 Hub filter 不能移除该出口 |
| `OnConnectedAsync` 失败 | handler 记录、尝试发送 close 后返回，不调用 Hub 断开回调 | `ErrorDispatchingHubEvent` 是该阶段的框架出口 |
| 消息解析 / dispatch 失败 | handler 记录、传入 Hub 断开回调并尝试关闭 | `ErrorProcessingRequest` 与 Chat / Notification 自有断开 Warning 可能描述同一原因的不同阶段 |
| 客户端 close 带 error | 框架将客户端文案构造为 HubException 传入断开回调 | 不能假设必有同原因的框架 Error；直接删除 Hub 自有日志可能漏记 |
| Hub 断开清理自身失败 | 框架记录 `ErrorDispatchingHubEvent` 后重新抛出 | 不应由 Hub catch 吞掉，也不能声称 handler 已最终消费；外层传输日志仍可能出现 |
| close 发送失败 | handler 有 `ErrorSendingClose` catch；底层写入还可能先消费故障 | 必须用框架调度测试区分发送、连接关闭和回调故障，不按连接简单去重 |
| API 响应已开始 | ExceptionHandlerMiddleware 跳过处理、记录并重抛 | `SuppressDiagnosticsCallback` 不覆盖这条未处理路径 |
| API 错误响应写入失败 | middleware 记录处理失败及原异常，并重抛原异常 | 当前 `http.failed` 在写响应前产生；存在自有事件与框架事件同时输出 |
| 非 MessageModel API 路径 | 自定义委托重抛；middleware 进入处理失败路径 | 不能用 API 已处理 404 的结论覆盖此路径 |

候选链把未登记框架来源转为安全的 `runtime.unclassified`，只证明候选输出有保护；旧链仍可能渲染原始异常。仅修改候选 sink，不能完成本批双链治理。

## 建议下一批：SignalR Core 框架日志适配

1. 在 `Radish.Extension.Log` 增加受限的框架事件适配，在日志上下文合并之后、旧链全部 sink / 候选输出之前重建安全事件。避免先渲染原模板、异常或任意属性；未知的非目标来源保持原行为。
2. 只匹配 SignalR Core 的 handler、dispatcher、connection context 来源；用来源与框架 EventId / Name 的组合识别已知事件。运行时回归确认准确 ID 和状态字段后固定映射，不以原始 Message 文本判断。
3. 方法执行、生命周期、消息处理及连接读写失败分别映射到受控的 `signalr.*` 事件，保留原级别和可用 trace/span。仅允许固定阶段、固定失败分类等字段；不输出异常对象、动态方法名、用户 / 连接标识、请求路径、客户端错误文案或其他 scope 属性。
4. 目标来源新增或未知的 Warning / Error 保留受控摘要并在测试中明确可见，不能因映射遗漏被静默丢弃。低级别诊断也需移除原始载荷，继续服从原有输出模式。适配后的事件不再回到原始分支重复输出。
5. 保留 Hub 自有断开事件的现有时机和等级；不声称整条连接只有一条日志。不同阶段的独立失败均保留，避免按异常实例或连接 ID 全局去重。
6. 不改变 Hub 方法、授权、分组 / presence 清理、客户端 Completion / Close 内容、`EnableDetailedErrors`、重连或异常传播。生产 `RadishLogging.Enabled=false` 保持。

接线涉及旧 / 候选共用日志出口，需要验证旧链的控制台 / 文件 / 数据库分流及配置入口均经过适配，且非目标来源、最低等级、启动失败所有权和 logger 生命周期保持原行为。现有包满足范围时不新增依赖；若发现必须变更依赖或扩大运行时范围，应先单独确认。

## 验证矩阵

- 四模式：旧 / 候选 × Development / Production；候选显式配置 mode，Development 同时验证 diagnostics。
- 使用现有框架和 `System.IO.Pipelines`，经真实 `HubConnectionHandler`、dispatcher、JSON 协议处理内存双向连接；不监听端口。
- 覆盖正常调用、普通方法异常、HubException、未知方法 / 参数绑定失败、初始化失败、消息解析失败、客户端带错误 close、断开清理失败、握手失败和写入失败。对难以触发的日志分支，单独标明映射单元证据，不能冒充真实框架分支验收。
- 核对客户端原协议结果、清理顺序、原异常传播、各阶段事件数量，以及异常 / 消息 / 参数 / scope 中敏感哨兵均不出现在输出；验证未知事件可见。
- 复跑三个 Hub 与日志适配 / 策略定向回归，执行文档、仓库文本卫生与 `git diff --check`。无需安装依赖、启动服务或容器。

## 后续独立范围

- `Microsoft.AspNetCore.Http.Connections`、Kestrel 与其他宿主层来源不随 SignalR Core 批次宣称关闭；断开清理异常继续向外传播时尤其需要后续核对。
- API 已开始响应、写入失败和非 API 路径需单独确定最终所有权；在原 `http.failed` 前移 / 后移或替换异常中间件之前再次确认方案。
- 早期 Controller 消费点的响应构造失败仍按当前规划保留。
