# 2026-03 第一周 (03-03 至 03-08)

## 2026-03-03 (周二)

### 聊天室 P0 编译收口与回归

- **后端编译错误修复**：修复 `ChatService` 对 `User.AvatarUrl` 的错误访问，在线成员头像字段先返回空值，确保聊天室主链路可编译。
- **后端构建验证**：`Radish.Api` 在 `--no-restore` 条件下完成编译通过，聊天室相关控制器、Hub、服务链路可用。
- **前端连接稳定性修复**：`chatHub` 在“连接成功/重连成功”后自动加入当前激活频道，降低首开或重连后漏订阅导致的实时消息丢失风险。
- **前端验证**：`radish.client` 类型检查通过，聊天室新增代码无 TS 类型回归。

### 聊天室联调资产补齐

- **新增联调脚本**：新增 `Radish.Api.Tests/HttpTest/Radish.Api.Chat.http`，覆盖频道列表、历史分页、发送、撤回、在线成员查询等 REST 主链路。
- **联调口径说明**：明确该脚本聚焦 REST，`MessageReceived/MessageRecalled/ChannelUnreadChanged` 等实时事件需配合 SignalR 客户端联调。

### 聊天室文档对齐更新

- **文档状态更新**：`chat-app-index.md` 从“设计阶段”更新为“P0 主链路已落地”。
- **架构文档对齐**：`chat-app-architecture.md` 同步当前代码目录与 store/action 实现现状，标注 P1 才进行 hooks/components 拆分。
- **实时文档收口**：`chat-app-realtime.md` 同步“连接后自动入组/重连自动回组”现状，并标记历史补拉为 P1 优化项。
- **系统文档补充**：`chat-system.md` 增加“当前实现快照”，注明 `ChannelCategory` 暂未单独落地、在线成员头像来源待补齐。

### 聊天室数据库拆分（Chat 专库）

- **实体路由落地**：`Channel`、`ChannelMessage`、`ChannelMember` 已增加 `[Tenant(configId: "Chat")]`，数据库路由与通知系统 `Message` 模式保持一致。
- **配置完成对齐**：`Radish.Api` 与 `Radish.Auth` 的 `Databases` 新增 `ConnId=Chat`，SQLite 默认文件为 `Radish.Chat.db`，并补齐 PostgreSQL `radish_chat` 示例。
- **迁移与种子对齐**：`DbMigrate` 聊天相关建表与默认频道 seed 已显式切换到 `Chat` 连接执行，避免误落主库。

### DbMigrate 可维护性重构

- **Seed 文件拆分**：将 `InitialDataSeeder.cs` 重构为 `partial` 多文件结构，按身份/论坛/聊天/等级/商城分类拆分，入口仅保留编排逻辑。
- **可读性提升**：单文件长度从千行级降到可维护范围，后续新增 seed 可按领域直接落到对应文件，降低冲突与检索成本。

### 明日待修复清单（夜间问题记录）

- **表情包附件被误判为孤立附件**：`2026-03-03 22:40` 日志出现 `FileCleanup` 将 `Sticker/2026/03/2028451843729784832.jpg` 移入回收站，需排查 Sticker 与 Attachment 的引用关系是否被清理任务正确识别。
- **表情包前端展示异常**：已新增表情包分组并上传表情，但前端表情包页面未显示新分组/新表情；需排查管理端写入链路、查询接口、前端缓存与刷新策略。
- **附件目录策略待确定**：当前回收站路径出现在 `Radish.Api/bin/Debug/net10.0/DataBases/Recycle/orphan/20260303/...`，目录层级过深且不直观；需讨论并确定是否统一落在解决方案根目录 `DataBases/`（含调试便利性与 Docker 部署影响评估）。

## 2026-03-04 (周三)

### 昨日遗留项修复：FileCleanup 误清理 Sticker 附件

- **孤立附件判定补强**：`CleanupOrphanAttachmentsAsync` 新增 Sticker 引用检测，清理前会排除已被 `Sticker.AttachmentId` 引用的附件，避免误清理。
- **回归测试补齐**：新增 `FileCleanupJobTest`，覆盖“附件被 Sticker 引用时不应被清理”的最小回归场景。

