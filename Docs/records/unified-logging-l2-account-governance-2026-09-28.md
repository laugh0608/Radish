# 统一日志 L2：币账户查询、人工调账与经验治理

日期：2026-09-28（Asia/Shanghai）。本批承接[统一日志专题](../features/unified-logging-governance-design.md)和[币扣除 / 转账批次](./unified-logging-l2-coin-movement-2026-09-23.md)，实现边界见[事件契约第 15 节](../features/unified-logging-contract.md)。

## 变更与异常责任

- 币余额、批量余额、分页流水、流水详情与统计查询不再复制逐次查询 / 初始化载荷或重复记录重抛异常。保留筛选、分页、用户展示名、统计日期范围与分类、初始化及返回对象。
- 人工调账不再将身份、金额、操作员、理由与流水号复制到运行日志。参数、余额版本、权限、幂等、事务、资产流水和变更审计保持原有语义。
- 经验调整、冻结与解冻移除逐次成功日志；权威经验流水和治理动作继续保存理由、身份、版本与时间。经验扣减归零、重放、冲突转换、升级 Outbox 和自动解冻路径保持原样。
- CoinController 的三处 InvalidOperationException 消费点输出固定 Warning：`coin.balance_query_rejected`、`coin.transaction_query_rejected`、`coin.adjustment_rejected`，仅携带安全 `failureKind`。既有 400 响应不改变，也不能据此把所有此类异常认定为正常业务拒绝，其中可能包括存储或映射失败。
- 人工调账捕获的 5xx BusinessException 记录一次 `http.failed` Error；4xx BusinessException 与 ArgumentException 保持安静。其他异常继续传播，由现有 API 最终边界负责；业务响应与权威审计原有文本没有因日志治理而被改写。

## 验证

新增 `AccountGovernanceLoggingTests`，共 36 个用例，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 查询结果、缺失余额初始化、详情不存在、非法分页和五类查询原异常传播。
- 调账流水、余额变更审计、完成记录、成功重放、版本冲突、参数拒绝及 Controller 消费的 5xx。
- 三处 Controller 消费异常的返回契约、稳定事件码、Warning 次数与标记秘密不可检出。
- 真实 CoinService / CoinController / TranAop 和内存 HTTP 管道组合：写入失败调用回滚，最终只有一次安全 Error。
- 经验扣减归零、权威流水、幂等重放、冻结 / 解冻版本和治理动作、竞争冲突映射及失败传播。

验证结果：

- .NET 定向回归 **188 项通过，0 失败，0 跳过**，包括本批 36 项以及 CoinService / CoinController、ExperienceService / ExperienceController、CoinMovementLogging、RewardLogging、ProducerLogging 和 RuntimeLog 既有用例。
- Node 日志契约 **27 项通过**。
- API 构建 **0 警告，0 错误**。
- 文档、改动文件卫生和 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首次沙盒编译成功，VSTest 因本地通信端口 `SocketException(13)` 中止；随后按最小范围提权运行上述定向回归通过。测试编译保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示，本批未新增编译警告。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~AccountGovernanceLoggingTests|FullyQualifiedName~CoinServiceTest|FullyQualifiedName~CoinControllerTest|FullyQualifiedName~ExperienceServiceTest|FullyQualifiedName~ExperienceControllerTest|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 剩余边界

本批只治理币账户查询与指定人工变更入口；经验账户 / 流水 / 每日统计查询、人工复核 / 等级治理、其余奖励与口令治理仍未全部完成。下一批按当前规划推进商城库存 / 权益依赖。

本批没有安装依赖、启动真实宿主 / 数据库 / 容器、修改真实业务数据或执行发布部署。测试使用 mock 仓储与事务管理器；验证到回滚调用和输出责任，不替代真实数据库事务 / 并发与线上 Smoke。`RadishLogging.Enabled` 默认 false，L2 与整个日志专题尚未关闭。
