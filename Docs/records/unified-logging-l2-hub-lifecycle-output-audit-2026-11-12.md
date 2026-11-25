# Hub 自有生命周期最终日志输出核验与实施

日期为 2026-11-12（Asia/Shanghai），基线为 `dev` 的 `e613bbb7`。先完成核验并形成[第 10.12 节方案](../features/unified-logging-l3-ingest-plan.md#l2-hub-lifecycle-output)，随后所有者批准并完成[本地实施](#implementation)。

## 实际入口

| 来源 | 真实方法与验证行为 |
| --- | --- |
| `ChatHub.OnDisconnectedAsync` | presence 先清理，退组等待期间安静，退组成功后输出 `chat.connection_closed`。presence 或退组失败继续传播原异常，正常断开安静 |
| `NotificationHub.OnDisconnectedAsync` | 正用户 ID 在退组成功后输出 `notification.connection_closed`，非正 ID 跳过退组，传入异常时仍输出。退组失败原样传播，正常连接和断开安静 |

真实 Hub 与已有 mock SignalR fixture 复用。旧链调用 `SerilogSetup.ConfigureLegacy`，分别通过配置 WriteTo / AuditTo 捕获最终 `LogEvent`，候选通过 `RuntimeLoggingConfiguration.Configure` 捕获 JSONL。Development / Production 各覆盖干净上下文及 Serilog LogContext / MEL scope 的字段污染、W3C Activity 和诊断标记。注入值均为合成数据，包含与 `signalr.disconnect_failed` 对应的框架来源、事件身份和方法属性。

## 复现结果

| 场景 | 实际结果 |
| --- | --- |
| 干净上下文及框架身份冲突 | 120 条实际输出均保留正确事件码、Warning 和失败分类，旧链保留两个类型化来源 |
| 旧链字段污染 | 两条正式路径均输出合成 `ConnectionId`、私有载荷、`HubMethod`、无关状态 / 次数和普通关联字符串，共 72 条输出继承私有载荷。即使存在真实 Activity，属性中仍保留外层同名 trace / span 字符串 |
| 候选字段污染 | JSONL 裁掉连接 ID 和私有载荷，仍输出无关 `statusCode`、`attempt` 和外层 `operationId`。无 Activity 时接受字符串 trace / span，有 Activity 时使用真实类型化关联 |
| 外层 `Diagnostic=true` | 正式旧链和 Development 候选误标为诊断。Production 候选对两种 scope 各丢弃三次触发，覆盖两个事件码。退组及 presence 行为断言继续通过 |

## 修复前输出故障与验证

- 12 项输出故障探针覆盖两个来源、两环境和三条路径。WriteTo 消费 sink 故障，候选 writer 输出有界 `pipeline.output_failed`。AuditTo 在已完成各自清理后抛出包含同一 sink 异常实例的 AggregateException。两条自有日志在源码中均位于清理之后、`base.OnDisconnectedAsync` 之前。
- 严格构建通过，0 警告、0 错误。沙盒 54 项探针全部通过，包含 42 组实际方法与正式出口矩阵和 12 项输出故障。126 次事件观测含 6 次生产候选漏记。探针通过表示复现符合断言。
- 同一程序集在沙盒通过 189 项关联回归，0 失败、0 跳过。范围含三个 Hub 的自有日志、聊天身份和在线状态、通知初始化、SignalR 框架及邻接通知与实时推送适配。70 项日志契约检查通过。
- 探针源码与合成报告归入本机临时目录。真 WebSocket、真实日志介质和 collector 仍由各自批次验证。

<a id="implementation"></a>

## 本地实施

- `ChatHub` 和 `NotificationHub` 各使用一个固定的命名 EventId。复用 `NotificationRealtimeLogSink` 的字段重建逻辑，由现有旧链代码 / 配置 WriteTo、配置 AuditTo 和候选 JSONL 处理两个精确 Hub 来源。事件保持原码和 Warning，分类为 `application`、非诊断。扩展字段仅允许封闭 `failureKind`，保留类型化 W3C 关联。见[事件契约第 87 节](../features/unified-logging-contract.md#l2-hub-lifecycle-final-output)。
- 两组既有 Hub 日志测试改用正式配置，覆盖清理顺序、分组、身份与凭据、正常生命周期和异常传播。新增矩阵从真实断开方法触发，验证两种 scope 和 Activity、清理等待、清理失败、通知 Hub 非正用户 ID 的断开分支、错误身份 / 等级、非法字段及代码 console。SignalR 框架及 CommentHub 继续通过关联回归。
- 输出故障回归确认 WriteTo 消费故障，候选 writer 产生有界 `pipeline.output_failed`，AuditTo 在清理后传播原 sink 异常。未将 mock 清理断言作为真实连接关闭证据。

### 实施验证

- 严格构建通过，0 警告、0 错误。沙盒 255 项关联回归全部通过，0 失败、0 跳过。含新增 46 项，其中 24 项覆盖真实断开方法与三条正式输出路径，12 项覆盖清理后的输出故障，10 项覆盖身份、字段、诊断及代码 console。
- 70 项日志契约检查、`npm run check:repo-quality:changed` 和 `git diff --check` 通过。事件契约与记录索引保留非阻断篇幅提醒。

定向复跑入口：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~HubLifecycleOutputLoggingTests|FullyQualifiedName~ChatHubLoggingTests|FullyQualifiedName~NotificationHubLoggingTests|FullyQualifiedName~CommentHubLoggingTests|FullyQualifiedName~SignalRFramework'
npm run check:logging-contract
```

真实 WebSocket、日志介质与 collector 随相应批次验收。生产候选开关保持关闭。
