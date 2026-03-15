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


### Console 权限治理文档口径统一

- **专题文档补齐**：新增 `Console 权限治理 V1` 专项文档，集中维护当前已完成范围、待决策项、收口清单与退出条件，避免权限治理继续分散在规划、日志与零散讨论中。
- **架构与模块文档对齐**：`console-system / core-concepts / architecture / modules / roadmap` 已同步更新为当前真实状态，明确 `RouteGuard + usePermission + VoPermissions + ApiModule.LinkUrl + DbMigrate` 的完整链路。
- **规划与 README 收口**：`development-plan`、`planning/current` 与 `radish.console/README` 已移除明显过期的“Users / Roles 待实现”“自定义 fetch 封装”等旧描述，改为 Console 权限治理 V1 收口清单。


### Console 权限覆盖矩阵补齐

- **覆盖矩阵落地**：新增 `Console 权限覆盖矩阵` 文档，统一核对路由元数据、前端权限常量、后端 `ConsolePermissions` 映射与 `DbMigrate` 种子，减少继续靠人工逐页巡检的成本。
- **剩余缺口收敛**：矩阵确认当前 Console 专属资源基本已闭环，剩余主要待决策项已收敛为共享上传接口 `Attachment/UploadImage` 的边界问题。

### Attachment 上传边界按方案 B 收口

- **共享接口边界已决策**：`Attachment/UploadImage` 不直接并入 `ConsolePermissions + DbMigrate` 的共享 URL 映射，避免影响 `Avatar` 与用户侧 `General` 上传。
- **Sticker 链路最小落地**：后端已仅对 `Sticker` / `StickerCover` 的 `businessType` 增加权限收口，分别复用 `console.stickers.create/edit/batch-upload` 与 `console.stickers.create/edit`。
- **文档与规划同步**：`README`、权限治理专题、覆盖矩阵、路线图、当前规划与 Sticker 设计文档已统一更新，V1 当前只剩工具化校验类尾项。
- **回归验证通过**：`dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter AttachmentControllerTest` 与 `dotnet build Radish.slnx -c Debug` 已通过。

## 2026-03-11 (周三)

### 规划优先级调整

- **权限主线切换**：`Console` 权限治理 V1 已从“继续补工具 / 补边界”切换为 **P0：权限冻结后的短回归观察期**，后续只做回归维护，不再横向扩张权限模型。
- **下一执行顺序确认**：规划已明确调整为 `P1 聊天室完善` → `P2 开源软件清单声明组件` → `P3 抽奖 / 投票 / 问答里先选一个做最小首版`。
- **大项统一后置**：国风 UI、主题切换、i18n 与邮件通知系统统一归类为 `Later`，避免当前阶段再次引入影响面过广的大治理任务。

### 聊天室 P1 文档与范围同步

- **路线图状态补记**：聊天室路线图已从 `2026-03-03` 旧快照更新到 `2026-03-11`，补记这几轮已经落地的 `引用回复`、`@mention`、图片消息、草稿恢复、成员面板、重连补拉与状态条、成员头像来源。
- **P1 剩余缺口收敛**：当前已将聊天室 P1 的最后一个核心项明确收敛为 `乐观发送 + 失败重试`，优先保障“图片发送成功率和重试体验可接受”“常用交互流畅且状态一致”两个验收点。
- **下一顺位保持不变**：聊天室 P1 收口后，下一主线仍为 `P2 开源软件清单声明组件`，避免在聊天室主链路尚未完全稳定前继续插入新功能。

### 聊天室乐观发送与失败重试落地

- **前端状态链路补齐**：`chatStore` 已支持 `sending / failed / sent` 三态消息，发送时先插入负数临时消息，失败后原位保留并展示 `重试 / 撤销` 操作。
- **REST / Hub 关联键落地**：前后端发送契约新增 `clientRequestId`，用于将 REST 返回和 `MessageReceived` Hub 推送稳定映射到同一条临时消息，避免重连或竞态时出现重复气泡。
- **图片重试保持克制**：图片消息发送失败后可直接复用已上传的 `attachmentId + imageUrl + imageThumbnailUrl` 重试，不重复上传附件。
- **验证结果**：`npm run type-check --workspace=radish.client` 通过；`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false --no-restore` 通过。

### Console 权限治理工具化校验落地

- **扫描脚本补齐**：新增 `Scripts/check-console-permissions.mjs`，自动对比 `routeMeta.requiredPermission`、前端 `CONSOLE_PERMISSIONS`、页面内 `usePermission`、后端 `ConsolePermissions.ApiPermissionMappings` 与 `DbMigrate` 的 `ApiModule.LinkUrl`，减少继续靠人工逐页核对的成本。
- **入口统一**：根目录新增 `npm run check:console-permissions`，`radish.console/README` 与权限治理专题、覆盖矩阵文档已同步补充使用方式。
- **阶段口径更新**：`Console 权限治理 V1` 的下一步建议已从“补工具”切换为“冻结边界、只做回归维护”，避免在收口尾声继续横向扩张权限模型。

