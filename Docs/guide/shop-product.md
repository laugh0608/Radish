# 3. 商品管理

> 最后更新：2026-08-12（Asia/Shanghai）
>
> 入口页：[商城系统设计方案](/guide/shop-system)
>
> 本页只维护当前商品模型、管理写入与可售边界；订单、权益和历史代码示例分别查看[订单系统](/guide/shop-order)、[权益与背包](/guide/shop-inventory)和[历史实现参考](/records/shop-product-implementation-reference)。

## 3.1 真相源与模型边界

当前代码真相源：

- 实体：`Radish.Model/Product.cs`、`Radish.Model/ProductCategory.cs`
- DTO / Vo：`Radish.Model/ViewModels/ProductVo.cs`
- Service：`Radish.Service/ProductService.cs`
- Console API：`Radish.Api/Controllers/ShopController.cs`
- 种子：`Radish.DbMigrate/InitialDataSeeder.Shop.cs`

### Product

| 字段组 | 当前字段 | 规则 |
| --- | --- | --- |
| 身份与租户 | `Id`、`TenantId` | 商品 ID 为雪花 LongId；JSON 和前端始终按字符串处理 |
| 基础资料 | `Name`、`Description`、`CategoryId` | 分类使用语义化字符串 ID |
| 媒体 | `IconAttachmentId`、`CoverAttachmentId` | 附件 ID 是持久化真值，URL 只在 Vo 中运行时派生 |
| 类型配置 | `ProductType`、`BenefitType`、`ConsumableType`、`BenefitValue` | 必须通过服务端能力矩阵和配置校验 |
| 价格 | `Price`、`OriginalPrice` | 单位为胡萝卜；原价可空 |
| 库存与限购 | `StockType`、`Stock`、`SoldCount`、`LimitPerUser` | `Limited` 才消费库存，`0` 限购表示不限 |
| 有效期 | `DurationType`、`DurationDays`、`ExpiresAt` | Permanent / Days / FixedDate 使用互斥字段 |
| 状态 | `IsOnSale`、`IsEnabled`、`OnSaleTime`、`OffSaleTime` | 可售还要同时满足配置、库存与服务端能力规则 |
| 并发与治理 | `Version`、`ModerationTargetActionId` | Console 编辑 / 上下架使用版本 CAS；治理下架保留来源动作 |
| 生命周期 | `IsDeleted`、创建 / 修改人和时间 | 业务删除使用软删除 |

### ProductCategory

`ProductCategory.Id` 是 `badge / card / boost` 这类语义化字符串主键；当前持有 `Name / IconAttachmentId / Description / SortOrder / IsEnabled / CreateTime / ModifyTime`。分类图标同样只持久化附件 ID。

## 3.2 服务端能力与公开可售

`ShopProductAvailabilityPolicy` 与 `GetProductCapabilities` 共同约束公开查询、购买、上架、Console 配置和权益启用。Console 不维护平行的商品类型表，也不能根据展示文案推断能力。

当前可售范围：

- 消耗品：RenameCard、ExpCard、CoinRedPacket；PostPinCard、PostHighlightCard、DoubleExpCard、LotteryTicket 继续禁止。
- 持续权益：Badge、Title、Theme；AvatarFrame、Signature、NameColor、LikeEffect 继续禁止。
- Badge 必须绑定公开有效图标附件；Title 文本长度为 `1..40`；Theme 只接受 `theme-dark-night / theme-sakura`。

“能力已开放”不代表历史商品自动上架。公开展示和购买至少同时满足：当前租户、未删除、启用、已上架、库存 / 限购有效、类型配置合法且能力允许。

## 3.3 Console 权威管理

### 查询

- `AdminGetProducts` 支持分类、商品类型、上下架状态和关键词的数据库权威筛选。
- 页码必须大于 `0`，页大小限制为 `1..100`；排序固定为 `CreateTime desc + Id desc`。
- `AdminGetProduct/{id}` 返回包含 `VoVersion` 的权威详情；LongId 在 Console URL 与类型中保持字符串。
- Console 查询可由 URL 回访；请求代际拒绝过期响应，同查询失败保留 stale 快照，新查询失败为 unavailable。

