# 统一日志事件契约与实现进度

> 更新：2026-10-02。L1 生成契约与采集安全 / 故障可见性子项已实现。主方案见[统一日志专题](./unified-logging-governance-design.md)，首轮实测见[L1 记录](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)。新增证据见[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。L2 入口层已接入显式候选开关，见[生成入口记录](../records/unified-logging-l2-producer-entry-2026-09-19.md)；生产默认链路未切换。

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

采集输入安全、API 不存在时启动、文件路径故障及队列满时丢弃可见性已取得本机证据。L2 入口与 SQL / AOP / 事务 / DbMigrate 入口、seed / 具体 migration / Auth seed 子项已落地，Outbox 与 Rust 显式输出见第 9 节，Hangfire / 清理任务见第 10 节，后台业务 Job、奖励发放、服务内清理与币扣除 / 转账见第 11–14 节，后续账户 / 商城 / 附件、支付口令、公开内容、统计报表子项见第 15–29 节，用户关注通知入队、通知创建 / 推送、通知 Hub、用户关系失效推送、ChatHub、评论实时推送、CommentHub、评论高亮链、轻回应通知入队、评论最终消费、内容提交冲突恢复、帖子编辑 / 置顶、版本恢复、问答、投票、抽奖、标签 / 分类创建与更新、表情回应最终消费及冲突重试见第 30–48 节，其余业务与框架来源继续治理；L1 的正式传输上界、目标部署平台和完整磁盘故障验证仍须关闭。L3 的真实 API / SQLite / PostgreSQL 幂等入库、L4 Console 查询、L5 告警、L6 迁移仍未完成，生产链路保持不变。

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
- `CoinRewardService` 的点赞加成 / 保留奖励方法以及 `OrderService.CancelOrderBySystemAsync` 移除重复的重抛日志；共享订单取消 helper 不再复制取消原因到运行日志。金额、返回理由、事务边界及数据库字段不变。**这不代表整条业务调用链已完成治理**：币 / 经验的实际发放后续见第 12 节，库存 / 订单履约依赖的后续治理见第 16 节；其他业务入口仍需按调用链处理。验证与剩余边界见[批次记录](../records/unified-logging-l2-business-jobs-2026-09-23.md)。

## 12. L2 币 / 经验奖励实际发放链

- `GrantCoinAsync / GrantCoinOnceAsync` 不记录逐项开始、成功或重复的最终 Error。币奖励失败继续抛给调用方；唯一键竞争仍回查既有成功流水，未找到时原异常继续传播。正常初始化竞争、幂等命中保持安静，流水号和结果保持原义。
- `GrantExperienceAsync / GrantExperienceOnceAsync` 保留原异常消费边界：分别返回 false 或 Skip，消费处用 `reward.failed` Error 与固定 `failureKind`，不输出用户、金额、类型、业务键、流水、备注、原始异常。唯一键竞争未回查到流水仍返回“奖励业务键冲突”，输出 `reward.conflict` Warning；命中既有流水不输出。奖励键规范化和用户查询等原本位于 catch 之前的异常继续传播，不扩张 catch 范围。
- 经验初始化失败后仍按原逻辑回查，恢复成功安静；回查无记录时仅初始化层输出一次安全 Error 并返回 null，上层不重复报错。非法参数、用户不存在、冻结、每日上限等既有业务返回路径不逐项打印 Warning。单次成功和等级变化不复制到运行日志；经验流水、每日统计、升级 Outbox、自动解冻的权威治理记录保持不变。
- 两服务的乐观锁 helper 只在即将继续重试时输出 `reward.retrying` Warning，包含固定 `rewardDomain`、attempt 和 delayMs；耗尽不重复记录 Error。币保持 3 次重试、100 / 200 / 400ms；经验保持 6 次重试、指数上限 1000ms 内的随机抖动。该共享 helper 也作用于现有其他调用方，未改重试捕获类型、延迟或事务。
- 等级配置缓存读取 / 写入 / 失效异常输出 `reward.cache_fallback` Warning；分别用固定 `rewardOperation` 区分，保留数据库回退和忽略缓存写入 / 清除失败的原有行为。安全日志不求值异常正文，不把缓存故障误报为奖励已失败。
- 币批量发放在消费异常的批次层汇总一次 `reward.batch_failed` Error，部分成功和返回流水列表不变。经验单项已经消费的异常由单项记录 Error，批次只汇总正常返回结果，不再重复 Error；若异常确实逃逸至批次 catch，则由批次汇总。`reward.batch_completed` 是结果汇总 Info，允许 outcome 为 partial / failed，**不表示全部发放成功**。processedCount 是成功返回数，rejectedCount 是 false 返回数（可能是业务拒绝或已消费异常），failedCount 仅统计批次自身捕获的异常；空批次安静。
- 本节仅关闭已列明发放入口、内部重试 / 初始化、缓存与批次路径的日志治理；币扣除 / 转账及直接消费边界的后续治理见第 14 节；币账户查询、人工调账及经验调整 / 冻结 / 解冻的后续治理见第 15 节；其余 CoinRewardService 入口及直接消费者见第 17 节，经验其余查询 / 人工治理见第 18 节。不会以该批宣称全业务异常已唯一归属、真实数据库并发 / 结算已验收或生产可切换。[验证记录](../records/unified-logging-l2-reward-services-2026-09-23.md)保留证据与未执行边界。

## 13. L2 服务内清理分支

- `ChunkedUploadService.CleanupExpiredSessionsAsync` 与 `FileAccessTokenService.CleanupExpiredTokensAsync` 采用局部批次摘要；`jobKind` 为 upload-sessions / file-tokens。有实际变更且正常结束时记录 `job.cleanup.completed` Info；空扫描、仅竞争跳过或仅成功结算重放保持安静。
- 令牌 `updatedCount` 只统计 `TryRevokeByIdAsync` 返回 true 的次数；`processedCount` 是正常返回次数，`skippedCount` 是 false 返回次数。查询条件仍为到期且未撤销，撤销顺序、时间及异常传播不变，不再把查询条数称为撤销成功数。
- 分片 `updatedCount` 是成功标记 Expired 的次数，`processedCount` 是该条件更新正常返回的次数，`skippedCount` 是返回 false 的次数；`removedDirectoryCount` 是删除调用正常完成的目录数。目录清理或配额释放失败不抹掉已完成的状态更新，也不能把状态更新当作全部清理成功。
- `settlementCount` 仅表示配额完成 / 释放方法正常返回的次数，**不是实际变更数**；现有 Task 接口不区分新结算、幂等命中与禁用。成功结算重放本身不触发摘要；底层 `UploadRateLimitService.CompleteUploadAsync / FailUploadAsync` 的逐项成功日志移除，保留内存与 Redis 原子操作及返回语义。
- 分片单项、目录枚举 / 删除及配额 helper 原来消费的异常继续消费，并按批次汇总一次 `job.cleanup.failed` Error；failedCount 是捕获次数，含多种失败时 failureKind 为 other。此前有实际状态 / 目录变更时 outcome 为 partial，否则为 failed。目录对账、终态查询和令牌操作原本向外传播的异常不增加本地 Error；若中断前已有变更且没有已消费异常，记录 `job.cleanup.interrupted` Info，明确只描述此前进度。若此前另有已消费异常，仍汇总这些异常，不把后续传播异常计入 failedCount。
- 同一目录 / 配额 helper 被前台上传流程调用时，已消费失败使用 `upload.cleanup.failed` Error，cleanupOperation 仅为 directory / quota-release / quota-complete；正常目录删除不逐条记录。上述生成端在旧与候选 sink 均不传递会话 / 用户 / 令牌标识、路径、文件名、附件身份或异常原文。
- 保留 keyed lock、跨租户条件更新、30 分钟孤立目录宽限、每次 500 个目录查询、8 天终态结算重放窗口、最多 2000 条以及批次结算去重；不改变文件生命周期、配额、令牌规则、调度与外部返回。摘要不是权威审计，也不保证崩溃时落盘或跨批次恰好一次。
- 本节不覆盖上传创建 / 合并业务日志及 AttachmentService 等其他入口；令牌创建 / 验证 / 主动撤销与配额申请 / 重置的后续治理见第 21 节；不据此宣称完整附件业务链收口。生产候选开关仍关闭，验证范围见[批次记录](../records/unified-logging-l2-service-cleanup-2026-09-23.md)。

## 14. L2 币扣除 / 转账及直接消费边界

- `ConsumeCoinAsync` 移除逐项开始 / 成功与仅记录再重抛的 catch。扣币流水、余额变动审计、业务字段、返回交易 ID / 流水号、余额校验、事务属性及既有乐观锁重试保持不变；异常由调用方消费或继续传播。共享重试事件仍遵循第 12 节。
- `TransferAsync` 不输出用户、金额、流水号、支付验证理由或异常正文。支付拒绝及幂等成功 / 终态失败重放保持原有结果且不逐条输出。异常规范化、资产写入后的终态失败占键、CompleteFailure 分支、Controller 的 BusinessException 映射均保持不变。
- 资金操作返回流水后，首次幂等完成写入抛错，仍只重试完成记录一次；第二次调用正常返回时输出 `coin.transfer_completion_recovered` Warning，只有安全 failureKind。该事件表示调用恢复返回，不证明记录持久化成功：底层缺失记录本来就可能正常返回。第二次仍抛错则不输出恢复事件，交给外层最终错误边界；不再执行资金写入。
- `OperationIdempotencyService` 唯一键竞争回查成功保持安静，回查无记录仍传播原异常。完成成功 / 失败时记录缺失使用 `idempotency.completion_missing` Warning，completionKind 仅为 success / failure，不输出记录 ID、幂等键、摘要、用户或异常。保存点、24 小时保留、响应及错误审计字段、缺失时直接返回等原行为不变；这些共享方法的其他调用方同样继承安全输出。
- `PaymentPasswordService.VerifyPaymentPasswordAsync` 及其哈希升级 helper 移除逐次成功 / 失败和重复重抛日志；仍保留验证算法、旧版本升级、成功重置计数 / 使用时间、失败累加、5 次失败锁定 30 分钟、返回错误码与剩余次数。无新增口令或安全遥测；设置 / 修改 / 人工解锁等其他入口的已完成治理见第 25 节。
- `OrderService.PurchaseAsync` 的扣币 / 权益 catch 消费异常时记录 `order.purchase_failed` Error，purchaseStage 仅为 payment / fulfillment。保留库存恢复、订单失败阶段 / FailReason、幂等结果与返回值；外层仍包装 BusinessException 后抛出；默认 400 导致最终 API 边界不记 Error 的责任补齐见第 16 节。移除逐次开始 / 成功与支付业务拒绝日志，避免权益失败却打印购买成功。
- 旧与候选 sink 均收到安全生成端事件。审计仍可能保留既有错误说明，本批不改变数据库或 API 契约。余额查询的后续治理见第 15 节；商城库存 / 订单履约的后续治理见第 16 节，商城其他入口尚未全部治理，不能以直接消费层完成宣称全链路异常唯一归属。真实数据库事务 / 并发、Redis 或支付场景运行验收不由 mock 回归替代；生产候选开关继续关闭。证据见[本批记录](../records/unified-logging-l2-coin-movement-2026-09-23.md)。

## 15. L2 币账户查询与人工调账 / 经验治理

- `CoinService.GetBalanceAsync / GetBalancesAsync / GetTransactionsAsync / GetTransactionByNoAsync / GetStatisticsAsync` 移除逐次查询、初始化提示与重复重抛日志。查询筛选、分页、用户展示名、统计日期范围 / 分类、缺失余额初始化及异常对象传播保持不变。
- `AdminAdjustBalanceAsync` 移除金额、操作员、理由、流水号等运行日志和重复重抛日志；权限、参数校验、幂等重放、余额版本、计算、公开事务边界、CoinTransaction / BalanceChangeLog 与返回值均保留。
- CoinController 的余额 / 交易查询和人工调账仍按既有契约将 InvalidOperationException 转为业务响应；最终消费点分别输出 `coin.balance_query_rejected / coin.transaction_query_rejected / coin.adjustment_rejected` Warning，只带固定 `failureKind`。这类异常可能包括业务拒绝和存储失败，不能将 Warning 一律解释为正常业务拒绝。ArgumentException 与 4xx BusinessException 保持安静；人工调账消费的 5xx BusinessException 记录一次安全 `http.failed` Error，其余上抛失败交给既有 API 最终边界。
- `ExperienceService.AdminAdjustExperienceAsync / FreezeExperienceAsync / UnfreezeExperienceAsync` 移除逐次成功日志。经验扣减归零、版本冲突转换、幂等重放、升级 Outbox、冻结状态、权威经验流水及治理动作保持不变；成功操作的身份与理由仍保存在权威记录中。
- 旧 / 候选输出均在 Development / Production 验证；通过真实 Service、Controller、TranAop 与内存 HTTP 管道确认回滚调用及单次安全 Error。mock 仓储与事务管理器不代表真实数据库事务 / 并发验收。
- 本节仅关闭上述入口。商城库存 / 订单履约的后续治理见第 16 节，其余奖励入口见第 17 节；经验账户 / 统计 / 流水查询、人工复核 / 等级治理见第 18 节，口令设置 / 修改 / 管理查询见第 25 节；生产候选开关继续关闭，L2 尚未整体完成。证据见[本批记录](../records/unified-logging-l2-account-governance-2026-09-28.md)。

## 16. L2 商城库存与订单履约依赖

- `ProductService.CheckCanBuyAsync / DeductStockAsync / RestoreStockAsync / IncreaseSoldCountAsync` 移除重复重抛日志；商品配置不完整只输出固定 `product.configuration_rejected` Warning，不携带商品、配置值或理由。购买校验、限购口径、库存扣减 / 回补、已售数量、租户 / 版本条件及返回结果保持不变。
- 扣库存继续沿用“InvalidOperationException 消息含乐观锁冲突”的既有重试判断、最多 5 次尝试和 50 / 100 / 200 / 400ms 退避。仅即将重试时记录 `product.stock_retrying` Warning，属性只含 attempt / delayMs；耗尽继续传播，不在底层额外打印 Error 或“已耗尽”事件。
- `UserBenefitService.GrantOrderFulfillmentAsync` 及权益 / 消耗品 helper 移除逐次开始、成功、重放和重抛日志。保留订单快照校验、来源唯一键回查、原异常传播、固定到期日、背包订单发放接口与事务属性；不会用当前商品替代历史履约快照。空订单的原有 ArgumentNullException 校验不再被日志取字段导致的 NullReferenceException 覆盖。
- `OrderService.CancelOrderAsync / RetryGrantBenefitAsync` 移除重复日志；取消原因、条件取消、库存回补、支付证据、失败阶段、履约资源与订单状态写入保持原样。ShopController 消费的 InvalidOperationException 分别记录 `order.cancellation_rejected / order.fulfillment_retry_rejected` Warning，只带 failureKind；它们可能包含存储或补偿失败，不能一律视为正常业务拒绝。重新发放的 4xx BusinessException 安静，消费的 5xx 使用安全 `http.failed` Error，其余上抛异常交给 API 最终边界。
- 购买支付 / 履约分支继续使用 `order.purchase_failed`。外层包装为默认 400 BusinessException，API 最终边界不会再记录 Error，因此包装点使用 `order.purchase_interrupted` Error 负责尚未被分支消费的失败，仅带 failureKind。支付失败后回补库存另抛错、履约失败后订单写入另抛错是独立失败，允许分别记录分支事件与中断事件；不改变默认 400、InnerException、补偿顺序或订单 FailReason。
- 旧 / 候选输出均覆盖 Development / Production，使用真实 ProductService / UserBenefitService / OrderService、Controller 与内存 HTTP 管道、mock 仓储验证；不代表真实数据库并发、事务或库存运行态验收。本节不涵盖商品管理 / 浏览、订单查询 / 备注、系统赠送、权益查询 / 激活 / 撤销及背包使用等其他入口，这些入口的后续治理见第 19–20 节。生产候选开关继续关闭，证据见[本批记录](../records/unified-logging-l2-shop-fulfillment-2026-09-28.md)。

## 17. L2 其余奖励入口与直接消费者

- `CoinRewardService` 的帖子点赞、评论点赞、评论发布、评论被回复、神评与沙发奖励移除逐项成功和仅记录再重抛的 catch；每日点赞奖励上限查询同样不再重复报错或逐次打印用户限额。金额计算、部分新发放结果、返回流水号、失败理由、业务键与业务日期保持不变；点赞者每日 50 的上限不阻止作者奖励。
- 六类入口的直接消费者 `ReliableTaskProcessor` 保持原有异常传播；由 `ReliableOutboxExecutionJob` 调用既有失败状态写入，在仓储真正更新后输出 `outbox.retrying` Warning 或 `outbox.dead_letter` Error，遵循第 9 节。成功与幂等重放不逐条输出，Pending 等非 Processing 状态重复执行不增加失败事件。该责任只涵盖向外传播的异常，不将下游经验服务已经消费的失败再记一次。
- `CheckRewardExistsAsync` 查询失败仍返回 true，保留避免重复发放的既有兜底；消费点只记录一次 `reward.existence_check_failed` Error，仅携带安全 failureKind，不携带业务类型、身份、日期或异常正文。true 不证明已有成功流水，现有查询筛选与可选日期范围未改变。
- 本批没有修改奖励流水写入、事务、结算时钟、Outbox 租约、重试计划、通知或经验发放规则。内存 SQLite 验证了真实 Outbox 状态更新，币发放及其他业务依赖使用 mock；不代表真实币账本事务 / 并发或 PostgreSQL 验收。旧 / 候选输出均覆盖 Development / Production，生产候选开关继续关闭。证据见[本批记录](../records/unified-logging-l2-reward-entries-2026-09-28.md)。

## 18. L2 经验查询、人工复核与等级治理

- 经验账户单个 / 批量查询、流水、每日统计、治理留痕、等级配置和排行榜移除仅记录再重抛的 catch；无效用户、每日统计更新的无效参数及初始化提示不逐次打印业务明细。筛选、分页钳制、统计窗口、冻结归一化、公开身份补全、初始化与返回语义均保留，未处理异常交给既有 API 最终边界。
- `GetUserRankAsync` 仍在消费异常后返回 0，只在该点输出 `experience.rank_query_failed` Error 和安全 failureKind。0 也可能表示无用户、未上榜或冻结，不意味着查询一定成功。
- 排行榜缺失用户仍跳过，改为每次查询至多一个 `experience.leaderboard_incomplete` Warning，仅含 skippedCount。返回 DataCount 沿用仓储总数，VoRank 只按实际返回用户递增；本批不改变排名或分页规则。
- 人工复核和等级整批重算不再把操作员、用户、版本和审计 ID 复制到运行日志；复核证据、版本推进、幂等结果、重算预览指纹、仓储审计、冲突映射及缓存清除顺序保持不变。缓存失败继续采用第 12 节的安全 Warning；已落库重算不因缓存清除失败被误报为未写入。
- `ExperienceCalculator` 移除缓存命中 / 写入 / 清除成功与无效等级明细；缓存读取 / 写入 / 清除异常分别以 `experience.calculator_cache_fallback` Warning 和固定 rewardOperation / failureKind 记录。公式、缓存键、序列化、过期设置及回退计算保持原样。
- 治理快照列表反序列化异常仍返回空列表，但每个解析失败字段记录一次 `experience.governance_snapshot_invalid` Warning，仅含 failureKind，不读取异常正文或输出快照。空值和 JSON null 沿用空列表返回且安静；多个损坏字段可产生多次独立解析告警。
- 旧 / 候选输出均覆盖 Development / Production，真实 Service、Controller 与内存 API 错误管道验证最终异常归属；mock 仓储不代表真实事务、并发或运行态验收。生产候选开关保持关闭，L2 尚未整体完成，证据见[本批记录](../records/unified-logging-l2-experience-governance-2026-09-28.md)。

## 19. L2 商品管理 / 浏览与订单查询 / 备注

- `ProductService` 的分类、公开商品列表 / 详情、管理列表 / 详情以及创建 / 更新 / 上下架 / 删除移除仅记录再重抛的 catch 和逐项成功明细。公开可用性筛选、资源校验、分类与附件补全、分页排序、租户 / 版本条件、软删除及已有订单禁止删除等规则保持不变。
- `OrderService` 的用户订单、详情、按订单号查询、购买计数、管理列表 / 详情与备注移除重复重抛日志；备注正常保存不再复制操作员与订单身份。用户隔离、订单快照、查询口径、备注规范化、ModifyBy / ModifyId / ModifyTime 和 false 返回均保持原样。
- ShopController 的五个商品写入入口与订单备注消费 InvalidOperationException 时输出 `product.management_rejected / order.remark_rejected` Warning，仅带安全 failureKind；原 400 / 409 响应不变。这些异常可能来自存储，不能一律理解为正常业务冲突。
- 上述入口消费的 4xx BusinessException 保持安静；消费的 5xx BusinessException 使用安全 `http.failed` Error，带状态码与 failureKind。其他上抛异常由既有 API 最终边界处理，不再被 Service 重复记录。业务错误码、响应消息及状态未改变。
- 本批只关闭上述商品与订单入口；系统赠送、权益查询 / 激活 / 停用 / 撤销、背包查询 / 使用 / 加减的后续治理见第 20 节，不宣称完整商城链路收口。旧 / 候选输出均覆盖 Development / Production，mock 仓储及内存 HTTP 管道不替代真实数据库或运行态验收。生产候选开关继续关闭，证据见[本批记录](../records/unified-logging-l2-shop-management-2026-09-28.md)。

## 20. L2 系统赠送、权益操作与背包使用

- `UserBenefitService` 的权益列表 / 按类型 / 当前激活 / 是否拥有查询及系统赠送移除仅记录再重抛的 catch；激活、停用、撤销不再复制用户与权益身份。有效期、撤销状态、激活选择、Changed 返回、原因规范化、仓储权威操作记录及事务属性保持不变。
- `UserInventoryService` 的列表 / 按类型 / 数量查询、加减道具移除重复异常及成功明细；道具使用不再将操作 ID、用户、背包项与数量写入运行日志。保持原合并规则、条件扣减、首次 / 持久化成功重放、效果业务键、权威操作流水和幂等完成顺序。
- ShopController 消费激活 / 停用 / 撤销的 InvalidOperationException 时记录 `benefit.activation_rejected / benefit.deactivation_rejected / benefit.revocation_rejected` Warning，仅带 failureKind。原 400 / 400 / 409 响应不变；异常也可能来自持久化，不能一律视为正常拒绝。其他异常继续交给 API 最终边界。
- 道具使用与改名卡接口消费一般异常或 5xx BusinessException 时记录 `inventory.use_failed / inventory.rename_failed` Error，仅带 failureKind。接口仍按原契约返回 400 和失败结果；不因错误事件变成新的 5xx 响应。4xx BusinessException、正常失败结果与成功重放保持安静；经验发放层已经消费并记录的失败不重复报错。
- 改名效果原本将 ArgumentException / InvalidOperationException 包装为默认 400 BusinessException；前者保持安静，后者在包装点记录一次 `inventory.rename_rejected` Warning。保留 InnerException、原消息、展示名变更审计与事务，不记录新旧展示名。IO 等未包装异常由道具接口消费，旧改名卡路由复用同一处理边界。
- 本批不改变系统赠送或背包加减的既有调用 / 事务边界，不宣称所有商城与用户资料入口完成。旧 / 候选输出均覆盖 Development / Production，生产候选开关继续关闭。验证及限制见[本批记录](../records/unified-logging-l2-shop-entitlements-2026-09-28.md)。

## 21. L2 文件访问令牌与上传配额

- `FileAccessTokenService` 移除创建、成功消费、主动撤销（记录 ID / 兼容原始令牌入口）的逐项 Info；空值、候选失效、当前 Wiki ACL 拒绝及原子消费未命中的正常拒绝不再输出 Warning。不再生成 TokenHashPreview，令牌原文、哈希及其片段、附件身份均不进入这些运行日志。
- 原始令牌仅创建响应返回、持久化只存哈希、有效期 / 用户 / IP / 次数限制、附件可用性、当前 Wiki 读写权限、原子消费与撤销规则保持不变。冲突、权限拒绝及令牌查询摘要契约不变，清理批次摘要继续按第 13 节执行。
- `UploadRateLimitService` 移除预留被拒的用户 / 文件大小 Warning 和重置计数的用户 Info；并发、分钟频率、日容量的拒绝仍由结果或 429 告知调用方。Redis Lua、内存 keyed lock、预留重放、业务日结算、失败释放及当前用户重置规则不变。
- 两个 Service 未新增 catch；存储 / 缓存异常继续上抛。令牌 Controller 的既有 500 BusinessException 包装和按 ID 撤销的直接传播保持原样，API 最终边界记录一次安全 `http.failed`；正常拒绝与 4xx 保持安静。普通上传申请配额发生在上传 try 之前，分片申请发生在创建会话 try 之前，异常也继续交给上层。
- 此批只关闭令牌与配额 Service 的上述生成点，未新增配额重置生产入口；分片上传 / 合并及 AttachmentService、普通上传后续处理的已完成治理见第 22–24 节。旧 / 候选输出覆盖 Development / Production，生产候选开关继续关闭。证据与限制见[本批记录](../records/unified-logging-l2-file-token-quota-2026-09-28.md)。

## 22. L2 分片上传与会话回写

- `ChunkedUploadService` 移除创建会话、逐片上传、合并成功与取消的明细 Info；不再输出会话 ID、文件名、分片索引及附件身份。参数校验、用户归属、keyed lock、分片回滚和成功重放保持不变。
- 合并主流程失败仍按原顺序尝试写入 Failed 状态、清理目录及释放 / 结算配额，然后原异常重抛；本层不再重复记录该异常。Controller 的既有 500 包装与 API 最终安全日志保持原样。
- 附件已持久化但 Completed 状态首次回写失败时，保留原有的一次补写；按最终结果仅输出一次事件：恢复为 `upload.session.update_recovered` Warning，仍失败为 `upload.session.update_failed` Error。只带固定 failureKind，不携带异常正文、会话或附件身份。无论补写结果如何，既有成功响应、配额结算和目录清理不变。
- 合并失败后的 Failed 状态补写异常单独消费，输出一次 `upload.session.update_failed`；它与原始合并异常是两次不同故障，不替代原异常传播。前台目录 / 配额 helper 及后台清理摘要继续按第 13 节执行，不新增重试。
- 本批覆盖分片编排层；底层 AttachmentService 和普通上传 Controller 的后续治理见第 23–24 节，不据此宣称完整附件系统已收口。旧 / 候选输出覆盖 Development / Production；使用 mock 附件服务、mock 仓储和真实临时分片文件，不能替代真实数据库或图片处理验收。生产候选开关保持关闭，证据见[本批记录](../records/unified-logging-l2-chunked-upload-2026-09-28.md)。

## 23. L2 附件上传与图片处理

- `AttachmentService.UploadFileAsync` 移除上传开始 / 成功、去重命中与跳过、水印配置跳过的明细；图片缩略图、水印、多尺寸和 EXIF helper 不再记录成功明细、结果中的 ErrorMessage 或包装再抛的异常。文件名、路径、哈希和水印文本不进入上述日志。
- LocalFileStorage 上传异常仍按既有规则尝试删除写入目标并返回 StorageFailed，仅移除重复原文日志；该结果由唯一生产调用方 AttachmentService 映射为原有 500 BusinessException。其他大小 / 类型 / 内容拒绝仍保留原错误码、状态和参数。
- 普通上传 Controller 消费 5xx BusinessException 或空附件结果时输出一次安全 `http.failed`，4xx 保持安静；未消费异常仍由 API 最终边界记录。上传成功后的配额结算异常、失败后的配额释放异常继续消费，复用 `upload.cleanup.failed` 与固定 cleanupOperation，不再记录附件 / 用户 / uploadId / 异常正文。响应状态、消息及成功语义保持原样。
- 去重记录存在而物理文件缺失时，以 `attachment.dedup_source_missing` Warning 保留可见性，原软删除及继续上传不变。文件替换仍最多尝试 3 次、两次间隔 100ms；最终恢复输出一次 `attachment.replace_recovered` Warning（count 为此前失败次数），持续失败原异常上抛，不逐次重复输出。
- 失败上传的主文件、缩略图与预登记派生路径仍逐项尝试清理；删除未成功且仍存在、以及捕获异常，按本次 cleanup 调用合并为一个 `attachment.cleanup_failed` Error，failedCount 为失败路径数，混合 failureKind 为 other。临时图片文件删除异常单独消费为 `attachment.temp_cleanup_failed` Error。不改变清理顺序、继续执行或原错误传播。
- 下载 / 删除 / 下载计数生成点的后续治理见第 24 节；Rust 原生能力回退事件保持既有边界，不以此批证明真实动态库、权限故障或重试恢复时序。生产候选开关继续关闭，证据与限制见[本批记录](../records/unified-logging-l2-attachment-upload-2026-09-28.md)。

## 24. L2 附件下载、删除与下载计数

- `AttachmentService.DeleteFileAsync` 的附件缺失与软删除成功保持安静；更新未产生正数结果时输出 `attachment.delete_rejected` Warning，捕获异常时输出 `attachment.delete_failed` Error，仅带安全 failureKind。返回 false、软删除字段与审计、单项删除接口的原响应不变，不新增物理删除。
- 批量删除仍逐项调用单项方法、失败继续、返回成功数量；已消费的单项故障各记录一次，不在 Controller 重复报错。权限预检或资产查询原本上抛的异常继续由 API 最终边界处理，不能把部分成功数量当作全部删除成功。
- 下载缺失 / 已删除 / 禁用 / 权限拒绝保持安静；存储返回空流时输出 `attachment.download_unavailable` Warning。LocalFileStorage 的既有空值契约同时覆盖缺失与捕获故障，本事件只说明不可用，不推断具体原因。Service 捕获查询、权限依赖、存储等异常时输出一次 `attachment.download_failed` Error，仍返回空结果及接口原有 404。
- 下载计数捕获异常输出 `attachment.download_count_failed` Error，仍继续返回已取得文件流；不被下载外层重复记录。计数更新返回 false 与附件不存在仍按既有语义安静，不修改计量并发算法。
- 原图 / 缩略图、下载文件名与 MIME、令牌下载、Chat / Wiki 权限及用户角色判断保持不变。上述日志不含附件 / 用户身份、业务类型、文件路径或异常正文。底层存储的其他布尔 / 空值语义未改变，真实权限故障和并发仍需单独验收。
- 此批关闭上述已有日志生成点，不据此宣称完整附件系统或 L2 已收口；生产候选开关保持关闭。旧 / 候选输出覆盖 Development / Production，证据见[本批记录](../records/unified-logging-l2-attachment-access-2026-09-28.md)。

## 25. L2 支付口令设置、修改与管理查询

- `PaymentPasswordService` 的状态、设置、修改、管理员重置 / 解锁、统计与过期锁定清理移除仅记录再重抛的 catch；正常设置 / 修改 / 管理操作不再输出用户、操作员、强度及原因明细。六位口令校验、Argon2id 与旧版本升级、失败计数 / 锁定、写入顺序与返回语义保持不变。
- 管理员重置继续保存 ModifyBy / ModifyId / ModifyTime / Remark；通用审计中间件、安全记录查询的用户范围、分页上界、管理员路径过滤及 DTO 映射未改变。本批不新增审计机制或事务保证，不把运行日志当作权威审计。
- 安全建议消费异常时使用 `payment.suggestions_failed` Error，仅带固定 failureKind，继续返回原有建议回退文字；状态查询调用建议失败时仍成功返回状态，不重复记录同一异常。
- 手动清理过期锁定仅在清理数量为正时输出 `payment.locks_cleared` Info，包含 processedCount；零进展保持安静。该入口不是新增后台任务，UTC 时间参数、仓储操作和原数量响应不变。
- PaymentPasswordController 设置 / 修改消费的 4xx BusinessException 保持安静；5xx 在该消费点输出安全 `http.failed` Error，仅带状态和 failureKind。其余上抛异常继续由 API 最终边界处理一次，原错误码、消息及响应保持不变。
- 旧 / 候选输出覆盖 Development / Production；mock 仓储、真实 Service / Controller 与内存 HTTP 管道不替代真实数据库、审计中间件运行或并发验收。生产候选开关继续关闭，L2 尚未整体完成，证据见[本批记录](../records/unified-logging-l2-payment-password-2026-09-28.md)。

## 26. L2 公开 head 快照、sitemap 与 Gateway 消费

- `PublicHeadSnapshotService` 的缓存读取 / 解析与写入失败分别使用 `public_head.cache_read_failed / public_head.cache_write_failed` Warning，仅带固定 failureKind，不再输出缓存键、快照、URL 或异常原文。缓存命中、缺失和未知路由保持安静；20 分钟 TTL、公开可见性筛选、快照字段及查询异常传播不变。
- `PublicSitemapService` 的缓存读取 / 写入异常分别使用 `sitemap.cache_read_failed / sitemap.cache_write_failed` Warning；生成异常使用 `sitemap.generation_failed` Error；索引单个栏目统计失败使用 `sitemap.section_count_failed` Warning。仅带 failureKind，不输出缓存键或栏目参数。30 分钟 TTL、查询 / 分片规则、上次成功 XML / 空 XML 回退及部分索引生成保持原样，各消费点只记录自身故障。
- API 两个公开 Controller 无新增 catch；head 查询异常继续由 API 最终边界记录一次 `http.failed`。缺失资源仍为 404，正常快照 / XML 的内容与响应类型不变。
- Gateway 获取快照 / 前端 HTML 的非成功响应分别使用 `public_head.snapshot_unavailable / public_head.html_unavailable` Warning，仅含整数 statusCode；快照 404 仍安静。原本捕获的 HttpRequestException / TaskCanceledException 使用 `public_head.snapshot_request_failed / public_head.html_request_failed` Warning，仅带 failureKind，继续返回 null；不扩大捕获范围。
- Gateway 注入 / 响应写入被捕获的异常使用 `public_head.injection_failed` Warning，仅带 failureKind，继续调用后续处理；日志作用域在调用后续处理前结束。五分钟入口 HTML 缓存、十分钟注入缓存、请求与转发头规则、缺失回退及 JSON 解析异常原传播边界未改变。
- 同一跨宿主故障可能分别产生 API 的处理失败和 Gateway 的不可用响应事件，二者描述不同边界，不宣称分布式全链只产生一条日志。旧 / 候选输出覆盖 Development / Production，生产开关继续关闭；本批未治理全部 HttpClient / YARP 框架日志，证据与限制见[本批记录](../records/unified-logging-l2-public-metadata-2026-09-28.md)。

## 27. L2 公开发现流

- `PublicDiscoverService` 移除逐请求生成完成 Info 和仅供日志使用的 Stopwatch；不再输出租户、候选数、页大小、游标版本与耗时明细。正常空页、成功续页、参数与游标拒绝保持安静。
- 非 BusinessException 仍包装为原有 `503 / PublicDiscover.SourceUnavailable / error.public_discover.source_unavailable`，删除包装前的原文 Error；既有包装未保留 InnerException，本批不修改该契约，也不伪称最终日志能够诊断原始数据库故障类别。BusinessException 仍原样上抛。
- 直接生产消费者 PublicDiscoverController 保持 `no-store`、响应内容与无 catch 的传播边界；API 最终边界仅对 5xx 输出一次安全 `http.failed`，4xx 安静。仓储无本批需要新增或修改的日志生成点，不新增事件码或查询摘要。
- 七个来源任务启动顺序与 Task.WhenAll、单来源失败导致整页失败、公开租户 / 24 小时窗口、来源资格、稳定排序、游标与分页、纯文本映射和 Pulse 聚合不变，不引入部分成功或跨请求缓存。
- 旧 / 候选输出覆盖 Development / Production；新增 mock 故障测试和既有临时 SQLite 仓储回归不替代 PostgreSQL / 真实宿主与浏览器验收。生产开关继续关闭，L2 尚未整体完成；证据见[本批记录](../records/unified-logging-l2-public-discover-2026-09-28.md)。

## 28. L2 公开排行榜与用户排名

- `LeaderboardService.GetLeaderboardAsync / GetUserRankAsync` 移除仅记录再重抛的 catch，不再复制用户、榜单类型、分页参数或异常原文；异常本身继续原样传播，不转换为排名 0 或部分成功。
- 唯一直接生产消费者 LeaderboardController 不新增 catch；正常结果、类型拒绝、个人排名不支持及未登录结果保持原样。上抛故障由 API 最终边界记录一次安全 `http.failed`；4xx BusinessException 安静，5xx 记录 Error，不新增事件码。
- 五类公开榜单白名单、四类用户排名、分页规范化、稳定排名、资格复核后的短暂名次空洞、元数据排序、用户展示与当前用户标记均保持不变。头像 / 经验 / 等级及商品图标依赖失败继续上抛；既有 DateTime.Now 参数语义不因日志治理改变。
- 旧 / 候选输出覆盖 Development / Production；新增真实 Service / Controller 与 mock 依赖、内存 HTTP 管道验证异常归属，既有临时 SQLite 仓储测试验证资格与排名。PostgreSQL 用例明确排除，本批不代表生产数据库或浏览器验收。生产开关继续关闭，L2 尚未整体完成；证据见[本批记录](../records/unified-logging-l2-leaderboard-2026-09-28.md)。

## 29. L2 统计报表

- `StatisticsService` 的仪表盘、订单趋势、商品销量排行与用户等级分布移除仅记录再重抛的 catch，不再输出查询参数或异常原文；Service 仍传播同一异常，不返回空报表或部分成功。
- 唯一直接生产消费者 StatisticsController 保留四个入口的现有包装：所有异常（包括原 4xx / 5xx BusinessException）均包装为 `500 / System.UnexpectedError / error.system.unexpected_error`，保留 InnerException 和各入口固定提示。API 最终边界只记录一次安全 `http.failed`，不新增事件码，不借日志治理调整响应状态。
- DashboardView 权限、用户软删除筛选、仓储聚合口径、天数 / 条数规范化、DateTime.Today 的本地日历边界、逐日顺序与排他结束时间、等级 0 补足及等级名称回退均保持原样。
- 旧 / 候选输出覆盖 Development / Production；真实 Service / Controller、mock 仓储与内存 HTTP 管道验证异常归属和结果，不替代真实数据库聚合、权限中间件或浏览器验收。生产开关继续关闭，L2 尚未整体完成；证据见[本批记录](../records/unified-logging-l2-statistics-2026-09-28.md)。

## 30. L2 用户关注通知入队

- `UserFollowService` 的通知准备 / 入队阶段不再输出双方用户身份或原始异常。唯一直接生产消费者 `UserFollowController.Follow` 会消费 `ArgumentException / InvalidOperationException` 并返回原有 400 / 404 业务响应；这两类通知阶段失败由 Service 记录一次 `user_follow.notification_enqueue_failed` Error，仅带受控 failureKind，然后原样重抛。参数、自关注、目标不可用及屏蔽拒绝不新增日志。
- 其余异常保持传播：API 最终边界对一般异常或 5xx BusinessException 输出一次安全 `http.failed`；4xx BusinessException 保持安静。不新增通用失败包装，不改变 Controller 捕获范围和响应正文；现有响应可能包含被消费异常的 Message，本批只治理运行日志。
- 关注关系仍先由仓储独立事务提交，再构造通知并写入可靠 Outbox；二者没有共同事务。入队失败不回滚已提交关系，再次关注返回未变更时不补投通知。本批不新增重试、补偿或事务保证，也不改写 Outbox 的异步消费 / 重试策略。
- 通知类型、接收者、模板参数、目标、身份快照、UTC 时间及通知业务键 / 任务幂等键保持原义；正常成功、重复关注保持安静。Outbox 权威载荷继续保存业务数据，日志安全裁剪不作用于通知载荷。
- 旧 / 候选日志路径覆盖 Development / Production；真实 Service、Controller、ReliableOutboxService 与 mock 仓储及内存 HTTP 管道验证异常所有权和原行为。既有 SQLite 仓储回归不替代真实宿主、PostgreSQL 或跨事务故障验收。生产开关继续关闭，L2 尚未完成，见[本批记录](../records/unified-logging-l2-user-follow-2026-10-02.md)。

## 31. L2 通知创建与实时推送降级

- `NotificationService.CreateNotificationAsync` 移除“按偏好抑制全部接收者”的逐条 Info 及无剩余用途的 logger 依赖。偏好 / 屏蔽抑制仍返回通知 ID，可靠任务成功结束，不创建空通知、不推送、不输出身份摘要；正常创建和幂等命中同样保持安静。
- 直接依赖 `NotificationPushService` 的 SignalR 分组访问、revision 事件及兼容角标事件失败仍被消费，只输出一次 `notification.push_failed` Warning，属性仅为受控 failureKind。用户 ID、revision、连接信息和原始异常不进入运行日志；推送仍为 best-effort，不回滚通知、不令 Outbox 进入重试，前一个发送失败时不继续后一个发送。
- 接收者规范化、偏好与屏蔽规则、强制分类、目标与模板校验、通知身份快照、入箱持久化与 revision、两种推送载荷均保持不变。`ReliableTaskProcessor` 继续以 Outbox 租户和发生时间覆盖载荷值；NotificationRequested 的 ArgumentException / JsonException 仍转为永久失败，其余失败按既有路径交给 Outbox 最终状态写入所有者输出 `outbox.retrying / outbox.dead_letter`。
- 旧 / 候选 × Development / Production 覆盖正常 / 抑制 / 混合接收者、推送两阶段降级、依赖失败重试与死信。使用真实 Service、Processor、ExecutionJob、PushService、SQLite Outbox，加上 mock 收件箱 / 用户仓储与 SignalR；既有 SQLite 收件箱回归覆盖仓储幂等，不代表真实网络、PostgreSQL 或全通知系统验收。
- 本批不改权威失败摘要、重试次数、审计、租约或推送可靠性；`NotificationHub` 连接生命周期与 SignalR 框架日志仍待治理。生产候选开关继续关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-notification-creation-2026-10-02.md)。

