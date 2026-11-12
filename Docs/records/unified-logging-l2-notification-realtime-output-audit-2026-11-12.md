# 通知与实时推送最终日志输出核验与实施

日期为 2026-11-12（Asia/Shanghai），基线为 `dev` 的 `818ba299`。先完成核验并形成[第 10.11 节方案](../features/unified-logging-l3-ingest-plan.md#l2-notification-realtime-output)，随后所有者批准并完成[本地实施](#implementation)。

## 实际入口与观察点

| 来源 | 真实方法与验证行为 |
| --- | --- |
| `UserFollowService` | 真实 Controller 和可靠 Outbox 服务触发入队异常。关系已提交的 fixture 状态及原 400 响应保持，重复关注安静 |
| `NotificationPushService` | SQLite Outbox → 执行 Job → 通知创建 → 推送。分别触发分组访问、revision 事件和兼容角标失败，均保持任务成功、尝试计数为 0、首个发送失败后跳过角标 |
| `UserInteractionRealtimeNotifier` | 两个接收者的 Chat、Notification 四次发送依次失败，每次独立输出。保持顺序并继续尝试 |
| `CommentRealtimePushService` | 真实 CommentController 创建成功，创建与高亮事件均发送失败，保持 200 和两次推送尝试 |

源码另核对到通知推送也由收件箱变更、屏蔽读取屏障和关系屏蔽任务共用。轻回应的事件由已收口 Controller 消费。Hub 生命周期与 SignalR 框架输出有独立范围。

## 正式出口证据

临时探针复用既有 fixture。旧链调用 `SerilogSetup.ConfigureLegacy` 并分别通过配置 WriteTo / AuditTo 捕获最终 `LogEvent`，候选通过 `RuntimeLoggingConfiguration.Configure` 捕获 JSONL。两种宿主环境和三条路径各运行 clean，以及 Serilog LogContext / MEL scope 各自的字段污染、带 W3C Activity 的字段污染与 `Diagnostic=true`。污染输入使用合成值，含相邻来源、冲突事件身份、私有载荷及格式合法的结构化字段。

| 场景 | 实际结果 |
| --- | --- |
| 干净及来源冲突 | 400 条实际输出均保留生成点的事件码、等级和失败分类，旧链始终保留四个类型化来源。现有身份由类型化 logger 和内层 scope 提供 |
| 旧链字段污染 | 两环境的 WriteTo / AuditTo 均保留外层私有载荷、`statusCode`、`attempt` 和字符串关联值，共 240 条带私有载荷。真实 W3C Activity 存在时，事件关联正确，同时结构化属性仍带外层同名字符串 |
| 候选字段污染 | JSONL 裁掉私有载荷，继续接收与五个事件无关的 `statusCode`、`attempt`、`operationId`。无 Activity 时使用外层字符串 trace / span，有 Activity 时使用真实 W3C trace / span |
| 诊断误判 | 旧链与 Development 候选保留事件并误标诊断。Production 候选对两种 scope 各丢弃 10 次实际触发，覆盖五个事件码，业务断言继续通过 |

## 输出故障

24 项故障探针覆盖每个来源、两环境和三条输出路径。WriteTo 消费 sink 错误，候选 writer 产生有界 `pipeline.output_failed`，业务返回和后续推送保持。AuditTo 传播含原 sink 异常的 AggregateException，探针观察到以下行为。

- 关注关系已完成 fixture 写入，入队失败后的原 404 响应被输出异常中断。
- 通知创建已调用入箱持久化，输出异常阻止正常返回通知 ID。
- 双 Hub 推送在第一处失败后中断，剩余 Hub 和接收者未尝试。
- 评论创建写入调用已完成，首次推送失败后的输出异常阻止高亮尝试和正常响应。

## 修复前验证与边界

- 严格构建通过，0 警告、0 错误。沙盒通过 66 项探针，包含 42 组实际方法与正式出口矩阵、24 项输出故障。420 次事件观测含 20 次生产候选丢失。探针通过表示复现符合断言。
- 既有关联回归在沙盒于 `PostQuickReplyLoggingTests.ErrorResultConstructionFailure_ShouldLeaveOnlyApiOwner` 无进展 30 秒后中止，保留 sequence，未采集转储。宿主用相同程序集复跑通过 231 项，0 失败、0 跳过，含四组日志测试、相关 Service / Controller、轻回应最终消费和 Outbox。沙盒等待原因未确定。
- 70 项日志契约测试、`npm run check:repo-quality:changed` 及 `git diff --check` 通过。
- 探针源码与合成报告归入本机临时目录。
- 本轮使用替身 SignalR / 收件箱、内存 SQLite Outbox、受控输出捕获器。真实 WebSocket、数据库事务、文件 / DB sink、collector 与部署验证随对应批次。

<a id="implementation"></a>

## 本地实施

- 四个类型化 logger 改用五个命名 EventId。`NotificationRealtimeLogSink` 接入旧链代码 / 配置 WriteTo、配置 AuditTo 和候选 JSONL，按来源、数值、名称及等级重建事件。原事件码和等级保持，分类统一为 `application`，登记事件固定为非诊断。正式字段契约见[第 86 节](../features/unified-logging-contract.md#l2-notification-realtime-final-output)。
- 四组既有日志测试改接正式日志配置，覆盖关注原 400 / 404 与 API 异常归属、入箱、Outbox 重试 / 死信、屏蔽任务和评论响应。新增结构化矩阵检查两种 scope、无 Activity 和类型化 W3C Activity、原 Serilog 时间、错误身份及等级、非法分类、代码 console 和候选诊断关闭。
- 输出故障回归验证 WriteTo 消费 sink 故障、候选 writer 走有界 `pipeline.output_failed`。AuditTo 仍传播原 sink 故障，可在关系已写入、入箱持久化已调用、双 Hub 第一处发送失败或评论创建已完成后中断原响应和后续调用。

### 实施验证

- 严格构建通过，0 警告、0 错误。沙盒 58 项定向测试通过，包含 24 项实际入口及正式出口矩阵、24 项输出故障和 10 项身份 / 字段 / 诊断及 console 用例。
- 完整关联回归在沙盒再次于既有 `PostQuickReplyLoggingTests.ErrorResultConstructionFailure_ShouldLeaveOnlyApiOwner` 用例处 30 秒无进展后中止，保留 sequence，未采集转储。相同程序集在宿主完成 384 项回归，0 失败、0 跳过，含本批 58 项、关注 / 通知 / 关系 / 评论及其直接消费者、轻回应、Outbox、SignalR 框架和相邻日志适配。沙盒等待原因未确定。
- 70 项日志契约测试、`npm run check:repo-quality:changed` 和 `git diff --check` 通过。契约与记录索引仍有非阻断篇幅提醒。

定向复跑入口：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~NotificationRealtimeOutputLoggingTests'
npm run check:logging-contract
```

真实 WebSocket、外部缓存、文件 / DB sink 与 collector 随相应批次验收。生产候选开关保持关闭。
