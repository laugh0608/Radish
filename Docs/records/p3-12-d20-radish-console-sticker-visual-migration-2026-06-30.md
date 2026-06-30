# P3-12-D20 radish.console 贴纸列表视觉迁移记录

> 日期：2026-06-30（Asia/Shanghai）

## 背景

`P3-12-D14-D19` 已完成 Console 壳层首批、订单 / 用户 / 商品表格代表页、文档治理首屏区块以及标签 / 分类普通列表迁移。D19 后的第一顺位是判断贴纸分组 / 贴纸列表是否仍可按普通 CRUD 外层迁移，还是需要先拆成媒体资产列表与分组详情工作流。

本批确认 `StickerGroupList` 与 `StickerList` 已有权限、上传、排序、表格和表单闭环；图片预览、批量上传、批量排序和分组详情跳转不需要在本轮改变工作流。因此 D20 只迁移页头、指标和筛选工具条，继续复用 D14 语义组件。

## 实施范围

- 将 `StickerGroupList` 页头迁入 `ConsolePageHeader`，保留刷新、新建分组入口和创建权限状态。
- 将 `StickerGroupList` 指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留全部分组、当前结果、启用分组和表情总数四项指标。
- 将 `StickerGroupList` 筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留名称 / Code / 描述关键词筛选。
- 将 `StickerList` 无效分组状态迁入 `ConsolePageHeader`，保留返回分组列表动作。
- 将 `StickerList` 正常页头迁入 `ConsolePageHeader`，保留返回分组列表、刷新、新增表情和批量上传入口。
- 将 `StickerList` 指标迁入 `ConsoleMetricGrid / ConsoleMetricCard`，保留全部表情、当前结果、启用表情和排序草稿四项指标。
- 将 `StickerList` 筛选区迁入 `ConsoleToolbar / ConsoleStatusChip`，保留名称 / Code 关键词筛选和保存排序动作。
- 删除贴纸页迁移后不再使用的旧页头动作样式和旧移动端页头样式。

## 边界

- 不修改贴纸 API、权限键、路由、筛选参数、表格列、图片预览、表单字段、上传流程、批量上传 Modal、批量排序请求、删除确认或后端契约。
- 不把贴纸页拆成新的媒体资产工作台；分组详情、上传预览、批量素材确认和素材状态流保持既有页面结构。
- 不调整 `StickerGroupForm`、`StickerForm`、`StickerBatchUploadModal`、对象摘要侧栏、错误提示或确认弹窗契约。
- 本批未执行 Gateway PC / mobile 真实页面 smoke；按当前节奏，真实页面 smoke 留到阶段验收、用户明确要求或用户确认前后端已启动后集中执行。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 做 Console 表格视觉成组静态收口检查，覆盖 D14-D20 的侧栏分组、页头、指标、筛选工具条、批量动作、权限反馈、表格密度和移动 CSS 视图可用性。
- 角色权限、内容治理、经验治理和运维任务页仍按页面类型独立推进，不把复杂治理动作压进普通表格工具条。
