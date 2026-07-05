# P3-12-D62 资产 / 订单 / 背包页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
>
> 状态：已完成 `P3-12-D62 Private / Author Pencil 逐页 UI 与功能缺口实现` 中资产、订单、背包页面族首批对齐

## 背景

`P3-12-D62` 已完成 `/workbench` 与 `/me` 内容历史复访组首批实现。按 [P3-12-D60 Pencil 逐页 UI 与功能缺口复盘](/records/p3-12-d60-pencil-page-ui-and-function-gap-review-2026-07-04) 和当前规划，下一顺位进入资产 / 订单 / 背包页面族，对照 `private-web-workflows.pen` 的 `P07-P11 / P24-P25`。

本批继续留在 `P3-12-D` UI 专题，不进入 `P3-12-E`，不启动发布或生产部署流程。

## 设计源读取

- 通过 Pencil MCP 确认当前活动文件为 `Docs/frontend/design-sources/private-web-workflows.pen`。
- `snapshot_layout` 与导出截图抽查：
  - `P07 - Assets Overview`：PC 为余额摘要、近期流水和右侧正式 Web 资产范围说明。
  - `P08 - Asset Transactions`：PC 为紧凑流水表、筛选 chips 和右侧流水详情 rail。
  - `P09 - Orders List`：PC 为订单状态分组和购买后回流。
  - `P10 - Order Detail`：PC 为支付 / 发放 / 背包回流时间线和右侧状态摘要。
  - `P11 - Inventory`：PC 为权益 / 道具列表、可用状态、来源订单和右侧背包摘要。
  - `P24 / P25` 移动任务流：移动端保留资产、订单和背包的单列任务节奏、状态摘要和回流边界。
- 本批未修改 `.pen` 设计源。

## 实现范围

- `Frontend/radish.client/src/me/MeAssetsPage.tsx`
  - `/me/assets` 增加余额任务面大数字、当前视图收入 / 支出 / 异常流水 rail。
  - `/me/assets/transactions` 保留现有类型 / 状态筛选和分页，补流水上下文 rail 与最新记录摘要。
  - 继续使用 `coinApi.getBalance` 与 `coinApi.getTransactions`，不新增资产 API。
- `Frontend/radish.client/src/apps/shop/pages/OrderList.tsx`
  - `/shop/orders` 按现有订单状态分为待处理、已完成、需关注三组。
  - 保留当前页分页、真实订单详情链接和旧桌面应用内导航。
- `Frontend/radish.client/src/apps/shop/pages/OrderDetail.tsx`
  - `/shop/order/:orderId` 将时间线收敛为数据驱动渲染。
  - 补订单状态、支付金额、背包状态和进入背包的右侧 rail。
  - 正式 Web 路由接入 `/shop/inventory` href，旧桌面视图继续使用内部导航函数。
- `Frontend/radish.client/src/apps/shop/pages/Inventory.tsx`
  - `/shop/inventory` 补可用权益、启用中权益、来源订单和消耗品总数量摘要。
  - 主列表外增加任务 chips 与右侧背包 rail，保留权益激活 / 停用、道具使用、来源订单 / 商品回流。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐资产、订单和背包任务面的中英文文案。
- 对应 CSS 文件
  - PC 下使用主内容 + rail 结构，移动端收为单列任务流。

## 保持不变

- 不新增业务 API。
- 不新增权限键。
- 不修改数据库结构。
- 不修改路由语义。
- 不修改保存 / 提交载荷。
- 不迁移 WebOS Dock、窗口系统、桌面背景或窗口几何语义。
- 不扩完整钱包、退款 / 售后、支付口令、资产风控或完整移动商城。
- 不进入 `P3-12-E`。

## 后续 D62 待办

1. 下一批建议继续通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态页面族。
2. 完整钱包、退款 / 售后、支付口令、资产风控、完整聊天平台和复杂资料 / 安全设置继续作为 D62 内后置产品 / API 缺口记录。
3. 若需要真实 Gateway smoke，仍必须先由用户当轮明确说明前后端已启动。

## 验证

- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。
- Gateway 真实页面 smoke：通过。
  - 账号：本地开发种子账号 `admin@radishx.com / admin123456`。
  - PC：`1920x1080` 覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/931200000001`、`/shop/inventory`；确认资产 rail、流水 rail、订单状态分组、订单详情侧栏、背包 rail 和真实链接存在，无横向溢出。
  - 移动：`390x844 @ DPR 3` 覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/931200000001`、`/shop/inventory`；确认主内容与 rail 单列化，无横向溢出。
  - 浏览器 console：无 error / warning。
  - 真实数据复核发现后端 `OrderVo.VoStatus` 以数字枚举进入前端时会让订单分组误判；本批已同步补 `normalizeOrderStatus`，订单分组、状态色、可取消判断和订单详情背包状态统一走规范化状态。
  - 截图留存在 `output/playwright/d62-assets-orders-assets-overview-desktop-1920.png`、`output/playwright/d62-assets-orders-assets-transactions-desktop-1920.png`、`output/playwright/d62-assets-orders-shop-orders-desktop-1920.png`、`output/playwright/d62-assets-orders-shop-order-detail-desktop-1920.png`、`output/playwright/d62-assets-orders-shop-inventory-desktop-1920.png` 以及对应 `mobile-390-dpr3` 文件。

## 未执行

- 未执行发布 / 部署级回归。
