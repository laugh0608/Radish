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
- **前端关系链页签升级**：`UserFollowPanel` 的“关注动态”新增“推荐/热门/最新”子切换，统一调用分发流接口并复用现有分页展示。
- **回归资产补充**：`UserFollowControllerTest` 新增分发流用例；`Radish.Api.Forum.http` 已增加三路流联调请求样例。

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
