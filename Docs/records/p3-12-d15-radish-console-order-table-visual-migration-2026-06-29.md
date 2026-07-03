# P3-12-D15 radish.console 订单表格代表页视觉迁移记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D14` 已完成 `radish.console` 壳层分组和首批页面语义组件，并将 `SystemConfigList` 作为低风险代表页迁入 `ConsolePageHeader`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleStatusChip` 和 `ConsoleToolbar`。

本批继续选择高频表格代表页验证组件复用边界。对比订单页和用户页后，优先迁移 `OrderList`：订单页同时包含 URL 筛选状态、来源返回、详情抽屉、失败重试、管理员备注权限、用户 / 商品 / 胡萝卜流水跳转，更能覆盖商业表格页的真实密度和权限反馈。

## 实施范围

- 将 `OrderList` 页头迁入 `ConsolePageHeader`，保留商业运营语义、来源返回和备注权限状态。
- 将订单指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前结果、本页订单、本页完成和发放失败四项指标。
- 将订单筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留用户 ID、订单状态、商品 ID、订单号、搜索和重置控件。
- 删除 `OrderList.css` 中旧页头类名对应的无效移动端样式。

## 边界

- 不修改订单 API、权限键、URL 查询参数、分页参数、表格列、详情抽屉、失败重试、备注保存或后端契约。
- 不新增批量操作，不改变失败订单重试入口；仍按逐行权限和详情页动作承载。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续按阶段验收或用户明确要求执行，且执行前需等待用户确认前后端已启动。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 继续迁移 `UserList` 或商品 / 文档等表格 CRUD 页，优先确认对象摘要侧栏、复杂筛选和权限反馈在语义组件下的展示密度。
- 迁移权限矩阵、内容治理或经验治理前，先判断是否需要扩展 `ConsoleToolbar` 或新增工作台区块组件，避免压扁批量操作、风险确认和治理动作反馈。
