# Outbox 最终输出本地收口

日期：2026-11-11（Asia/Shanghai）。实施基线：本地 `dev` 的 `dd892696`。所有者在本任务明确批准[第 10.7 节](../features/unified-logging-l3-ingest-plan.md#l2-outbox-output)，本记录只覆盖其实现和本地回归。

## 实现与边界

- 新增 `OutboxLogSink`，接入旧链代码 / 配置 WriteTo、配置 AuditTo 和候选 JSONL。分派与状态摘要按两个精确来源分别重建，固定普通 Job 事件，排除外层来源、诊断标记和无关字段。契约见[第 82 节](../features/unified-logging-contract.md#l2-outbox-final-output)。
- Repository 绑定固定来源和本次 `failureKind`，包括缺省 null；Service 经仓储接口显式传递现有分类器结果。规范 `operationId` 继续使用原 SHA-256 派生算法及 main / chat 库域。未改数据库结构、关联算法、业务事务、重试 / 死信、审计、租约或部署开关。
- 分派数量仍是入队数。实际状态更新影响行后才输出重试 / 死信，零行与重复执行安静。WriteTo 消费 sink 故障，AuditTo 保留异常传播，候选链使用有限应急；输出失败不撤销已完成的状态更新及入队。

## 验证结果

在本机 macOS 的 .NET 10 环境执行，未安装依赖、启动项目服务或容器。复用已有 `HttpBoundaryCapture`、main / chat 内存 SQLite 和 Hangfire 客户端替身；不是实际 Hangfire 调度或网络端到端验收。

| 验证 | 结果 |
| --- | --- |
| `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror` | 通过，0 warning / 0 error |
| 定向 .NET 回归（下方命令） | 323 通过、0 失败、1 跳过；含本次新增 27 项最终输出回归 |
| `npm run check:logging-contract` | 70 通过 |
| `npm run check:repo-quality:changed`、`git diff --check` | 通过；记录索引 381 行有超过 300 行建议值的非阻断提醒，未超过 500 行硬上限 |

.NET 定向命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~OutboxFinalOutput|FullyQualifiedName~OutboxSink|FullyQualifiedName~Outbox_Should|FullyQualifiedName~PermanentFailure_Should|FullyQualifiedName~Dispatcher_Should|FullyQualifiedName~ReliableOutboxServiceTest|FullyQualifiedName~ReliableOutboxJobTest|FullyQualifiedName~ReliableOutboxRepositoryTest|FullyQualifiedName~HangfireOutputLoggingTests|FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~CleanupLoggingTests|FullyQualifiedName~ServiceCleanupLoggingTests|FullyQualifiedName~RuntimeLoggingTests' --logger 'trx;LogFileName=outbox-final-output.trx' --results-directory /tmp/radish-outbox-results
```

| 本次新增覆盖 | 观察 |
| --- | --- |
| 两环境、旧链配置 WriteTo / AuditTo、候选 JSONL | 外层 `Diagnostic=true` 不再使登记事件消失；事件无原始异常、无关计数、scope 关联值及合成私密文本 |
| 实际 ExecutionJob → Service → Repository → SQLite | main / chat 均按真实 Pending / DeadLetter 状态输出，attempt 为 1 / 2，关联值与各库各任务派生值一致。永久失败仍保留原审计摘要，运行日志只输出安全分类 |
| 直接 Repository 调用 | 缺省 null 和无效分类都不继承外层 `failureKind`；Service 的 IO / 永久失败分别得到 io / other。严格 Service mock 另验证 timeout 分类和原重试时间 |
| 分派和入队故障 | 空批次安静，两库各入队一次后 count=2；第二次入队失败传播原异常，已领取记录保持 Processing，未虚构完成摘要 |
| 重复和条件更新零行 | 重复执行及无效状态保持安静；两库通过 AOP 在 UPDATE 前将状态改为 Succeeded，原条件更新影响 0 行，attempt 保持 0，无状态日志 |
| 输出故障 | 旧链 WriteTo、AuditTo 及候选分别保持原故障归属；死信写入与入队已发生，候选仅生成有限 `pipeline.output_failed` 应急摘要 |
| 未知事件及来源隔离 | 跨来源事件码和错误等级重建为安全 unknown，生产候选按诊断规则过滤低等级 unknown；邻接来源不匹配 |
| 字段合同与代码 console | 非法计数、时长、库域、分类、outcome 和非规范 GUID 不输出；适配保留原时间、等级及类型化 trace / span。两环境代码 console 均使用三类固定模板 |

## 未验证与下一步

- 筛选同时匹配到 `ReliableOutboxPostgresIntegrationTest.Outbox_ShouldPreserveTransactionAndClaimSemantics_OnPostgreSql`；本任务未配置 PostgreSQL 测试环境，按原条件跳过，不能记作通过。
- 真实 Hangfire 调度、PostgreSQL 并发、日志介质及 collector 联验仍按日志专题的对应门禁推进。SQLite AOP 零行覆盖不证明 PostgreSQL 并发或租约归属已修复。
- 本批关闭 Outbox 的本地最终输出切片，不关闭 L2 整体或日志 L1–L6；下一组按既有有限清单核验奖励、币与经验。未进行提交、推送、发布或生产切换。
