# P3-12-D14 radish.console 视觉代码实现首批记录

> 日期：2026-06-29（Asia/Shanghai）

## 背景

`P3-12-D5` 已完成 Console 治理工作台设计源，`P3-12-D6` 已完成 Console 视觉代码实现前盘点。`radish.client` 私域 / 作者态第二批视觉实现与 D9-D13 成组 Gateway PC / mobile smoke 已收口后，当前顺位进入 `radish.console` 视觉代码实现。

本批目标是先把 Console 壳层分组、页面语义组件和一个低风险代表页接入代码，确认实现形态可以承接后续订单、用户、治理等高频页面迁移。

## 实施范围

- 扩展 Console 路由元数据，补充分组、侧栏 icon、侧栏排序和可选 badge 语义。
- 调整 `AdminLayout` 侧栏渲染，按总览 / 商业与资产 / 内容与文档 / 治理与权限 / 系统工具分组展示；保留既有权限过滤和路由跳转逻辑。
- 新增 `ConsolePageHeader`、`ConsoleStatusChip`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleToolbar`，作为 Console 页面首批语义组件。
- 将 `SystemConfigList` 的页头、指标区和筛选工具条迁入语义组件，作为低风险代表页。

## 边界

- 不修改系统设置 API、权限键、表单字段、上传行为、编辑动作、恢复默认动作或后端契约。
- 不引入新的 UI 依赖，不把首批组件抽入 `@radish/ui`。
- 不调整 Console 登录、认证、角色授权和菜单可见性判断。
- 不在本批迁移全部 Console 页面；后续按代表页逐步验证组件复用边界。

## 验证

- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批未执行 Gateway PC / mobile 真实页面 smoke。按当前开发节奏，真实 smoke 放在阶段验收或用户明确要求时执行，且必须先提醒用户启动前后端并等待确认。

## 下一步

- 继续把语义组件迁移到订单 / 用户等高频表格代表页。
- 迁移治理页前，先确认复杂筛选、批量操作、风险提示和权限反馈是否需要扩展 `ConsoleToolbar` 或新增更明确的页面区块组件。
- 后续成组页面准备验收时，再补 Console Gateway PC / mobile smoke 与记录。