### 昨日遗留项修复：新增表情前端不展示

- **租户口径修复**：`radish.client` 的 `GetGroups` 请求改为携带认证（`withAuth: true`），避免匿名请求固定落到 `TenantId=0` 导致看不到租户分组。
- **链路结果**：Console 新增分组/表情后，Client 可按当前登录租户正确拉取与展示。

### 昨日遗留项修复：附件/回收站目录策略统一

- **统一路径工具落地**：新增 `AppPathTool`，统一解析 `DataBases` 根目录（优先解决方案根目录，容器内回退应用根目录）。
- **FileCleanup 路径收口**：`Temp` 与 `Recycle` 全部改为基于统一 `DataBases` 根目录，不再写入 `bin/.../DataBases`。
- **附件处理临时目录收口**：`AttachmentService` 的水印/EXIF 临时文件路径改为统一 `DataBases/Temp`。
- **分片上传临时目录收口**：`ChunkedUploadService` 的分片目录改为统一 `DataBases/Temp/Chunks`。

### 租户口径对齐：公共租户基线（TenantId=0）

- **种子用户口径统一**：`DbMigrate` 的默认用户（`system/admin/test`）统一使用 `TenantId = 0`，与当前“暂不启用实际多租户业务”的阶段目标一致。
- **历史数据纠偏**：`DbMigrate` 重复执行时，会自动将已存在默认用户的非 `0` 租户值修正为 `0`，避免新老库口径不一致。
- **本周任务单列**：在 `development-plan.md` 增加“补全租户相关逻辑”独立节点，明确当前公共租户基线与后续全链路补全范围。

### 验证状态

- ✅ `dotnet test Radish.Api.Tests --filter FileCleanupJobTest` 通过（新增回归测试通过）。
- ✅ `dotnet build Radish.slnx -c Debug` 通过。
- ✅ `npm run type-check --workspace=radish.client` 通过。

### 聊天室联调修复：Hub 路由与表情渲染

- **Gateway 路由补齐**：新增 `/hub/chat/{**catch-all}` 反向代理规则，修复聊天室 `negotiate` 404，确保 ChatHub 可经 Gateway 建链。
- **表情图片渲染兼容**：评论与 Markdown 渲染统一放宽贴纸 URL 安全校验，支持站内相对路径（`/uploads/...`）与 `http(s)` 绝对路径，修复“表情只显示 `:group/code:` 文本”的问题。
- **前端验证**：`radish.client` 与 `@radish/ui` 类型检查均通过。

## 2026-03-05 (周四)

### 租户隔离收口（M12 本周专项）

- **全局过滤修复完成**：`RepositorySetting` 已统一为 `TenantId<=0` 仅可见公共租户（`TenantId=0`），修复公共租户可见范围越权问题。
- **仓储统一化落地**：`BaseRepository` 完成租户读写作用域统一，覆盖查询、分页、聚合、更新、删除等主路径。
- **联表查询补齐**：`QueryMuchAsync` 已接入三表联查自动租户过滤，避免联表查询绕过隔离。
- **写入边界收口**：无租户上下文默认仅允许写公共租户；写入指定租户仅放行 `System/Admin` 或后台任务上下文。

### 实体与业务链路补齐

- **核心实体接入**：补齐 `UserBalance/UserBenefit/UserInventory/UserExperience/UserExpDailyStats/CoinTransaction/ExpTransaction/BalanceChangeLog` 的 `ITenantEntity`。
- **高风险链路修复**：用户提及（`SearchForMention`）、附件查询与去重、商城商品与订单、排行榜聚合查询已统一租户口径。

### 文档与回归资产

- **新增专题文档**：`architecture/tenant-isolation.md`，集中沉淀隔离策略、审计结论与验收清单。
- **规范文档更新**：`architecture/specifications.md` 增补“字段/分表/分库策略矩阵”与“写入约束规则”。
- **回归测试补齐并执行**：`TenantIsolationRegressionTests` 已补充行为类实体断言，并在 Linux 环境执行通过（4/4）。

### 续接完成：行为类实体租户升级 + Linux 验证

