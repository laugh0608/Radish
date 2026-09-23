# 日志 L2 币扣除 / 转账及直接消费边界

> 日期：2026-09-23（Asia/Shanghai）。承接项目所有者“继续推进下一步”，按既定顺位治理币扣除 / 转账日志。契约见[第 14 节](../features/unified-logging-contract.md)，后续顺位见[当前进行中](../planning/current.md)。

## 完成范围

- `CoinService.ConsumeCoinAsync / TransferAsync` 移除逐项用户 / 金额 / 流水明细和重复异常；转账完成记录第二次调用恢复后输出安全 Warning，保留原资金操作和所有重试 / 返回分支。
- 直接依赖 `PaymentPasswordService.VerifyPaymentPasswordAsync` 及哈希升级 helper 不再打印用户与业务参数；保留算法、升级、失败次数、锁定、返回对象和异常传播。
- `OperationIdempotencyService` 唯一键竞争回查保持安静，缺失完成记录按 success / failure 输出安全 Warning；保存点、响应 / 错误审计、保留期与缺失即返回均不变。
- 直接消费者 `OrderService.PurchaseAsync` 的扣币 / 权益失败保留安全 Error；外层仍包装原异常，库存补偿、失败阶段、FailReason、幂等结果和返回值不变。移除原先可能在权益失败后仍输出的购买成功日志。
- 新事件 `coin.transfer_completion_recovered / idempotency.completion_missing / order.purchase_failed` 均注册共享策略，属性只接受安全 failureKind 及 completionKind / purchaseStage 固定枚举。

恢复事件只证明完成方法第二次正常返回；底层记录缺失原来即可能返回，故不声称写库成功。旧与候选 sink 均不接收本批入口中的身份、金额、备注、幂等键、口令验证理由与异常正文；权威交易 / 安全 / 订单审计和 API 字段不属于运行日志，保持原契约。

## 验证范围

新增 `CoinMovementLoggingTests`，覆盖旧 / 候选输出、扣币流水与原异常传播、转账幂等成功重放 / 业务拒绝 / 终态失败、完成记录恢复或二次失败且不重复资金操作、商城扣币 / 权益失败与库存补偿、真实支付口令验证计数与锁定、幂等竞争回查及缺失完成记录。

定向回归同时包含既有 CoinService、CoinController、OrderService、PaymentPasswordService、OperationIdempotencyService、RewardLogging 和 RuntimeLog 用例。

- .NET 定向回归：**141 项通过、0 失败、0 跳过**。
- Node 日志契约：**27 项通过**；API 构建：**0 警告、0 错误**。
- 文档、仓库卫生与 `git diff --check` 通过；记录索引 311 行保留建议上限 300 行的非阻断提醒。
- 测试先在沙盒编译，VSTest 因本地通信端口 SocketException(13) 中止，随后最小范围提权运行。首轮新夹具缺少用户查询返回值导致 2 项失败，补齐并加入权益失败覆盖后重跑取得上述结果；测试编译保留既有 `ProducerLoggingTests.cs:261` xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~CoinServiceTest|FullyQualifiedName~CoinControllerTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~PaymentPasswordServiceTest|FullyQualifiedName~OperationIdempotencyServiceTest|FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 未覆盖边界

库存 / 权益服务、余额 / 统计 / 交易查询、人工调账、其他奖励调用方、口令设置 / 修改 / 人工治理及其外层入口仍需继续处理；本批不宣称商城整链路日志收口。

本批未安装依赖、启动宿主 / 隔离服务、修改真实资产或业务数据、推送、发布或部署。mock 回归不替代真实数据库事务 / 并发、线上转账与商城 Smoke。生产 `RadishLogging.Enabled=false` 不变，L2 尚未关闭；下一批优先账户查询 / 人工调账与治理。
