# 2026 年 5 月第 4 周开发日志

> 时间范围：2026-05-18 至 2026-05-24（Asia/Shanghai）

## 2026-05-18

- `P3-6-A` 本地 Gateway 公开增长观察首轮收口：公开 head smoke 已覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情首包 head。
- `Scripts/check-public-head-smoke.mjs` 已纳入 sitemap 分片 `<loc>` origin 自动检查，减少部署后人工临时抽查。
- `P3-6-B` 公开增长 smoke 失败诊断增强已收口：失败时输出请求 URL、状态码、content-type、响应 body 前段、疑似 SPA shell 判断、失败阶段和关键断言；self-test 与本地 Gateway smoke 均已通过。
- `P3-6-C` 部署观察与分流记录入口已补齐：新增公开增长观察记录模板，用于按 local / testing / release 前生产 facts 分流公开 head smoke、动态 sitemap、head snapshot、公开域名配置、分享预览和搜索抓取反馈。
- `P3-6-C` 首份本地观察记录已补齐：本地 Gateway public head smoke 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情并通过；后续优先补 testing URL 观察，生产域名验证改为 release 前置项。
- WebOS / PC 工作台成片工作流阻断级缺口筛查已完成：应用注册、窗口复用、继续使用、通知回流、forum / docs / shop 主路径未发现新的 `P0/P1`，轻量基线验证通过。
- 观察中发现本地 SQLite + Hangfire 后台任务并发读可能触发 reader closed 异常；已在仓储 SQLite fallback 读路径按连接串行化处理，并完成仓储项目构建与重启后初步观察。
- 下一步继续按 `P3-6` 真实使用观察口径分流高信号问题；未出现新的 `P0/P1` 前，不启动运营平台、完整可观测性平台或 SSR / SSG。
- 已将 Console 后续 UI 一致性治理写入未来规划：后续回拉 Console 扩展时优先复用 `@radish/ui` 组件、交互反馈与主题 token，避免后台视觉继续分叉。
- Console 后续 UI 一致性评估已补记录：当前后台已部分复用 `@radish/ui`，但页面壳、局部 CSS、直接 `antd` 引入和硬编码色值仍需在后续新增 / 改动页面时小范围收敛；当前不启动整站视觉重构。
- Console 用户列表一处疑似乱码错误提示已恢复为“获取用户列表失败”，并通过 `radish.console` 类型检查与 changed-only 文本卫生检查。
- 收工前已补 [2026-05-18 收工回顾与明日事项](/records/daily-handoff-2026-05-18)：今日提交已复核到公开 head smoke 手册、`P3-6` 规划、阶段专题、记录索引和开发日志；明日优先补 testing URL 公开增长观察，若 testing URL 暂不可用则只做 Console token bridge 小方案评审。

## 2026-05-19

- 部署态 Compose 与镜像轨道继续收敛：测试 / 生产默认使用统一 `Deploy/docker-compose.yaml`、`RADISH_IMAGE_TRACK` 与 `test-latest` / `release-latest` 浮动别名，固定 tag 仅用于可复现部署。
- PostgreSQL / Redis / Auth 证书持久化默认改为宿主目录，部署说明已强调测试 / 生产环境应提前创建并维护 `DeployData/Postgres`、`DeployData/Redis` 与 `DeployData/AuthCerts`。
- 测试 / 生产首次部署安全口径已收口：`Seed:DeveloperDefaultsEnabled=false` 时不创建开发管理员账号，首次访问根入口由部署人员创建首个管理员。
- 修复首次管理员初始化在 PostgreSQL 旧库上的 `SystemBootstrapState.CompletedUserId / CompletedTime` 非空约束问题，并补 `DbMigrate` 旧库约束修复。
- 修复 PostgreSQL 写入 `timestamp with time zone` 时拒绝 `DateTimeKind.Local` 的问题：SqlSugar AOP 参数层统一把 `DateTime` / `DateTimeOffset` 规范化为 UTC。
- 复核 Redis 当前能力：业务实际主要通过 `ICaching` / `IDistributedCache` 走 Redis 或内存缓存，Redis 已在部署态生效，但尚未系统性承担原子计数、在线状态、Backplane 或分布式锁。
- 新增 [Redis 与缓存治理专题](/planning/redis-cache-governance)，将通知未读原子计数、上传限流、聊天室在线状态、SignalR Backplane、商城 / 萝卜币幂等与并发保护、排行榜 / 热点读模型缓存纳入后置规划。
- 收工前已补 [2026-05-19 收工回顾与明日事项](/records/daily-handoff-2026-05-19)：今日提交已复核到部署指南、架构框架、开发路线图、backlog、Redis 专题和开发日志；配置指南当前已超篇幅硬上限，本次不继续追加。