## 32. L2 通知 Hub 连接生命周期

- `NotificationHub.OnDisconnectedAsync` 在传入异常非空且原分组清理成功后，输出 `notification.connection_closed` Warning，仅包含受控 failureKind；不传递异常对象、用户 / 租户 ID、连接 ID 或凭据。正常连接和断开继续安静，不新增连接计数或逐用户摘要。
- 身份标准化、query access_token 优先于 Authorization 的既有读取规则、用户组名、连接初始化 revision / 兼容角标与调用顺序保持不变。连接时用户 ID 无效仍拒绝；断开时 ID 非正仍跳过移组；身份解析、加 / 移组、摘要或发送失败继续原样传播，不新增 catch、重试或兜底。
- 该事件描述 Hub 收到的异常断开原因，不替代 SignalR 框架最终处理日志。与本机运行时相同的 ASP.NET Core 10.0.8 源码显示，连接初始化、消息处理及断开回调失败分别由框架记录；消息处理失败还可能传入 Hub 回调。因此不宣称全连接故障只输出一条事件，也不将 SignalR 整体来源视为已迁移。
- 新回归覆盖旧 / 候选 × Development / Production，验证原异常实例、清理前不记录、清理失败仍传播、正常初始化载荷、身份拒绝及凭据选择。证据为真实 Hub 方法与 mock SignalR / 业务依赖；不替代真实 WebSocket、框架调度或认证中间件验收。生产开关保持关闭，见[本批记录与框架依据](../records/unified-logging-l2-notification-hub-2026-10-02.md)。