- **行为类实体升级完成**：`Reaction`、`UserPostLike`、`UserCommentLike`、`UploadSession`、`UserPaymentPassword` 已接入 `ITenantEntity` 与 `TenantId`（默认 `0`）。
- **Linux 构建闭环完成**：`dotnet build Radish.slnx -c Debug -m:1 /nr:false` 通过。
- **Linux 回归闭环完成**：`dotnet test Radish.Api.Tests --filter "FullyQualifiedName~TenantIsolationRegressionTests" -m:1 /nr:false` 通过。

### 当前状态与后续

- ✅ 租户隔离本周续接项已完成（行为类实体升级 + Linux 构建/测试闭环）。
- 🔄 下一步继续评估其余低风险实体是否需要升级为字段租户隔离（按审计收益与改造成本排序推进）。

## 2026-03-06 (周五)

### 社区关系链最小闭环落地（M12-P0）

- **后端关系链主链路上线**：新增 `UserFollow` 实体与 `UserFollowService`，完成关注/取关、关注状态、粉丝列表、关注列表与关注动态流接口。
- **关系链控制器落地**：新增 `UserFollowController`，统一提供 `Follow`、`Unfollow`、`GetFollowStatus`、`GetMyFollowers`、`GetMyFollowing`、`GetMyFollowingFeed`、`GetMySummary`。
- **关注动态流打通**：基于关系链聚合关注用户已发布帖子，形成“关系链动态”分页接口，满足用户主页动态流最小可用能力。

### 前端接入完成

- **论坛帖子详情接入关注交互**：帖子作者区新增关注/取关按钮，并展示作者粉丝数/关注数，支持实时状态刷新。
- **个人主页新增关系链页签**：新增“关系链”页签，支持三类视图：关注动态、我的粉丝、我的关注；包含分页与互关标记展示。
- **统一 API 封装**：新增 `api/userFollow.ts`，前端统一通过 `@radish/http` 调用关系链接口。

### 回归与验证

- ✅ `dotnet build Radish.slnx -c Debug -m:1 /nr:false` 通过。
- ✅ `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~UserFollowControllerTest" -m:1 /nr:false` 通过（4/4）。
- ⚠️ `npm run type-check --workspace=radish.client` 未执行通过（当前环境缺少 `tsc` 命令）。

### 内容治理闭环最小版（M12-P0）

- **治理主链路落地**：新增 `ContentReport`（举报单）与 `UserModerationAction`（禁言/封禁记录）实体，打通举报提交、审核队列、审核处理、手动治理动作与治理记录查询。
- **审核与动作联动**：管理员审核举报时可直接触发禁言/封禁动作，形成“举报 -> 审核 -> 治理执行 -> 记录沉淀”的最小闭环。
- **主流程拦截接入**：`PostController.Publish`、`CommentController.Create` 已接入发布权限校验，禁言/封禁状态下阻断发帖与评论。
- **回归资产补齐**：新增 `ContentModerationController` 及控制器单元测试；`Radish.Api.Forum.http` 已补充 ContentModeration 联调段。

### 分发能力补齐（M12-P0）

- **三路分发流落地**：`UserFollowController` 新增 `GetMyDistributionFeed`，支持 `recommend/hot/newest` 三种流式查询，保留既有 `GetMyFollowingFeed` 兼容旧调用。
- **基础权重配置化**：新增 `FeedDistributionOptions` 与 `appsettings.json` 的 `FeedDistribution` 节点，支持热门权重（浏览/点赞/评论）与推荐附加权重（关注作者加权、新鲜度衰减）动态调参。
- **前端关系链页签升级**：`UserFollowPanel` 已区分“关注动态”主流与“推荐/热门/最新”分发子切换，避免将关系链动态流与分发流混为同一入口。
- **回归资产补充**：`UserFollowControllerTest` 新增分发流用例；`Radish.Api.Forum.http` 已增加三路流联调请求样例。

### M12-P0 社区主线收口复扫（2026-03-07）