## 2026-05-20

- `P3-6-C` 生产公开增长观察已补齐：`https://radishx.com` public head smoke 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情首包 head 并通过。
- 新增 [P3-6 公开增长部署观察记录（2026-05-20）](/records/p3-6-public-growth-observation-record-2026-05-20)，并挂入 [记录与验收索引](/records/)。
- `P3-6` 阶段收口结论已同步到 [当前进行中](/planning/current)、[开发路线图](/development-plan) 与 [第三开发阶段专题](/planning/phase-three-real-usage-contract-governance)：本轮未发现新的公开访问、head / sitemap、分享入口或回流 `P0/P1`，不切修复小闭环。
- 真实平台分享预览、Search Console / 爬虫日志、生产访问日志和用户回流断点转入运营维护观察；未出现真实证据前，不启动运营平台、完整可观测性平台、完整 E2E、SSR / SSG、正文预渲染或全量 `PublicId` 迁移。
- Console token bridge 小范围落地：`radish.console` 入口样式、`AdminLayout.css` 与 `adminFeature.css` 已接入局部 `--console-*` token 口径，并删除旧 Vite 模板入口样式污染；当前不启动后台整站视觉重构。
- `P3-7-A` WebOS / PC 工作台复访链路首批小闭环已完成：订单通知携带合法业务 ID 时进入商城订单详情，订单详情可回到商品详情，购买成功后进入订单详情，背包权益可基于 `VoSourceOrderId / VoSourceProductId` 回到来源订单 / 商品。
- 今日同步 [当前进行中](/planning/current) 与 [2026-05-20 收工回顾与明日事项](/records/daily-handoff-2026-05-20)；[商城前端设计说明](/guide/shop-frontend) 当前已超文档篇幅硬上限，后续若要迁入复访链路说明需先拆分该说明书；明日优先做商城复访链路收尾复核，不扩大到运营平台、完整 E2E、SSR / SSG 或全量 `PublicId`。

## 2026-05-21

- `P3-7-A` 商城背包消耗品来源入口已完成最小契约：`UserInventoryVo` 暴露 `VoSourceProductId`，`radish.client` 背包消耗品卡显示“相关商品”回流入口。
- 本次只把 `UserInventory.SourceProductId` 作为相关商品定位，不新增 `SourceOrderId`，不改变同类消耗品按类型和值聚合的库存模型，避免把最近关联商品误写成完整订单溯源。
- 验证已覆盖 `radish.client` 类型检查、`ShopProfileTest` 后端映射定向测试与 `git diff --check`。
- `P3-7-A` 首轮收尾复核结论为可收尾并转观察：本地 `/desktop` 与 API health 可达，商品列表 / 商品详情接口可读，本地有效消耗品库存已带相关商品 ID；未发现新的购买、订单、背包或通知回流高信号断点。
- `P3-7-B` WebOS / PC 工作台高信号候选筛查已完成：桌面启动续接、最近应用、通知分流、forum / chat / workspace 窗口参数和窗口几何记忆定向测试 `67/67` 通过；本地 `/desktop` 与 API health 可达，未发现新的 `P0/P1` 或需要立即拉起的小闭环。

## 2026-05-22