## 33. L2 用户关系失效推送

- `UserInteractionRealtimeNotifier` 双 Hub 的 catch 分别输出 `user_interaction.chat_push_failed / user_interaction.notification_push_failed` Warning，仅带受控 failureKind；不输出异常对象、用户 ID、关系版本或动态 Hub 名。每个失败发送保留一个事件，一次调用最多两个有效去重接收者、四次发送，不将多次独立降级合并为一个失败。
- 非正接收者过滤、按输入顺序去重、每人先 Chat 后 Notification、版本使用 InvariantCulture 字符串以及只含版本的推送载荷均保留。分组访问或发送失败继续消费，仍尝试后续 Hub 和接收者；不新增重试或改变 best-effort 语义。
- 唯一直接生产消费者 `ReliableTaskProcessor` 保留 Blocked 先抑制双方通知、Unblocked 不恢复通知、随后发送关系失效的顺序。真实 notifier 消费推送失败后 Outbox 仍成功；通知抑制失败与未知事件继续由既有任务 / Outbox 边界处理，不更改权威错误摘要、重试或审计。
- 回归覆盖旧 / 候选 × Development / Production，候选显式配置并核对 mode。真实 notifier、Processor、ExecutionJob 和 SQLite Outbox 配合 mock SignalR / 收件箱验证继续发送、纯版本载荷、成功 / 重试 / 死信及日志安全；不代表真实 SignalR、屏蔽事务或 PostgreSQL 验收。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-user-interaction-2026-10-02.md)。

