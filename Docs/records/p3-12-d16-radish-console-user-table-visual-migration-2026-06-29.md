# P3-12-D16 radish.console 用户表格代表页视觉迁移记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D14` 已完成 Console 壳层和语义组件首批，`P3-12-D15` 已将订单页作为商业表格代表页迁入语义组件。本批继续迁移 `UserList`，用于验证对象管理场景下的身份展示、状态筛选、详情入口、查看权限反馈和对象摘要侧栏密度。

## 实施范围

- 将 `UserList` 页头迁入 `ConsolePageHeader`，保留用户管理语义和查看权限状态。
- 将用户指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前结果、本页账号和本页启用三项指标。
- 将用户筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留关键词、用户状态、用户角色、搜索和重置控件。
- 删除 `UserList.css` 中旧页头类名对应的无效移动端样式。

## 边界

- 不修改用户管理 API、权限键、查询参数、分页行为、表格列、详情路由或后端契约。
- 不新增用户批量操作，不改变禁用、锁定等后续治理动作的入口归属。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续按阶段验收或用户明确要求执行，且执行前需等待用户确认前后端已启动。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 继续迁移商品 / 文档等表格 CRUD 页，确认商品详情入口、文档治理入口和对象摘要侧栏在语义组件下的展示密度。
- 权限矩阵、内容治理和经验治理仍应后置到工作台区块边界明确后再推进，避免普通表格工具条承载高风险确认、批量动作或治理动作反馈。
