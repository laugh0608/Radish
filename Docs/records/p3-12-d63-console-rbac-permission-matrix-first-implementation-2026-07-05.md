# P3-12-D63 Console 权限矩阵页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.console` `/console/roles/:roleId/permissions`
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen`

## 背景

`P3-12-D63 Console` 已完成治理工作台、商业运营、文档治理和用户管理表格 CRUD 首批实现。按当前规划，本批继续处理 `P12 - Console RBAC Permission Matrix`，目标是在现有角色 / 权限 API 内补齐授权快照、权限预览、保存反馈和权限证据上下文。

本批通过 Pencil MCP 只读导出 `P12`。设计源强调权限矩阵应围绕角色快照、资源矩阵、授权预览、旧快照拦截和授权留痕组织，而不是只展示资源树和保存按钮。

## 实现范围

- `/console/roles/:roleId/permissions` 保留现有资源树读取、角色授权快照读取、权限预览读取和保存授权 API。
- 新增角色权限矩阵任务流，串联角色快照、资源矩阵、授权预览和保存反馈。
- 指标区将保存状态替换为授权快照状态，明确当前变更数量与快照时间。
- 资源树面板改为“权限矩阵”，补资源节点、叶子资源和当前授权数量摘要。
- 页面外层改为主矩阵 / 预览区 + 授权证据 rail。
- 授权证据 rail 展示角色、快照时间、变更资源数量、接口映射变化、保存状态、变更线索和敏感权限线索。
- 保存继续通过 `buildRoleAuthorizationSavePayload` 携带 `expectedModifyTime`，让后端旧快照拦截契约保持为唯一写入防线。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不改变资源种子、接口映射、权限继承、角色列表、角色启停或角色删除逻辑。
- 不新增审批流、授权审计写入、权限版本号字段、独立移动 Console、内部 Jobs 平台或新的治理 API。
- 不把 `/console/roles` 角色 CRUD 与 `/console/roles/:roleId/permissions` 保存协议合并成新的复合写入。

## 验证

- `npm run build --workspace=radish.console`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- `P3-12-D63 Console` 当前发布前页面族首批实现已覆盖治理、商业、文档、用户管理和权限矩阵。
- 下一步建议进入 D63 成组静态收口与后置缺口整理，确认规划记录、页面宽度、权限矩阵保存反馈和 D63 后置产品 / API 缺口是否一致；仍不直接进入 `P3-12-E`。
