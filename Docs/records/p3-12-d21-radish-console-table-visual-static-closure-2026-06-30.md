# P3-12-D21 radish.console 表格视觉成组静态收口记录

> 日期：2026-06-30（Asia/Shanghai）

## 背景

`P3-12-D14-D20` 已完成 Console 公共壳层首批、系统设置、订单、用户、商品、文档治理首屏、标签 / 分类和贴纸类页面的语义组件迁移。D21 按当前计划对这些页面做成组静态收口检查，确认普通表格页和代表治理页在页头、指标、筛选工具条、批量动作、权限反馈、表格密度和移动 CSS 视图口径上的一致性。

本批不扩展新的 Console 页面范围，不把角色权限、内容治理、经验治理或运维任务页压进普通表格结构。

## 检查范围

- `SystemConfigList`
- `OrderList`
- `UserList`
- `ProductList`
- `DocumentGovernancePage` 首屏列表区块
- `TagList`
- `CategoryList`
- `StickerGroupList`
- `StickerList`

## 结论

- 上述页面均已使用 `ConsolePageHeader` 承载页头、领域标识、权限 / 状态反馈和主动作。
- 上述页面均已使用 `ConsoleMetricGrid / ConsoleMetricCard` 承载页面指标。
- 上述页面均已使用 `ConsoleToolbar / ConsoleStatusChip` 承载筛选区和筛选条件状态。
- 目标页面继续保持“语义页头 + 指标网格 + 工具条 + 表格 + 摘要侧栏”的扫描顺序；`DocumentGovernancePage` 因承载发布、访问策略和版本治理，仍按 D18 口径只收口首屏列表与治理摘要，深层动作保留既有 Modal。
- 批量上传、保存排序、刷新、新增、返回、导入、详情抽屉、备注、删除 / 恢复等动作保持原业务契约，不在本批重排工作流。

## 修正

- 移除 `SystemConfigList.css` 中已不再命中的旧 `admin-feature-header` 移动端样式。
- 将 `StickerGroupList.css` 的分组封面占位背景、边框和文字色从硬编码颜色改为 Console 语义 token，与 `StickerList` 的表情预览样式口径一致。

## 边界

- 不修改 API、权限键、路由、URL 查询参数、分页、表格列、上传流程、排序保存、详情抽屉、Modal 或后端契约。
- 不执行 Gateway PC / mobile 真实页面 smoke；该项继续留到阶段验收、用户明确要求或用户确认前后端已启动后集中执行。
- 不把本批结论扩展为完整 Console 全站视觉收口；复杂治理页仍按页面类型独立推进。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一步

- 若继续推进 Console 视觉实现，下一顺位应评估角色权限、内容治理、经验治理和运维任务页的页面类型边界，先确定是权限矩阵、治理工作台还是运维任务流，再进入代码。
- 若准备阶段验收，再按规则等待前后端已启动后执行 Gateway PC / mobile smoke。
