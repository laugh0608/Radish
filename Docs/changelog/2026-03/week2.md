# 2026-03 第二周 (03-09 至 03-15)

## 2026-03-09 (周一)

### 规划文档拆分收口

- **路线图瘦身完成**：`development-plan.md` 已从长篇混合文档精简为“总览页”，只保留当前里程碑、当前主线、里程碑表与跳转入口。
- **规划分层落地**：新增 `planning/current.md`、`planning/backlog.md`、`planning/archive.md`，分别承接“当前进行中 / 未来规划 / 已完成摘要”，减少单页持续膨胀。
- **文档入口优化**：`Docs/index.md` 与 `Docs/README.md` 已按“高频入口 / 完整目录”职责重新整理，降低新会话和人工阅读时的噪音。

### 开发日志索引瘦身

- **日志分层完成**：`Docs/changelog/index.md` 改为总导航入口，新增 `2025.md` 与 `2026.md` 年度索引页。
- **月度页收口**：`2025-09 ~ 2026-03` 月度日志已统一改为“周志列表 + 简要摘要”，不再在月度页重复展开大段阶段总结。
- **结果**：规划与日志入口均已转为“总览 + 分页索引”结构，后续迭代优先追加到对应分层页，不再把单页继续堆长。

### 当前主线调整

- **主线切换**：M12 当前主线从“体验规范与国际化”调整为 **P1 权限治理能力**，优先推进角色-权限-资源映射、菜单 / 按钮级权限与前后端双重校验闭环。
- **后置项明确**：`i18n`、主题切换与邮件通知系统已明确后置，不作为当前阶段优先任务，避免当前范围扩张过快。
- **并行治理保留**：身份语义 Phase 4 协议输出收敛与 `DbMigrate` 解耦宿主继续作为并行工程治理尾项。

## 2026-03-10 (周二)

### Console 权限治理最小闭环落地

- **当前用户权限快照补齐**：`UserController.GetUserByHttpContext` 已补充 `VoRoles` 与 `VoPermissions`，Console 不再依赖前端默认 `Admin` 回退拼装权限。
- **权限快照收口**：新增 `ConsolePermissions` 常量与角色权限快照拼装逻辑，先以 Console 侧菜单 / 页面 / 按钮权限为最小收口面，并兼容从 `RoleModulePermission + ApiModule.LinkUrl` 推导角色管理权限。
- **角色管理首个闭环**：`radish.console` 已将 `角色管理` 页面接入真实权限判断，覆盖页面访问、左侧菜单可见性以及新增 / 编辑 / 启用禁用 / 删除按钮控制。
- **种子权限补齐**：`DbMigrate` 已为 `RoleController` 主链路补齐 `ApiModule` 与 `RoleModulePermission` 种子，后续重建 / 补种后可让角色管理页面权限从数据库映射稳定产出。

### 验证结果

- ✅ `npm run type-check --workspace=radish.console` 通过。
- ✅ `dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false --no-restore` 通过。
- ✅ `dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj -c Debug -m:1 /nr:false --no-restore` 通过。

### Console 权限治理第二批继续收口

- **Users 页面权限接入**：`用户管理` 与 `用户详情` 已接入页面可见性控制，列表页的查看详情、启用禁用、重置密码、强制下线、删除与新增入口均按权限开关显示。
- **Applications 页面权限接入**：`应用管理` 已接入页面访问控制以及新增、编辑、删除、重置密钥按钮级权限控制。
- **SystemConfig 页面权限接入**：`系统配置` 已接入页面访问、创建、编辑、删除权限控制，并避免无权限时继续请求配置列表。
- **权限种子扩展**：`DbMigrate` 已补充 `UserController`、`ClientController`、`SystemConfigController` 的主链路 `ApiModule` / `RoleModulePermission` 种子，为第二批页面权限快照提供稳定来源。

### 第二批收口验证

- ✅ 第二批继续复用 `CurrentUserVo.VoPermissions` 与 Console 权限常量，不新增前端私有权限模型。
- ✅ `Users / Applications / SystemConfig` 均已接入页面级可见性控制；无权限时不再继续触发首屏数据请求。
- ✅ `DbMigrate` 权限种子已覆盖第二批页面依赖的主要后端接口，后续可通过补种让非默认角色获得相应页面能力。