### DbMigrate 启动入口继续收口

- **项目依赖解耦**：`Radish.DbMigrate` 已移除对 `Radish.Api` 的直接项目引用，改为显式依赖 `Radish.Extension`，避免迁移工具继续绑定具体宿主。
- **Bootstrap 收口**：新增 `DbMigrateBootstrap`，统一承接配置加载顺序、选项注册、`SqlSugar` 注册与经验值计算器注册，`Program.cs` 收缩为薄入口。
- **验证结果**：`dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj -c Debug -m:1 /nr:false --no-restore` 通过，未引入新的构建错误。

### DbMigrate 增加只读校验命令

- **命令补齐**：新增 `doctor` / `verify` 子命令，只读检查当前环境名、`MainDb`、已启用连接、关键 `ConnId` 完整性以及 `seed` 依赖核心表状态。
- **边界保持克制**：`doctor` 不执行 `init`、不执行 `seed`，仅输出“可直接执行 / 建议先 init / 需要先补配置”的结论，避免在工程治理阶段引入额外副作用。

### 聊天室 P1 回归问题收口

- **长整型 ID 精度问题修复**：聊天室前端已改为保留服务端字符串 ID，仅在乐观发送阶段使用负数本地临时 ID，撤回、引用回复与历史分页不再因为 Snowflake 精度丢失而误报“消息不存在 / 引用消息不存在”。
- **输入区布局修复**：聊天输入区已改成“左侧输入框 + 右侧固定按钮列”布局，`textarea` 不再挤压“图片 / 发送”按钮。
- **文档与规划同步**：聊天前端、实时设计、路线图、总览、系统设计、总路线图、当前规划与 2026-03 月志已同步更新到“聊天室 P1 基本收口”的最新状态。
- **验证结果**：聊天室问题已完成实际回归确认；`npm exec vite build --workspace=radish.client` 通过。完整 `npm run build --workspace=radish.client` 仍被既有无关类型错误阻塞：`Frontend/radish.client/src/apps/forum/components/PostDetail.tsx` 访问了不存在的 `voAuthorAvatarUrl` 字段。

## 2026-03-12 (周四)

### 开源软件清单声明组件最小首版落地

- **欢迎 App 新增入口**：`radish.client` 的 `WelcomeApp` 已新增“开源软件”标签页，作为当前阶段的产品内说明入口，不单独增加新应用。
- **静态清单首版完成**：首版以项目级核心依赖为范围，展示 `ASP.NET Core`、`React`、`TypeScript`、`Vite`、`Zustand`、`SignalR`、`PostgreSQL`、`Iconify` 等项目的用途、许可证与官方仓库链接。
- **范围保持克制**：当前不引入构建时自动扫描，也不在本轮直接扩展为完整 `THIRD-PARTY-NOTICES` 生成流程，避免把中小体量补齐项升级为工程治理任务。
- **文档口径同步**：`Docs/frontend/webos-quick-start.md` 已补充欢迎应用中的“开源软件说明入口”，规划文档已将 `P2` 更新为“最小首版已落地，后续如需继续推进则转为 `P2-ext` 扩展项”。
- **验证结果**：用户本地已执行 `npm install` 并确认相关前端检查通过；当前改动已提交为 `feat(client): 在欢迎应用中添加开源软件说明`。

### 论坛投票 MVP 启动并完成后端前两步

