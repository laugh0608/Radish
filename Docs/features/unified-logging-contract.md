# 统一日志事件契约与实现进度

> 更新：2026-09-23。L1 生成契约与采集安全 / 故障可见性子项已实现。主方案见[统一日志专题](./unified-logging-governance-design.md)，首轮实测见[L1 记录](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)。新增证据见[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。L2 入口层已接入显式候选开关，见[生成入口记录](../records/unified-logging-l2-producer-entry-2026-09-19.md)；生产默认链路未切换。

## 1. 策略唯一来源

[`runtime-log-policy.v1.json`](../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json) 维护版本、服务 / 分类注册表、原始级别映射、事件说明和属性类型约束。

- .NET：作为 `Radish.Common` 嵌入资源，由 `RuntimeLogPolicy` 读取；不依赖业务层。
- Node：`Frontend/scripts/logging/runtime-event.mjs` 读取同一文件；静态服务器已接入候选输出，Dockerfile 随运行脚本复制此策略。
- 两端运行同一组 [`runtime-events.json`](../../Scripts/logging/fixtures/runtime-events.json) 样本，不各写一份脱敏规则。
- Serilog 已提供与旧 sinks 互斥的候选路径，默认尚未启用；浏览器 / Flutter logger 和数据库接收仍待迁移。旧默认行为见[日志系统](../guide/logging.md)。

## 2. 生成契约 v1 首批字段

| 字段 | 当前实现 |
| --- | --- |
| `schemaVersion` | `1` |
| `eventId` | 在生成入口创建 UUID；同一个事件对象在不同介质 / 重试间复用；独立发生生成不同 ID |
| `eventCode` | 策略注册表里的稳定类别；未知值变为 `runtime.unclassified`，不输出原值 |
| `occurredAtUtc / observedAtUtc` | 生成时的 UTC 毫秒时间；.NET 使用可注入的 `TimeProvider`；不接受载荷伪造时间 |
| `deploymentId / service / instanceId / release` | 从宿主受信配置构造 `RuntimeLogSource`；请求 / 日志载荷不能覆盖 |
| `mode` | 仅 `Development / Production`，与 `test-latest` 镜像标签无关 |
| `level / diagnostic / isFatal` | 统一三级，诊断是独立标志，Critical / Fatal 保留 `isFatal=true` |
| `sourceCategory` | 注册表内的来源分类；未知分类回到 application 并标记裁剪 |
| `messageTemplate / message` | 首批仅登记的静态安全说明；没有自由文本插值 |
| `properties` | 有限数值 / 整数范围或固定枚举；尚不接受自由文本、数组及嵌套对象 |
| `traceId / spanId / operationId` | 可选、格式严格限制；trace / span 拒绝全零值 |
| `normalizationStatus / redacted / truncated` | 未分类或裁剪行为可辨识；当前是省略未知数据，不把省略伪称字符串截断 |

`containerId` 现由采集规范化层从 Docker FullID 标签补充；instanceId 只保留与 FullID 匹配的完整 / 短 ID，否则使用受信 FullID 并标记裁剪。`requestId / jobId / tenantId` 的权威上下文、异常类型 / 安全栈帧在宿主适配时补齐。当前最终处理边界只提供固定枚举 `failureKind`；未知异常归为 other，不回显 `Exception.Message / StackTrace / Data`。不能把首批字段子集称为最终全部 schema。

## 3. 安全与模式

1. Production 拒绝启用 diagnostics，Debug / Trace / Verbose 在映射输出前丢弃；Development 也须显式打开 diagnostics。
2. Warning / Error 不自动放开载荷。普通 `message`、OAuth URL、Header、body、exception 和未登记键在生成前省略。
3. 即使键被允许，值也须通过类型和范围校验；例如 `count` 不能携带对象、`outcome` 不能携带任意字符串。
4. 生产 stdout 序列化须使用 `RuntimeLogEvent.ToJsonLine()` / `serializeRuntimeLogEvent()`；最终 UTF-8 JSON 上限 **8 KiB**，超限拒绝序列化。此上限从原建议 32 KiB 下调，防止单条应用事件越过已观察到的 Docker 长行分片边界。
5. 该限制不解决第三方容器的任意长行。采集层已选择拒收分片、把解析失败变为安全摘要；不重组敏感长行，也不保留 Docker `log` 原文。
6. .NET / Node 输出适配已将策略 / 序列化 / 写入错误转入限频应急摘要 `pipeline.output_failed`，不携带原始异常；每个输出实例至多每分钟一次，并累计失败数。应急介质也失败时不递归。

## 4. 采集协议校准结论

首轮固定候选为 Fluent Bit `5.1.2`，只取得 OrbStack / Linux arm64 证据，尚未冻结 amd64 或生产选型。

- Forward、parser、file、HTTP、filesystem buffer 路径已运行；API 模拟失败不会阻止文件写入，已持久化队列可以在 `SIGKILL` 后恢复。
- 原建议的 **200 条 / 2 MiB HTTP 硬限制不可直接使用**。插件按 chunk 发送，413 属于不可重试失败，会丢弃 HTTP 支路中的整个 chunk。
- 校准方向为：HTTP 使用有界流式大信封，内部按最多 200 条处理 / 提交；最后一次提交成功后才确认请求。部分提交后重试依赖事件唯一键。16 MiB / 32768 条的信封仅通过当前代表样本，尚需最坏情况上界及真实入库验证。
- file 轮转按 chunk 生效，`rotate_max_size` 是触发阈值，不能视为严格单片上限；总容量预算要计入每片最大超出量。
- 默认实验现使用 preflight / parser / normalize / UUID / finalize 链；原 parser-only 实验仅保留为 `--raw-transport`。两者都不是 production Compose 配置。
- 文件打开失败有丢弃指标，恢复后可继续写新事件，但不会自动补写故障事件；HTTP 队列满的旧记录丢弃同样有指标。后续查询状态必须呈现这些介质缺口。

这些是 L1 的实测修订，不改变“stdout → 独立 collector → 文件 + 内网 API → 日志库”的架构。

## 5. 验证入口与剩余门禁

无需启动业务服务：

```bash
npm run check:logging-contract
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter FullyQualifiedName~RuntimeLogPolicyTests
```

`node Scripts/logging/collector-probe.mjs` 会启动隔离容器，必须取得当前任务授权；固定镜像、端口及清理边界见脚本与 L1 记录。默认报告输出 `.tmp/logging-l1/boundary-report.json`；原始传输报告仍为 `collector-report.json`。`guarded-boundaries-observed` 仅表示本机采集边界实验通过，不是生产发布成功。

采集输入安全、API 不存在时启动、文件路径故障及队列满时丢弃可见性已取得本机证据。L2 入口与 SQL / AOP / 事务 / DbMigrate 入口、seed / 具体 migration / Auth seed 子项已落地，Outbox 与 Rust 显式输出见第 9 节，Hangfire / 清理任务见第 10 节，后台业务 Job、奖励发放与服务内清理见第 11–13 节，下一步治理剩余业务事件；L1 的正式传输上界、目标部署平台和完整磁盘故障验证仍须关闭。L3 的真实 API / SQLite / PostgreSQL 幂等入库、L4 Console 查询、L5 告警、L6 迁移仍未完成，生产链路保持不变。

## 6. L2 候选生成入口

`RadishLogging.Enabled` 默认 false；这是一项阶段性迁移开关，不是新增日志模式。API / Auth / Gateway 在配置加载完成后创建 `RuntimeLoggingSession`，引导和运行共用一个 Serilog 实例；启用时不再读取旧 Serilog sink / MinimumLevel 配置。DbMigrate 已将命令报告与诊断分开；seed / 具体 migration 已接入安全事件，其他裸输出旁路尚未收口，禁止据此提前切换生产。

| 配置 | 默认 / 约束 |
| --- | --- |
| `Enabled` | false；正式切换在 L6 统一收口 |
| `Mode` | Production；Development 只允许在 Development 宿主 / Node `NODE_ENV=development` 使用 |
| `MinimumLevel` | Info；只接受 Info / Warning / Error |
| `Diagnostics` | false；Production 下 true 为配置错误 |
| `DeploymentId / Release` | local / unversioned；部署接入须设置明确身份 |
| `InstanceId` | .NET MachineName / Node hostname；采集器再按 Docker 标签校验 |

Node 读取相同名称的 `RadishLogging__Enabled / Mode / MinimumLevel / Diagnostics / DeploymentId / Release / InstanceId` 环境变量；不会根据 test-latest 或 NODE_ENV 自动开启诊断。.NET 非敏感默认值仍放共享 appsettings，本地仅通过原有 appsettings.Local.json 覆盖。

自有调用通过结构化属性 `EventCode`、`SourceCategory`、`Diagnostic` 标明意图，数值 / 枚举属性使用策略中的原名（如 `statusCode`）。ILogger 可用 BeginScope，存量 Serilog 可用 ForContext；消息模板不承担安全契约。未登记的普通 Info 归入诊断，未知 Warning / Error 则保留安全未分类摘要，后续调用点必须逐项迁移，不能长期依靠未分类摘要。

新路径使用同一策略生成 JSONL stdout，不自建文件或数据库写入。应急事件写 stderr，仍用规范 JSON，但计数保存在进程内。静态服务器健康检查保持安静，拒绝 / 失败只记录受控 HTTP method 和 statusCode。详细验证与未关闭边界见[L2 入口记录](../records/unified-logging-l2-producer-entry-2026-09-19.md)。

## 7. L2 SQL、异常与 CLI 生成端治理

2026-09-23 本批推进 SQL / AOP / 事务、API 已处理异常与 DbMigrate Program / Runner / Doctor；[批次记录](../records/unified-logging-l2-producer-governance-2026-09-23.md)维护验证证据。

- SQL 的普通开发诊断与慢链路分开；`SqlAopLog.Enabled` 默认 false，独立慢操作 / 连接默认 true，阈值分别 1000 / 500ms。普通诊断额外要求 Development 宿主、Development 日志模式和 Diagnostics。CRUD 与表 / 用户排除不影响慢链路。
- SQL 不再接受正文或参数值作为日志输入；事件只包含安全操作枚举、参数数与耗时。SQL `OnError`、Service AOP、事务和 UnitOfWork 的重复异常输出移除；数据库规范化、提交、回滚、保存点语义保留。
- API 已处理 5xx 只生成一次 `http.failed`；正常业务 4xx 不逐条记录 Warning，返回契约不变。响应已开始和非 API 等无法处理路径仍由框架拥有记录责任。
- 顶层 `RuntimeProcess` 捕获配置加载前及运行失败，输出 stderr 的 `runtime.failed` Fatal，退出 1；正常结束为 0。该终止事件使用独立 `bootstrap / unversioned` 来源，不读取可能损坏的配置，不附带异常原文。HostAbortedException 作为工具控制信号继续传播。
- DbMigrate 诊断固定使用同一 JSONL 生成策略写 stderr，候选开关仍控制 Web 宿主切换；CLI 的 doctor / verify / help 报告使用 stdout，保留成功 / 失败判定。阶段事件使用登记的 `dbmigrate.*` 代码与变更数量；`command` 只允许 apply / doctor / verify / init / seed / help。
- CLI 连接目标和探测失败消息使用安全说明，不显示连接串或第三方异常文本；具体 migration 与 seed 的输出规则见下节。权威 schema ledger 与业务审计写入没有迁移到运行日志。
- 完整异常安全类型 / 栈帧、后台任务、Rust、完整业务事件分类及框架来源治理仍未全部完成。本批不声明 L2 或 L1–L6 整体关闭。

`failureKind` 只允许固定异常类别；`schemaIndex` 只允许 Runner 中登记的仓库自有索引名，便于保留迁移动作对象而不开放自由文本属性。宿主 `HostedServiceStartupFaulted`（EventId 11）仅在 `RuntimeProcess` 正在接管时抑制重复输出；未进入该边界时不静默丢弃。

## 8. L2 seed / migration 生成端治理

- `InitialDataSeeder` 不再接管全局 `Console.Out`，不捕获或回放逐行明细。`dbmigrate.seed.step_finished` 每阶段一次，`seedStep` 仅接受固定枚举，`outcome` 为 succeeded / failed，`durationMs` 为数值；失败仍记录 Info 阶段结果，原异常交给 `RuntimeProcess` 唯一最终错误边界。
- 逐用户 / 逐行正常过程输出移除。默认资源缺失、前置 schema / API 权限缺失、身份冲突、未解决库存快照和偏好纠正失败使用登记的 Warning；必要修复、回收和回填只记录固定事件与数量。邮箱、名称、路径、回调 URI、SQL 与异常文本均不进入这些事件。
- `dbmigrate.seed.completed.count` 表示成功完成的配置阶段数，不表示新增行数；保留开发默认账号开关、阶段顺序和失败即停止的行为。阶段成功不保证不存在已报告的缺失资源警告。
- `dbmigrate.schema.applied` 只在 ledger 事务提交后生成，使用登记的 `migrationId`、`databaseScope` 和耗时；重复 apply 无新提交时不生成。新增迁移 ID 需同步共享 JSON 策略，测试对注册表逐项验证；checksum source、账本写入和迁移顺序保持原义。
- `auth.schema.adopted` 在 OpenIddict history 事务提交后生成。`auth.seed.completed` 仅在整组 seed 成功后记录 `createdCount / updatedCount / removedCount / durationMs`；计数包含 scope 与 client 管理器成功完成的操作，updatedCount 不声称是数据库实际变更行数。客户端、权限、回调与旧 shop 清理逻辑保持不变；失败不报完成，不另记重复 Error。
- 旧 sink 和候选 sink 均只接收这些安全摘要；`RadishLogging.Enabled=false` 保持不变。验证及限制见[本批记录](../records/unified-logging-l2-seed-migration-2026-09-23.md)。

## 9. L2 Outbox 与 Rust 调用边界

- Outbox 空分派保持安静，实际分派一批后仅生成 `outbox.dispatched`，包含 count / durationMs。分派数量不等同于成功处理数量；逐消息成功仍以 Outbox 状态为准，不增加逐条运行日志。
- `ReliableOutboxRepository` 在条件更新确实影响行后记录 `outbox.retrying`（Warning）或 `outbox.dead_letter`（Error）；使用本次真正选择的 Pending / DeadLetter 状态，不在 Job 根据旧快照猜测重试是否耗尽。无效状态、重复执行和未影响行的写入不生成状态事件。`ReliableOutboxExecutionJob` 不再追加携带异常及业务标识的 Error。
- `attempt / databaseScope / outcome` 均为受控属性，Service 通过逻辑上下文补充安全 `failureKind`。`operationId` 由固定前缀、规范库名与 Outbox ID 的 SHA-256 前 16 字节转换为 Guid，同一部署同一库同一任务重试 / 重放可关联；部署之间须同时使用日志 source 区分。它不是凭据，也不替代权威审计键。载荷、任务类型原文、机器名、用户标识和异常文本不进入本批日志。
- 重试间隔、抖动、MaxAttempts、租约、审计错误码及永久失败摘要和内容治理失败记录保持原义。瞬时失败的固定审计提示改为查看错误码和安全摘要，不再误导读者寻找已禁止输出的完整异常。本批没有修复既有租约归属保护问题；日志不是额外数据库事务，也不保证跨重放全局只记录一次失败。领取 / 写库 / 内容治理记录自身抛出的异常仍传播，其 Hangfire 处理边界见第 10 节。
- Rust FFI 移除显式 stderr 打印，只返回既有 ABI 错误码：watermark 为 0 / -1，hash 为 0 / -1 / -2。.NET 使用受控 `nativeOperation / nativeReason / failureKind` 生成安全事件，不输出文件路径、水印、哈希、异常文本或原生错误正文。
- `native.fallback` 与 `native.cleanup_failed` 是 Warning；直接 hash 失败或 native 声称成功却无输出文件使用 `native.failed` Error。图片水印本来就走 C#，无需降级警告；正常工厂创建也不逐次打印 Info。水印 fallback 只调用一次，其结果 / 异常继续交给业务调用方最终处理。
- 本批不改变 FFI 参数、返回值、业务失败结果或部署开关；不宣称已解决 Rust panic / 非法指针边界，或 AttachmentService 等上层业务日志。本机原生构建、返回码 / stderr 和真实动态库 .NET 回归已通过；发现既有 `.tmp` 输入触发水印回退，未据此声明 wrapper 原生水印加速成功。证据与后续边界见[本批记录](../records/unified-logging-l2-outbox-native-2026-09-23.md)。

## 10. L2 Hangfire 与清理任务

- API 的 `HangfireRuntimeStateFilter` 在既有 AutomaticRetry 状态选举之后观察候选结果：失败转为 Scheduled / Enqueued 时记录 `job.retrying` Warning，保留 Failed 或转为 Deleted 时记录 `job.failed` Error；普通调度、成功和无 FailedState 的停机重入队不生成失败事件。不修改次数、延迟、候选状态、异常传播或 Hangfire 存储审计。
- 这是**存储提交前的处理决定**，不是状态已提交或跨重放恰好一次的证据。存储提交失败、进程中断或状态选举重入可产生新的事件。`operationId` 由固定前缀与 Hangfire Job ID 的 SHA-256 前 16 字节转换为 Guid，同一部署 / 存储内可关联；不输出 Job ID、参数、任务类型原文或异常正文。
- 默认全局 AutomaticRetry 与当前清理 / Outbox 的方法属性关闭 `LogEvents`；`HangfireRuntimeLogProvider` 同时抑制该来源的重复输出。其他 Hangfire Warning / Error / Fatal 分别保留安全 `hangfire.runtime_warning / hangfire.runtime_failed`，异常仅映射 `failureKind`；不求值框架消息工厂。框架 Info 及以下降为 Debug 诊断，候选生产模式不输出。该 provider 在 AddHangfire 配置回调中接入，旧 sink 同样只能收到安全摘要。
- 通知收件箱、Wiki 草稿正文、Chat 回应幂等事实只在有清理数量时生成 `job.cleanup.completed`；收件箱只删除关系也属于有效变更。容量告警按本批用户数量聚合为 `job.cleanup.capacity_warning`，不输出 tenant / user 标识。仓储异常继续抛给 Hangfire，不在 Job 重复记录 Error。
- 文件软删除、临时文件、回收站和孤立附件清理使用局部批次计数。成功一次 Info，缺失文件 / 空目录清理异常一次 Warning，已消费的主流程 / 单项异常一次 Error；混合结果为 partial。空批次或仅引用保护跳过保持安静。原有“单项失败继续、外层失败返回 0”、保留期、分片目录排除和引用保护保持不变，不新增重试。
- 文件 `processedCount` 沿用原返回计数口径：软删除 / 孤立附件可包含源文件缺失的已处理记录；`movedCount` 才表示实际移动的主文件与缩略图数量。两者不可互换。`missingCount / failedCount / directoryFailureCount / skippedCount / removedDirectoryCount` 分别记录缺失、已消费异常、目录异常、引用保护与删除空目录数量；不包含路径、文件名或附件身份。
- 该批只覆盖上述任务与 Hangfire 日志适配；商城、抽奖、神评和保留奖励的后续治理见第 11 节，ChunkedUploadService / FileAccessTokenService 内清理分支的后续治理见第 13 节。完整安全栈帧与其他框架来源仍后置；生产候选开关继续关闭。验证范围见[批次记录](../records/unified-logging-l2-hangfire-cleanup-2026-09-23.md)。

## 11. L2 后台业务任务批次摘要

- `ShopJob / PostLotteryJob / CommentHighlightJob / RetentionRewardJob` 的开始、空扫描、逐项成功和业务明细改为批次摘要。`job.batch.completed` 为 Info，非异常拒绝结果为 `job.batch.warning` Warning，当前层已消费的异常为 `job.batch.failed` Error；有成功工作同时有失败时 outcome 为 partial。空扫描、只有幂等跳过以及商城取消锁被占用均不逐轮输出。
- 所有属性只接受固定 jobKind、数字计数与 failureKind，不输出订单 / 评论 / 作者标识、金额、交易号、日期原文、奖励业务键、失败原因或异常正文。旧与候选 sink 使用相同的安全生成端；返回对象、数据库快照和审计仍保留原有内容。
- 商城取消保留“每次正常返回都计入返回值”的原口径，`processedCount` 记录正常返回次数，`updatedCount` 单独记录服务返回 true 的次数；不能把 processedCount 全部解释为实际取消。单项 InvalidOperationException 沿用拒绝后继续的路径并聚合 Warning，不假定其全部属于状态竞争；其他异常消费后汇总 Error，外层失败仍返回 0。权益只统计返回 true 的变更；日报返回统计不变，运行日志只记录被统计的订单数量，不复制收入明细。
- 抽奖保持 batchSize 1–100 钳制和 PostId 去重；单项异常消费后继续，当前批次一次 Error，成功计数沿用服务正常返回次数。扫描异常不消费、不输出本地 Error，仍交给 Hangfire；后续周期能否成功不在当前批次作保证。
- 神评 / 沙发保持统计窗口、排名、快照、先奖励后批量插入和幂等业务键。移除各层仅记录再重抛的 catch，异常仍由 Hangfire 接管；完整执行后才输出本 Job 的批次摘要。`processedCount` 是成功返回的 AddRange 调用所提交的记录数，`updatedCount` 是取消旧当前标记的更新返回行数，`rewardCount` 是服务报告新发放的币 / 经验操作数，不是金额。只有旧标记退役也有摘要。中途异常不生成完成摘要，不宣称此前动作已回滚。
- 保留奖励保持两阶段顺序、按原 DateTime.Now 计算完整周数、最多 3 周及既有失败处理。每项异常继续、阶段查询异常返回 0 后继续下一阶段、顶层异常返回 (0, 0) 均不变；跨阶段已成功的奖励仍计入 rewardCount。现有“已发放过”文本判定与空 FailureReason 行为保持不变，本批不改结果类型或结算时钟。
- `CoinRewardService` 的点赞加成 / 保留奖励方法以及 `OrderService.CancelOrderBySystemAsync` 移除重复的重抛日志；共享订单取消 helper 不再复制取消原因到运行日志。金额、返回理由、事务边界及数据库字段不变。**这不代表整条业务调用链已完成治理**：币 / 经验的实际发放后续见第 12 节，库存服务及其他业务入口仍需按调用链处理。验证与剩余边界见[批次记录](../records/unified-logging-l2-business-jobs-2026-09-23.md)。

## 12. L2 币 / 经验奖励实际发放链

- `GrantCoinAsync / GrantCoinOnceAsync` 不记录逐项开始、成功或重复的最终 Error。币奖励失败继续抛给调用方；唯一键竞争仍回查既有成功流水，未找到时原异常继续传播。正常初始化竞争、幂等命中保持安静，流水号和结果保持原义。
- `GrantExperienceAsync / GrantExperienceOnceAsync` 保留原异常消费边界：分别返回 false 或 Skip，消费处用 `reward.failed` Error 与固定 `failureKind`，不输出用户、金额、类型、业务键、流水、备注、原始异常。唯一键竞争未回查到流水仍返回“奖励业务键冲突”，输出 `reward.conflict` Warning；命中既有流水不输出。奖励键规范化和用户查询等原本位于 catch 之前的异常继续传播，不扩张 catch 范围。
- 经验初始化失败后仍按原逻辑回查，恢复成功安静；回查无记录时仅初始化层输出一次安全 Error 并返回 null，上层不重复报错。非法参数、用户不存在、冻结、每日上限等既有业务返回路径不逐项打印 Warning。单次成功和等级变化不复制到运行日志；经验流水、每日统计、升级 Outbox、自动解冻的权威治理记录保持不变。
- 两服务的乐观锁 helper 只在即将继续重试时输出 `reward.retrying` Warning，包含固定 `rewardDomain`、attempt 和 delayMs；耗尽不重复记录 Error。币保持 3 次重试、100 / 200 / 400ms；经验保持 6 次重试、指数上限 1000ms 内的随机抖动。该共享 helper 也作用于现有其他调用方，未改重试捕获类型、延迟或事务。
- 等级配置缓存读取 / 写入 / 失效异常输出 `reward.cache_fallback` Warning；分别用固定 `rewardOperation` 区分，保留数据库回退和忽略缓存写入 / 清除失败的原有行为。安全日志不求值异常正文，不把缓存故障误报为奖励已失败。
- 币批量发放在消费异常的批次层汇总一次 `reward.batch_failed` Error，部分成功和返回流水列表不变。经验单项已经消费的异常由单项记录 Error，批次只汇总正常返回结果，不再重复 Error；若异常确实逃逸至批次 catch，则由批次汇总。`reward.batch_completed` 是结果汇总 Info，允许 outcome 为 partial / failed，**不表示全部发放成功**。processedCount 是成功返回数，rejectedCount 是 false 返回数（可能是业务拒绝或已消费异常），failedCount 仅统计批次自身捕获的异常；空批次安静。
- 本节仅关闭已列明发放入口、内部重试 / 初始化、缓存与批次路径的日志治理；币扣除 / 转账、账户查询、人工调账 / 治理、其余 CoinRewardService 入口及其外层消费者仍需治理。不会以该批宣称全业务异常已唯一归属、真实数据库并发 / 结算已验收或生产可切换。[验证记录](../records/unified-logging-l2-reward-services-2026-09-23.md)保留证据与未执行边界。

## 13. L2 服务内清理分支

- `ChunkedUploadService.CleanupExpiredSessionsAsync` 与 `FileAccessTokenService.CleanupExpiredTokensAsync` 采用局部批次摘要；`jobKind` 为 upload-sessions / file-tokens。有实际变更且正常结束时记录 `job.cleanup.completed` Info；空扫描、仅竞争跳过或仅成功结算重放保持安静。
- 令牌 `updatedCount` 只统计 `TryRevokeByIdAsync` 返回 true 的次数；`processedCount` 是正常返回次数，`skippedCount` 是 false 返回次数。查询条件仍为到期且未撤销，撤销顺序、时间及异常传播不变，不再把查询条数称为撤销成功数。
- 分片 `updatedCount` 是成功标记 Expired 的次数，`processedCount` 是该条件更新正常返回的次数，`skippedCount` 是返回 false 的次数；`removedDirectoryCount` 是删除调用正常完成的目录数。目录清理或配额释放失败不抹掉已完成的状态更新，也不能把状态更新当作全部清理成功。
- `settlementCount` 仅表示配额完成 / 释放方法正常返回的次数，**不是实际变更数**；现有 Task 接口不区分新结算、幂等命中与禁用。成功结算重放本身不触发摘要；底层 `UploadRateLimitService.CompleteUploadAsync / FailUploadAsync` 的逐项成功日志移除，保留内存与 Redis 原子操作及返回语义。
- 分片单项、目录枚举 / 删除及配额 helper 原来消费的异常继续消费，并按批次汇总一次 `job.cleanup.failed` Error；failedCount 是捕获次数，含多种失败时 failureKind 为 other。此前有实际状态 / 目录变更时 outcome 为 partial，否则为 failed。目录对账、终态查询和令牌操作原本向外传播的异常不增加本地 Error；若中断前已有变更且没有已消费异常，记录 `job.cleanup.interrupted` Info，明确只描述此前进度。若此前另有已消费异常，仍汇总这些异常，不把后续传播异常计入 failedCount。
- 同一目录 / 配额 helper 被前台上传流程调用时，已消费失败使用 `upload.cleanup.failed` Error，cleanupOperation 仅为 directory / quota-release / quota-complete；正常目录删除不逐条记录。上述生成端在旧与候选 sink 均不传递会话 / 用户 / 令牌标识、路径、文件名、附件身份或异常原文。
- 保留 keyed lock、跨租户条件更新、30 分钟孤立目录宽限、每次 500 个目录查询、8 天终态结算重放窗口、最多 2000 条以及批次结算去重；不改变文件生命周期、配额、令牌规则、调度与外部返回。摘要不是权威审计，也不保证崩溃时落盘或跨批次恰好一次。
- 本节不覆盖上传创建 / 合并业务日志、令牌创建 / 验证 / 主动撤销、配额申请 / 重置及 AttachmentService 等其他入口；不据此宣称完整附件业务链收口。生产候选开关仍关闭，验证范围见[批次记录](../records/unified-logging-l2-service-cleanup-2026-09-23.md)。
