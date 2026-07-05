# P3-12-D63 Console 用户管理表格 CRUD 首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.console` `/console/users`
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen`

## 背景

`P3-12-D63 Console` 已完成内容治理 / 经验治理工作台、商业运营页面族和文档治理页面族首批实现。按 `Docs/planning/current.md` 的下一顺位，本批继续处理 Console 表格 CRUD 页面族，并选择边界清楚的 `P05 - Console Table CRUD - User Management` 作为首批落点。

本批通过 Pencil MCP 只读抽取 `console-governance-workbench.pen` 的 `P05`。设计源强调用户管理不是只展示基础表格，而应形成对象管理任务面：查询范围、状态队列、选中对象摘要、权限与风险线索，以及到用户详情和审计上下文的清晰边界。

## 实现范围

- `/console/users` 保留现有 `userManagementApi.getUserList`、权限守卫、关键词 / 状态 / 角色筛选、分页和用户详情路由。
- 指标区补当前页禁用账号统计，用于快速识别需要关注的账号对象。
- 新增用户对象管理任务流，按查询范围、状态检查、对象详情、审计边界组织当前页信息。
- 表格补公开索引、更新时间和统一状态展示，状态文本与颜色复用既有 `getUserStatusDisplay` / `getUserStatusColor`。
- 右侧摘要 rail 改为选中对象上下文，展示当前筛选、分页、状态队列、可执行动作、首个需关注用户或当前页首个用户。
- rail 提供现有详情路由入口和“设为筛选”动作；不新增状态写入、审计写入或高风险账号治理动作。
- 修正搜索、重置和分页重新加载时对旧 React state 的依赖，改为向加载函数传入明确目标页、页大小与筛选条件。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不新增账号冻结 / 解冻、风险处置、审计写入、资产调整或经验治理入口。
- 不改变用户详情页、用户管理 API、状态筛选协议、角色筛选协议或分页契约。
- 不实现独立移动 Console、内部 Jobs 平台或新的治理 API；这些继续作为 D63 内后置产品 / API 缺口记录。

## 验证

- `npm run build --workspace=radish.console`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- 继续 `P3-12-D63 Console`，下一批建议进入 `P12 - Console RBAC Permission Matrix`。
- 权限矩阵批次应继续使用现有角色 / 权限 API、现有权限键和现有路由语义，重点处理授权快照、权限预览、保存反馈、继承边界和权限证据 rail。