- `P3-7-C` 已完成近期开发任务重评估：不把维护观察当作无事可做，也不直接切入后续 `P3-8` 多端功能补全与 UI / Pencil 设计治理；下一批一天级任务选择 `WikiApp -> ChatApp` 工作台热区治理。
- `P3-7-C1 WikiApp 文档工作台首批热区拆分` 已完成：新增 `WikiSidebar` 承接目录树、搜索结果、筛选和阅读态侧栏；`wikiApp.helpers.ts` 承接编辑草稿、窗口参数解析、树展开、请求构造和时间格式化等纯逻辑。
- `WikiApp.tsx` 从约 `1759` 行降至 `1419` 行，回到项目单文件硬上限以内；本批不改 API、公开 docs 路由、视觉设计或文档业务能力。
- 验证已覆盖 `npm run type-check --workspace=radish.client`、`node --test --test-isolation=none ./Frontend/radish.client/tests/wikiApp.helpers.test.ts ./Frontend/radish.client/tests/workspaceNavigation.test.ts`、`npm run build --workspace=radish.client`、`npm run check:repo-hygiene:changed` 与 `git diff --check`；构建仍保留既有 `app-shop` chunk size warning。
- `P3-7-C2 ChatApp 聊天工作台首批热区拆分` 已完成：消息列表、频道侧栏、成员面板、输入区状态和纯 helper 已从 `ChatApp.tsx` 抽出，主文件从约 `2004` 行降至 `1489` 行。
- 本批保持行为等价，不启动私聊、消息搜索、Reaction、置顶、阅读回执或权限细化等 Chat P2 backlog 功能；下一顺位转向 `P3-7-C3` 后端 Service 热区评估与首批治理候选。
- 验证已覆盖 `npm run type-check --workspace=radish.client`、`node --test --test-isolation=none ./Frontend/radish.client/tests/chatApp.helpers.test.ts ./Frontend/radish.client/tests/chatNavigation.test.ts ./Frontend/radish.client/tests/desktopRecentApps.test.ts ./Frontend/radish.client/tests/windowGeometry.test.ts`（`24/24`）、`npm run build --workspace=radish.client`、`npm run check:repo-hygiene:changed` 与 `git diff --check`；构建仍保留既有 `app-shop` chunk size warning。
- 收工前已补 [2026-05-22 收工回顾与明日事项](/records/daily-handoff-2026-05-22)，并补正 [2026-05-21 收工回顾与明日事项](/records/daily-handoff-2026-05-21) 的提交级回顾；明日优先做 `P3-7-C3` 后端 Service 热区评估，不切入 `P3-8`。

## 2026-05-23

