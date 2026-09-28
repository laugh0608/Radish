# 统一日志 L2：其余奖励入口与直接消费者

日期：2026-09-28（Asia/Shanghai）。本批承接[商城库存与订单履约批次](./unified-logging-l2-shop-fulfillment-2026-09-28.md)，按[统一日志专题](../features/unified-logging-governance-design.md)继续 CoinRewardService 其余入口；稳定契约见[事件契约第 17 节](../features/unified-logging-contract.md)。

## 实现范围

- 帖子点赞、评论点赞、评论发布、评论被回复、神评与沙发奖励去掉逐次成功明细和仅记录再重抛的 catch。每日点赞奖励上限查询去掉用户明细与重复异常日志。
- 保留金额计算、作者与点赞者部分新发放结果、每日上限、业务日期、幂等键、返回流水号和原有失败理由。点赞加成与保留奖励已在前批治理，本批没有改动。
- 核对六类入口的真实直接消费者 ReliableTaskProcessor；上抛异常经 ReliableOutboxExecutionJob 进入既有 Outbox 状态写入，实际更新后由仓储记录重试 / 死信。本批无需修改消费者或 Outbox 的运行规则。
- CheckRewardExistsAsync 的查询失败仍返回 true，避免重复发放；仅在消费点输出固定 `reward.existence_check_failed` Error 和安全 failureKind，去掉业务身份与异常原文。保留原筛选与日期语义，不把 true 解释为查询证明已发放。

## 验证

新增 `CoinRewardEntryLoggingTests` 共 64 项，覆盖旧 / 候选日志与 Development / Production 四种组合：

- 六类奖励的金额、幂等业务键、成功和重复发放返回；部分新发放时的金额与流水号选择。
- 点赞者每日 50 的上限、低于上限的发放及业务日 UTC 半开区间；达到上限不影响作者奖励。
- 每日上限查询失败和奖励入口失败保持同一异常对象；存在性查询的原筛选与失败返回 true、单次安全 Error。
- 使用真实 CoinRewardService、ReliableTaskProcessor、Outbox Service / Repository / ExecutionJob 和内存 SQLite，分别验证六类任务的 Pending 重试、重复执行安静、第二次失败转 DeadLetter，以及奖励重放成功后 Succeeded。
- 捕获全部日志，确认每次失败状态更新只有一个对应事件，无测试敏感载荷或未分类事件。

结果：

- .NET 定向回归 **198 项通过，0 失败，0 跳过**，包含本批 64 项与奖励业务键、币服务、RewardLogging、ReliableTaskProcessor、Outbox Job / Service、BusinessJobLogging、CommentHighlightRealtimeService、RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**。
- API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 沙盒编译完成，VSTest 本地通信端口因 SocketException(13) 被阻止；最小范围提权后运行上述回归通过。测试项目编译保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~CoinRewardEntryLoggingTests|FullyQualifiedName~CoinRewardBusinessKeyTest|FullyQualifiedName~CoinServiceTest|FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~ReliableTaskProcessor|FullyQualifiedName~ReliableOutboxJobTest|FullyQualifiedName~ReliableOutboxServiceTest|FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~CommentHighlightRealtimeServiceTest|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

Outbox 状态使用真实内存 SQLite；币发放、经验、通知和其他业务依赖使用 mock，不替代真实币账本事务 / 并发、PostgreSQL、完整奖励链运行态或生产验收。没有安装依赖、启动真实宿主 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关保持关闭。

L2 仍未整体完成，下一批继续经验查询 / 人工复核 / 等级治理，按调用链核对最终日志责任，保留原有业务规则与权威记录。