- **关系链口径修正**：个人主页“关系链”页签已恢复“关注动态”真实入口，同时保留“推荐/热门/最新”作为独立分发子视图，和后端 `GetMyFollowingFeed` / `GetMyDistributionFeed` 职责保持一致。
- **回归资产补强**：`UserFollowControllerTest` 补充非法 `streamType` 返回 400 的回归用例；`Radish.Api.Forum.http` 补充“关注动态流”与“非法 streamType”验收请求，便于人工联调时快速识别契约回退。
- **文档口径统一**：`development-plan.md` 已新增“社区主线收口快照”，明确“已完成可验收 / 可低风险补齐 / 留待后续阶段”三类边界，避免继续把 P1 能力误记为本轮收口项。
- **验收文档补齐**：新增 `features/community-m12-p0-acceptance.md`，沉淀四条主链的验收步骤、通过标准与后续阶段边界，并已加入 VitePress 侧边栏入口。
- **聊天室边界维持不扩张**：本轮未继续推进 `@mention`、图片上传 UI、引用回复等 P1 内容，仅保留 P0 主链路验收口径。

### 配置治理：Redis 部分共享收口

- **共享项上收**：将 `Redis.Enable` 与 `Redis.ConnectionString` 上收至根目录 `appsettings.Shared.json`，统一 API/Auth 的缓存开关与连接串默认值。
- **宿主差异保留**：`Radish.Api/appsettings.json`、`Radish.Auth/appsettings.json` 的 `Redis` 节仅保留 `InstanceName`，用于键名前缀隔离。
- **文档同步**：已对齐 `guide/configuration.md`、`architecture/framework.md`、`AGENTS.md`、`CLAUDE.md` 的配置加载顺序与 Redis 配置说明。

### 配置治理：数据库与 Snowflake 续接收口

- **数据库共享项上收**：将 `MainDb` 与 `Databases` 上收至根目录 `appsettings.Shared.json`，统一 API/Auth 默认连接配置，减少宿主重复配置漂移。
- **Snowflake 部分共享**：将 `Snowflake.DataCenterId` 上收至共享配置，API/Auth 各自仅保留 `Snowflake.WorkId`。
- **文档同步更新**：`guide/configuration.md`、`architecture/framework.md`、`AGENTS.md`、`CLAUDE.md` 已补充新的归属口径（共享项 vs 宿主差异项）。

### OIDC Claim 解析口径补齐（HttpContextUser）

- **TenantId 回退增强**：`HttpContextUser` 新增从原始 Token 回退解析 `tenant_id` / `TenantId`，与 `UserId` 的回退策略保持一致。
- **单测补充**：`HttpContextUserTests` 新增“仅 Bearer Token、无认证主体”场景，验证 `TenantId` 与 `UserId` 可正常解析。
- **验证结果**：`dotnet test Radish.Api.Tests --filter "FullyQualifiedName~HttpContextUserTests"` 通过（4/4）。

### 控制器 Claim 解析收口（第一批）

- **统一收口到 HttpContextUser**：`ExperienceController`、`LeaderboardController`、`ShopController` 已移除手写 `sub/jti` 解析，统一通过 `IHttpContextUser` 获取当前用户信息。
- **行为保持兼容**：`ShopController` 在用户名缺失时仍回退为 `"Unknown"`，避免管理操作审计字段空值。
- **编译验证**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false` 通过（0 Error）。

### 控制器 Claim 解析收口（第二批）

- **客户端管理控制器收口**：`ClientController` 已移除手写 `sub/jti` 解析，创建/更新/软删除审计字段统一改为 `IHttpContextUser.UserId`。
- **测试同步对齐**：`ClientControllerTest` 改为注入 `IHttpContextUser` mock，不再通过 `HttpContext` 手工塞 claim。
- **编译验证**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false` 通过（0 Error）。

## 2026-03-07 (周六)

### 身份语义收敛专项立项（最高优先级）

- **专项升级**：将原“Claim 解析补漏”正式升级为“身份语义收敛与 Claim 架构治理”专项，不再按控制器、Hub、中间件逐点修补，而是按统一身份语义层一次性治理。
- **设计文档落地**：新增 `architecture/identity-claim-convergence.md`，明确 `CurrentUser`、`ICurrentUserAccessor`、统一 Claim 标准化层、协议边界保留范围与防回归规则。
- **迁移文档落地**：新增 `guide/identity-claim-migration.md`，明确从文档、抽象、运行时迁移、旧接口废弃、协议输出收敛到 CI 防回归的完整阶段计划。
- **规划优先级调整**：`development-plan.md` 已将该事项提升为 **M12 当前最高优先级 P0 工程治理任务**，优先级高于新增功能扩张。
- **规范入口同步**：`framework`、`specifications`、`authentication` 与文档导航已同步增加入口，后续身份相关改造以这两份新文档为唯一执行依据。