- `P3-7-C3` 后端 Service 首批热区治理已完成：选定 `ContentModerationService` 作为一天级、行为等价拆分候选，拆出举报目标快照解析与举报队列导航装配到 `ContentModerationService.Navigation.cs`。
- `ContentModerationService.cs` 从 `1735` 行降至 `806` 行；本批不改 API 契约、权限语义、数据库结构、审核规则或举报 / 治理运行时行为。
- 验证已覆盖 `dotnet test Radish.Api.Tests --filter ContentModerationServiceTest`（`7/7`）、`dotnet build Radish.slnx -c Debug` 与 `git diff --check`；沙盒内因无法读取用户级 `NuGet.Config` 失败后，已按验证用途提权重跑通过。
- `ExperienceService` 后端热区后续拆分已完成：每日统计窗口、异常观察规则、治理建议和最近治理留痕查询迁移到 `ExperienceService.DailyStats.cs`，治理动作留痕辅助逻辑迁移到 `ExperienceService.GovernanceActions.cs`，主文件从 `2807` 行降至 `1807` 行。
- 本批仍保持行为等价，不改经验发放、冻结、等级计算、API 契约、数据库结构或经验治理规则阈值。
- 验证已覆盖 `dotnet test Radish.Api.Tests --filter ExperienceServiceTest`（`13/13`）、`dotnet build Radish.slnx -c Debug` 与 `git diff --check`。
- 下一步继续复核 `ExperienceService.cs` 剩余缓存、等级配置与发放辅助逻辑是否存在一天级、安全可验证的行为等价拆分点；不直接切入 `P3-8`。
- `ExperienceService` 等级配置与缓存辅助逻辑已继续拆分到 `ExperienceService.LevelConfigs.cs`，覆盖等级配置查询、等级配置缓存读写、图标 / 徽章 URL 补全和纯等级计算；主文件进一步降至 `1652` 行。
- 本次仍不改经验发放、冻结、等级计算语义、API 契约、数据库结构或缓存配置口径；验证已覆盖 `dotnet test Radish.Api.Tests --filter ExperienceServiceTest`（`13/13`）、`dotnet build Radish.slnx -c Debug` 与 `git diff --check`。
- `ExperienceService` 交易记录辅助逻辑已拆分到 `ExperienceService.Transactions.cs`，覆盖交易分页筛选、经验类型过滤、日期过滤、分页归一化和交易用户名称补全；主文件进一步降至 `1514` 行。
- 本次仍保持行为等价，不改交易查询 API、分页规则、筛选语义、AutoMapper 映射或经验发放 / 冻结链路；验证已覆盖 `dotnet test Radish.Api.Tests --filter ExperienceServiceTest`（`13/13`）、`dotnet build Radish.slnx -c Debug` 与 `git diff --check`。
- `ExperienceService` 冻结状态辅助逻辑已拆分到 `ExperienceService.Freeze.cs`，覆盖冻结 / 解冻入口、事务内部方法、过期冻结自动释放和冻结状态判断；主文件进一步降至 `1305` 行。
- 本次仍保持行为等价，不改冻结语义、治理留痕、乐观锁条件、经验发放或排行榜过滤逻辑；验证已覆盖 `dotnet test Radish.Api.Tests --filter ExperienceServiceTest`（`13/13`）、`dotnet build Radish.slnx -c Debug` 与 `git diff --check`。
- `ExperienceService` 排行榜与管理员调整辅助逻辑继续拆分，`P3-7-C3` 后端 Service 热区治理阶段收口；剩余经验发放主流程进入后续单独评审池，不作为默认继续硬拆目标。
- `P3-8-A` 已切为当前 UI / 多端治理主线并完成首批审计，`P3-8-B1` Flutter 公开榜单只读入口已完成：新增 Flutter 原生榜单 tab、发现页跳转、公开榜单仓储、加载 / 空态 / 错误 / 刷新态与单测。
- `P3-8-B2` Console 治理工作台设计端点已建立：新增端点说明、Pencil 设计源文件入口和 `.pen` 工具维护规则，明确内容治理、经验治理、工作台信息架构、实现边界和验证入口。
- `P3-8-C1` Console 治理工作台结构基座已完成：内容治理页面拆出 helper、列定义与手动治理动作区；经验治理页面拆出 helper、列定义、用户查询摘要、观察摘要、复核区、流水区、治理动作表单、页头和等级配置。
- Console 首批治理页面已接入工作台布局承载，保持 API、权限、表单字段、表格列、经验规则、冻结 / 解冻语义和数据契约行为等价；`ModerationPage.tsx` / `ExperienceAdminPage.tsx` 分别降至 `843` / `712` 行。
- `Console Case Desk` 设计稿已扩展到 `P01-P08` 编号画板，覆盖壳层基座、内容审核、经验台账、治理调度总览、表格 CRUD、设置策略和两个移动端治理视图；`Console 样式与 Token 使用说明` 已同步低饱和暖灰 / 纸色背景、轻侧栏、按钮层级、页面类型和“不硬套模板”规则。
- 今日收工前已补 [2026-05-23 收工回顾与明日事项](/records/daily-handoff-2026-05-23)：明日优先推进 `P3-8-C2 Console 设计稿到实现的对齐试点`，先复核 `P01-P08` 与当前 Console 页面差距，选择一个低风险页面试点，不做整站一次性换皮。