## 34. L2 ChatHub 自有日志

- ChatHub 移除逐连接 Info、加入 / 离开频道 Debug。异常断开在 presence 清理和用户分组移除成功后输出 `chat.connection_closed` Warning，仅保留受控 failureKind；不记录用户 / 租户 / 频道 / 连接身份或原始异常。正常生命周期与频道进出保持安静，Development 开启 diagnostics 后也不重新生成这些事件。
- 加入频道仍先经 ChatService 权限校验，再入租户频道组并登记 presence；离开仍先调用 Service，再退组与清理 presence；断开仍先清理 presence 再移除用户组。身份、权限、分组或 presence 失败原样传播，不增加 catch、补偿或重试。
- 非正频道的加入拒绝、离开 / 输入中安静返回、非法用户拒绝、输入中 CanSend 校验和 OthersInGroup 广播载荷保持不变；不改组名、凭据读取、权限、在线状态算法或重复加入语义。
- 旧 / 候选 × Development / Production 回归包含实际 ChatHub、mock SignalR / Service 以及真实 ChatPresenceService 双连接清理；既有 ChatService / ChatChannelAccessService 测试覆盖相关服务。框架输出仍按第 32 节的未覆盖边界处理，不宣称整个连接故障只有一条事件或已完成真实 WebSocket 验收。生产开关保持关闭，见[本批记录](../records/unified-logging-l2-chat-hub-2026-10-02.md)。