### Claim 解析口径统一收口完成

- **控制器角色读取收口**：`PostController`、`CommentController`、`AttachmentController`、`UserController`、`ChannelMessageController` 已移除分散的 `role` claim 读取与 `User.IsInRole(...)` 判断，统一改为通过 `IHttpContextUser` 暴露的角色能力判定。
- **当前用户视图补齐**：`IHttpContextUser` / `HttpContextUser` 已补充 `Roles` 与 `IsInRole(...)`，并兼容 OIDC `role`、`ClaimTypes.Role` 及 Bearer Token 回退解析。
- **共享解析器落地**：新增 `UserClaimReader`，统一沉淀 `UserId/UserName/TenantId/Roles` 解析逻辑，避免控制器、Hub、中间件、权限处理器重复维护多套 claim 口径。

### Hub 与基础设施同步收口

- **Hub 口径统一**：`ChatHub`、`NotificationHub` 已改为复用 `UserClaimReader`，不再各自手写 `sub/name/tenant_id` 解析。
- **权限处理器收口**：`PermissionRequirementHandler` 已改为复用 `UserClaimReader.GetRoles(...)`，并下线手工 `ClaimTypes.Expiration` 过期判断，统一依赖认证中间件处理 Token 生命周期。
- **审计中间件收口**：`AuditLogMiddleware` 已改为复用 `UserClaimReader` 获取 `UserId/UserName/TenantId`，与控制器/Hub/权限处理器保持一致的审计口径。

### 验证结果

- ✅ `dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false` 通过。
- ✅ `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~HttpContextUserTests" -m:1 /nr:false` 通过（4/4）。
- ℹ️ 本轮已完成控制器、Hub、权限处理器、审计中间件四类入口的 Claim 解析收口；`Program.cs` 保持协议边界职责，不做额外抽象改造。

### 身份语义专项继续收敛（兼容层冻结）

