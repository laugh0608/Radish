# 2026-11-10 功能完成主线收口

日期按 Asia/Shanghai。项目所有者确认收口方案，本轮只修改现有规划和专题，不改代码、不执行环境操作、不提交或推送。

- 日志保留 L1–L6，剩余工作按最终出口 / 诊断、真实采集 / 持久化、Console 查询 / 站内告警、迁移 / 候选四批推进。明确每批阻塞、结束条件、后置及停止扩张规则，未标记任何阶段通过。
- 日志退出后接续 Native P8-C / D / E 与客户端连接；资源受阻时先记录并评审重排，不自动扩大环境工程。对应运行、官方入口和分发证据分别判断。
- 原生字段、诊断必需项、运行目标与具体操作仍待下一工作包落实。已有实现和历史失败保留，来源后置不等于正式发行放行。
- `8d6cc63a` 为本地已集成成果；本轮没有核验线上状态或取得新运行证据。根入口、代码、配置、依赖与机器契约未修改。

已运行 `npm run check:docs`：973 个文件文本卫生、894 个 Markdown / 1522 个相对链接通过；`npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。记录索引为 379 行，保留既有超出 300 行建议值的提醒；未借本轮改动拆分历史索引。未运行产品构建、测试、服务 / 容器、平台或部署验收，静态结果不代替阶段出口。

启动时 dev 工作树干净。当前仅有未提交文档；未暂存、提交、推送或启动后台进程。

<a id="l2-job-output-audit"></a>

## 同日接续的 L2 Job 最终输出核验

候选为 `dev` 的 `546b0968`。使用真实 `ShopJob.CancelTimeoutOrdersAsync`、固定替身仓储和订单服务，以及现有 `HttpBoundaryCapture`。探针分别调用 `SerilogSetup.ConfigureLegacy` 的配置 WriteTo / AuditTo 和 `RuntimeLoggingConfiguration.Configure` 的候选 JSONL，各覆盖 Development 与 Production。使用合成上下文字段，无业务数据。

| 核验 | 观察 |
| --- | --- |
| 空批次和部分失败 | 空批次返回 0 且安静。四个订单依次正常返回 true、false、拒绝及 IO 异常时返回 2，输出一条 `job.batch.failed`。摘要记录 processed 2、updated 1、rejected 1、failed 1、outcome partial、failureKind io。scope 内同名计数未覆盖生成点计数 |
| 旧链上下文 | 两个配置出口均保留无关自定义字符串、`statusCode` 和 scope 的 `operationId`。输出事件不带原异常对象 |
| 候选上下文 | JSONL 剔除了自定义字符串，仍输出与 Job 无关的 `statusCode` 和 scope 的 `operationId`。额外 scope 的 `Diagnostic=true` 使候选生产模式整条不输出，候选开发诊断模式将其标为诊断事件 |
| 输出失败 | WriteTo 消费 sink 故障，Job 仍返回 2。AuditTo 传播聚合异常，展开后包含原 sink 故障实例。候选 writer 故障仍返回 2 并产生安全的 `pipeline.output_failed`。各场景后续调用均实际重新查询，锁已释放 |

用 `CustomAfterMicrosoftCommonTargets` 导入临时 targets，仅为 `Radish.Api.Tests` 添加探针源码。执行 `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore`，过滤器为 `FullyQualifiedName~JobFinalOutputProbe|FullyQualifiedName~BusinessJobLoggingTests`。12 项探针和 21 项既有 Job 回归共 33 项通过。初跑中两项 AuditTo 的异常包装断言仅检查一层，按实际聚合结构校正后复跑。移除临时编译参数后单独重跑原 Job 测试，21 项通过。

替身用于验证 Job 返回与调用顺序。真实调度、事务、重试、File / DB sink、网络和 collector 仍需目标环境证据。本轮保留产品代码和机器契约。首组实施范围及字段建议见[衔接方案第 10 节](../features/unified-logging-l3-ingest-plan.md#l2-final-output-first-slice)。

<a id="l2-job-output-implementation"></a>

## 首组 Job 最终输出实施

所有者确认首组后，为 `BusinessJobSummary` 固定来源，在三条日志接线中引入 `BusinessJobLogSink`。旧链代码 console、配置 WriteTo / AuditTo 与候选 JSONL 都经同一精确来源重建。商城取消在含合成 scope 的 Production 候选中保留一条正常失败摘要，旧链和候选均排除无关字段及 scope 关联值。真实业务方法与替身验证原返回、计数、输出故障传播及锁释放。共享 helper 的其余四入口沿既有测试核验，另验证同名抽奖来源透传。

Debug 解决方案 `--no-restore --warnaserror` 构建无警告及错误。定向 .NET 回归 292 项通过，含 20 项新增最终输出验证及既有 Job、Hangfire、应用操作、Controller 和运行日志适配测试。`npm run check:logging-contract` 70 项通过。该证据来自本地日志出口和测试替身，运行调度、真实 File / DB sink 和 collector 仍待对应验收。输出契约见[第 78 节](../features/unified-logging-contract.md#l2-business-job-final-output)。

<a id="l2-lottery-job-output-audit"></a>

## 抽奖 Job 最终输出核验

以 `dev` 的 `0ed03b5f` 为基线，通过真实 `PostLotteryJob.ExecuteAutoDrawAsync`、替身仓储和开奖服务及 `HttpBoundaryCapture`，核验 Development / Production 的旧链配置 WriteTo / AuditTo 与候选 JSONL。探针使用合成上下文。

| 核验 | 观察 |
| --- | --- |
| 来源与批次 | 配置 WriteTo 验证 `SourceContext=Radish.Service.Jobs.PostLotteryJob`、`job.batch.failed` 和 `SourceCategory=job` 抵抗外层同名字段。批量值 999 限制为 100，三行记录按两个帖子处理。一项 IO 异常后继续，返回 1，摘要计数为 processed 1、failed 1，outcome 为 partial，failureKind 为 io |
| 上下文字段 | 两个旧链出口带出无关自定义字符串、`statusCode`、`updatedCount` 和 scope 的 `operationId`。候选 JSONL 剔除自定义字符串，仍带出后三项。各出口的成功摘要均继承外层 `failureKind` |
| 诊断开关 | 外层 `Diagnostic=true` 使候选 Production 丢掉整条失败摘要。候选 Development 和两个旧链出口均把该失败摘要标为诊断事件 |
| 异常归属 | 批量值 0 限制为 1，空批次返回 0。扫描异常原样传播，两种情况均无 Job 摘要。输出失败时 WriteTo 和候选仍返回 1，候选产生安全的 `pipeline.output_failed`。AuditTo 抛出的聚合异常保留原 sink 故障实例 |

用 `CustomAfterMicrosoftCommonTargets` 仅向 `Radish.Api.Tests` 导入临时探针。`dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore` 过滤 `LotteryFinalOutputProbe`、`BusinessJobLoggingTests` 和 `LotteryControllerLoggingTests`，共 78 项通过，其中 13 项为探针。既有抽奖测试含本地 SQLite 事务、审计和 Outbox 回归。探针验证到本地输出，真实 Hangfire 调度、生产介质和 collector 仍待对应环境验收。实施范围见[衔接方案第 10.4 节](../features/unified-logging-l3-ingest-plan.md#l2-lottery-job-output)。

<a id="l2-lottery-job-output-implementation"></a>

## 抽奖 Job 最终输出实施

所有者确认后，在既有 `BusinessJobLogSink` 接线中为 `PostLotteryJob` 增加独立事件与字段白名单。Production 候选在合成外层 `Diagnostic=true` 下仍输出普通完成和失败摘要。旧链与候选都移除无关计数、scope `operationId` 和成功事件继承的 `failureKind`，保留本批计数及类型化 trace / span。业务生成端、事务及调度代码沿用原实现。见[事件契约第 79 节](../features/unified-logging-contract.md#l2-lottery-job-final-output)。

Debug 解决方案 `--no-restore --warnaserror` 构建无警告及错误。定向 .NET 回归 327 项通过，覆盖两环境的配置 WriteTo / AuditTo、候选 JSONL、代码 console、抽奖 SQLite 业务回归和相邻日志适配。`npm run check:logging-contract` 70 项通过。配置输出用测试 sink / writer，真实 Hangfire 调度、生产介质和 collector 仍待目标环境验收。

<a id="l2-cleanup-job-output-audit"></a>

## 文件及记录清理 Job 最终输出核验

以 `dev` 的 `4a5555a2` 为基线，通过实际七个 Job 入口、替身仓储、临时文件和 `HttpBoundaryCapture`，覆盖 Development / Production 的旧链配置 WriteTo / AuditTo 与候选 JSONL。另以旧链代码 console 检查文件来源错配。scope 值均为合成数据。

| 核验 | 观察 |
| --- | --- |
| 文件业务与计数 | 软删除混合批次返回 2，实际移动 1、缺失 1、IO 失败 1，输出一条 Error。仅缺失时返回 1 并输出 Warning。临时文件移动时保留分片，回收站删除过期文件，孤立附件路径缺失时仍更新已删除标记。四入口空批次返回 0，均无摘要 |
| 文件来源 | 无 scope 的旧链文件警告缺少 `SourceContext`。加普通外层来源后，配置旧链带出该值。外层来源设为 `BusinessJobSummary` 时，配置 WriteTo 和 Production 候选把该警告改为 `runtime.unclassified`，配置 WriteTo 的原缺失计数消失。两种环境的旧链代码 console 同样改成未识别事件文本 |
| 记录来源与字段 | 收件箱仅删 3 条关系时输出完成，并另发一条 `count=2` 的容量告警。Wiki 和聊天分别返回并输出 4、5。三类精确来源在旧链优先于外层同名字段，但三个出口仍带出无关计数。容量告警还带出外层 `durationMs` |
| 共有字段和诊断 | 旧链两个配置出口带出自定义字符串、`statusCode`、`operationId` 和跨来源计数。候选剔除自定义字符串，仍带出后三类字段。文件仅缺失警告、正常完成和记录摘要都可继承外层 `failureKind`。外层 `Diagnostic=true` 时，候选 Production 丢掉文件失败、记录完成和容量告警。候选 Development 与旧链将这些事件标为诊断 |
| 失败归属 | 文件查询失败由 Job 消费，生成一条失败摘要并返回 0。三个记录 Job 的仓储异常按原实例传播，均无本地失败摘要。文件和收件箱输出故障探针中，WriteTo 和候选保留业务返回，候选走有限 `pipeline.output_failed`。AuditTo 的聚合异常仍包含原 sink 故障 |

用 `CustomAfterMicrosoftCommonTargets` 仅向 `Radish.Api.Tests` 导入临时探针。`dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore` 过滤 `CleanupFinalOutputProbe`、`CleanupLoggingTests` 和 `FileCleanupJobTest`，28 项探针和 40 项既有回归共 68 项通过。去掉临时导入后，40 项既有回归再次通过，含过滤器一并命中的 `ServiceCleanupLoggingTests`。真实目录权限故障、Hangfire 调度、生产日志介质及 collector 仍待对应环境验收。修复范围见[衔接方案第 10.5 节](../features/unified-logging-l3-ingest-plan.md#l2-cleanup-job-output)。

<a id="l2-cleanup-job-output-implementation"></a>

## 文件及记录清理 Job 最终输出实施

所有者确认后，文件摘要固定为 `FileCleanupJob` 来源，四个来源由 `CleanupJobLogSink` 按事件限定字段。旧链的文件警告保留原事件和计数。候选 Production 在外层 `Diagnostic=true` 时仍输出清理失败、记录完成和收件箱容量告警。目录异常警告保留分类，只有文件缺失的警告和收件箱容量告警剔除无关字段。见[事件契约第 80 节](../features/unified-logging-contract.md#l2-cleanup-job-final-output)。

Debug 解决方案 `--no-restore --warnaserror` 构建无警告及错误，定向 .NET 回归 327 项通过，含 21 项新增用例。覆盖实际七个入口、旧链代码 console、两环境的配置 WriteTo / AuditTo 与候选 JSONL、临时文件操作、仓储及输出故障归属。`npm run check:logging-contract` 70 项通过。目录异常警告的分类用适配器输入验证。真实目录权限故障、Hangfire 调度、生产日志介质及 collector 留到对应环境验收。