## 35. L2 评论实时推送及直接消费边界

- CommentRealtimePushService 统一在既有 catch 消费分组访问或发送失败，生成 `comment.push_failed` Warning，仅保留受控 failureKind；不记录帖子 / 评论 / 用户身份、事件名、组名、载荷或原始异常，不求值异常正文。成功推送保持安静，失败不重试。
- CommentCreated / CommentUpdated / CommentDeleted / CommentLikeChanged / CommentHighlightsChanged 的事件名、`post-comments:{postId}` 组名、对象载荷、父 / 根评论关系及 UTC 事件时间保持原义。创建 / 更新仍在帖子或评论 Id 非正时短路，高亮无变更或帖子 Id 非正时短路；删除 / 点赞原本没有该校验，本批不扩张参数规则。
- 唯一直接生产消费者 CommentController 保留创建、点赞、删除、编辑及版本恢复的写入 / 读取 / 重算 / 推送顺序与响应。首个推送失败后仍尝试高亮推送；幂等重放、重复内容、无变更不重复发送；创建 / 点赞的详情缺失仍可发送高亮，编辑 / 恢复详情缺失则不重算或发送。
- 推送 catch 不扩展到 Controller 的业务写入、详情读取或高亮重算；这些步骤原有异常传播、ArgumentException / InvalidOperationException 响应映射及软删除审计字段均保留。Controller 原有静默 400 消费边界未新增日志，不据此宣称所有业务失败已有最终日志。
- 旧 / 候选 × Development / Production 回归使用真实推送服务、真实 Controller 与 mock SignalR / 业务依赖；不代表真实数据库事务、幂等并发或 WebSocket 验收。CommentHub 自有日志的后续治理见第 36 节；CommentService 神评 / 沙发计算与填充日志的后续治理见第 37 节；SignalR 框架来源仍待治理，生产开关保持关闭。见[本批记录](../records/unified-logging-l2-comment-realtime-2026-10-02.md)。

