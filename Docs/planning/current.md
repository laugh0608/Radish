# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-10 Web-first 首页信息流与关键契约开发`
- **复核日期**：`2026-06-15`
- **最近结论**：
  - `P3-1` 至 `P3-5` 已完成公开内容增长、PublicId 试点、留存回流、动态 sitemap 与详情 head snapshot 首批建设。
  - `P3-6` 公开增长部署观察已收口，本地 Gateway 与生产公开域名 `https://radishx.com` 的 public head smoke 均通过，转入维护线。
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访和高信号候选筛查，当前未发现新的 `P0/P1`。
  - `P3-7-C` 已完成 WikiApp、ChatApp、ContentModerationService 与 ExperienceService 首批热区治理；剩余经验发放主流程进入后续评审池，不再作为当前默认主线。
  - `P3-8-A / B / C` 已完成多端功能缺口与 UI 设计入口审计、Flutter 公开榜单入口、Console 治理工作台设计端点和 Console 高频页面类型试点；继续筛剩余 CSS / 历史页面已转为低收益维护项。
  - `P3-8-D` 已完成移动 Web 公开视图矩阵、根路径 `/` 切向纯 Web、`/desktop` 保留入口、公开分享 / 来源返回、纯 Web forum / shop 登录回流、Flutter 通知 / 论坛 / 商城 / 资产 / 经验 / 最近访问 / 个人资料、单商品购买、订单 / 背包 / 胡萝卜流水排障、Console returnTo / 权限授权 / LongId 字符串守护等多轮治理。
  - `P3-8-D` 作为默认主线可以阶段收口；继续围绕同一批购买、订单、背包、权限或 ID 守护反复深挖，会进入收益递减，后续只在新缺口、新扫描命中、真实编译错误或发布候选验收暴露问题时回拉。
  - 项目仍处于单人开发期和功能建设期，尚未进入稳定运营期；本轮跳过发布后不直接切 Phase 4，而是在 Phase 3 内进入 `P3-10`，继续梳理跨端信息架构、功能缺口和 UI 开发优先级。
  - `P3-9-A / B / C / D` 已完成首批主路径验收入口、Flutter 登录移动用户、访客公开访问与分享、Console 管理员排障入口整备。
  - `P3-9-E` 已完成发布候选路径自动化总回归；`validate:baseline`、`validate:identity`、client / console / Flutter 定向验证均通过。
  - Flutter 登录态商城 / 钱包路径已完成修复复测；商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口未暴露新增 `P0/P1`。
  - 访客公开访问与 Console 管理员排障路径抽查未暴露新增阻断；`dev -> master` PR #54 已合并，`Repo Quality` 四项检查均通过。
  - P3-9 合并后本地验证已完成：`validate:ci` 通过；因规划入口命中默认执行面 / 门禁资产，已补 `validate:identity` 并通过。
  - 本轮明确跳过发布：不创建 tag，不等待镜像，不进入 M15 测试 / 生产部署流程；P3-9 转入维护与回拉线。
  - 当前继续第三开发阶段，不进入生产稳定运营；下一主线为 `P3-10`，围绕 Web 默认入口、信息流 / 个人圈子边界、PublicId 分批、评论互动治理、UI 改造和历史功能规划回拉选择下一批开发范围。
  - Flutter 先从当前第一顺位后移，后续在 Web 信息架构与 API 契约稳定后承接移动原生复访、通知、消息和轻互动。
  - `P3-10-A` 已完成初版任务归属、历史功能规划回拉和源码复核。
  - `P3-10-B1 / B3` 首批代码已完成：`/discover` 已调整为 Web-first 公开信息流，User PublicId 公开主页 / 榜单契约已落地并通过 PC 视角浏览器复核。
  - `P3-10-B4 / B5` 首批代码已完成：新增评论实时 Hub、Web / 公开详情订阅、typing 提示、评论树事件合并、神评 / 沙发稳定窗口、并列展示与奖励幂等；`dotnet build`、`dotnet test Radish.Api.Tests`、评论定向测试和 `radish.client` 构建均已通过。
  - `P3-10-B4 / B5` 评论实时与神评稳定性人工联调已确认通过，进入维护与发布候选前批次级回归线。
  - `P3-10-B2` 首批代码已完成：`/circle` 作为登录态个人圈子入口落地，发现 / 论坛 / 圈子职责分工已写入 P3-10 专题，关系链用户项补 `VoPublicId` 并支持 `/u/usr_...` 公开主页入口。
  - `P3-10-B6` 首批代码与 Gateway 下首轮真实联调已完成：Console 登录授权、refresh 请求参数、刷新后受保护页、Web 登录态页面、通知 / 聊天 Hub 和评论 Hub 均未发现新增代码阻断；Chrome 插件无法覆盖移动 DPR 视图，真实 7 天 idle 过期只能由定向测试覆盖，作为发布候选前补验项保留。
  - `P3-10-B7` 已完成公共壳层一致性清理、论坛公开详情轻互动回迁、Gateway / Chrome PC + 移动视图补验、公共头部动作策略、公开参与登录文案和来源返回语义多轮收口；公共页头部固定为公开发现、登录态圈子和历史工作台三层动作，`/desktop` 保留但不作为新增功能主动作。
  - `P3-10-C` 首批代码与 Gateway PC / 移动视图复核已完成：公开发现、公开论坛列表 / 搜索 / 标签 / 类型流和公开个人页内容项已补齐真实链接契约，普通点击继续保留公开壳层来源返回，新开标签 / 复制链接使用公开 URL。
  - `P3-10-C` 第二批代码与 Gateway PC / 移动视图复核已完成：`/circle` 关注动态和关系链用户跳转公开详情 / 公开个人页时，可通过一次性来源转交返回“我的圈子”；移动视图因 Chrome 插件鼠标派发超时，使用唯一链接键盘激活验证路由事件，不把触控点击写成已验证。
  - `P3-10-C` 第三批公开参与 / 登录恢复 / 来源返回矩阵和 Console 高频治理入口已完成；公开入口治理默认进入维护回归线，不继续围绕同一组返回文案、来源状态或 Console 高频入口反复深挖。
  - 当前不直接进入新阶段，也不直接启动 `P3-10-B8` 电子宠物代码；`P3-10-B7` WebOS 功能迁移图已完成首批纯 Web 私域复访判断，后续以维护回拉和 B8 边界设计为主。
  - `P3-10-B7` 首批迁移代码已推进纯 Web 通知复访入口：`/notifications` 作为登录态私域入口接入主路由、登录恢复和通知目标分流；本地契约测试、`radish.client` type-check / build 与 Gateway PC / 移动 smoke 已通过，用户真实手测确认通知单击可跳转。
  - `P3-10-B7` 第二批迁移代码已推进纯 Web 我的状态入口：`/me` 作为登录态私域入口聚合公开主页、成长、资产和最近复访，只读承接个人状态 / 经验 / 资产高价值复访，不搬迁转账、支付密码、完整钱包或完整个人中心。
  - `P3-10-B7` 第三批迁移代码已推进纯 Web 消息复访入口：`/messages` 作为登录态私域入口承接聊天通知中的会话 / 消息定位、登录恢复和公开个人页来源返回；完整聊天平台、私聊、搜索、Reaction、移动系统通知和设备级会话治理继续后置。
  - `P3-10-B7` WebOS 功能迁移图已完成进入 B8 前的首批收口判断：`/notifications`、`/me`、`/messages` 均已完成代码、定向测试、`radish.client` type-check / build 和 Gateway PC / 移动 smoke；用户真实手测确认纯 Web 通知单击可跳转，不再因 Chrome 插件自动化单击不稳定阻塞 B7 收口。
  - `P3-10-B8` 已完成电子宠物开发前冻结口径、Phase B 首批代码与 Gateway 首轮联调：新增 `PetProfile` / `PetStatLog`、`PetController`、`/pet` 登录态私域页面、`/me` 宠物摘要、领取 / 命名 / 基础照顾和状态变化流水；`dotnet test Radish.Api.Tests`、`radish.client` 构建和 Gateway PC / 移动 smoke 通过。
  - `P3-10-B8` 首轮联调前暴露本地主库缺少 `PetProfile` / `PetStatLog` 的迁移问题；已补版本化 SQL `Deploy/sql/20260615_add_pet_tables.sql`，本地经 `DbMigrate init/apply` 同步后已验证恢复。
  - `P3-10-B8` Phase B 体验补漏与契约测试已完成：`/pet` 已补状态洞察、状态等级、动作冷却展示和照顾反馈；后端定向测试覆盖重复领取、只读查询、幂等、每日上限、冷却、状态边界、动作状态和日志空态；当前转入发布候选前批次级回归线，不直接启动经济、商城、社区任务奖励或 Flutter 承接。
  - `P3-10-B8` 发布候选前批次级回归已完成：后端定向 / 完整测试、`radish.client` 测试 / 类型检查 / 构建、身份契约、迁移 SQL 自检和 Gateway PC / 移动页面补验均通过；已补 `/pet` 登录回流契约测试，未发现需要阻断的真实缺口。
  - `P3-10-B8` 合并前自动化总验证已完成：`validate:baseline`、`validate:identity`、`validate:baseline:host`、迁移 SQL 重放、`git diff --check` 均通过；最新运行态健康复查因本机 API/Auth 未监听未闭合，如 PR 需要最新健康端点结论，重启宿主后复跑 `check:host-runtime`。
  - 本轮不合并 B8，按整体计划直接进入 `P3-10-B9 用户身份语义与公开索引`。
  - `P3-10-B9` 首批代码与自动化验证已完成：新增 `User.PublicIndex` / `DisplayHandle`，注册登录名和邮箱规范化，登录支持登录名或邮箱，公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户排障展示切换为公开句柄，`DbMigrate` 与 PostgreSQL 差异 SQL 补齐公开索引入口；后端完整测试、`radish.client` / `radish.console` 构建、`validate:identity` 与 `validate:baseline:quick` 均通过。
  - `P3-10-B9` 运行态 Gateway 页面 smoke 当前未闭合：提权后 `check:host-runtime` 仍显示 API/Auth 端口未监听，`5000` 由 macOS `ControlCe` 占用并超时；待宿主恢复后再补 PC / 移动页面抽查。

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)
- [Radish 电子宠物开发计划](/features/radish-pet-roadmap)
- [用户身份语义与公开索引](/architecture/user-identity-semantics)
- [P3-10-B9 用户身份语义首批记录](/records/p3-10-b9-user-identity-first-batch-record-2026-06-15)
- [个人圈子](/features/circle)
- [Token 不活跃过期治理](/guide/auth-idle-session)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **推进 P3-10-B9 用户身份语义与公开索引**
   - 首批目标是把登录凭证、公开展示、公开搜索和 Console 排障语义拆清楚：`LoginName / Email` 保持私有，`DisplayName#PublicIndex` 用于页面展示、榜单、关系链和艾特搜索，`PublicId` 继续承担公开路由和分享回流。
   - 首批代码已完成模型、注册 / 登录、公开资料、榜单、关系链、艾特搜索、Console 用户列表 / 详情和迁移入口；当前保留 Gateway 运行态 PC / 移动补验。
   - 后续只在真实缺口、发布候选回归或跨端承接需要时继续扩展，不把数据库主键迁移、ActivityPub / WebFinger、邮箱通知系统或 Console 高风险账号字段治理并入本批。
