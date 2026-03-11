# Console 权限覆盖矩阵

> 最后更新：2026-03-11
> 适用范围：`radish.console` 当前已接入权限治理的页面与其真实依赖的后端资源

本文档用于把 Console 权限治理涉及的四层对象放到同一张表里：

- 路由元数据
- 前端权限常量与页面内 `usePermission`
- 后端 `ConsolePermissions` 资源映射
- `DbMigrate` 种子化 `ApiModule / RoleModulePermission`

## 1. 判定规则

### 1.1 视为“已覆盖”

满足以下条件之一：

1. 页面访问路由具备明确的 `requiredPermission`，且该权限存在于前后端常量中
2. 页面真实调用的后台接口已进入 `ConsolePermissions` 映射，并具备 `DbMigrate` 种子
3. 路由本身是 `authOnly`，明确约定只要求登录态，不进入 Console 专属资源模型

### 1.2 不纳入本表的内容

- 当前页面未真实调用的候选接口
- 尚未落地的伪能力
- 未来可能存在但当前未接入的后台模块

## 2. 路由与页面覆盖矩阵

| 模块/页面 | 路由 | 路由访问权限 | 页面内操作权限 | 真实后端资源 | 种子状态 | 备注 |
| --- | --- | --- | --- | --- | --- | --- |
| Dashboard | `/` | `console.dashboard.view` | `console.orders.view`、`console.products.create`、`console.users.view`、`console.applications.view` | `Statistics/GetDashboardStats`、`Shop/AdminGetOrders` | ✅ | 最近订单按订单查看权限单独控制 |
| Applications | `/applications` | `console.applications.view` | `console.applications.create/edit/delete/reset-secret` | `Client/GetClients`、`GetClient/.+`、`CreateClient`、`UpdateClient/.+`、`DeleteClient/.+`、`ResetClientSecret/.+` | ✅ | 列表与弹窗链路均已闭环 |
| Products | `/products` | `console.products.view` | `console.products.create/edit/delete/toggle-sale` | `Shop/GetCategories`、`AdminGetProducts`、`CreateProduct`、`UpdateProduct`、`DeleteProduct/.+`、`PutOnSale/.+`、`TakeOffSale/.+` | ✅ | `AdminGetProduct` 当前页面未使用 |
| Orders | `/orders` | `console.orders.view` | `console.orders.retry` | `Shop/AdminGetOrders`、`RetryGrantBenefit/.+` | ✅ | 详情当前复用列表数据，不依赖额外详情接口 |
| Users | `/users` | `console.users.view` | 无额外操作权限 | `User/GetUserList`、`GetUserById/\\d+` | ✅ | 未落地操作已收口，不再保留伪权限 |
| User Detail | `/users/:userId` | `console.users.view` | 无额外操作权限 | 当前页面仍以 mock 为主，无新增真实资源依赖 | ✅ | 路由边界已稳定，后续若接真接口需重新补矩阵 |
| Roles | `/roles` | `console.roles.view` | `console.roles.create/edit/toggle/delete` | `Role/GetRoleList`、`GetRoleById`、`CreateRole`、`UpdateRole`、`DeleteRole`、`ToggleRoleStatus` | ✅ | 首批闭环模块 |
| Tags | `/tags` | `console.tags.view` | `console.tags.create/edit/delete/restore/toggle/sort` | `Tag/GetPage`、`Create`、`Update/.+`、`Delete/.+`、`Restore/.+`、`ToggleStatus/.+`、`UpdateSort/.+` | ✅ | 页面与资源映射已一致 |
| Stickers Groups | `/stickers` | `console.stickers.view` | `console.stickers.create/edit/delete/toggle` | `Sticker/GetAdminGroups`、`CreateGroup`、`UpdateGroup/.+`、`DeleteGroup/.+`、`CheckGroupCode` | ✅ | 分组启停复用 `UpdateGroup` |
| Stickers Items | `/stickers/:groupId/items` | `console.stickers.view` | `console.stickers.create/edit/delete/sort/batch-upload` | `Sticker/GetGroupStickers/.+`、`AddSticker`、`UpdateSticker/.+`、`DeleteSticker/.+`、`BatchAddStickers`、`BatchUpdateSort`、`CheckStickerCode`、`NormalizeCode` | ✅ | 上传文件仍走共享接口，但已按 `businessType` 对 Sticker 链路收口，见第 4 节 |
| SystemConfig | `/system-config` | `console.system-config.view` | `console.system-config.create/edit/delete` | `SystemConfig/GetSystemConfigs`、`GetConfigCategories`、`GetConfigById`、`CreateConfig`、`UpdateConfig`、`DeleteConfig` | ✅ | 编辑详情链路已覆盖 |
| Hangfire | `/hangfire` | `console.hangfire.view` | 无 | `/hangfire(/.*)?` | ✅ | 特殊入口，走 `HangfireAuthorizationFilter` |

