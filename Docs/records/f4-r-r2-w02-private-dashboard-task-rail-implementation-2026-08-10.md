# F4-R R2-W02 Private 仪表 / 任务侧栏正式实现与 Gateway 验收

> 日期：2026-08-10（Asia/Shanghai）
> 状态：设计—实现—Gateway PC / mobile 运行态闭环已完成，专题关闭

## 1. 本批结论

`R2-W02` 已把确认的“主任务宽列—紧凑摘要—窄辅助轨”接入 Notifications、Me、Circle、Pet、Private Shop 与 Workbench 正式页面，并在 Mobile 固定为“主任务—摘要—折叠辅助信息”。实现继续继承 `R1-F01 / R1-W01`，没有新增 API、数据库、权限、业务动作、移动壳层或 WebOS 窗口结构，也没有再次修改 Pencil。

## 2. 共享响应式契约

- 新增 `WebTaskRailDisclosure` 作为 Private 页面族统一辅助轨容器；PC 保持原网格中的 `display: contents`，不额外制造卡片或布局层。
- `720px` 及以下只显示带 `aria-controls / aria-expanded` 的轻量折叠入口，辅助信息默认收起并在用户请求后展开。
- Notifications、Circle、Pet、Workbench、Me 账户摘要，以及 Private Shop 订单详情 / 背包均消费同一契约；没有为各页面复制独立的移动折叠状态机。
- 删除 Circle、Pet 和 Me 中把完整辅助轨提前到主任务前的 `order: -1`，以 DOM 与 CSS 双重固定 Mobile 主任务优先。

## 3. 页面落地

- Notifications 将三个核心指标压缩为紧凑摘要，通知列表保持主任务，偏好和上下文进入右侧辅助轨；权威 loading / unavailable / stale 与 dirty 保护保持既有能力门禁。
- Circle 将关系摘要压缩到主任务前的轻量状态行，圈子内容与管理入口保持主轴，关系上下文进入辅助轨。
- Pet 将等级、心情与活力压缩为首屏摘要，资料 / 照料任务保持主轴，养成上下文进入辅助轨；无宠物时仍使用原权威认领空态。
- Workbench 保持今日任务队列为主轴，状态摘要进入辅助轨，能力地图继续位于主任务之后。
- Me 新增以最近内容为来源的“最近回访”主任务区，现有详情区随后呈现，账户概览进入辅助轨；局部来源失败仍按既有独立权威状态裁决。
- Private Shop 订单详情把上下文信息收口到辅助轨；背包移除主任务前的大型摘要网格，把指标并入右侧轨道，现有标签、资产列表和结构化错误保持主轴。

## 4. 验证与运行态验收

### 4.1 代码侧验证

- `npm run test --workspace=radish.client`：`526 / 526` 通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过；仅保留既有大 chunk 提示。
- 新增 Private 仪表 / 任务侧栏静态契约测试 `4 / 4`：覆盖共享折叠语义、七处 consumer、禁止 `order: -1`、Me Mobile 顺序和背包摘要收口。
- `./scripts/check-docs.sh`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

### 4.2 Gateway 浏览器验收

- 经用户明确授权启动 Gateway、API、Auth、Client 与 Console，并使用项目种子账号登录；验收结束后恢复浏览器视口、关闭页签并停止全部服务。
- PC `1440 × 980` 覆盖 `/me`、`/notifications`、`/circle`、`/pet`、`/workbench`、`/shop/orders` 与 `/shop/inventory`：主任务宽列、紧凑摘要和窄辅助轨结构清晰，没有新增窗口壳层或卡片堆叠。
- Mobile `390 × 844` 覆盖同组入口：所有页面 `scrollWidth === clientWidth`，主任务均先于完整辅助轨；Notifications、Circle、Me 与 Inventory 的折叠入口已实际点击验证 `aria-expanded=false → true` 和内容显隐。
- Me 的实际几何顺序确认详情区先于 Account overview；Workbench 的今日任务先于状态轨，Private Shop 背包的资产主任务先于指标轨。
- 验收结束时浏览器 `error` 日志为 `0`。

本地种子库的 `PetProfile / ShopOrder / ShopUserBenefit / ShopUserInventory` 记录数均为 `0`，因此 Pet 已认领态和订单 / 背包有数据态无法在不制造业务数据的前提下完成运行态复核。本批没有写入测试数据：Pet 认领空态、订单空态和背包空态已通过浏览器复核，有数据结构、折叠消费与响应式顺序由静态契约、全量测试和 production build 守卫。

## 5. 停止线与下一步

1. `R2-W02` 保持关闭；不重开 Pencil，不新增页面专属折叠实现，也不扩入 API、数据库、权限或 WebOS 新能力。
2. 下一顺位进入 `R2-A02 Author 列表、修订与 Forum 发布差异` 设计前代码事实与能力覆盖审计。
3. 先核对 Docs Mine / Revisions、Forum Compose 与 `R1-A01 / R1-P02` 的继承是否成立；R2-A02 关闭前不成组推进 R3 派生页面。