2. **把 P3-8-D 降级为维护与回拉线**
   - 移动 Web 公开页逐页打磨、Console 剩余页面迁移、购买 / 订单 / 背包重复复核、ID Phase A 广泛扫描不再作为默认日常主线。
   - 新增外部 ID 边界、扫描命中、真实编译错误或发布候选验收暴露问题时，再做定向治理。
3. **保留必要门禁，放宽过期约束**
   - Flutter 不再作为本批第一顺位；成熟 API 支撑的一组同一工作流动作可以在后续跨端承接批次进入受控写入。
   - 大页面重设计、端点级视觉治理和跨页面视觉体系变更仍先更新 Pencil；小范围功能、状态、表单、文案或行为等价修正不被 Pencil 阻塞。
   - 本地验证继续按风险分层；完整 baseline 默认放在准备合并到 `master` 前或发布部署节点。

## 下一顺位

- `P3-10-B9 用户身份语义与公开索引`
  - 首批代码已完成并通过自动化验证；下一步在宿主恢复后补 Gateway `1920x1080` 与 `390x844` 视图抽查，重点覆盖注册文案、登录名或邮箱登录、公开个人页、公开榜单、关系链用户项、艾特搜索和 Console 用户列表 / 详情。
  - 测试 / 生产上线前使用 `Deploy/sql/20260615_add_user_public_index.sql` 作为 PostgreSQL 版本化差异 SQL 审核入口；本地 SQLite 开发库继续通过 `Radish.DbMigrate init/apply` 自动补列、回填和建索引。
  - 后续不把 `DisplayName#PublicIndex` 替代 `PublicId` 路由，不展示登录名 / 邮箱到公开页面，不启动数据库主键迁移、邮箱通知系统、ActivityPub / WebFinger 或 Console 高风险账号字段治理。