## 3. `authOnly` 路由矩阵

以下路由当前明确只要求“登录即可访问”，不进入 Console 专属权限资源模型：

| 路由 | 类型 | 真实依赖 | 是否进入 ConsolePermissions | 备注 |
| --- | --- | --- | --- | --- |
| `/profile` | `authOnly` | `Attachment/UploadImage`、`User/SetMyAvatar` | 否 | 个人资料页，当前不按 `console.*` 控制 |
| `/settings` | `authOnly` | 当前主要为本地设置/占位 | 否 | 仍属登录态页面 |
| `/theme-test` | `authOnly` | 无后台依赖 | 否 | 调试/展示页，不纳入治理主线 |

## 4. 共享接口边界矩阵

### 4.1 已落地边界项：`Attachment/UploadImage`

| 接口 | 当前调用方 | 当前鉴权方式 | 是否已进入映射/种子 | 当前状态 |
| --- | --- | --- | --- | --- |
| `/api/v1/Attachment/UploadImage` | `StickerForm`、`StickerGroupForm`、`StickerBatchUploadModal`、`UserProfile` | `[Authorize]` 登录态；其中 `Sticker/StickerCover` 额外按 `businessType` 复用 `console.stickers.*` 权限校验 | 否 | 已按方案 B 最小落地 |

### 4.2 当前判断

- 该接口属于“共享上传能力”，并非纯粹的 Console 专属业务资源
- 它确实影响 Sticker 后台的完整操作链路，但也被 `UserProfile` 与用户侧上传复用
- 因此它仍不适合直接并入现有 `ConsolePermissions + DbMigrate` 的共享 URL 映射
- 当前已采用方案 B：只对 `Sticker` / `StickerCover` 业务类型做后端最小收口，其他业务类型继续维持登录态能力

### 4.3 当前处理策略

1. 共享 URL 仍不进入 `ConsolePermissions + DbMigrate` 映射，避免误伤 `Avatar` 与 `General`
2. `Sticker` 上传复用既有 `console.stickers.create/edit/batch-upload`
3. `StickerCover` 复用既有 `console.stickers.create/edit`
4. 若未来需要平台化上传权限，再作为下一阶段独立议题处理

## 5. 当前结论

### 5.1 已完成

- 已接入权限治理的 Console 主页面，路由访问权限已全部具备来源
- 页面真实调用的 Console 专属后台接口当前已基本完成 `ConsolePermissions + DbMigrate` 对齐
- `Users` 未落地能力已从页面、前后端权限常量与文档口径中一并清理

### 5.2 工具化校验已落地

当前已补充轻量扫描脚本：`npm run check:console-permissions`

该脚本会自动对比：

- `routeMeta.requiredPermission`
- `CONSOLE_PERMISSIONS`
- 页面内 `usePermission(CONSOLE_PERMISSIONS.xxx)`
- `ConsolePermissions.ApiPermissionMappings`
- `InitialDataSeeder.Identity.cs` 中的 `ApiModule.LinkUrl`

建议在以下场景运行：

- 新增 Console 页面或路由权限时
- 新增按钮级权限或页面真实接口依赖时
- 调整 `ConsolePermissions` 或 `DbMigrate` 种子时

### 5.3 暂不视为缺口的项

- `AdminGetProduct`、`AdminGetOrder`：当前页面未实际调用
- `GetOrderTrend`、`GetProductSalesRanking`、`GetUserLevelDistribution`：当前页面未实际接入
- `GetUserStats`：用户详情页仍以 mock/TODO 为主

## 6. 使用方式

建议把本文档作为 Console 权限治理 V1 的“覆盖核对表”：

- 权限链路改动后先运行 `npm run check:console-permissions`
- 新增一个页面时，先补路由行
- 页面新增真实接口依赖时，补“真实后端资源”列
- 只在接口真正被页面使用后，才推进 `ConsolePermissions` 与 `DbMigrate` 对齐
- 若某个接口属于共享能力，先落到第 4 节的“共享接口边界矩阵”，不要直接强行塞进 `console.*`
