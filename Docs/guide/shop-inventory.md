# 5. 商城权益与背包

> 版本：v2.1 | 最后更新：2026-07-14
>
> 入口页：[商城系统设计方案](/guide/shop-system)

商城将持续权益、当前选择和消耗品数量拆成不同真相源。页面状态、业务授权、定时过期和 Console 排障都必须遵循同一套服务端 UTC 规则。

## 5.1 数据模型

| 模型 | 职责 | 关键唯一约束 |
|------|------|--------------|
| `UserBenefit` | 用户拥有的一份持续权益 | `(TenantId, SourceOrderId)` 防止订单重复发放 |
| `UserActiveBenefit` | 用户每种权益类型的当前选择 | `(TenantId, UserId, BenefitType)` |
| `UserInventory` | 同类型、同值消耗品的当前聚合数量 | `(TenantId, UserId, ConsumableType, ItemValue)` |
| `UserInventoryGrantRecord` | 每张订单的消耗品发放事实 | `(TenantId, SourceOrderId)` |
| `ShopEntitlementOperation` | 使用、启用、停用、过期、撤销的成功业务流水 | 使用动作按租户、用户、类型和幂等键唯一 |

`UserBenefit.IsActive / ActivatedAt / IsExpired` 只保留迁移兼容和查询优化用途，不再是当前选择或授权真相。

## 5.2 来源与回访

持续权益保存：

- `SourceOrderId`
- `SourceProductId`
- `SourceType`
- 商品名称和图标附件快照

`UserBenefitVo` 通过 `VoSourceOrderId / VoSourceProductId` 支持从背包回到订单和商品。

消耗品聚合行只公开 `VoSourceProductId` 作为“相关商品”。一条聚合行可能来自多张订单，因此不能把最后一次订单误写成完整来源；订单级历史由 `UserInventoryGrantRecord` 保存。

所有来源 ID 在前端按字符串 LongId 传递，不进入 JavaScript 或 Dart 数值域。

## 5.3 持续权益状态

服务端在每次读取时使用 `TimeProvider` 的 UTC 当前时间计算：

```text
RevokedAt != null                        -> Revoked
ExpiresAt != null && ExpiresAt <= nowUtc -> Expired
有效 UserActiveBenefit 指针指向该权益    -> Active
否则                                      -> Available
```

对应 `UserBenefitVo`：

| 字段 | 用途 |
|------|------|
| `VoStatus / VoStatusDisplay` | 服务端派生的正式状态 |
| `VoCanActivate` | 当前是否允许选择 |
| `VoCanDeactivate` | 当前是否允许停用 |
| `VoUnavailableReason` | 未开放、尚未生效、过期或撤销原因 |
| `VoEffectiveAt / VoExpiresAt` | UTC 生效和到期时间 |
| `VoRevokedAt / VoRevocationReason` | 管理员撤销事实 |

Client 不得根据本机时间重新判定有效状态，也不得用 `VoIsActive / VoIsExpired` 覆盖 `VoStatus`。

`UserBenefitStatus` 当前没有单独的“等待生效”枚举；`EffectiveAt > nowUtc` 的记录保持 `Available`，同时由 `VoCanActivate=false` 和 `VoUnavailableReason=权益尚未生效` 表达操作边界。业务授权仍必须显式检查 `EffectiveAt`。

## 5.4 唯一选择

启用入口：

```http
POST /api/v1/Shop/ActivateBenefit/{benefitId}
```

启用前检查：

- 权益属于当前用户和租户。
- 权益未删除、未撤销、未到期且已经生效。
- 权益类型已被服务端能力策略开放。

仓储通过原子 upsert 将 `(TenantId, UserId, BenefitType)` 指针切换到目标权益。重复启用同一权益返回成功但 `VoChanged=false`，不生成重复操作事实。

停用入口：

```http
POST /api/v1/Shop/DeactivateBenefit/{benefitId}
```

停用只删除仍指向目标权益的当前指针。重复停用返回 `VoChanged=false`；并发切换后，旧请求不能误删后来选择的新权益。

两类接口都返回 `UserBenefitActionResultVo`，其中包含操作类型、目标权益状态、该类型当前权益 ID 和当前权益对象，Client 不需要猜测切换结果。

当前允许启用的权益类型为 Badge、Title、Theme：

- Badge / Title 的当前选择由 `UserAdornmentService` 批量装配为公开 `UserAdornmentVo`。
- Theme 的当前选择由 client 主题运行时读取；切回内置主题时先停用当前 Theme 权益。
- 其他权益即使存在历史拥有记录，也必须保持 `VoCanActivate=false`。

## 5.5 到期与撤销

### 实时失效

当 `ExpiresAt <= nowUtc` 时，查询和实际效果必须立即视为失效，即使定时任务尚未运行。业务正确性不能依赖任务调度及时性。

### 过期任务

`ShopJob` 分批读取到期权益 ID，并逐份委托 `UserBenefitService.ExpireBenefitAsync`：

- 条件物化 `IsExpired` 兼容标记。
- 清除仍指向该权益的 `UserActiveBenefit`。
- 写入 `Expire` 操作流水。
- 通过可靠 Outbox 请求到期通知。
- 重复执行不产生第二条过期事实或通知业务键。

### 管理员撤销

Console 使用 `console.benefits.revoke` 权限调用：