- `P3-10-B8 Radish 电子宠物维护线`
  - B8 已完成开发前冻结口径、Phase B 首批代码、Gateway PC / 移动首轮联调、首批体验补漏、后端契约测试补强、发布候选前批次级回归和合并前自动化总验证；本轮不继续合并动作，转入维护回拉。
  - 经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件和公开个人主页默认展示继续后置。
- `P3-10-B7 WebOS 功能迁移图收口`
  - `/notifications`、`/me`、`/messages` 三个纯 Web 私域复访入口已完成首批代码和 Gateway PC / 移动 smoke；后续只在新真实缺口、发布候选回归或 WebOS 保留入口阻断时回拉。
  - 论坛、公开详情、发现、圈子、榜单、文档阅读、商城公开浏览、个人状态、通知和消息复访已进入维护补漏；完整聊天平台、完整钱包、完整个人中心和 Flutter 系统通知继续后置。
- `P3-10-B6 Token 不活跃过期补验`
  - 保留移动 DPR 视图、真实 7 天 idle 等待和发布候选前批次级回归；不因工具限制把首轮联调写成完整全量通过。
  - 继续关注通知 / 聊天授权 Hub 停连、评论匿名订阅恢复、受保护页面重定向和 Console `/console/login?auto=1&reason=idle`。
