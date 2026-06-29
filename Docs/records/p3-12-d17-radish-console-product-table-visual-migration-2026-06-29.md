# P3-12-D17 radish.console 商品表格代表页视觉迁移记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D14` 已完成 Console 壳层和语义组件首批，`P3-12-D15` 与 `P3-12-D16` 已分别完成订单和用户表格代表页迁移。本批继续迁移 `ProductList`，用于验证商品运营场景下的创建、详情、编辑、上下架、删除、关联订单跳转和对象摘要侧栏密度。

文档侧当前入口为 `DocumentGovernancePage`，更接近治理工作台而不是普通列表页；本批不将其混入表格 CRUD 迁移。

## 实施范围

- 将 `ProductList` 页头迁入 `ConsolePageHeader`，保留商品运营语义、新建商品入口和创建权限状态。
- 将商品指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前结果、本页商品、本页上架和本页启用四项指标。
- 将商品筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留分类、商品类型、上架状态、关键词、搜索和重置控件。
- 删除 `ProductList.css` 中旧页头类名对应的无效移动端样式和迁移后不再使用的页头动作样式。

## 边界

- 不修改商品 API、权限键、URL 查询参数、分页行为、表格列、详情抽屉、创建 / 编辑表单、上下架、删除、关联订单跳转或后端契约。
- 不新增批量上下架、批量删除或商品治理审批流；商品高风险动作仍按既有逐行动作与确认弹窗承载。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续按阶段验收或用户明确要求执行，且执行前需等待用户确认前后端已启动。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 继续评估 `DocumentGovernancePage` 是否先拆分治理工作台区块，再迁入 Console 语义组件；不要把发布、归档、访问策略、导入导出和版本回滚强行压进普通表格工具条。
- 若继续推进普通表格 CRUD，可优先看标签、分类、贴纸等低风险列表页是否还需要同口径迁移。
