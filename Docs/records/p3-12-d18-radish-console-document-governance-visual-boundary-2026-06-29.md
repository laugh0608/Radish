# P3-12-D18 radish.console 文档治理页区块边界与首批语义迁移记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D14-D17` 已完成 Console 壳层首批、系统设置、订单、用户和商品表格代表页的语义组件迁移。`DocumentGovernancePage` 当前承载文档发布、访问策略、版本回滚、导入导出和回收站治理，不能直接按普通表格 CRUD 页处理。

本批目标是先固定文档治理页的首屏区块边界，并完成页头、指标和筛选区的首批语义迁移；详情证据、访问策略和版本治理仍保持既有 Modal 与动作契约。

## 实施范围

- 将 `DocumentGovernancePage` 页头迁入 `ConsolePageHeader`，保留导入 Markdown、刷新和查看权限状态。
- 将文档治理指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留当前页文档、已发布、受限和内置四项指标。
- 将文档筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留关键词、状态、可见性、来源和回收站范围筛选。
- 在首屏右侧增加治理区块摘要，明确列表定位、详情证据、访问策略和版本回滚四类承载边界。

## 边界

- 不修改文档治理 API、权限键、筛选参数、分页行为、表格列、发布 / 下架 / 归档、访问策略、导入导出、删除 / 恢复、版本列表或回滚契约。
- 不新增批量发布、批量删除、审批流或新的文档治理接口。
- 不把详情证据、访问策略和版本治理从既有 Modal 改造成新工作台；后续如继续推进，应作为独立结构批次处理。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续按阶段验收或用户明确要求执行，且执行前需等待用户确认前后端已启动。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 继续评估文档治理详情证据、访问策略和版本治理是否需要从 Modal 拆为稳定工作台区块。
- 如果本阶段只做 Console 表格视觉收口，可优先迁移标签、分类、贴纸等低风险普通列表页，再做成组静态收口检查。
