# 日志 L2 币 / 经验奖励发放链记录

> 日期：2026-09-23（Asia/Shanghai）。项目所有者确认承接后台业务 Job，继续 CoinService / ExperienceService 的奖励实际发放、重试和失败处理。规则见[契约第 12 节](../features/unified-logging-contract.md)。

## 完成范围

- 币奖励正常发放和幂等重放去掉逐项日志，异常保留传播；经验奖励保留原 false / Skip 返回，将消费处原文 Error 改为安全事件。
- 共享重试 helper 只记录真正执行的下一次重试，次数 / 延迟保留；耗尽不在 helper 重复输出 Error。经验初始化回查恢复安静，无法恢复只记一次 Error。缓存读 / 写 / 失效各保留固定操作名的 Warning。
- 批量发放保留继续处理和成功计数：币批次汇总已消费异常；经验批次不重复记录已经由单项消费的异常，只将 false 纳入结果计数。正常拒绝、冻结、限额、单次成功和幂等命中不逐条输出。
- 业务返回理由、币 / 经验流水、余额变动记录、每日统计、升级 Outbox、自动解冻审计、奖励业务键、金额、事务边界、结算时钟和配置均未更改。日志只记录固定领域 / 操作枚举、计数、延迟与 failureKind。

## 验证

```bash
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~CoinServiceTest|FullyQualifiedName~ExperienceServiceTest|FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~RuntimeLog|FullyQualifiedName~CoinRewardBusinessKeyTest|FullyQualifiedName~ReliableTaskProcessorContentRewardTest|FullyQualifiedName~UnitOfWorkManageTest'
npm run check:logging-contract
npm run check:repo-quality:changed
git diff --check
```

- API 在沙盒内构建成功，0 警告、0 错误；关闭共享编译仅为该次命令选项，没有改工程或全局设置。
- .NET 最终定向回归 **130 通过、0 失败、1 跳过**。跳过项为 UnitOfWorkManageTest 的 PostgreSQL 保存点集成测试，原因是未配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`；未为此启动数据库，不宣称获得 PostgreSQL 并发 / 事务实测证据。
- RewardLoggingTests 新增 **22 项**：旧 / 候选日志安全、成功流水与幂等重放、币 4 次执行 / 3 次固定延迟、经验 7 次执行 / 6 次有界抖动、恢复后停止重试、唯一键竞争成功 / 无既有流水、初始化竞争恢复 / 未恢复、缓存读写故障回退、冻结 / 限额拒绝、批量继续与错误不重复、catch 前异常仍传播。使用真实 Service 与 mock 仓储 / 缓存，审计载荷仍保留敏感哨兵而运行日志不包含。
- 既有币 / 经验服务、奖励业务键、后台 Job、奖励处理器、事务与统一日志回归通过。测试构建仅保留既有 ProducerLoggingTests 的 xUnit1051 提示，本批未新增警告。
- Node 共享日志契约 **27 项通过**。初次测试编译成功，但 VSTest 被沙盒禁止绑定本机通信套接字；随后仅对无 restore 定向测试提权，未安装依赖或启动宿主。
- 文档链接、仓库卫生与 diff 检查通过；records 索引保留既有篇幅软提醒。

## 剩余边界

- 下一批推进 ChunkedUploadService / FileAccessTokenService 内清理分支；随后继续币扣除 / 转账、账户查询、人工治理、其他奖励入口与外层消费者。实际发放路径完成不等于整个币 / 经验服务文件或全业务调用链完成。
- L2 完整异常安全栈帧及其他框架来源仍未收口，L1 正式传输上界及 L3–L6 后续门禁未关闭。`RadishLogging.Enabled=false` 保持不变，没有新增真实宿主、生产运行、部署或发布证据。
