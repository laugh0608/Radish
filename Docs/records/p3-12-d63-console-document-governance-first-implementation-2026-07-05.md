# P3-12-D63 Console 文档治理页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.console` 文档治理
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen`

## 背景

`P3-12-D63 Console` 已完成治理工作台和商业运营页面族首批实现。按 `Docs/planning/current.md` 的下一顺位，本批继续处理 Console 文档治理页面族。

本批通过 Pencil MCP 只读抽取 `console-governance-workbench.pen` 的 `P11 - Console Document Governance - Publishing & Access`，并同时确认 `P12 - Console RBAC Permission Matrix` 属于后续权限矩阵批次。P11 强调文档治理应围绕发布状态、访问策略、版本回看和回收站语义形成治理板，而不是只停留在表格和弹窗。

## 实现范围

- `/console/documents` 在既有筛选、表格、详情、访问策略、版本治理、导入导出和回收站动作上补文档治理任务流。
- 任务流按列表定位、发布状态、访问策略和版本回看组织管理员扫描顺序。
- 右侧上下文从普通治理区块升级为文档治理详情 rail，展示当前查询范围、发布治理权限、访问策略权限、版本治理权限和回收站状态。
- 当前文档证据区优先使用已打开详情、访问策略弹窗、版本治理弹窗或当前页需要治理的文档，展示状态、可见性、来源、摘要、访问策略、版本和来源路径。
- rail 动作继续复用现有详情、版本、访问策略、导出、发布和下架函数，不新增接口或提交字段。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不改变文档列表查询、分页、筛选、详情读取、导入导出、访问策略保存、版本列表 / 详情读取、回滚、发布 / 下架 / 归档 / 删除 / 恢复等既有行为。
- 不把作者态 Markdown 编辑器、发布审批、文档权限治理 API、全文搜索治理、导入导出队列或 RBAC 权限矩阵并入本批。
- 不启动独立移动 Console；P11 仅作为响应式后台任务流参考。
- 不启动 Gateway PC / mobile 真实页面 smoke；本轮未由用户明确确认前后端已启动。

## 验证

- `npm run build --workspace=radish.console`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批真实 Gateway PC / mobile smoke 未执行。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- 继续 `P3-12-D63 Console`，下一批建议进入表格 CRUD / 权限矩阵页面族，对照 `P05 / P12` 处理选中对象摘要、审计 / 权限上下文、详情抽屉、权限预览、保存反馈和授权快照语义。
- D63 完成前不进入 `P3-12-E`，不创建发布 tag，不恢复发布流程。