- **运行时兼容层收束**：`PostController`、`CommentController`、`AttachmentController`、`UserController` 等重度控制器已完成从 `IHttpContextUser` 到 `ICurrentUserAccessor`/`CurrentUser` 的迁移，`Radish.Api` 运行时直接依赖已收束到 `Program.cs` 兼容注册。
- **底层入口收口**：新增 `App.CurrentUser` 标准静态入口，`Repository`、`Service`、`Infrastructure`、`SQL AOP` 等底层代码改为优先读取统一身份语义；`App.HttpContextUser` 已明确标记为兼容属性。
- **兼容残余显式冻结**：`IHttpContextUser.GetToken()`、`HttpContextUserCompatibilityExtensions` 等兼容 API 已补充 `[Obsolete]`，明确“禁止新增使用”。
- **剩余散点收尾**：`ExperienceController` 的管理员操作者回退已改为复用 `UserRoles.Admin`，`AttachmentService.DeleteFileAsync` 已接入当前用户上下文，不再硬编码审计操作者。
- **文档对齐**：`identity-claim-convergence.md` 与 `identity-claim-migration.md` 已补充“当前实施状态/兼容层最终边界”，后续协作以文档中的冻结边界为准。
- **验证结果**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false` 与 `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug -m:1 /nr:false` 均通过；`dotnet test` 仍受当前环境 `vstest socket` 限制，不作为失败判断依据。

### 路线图口径复核更新

- **路线图状态修正**：复核仓库实际代码后，`development-plan.md` 已从"身份专项仍为当前最高优先级主任务"调整为"身份语义收敛专项主体已完成，保留协议输出收敛与防回归资产两个尾项"。

## 2026-03-07 (周六)

### Wiki 版本历史 / 回滚能力落地

- **后端接口补齐**：`WikiController` 已新增版本历史列表、版本详情与按版本回滚接口，`WikiDocumentService` 已补齐对应服务实现。
- **回滚语义确定**：当前按“恢复为新版本”执行，仅恢复 `Title + MarkdownContent`，不回滚父级、排序、状态等结构字段，降低误操作风险。
- **前端能力接入**：`radish.client` 的 Wiki App 已增加历史面板、版本详情预览与回滚确认交互，管理员可在详情页直接查看和回滚历史版本。
- **回归资产同步**：补齐 `WikiControllerTest` 与 `Radish.Api.Wiki.http` 的版本历史 / 回滚覆盖，并同步更新 Wiki 系统设计文档。

### Forum 空值类型错误修复

- **构建阻断项清理**：修复 `PostDetail.tsx` 中 `post` 可能为 `null` 时仍访问 `voAuthorId` 的类型错误，恢复 `radish.client` 构建通过。

### DbMigrate 幂等性加固

- **用户时区偏好纠正**：`UserTimePreference` seed 已统一到公共租户 `TenantId=0`，重复执行会自动纠正旧记录，不再因唯一键冲突失败。
- **聊天室默认频道纠正**：Chat 默认频道改为按固定 `Id` 执行幂等 upsert，并绕过租户过滤读取旧记录，避免 `Channel.Id` 冲突。
- **默认用户 / 商城商品兜底**：默认用户与商城商品在命中唯一键时，会自动进入“纠正旧记录”分支，而不是直接中断 seed。
- **统一异常判断**：`DbMigrate` 新增统一唯一约束异常识别工具，减少各 seed 文件内对 SQLite 文本报错的散落判断。

### DbMigrate 自动种子日志收口

- **统一步骤执行器**：`InitialDataSeeder.SeedAsync` 已改为统一步骤注册与执行，自动输出“开始 / 完成 / 耗时 / 失败”日志。
- **自动汇总生成**：seed 结束后根据实际执行步骤自动生成汇总，不再需要手写维护固定清单。
- **占位步骤可见化**：`Wiki 文档`、`表情包默认数据` 当前虽未预置内容，但会在 seed 日志中明确输出“跳过”，避免误判为遗漏执行。

### 帮助中心迁移试点启动

- **默认 Wiki 种子落地**：`Radish.DbMigrate` 已补齐首批帮助中心种子，初始化后会生成“帮助中心 / 快速开始 / 配置说明 / 论坛使用指南”四篇默认文档。
- **迁移来源标记**：首批试点文档统一标记为 `DocsMigrated`，并记录来源路径，便于后续区分“手动创建 / 文件导入 / 文档迁移”三类内容来源。
- **种子覆盖策略**：重复执行 seed 时，仅会更新仍处于种子控制状态的 `DocsMigrated` 文档；若文档已有人为修改（`Version > 1`）则保留现状，避免覆盖编辑结果。

### docs / wiki 入口命名收口

- **桌面入口区分**：WebOS 应用注册表已将 `docs` 收口为“开发文档”，`wiki` 收口为“帮助中心”，避免用户混淆开发规范站与运行时帮助中心。
- **前端口径对齐**：`WikiApp` 侧边栏、标题与空状态文案已统一为“帮助中心”语义，并将来源类型展示改为中文标签。
- **来源路径可见化**：帮助文档详情页已展示迁移来源路径，便于后续人工校对 `radish.docs` 与 Wiki 内容映射关系。

### Wiki 方案文档状态同步

- **阶段状态更新**：`wiki-markdown-system.md` 已从“待进入实现”更新为“Wiki MVP 已落地，已进入 Phase 3 迁移试点”。
- **迁移清单纠偏**：首批试点文档清单中的论坛文档路径已从旧路径修正为 `docs/features/forum-features.md`。

### 验证状态

- ✅ `dotnet test Radish.Api.Tests --no-restore` 通过（135/135）。
- ✅ `npm run build --workspace=radish.client` 通过。
- ✅ `dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj --no-restore` 通过。
- **当前主线切换**：M12 当前功能主线已明确切换为 **P1 内容与文档体系重构**，优先推进 Markdown 导入/导出、Wiki/文档 App 承接与 `radish.docs` 迁移收口；身份专项尾项改为并行治理任务持续清理。
- **方案文档落地**：新增 `guide/document-system.md`，统一说明 Wiki / Markdown 文档体系的产品边界、后端模型、API、前端 App 结构、`radish.docs` 迁移策略与分阶段实施计划，后续开发以该文档为主依据。

### Wiki / Markdown 文档体系首批落地

- **后端 MVP 完成**：新增 `WikiController`、`WikiDocumentService`、`WikiDocument` / `WikiDocumentRevision` 模型、`WikiProfile` 映射与 `.http` 联调脚本，已打通文档列表、树、详情、创建、更新、发布、归档、Markdown 导入导出主链路。
- **前端 Wiki App 接入**：`radish.client` 新增 `wiki` 内置应用，已接入 WebOS 应用注册表，支持目录树浏览、搜索结果列表、Markdown 详情预览、创建/编辑、单文件导入与导出。
- **编辑能力复用**：Wiki 编辑器已复用 `@radish/ui` 的 `MarkdownEditor` / `MarkdownRenderer`，并接入附件上传业务类型 `Wiki`，支持在文档编辑中上传图片与文档链接。
- **验证结果**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug -m:1 /nr:false`、`dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug -m:1 /nr:false`、`npm run type-check --workspace=radish.client` 均通过。

