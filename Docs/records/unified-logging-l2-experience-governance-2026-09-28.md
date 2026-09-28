# 统一日志 L2：经验查询、人工复核与等级治理

日期：2026-09-28（Asia/Shanghai）。本批承接[奖励入口批次](./unified-logging-l2-reward-entries-2026-09-28.md)，按[统一日志专题](../features/unified-logging-governance-design.md)推进经验查询、复核、等级重算及直接计算器依赖；稳定契约见[事件契约第 18 节](../features/unified-logging-contract.md)。

## 实现范围

- 移除经验账户单个 / 批量、流水、每日统计、治理留痕、等级配置与排行榜中仅记录再重抛的 catch。查询筛选、分页、日期、冻结归一化、用户公开身份补全及初始化保持不变。
- 排名查询仍消费异常并返回 0，改为一次安全 `experience.rank_query_failed` Error。排行榜缺失用户仍跳过，使用一次 `experience.leaderboard_incomplete` Warning 汇总 skippedCount，保留原 DataCount 和排名递增口径。
- 人工复核与等级重算移除操作员、目标、版本和审计身份明细；保留治理证据、版本条件、幂等、预览指纹、冲突映射、审计及缓存清除顺序。ExperienceController 无本地 catch，未处理异常交给既有 API 最终边界。
- 治理快照反序列化失败继续返回空列表，同时补齐固定 Warning；不输出原始快照或异常正文。两个字段分别解析，独立损坏可各自产生告警。
- ExperienceCalculator 移除缓存正常过程明细与无效等级 Warning；缓存读取、写入和清除失败使用固定事件和安全类别，保留原回退、公式、序列化和过期行为。ExperienceService 的等级配置缓存继续使用此前登记的安全事件。

## 验证范围

新增 `ExperienceQueryGovernanceLoggingTests` 共 36 项，覆盖旧 / 候选日志与 Development / Production 四种组合：

- 查询结果、空值、分页默认值、每日统计 7 / 30 天窗口、冻结不参与排名与无效统计参数返回。
- 查询原异常对象传播，并通过真实 Controller 与内存 API 错误管道验证最终单次 `http.failed`；排名消费失败仅在 Service 输出一次 Error。
- 排行榜多个缺失用户只有一次数量摘要，原分页总数和实际条目排名保持不变。
- 治理快照一个损坏字段回退为空、另一个有效字段与原备注保持不变，日志不含敏感哨兵。
- 人工复核正常版本推进、审计字段、幂等完成与成功重放；版本冲突返回原 409，上抛存储失败由 API 唯一记录。
- 等级重算预览、审计、结果、冲突与缓存清除失败返回；计算器实际公式结果、缓存命中安静、读 / 写 / 清除异常的安全回退。

结果：

- .NET 定向回归 **153 项通过，0 失败，0 跳过**，包含本批 36 项与 ExperienceService、ExperienceController、ExperienceCalculator、RewardLogging、AccountGovernanceLogging、RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首轮修正了新测试的 Controller 命名空间、仓储分页 mock 重载和内存缓存生命周期声明；随后上述回归通过。测试项目保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。
- 沙盒编译完成后，VSTest 本地通信端口因 SocketException(13) 被阻止；最小范围提权运行测试取得上述结果。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ExperienceQueryGovernanceLoggingTests|FullyQualifiedName~ExperienceServiceTest|FullyQualifiedName~ExperienceControllerTest|FullyQualifiedName~ExperienceCalculatorTest|FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~AccountGovernanceLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

业务仓储、幂等与外部缓存故障使用 mock，HTTP 管道仅在内存中调用；不替代真实数据库事务 / 并发、Redis 或运行态验收。计算器正常缓存使用内存实现，缓存命中测试确认安静与键集合，不宣称完成序列化往返数值正确性验收。本批未安装依赖、启动真实宿主 / 数据库服务 / 容器、写入真实业务数据、发布或部署。

生产候选开关继续关闭，L2 尚未整体完成。下一批按组推进商城其余管理 / 查询及权益使用入口。