## 36. L2 CommentHub 自有日志

- 移除加入 / 离开帖子组的逐次 Debug 与不再使用的 ILogger 注入；CommentHub 自有路径不再生成运行日志。不新增 Warning / Error 或捕获边界，正常调用与向外传播的失败均不在 Hub 重复记录。
- 加入时非正帖子 Id 仍抛原 HubException；离开和输入中对非正帖子 Id 仍直接返回。加入 / 离开继续等待既有组操作，不新增身份查询或权限校验；`post-comments:{postId}` 组名保持不变。
- 输入中继续通过原 normalizer 读取身份，只有 IsAuthenticated 且 UserId 为正时发送 CommentTyping 给 OthersInGroup。帖子 / 可空评论 / 用户 Id、名称空白回退 Unknown、UTC 时间与凭据选择顺序保持原义；原本未校验的 commentId 不增加约束。
- 四种输出模式使用 DI 激活真实 Hub，验证分组等待、参数短路、广播载荷、query / header 凭据传递及原异常实例传播。旧路径显式捕获 Trace 以上日志，候选 Development 开启 diagnostics，避免用输出过滤掩盖逐次日志。
- 回归覆盖 Hub 方法和 mock 分组 / 发送 / normalizer，不能替代真实 SignalR 调度、WebSocket 或认证中间件验收。CommentHub 自有日志已收口；CommentService 高亮链的后续治理见第 37 节，框架来源仍待治理，生产开关保持关闭。见[本批记录](../records/unified-logging-l2-comment-hub-2026-10-02.md)。

## 37. L2 评论神评 / 沙发实时重算与标识填充

- CommentService 移除实时重算、扫描、逐项更新与递归标识填充的 Info / Debug。既有消费点使用 `comment.god_recheck_failed / comment.sofa_recheck_failed / comment.highlight_fill_failed` Error，外层重算 catch 使用 `comment.highlight_recheck_failed` Error；仅保留受控 failureKind，不记录帖子 / 评论 / 作者身份、内容快照、点赞数、排名、缓存键、奖励键或原始异常。
- 实时开关、数量门槛、前五名查询、最高赞并列、稳定窗口与替换领先值、零赞清理、排名和快照保持不变。新高亮仍先退役旧记录、插入新记录、入队基础奖励再失效缓存；既有高亮的点赞增量仍先入队奖励再更新快照，原业务键、载荷和 UTC 入队时间不变。
- 原 catch 继续消费计算 / 存储 / 入队 / 缓存异常，保留原返回对象；中途失败可能在已写入高亮或奖励后返回 NoChange。事务拦截器见到正常返回仍走 commit，不因安全 Error 自动回滚，日志也不宣称此前写入已撤销或高亮最终状态已同步。这一现有可靠性边界未在日志治理中改变。
- 根评论页标识填充失败仍返回评论并继续作者资料填充；单条详情的高亮读取、外层分页和作者资料失败继续传播，不扩大 catch。递归填充保留神评 / 沙发布尔标识和排名；当前根页清空子评论，递归分支通过定向调用单独验证，不冒充页面运行态覆盖。
- 真实 CommentService、ReliableOutboxService、TranAop 与 Controller 配合 mock 仓储 / 缓存 / SignalR 验证成功与无变更安静、失败单条事件、部分进度、创建 / 点赞 / 删除的返回与无变更高亮广播抑制；上一批 Controller 回归继续覆盖编辑 / 恢复的调用边界。持久化仅在内存 mock 中模拟，不代表真实 SQLite / PostgreSQL 事务、并发奖励或 Redis 验收。
- 本批关闭上述高亮日志生成点；CommentController 静默 400 消费边界的后续治理见第 39 节；其余业务 / 框架来源继续治理，生产开关保持关闭，L2 尚未整体完成。见[本批记录](../records/unified-logging-l2-comment-highlight-2026-10-02.md)。

## 38. L2 轻回应通知入队与 Controller 最终消费

- `PostQuickReplyService` 通知 helper 不再对所有异常记录含身份与异常原文的 Warning 后重抛。普通异常保持原实例传播并由 API 最终边界记录 `http.failed`；通知阶段的 ArgumentException 因 Create 会消费并返回 400，在 helper 保留 `quick_reply.notification_enqueue_failed` Error，仅带 failureKind。
- `PostQuickReplyController` Create / Delete 消费的 5xx BusinessException 使用安全 `http.failed` Error，仅带 statusCode / failureKind；4xx 业务拒绝与普通参数校验保持安静。GetRecentByPostId 消费的 InvalidOperationException 使用 `quick_reply.query_rejected` Warning，保留既有 404 响应；未调整错误码、消息键、响应文案与 ApiErrorContract 状态映射。
- 原内容规范化、长度限制、权限、冷却 / 重复内容限制、帖子作者筛选、通知业务键 / 载荷 / UTC 时间及软删除审计不变。入队失败仍向事务 AOP 传播并调用 rollback；此前写入的冷却与去重缓存不会随数据库事务撤销，失败后重试仍可能返回 429 / 409，不新增补投或创建成功重放。
- 旧 / 候选 × Development / Production 覆盖真实 Service、ReliableOutboxService、TranAop、Controller、结果过滤器和内存 API 异常管道；mock 依赖验证调用顺序与缓存现状，不替代真实数据库回滚或 Redis / HTTP 宿主验收。
- 生产开关保持关闭，L2 尚未整体完成；CommentController 创建 / 点赞 / 编辑静默消费边界的后续治理见第 39 节。见[本批记录](../records/unified-logging-l2-post-quick-reply-2026-10-02.md)。

## 39. L2 评论创建 / 点赞 / 编辑最终消费

- 正常内容长度校验改用继承 ArgumentException 的 `CommentContentValidationException`；点赞评论不存在、编辑服务已返回失败结果改用继承 InvalidOperationException 的 `CommentOperationRejectedException`。这是项目所有者单独确认的内部异常分类变更，不调整 HTTP 状态、文案或业务规则，不通过匹配异常文本判断是否为正常拒绝。
- `CommentController` Create / ToggleLike / Update 消费的非正常 ArgumentException / InvalidOperationException 分别使用 `comment.create_failed / comment.like_failed / comment.edit_failed` Error，仅带固定 failureKind。专用拒绝类型保持安静；4xx BusinessException 继续按既有契约上抛至 API 并安静处理；其他未处理异常仍只由 API 最终边界记录 `http.failed`。
- 编辑内容校验 catch 保留原 `(false, message)` 返回。明确长度拒绝保持安静；系统设置依赖抛出的普通 ArgumentException 在此最终消费点记录一次 `comment.edit_failed`，随后写入服务转换为拒绝异常，Controller 不再重复记录。恢复版本的消费者继续接收原失败结果。
- 提交台账、版本追加、Outbox 载荷、提交 / 回滚顺序及重放 / 重复 / 无变化时不推送的决策不变。创建 / 编辑成功后的详情读取在写入事务提交后，失败仍返回原 500；点赞详情读取仍在原 catch 内，InvalidOperationException 仍转为 400。本批不把响应失败解释为先前写入已回滚。
- 四模式回归覆盖真实 Service、ForumContentWriteService、TranAop、Controller、结果过滤器与内存 API 管道；SQLite 补验真实评论点赞缺失拒绝与 Outbox 失败后关系 / 计数回滚。mock 台账 / 版本 / 缓存与 SQLite 证据不替代 PostgreSQL、真实宿主或并发验收。
- 本批关闭评论三类操作的最终消费日志边界；ContentSubmissionService 并发冲突的后续治理见第 40 节；其余业务和框架来源继续治理。生产开关保持关闭，L2 尚未整体完成。见[本批记录](../records/unified-logging-l2-comment-controller-2026-10-02.md)。

