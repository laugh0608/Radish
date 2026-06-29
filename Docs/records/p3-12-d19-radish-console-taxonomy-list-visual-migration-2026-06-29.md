# P3-12-D19 radish.console 标签与分类列表视觉迁移记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D14-D18` 已完成 Console 壳层首批、订单 / 用户 / 商品表格代表页迁移，以及文档治理首屏区块边界迁移。标签与分类属于内容分类法治理页，业务动作完整但页面结构相对稳定，适合作为 Console 普通列表页的视觉收口批次。

本批目标是迁移 `TagList` 与 `CategoryList` 的页头、指标和筛选工具条，继续验证 D14 语义组件在低风险普通 CRUD 列表中的复用边界，同时保持原有筛选、批量操作、权限反馈和表格密度不变。

## 实施范围

- 将 `TagList` 页头迁入 `ConsolePageHeader`，保留刷新、新增标签入口和创建权限状态。
- 将 `TagList` 指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前结果、本页标签、本页启用和固定标签四项指标。
- 将 `TagList` 筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留关键词、标签类型、启用状态和回收站范围筛选。
- 将 `CategoryList` 页头迁入 `ConsolePageHeader`，保留刷新、新增分类入口和创建权限状态。
- 将 `CategoryList` 指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前结果、本页分类、本页启用和顶级分类四项指标。
- 将 `CategoryList` 筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留关键词、启用状态和回收站范围筛选。
- 删除 `TagList.css` 与 `CategoryList.css` 中迁移后不再使用的旧页头动作样式和旧移动端页头样式。

## 边界

- 不修改标签 / 分类 API、权限键、筛选参数、分页行为、表格列、排序、启停、固定标签、删除 / 恢复、批量删除或后端契约。
- 不新增批量启停、批量恢复、分类树重排工作台或标签分类合并能力。
- 不调整 `TagForm`、`CategoryForm`、对象侧栏、确认弹窗或错误提示契约。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续按阶段验收或用户明确要求执行，且执行前需等待用户确认前后端已启动。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 继续评估贴纸分组 / 贴纸列表是否按标签 / 分类同口径迁移。
- 若不继续扩展页面范围，先做 Console 表格视觉成组静态收口检查，重点看页头、指标、筛选工具条、批量动作和移动端密度是否一致。
