# 6. 商城后端设计与接口边界

> 版本：v2.0 | 最后更新：2026-07-13
>
> 入口页：[商城系统设计方案](/guide/shop-system)

本页维护商城当前后端分层、接口和一致性边界，不复制 Service 实现代码。业务状态说明见[订单系统](/guide/shop-order)和[权益与背包](/guide/shop-inventory)。

## 6.1 分层与职责

| 层 | 主要类型 | 职责 |
|----|----------|------|
| Controller | `ShopController` | 身份、权限、HTTP 契约和结果包装 |
| Service | `ProductService` | 商品查询、配置校验、上下架和购买资格 |
| Service | `OrderService` | 购买事务、订单快照、支付和履约重试 |
| Service | `UserBenefitService` | 持续权益发放、查询、选择、停用、过期和撤销 |
| Service | `UserInventoryService` | 消耗品查询、条件扣减、真实效果和使用幂等 |
| Repository | `IUserBenefitRepository` | 权益选择、过期、撤销的事务仓储和原子 upsert |
| Repository | `IUserInventoryRepository` | 背包条件扣减和聚合数量并发保护 |
| Job | `ShopJob` | 超时订单和权益过期的批处理编排 |
| DbMigrate | schema ledger migrations | 显式建表、回填、doctor 和 verify |

Controller 不直接访问仓储；Service 不使用 `_repository.Db.Queryable` 绕过仓储边界。

## 6.2 主要模型

```text
Product
  └─ 当前配置、库存和可售状态

Order
  ├─ 商品与有效期快照
  ├─ 支付状态与 CoinTransactionId
  └─ GrantedBenefitId / GrantedInventoryId

UserBenefit ──拥有──> UserActiveBenefit ──当前选择

UserInventory ──当前数量
  └─ UserInventoryGrantRecord ──订单级发放事实

ShopEntitlementOperation
  └─ Use / Activate / Deactivate / Expire / Revoke
```

## 6.3 用户 API

| 方法 | 路径 | 权限 | 返回 |
|------|------|------|------|
| `GET` | `/api/v1/Shop/GetCategories` | 匿名 | 商品分类 |
| `GET` | `/api/v1/Shop/GetProducts` | 匿名 | 可公开商品分页 |
| `GET` | `/api/v1/Shop/GetProduct/{id}` | 匿名 | 商品详情 |
| `GET` | `/api/v1/Shop/CheckCanBuy/{id}` | Client | 购买资格和原因 |
| `POST` | `/api/v1/Shop/Purchase` | Client | 购买结果与履约资源 |
| `GET` | `/api/v1/Shop/GetMyOrders` | Client | 当前用户订单分页 |
| `GET` | `/api/v1/Shop/GetOrder/{id}` | Client | 当前用户订单详情 |
| `POST` | `/api/v1/Shop/CancelOrder/{id}` | Client | 取消待支付订单 |
| `GET` | `/api/v1/Shop/GetMyBenefits` | Client | 当前用户持续权益 |
| `GET` | `/api/v1/Shop/GetMyActiveBenefits` | Client | 当前有效选择 |
| `POST` | `/api/v1/Shop/ActivateBenefit/{id}` | Client | 结构化选择结果 |
| `POST` | `/api/v1/Shop/DeactivateBenefit/{id}` | Client | 结构化停用结果 |
| `GET` | `/api/v1/Shop/GetMyInventory` | Client | 消耗品聚合背包 |
| `POST` | `/api/v1/Shop/UseItem` | Client | 使用结果和操作流水 ID |
| `POST` | `/api/v1/Shop/UseRenameCard` | Client | 改名卡使用结果 |

外部 LongId 由服务端按 `long` 绑定，但 JSON 和前端类型必须保持字符串。

## 6.4 Console API 与权限

| 能力 | 权限 |
|------|------|
| 商品查询、详情 | `console.products.view` |
| 商品创建、更新 | 对应商品写权限 |
| 上下架 | `console.products.toggle-sale` |
| 订单查询、详情 | `console.orders.view` |
| 履约重试 | `console.orders.retry` |
| 订单备注 | `console.orders.remark` |
| 用户权益与业务流水 | `console.benefits.view` |
| 撤销持续权益 | `console.benefits.revoke` |

对应接口包括：

- `AdminGetProducts / AdminGetProduct`
- `CreateProduct / UpdateProduct / PutOnSale / TakeOffSale`
- `AdminGetOrders / AdminGetOrder / RetryGrantBenefit / AdminRemarkOrder`
- `AdminGetUserBenefits / AdminGetEntitlementOperations / AdminRevokeBenefit`