- `P3-10-B4 / B5 评论实时与神评稳定性回归`
  - 评论实时与神评稳定性人工联调已通过；后续只在发布候选前做批次级回归，覆盖评论事件、typing、神评变化和奖励幂等。
- `P3-10-B2 个人圈子维护与回归`
  - 继续按 [个人圈子](/features/circle) 中的 `/circle` 登录态私域、`/forum` 讨论对象权威归属、`/discover` 公开分发面分工复核。
  - 如后续扩展关系链，只补真实复访需要的关注动态和用户跳转，不直接实现 ActivityPub / WebFinger、推荐算法、短动态或转发 / 引用。
- `P3-10-B1 / B3 维护与回归`
  - 将 `/discover` 从公开导航聚合页调整为可持续浏览的内容流方案。
  - 继续保留公开 head、分享、移动 / PC 布局、LongId 兼容读取和 PublicId 分享路由回归。

## 明日事项

- 第一顺位：`P3-10-B9 用户身份语义与公开索引` 已完成首批代码和自动化验证；宿主恢复后补 Gateway PC / 移动页面 smoke，并按页面实测结果决定是否需要成组修复。
- 第二顺位：B9 若继续推进，只处理公开身份语义真实缺口：注册 / 登录文案、公开展示句柄、艾特搜索、榜单 / 关系链、Console 排障入口和迁移入口；不扩展到联邦协议或邮箱通知。
- 第三顺位：保留 `P3-10-B7 WebOS 功能迁移图收口` 维护入口，只在发布候选回归、用户真实复访路径或新缺口暴露时回拉 `/notifications`、`/me`、`/messages`。
- 第四顺位：保留 `P3-10-B6` 补验入口，工具条件满足时补移动 DPR 视图；发布候选前再做真实 idle 与 Hub 停连 / 匿名恢复批次级回归。
- 第五顺位：保留 `P3-10-B2` 圈子回归入口，复核 `/circle` 登录回流、关注动态、关注 / 粉丝列表、`/u/usr_...` 跳转和论坛详情跳转。
- 第六顺位：保留 `P3-10-B1 / B3` 回归入口，必要时复核 `/discover`、`/leaderboard`、`/u/usr_...`、公开 head 与 `DbMigrate apply` 后的旧库补列结果；不要回到 P3-8-D 购买 / 订单 / 背包或 Console 低频页面筛查作为默认主线。

## 并行维护项

- 公开 head smoke、动态 sitemap、head snapshot 缓存与 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生产域名配置维护。
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护。
- Console 新增或改动页面继续优先复用 `@radish/ui` 组件、交互反馈和主题 token。

## 当前不做

- 不把维护观察继续当作唯一主线。
- 不等待真实用户反馈出现后才继续开发未完成功能。
- 不创建本轮发布 tag，不进入 M15 测试 / 生产部署流程。
- 不把跳过发布误判为进入生产稳定运营。
- 不继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 不继续把移动 Web 公开页逐页打磨、Console 剩余 CSS / 历史页面筛查作为默认主线。
- 不绕过 Pencil / 设计稿直接大面积改 UI；但小范围功能、状态、表单、文案或行为等价修正不被 Pencil 前置阻塞。
- 不把 Tauri 当作原生 UI 重写路线或 WebOS 默认分发路线。
- 不做“移动版 WebOS”，新功能默认不进入 WebOS。
- 不把 WebOS 所有功能全量迁移设为下一阶段前置门禁。
- 不再把 WebOS 全量迁移作为电子宠物后续迭代的前置门槛。
- 不让 PC 客户端抢占纯 Web / Flutter 主线。
- 不让 Flutter 抢占当前 Web 默认入口、信息流、ID 契约和评论互动治理主线。
- 不把首页瀑布流、个人圈子、推荐算法和联邦社交一次性混成同一个开发任务。
- 不一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史治理。
- 不启动完整 `PublicId` 全量迁移或数据库主键迁移。
- 不启动完整 Playwright / E2E 平台。
- 不启动完整可观测性平台或大而全运维平台。

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取。
- 没有主线切换、优先级变化或新的关键事实，不改本页。
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页。
- `P3-10` 细节统一写入 [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)。
