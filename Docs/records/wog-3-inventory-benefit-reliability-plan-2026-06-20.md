# WOG-3 背包 / 权益发放可靠性方案

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 状态：`已确认 / 首批代码已实现`
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)

## 目标

`WOG-3` 聚焦商城订单完成后的权益 / 背包发放，以及背包道具使用时的资产一致性。

本批目标是让同一订单的资产发放具备订单级幂等真值，让背包数量增减由数据库条件写入保护，并让经验卡 / 萝卜币红包使用不再出现“奖励已发但库存未扣”或并发重复发放奖励的问题。

本批不改变前端 API 入参，不启动完整钱包、经济扩展、资产风控平台、Redis 分布式锁、Outbox 或通用任务队列。

## 执行结论

`2026-06-20` 已按确认方案完成首批代码实现：

- 新增 `UserInventoryGrantRecord`，用 `TenantId + SourceOrderId` 记录消耗品订单发放事实。
- `UserBenefit` 已增加 `TenantId + SourceOrderId` 唯一索引，购买来源权益发放支持重复重试复用既有结果。
- `UserInventory` 已增加 `TenantId + UserId + ConsumableType + ItemValue` 聚合唯一索引，`ItemValue` 统一归一为空字符串而非 `NULL`。
- 新增 `IUserInventoryRepository / UserInventoryRepository`，承接订单消耗品发放、发放流水写入、背包数量条件递增和条件扣减。
- `UserBenefitService.GrantBenefitAsync`、`OrderService.RetryGrantBenefitAsync` 已纳入事务边界；订单重试可通过发放层幂等补偿为完成状态。
- 经验卡、萝卜币红包和改名卡已改为先条件扣减，再执行奖励或用户状态变更；扣减后效果失败时抛出业务异常触发事务回滚，Controller 仍返回原有失败响应结构。
- 当时已补阶段性结构脚本，覆盖历史权益重复清理、背包聚合项合并、新发放流水表、可确认订单流水回填和唯一索引创建；2026-06-22 后按上线前数据库口径清理，不再作为当前执行入口。
- `Radish.DbMigrate` 已补本地结构治理入口：本地 `apply` 会补发放流水表、规整历史数据、回填可确认消耗品订单发放流水并补齐索引。

## 当前问题

1. `UserBenefitService.GrantBenefitAsync` 的权益类商品会直接新增 `UserBenefit`，`UserBenefit.SourceOrderId` 当前没有唯一约束。若订单发放成功但订单状态更新失败，管理员重试可能再次发放同一订单权益。
2. `UserBenefitService.GrantConsumableItemAsync` 对消耗品背包采用“查询已有行 -> 内存加数量 -> 普通更新 / 新增”的流程。并发购买同一道具时可能丢失数量更新；订单重试也可能重复累加数量。
3. `UserInventoryService.DeductItemAsync` 当前先查询数量再写回，扣减条件不在数据库更新语句中表达。并发使用同一背包项时，可能多次通过数量校验。
4. 经验卡和萝卜币红包当前先发放经验 / 胡萝卜，再扣减背包。若扣减失败或并发请求同时进入，可能产生超发奖励。
5. 改名卡当前先改昵称再扣道具；若扣减失败，用户状态与背包消耗不一致。

## 数据真值

- 订单发放真值：同一租户下，同一 `SourceOrderId` 的购买发放只能成功一次。
- 权益类商品真值：`ShopUserBenefit.SourceOrderId` 是购买来源权益的订单级幂等键。
- 消耗品发放真值：需要独立的订单发放流水记录订单与背包增量关系，聚合背包行本身不能承载所有来源订单。
- 背包聚合真值：同一租户、同一用户、同一 `ConsumableType`、同一 `ItemValue` 只保留一条未删除聚合背包行。
- 道具使用真值：背包扣减必须通过数据库条件更新完成，只有扣减确实成功后才能发放对应奖励或更新用户状态。

## 建议实施

### 1. 订单级发放幂等

为购买来源权益增加唯一约束：

- `UserBenefit` 增加唯一索引：`TenantId + SourceOrderId`。
- `SourceOrderId` 为空的系统赠送、活动赠送不受该唯一索引影响；PostgreSQL / SQLite 均允许唯一索引中存在多条 `NULL`。

为消耗品发放新增订单发放流水实体，建议命名为 `UserInventoryGrantRecord`：

- 字段包含 `TenantId`、`UserId`、`InventoryId`、`SourceOrderId`、`SourceProductId`、`ConsumableType`、`ItemValue`、`Quantity`、`CreateTime` 和审计字段。
- 唯一索引：`TenantId + SourceOrderId`。
- 同一订单重试时先查询或尝试写入发放流水；流水已存在时直接返回对应 `InventoryId`，不再次累加数量。
- 发放流水和背包数量增加必须处于同一事务中，避免“数量已加但流水缺失”或“流水存在但数量未加”。

不建议把 `SourceOrderId` 直接加到 `UserInventory` 聚合行上作为唯一键，因为同一背包项可能来自多笔订单，聚合行只能表达当前数量，不能表达每笔发放事实。

### 2. 背包聚合唯一键与条件增量

为 `UserInventory` 增加聚合唯一约束：

- 唯一索引：`TenantId + UserId + ConsumableType + ItemValue`。

