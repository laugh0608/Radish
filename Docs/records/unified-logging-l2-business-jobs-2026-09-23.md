# 日志 L2 后台业务任务记录

> 日期：2026-09-23（Asia/Shanghai）。承接 Hangfire / 清理任务，项目所有者确认继续商城、抽奖、神评与保留奖励任务日志治理。实现契约见[第 11 节](../features/unified-logging-contract.md)，本记录不关闭 L2 或整条业务调用链。

## 完成范围

- 四类 Job 改为安全批次摘要：空扫描与纯幂等跳过安静；成功、非异常拒绝和当前层消费的异常分别为 Info / Warning / Error，部分成功保持可辨识。
- 神评移除 Job 内多层重复异常输出，扫描 / 奖励异常原样抛给 Hangfire；抽奖仅消费单项失败，仓储扫描失败继续抛出。商城及保留奖励仍消费原有异常并按原契约返回，不借日志改成自动重试。
- 直接调用的 CoinRewardService 两个奖励方法和系统取消订单包装移除重抛日志；共享取消 helper 去掉带业务标识与原因的成功日志。保留奖励金额、快照、业务键、事务和返回理由。
- 局部 BusinessJobSummary 复用于商城、神评和保留奖励；抽奖沿用已有 ILogger 构造依赖，未增加公开接口或修改 DI。新增 jobKind、rewardCount、rejectedCount 与三类 job.batch 事件进入共享策略。
- 原商城取消返回计数可能包含服务返回 false 的调用，另用 updatedCount 表示 true；神评只清退旧当前标记也输出变更摘要；保留奖励的阶段失败不会抹去日志中已经完成的奖励计数。这些日志不替代数据库审计和事务提交凭据。

## 验证

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~ShopJobTest|FullyQualifiedName~PostLotteryServiceTest|FullyQualifiedName~CoinRewardBusinessKeyTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~UserBenefitServiceTest|FullyQualifiedName~CleanupLoggingTests|FullyQualifiedName~RuntimeLog'
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror
npm run check:logging-contract
npm run check:repo-quality:changed
git diff --check
```

- .NET 定向回归 102 通过、0 失败、0 跳过，其中 BusinessJobLoggingTests 新增 21 项。覆盖旧 / 候选日志的敏感哨兵过滤、空扫描、部分失败继续、订单取消 false 返回计数、锁释放、权益 true / false、统计失败默认值、抽奖去重与批次钳制、神评与沙发两条经验业务键和快照、奖励失败不插入、退役旧标记、保留奖励三周上限与跨阶段继续、奖励服务异常传播及重复结果。
- 原有商城、抽奖、订单、权益、奖励业务键及上一批清理 / 日志契约回归通过。测试使用 mock 服务 / 仓储，既有定向资产按自身方式执行；没有据此声明真实 Hangfire Server、生产结算或数据库提交已验收。
- Node 共享契约 27 项通过；API 构建 0 警告、0 错误，文档链接、仓库卫生与 diff 检查通过，records 索引保留既有篇幅软提醒。测试项目仅保留既有 ProducerLoggingTests 的 xUnit1051 提示，本批没有新增编译警告。
- 沙盒串行构建长时间缓慢推进，结束该次任务后，在沙盒外执行无 restore 构建 / 测试；没有修改依赖、工程配置或全局工具链。

## 剩余边界

- CoinService / ExperienceService 等底层仍有原文、逐项输出和重复异常日志，本批只关闭已列明的 Job 与直接包装方法。下一批优先治理奖励实际发放与失败处理链，再补 ChunkedUploadService / FileAccessTokenService 内清理分支和其余业务 / 框架来源。
- 保留奖励既有本地时钟与“已发放过”文本判定、神评的奖励 / 插入顺序没有改变；不把日志治理扩展成结算规则或时间语义修复。
- 未安装依赖、启动宿主 / 容器、执行浏览器 Smoke 或推送发布。`RadishLogging.Enabled=false` 保持不变；Rust `.tmp` 水印回退仍独立后置。
