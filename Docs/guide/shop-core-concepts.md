# 2. 商城核心概念

> 版本：v2.0 | 最后更新：2026-07-13
>
> 入口页：[商城系统设计方案](/guide/shop-system)

本页说明商城运行时使用的核心名词、状态和真相源。商品开放范围以服务端能力策略为准；具体履约规则见[订单系统](/guide/shop-order)，背包、权益选择和使用规则见[权益与背包](/guide/shop-inventory)。

## 2.1 商品类型

| `ProductType` | 购买结果 | 当前边界 |
|---------------|----------|----------|
| `Benefit` | 生成一份持续权益 `UserBenefit` | 当前所有权益类型仍禁止销售和启用，待真实消费面逐类开放 |
| `Consumable` | 增加 `UserInventory` 聚合数量并记录订单级发放事实 | 改名卡、经验卡、萝卜币红包已具备真实使用能力 |
| `Physical` | 无 | 不实现，不允许创建为可售商品 |

商品“存在于枚举或种子数据”不等于已经开放。`ShopProductAvailabilityPolicy` 是当前服务端可用性真相；Client 和 Console 可以展示原因，但不能绕过服务端判断。

## 2.2 持续权益类型

当前 `BenefitType` 包含：

| 值 | 类型 | 产品含义 | 当前状态 |
|----|------|----------|----------|
| `1` | `Badge` | 公开身份徽章 | 未开放；F1-D 完成公开消费面后首批开放候选 |
| `2` | `AvatarFrame` | 头像装饰 | 未开放 |
| `3` | `Title` | 公开身份称号 | 未开放；F1-D 完成公开消费面后首批开放候选 |
| `4` | `Theme` | 产品主题 | 未开放；真实效果归属 F2 |
| `5` | `Signature` | 内容签名 | 未开放 |
| `6` | `NameColor` | 用户名颜色 | 未开放 |
| `7` | `LikeEffect` | 轻回应动画 | 未开放 |

一名用户可以拥有同类型的多份权益，但每种类型最多只有一个“当前选择”。拥有事实与选择事实必须分离：

- `UserBenefit` 表示用户拥有的一份权益。
- `UserActiveBenefit` 表示该用户某种权益类型的当前选择。
- `(TenantId, UserId, BenefitType)` 唯一约束保证同类型只有一个当前指针。

## 2.3 消耗品类型

| 值 | 类型 | 真实效果 | 当前状态 |
|----|------|----------|----------|
| `1` | `RenameCard` | 修改公开展示名 | 已支持 |
| `2` | `PostPinCard` | 帖子置顶 | 已废弃，仅兼容历史读取 |
| `3` | `PostHighlightCard` | 帖子高亮 | 已废弃，仅兼容历史读取 |
| `4` | `ExpCard` | 原子增加经验并生成经验流水 | 已支持 |
| `5` | `CoinCard` | 原子增加胡萝卜并生成资产流水 | 已支持；不自动新增种子商品 |
| `6` | `DoubleExpCard` | 临时经验倍率 | 未开放 |
| `99` | `LotteryTicket` | 抽奖活动凭证 | 未开放，归属抽奖专题 |

消耗品按 `(TenantId, UserId, ConsumableType, ItemValue)` 聚合数量。聚合行不是订单来源历史；每个来源订单由 `UserInventoryGrantRecord` 单独防重和追溯。

## 2.4 订单状态与失败阶段

`OrderStatus` 描述用户可见主状态：

| 状态 | 含义 |
|------|------|
| `Pending` | 已创建、尚未完成支付 |
| `Paid` | 已形成支付事实，正在履约 |
| `Completed` | 支付与商品发放均完成 |
| `Cancelled` | 待支付订单被用户或系统取消 |
| `Refunded` | 预留状态；当前不提供退款流程 |
| `Failed` | 购买失败，必须结合 `OrderFailureStage` 判断 |

`OrderFailureStage` 将失败分成两个不同的恢复边界：

| 阶段 | 含义 | 是否允许重试发放 |
|------|------|------------------|
| `None` | 当前没有失败 | 否 |
| `Payment` | 未形成可信支付事实 | 否 |
| `Fulfillment` | 支付成功后商品发放失败 | 校验支付证据通过后允许 |

不得仅凭 `Status=Failed` 发放商品，也不得把支付失败订单改写成履约失败。

## 2.5 权益有效状态

服务端统一派生 `UserBenefitStatus`：

```text
RevokedAt != null                       -> Revoked
ExpiresAt != null && ExpiresAt <= nowUtc -> Expired
存在有效 UserActiveBenefit 指针          -> Active
否则                                     -> Available
```

- `Available`：已拥有且不是该类型当前选择；若 `EffectiveAt > nowUtc`，仍显示为该状态，但 `VoCanActivate=false` 并返回“尚未生效”原因。
- `Active`：已拥有、有效且被当前选择指针引用。
- `Expired`：到达服务端 UTC 到期时间。
- `Revoked`：已被管理员撤销，不可再次启用。

`IsActive / ActivatedAt / IsExpired` 是迁移兼容字段，不再是授权真相。查询、展示和实际效果消费都必须实时比较 UTC；定时任务只负责物化过期标记、清理指针、写流水和请求通知。

## 2.6 有效期

| `DurationType` | 计算方式 |
|----------------|----------|
| `Permanent` | `ExpiresAt = null` |
| `Days` | 从订单履约时间起增加订单快照中的 `DurationDays` |
| `FixedDate` | 使用订单快照中的 `FixedExpiresAt` |

持续权益的有效期从履约完成时开始，不延迟到首次启用。同一用户再次购买相同、仍有效的持续权益时默认拒绝；当前不实现自动续期或时长叠加。

## 2.7 业务真相源

| 模型 | 职责 |
|------|------|
| `Product` | 当前商品配置和可售状态 |
| `Order` | 购买、支付、履约快照及最终结果 |
| `CoinTransaction` | 扣款或资产变化事实 |
| `UserBenefit` | 持续权益拥有事实 |
| `UserActiveBenefit` | 每种权益类型当前选择 |
| `UserInventory` | 消耗品当前聚合数量 |
| `UserInventoryGrantRecord` | 消耗品订单级发放事实 |
| `ShopEntitlementOperation` | 使用、启用、停用、过期、撤销的成功业务流水 |

审计日志不替代业务真相，业务流水也不保存支付口令、完整异常堆栈或敏感输入明文。

## 2.8 时间、租户与外部 ID

- 业务有效状态使用 `TimeProvider` 提供的 UTC 当前时间。
- 所有查询和写入必须受当前租户边界约束，不能只按用户或资源 ID 查询。
- 服务端实体主键继续使用 `long`；HTTP、URL、前端状态和通知载荷中的 LongId 必须按字符串传递。
- JavaScript / Dart 客户端不得通过 `Number(...)`、`parseInt(...)` 或 `int.tryParse` 承接外部 LongId。

## 2.9 当前产品停止线

- 不实现实物、购物车、优惠券、赠送、交易市场和完整退款平台。
- 不恢复帖子置顶卡和高亮卡。
- 不开放没有真实消费面、失效处理、Console 排障和回归测试的商品类型。
- Flutter 只读承接既有订单和背包，不新增使用或权益启停动作。
- WebOS `/desktop` 只保留历史兼容，不维护第二套商城规则。

---

> 下一篇：[3. 商品管理](/guide/shop-product)