- **方向确认**：`P3` 已正式选择“论坛帖子附带投票”作为最小首版，不拆独立投票 App，继续复用论坛发帖、详情与权限链路。
- **文档先行完成**：已补齐 `Docs/features/forum-poll-mvp.md`，明确 MVP 范围、接口切分、前后端边界与最小测试清单，作为当前实现口径。
- **后端模型与契约打底完成**：新增 `PostPoll`、`PostPollOption`、`PostPollVote` 三个实体，以及 `CreatePollDto / PollOptionDto / VotePollDto / PostPollVo` 等 DTO / Vo，帖子视图模型也已补充最小投票摘要字段。
- **发帖链路接入完成**：`PostService.PublishPostAsync` 已支持在发布帖子时一并创建投票，帖子详情和列表查询也已补充投票摘要 / 详情回传，便于后续前端直接接入。
- **构建验证通过**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false` 已通过；剩余 warning 为既有问题，未由本轮引入。
- **下一步明确**：后续继续推进 `PollController`、投票提交 / 查询接口，以及欢迎 App 中论坛发帖与详情页的投票交互接入。

## 2026-03-13 (周五)

### 论坛投票 MVP 第 3、4 步继续推进

- **投票接口已落地**：新增 `PollController` 与 `PostPollService`，已补齐“按帖子查询投票详情”“登录用户提交投票”两条最小接口链路。
- **最小回归测试已补**：新增投票服务测试，覆盖首次投票成功、重复投票拦截、截止态拦截与非法选项拦截，后端新链路已完成最小回归验证。
- **论坛详情页已接入投票交互**：帖子详情页现可展示投票问题、票数、百分比、截止状态，并支持登录用户提交投票后原位刷新结果。
- **论坛发帖弹窗已接入附带投票 UI**：欢迎 App 中发帖弹窗已新增“附带投票”开关、问题、选项与截止时间输入区，前端可直接发送带投票的发帖请求。
- **列表识别已补轻量入口**：帖子卡片已增加投票帖标识与票数摘要，维持列表页轻展示、详情页完整交互的结构。
- **前端构建恢复通过**：`radish.client` 构建已通过，先前 `PostDetail.tsx` 缺少 `voAuthorAvatarUrl` 类型定义的问题已一并收口。

### 论坛列表投票摘要查询完成收口

- **列表查询改为批量回填**：`PostController.GetList` 不再逐条调用 `GetPostDetailAsync`，改由 `PostService.FillPostListMetadataAsync` 按页批量补齐分类名、标签与投票轻量摘要。
- **`N+1` 风险已消除**：帖子列表投票摘要不再沿用“逐条补详情”路径，列表接口重新回到“轻量摘要、详情完整”的设计边界。
- **最小回归已补**：新增 `PostControllerTest`，覆盖列表接口使用批量回填且不再逐条查询帖子详情的行为。
- **文档状态已同步**：`development-plan`、`planning/current` 与 `forum-poll-mvp` 已更新为“列表查询优化完成，下一步转入补测试与 MVP 验收”。

### 论坛投票 MVP 第 5 步继续补回归

- **发帖附带投票回归补齐**：新增 `PostServiceTest` 用例，覆盖带投票发帖时标签关系、投票主体与投票选项的最小创建链路。
- **列表 / 详情契约继续加固**：`PostControllerTest` 新增详情用例，明确列表保持轻量摘要，详情仍返回完整 `VoPoll`。
- **发布失败路径已补**：`PostController.Publish` 已新增无效投票参数返回 `400` 的回归，避免控制器吞掉服务层参数校验。
- **人工验收脚本已补**：`Radish.Api.Forum.http` 新增论坛投票验收顺序与请求样例，覆盖“发帖附带投票、列表识别、详情查看、提交投票、重复投票拦截、截止态拦截”。

## 2026-03-14 (周六)

### 论坛投票 MVP 完成验收收口

- **短时截止时间问题已修复**：投票创建与投票状态判断已统一改为 UTC 比较，修复“截止时间设为几分钟后时被误判为已过期”的问题。
- **回归测试已补齐**：新增“几分钟后截止可正常创建”的最小回归，并同步将投票相关服务测试的时间口径统一为 UTC。
- **投票链路验收通过**：手工联调已确认普通发帖、附带投票发帖、列表识别、详情展示、登录投票、重复投票拦截、未登录只读与截止态拦截整体正常。
- **阶段结论更新**：论坛投票 MVP 已达到“可演示、可联调、可回归”的收口标准，后续如继续推进则进入 `P3-ext` 扩展阶段。

### 论坛问答 MVP 完成联调收口

- **后端最小闭环已具备**：问答帖发布、回答提交、提问者采纳，以及 SQLite / `DbMigrate` 初始化修复均已落地。
- **前端发布入口已补齐**：论坛发帖弹窗已支持“作为问答发布”，并与投票保持互斥，不把两种帖子类型叠加到同一首版中。
- **列表与详情展示已补齐**：帖子卡片已显示“问答 / 已解决 / 回答数”摘要；帖子详情已补充回答区、最佳答案高亮与作者资料入口。
- **回答体验已统一**：回答输入已切换为复用现有 `MarkdownEditor`，支持图片、文档与贴图；评论区对外文案已统一收敛为“讨论区”，降低与回答区的割裂感。
- **联调与反馈收口**：手工联调已确认回答提交、提问者采纳、成功 / 失败反馈与状态回显整体正常，论坛问答 MVP 当前已达到“可演示、可联调、可回归”的收口标准。

### 论坛投票 / 问答卡片口径与问答异常语义补丁

- **列表卡片口径已统一**：论坛列表中投票帖与问答帖现在统一为“左侧只放类型 / 状态标签，右侧再放数量统计”，避免出现 `投1 / 票1` 或左侧重复显示回答数的混乱表达。
- **问答采纳异常已收口**：`QuestionController` 已兼容事务 AOP 包装出来的 `AggregateException`，像“采纳自己的回答”这类业务拒绝场景现在会稳定返回业务错误，不再变成未处理 `500`。
- **最小回归已补**：控制器测试已新增聚合异常解包场景，覆盖“问答帖不存在”与“不能采纳自己的回答”两条关键路径。

### 聊天室 P1 冒烟联调通过

- **专项冒烟已完成**：已对频道切换、历史消息加载、文本发送、失败重试、图片消息、断线重连、成员面板与头像展示进行一轮手工联调。
- **本轮未发现新增阻塞**：消息重复、重连错乱、失败态不可恢复、头像串位与输入区布局异常等高感知问题本轮未再复现。
- **当前阶段判断更新**：聊天室 `P1` 已达到“核心交互稳定、可继续转出短观察期”的状态，后续如无新增回归，可按规划切向下一个候选事项。

## 2026-03-15 (周日)

### 当前阶段正式收口

- **阶段结论确认**：截至 `2026-03-15`，`Console` 权限治理 V1、聊天室 `P1`、论坛投票 MVP 与论坛问答 MVP 已全部完成本轮收口，可从当前主线移出。
- **权限治理边界再确认**：本轮完成的是“权限生效闭环”，即 `ApiModule + RoleModulePermission -> VoPermissions -> RouteGuard / usePermission`；`权限 / 菜单 / 按钮管理 UI` 不属于 V1 承诺范围。
- **Console 后续规划明确**：`权限配置后台与授权界面` 已转入后续 `Console-ext` 规划，后续优先考虑角色授权面板，以及菜单 / 页面 / 按钮授权配置，不作为本阶段阻塞项。
- **下一步入口统一**：规划口径已切到“上一轮收口完成，进入下一主线启动准备”，下一候选主线优先为 `P2-ext` 开源软件清单扩展。

### Console 联调与 OIDC 收口

- **Console 联调验收通过**：后台独立登录、回调、登出、权限快照和页面联调已完成一轮收口，当前未发现新的阻塞问题。
- **官方客户端种子修正**：内置 OIDC 客户端已统一收敛为 `radish-client`、`radish-console`、`radish-scalar` 三个官方应用，并补齐描述、开发者信息、应用类型和启用状态展示。
- **遗留商城客户端移除**：`radish-shop` 的遗留内置客户端种子已删除，不再占用官方客户端矩阵。
- **授权确认页上线**：Auth 已补齐独立的 OIDC 授权确认页，展示应用名称、描述、开发方、回调来源与授权范围，视觉风格向当前登录页对齐。
- **确认策略配置化**：授权确认是否展示已改为配置驱动，当前测试配置为 `radish-console` / `radish-scalar` 显示确认页、`radish-client` 跳过，后续可平滑收口到“官方应用免确认、第三方应用确认”。

### 规划口径二次校准

- **P2-ext 状态修正**：结合 `feat(welcome): 扩展开源软件说明页` 的实际产出，规划口径已更新为“内容扩展版已完成”；后续 backlog 中只保留更重的自动生成 / 发布物公告方向。
- **Console-ext 状态修正**：结合角色授权页、`console.access`、资源种子与 OIDC 联调收口情况，规划口径已更新为“Console-ext 一期已完成本轮收口”，不再继续挂在当前主线上。
- **下一主线建议前移**：当前规划已将“下一主线确认与启动准备”设为过渡状态，并把 `P4-ext` 论坛问答增强提升为优先候选入口。

### P4-ext 首轮最小闭环落地

- **问答视图已补齐**：论坛帖子列表已新增 `全部 / 问答` 视图切换，并在问答视图下支持 `全部状态 / 待解决 / 已解决` 过滤。
- **问答排序已补齐**：问答视图已新增 `最新 / 待解决优先 / 回答数` 三档排序，继续保持普通帖子与问答帖子排序边界分离。
- **回答区排序切换已补齐**：问答详情回答区已支持 `默认排序 / 最新回答` 两档切换，不影响评论区与普通帖详情。
- **回答区轻筛选已补齐**：问答详情回答区已支持 `全部回答 / 只看已采纳` 两档轻筛选，继续保持为详情内能力，不扩张到独立问答页。
- **问题历史入口已补齐**：问答帖详情已新增“问题历史”入口，直接复用现有帖子编辑历史弹窗，不额外扩张到回答编辑历史。
- **后端列表口径已收口**：帖子列表接口已补齐问答视图与状态参数，问答筛选和排序在服务端完成，不依赖前端本地拼接排序。