新增实体专属仓储，建议为 `IUserInventoryRepository` / `UserInventoryRepository`，承接以下数据库级写入：

- `GrantConsumableForOrderAsync(...)`：在事务中完成订单发放流水去重、聚合背包行创建 / 数量递增和最终 `InventoryId` 返回。
- `TryDeductItemAsync(...)`：使用 `Quantity >= quantity` 作为数据库更新条件，返回扣减后的数量或明确失败。

Service 层不直接访问 `Db` / `Queryable`，继续只负责编排业务语义和返回 DTO。

### 3. 道具使用顺序

经验卡、萝卜币红包、改名卡统一调整为先完成条件扣减，再执行对应效果：

1. 查询并校验背包项存在、类型匹配、数量参数合法、配置值合法。
2. 执行条件扣减；扣减失败直接返回数量不足或使用失败，不发放奖励、不改用户状态。
3. 扣减成功后发放经验 / 胡萝卜，或更新昵称。
4. 外层公开服务方法增加 `[UseTran]`，让扣减、奖励 / 用户状态更新和流水写入处于同一事务边界。

事务标记必须放在外部可被 AOP 拦截的公开服务方法上，不能依赖私有方法上的标记。

### 4. 订单重试行为

`OrderService.RetryGrantBenefitAsync` 保持只允许 `Failed` 订单重试，但重试发放结果应由发放层的订单级幂等保证：

- 如果同一订单已存在权益或消耗品发放流水，重试返回既有资产 ID，并把订单补偿为 `Completed`。
- 如果确实未发放，按正常发放流程写入权益或背包。
- 如果商品不存在、订单状态不符合或发放失败，继续返回当前错误语义。

这样可以覆盖“发放成功但订单状态更新失败”的补偿场景。

## 迁移与历史数据

代码批次需要同步：

1. `Radish.Model` 实体索引声明。
2. `Radish.DbMigrate` 本地结构补丁和必要历史数据规整。
3. 正式数据库存在后再生成发布 SQL；上线前不维护该阶段历史发布脚本。

迁移建议：

- `ShopUserBenefit` 按 `TenantId + SourceOrderId` 清理历史重复购买来源权益，优先保留未删除、创建时间较早或订单已关联的记录，其他异常重复行软删除或显式归档处理。
- `ShopUserInventory` 按 `TenantId + UserId + ConsumableType + ItemValue` 合并历史重复背包项，数量求和，保留一条规范记录，其他异常重复行软删除。
- 新增消耗品发放流水表后，只能从已有 `Order.UserBenefitId` 和订单快照补回可确认的已完成订单发放流水；无法可靠反推的历史订单不强行伪造流水。

历史规整必须在唯一索引创建前完成。

## 验证

代码批次完成后建议执行：

1. `dotnet test Radish.Api.Tests`
2. 新增 `UserBenefitService` / `UserInventoryService` 定向测试：
   - 同一订单权益发放重复调用只返回同一结果。
   - 同一订单消耗品发放重复调用只增加一次数量。
   - 同一道具并发 / 重复扣减时只有一次成功。
   - 经验卡、萝卜币红包在扣减失败时不发奖励。
   - 改名卡在扣减失败时不改昵称。
3. 覆盖 `OrderService.RetryGrantBenefitAsync`：
   - 已存在发放事实时可补偿订单完成。
   - 未存在发放事实时按正常流程发放。
4. 覆盖结构同步 / 正式数据库阶段发布 SQL：
   - 历史重复权益和背包聚合行规整后唯一索引可创建。
   - 已完成订单可补回消耗品发放流水。
5. `dotnet build Radish.slnx -c Debug`
6. `git diff --check`
7. `npm run check:repo-hygiene:changed`

本批已执行：

- `dotnet build Radish.slnx -c Debug`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~UserInventoryRepositoryTest|FullyQualifiedName~UserInventoryServiceTest|FullyQualifiedName~UserBenefitServiceTest|FullyQualifiedName~OrderServiceTest"`：通过，`19` 个测试全部通过。
- `dotnet test Radish.Api.Tests`：通过，`490` 个测试全部通过。

## 停止线

- 不重做商城购买整体幂等；购买入口已由支付 / 转账幂等治理承接。
- 不改变前端购买、背包使用或订单重试 API 契约。
- 不把所有萝卜币 / 经验奖励业务键唯一性并入本批；奖励流水自身的业务键治理继续留给 `WOG-4`。
- 不引入 Redis 锁、队列、Outbox 或完整资产风控平台。
- 不一次性治理抽奖券、双倍经验卡、帖子置顶卡和帖子高亮卡的未来业务效果；这些道具当前仍按未开放口径处理。

## 已确认决策

1. 新增 `UserInventoryGrantRecord` 作为消耗品订单发放流水，承担 `TenantId + SourceOrderId` 唯一真值。
2. `UserInventory` 聚合唯一键采用 `TenantId + UserId + ConsumableType + ItemValue`，并将空道具值归一为空字符串。
3. 经验卡、萝卜币红包、改名卡统一改为“先条件扣减，再执行效果”，并在公开服务方法上建立事务边界。
4. 本批保持 API / 前端契约不变，只改后端持久化、结构同步入口、DbMigrate 和定向测试。