### Wiki 首批回归资产补齐

- **DbMigrate 缺表检测补齐**：`Radish.DbMigrate` 的 `seed` 预检查已纳入 `WikiDocument` 与 `WikiDocumentRevision`，当 Wiki 表缺失时会自动触发 `init`，避免新库执行 seed 后遗漏 Wiki 表结构。
- **控制器回归测试新增**：新增 `WikiControllerTest`，覆盖文档不存在、创建成功、发布失败、Markdown 导入成功、Markdown 导出成功等最小主链路断言。
- **验证结果**：`dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj -c Debug -m:1 /nr:false` 与 `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug -m:1 /nr:false` 通过；`dotnet test` 仍受当前环境 `vstest socket` 权限限制中断，不作为代码失败判断依据。

## 2026-03-08 (周日)

### 文档 App 目录布局与交互收口

- **窄窗口自适应修复**：`WikiApp` 已改为根据文档窗口自身宽度切换单栏 / 双栏布局，修复 WebOS 窗口较窄时目录面板被挤压、内容区异常留白的问题。
- **目录树交互增强**：目录树已支持分级展开 / 收起；默认展开根层级，并在选中文档时自动展开祖先节点，提升长目录浏览可控性。
- **侧栏结构收稳**：目录树与检索结果继续共用同一侧栏内容面板，标题行、提示区与滚动区样式已同步对齐，减少窗口态下的层级混乱。

### 文档对齐更新

- **方案文档同步**：`guide/document-system.md` 已补充文档 App 的窄窗口单栏策略、目录树折叠规则与新增验收项，当前说明与实现保持一致。

### 验证状态

- ✅ `npm run test --workspace=radish.client` 通过。
- ✅ `npm run type-check --workspace=radish.client` 通过。

## 2026-03-08 (周日)

### SQL AOP 日志过滤与文档对齐

- **共享配置落地**：新增 `SqlAopLog` 共享配置与 `SqlAopLogOptions` 强类型选项，支持按 `Query/Insert/Update/Delete` 维度控制 SQL AOP 日志输出。
- **过滤能力补齐**：`SqlSugarAop` 新增 `SkipTables`、`SkipUsers` 过滤能力，可按表名或操作人直接跳过 SQL 日志记录。
- **大文本脱敏收口**：对 `MarkdownContent`、`Content`、`RequestBody` 等字段统一输出 `<text len=... omitted>` 占位，避免文档正文和请求正文刷屏。
- **默认噪音收敛**：共享配置默认跳过 `WikiDocument` 与 `WikiDocumentRevision` 两张表，固定文档同步阶段不再输出大量 SQL 日志。
- **文档同步**：更新 `guide/logging.md`、`architecture/framework.md`、`architecture/specifications.md`，补齐 `SqlAopLog` 的配置说明与默认行为。
- **验证结果**：`dotnet build Radish.Api/Radish.Api.csproj -c Debug` 通过，`dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter SqlSugarAopTests` 通过（5/5）。
