# P3-12-D23 radish.console 角色权限外层语义迁移记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/pages/Roles/RoleList.tsx`、`Frontend/radish.console/src/pages/Roles/RolePermissionPage.tsx` 及对应 CSS。

## 目标

承接 D22 的页面类型判断，将角色管理和角色权限配置按 `P12 Console RBAC Permission Matrix` 进入外层语义迁移。该批只迁移页头、指标和上下文容器，不改变角色、授权、资源树、接口映射或保存契约。

## 代码变更

- `RoleList` 接入 `ConsolePageHeader`、`ConsoleMetricGrid / ConsoleMetricCard`、`ConsoleStatusChip` 和 `ConsoleToolbar`。
- `RoleList` 保留角色列表 API、角色创建 / 编辑 / 删除、启停、权限配置跳转、表格列、分页和权限摘要侧栏不变。
- `RolePermissionPage` 接入 `ConsolePageHeader`、`ConsoleMetricGrid / ConsoleMetricCard` 和 `ConsoleToolbar`，将角色上下文从旧 `admin-feature-card` 迁入语义工具区。
- `RolePermissionPage` 保留资源树递归、父子勾选继承、已选资源集合、权限键 / 接口映射实时预览、已生效快照、保存禁用逻辑和 `saveRoleAuthorization` 载荷不变。
- 移除 `RoleList.css` 中已不再命中的旧 `.admin-feature-header` 移动端残留样式，补充角色上下文卡片宽度约束。

## 停止线

- 不新增角色模型、审批流、高危授权二次确认或审计 API。
- 不改变 `CONSOLE_PERMISSIONS.roles*` 权限键、路由守卫或 Console 侧栏分组。
- 不把应用管理、内容治理、经验治理或 Hangfire 外壳并入本批。
- 不执行真实 Gateway PC / mobile smoke；本批属于代码侧视觉外层迁移，真实页面复核留到成组验收或用户明确要求时执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d23-radish-console-role-permission-visual-migration-2026-06-30.md`
- `git diff --check`

## 下一步

优先评估并推进 `P02 / P03` 内容治理与经验治理工作台外层语义收口；两类页面已有 `governance-workbench` 分区，应先处理页头、指标、状态 chip、硬编码色和 inline 样式，不改变举报审核、手动治理、经验复核、冻结 / 解冻、管理员调整或等级配置 API。