```http
POST /api/v1/Shop/AdminRevokeBenefit/{benefitId}
```

撤销原因必须为 2–500 个字符。撤销会记录操作者、UTC 时间和原因，清理当前指针并写入 `Revoke` 流水。撤销不是退款，撤销后的权益不能重新启用。

## 5.6 消耗品使用

当前真正支持使用的消耗品只有：

| 类型 | 效果 |
|------|------|
| `RenameCard` | 复用统一展示名格式、保留字、唯一性和频率规则 |
| `ExpCard` | 使用服务端操作 ID 作为业务键，原子增加经验 |
| `CoinCard` | 使用服务端操作 ID 作为业务键，原子增加胡萝卜并生成资产流水 |

帖子置顶卡、帖子高亮卡、双倍经验卡和抽奖券保持不可用。历史背包记录可以展示，但不能使用。

通用入口：

```http
POST /api/v1/Shop/UseItem
```

```json
{
  "inventoryId": "2060964941900283904",
  "quantity": 1,
  "targetId": null,
  "idempotencyKey": "inventory-use:550e8400-e29b-41d4-a716-446655440000"
}
```

改名卡正式入口：

```http
POST /api/v1/Shop/UseRenameCard
```

```json
{
  "inventoryId": "2060964941900283904",
  "newDisplayName": "新的展示名",
  "idempotencyKey": "inventory-use:550e8400-e29b-41d4-a716-446655440000"
}
```

旧 query string 改名入口只保留迁移兼容，已从 API 文档隐藏，正式 Web 不再调用。

## 5.7 使用幂等与事务

一次用户意图只生成一个 `inventory-use:{uuid}`：

- 同一键和同一请求返回第一次成功结果，并设置 `IsIdempotentReplay=true`。
- 同一键绑定不同背包项、数量、目标或改名内容时返回冲突。
- 请求摘要只保存新展示名的 SHA-256，不在幂等记录中保存敏感明文。
- 处理中请求不会再次扣减。

条件扣减、真实效果、`ShopEntitlementOperation` 和幂等终态在同一 Main 数据库事务内完成。任何效果失败都会回滚数量和操作流水。

成功结果 `UseItemResultDto` 返回：

- `OperationId`
- `IsIdempotentReplay`
- `RemainingQuantity`
- `EffectDescription / EffectType / EffectValue`
- `EffectResourceType / EffectResourceId / EffectResourceNo`

经验和胡萝卜效果还会以 `OperationId` 形成下游业务防重键，服务重试不能重复发放。

## 5.8 正式 Web 背包

正式路径为 `/shop/inventory`：

- 权益卡片展示 `Available / Active / Expired / Revoked`、来源、有效期和不可用原因。
- 操作按钮完全消费 `VoCanActivate / VoCanDeactivate`。
- 任一权益操作期间禁用重复点击，成功后刷新权益列表。
- 消耗品确认弹层在同一用户意图内稳定复用幂等键；修改数量或改名内容后生成新键。
- 成功后展示真实效果和剩余数量，只刷新背包及直接受影响的用户、经验或资产状态。

WebOS `/desktop` 复用同一 API、类型和业务 Hook，只负责历史窗口导航。Flutter 当前只读展示权益和背包，不提供使用或启停动作。

## 5.9 Console 排障

Console 用户详情按权限展示：

- `console.benefits.view`：持续权益、服务端状态、来源、有效期、撤销信息和通用操作流水。
- `console.benefits.revoke`：带原因确认的高风险撤销动作。
- 操作流水可按操作类型、权益类型和消耗品类型筛选。

操作流水类型包括 `Use / Activate / Deactivate / Expire / Revoke`。Console 只提供受控业务动作，不提供直接编辑数量、`IsActive`、`IsExpired` 或 `RevokedAt` 的万能表单。

## 5.10 数据库迁移

相关 schema ledger：

- `20260713_002_shop_entitlement_operation`：建立消耗品成功操作流水和使用幂等结果。
- `20260713_003_user_active_benefit`：建立唯一选择表、撤销字段并扩展通用权益流水。
- `20260714_001_shop_entitlement_operation_subject_nullability`：放宽 `InventoryId / ConsumableType / Quantity / ItemValue`，使非消耗品的启用、停用、过期和撤销流水不再受历史 `NOT NULL` 约束阻断。

F1-C 迁移会从合法历史 `IsActive` 数据选择每种类型最近的有效记录；多激活、无效激活和无法判断的数据进入 doctor 报告。后续字段可空迁移对 SQLite 重建表并恢复索引 / trigger，对 PostgreSQL 精确移除目标列 `NOT NULL`；verify 同时检查可空性、必要索引、唯一指针归属、实时有效性和兼容镜像。

禁止绕过 ledger 用运行时 Code First 修补正式 schema。

## 5.11 排障顺序

权益或背包异常时依次检查：

1. 当前租户、用户和资源归属。
2. 订单发放资源 ID 与来源订单唯一记录。
3. `UserBenefit` 的生效、到期和撤销字段。
4. `UserActiveBenefit` 是否指向同租户、同用户、同类型权益。
5. `ShopEntitlementOperation` 是否已有相同业务事实。
6. 使用幂等记录和效果资源流水是否一致。

不要通过直接改布尔字段、删除流水或增加背包数量掩盖根因。

---

> 下一篇：[6. 商城后端边界](/guide/shop-backend)