### 创建与普通更新

- `CreateProductDto / UpdateProductDto` 不包含 `IsOnSale`。
- 新商品固定以 `IsOnSale=false` 创建，并清空 `OnSaleTime / OffSaleTime`。
- 普通更新只写资料、配置、价格、库存、有效期和排序等字段，保持当前上下架状态。
- 更新携带 `ExpectedVersion`，服务端按商品 ID、租户、版本和未删除条件执行 CAS；冲突返回结构化 `409`。
- Form 提交同时受权限、能力元数据、权威列表 / 详情状态、dirty / busy 和附件上传状态约束。

### 独立上下架

- 上架只允许 `PutOnSale/{id}`，下架只允许 `TakeOffSale/{id}`，统一要求 `console.products.toggle-sale + ExpectedVersion`。
- 上架重新校验商品配置、能力、启用状态和库存；成功递增 `Version` 并记录上架时间。
- 下架递增 `Version` 并记录下架时间。普通保存不得替代这两个动作。

### 删除

- `DeleteProduct/{id}` 要求 `console.products.delete`。
- 任何关联订单都会阻止删除；管理员应下架商品以保留历史订单快照。
- 合法删除执行软删除、下架并清理治理来源，不物理移除商品或订单事实。

## 3.4 库存、限购与订单快照

- `StockType.Unlimited` 不扣减库存；`Limited` 在购买事务中执行条件扣减，不能只依赖前端预检。
- `LimitPerUser` 由服务端基于当前用户有效订单聚合裁决；客户端展示不构成购买资格。
- 下单时固化商品名称、类型、价格、数量与有效期等快照。历史履约和重试使用订单快照，不回读商品当前配置。
- 库存、订单、胡萝卜扣款和履约的一致性边界详见[订单系统](/guide/shop-order)和[商城后端边界](/guide/shop-backend)。

## 3.5 Console API 与权限

| 能力 | API | 权限 |
| --- | --- | --- |
| 分类与能力元数据 | `GetCategories`、`GetProductCapabilities` | 商品查看上下文 |
| 商品列表 / 详情 | `AdminGetProducts`、`AdminGetProduct/{id}` | `console.products.view` |
| 创建 | `CreateProduct` | `console.products.create` |
| 普通更新 | `UpdateProduct` | `console.products.edit` |
| 上下架 | `PutOnSale/{id}`、`TakeOffSale/{id}` | `console.products.toggle-sale` |
| 删除 | `DeleteProduct/{id}` | `console.products.delete` |

按钮可见性不是安全边界；Controller 权限、Service 业务规则和数据库条件更新共同裁决写入。

## 3.6 种子与迁移边界

- 当前种子只以 `InitialDataSeeder.Shop.cs` 为真相源，固定 ID 保证重复 apply 幂等；不得在 seed 中临时生成新 Snowflake ID。
- seed 数据仍必须通过当前能力和配置规则；`IsOnSale=true` 不能让不支持或配置无效的商品公开销售。
- Badge 缺少公开附件时不可售；Theme 默认保持下架，是否销售由运营显式决定。
- migration / seed 不得借能力开放强制恢复历史商品的上架状态。

## 3.7 回归入口

- 后端：`ProductServiceTest`、`ShopControllerTest`、`ShopProfileTest`
- Console：`productListUrlState.test.ts`、`productPresentation.test.ts`、`r3C04ProductsContract.test.ts`
- 跨资源：`consoleTroubleshootingPathContracts.test.ts`、权限扫描与 LongId 安全门禁

R3-C04-D 的批次事实和命令级证据见[商品权威列表与独立上下架实现记录](/records/f4-r-r3-c04-d-console-products-implementation-2026-08-12)。

> 下一篇：[4. 订单系统](/guide/shop-order)