Console 权限是服务端强制约束，不以按钮是否显示作为安全边界。

## 6.5 事务边界

### 购买事务

订单、库存、胡萝卜扣款、履约资源和购买幂等终态必须形成一致结果。支付失败与履约失败分阶段记录；履约重试必须校验真实扣款流水。

### 消耗品使用事务

条件扣减、改名/经验/胡萝卜效果、`ShopEntitlementOperation` 和幂等成功终态位于同一 Main 数据库事务。效果失败必须回滚数量和流水。

### 持续权益事务

选择、停用、过期和撤销由专属仓储原子更新 `UserActiveBenefit`、兼容字段和操作流水。并发请求不能产生同类型多个指针，也不能让旧停用请求删除新选择。

## 6.6 幂等边界

| 动作 | 幂等键 | 持久化保护 |
|------|--------|------------|
| 购买 | `shop:{uuid}` | `OperationIdempotencyRecord` + 来源订单唯一约束 |
| 使用消耗品 | `inventory-use:{uuid}` | `OperationIdempotencyRecord` + `ShopEntitlementOperation` 唯一键 |
| 启用/停用 | 由目标权益和当前指针决定 | 原子 upsert / 条件删除 + 操作事实防重 |
| 过期 | 服务端业务键 | 条件过期 + 唯一操作事实 + Outbox 业务键 |
| 撤销 | 目标权益当前状态 | 条件撤销 + 操作事实 |

同一幂等键绑定不同请求摘要必须返回冲突，不能采用“最后一次请求覆盖前一次”的语义。

## 6.7 UTC 与有效状态

- 注入 `TimeProvider`，使用 `GetUtcNow().UtcDateTime` 进行业务判断。
- `EffectiveAt / ExpiresAt / RevokedAt` 统一按 UTC 解释。
- `ExpiresAt <= nowUtc` 立即失效；定时任务不是授权真相。
- 前端只展示服务端 `VoStatus / VoCanActivate / VoCanDeactivate`。
- 自然日规则与绝对时刻规则的全局说明见[时间语义与业务自然日](/guide/time-semantics)。

## 6.8 多租户与安全

- 订单、权益、选择、背包和流水都必须携带租户字段或受租户过滤器保护。
- 支付证据校验同时匹配租户、用户、订单业务键、成功状态和金额。
- 改名请求摘要只保存新展示名 hash，不在幂等摘要重复保存明文。
- 业务流水不保存支付口令、完整异常堆栈或未脱敏凭据。
- 附件只通过 `IAttachmentUrlResolver` 生成安全展示 URL。

## 6.9 商品可用性

`ShopProductAvailabilityPolicy` 同时约束：

- 公开商品列表和详情。
- 购买资格。
- Console 上架。
- 持续权益启用。
- 历史不可用商品 seed 收敛。

当前允许真实使用的消耗品为改名卡、经验卡和萝卜币红包；所有 `BenefitType` 仍关闭。开放新类型必须同时具备配置校验、发放、使用或启用、真实消费、失效、Console 排障和测试。

## 6.10 Schema ledger

本批商城迁移按顺序登记：

1. `20260713_001_shop_order_fulfillment_safety`
2. `20260713_002_shop_entitlement_operation`
3. `20260713_003_user_active_benefit`

`Radish.DbMigrate` 负责：

- `doctor`：报告支付证据、历史资源字段、多激活和无效指针问题。
- `apply`：在锁和 ledger 保护下执行显式迁移与安全回填。
- `verify`：检查字段、唯一索引、选择归属、实时有效性和不可用商品状态。

运行时 Code First 不得在 baseline 后静默补商城字段。

## 6.11 错误与日志

- Controller 通过统一 `MessageModel<T>` 和 API 错误契约返回结果。
- 业务可预期失败返回精确原因；未处理异常不向客户端暴露堆栈。
- Serilog 使用结构化字段记录用户、订单、权益和操作 ID。
- 不记录支付口令、完整幂等载荷和敏感展示名输入。

## 6.12 测试入口

商城核心回归集中在 `Radish.Api.Tests`：

- `OrderServiceTest`
- `UserInventoryServiceTest`
- `UserBenefitServiceTest`
- `UserBenefitRepositoryTest`
- 三个商城 schema migration 测试
- `ShopJobTest`

开发中执行定向或全量后端测试；真实 Gateway smoke 只在专题准备验收且当前任务获得服务启动授权后执行。

---

> 下一篇：[7. 商城前端与操作说明](/guide/shop-frontend)
