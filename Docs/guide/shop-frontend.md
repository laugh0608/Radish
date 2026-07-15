# 7. 商城前端与操作说明

> 版本：v2.2 | 最后更新：2026-07-15
>
> 入口页：[商城系统设计方案](/guide/shop-system)

正式产品主线是纯 Web。`radish.client` 同时承接公开商城和登录态订单/背包；`/desktop` 的 WebOS 商城窗口只保留历史兼容，并复用同一 API、类型和业务 Hook。

## 7.1 路由

| 路径 | 登录 | 用途 |
|------|------|------|
| `/shop`、`/shop/products` | 否 | 公开商品浏览 |
| `/shop/product/:productId` | 否 | 公开商品详情和分享 |
| `/shop/product/:productId?intent=purchase` | 是 | 登录恢复后的购买意图 |
| `/shop/orders` | 是 | 我的订单 |
| `/shop/order/:orderId` | 是 | 我的订单详情 |
| `/shop/inventory` | 是 | 我的权益与背包 |
| `/desktop?app=shop...` | 视目标而定 | WebOS 历史深链 |

公开 canonical、分享和 sitemap 只使用无购买意图的商品详情 URL。订单、背包和购买意图不进入公开索引。

## 7.2 数据访问边界

- 所有请求通过 `@radish/http` 统一客户端，不新建 fetch/axios 封装。
- `radish.client/src/api/shop.ts` 维护用户侧商城调用。
- Client 与 Console 的订单、权益和流水 ID 均使用 `LongId` 字符串。
- 业务时间只用于格式化展示，不在浏览器重新判定权益有效状态。
- 服务端 `MessageModel<T>` 失败不能被当成成功数据消费。
- 商品、订单、权益、能力与失败阶段按稳定枚举值或 key 决定筛选、状态样式和操作资格；`vo*Display` 只作兼容展示，不参与控制流或本地化映射。
- 商品名称、描述、管理员备注、失败原因和撤销原因等配置型或人工内容保持原文；金额、数量和日期按当前 locale 展示，英文可计数文案使用 plural 规则。

## 7.3 购买流程

1. 匿名用户在公开商品详情点击购买后进入 OIDC，回到原商品和 `intent=purchase`。
2. 登录用户先请求 `CheckCanBuy`；失败时展示原因，不打开支付口令弹层。
3. 打开购买确认时生成 `shop:{uuid}`，同一次提交和网络重试复用该键。
4. 用户输入 6 位数字支付口令并提交 `Purchase`。
5. 成功后刷新余额、订单、背包和购买资格，并进入 `/shop/order/:orderId`。
6. 失败时保留当前商品上下文，不记录支付口令或完整请求对象。

用户修改商品、数量或重新发起购买意图时生成新幂等键；单纯按钮重复点击或网络重试不能换键。

## 7.4 订单页面

订单列表和详情展示服务端状态，不根据 `PaidTime` 自行推断：

- `Pending`：待支付。
- `Paid`：已支付，等待履约。
- `Completed`：购买完成。
- `Cancelled`：已取消。
- `Failed + Payment`：支付失败。
- `Failed + Fulfillment`：履约失败。

订单详情显示商品快照、数量、价格、时间、失败原因和已发放资源。用户侧不提供履约重试；管理员在 Console 根据服务端资格处理。

订单详情可回到商品详情，来源返回使用正式 Web URL；不得把普通浏览器用户强制带回 `/desktop`。

## 7.5 权益页面

`/shop/inventory` 的权益标签直接消费 `UserBenefitVo`：

| 字段 | UI 用途 |
|------|---------|
| `voStatus` | 状态徽标、筛选和控制流的稳定值 |
| `voStatusDisplay` | 兼容展示回退，不参与控制流 |
| `voCanActivate` | 是否显示可用的“启用”动作 |
| `voCanDeactivate` | 是否显示可用的“停用”动作 |
| `voUnavailableReason` | 禁用原因和按钮提示 |
| `voSourceOrderId / voSourceProductId` | 来源订单和商品回访 |
| `voExpiresAt / voDurationDisplay` | 有效期展示 |
| `voRevokedAt / voRevocationReason` | 治理结果展示 |

权益操作返回 `UserBenefitActionResultVo`。前端应以结构化结果和刷新后的列表为准，不在本地切换多个卡片的 `isActive` 布尔值。

Badge、Title、Theme 权益可以按服务端 `voCanActivate / voCanDeactivate` 启停；其他类型只展示历史拥有记录和明确不可用原因。Badge / Title 切换后，公开主页、帖子、问答回答、评论和回复使用同一 `UserAdornment` 契约刷新身份装饰；Theme 切换由根主题运行时接管，页面不得局部模拟主题状态。

## 7.6 消耗品页面

当前可操作：

