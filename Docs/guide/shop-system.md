# 商城系统设计方案

> 版本：v2.1 | 最后更新：2026-07-14
>
> 状态：订单履约、消耗品使用、持续权益唯一选择和失效治理已完成；Badge、Title、Theme 已接入真实消费面，商品是否可见仍由配置与上架状态决定

## 文档导航

- [商品效力与权益履约专题](/features/shop-product-effect-entitlement-fulfillment)：F1 商品开放矩阵与专题边界
- [2. 核心概念](/guide/shop-core-concepts)：商品类型、订单失败阶段、权益状态和真相源
- [3. 商品管理](/guide/shop-product)：商品模型、配置、库存和上下架
- [4. 订单系统](/guide/shop-order)：购买、支付证据、快照履约和重试
- [5. 权益与背包](/guide/shop-inventory)：消耗品使用、持续权益选择、过期和撤销
- [6. 后端设计](/guide/shop-backend)：分层、API、权限和 schema ledger
- [7. 前端与操作说明](/guide/shop-frontend)：正式 Web、WebOS 兼容和 Console 排障
- [商城正式 Web 回流](/guide/shop-web-return-paths)：正式路由、登录恢复和错误恢复
- [WebOS 商城回访](/guide/shop-workspace-revisit)：`/desktop` 历史深链

## 1. 产品定位

Radish 商城使用胡萝卜作为社区内部资产，让用户购买消耗品和持续权益。商城不是独立电商平台，目标是为社区身份表达和参与激励提供可追溯、可恢复的虚拟商品能力。

当前不实现实物、购物车、优惠券、赠送、交易市场、提现和完整退款平台。

## 2. 核心原则

- **服务端权威**：可售、可启用、有效状态和失败恢复都由服务端判断。
- **快照履约**：历史订单只使用下单快照，不依赖商品当前配置。
- **资产一致性**：扣款、履约、使用和真实效果具备事务或幂等保护。
- **拥有与选择分离**：`UserBenefit` 保存拥有，`UserActiveBenefit` 保存每种类型当前选择。
- **实时 UTC 失效**：读取和效果消费实时判断到期，定时任务只负责物化与通知。
- **可追溯治理**：订单、来源发放、业务流水和 Console 排障形成稳定证据链。
- **逐类开放**：只有配置、发放、使用/启用、真实消费、失效和回归均完成的类型才能开放。

## 3. 当前支持范围

### 消耗品

| 类型 | 购买 | 使用 | 效果追溯 |
|------|------|------|----------|
| 改名卡 | 支持 | 支持 | 展示名变更上下文 + 商城操作流水 |
| 经验卡 | 支持 | 支持 | 经验流水 + 商城操作流水 |
| 萝卜币红包 | 支持 | 支持 | 胡萝卜流水 + 商城操作流水 |
| 置顶卡/高亮卡 | 禁止 | 禁止 | 仅兼容历史读取 |
| 双倍经验卡/抽奖券 | 禁止 | 禁止 | 待独立专题 |

### 持续权益

| 类型 | 当前能力 | 真实消费面 |
| --- | --- | --- |
| Badge | 可售、可启用 | 公开主页、帖子、问答回答、评论与回复的共享身份装饰 |
| Title | 可售、可启用 | 与 Badge 相同，并受长文本布局约束 |
| Theme | 可售、可启用 | client 四主题运行时；权益资源只允许暗夜与樱花 |
| AvatarFrame / Signature / NameColor / LikeEffect | 不可售、不可启用 | 尚未建立完整消费与失效链路 |

能力开放只表示服务端允许合法配置进入销售与启用流程，不会强制修改历史商品的 `IsOnSale`。Badge 必须绑定公开有效附件，Title 必须提供合法文本，Theme 必须使用共享资源白名单。

## 4. 总体架构

```text
正式 Web / WebOS 兼容 / Flutter 只读 / Console
                    │
               ShopController
                    │
      ┌─────────────┼─────────────────┐
 ProductService  OrderService  UserBenefitService
                                  │
                         UserInventoryService
                    │
      ┌─────────────┼─────────────────┐
   商品与订单     权益与背包       资产/经验/通知
                    │
       PostgreSQL / SQLite + schema ledger
```

正式产品主线是纯 Web。WebOS `/desktop` 复用相同业务 API，只保留历史窗口和深链；Flutter 继续承接既有购买、订单和背包只读能力，不新增使用或权益启停。

## 5. 购买与履约

```text
浏览商品
  -> 购买资格预检
  -> 支付口令校验
  -> 绑定 shop:{uuid}
  -> 扣库存并创建订单快照
  -> 扣减胡萝卜
  -> 按订单快照发放
  -> 写明确履约资源 ID
  -> 可靠请求购买通知
```

支付失败记录 `Failed(Payment)`，禁止发放；支付后履约失败记录 `Failed(Fulfillment)`，只有真实扣款流水与订单完全匹配时才允许管理员重试。

## 6. 权益与背包

持续权益和消耗品使用不同模型：

- `UserBenefit`：一份持续权益拥有记录。
- `UserActiveBenefit`：每种权益类型当前唯一选择。
- `UserInventory`：同类、同值消耗品当前数量。
- `UserInventoryGrantRecord`：消耗品订单来源事实。
- `ShopEntitlementOperation`：使用、启用、停用、过期和撤销成功事实。

客户端只消费服务端派生的 `Available / Active / Expired / Revoked`，不按本地时间重算。

## 7. 消耗品使用

正式 Web 为一次使用意图生成 `inventory-use:{uuid}`。条件扣减、真实效果、操作流水和幂等终态位于同一事务；重复请求回放首次结果，同键异参返回冲突。

使用成功返回操作 ID、剩余数量、效果类型、效果值和效果资源，便于用户确认和 Console 排障。

## 8. Console 治理

### 商品

- 商品详情、编辑、上下架和删除拦截。
- 乐观并发版本保护。
- 不可用商品类型由服务端拒绝上架。

### 订单

- 支付证据、失败阶段和明确履约资源。
- 用户、商品和胡萝卜流水回跳。
- 只对具备履约重试资格的订单提供受权操作。

### 用户权益

- 查看服务端状态、来源、有效期和撤销信息。
- 查看通用商城权益操作流水。
- 使用独立权限和原因确认撤销权益。
- 不提供直接编辑状态或数量的万能表单。

## 9. 数据库演进

商城 schema 通过 `Radish.DbMigrate` ledger 演进：

1. 订单履约安全迁移。
2. 消耗品操作流水迁移。
3. 持续权益唯一选择与撤销迁移。
4. 通用权益流水主体字段可空迁移，允许 Badge / Title / Theme 的启停、过期与撤销流水不伪造消耗品字段。

每个迁移都有 doctor、apply、verify 和重入测试。baseline 后禁止运行时 Code First 静默修补正式数据库。

## 10. 正式入口

- 公开商城：`/shop`
- 商品详情：`/shop/product/:productId`
- 我的订单：`/shop/orders`
- 订单详情：`/shop/order/:orderId`
- 我的背包：`/shop/inventory`
- Console：`/console/`
- WebOS 历史入口：`/desktop?app=shop...`

浏览器联调默认通过 Gateway 访问；真实 smoke 只在成组验收且当前任务明确确认服务已启动后执行。

## 11. 相关系统

- [胡萝卜机制说明](/guide/radish-coin-mechanisms)
- [通知系统](/guide/notification-realtime)
- [经验等级系统](/guide/experience-level-system)
- [时间语义与业务自然日](/guide/time-semantics)
- [Client 与 Console 跨应用导航](/frontend/client-console-navigation-contract)