## 40. L2 内容提交并发冲突恢复

- ContentSubmissionService 移除恢复尝试前包含原始异常、用户、操作类型和提交键的 Warning。只有唯一约束冲突后成功从既有记录取得结果，才生成 `content_submission.conflict_resolved` Warning，仅带固定 failureKind。
- 该事件只表示既有记录读取 / 必要重置已返回结果，不表示业务提交成功或外层事务已提交。Processing、Succeeded、DuplicateContent、Conflict 与重置后的 Started 均保持原返回；之后独立的业务失败仍可产生最终 Error。
- 记录不存在时保留原冲突异常；读取或重置失败时保留该失败传播，不输出恢复摘要。评论 Controller 与 API 已治理边界仍负责最终 Error；PostController 编辑的后续治理见第 41 节；帖子版本恢复的后续治理见第 42 节；QuestionController 的后续异常消费治理见第 43 节，不能据本批宣称所有共享消费者已收口。
- 保存点、唯一约束识别、键规范化、指纹、限频、24 小时保留与审计不变。既有冲突识别仍扫描异常 Message / InnerException，本批只约束日志载荷，未把它改成数据库错误码分类器。
- 四模式回归验证安全输出、恢复结果、失败传播与真实评论写入消费者；SQLite 使用真实唯一约束和保存点，控制首次查询不可见以触发冲突，验证保存点回滚保留外层先前写入、事务可继续及最终提交 / 回滚。该证据不代表并发竞态或 PostgreSQL 验收。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-content-submission-2026-10-02.md)。

## 41. L2 帖子编辑 / 置顶最终消费

- 已经项目所有者确认，明确内容校验改用 `PostContentValidationException : ArgumentException`，帖子缺失、编辑上限与分类不可用改用 `PostOperationRejectedException : InvalidOperationException`。原消息、参数名、父类消费契约和响应状态保持；系统设置配置错误及依赖异常不标记成正常拒绝。
- PostController 编辑保留 ArgumentException → 400、InvalidOperationException → 403，置顶保留 InvalidOperationException → 404。明确拒绝保持安静，其余已消费异常分别生成 `post.edit_failed` / `post.top_failed` Error，仅带固定 failureKind。未消费异常仍由 API `http.failed` 记录；正常 4xx BusinessException 继续安静。
- 输入预检、权限查询、提交台账、版本追加、CAS、分类 / 标签计数、重放 / 无变化和事务边界不变。置顶详情读取仍在事务内，读取故障或不可见帖子仍走原回滚；编辑前置查询在 Controller catch 外，其失败继续由 API 处理。
- 旧 / 候选 × Development / Production 回归覆盖真实 PostService、ForumContentWriteService、TranAop、Controller、结果过滤器及内存 API 管道。SQLite 补验真实帖子行在置顶成功时提交、详情故障 / 不可见时回滚、编辑完成台账失败时回滚；其他仓储、台账及版本服务为 mock，不代表全部数据表联合持久化或 PostgreSQL 验收。
- `ForumContentRevisionService.RestorePostAsync` 的后续异常消费治理见第 42 节；QuestionController 的后续治理见第 43 节；其余业务与框架来源继续治理。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-post-controller-2026-10-02.md)。

## 42. L2 帖子版本恢复最终消费

- `ForumContentRevisionService.RestorePostAsync` 在 PostService 更新异常成功转换为原 409 BusinessException 后，对普通 ArgumentException / InvalidOperationException 生成一次 `post.restore_failed` Error，仅带固定 failureKind；明确的 PostContentValidationException / PostOperationRejectedException 继续安静。
- 原响应消息、ContentRejected / EditLimitReached 错误码及消息键、按“次数”选择错误码的旧逻辑保持。分类器不读取异常原文；业务转换仍按原契约读取 Message。如果转换本身失败，恢复服务未消费成功，不先生成 Error，新的失败继续交给 API。
- 前置授权、版本 / 分类 / 标签 / 附件检查及后续快照追加、台账完成在该 catch 外。普通故障继续传播，由 API 记录一次 `http.failed`；原 4xx BusinessException 安静，5xx BusinessException 由 API 记录。PostController 恢复入口不重复记录。
- 恢复规则、CAS、正文 / 封面更新、不可变版本及标签 / 附件引用、UTC 审计、提交台账、重放 / 重复和事务边界不变。四模式回归覆盖真实 PostService、版本服务、写入服务、TranAop 与 Controller；SQLite 验证帖子、版本、标签快照和附件引用四类记录成功提交或共同回滚。分类 / 标签主数据与提交台账依赖仍为 mock，不代表完整持久化、并发或 PostgreSQL 验收。
- QuestionController 参数 / 业务 / 聚合异常最终消费的后续治理见第 43 节；生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-post-restore-2026-10-02.md)。

## 43. L2 问答 Controller 最终消费

- 项目所有者已确认新增 `ForumAnswerContentValidationException : ArgumentException`，仅用于 ForumQuestionService 创建 / 编辑回答的空内容与超长内容检查。原文案、参数名、父类消费和响应契约保持；恢复历史正文沿用原规则，不新增内容校验。
- QuestionController 九个入口统一在原错误响应成功构造后记录故障：普通 ArgumentException 使用 `question.request_failed` Error，仅带 failureKind；已消费的 5xx BusinessException 使用 `http.failed` Error，仅带 statusCode / failureKind。明确内容拒绝与 4xx BusinessException 安静，异常对象、消息原文、提交键、正文及业务身份不进入运行日志。
- AggregateException 的原 Flatten / 单一已知异常解包规则保持；多异常、空聚合及未知异常仍由 API 最终处理。若 Message getter 在构造响应时抛错，Controller 不提前输出 Error，继续按原直接 catch / 异常过滤器语义传播，由 API 记录一次；正常响应的消息参数规范化、错误码和消息键不变。
- 回答创建 / 编辑 / 删除 / 恢复、采纳 / 撤销的提交台账、CAS、不可变版本、附件绑定、采纳审计、通知业务键 / 载荷与事务边界不变。读取分页、版本权限、正常拒绝和成功重放保持原义，不新增重试或补偿。
- 四模式回归覆盖真实 ForumQuestionService、问答仓储、ContentSubmissionService、ReliableOutboxService、TranAop、Controller、结果过滤器及内存 API 管道。SQLite 验证回答、版本、附件及引用、采纳记录、台账和 Outbox 的提交 / 回滚；包含通知实际写入后再抛错。不代表 PostgreSQL、并发请求、真实宿主、附件存储或消息投递验收。
- PollController / PostPollService 投票消费边界的后续治理见第 44 节；其余业务与框架来源继续治理。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-question-controller-2026-10-02.md)。

## 44. L2 投票 Controller 最终消费

- 项目所有者已确认新增 `PollInputValidationException : ArgumentException`，仅标记 PostPollService 查询 / 投票 / 关闭的四处现有帖子与选项 ID 校验。原消息、参数名、父类消费和 HTTP 状态保持，不新增业务规则。
- PollController 三个入口在原响应成功构造后，对其他 ArgumentException 生成 `poll.request_failed` Error，仅带 failureKind；已消费的 5xx BusinessException 使用 `http.failed` Error，仅带 statusCode / failureKind。明确参数拒绝与 4xx BusinessException 安静，原错误码 / 消息键保留，原未复制的 MessageArguments 不新增。
- 未消费的 IO / 超时 / 取消 / InvalidOperationException 等继续由 API 最终处理。Controller 不新增聚合异常解包；事务 AOP 现有单一聚合解包保持。异常 Message getter 若使响应构造失败，Controller 不提前输出 Error，API 记录最终失败。
- 投票记录、选项 / 总计数、截止判断、作者权限、UTC 审计、匿名查看和返回载荷保持；重复投票 / 再次关闭仍返回原 409。写入后的帖子详情刷新仍在原事务内，刷新失败或帖子不可见会回滚，不因已生成过写入而提交。
- 四模式回归使用真实 PostPollService、PostService、ForumProfile、BaseRepository、TranAop 与 SQLite 的 Post / PostPoll / PostPollOption / PostPollVote 表，验证成功提交、阶段故障 / 刷新拒绝回滚、排序 / 比例 / 总数回退和唯一约束故障归属。辅助标签 / 问答依赖为 mock；唯一约束用受控前置查询触发，不代表真实并发、PostgreSQL、认证 / MVC 模型验证或 HTTP 宿主验收。
- LotteryController / PostLotteryService 抽奖消费边界的后续治理见第 45 节；其余业务与框架来源继续治理。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-poll-controller-2026-10-02.md)。

## 45. L2 抽奖 Controller 最终消费

