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

### Console 权限治理第三批日志补齐

- **商城与内容页权限闭环**：`Products / Orders / Tags / Stickers` 已接入页面级访问控制与按钮级权限控制，继续复用 `CurrentUserVo.VoPermissions` 与 Console 权限常量。
- **首屏请求收口**：上述页面在无访问权限时已停止首屏数据请求，避免仅依赖按钮隐藏但仍触发后台接口访问。
- **权限种子扩展**：后端权限常量与 `DbMigrate` 已补齐第三批页面依赖的主链路权限映射，为非默认角色按数据库授权访问提供基础。

### Console 页面级判断继续收口

- **页面级判断下沉完成**：`Applications / Users / Roles / Products / Orders / Tags / Stickers / SystemConfig` 等页面已移除重复的页面级无权限占位返回，统一交由路由入口 `RouteGuard` 处理。
- **页面职责收敛**：页面内部仅保留按钮级 / 操作级权限控制，以及无权限时不触发首屏请求的保护逻辑，避免与路由守卫产生双重判断漂移。


### Console 路由级守卫继续收口

- **路由入口统一鉴权**：`radish.console` 新增路由元数据与 `RouteGuard`，将页面访问控制从页面内部前移到路由入口，阻止用户手输 URL 进入无权限页面。
- **菜单 / 搜索 / 路由同源**：侧边菜单与全局搜索统一复用同一份路由权限元数据，避免“菜单隐藏了但搜索或直链还能进入”的权限裂缝。
- **首页回退补齐**：当当前账号无 `Dashboard` 访问权限时，`/` 将自动回退到首个可访问页面，避免首页直接落入无权限页。
- **边界补齐**：`Dashboard` 与 `Hangfire` 统一走权限守卫；`Profile` 与 `Settings` 明确为“登录即可访问”的路由边界。

### Console Dashboard 权限种子继续补齐

- **仪表盘权限映射补齐**：`ConsolePermissions` 已补充 `StatisticsController.GetDashboardStats -> console.dashboard.view` 映射，避免 `dashboardView` 只停留在前端路由和默认角色权限全集中。
- **种子数据闭环**：`DbMigrate` 已新增 `GetDashboardStats` 的 `ApiModule` 与默认角色授权种子，为非默认角色通过数据库授权获取仪表盘访问能力提供基础。
- **页面边界细化**：`Dashboard` 页面已将“最近订单”和相关快捷入口按现有页面权限分别控制，避免仅有仪表盘权限时继续请求订单接口或暴露越权入口。

### Console Hangfire 权限种子继续补齐

- **Hangfire 资源映射补齐**：`ConsolePermissions` 已补充 `"/hangfire(/.*)?" -> console.hangfire.view` 映射，并与 `DbMigrate` 的 `ApiModule.LinkUrl` 保持一致，便于非默认角色从数据库授权派生 Console 权限快照。
- **Dashboard 访问闭环**：`HangfireAuthorizationFilter` 已改为显式完成认证并按角色权限快照校验 `console.hangfire.view`，不再仅依赖 `System/Admin` 角色硬编码放行。
- **边界保持最小改动**：前端菜单、搜索与路由守卫维持现状，本轮只补后端权限资源与访问校验闭环。

### Console Users 误暴露权限入口收口

- **未实现操作先下线**：`Users` 页已移除创建、状态切换、重置密码、强制下线、删除等占位按钮，避免 `Admin/System` 看到会直接打到不存在后端接口的伪能力入口。
- **详情入口语义纠正**：列表中的“查看详情”现统一按 `console.users.view` 控制，不再误绑到不存在的 `console.users.edit` 能力。
- **无效权限常量清理**：前后端 `ConsolePermissions` / `permissions.ts` 已同步移除未落地的用户操作权限定义，避免默认权限全集继续派生无效能力。

### Console Products / Stickers 权限种子继续补齐

- **Products 辅助资源补齐**：`ConsolePermissions` 与 `DbMigrate` 已补充 `ShopController.GetCategories -> console.products.view`，覆盖商品列表筛选与表单分类加载所依赖的辅助接口，避免非默认角色仅拿到商品页主链路权限后仍因缺分类资源而出现能力断裂。
- **Stickers 辅助资源补齐**：已补充 `StickerController.CheckGroupCode`、`CheckStickerCode`、`NormalizeCode` 的资源映射与默认角色种子，分别归属 `console.stickers.create/edit/batch-upload`，与分组创建、表情编辑、批量上传的真实前端调用保持一致。
- **本轮继续保持最小改动**：仅补真实在用的辅助接口资源，不扩张到 Console 当前未实际调用的 `AdminGetProduct`、`GetCategory` 等链路，避免权限模型先于页面需求过度生长。
