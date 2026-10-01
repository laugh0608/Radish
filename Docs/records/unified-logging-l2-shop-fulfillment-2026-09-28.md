# 统一日志 L2：商城库存与订单履约依赖

日期：2026-09-28（Asia/Shanghai）。本批承接[账户与人工治理批次](./unified-logging-l2-account-governance-2026-09-28.md)，按[统一日志专题](../features/unified-logging-governance-design.md)继续库存、购买、取消和订单履约依赖；稳定契约见[事件契约第 16 节](../features/unified-logging-contract.md)。

## 实现范围

- ProductService 的购买校验、扣库存、回补和已售数量移除重复重抛日志。配置不完整保留固定 Warning；库存冲突仍最多尝试 5 次，重试等待为 50 / 100 / 200 / 400ms，只在实际继续重试时输出 attempt / delayMs。
- UserBenefitService 的订单发放入口、持续权益和消耗品 helper 移除逐次明细和重复 Error，保留订单快照、固定到期日、来源唯一键回查、背包订单幂等接口和事务。空订单仍由入口的 ArgumentNullException 校验拒绝，日志不再读取空订单而覆盖原异常。
- OrderService 的用户取消和重新发放移除重复日志；ShopController 原本消费的 InvalidOperationException 只在最终处理点输出安全 Warning，原响应不变。重新发放消费的 5xx BusinessException 记录安全 Error，4xx 业务拒绝不逐次记录。
- 购买外层原本将所有异常包装为默认 400 的 BusinessException，API 最终边界对此不记录 Error。本批在包装点增加 `order.purchase_interrupted`，补齐库存、查询、订单写入或补偿等失败的记录责任，保留既有响应、InnerException 与补偿语义。
- 支付 / 履约分支仍各自记录已消费失败；若后续补偿或写入又发生独立异常，外层再记录对应中断，不把两次不同失败错误合并。

本批没有改变商品能力、库存 / 限购计算、支付证据、订单 / 权益状态、幂等或审计存储规则。权益查询 / 激活 / 撤销、系统赠送、背包使用、商品管理 / 浏览、订单查询 / 备注等入口继续后续治理。

## 验证

新增 `ShopFulfillmentLoggingTests` 共 44 项，覆盖旧 / 候选日志与 Development / Production 四种组合：

- 库存更新值、版本条件、缺失商品与无限库存返回、配置拒绝、5 次尝试和重试耗尽、仓储原异常传播。
- 权益订单快照与固定到期日、已发放重放、来源唯一键竞争回查成功 / 未恢复、消耗品首次 / 重放 / 失败返回。
- 真实 ProductService / UserBenefitService / OrderService 组合的支付补偿、履约失败、补偿独立失败与安全输出次数。
- 真实 Controller 和内存 HTTP 管道下，购买外层包装的 400 仍有一次 Error；取消库存回补失败、重新发放拒绝和未处理失败各自有明确记录责任。
- 重新发放使用支付证据及订单快照，不读取当前商品；成功与普通业务拒绝保持安静。

结果：

- .NET 定向回归 **197 项通过，0 失败，0 跳过**，包括本批 44 项和 ProductService、UserBenefitService、OrderService、ShopController、ShopJob、BusinessJobLogging、CoinMovementLogging、AccountGovernanceLogging、RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**。
- API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 沙盒编译成功，VSTest 本地通信端口因 SocketException(13) 被阻止，随后最小范围提权运行上述回归通过。编译保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~ProductServiceTest|FullyQualifiedName~UserBenefitServiceTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~ShopControllerTest|FullyQualifiedName~ShopJobTest|FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~AccountGovernanceLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

仓储与外部业务依赖使用 mock，HTTP 管道仅在内存中调用；不替代真实数据库事务 / 并发、运行态商城 Smoke 或生产验收。本批未安装依赖、启动真实宿主 / 数据库 / 容器、写入真实业务数据、发布或部署。生产候选开关保持关闭。

L2 仍未整体完成，下一批继续其余 CoinRewardService 奖励入口及直接消费者，按组确认异常最终责任与原有业务回归。