- 项目所有者已确认新增 `LotteryInputValidationException : ArgumentException`，仅标记 PostLotteryService 查询 / 手动开奖的两处非正帖子 ID 校验。消息、参数名和 HTTP 响应保持；自动开奖的同类检查继续抛原 ArgumentException，由任务边界按故障处理。
- LotteryController 两个入口在原响应成功构造后，对其他 ArgumentException 输出 `lottery.request_failed` Error，仅含 failureKind；已消费的 5xx BusinessException 输出 `http.failed` Error，仅含 statusCode / failureKind。明确参数拒绝、模型 / 登录预检、4xx 业务拒绝和正常成功保持安静，错误码 / 消息键及不复制 MessageArguments 的行为不变。
- 未消费异常继续交给 API；Controller 不新增聚合异常解包，TranAop 原有单一聚合解包保持。Message getter 若使响应构造失败，Controller 不提前输出日志，API 记录最终失败。服务不新增 catch 或失败日志；自动开奖仍由 PostLotteryJob 生成既有批次摘要，任务中的 4xx 业务异常仍计作失败，扫描失败继续向外传播。
- 参与池筛选、同一作者最早父评论、中奖人数上下限、内容快照、作者权限、一小时手动门槛、截止时间与审计字段不变。自动开奖按配置截止时间筛选及留痕；手动空池返回 409，自动空池完成结算且不入队通知。重复开奖仍返回 409，不新增幂等成功、重试或补偿。
- 中奖记录、开奖主体、通知 Outbox 入队和详情刷新继续处于原事务内；Outbox 写入后或详情刷新失败会共同回滚。通知业务键 / 任务键、接收者、模板、目标、身份和时间保持，不将日志裁剪作用于权威载荷。
- 发现既有 SQLite 时间边界：自动截止时间读回为 Unspecified，ReliableOutboxRepository 按本地时区调用 ToUniversalTime；在本机 Asia/Shanghai 下，Outbox 信封时间相对中奖 / 通知载荷时间提前 8 小时。本批保留并记录现状，不将日志回归视为 UTC 契约修复；时间规范化需独立确认。
- 四模式回归使用真实 PostLotteryService、PostService、ForumProfile、BaseRepository、ReliableOutboxService / Repository、TranAop、PostLotteryJob 及 SQLite 五表，覆盖成功提交、晚期失败回滚、空池、批次部分失败与继续处理；辅助标签 / 投票 / 问答依赖为 mock，API 为内存管道。不代表 PostgreSQL、真实并发开奖、认证 / MVC 模型绑定、宿主或通知投递验收。
- TagController / TagService 创建与更新最终消费的后续治理见第 46 节。其余业务与框架来源仍待治理，生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-lottery-controller-2026-10-02.md)。

## 46. L2 标签创建 / 更新最终消费

- 项目所有者已确认新增 `TagNameConflictException : InvalidOperationException`，仅标记 TagService 创建 / 更新中未删除标签的重名拒绝。保留原“标签名称已存在”文案、父类消费及 400 响应；不根据异常消息区分拒绝与故障。
- TagController 两个入口在原响应成功构造后，为其他 InvalidOperationException 生成 `tag.request_failed` Error，仅带 failureKind。重名拒绝、模型 / ID 预检、目标缺失及正常成功保持安静；日志不包含异常对象 / 原文、标签名称 / slug / 描述 / 颜色或操作人身份。依赖故障仍返回原 400 与消息，不在本批调整响应语义。
- Service 不新增 catch / 日志；其他异常和响应 Message getter 失败仍向 API 传播。Controller 不新增聚合异常解包，普通 ArgumentException / IO / 超时 / 取消等仍由 API 最终处理，不能把它们统称为正常输入拒绝。
- 名称去空白、重名排除已删除但包含禁用标签、更新排除自身、slug 规范化与后缀避重、字段与审计写入保持。创建 / 更新没有事务特性，写入前故障不产生该次写入；仓储实际写入后再抛错时，已提交数据继续保留，不因日志治理增加回滚、重试或补偿。GetOrCreateTagAsync 及其他标签入口未调整。
- 六组四模式回归覆盖真实 TagService、BaseRepository、UnitOfWorkManage / TranAop、SQLite Tag 表及内存 API / 结果过滤器。正常拒绝、阶段故障、实际写入后故障、成功数据和安全日志归属均已验证；合成 BusinessException(404) 在 DefaultHttpContext 未开始响应时触发既有框架重抛，旧 sink 仍收到原异常，候选仅有安全未分类摘要，单独作为未治理框架边界留痕，不计入标签安全消费完成范围。
- 不代表 PostgreSQL、并发创建、MVC 认证 / 模型绑定或真实 HTTP 宿主验收。CategoryController 创建 / 更新最终消费的后续治理见第 47 节；L2 尚未整体完成，`RadishLogging.Enabled=false` 保持，见[本批记录](../records/unified-logging-l2-tag-controller-2026-10-02.md)。

## 47. L2 分类创建 / 更新最终消费

- 项目所有者已确认将 CategoryController 私有 ResolveCategoryLevelAsync 的父分类缺失表示为可空层级；创建 / 更新入口直接返回原 400 与“父分类不存在”，不生成运行日志。没有新增共享异常类型或公共接口，依赖抛出的同文案 InvalidOperationException 不被视为明确业务拒绝。
- 两个入口消费的其他 InvalidOperationException 在原响应成功构造后输出 `category.request_failed` Error，仅含 failureKind，不传异常对象 / 原文、分类字段、附件标识或操作人身份。响应继续为原 400 + 消息；Message getter 导致响应构造失败时不提前记录，由 API 最终处理。
- 更新目标查询及实体到 Vo 的映射仍在 try 外，此处 InvalidOperationException 继续传播至 API 并返回原 500；父分类查询 / 映射与写入仍在原 catch 内。其他异常、聚合与 BusinessException 不新增本地消费，也不调整 API 契约。
- 名称去空白、slug 的空白回退 / 小写 / 空格替换、描述原样保留、附件标识、层级和审计字段不变。禁用父分类仍可使用，已删除父分类仍不可用；顶级为 0，其余按原 Math.Max 计算。模型 / ID / 自身父级预检、目标缺失、正常成功均安静。
- 更新预读后实际影响 0 行仍返回成功；创建 / 更新没有事务特性，实际写入后再抛错仍保留已写数据。本批不新增行数判定、事务、重试、补偿或层级规则。
- 七组四模式回归覆盖真实 BaseService、BaseRepository、ForumProfile、UnitOfWorkManage / TranAop、SQLite Category 表及内存 API / 结果过滤器，验证父分类拒绝、映射 / 仓储故障归属、写入前后状态和实际 0 行更新。不代表 PostgreSQL、真实并发、MVC 认证 / 模型绑定或 HTTP 宿主验收；上批内存 404 框架分支保留独立证据。
- ReactionController 单目标 / 批量汇总、切换回应及 Service 重试日志的后续治理见第 48 节；L2 尚未整体完成，生产开关继续关闭，见[本批记录](../records/unified-logging-l2-category-controller-2026-10-02.md)。

## 48. L2 表情回应最终消费与冲突重试

- ReactionController 三个入口在原错误响应成功构造后，对消费的 5xx BusinessException 记录一次 `http.failed` Error，仅带 statusCode / failureKind。4xx 业务拒绝、模型预检和正常成功保持安静；原响应消息、错误码及不复制 MessageKey / MessageArguments 的行为不变。
- ReactionService 原冲突 Warning 改为 `reaction.retrying`，仅带固定 failureKind，不传异常、目标 / 用户身份或表情值。它表示选择一次重试，不表示重试成功或事务已提交；二次唯一冲突仍转原 409，不追加 Error，二次依赖故障仍交给 Controller / API 最终处理。
- 原唯一冲突过滤器仍通过异常 ToString 判断三个既有标记；这是保留的控制流判定，日志分类与输出不读取原文。未新增冲突识别规则、次数、退避、保存点或 PostgreSQL 事务恢复。其他异常继续传播；Message getter 导致 Controller 响应构造失败时不提前记录 Error。
- 单目标 / 批量汇总的规范化、有效 ID 去重、100 目标上限、空目标键、分组 / 排序 / 缩略图保持；汇总原本不检查目标存在或可见性。Toggle 的目标 / 贴纸可用性、10 种上限、软删除审计和 UTC 写入保持，返回汇总仍在原事务内，实际写入后或刷新失败继续回滚。
- 真实 SQLite 发现既有恢复偏差：BaseRepository.QueryFirstAsync 默认排除软删除记录，Service 查找已删除回应不能命中，随后两次 Add 触发唯一约束并返回 409。添加 / 取消可提交，再次添加仍失败；本批只留痕，不将日志治理视为恢复功能修复。仅用显式代理提供已删除行时，另行验证原恢复分支的写入 / 回滚，不冒充真实仓储恢复成功。
- 七组四模式回归覆盖真实 ReactionService、BaseRepository、TranAop、SQLite 的 Reaction / Post / Comment / StickerGroup / Sticker 五表及内存 API 管道。受控隐藏预读触发真实唯一约束，验证重试成功或耗尽；模拟第二次依赖故障与刷新失败验证日志所有权和回滚。附件 URL 解析为 mock；不代表真实并发、PostgreSQL、MVC 认证 / 模型绑定或 HTTP 宿主验收。
- 下一项先核对 StickerController 创建分组 / 单表情、批量保存 / 排序的异常消费，确认方案后实施；其余业务与框架来源仍待治理。生产开关保持关闭，L2 尚未整体完成，见[本批记录](../records/unified-logging-l2-reaction-controller-2026-10-02.md)。