- 改名卡
- 经验卡
- 萝卜币红包

置顶卡、高亮卡、双倍经验卡和抽奖券只允许历史展示，使用按钮保持禁用。

使用流程：

1. 用户打开使用确认弹层，前端生成 `inventory-use:{uuid}`。
2. 同一弹层和同一用户意图内复用该键。
3. 修改数量或新展示名后生成新键。
4. 提交时禁用重复点击。
5. 成功后展示实际效果、剩余数量和可回看的资源编号。
6. 只刷新背包及被影响的资料、经验或资产状态。

改名卡调用 body DTO 的 `UseRenameCard`；正式 Web 不调用旧 query string 兼容入口。

## 7.7 状态与错误

列表、详情和操作页必须分别处理：

- 加载中
- 空数据
- 未登录或会话失效
- 无权限
- 资源不存在
- 服务端业务失败
- 网络/非 JSON 异常

操作失败时保留背包项和表单上下文，并展示可理解的服务端原因。不得通过静默刷新或默认成功掩盖失败。

Client 与 Console 的商城 helper 统一抛出 `ApiResponseError`，保留 `httpStatus / code / messageKey / traceId`。页面以 HTTP status、稳定 `Code`、`voCan*` 和明确数据状态决定未登录、无权限、not-found、并发冲突或履约重试资格；本地提示优先解析 `MessageKey`，缺少资源时使用服务端安全 `MessageInfo`，不得匹配中英文错误消息。

## 7.8 Console 商品和订单治理

Console 通过 Gateway 的 `/console/` 访问：

- 商品页：详情、编辑、上下架、删除拦截和相关订单。
- 订单页：失败阶段、支付证据、履约资源、管理员备注和重试资格。
- 订单详情：可回到用户、商品和胡萝卜流水。

重试按钮只在 `voCanRetryFulfillment` 成立时展示，但服务端仍会再次验证支付证据。Console 不提供直接编辑订单状态或发放资源 ID 的表单。

商品表单从服务端能力矩阵读取稳定类型与能力元数据。配置要求优先解析 `voConfigurationRequirementKeys`，不可售原因优先解析 `voUnavailableReasonKey`；兼容的 `voConfigurationRequirements / voUnavailableReason` 只在缺少本地资源时展示，不能决定类型是否可售、是否可启用或是否允许上架。

## 7.9 Console 用户权益治理

用户详情新增：

- 持续权益列表及 `Available / Active / Expired / Revoked` 状态。
- 来源、有效期、撤销时间和原因。
- `Use / Activate / Deactivate / Expire / Revoke` 通用操作流水。
- 具有 `console.benefits.revoke` 权限时的撤销确认。

撤销必须填写 2–500 字符原因。页面不能直接编辑 `IsActive`、`IsExpired`、背包数量或撤销时间。

## 7.10 Client 与 Console 导航

- 从 Client 进入 Console 使用真实 `/console/...` URL，并通过 `backTo` 携带产品端来源。
- Console 内部返回继续使用 `returnTo`，不得与 `backTo` 混用。
- Console 页头固定提供“返回社区”；无合法 `backTo` 时回 `/workbench`。
- 订单、商品、用户和流水查询参数保持字符串 LongId。

完整规则见[Client 与 Console 跨应用导航契约](/frontend/client-console-navigation-contract)。

## 7.11 响应式与无障碍

- PC 与移动视图都要保留商品名、状态、价格和主要动作的可读层级。
- 操作按钮具备明确 `type`、禁用态和处理中反馈。
- 图片缺失时使用语义图标或空态，不显示破损占位。
- 长错误信息、订单号和资源编号允许换行或复制，不撑破卡片。
- 移动端不把 Console 桌面表格能力机械复制为同密度页面。

公开主页、帖子、问答回答、评论和回复已经使用共享身份装饰组件。长称号必须允许收缩或换行，不能挤压作者名、楼层、时间或操作按钮；徽章附件不可公开、被禁用、删除或审核拒绝时，服务端应直接省略徽章，不输出失效图片地址。

## 7.12 Flutter 与 WebOS

- Flutter 保留商品浏览、购买、订单和背包只读能力，不新增使用、启停或 Console 治理。
- WebOS 商城窗口继续支持历史商品、订单和背包深链，但不维护独立业务规则。
- Tauri 冻结为实验资产，不进入当前商城开发和验证门禁。

## 7.13 验证

开发中优先执行：

- Client / Console type-check
- changed-only lint
- 组件与静态契约测试
- production build
- `git diff --check`
- changed-file repo hygiene

真实 Gateway PC / mobile smoke 在商城专题准备成组验收时执行，且必须在当前任务获得服务启动授权。

---

> 相关：[商城正式 Web 回流与 WebOS 深链保留](/guide/shop-web-return-paths)
