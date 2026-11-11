# 奖励、币与经验最终日志输出核验与实施

日期：2026-11-11（Asia/Shanghai）。基线：`dev` 的 `38d15bd3`（Outbox 最终输出修复）。初次核验仅确认实际缺口及修复范围，未修改本组产品代码；其后所有者批准并完成下文[本地实施](#implementation)。范围见[衔接计划第 10.8 节](../features/unified-logging-l3-ingest-plan.md#l2-reward-output)。

## 实际调用与有限范围

| 生成来源 | 实际入口 / helper 与消费边界 |
| --- | --- |
| `RewardRuntimeLog` | CoinService 的 GrantCoin / GrantCoinOnce / ConsumeCoin / Transfer 共用乐观锁重试；币批次消费单项异常后汇总。ExperienceService 的 GrantExperience / GrantExperienceOnce、初始化、等级配置缓存和批次分别记录重试、已消费失败、冲突、缓存回退和结果。probe 通过真实发放 / 批次 / 配置方法到 helper；不直接调用 helper 冒充业务链 |
| `CoinService` | TransferAsync 资产写入后幂等完成抛错，再调用完成记录成功后输出 recovered；仍只发生一次资金写入，恢复仅表示调用返回 |
| `OperationIdempotencyService` | CompleteSuccessAsync / CompleteFailureAsync 查不到记录时输出 completion_missing，并按原行为返回；无更新调用。共享方法其他调用方属于同一精确来源 |
| `CoinRewardService` | CheckRewardExistsAsync 消费查询异常，记录 existence_check_failed 并返回 true；六类奖励入口的外抛失败仍交 ReliableTaskProcessor → Outbox，已完成边界沿契约 §17 和 Outbox §82 |
| `ExperienceService` | GetUserRankAsync 消费异常返回 0；GetLeaderboardAsync 聚合缺失用户并维持原分页口径；GetGovernanceActionsAsync 经 DeserializeSnapshotItems 对损坏字段返回空列表 |
| `ExperienceCalculator` | CalculateAllLevels / ClearCache 的缓存读取、写入及清除异常保留公式计算或返回，LogCacheFallback 记录固定操作 |

六个来源合计 13 个已登记事件码，见第 10.8 节字段表。Coin / Experience 的正常查询、人工调账 / 冻结 / 解冻和 Controller 已有消费出口沿原契约，不重复引入新日志；商城、库存和支付口令属于下一分组。

## 最终出口观察

探针通过实际方法、原依赖替身和 `HttpBoundaryCapture` 接到 Development / Production 的旧链配置 WriteTo / AuditTo 与候选 JSONL。每种路线分别检查正常、字段污染、来源冲突和诊断标记；两环境另核验旧链代码 console。只使用合成标记，无业务或生产数据。

| 条件 | 已复现结果 |
| --- | --- |
| 正常上下文 | 13 个事件码及被测领域 / 操作字段成立，但旧链事件均没有固定 SourceContext。无外层干扰时已有生成端测试看不出出口缺口 |
| 字段污染 | 普通 scope 的 operationId、statusCode 被候选 JSONL 接受；旧链还保留合成 privatePayload。没有本次异常的 retrying、batch_completed、completion_missing 和 leaderboard_incomplete 会继承外层 failureKind=database；本次明确绑定的 io / argument / invalid-operation 等仍优先 |
| 来源冲突 | 外层 SourceContext=Radish.Service.Internal.ServiceCleanupSummary 时，所有被测事件被其适配器归为 runtime.unclassified，业务字段消失。Info 批次结果在生产候选链进一步被作为诊断过滤；旧链代码 console 同样出现服务清理 unknown 模板 |
| 仅 Diagnostic=true | Production 候选链丢掉本次所有被测奖励 / 币 / 经验事件，包括真实已消费失败；Development 候选和旧链保留，但被标为诊断 |
| 输出故障 | WriteTo 消费 sink 故障，候选生成有限 pipeline.output_failed。AuditTo 仍抛包含原 sink 故障的 AggregateException：币批次已写入一次后向调用方抛错；经验已消费仓储失败的路径不再返回 false；缓存读取失败后的日志再抛错时，数据库回退尚未执行 |

被测方法的业务结果同时断言：币批次部分成功仍返回一条流水；经验一拒绝一成功返回 1 且 completed 的 outcome=partial；币和经验一次冲突后恢复；经验冲突 / 消费失败 / 初始化失败保持 Skip 或 false；转账完成重试两次而资金写入一次，余额替身为 77 / 123；奖励存在查询返回 true；排名失败返回 0；排行榜 DataCount=3 但实际一条、rank=1；坏快照字段为空且正常字段保留；等级重算保留审计结果；计算器三个缓存操作失败后公式结果保持一致。

以上资产和审计写入通过 mock 调用或内存对象验证，没有证明真实账本持久化、事务回滚、数据库并发或 Redis 行为。外层 scope 为主动注入的合成条件，不表示生产已经发生相同污染。

## 验证与证据边界

- 临时探针位于 `/tmp/radish-reward-output-audit/RewardOutputProbe.cs`，经同目录 `Probe.targets` 的 `CustomAfterMicrosoftCommonTargets` 导入测试项目；未改仓库产品或正式测试源文件。复用三组既有测试 fixture 的依赖配置，通过真实 public 方法调用，不使用反射调用私有业务方法。
- 严格编译并执行最终探针：80 项通过、0 失败、0 跳过。其中 72 项覆盖三组业务链 × 六种出口环境路线 × 四种上下文条件，6 项验证输出故障，2 项验证代码 console 来源冲突。这些通过证明观察可复现，不表示缺口已修复。
- 初次探针装配曾复用关闭缓存的 fixture，并耗尽一个 Moq SetupSequence；修正测试配置与替身后再执行，未据探针装配失败判定产品缺陷。临时 nullable 警告已修正，最终使用 `--warnaserror` 通过。
- 移除命令行临时导入后，以 `--no-restore --warnaserror` 重新编译，未出现编译错误或 warning；既有奖励 / 币流转 / 账户治理 / 奖励入口 / 经验查询治理及相关服务回归最终在宿主环境 232 项全部通过，0 失败、0 跳过。
- 沙盒回归曾在既有 `CoinAdjustment_ShouldRollbackAndLeaveOneErrorToHttpBoundary` 内无进展；先中断并拆分定位，再用 30 秒无进展超时终止并留存 sequence（不采集内存转储）。同一测试的四个组合在宿主约两秒通过，随后相同已编译程序集的完整定向范围在宿主约 11 秒通过。沙盒中止不计为通过，也未据此认定产品代码缺陷；底层等待机制尚未进一步定位。
- `npm run check:repo-quality:changed` 与 `git diff --check` 通过；记录索引篇幅有非阻断建议提醒。
- 没有启动项目、容器或访问真实服务。币 / 经验真实事务、PostgreSQL 并发、Redis、Hangfire 调度、采集介质与 collector 保留原门禁。

探针命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror -p:CustomAfterMicrosoftCommonTargets=/tmp/radish-reward-output-audit/Probe.targets --filter 'FullyQualifiedName~RewardOutputProbe' --logger 'trx;LogFileName=reward-output-audit.trx' --results-directory /tmp/radish-reward-output-audit/results
```

既有回归最终命令（不再导入探针，使用沙盒中已完成严格编译的程序集，在宿主执行）：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~AccountGovernanceLoggingTests|FullyQualifiedName~CoinRewardEntryLoggingTests|FullyQualifiedName~ExperienceQueryGovernanceLoggingTests|FullyQualifiedName~Services.CoinServiceTest|FullyQualifiedName~Services.ExperienceServiceTest|FullyQualifiedName~Services.ExperienceCalculatorTest|FullyQualifiedName~Services.OperationIdempotencyServiceTest' --blame-hang-timeout 30s --blame-hang-dump-type none --logger 'console;verbosity=normal' --logger 'trx;LogFileName=reward-existing-host.trx' --results-directory /tmp/radish-reward-output-audit/results
```

## 结论与停止线

本组需要同时固定生成身份与最终事件字段，不能只新增全局白名单或关闭诊断过滤。有限实施范围是上述六个来源和 13 个事件码，保持原业务返回、重试、审计、事务及输出故障归属。新日志关联键、错误响应改造、资金 / 经验规则、全仓日志重构和真实环境建设均不进入此批。

核验时第 10.8 节仍为待批准提案；该轮探针不关闭最终输出门禁。随后所有者明确批准实施，完成情况见下节；L2 整体及真实链路门禁继续保留。


<a id="implementation"></a>

## 已批准范围的本地实施

实施基线为 `419513d6`。2026-11-11 所有者确认按第 10.8 节完成修复、回归及文档同步；本批仅修改日志生成身份和最终输出适配。

- 六个生成来源绑定固定 `SourceContext`，新增 `RewardLogSink` 接入旧链 WriteTo / AuditTo 与候选日志既有接线。在上下文合并后按精确来源、事件码、等级及合法业务枚举重建，固定 application / 非诊断属性、文本和事件字段，保留原等级、Serilog 时间与类型化关联。
- 重试、批次完成、幂等记录缺失和排行榜缺失用户不继承外层 failureKind；实际消费异常的事件才保留封闭分类。自由 operationId、字符串 trace / span、无关计数、业务载荷、原模板和异常对象均剔除。未知 / 错配事件及非法枚举走安全 unknown；数值类型和范围非法时丢弃该字段。
- 正式测试复用三组原 fixture，通过真实公开方法覆盖批次、发放、重试恢复、冲突、初始化、转账、完成记录缺失、奖励存在检查、排名 / 分页 / 快照及缓存三个操作。事务、账本、返回、审计和异常归属不作变更。字段矩阵另外覆盖全部登记组合、非法身份 / 等级 / 枚举、时间和关联类型、数值边界、邻接来源与代码 console。
- 输出故障回归保持 WriteTo 消费、AuditTo 聚合异常传播、候选有限应急；断言币批次已写入、经验失败不再返回 false、缓存回退被 AuditTo 中断等既有后果。生成点业务结果与 sink 接收到的最终事件同时验证，不只比较渲染文本。

### 本批验证

- `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror` 通过，0 warning、0 error。初次编译发现新增测试缺少命名空间引用及 Scalar 名称冲突，已在测试代码内修正后重新严格构建。
- 最终输出定向范围 `FullyQualifiedName~RewardOutputLoggingTests|FullyQualifiedName~FinalOutput_Should` 在沙盒通过 75 项，0 失败、0 跳过。该筛选也包括既有最终输出用例，不将 75 全计为新增测试。
- 关联范围首次在沙盒内触发 30 秒无进展终止，sequence 指向既有 `AccountGovernanceLoggingTests.CoinAdjustment_ShouldRollbackAndLeaveOneErrorToHttpBoundary`；该次中止不计通过，也不据此认定产品缺陷。未采集内存转储。
- 首次宿主成组运行在测试启动阶段无进展，主动中止后拆分范围、移除 blame 采集并启用正常进度。相同已编译程序集的奖励与相关服务 283 项全部通过（含本次新增 51 项），Controller 最终输出及直接消费 66 项全部通过。启动等待的底层原因未确认，不据成功重试归因产品或运行环境。
- Outbox / Controller 及 ApplicationOperation 关联回归在宿主完成：413 项通过、0 失败、1 跳过，耗时约 5.68 分钟；唯一跳过为未提供 PostgreSQL 环境的 `ReliableOutboxPostgresIntegrationTest.Outbox_ShouldPreserveTransactionAndClaimSemantics_OnPostgreSql`。该跳过不作为 PostgreSQL 验收通过。各筛选含部分重叠用例，不将各组数量相加为独立覆盖数。
- `npm run check:repo-quality:changed` 和 `git diff --check` 通过。事件契约 804 行与记录索引 383 行有非阻断篇幅建议提醒，未达到硬上限；未借本批重新拆分其他已完成来源。

本批本地切片完成，契约、衔接计划和当前进度同步；L2 整体继续按既有门禁推进。

本批未安装依赖、启动项目服务 / 容器或执行浏览器 Smoke。正式配置 File / DB sink 的介质故障、真实资金 / 经验事务、PostgreSQL 并发、Redis、Hangfire 调度和 collector 均保留原验收条件；不以 mock 或内存 HTTP 边界代替真实运行证据。


复跑入口（先严格编译；服务 / Controller 内存 HTTP 用例在宿主完成）：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~RewardOutputLoggingTests|FullyQualifiedName~FinalOutput_Should'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~RewardOutputLoggingTests|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~AccountGovernanceLoggingTests|FullyQualifiedName~CoinRewardEntryLoggingTests|FullyQualifiedName~ExperienceQueryGovernanceLoggingTests|FullyQualifiedName~Services.CoinServiceTest|FullyQualifiedName~Services.ExperienceServiceTest|FullyQualifiedName~Services.ExperienceCalculatorTest|FullyQualifiedName~Services.OperationIdempotencyServiceTest'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Outbox|FullyQualifiedName~ControllerLoggingTests|FullyQualifiedName~ApplicationOperationLoggingTests'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ControllerFinalOutputTests|FullyQualifiedName~ControllerConsumerOutputTests|FullyQualifiedName~ApplicationOperationOutputTests'
npm run check:repo-quality:changed
git diff --check
```
