# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-10 后续产品增量选择（P3-10-D 已收束，PR 暂缓）`
- **复核日期**：`2026-06-20`
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
  - `P3-10-B7` WebOS 功能迁移图已完成首批纯 Web 私域复访判断，后续转入维护回拉；不把 WebOS 全量迁移设为后续批次前置门槛。
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
  - `P3-10-B9` 首批代码、自动化验证和 Gateway PC / 移动页面补验已完成：新增 `User.PublicIndex` / `DisplayHandle`，注册登录名和邮箱规范化，登录支持登录名或邮箱，公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户排障展示切换为公开句柄，`DbMigrate` 与 PostgreSQL 差异 SQL 补齐公开索引入口；后续转入维护线。
  - `P3-10-B10` 已进入首批实现：系统设置从自由 key-value 收敛为代码级设置定义、默认值、覆盖值、风险等级、生效方式和低风险恢复默认入口；不把部署密钥、宠物经济数值或高危资产 / 会话设置搬进 Console。
  - `P3-10-B10` 第二批已补系统设置变更审计、修改原因 / 确认参数基础和 Console 历史查看入口；可编辑范围仍只保留低风险 `Site.Branding.FaviconUrl`，Medium / High / Critical 未开放编辑。
  - `P3-10-B10` 第三批已推进统一读取入口与首批内容长度设置：新增 `ISystemSettingProvider`，开放 `Content.PostTitle.MinLength`、`Content.PostBody.MinLength`、`Comment.Body.MinLength`，帖子 / 评论发布与编辑路径改为读取代码默认值叠加覆盖值后的强类型设置；High / Critical 仍不开放编辑。
  - `P3-10-B10` 第四批已推进设置校验规则可视化与编辑控件收敛：后端从代码级设置定义暴露数值范围、整数约束和影响范围摘要，Console 数字控件按规则限制输入并展示校验规则 / 影响范围；后端定向测试、`radish.console` 构建和 Gateway PC / 移动页面补验已通过。
  - `P3-10-B10` 第五批已推进内容发布上限设置：开放帖子标题最大长度、帖子正文最大长度、帖子摘要最大长度和评论内容最大长度，帖子 / 评论发布与编辑路径统一读取上下限设置并与 DTO / 实体硬边界对齐；后端构建、定向测试、完整测试和 `radish.console` 构建已通过，Gateway PC / 移动页面补验因本机 5000 / 3100 / 5100 端口未监听未闭合。
  - `P3-10-B10` 第六批已推进论坛轻回应内容最大长度设置：新增 `Comment.QuickReply.MaxContentLength`，轻回应发布路径改为通过 `ISystemSettingProvider` 读取上限，旧 `ForumQuickReply.MaxContentLength` 配置入口已移除；后端测试、Debug 构建和 Console 新增设置项的 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第七批已推进账号身份长度设置：新增 `UserIdentity.LoginName.MinLength`、`UserIdentity.LoginName.MaxLength`、`UserIdentity.DisplayName.MinLength`、`UserIdentity.DisplayName.MaxLength`，Auth 注册和 API 个人资料展示名校验改为通过 `ISystemSettingProvider` 读取上下限；本批不开放邮箱、登录凭证、高风险账号字段或 Console 账号变更动作，后端定向测试、完整测试、Debug 构建、`radish.console` 构建和 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第八批已推进论坛轻回应剩余运营参数：新增 `Comment.QuickReply.DefaultTake`、`Comment.QuickReply.MaxTake`、`Comment.QuickReply.PerPostCooldownSeconds`、`Comment.QuickReply.DuplicateWindowSeconds`，轻回应列表返回条数、单帖冷却和重复内容窗口改为通过 `ISystemSettingProvider` 读取；旧 `ForumQuickReply` 数值配置从宿主配置移除，`ForumQuickReply.Enable` 功能开关仍保留在 `appsettings`，不开放到 Console。后端定向测试、完整测试、Debug 构建、`radish.console` 构建和 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第九批已推进神评 / 沙发稳定性运营参数：新增 `Comment.Highlight.StabilityWindowMinutes`、`Comment.Highlight.ReplacementMinLikeDelta`，神评 / 沙发实时重算稳定窗口和替换阈值改为通过 `ISystemSettingProvider` 读取；旧 `CommentHighlight` 稳定窗口数值配置从宿主配置移除，任务启停、调度、扫描窗口、触发评论数量门槛和奖励数值仍不开放到 Console。后端定向测试、完整测试、Debug 构建和 `radish.console` 构建均已通过；真实页面 smoke 改为较大阶段推进完成或发布候选前集中执行，不作为每个系统设置批次的默认要求。
  - `P3-10-B10` 低 / 中风险系统设置首轮治理已阶段收束：当前开放 19 个设置，覆盖品牌 favicon、账号身份长度、内容发布长度、轻回应运营参数、神评 / 沙发稳定窗口和替换阈值。第九批后不继续默认追加第十批设置；评论 typing 节流、神评触发评论数量门槛、内容发布频率限制、论坛编辑历史策略、基础设施、安全会话、资产、奖励和高风险账号字段均转入后续独立评审或维护观察。
  - `P3-10` B1-B10 已完成首批代码推进或阶段收束；阶段级真实联调 smoke 已完成，当前转入 Web 信息流 / UI 结构整理。Flutter 承接准备排在 Web 默认入口和 API 契约稳定之后。
  - `P3-10` 阶段真实联调 smoke 已完成：Gateway / Api / Auth 健康检查通过，论坛详情 / 文档详情 public head smoke 通过，PC `1920x1080` 与移动 `390x844` 视图覆盖 `/discover`、公开帖子详情、公开文档详情、`/leaderboard`、`/shop`、`/circle` 和 `/console/` 授权确认页，未发现阻断问题；DPR 与 Console 深层设置页仍保留限制说明。
  - `P3-10-D` 首日整理已完成：`/discover`、公开帖子详情、`/circle`、`/me`、公开详情轻互动入口、公开文档详情和公开个人页已完成信息结构 / 标题层级 / 公开私域边界首批整理；公开壳层阶段词、入口动作与详情规范化来源返回已完成代码侧修正。
  - `P3-10-D` 阶段级 Gateway PC / 移动收口复核已开始：公开壳层主要入口、公开论坛流、公开文档详情、公开个人页、榜单、商城、圈子和我的状态入口均已覆盖首轮运行态检查；本轮修复公开文档详情重复 H1 与正文内链公开 URL 口径，完成公开商城首页 / 列表 / 详情标题层级收口，并修正公开榜单用户项 PublicId-only 跳转判断，后续继续按同类问题成组治理。
  - `P3-10-D` 第二批链接语义收口已完成：公开发现信息流底部入口、公开文档目录 / 卡片 / 搜索结果 / 详情返回、公开榜单类型切换 / 分页和 `/circle` tab / 分页均补齐真实 `href`，普通点击继续保留壳层导航、来源返回与登录回流语义；本轮未执行 Gateway / 浏览器真实 smoke。
  - `P3-10-D` 第三批链接语义收口已完成：公开个人页返回、内容 tab、分页和状态卡返回补齐真实公开 `href`，公开帖子详情未找到 / 错误状态卡返回补齐来源 `href`；普通点击继续保留公开壳层来源返回与内部导航语义，本轮未执行 Gateway / 浏览器真实 smoke。
  - `P3-10-D` 第三批后 Gateway 补验已完成：`check:host-runtime -- --details` 通过，PC `1920x1080` 与移动 `390x844` CSS 视口覆盖 `/discover`、公开论坛流、公开帖子详情、公开文档、公开榜单、公开商城、公开个人页来源返回、`/circle` 和 `/me` 登录回流；当前未发现阻断级页面问题，DPR 3 物理高分屏未写成完整结论。
  - `P3-10-D` 第四批链接语义收口已推进：共享公开壳层“发现”动作补齐真实 `/discover` href，`/me` 最近访问只对公开详情页转交“我的状态”来源，避免公开浏览页误用详情返回语义；本轮未执行 Gateway / 浏览器真实 smoke。
  - `P3-10-D` 已进入阶段收束判断：四批链接 / 入口语义治理后，同类问题继续主动深挖已进入收益递减；后续不再默认追加第五批链接扫尾，只在真实 smoke、自动化验证或明确缺口命中时回修。
  - `P3-10-D` 合并前验证准备已完成：`validate:baseline`、`validate:identity`、`check:host-runtime -- --details`、`radish.client` build 与 Gateway PC / 移动真实页面复核通过；验证命中的公开用户 LongId `string | number` 回退已收敛为字符串契约并复测通过。
  - `P3-10-D` PR 合并判断已完成：本批 5 个 `dev` 提交范围、验证结论和剩余限制已整理，`validate:ci -- --report` 通过，当前具备进入 `dev -> master` PR 的工程条件。
  - 用户明确本轮暂不创建 `dev -> master` PR；`dev` 已推送到 `origin/dev`，P3-10-D 保持可 PR 状态并转入维护回拉，下一步从 P3-10 后续候选中选择边界清楚的产品 / 治理增量。
  - `P3-10 后置治理候选：前端敏感日志脱敏` 已在本地提交 `a3d7df4f` 完成：`radish.client`、`radish.console` 与 `@radish/http` 已统一脱敏敏感字段日志，三端测试、type-check、`validate:baseline:quick`、`validate:ci`、lint 和仓库卫生检查均已通过；该项转入维护线。
  - `P3-10 后置安全治理：支付口令哈希升级` 已完成首批代码：新支付口令写入 Argon2id v2，历史 v1 SHA256 口令验证成功后自动升级，null 旧口令仍要求重置；后端定向测试已通过。
  - `P3-10 后置安全治理：支付 / 转账幂等与重放边界` 已完成首批代码、分层验证、仓库卫生检查和本地提交：商城购买 / 萝卜币转账已接入 `idempotencyKey`、请求摘要绑定和终态响应重放；转入发布候选前回归线。
  - 本轮 warning 清理已完成：SQLitePCLRaw 安全版本、`CoinController` XML 参数说明、Console 兼容权限扫描豁免和 Forum 评论 chunk warning 已收口，后端构建、client build、`validate:ci`、NuGet vulnerability scan 和仓库卫生检查均已通过；提交 `fac0150b`。
  - `WOG-1 写操作分级盘点` 已完成只读代码和文档盘点：资产 / 库存、奖励 / 玩法、内容互动、管理治理、通知 / 消息五类写入口已形成矩阵和候选排序；下一候选为 `WOG-2 内容互动关系写入与计数一致性`，进入代码前需先确认唯一索引、历史重复数据清理和服务层并发处理方案。
  - `WOG-2 内容互动关系写入与计数一致性` 已按确认方案完成首批代码实现：帖子 / 评论点赞关系增加用户-目标唯一索引，历史重复关系清理与计数校准进入部署 SQL 和 DbMigrate，服务层按 `Delta` 控制奖励、通知和高亮重算。
  - `WOG-3 背包 / 权益发放可靠性` 已按确认方案完成首批代码实现：订单级发放幂等、背包聚合唯一键、消耗品发放流水、条件扣减和道具使用事务边界已落地。
  - `WOG-4 奖励业务键唯一性` 已按确认方案完成首批代码实现：奖励流水和经验流水新增业务去重键，互动奖励、经验奖励、神评 / 沙发基础奖励、点赞加成和保留奖励已收敛到服务端业务键唯一真值。
  - `WOG-5 管理覆盖类写入版本语义` 已按确认方案完成首批代码实现：系统设置覆盖版本、Console 授权旧快照拦截、商品管理版本条件更新和内容举报审核状态条件更新已落地。

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)
- [Radish 电子宠物开发计划](/features/radish-pet-roadmap)
- [用户身份语义与公开索引](/architecture/user-identity-semantics)
- [系统设置治理专题](/guide/system-settings-governance)
- [支付与转账幂等治理](/guide/payment-idempotency-governance)
- [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)
- [WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)
- [WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20)
- [WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20)
- [WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20)
- [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20)
- [WOG-6 跨端幂等契约补齐方案](/records/wog-6-cross-client-idempotency-contract-plan-2026-06-20)
- [P3-10-B9 用户身份语义首批记录](/records/p3-10-b9-user-identity-first-batch-record-2026-06-15)
- [P3-10-D 合并前验证记录](/records/p3-10-d-pre-merge-validation-record-2026-06-19)
- [P3-10-D PR 准备记录](/records/p3-10-d-web-feed-pr-prep-record-2026-06-19)
- [个人圈子](/features/circle)
- [Token 不活跃过期治理](/guide/auth-idle-session)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **收束 P3-10-D Web 信息流 / UI 结构整理**
   - 首日公开页结构整理、首轮 Gateway PC / 移动运行态复核、四批同类入口语义治理、合并前验证和 PR 合并判断已完成。
   - 本批已覆盖公开壳层的进入动作、分享 URL、来源返回、登录回流、公开 / 私域边界、标题层级和真实内容密度，范围包括 `/discover`、公开论坛列表 / 搜索 / 标签 / 类型流、公开帖子详情、公开文档详情、公开个人页、`/leaderboard`、`/shop`、`/circle` 和 `/me`。
   - 本轮暂不创建 PR；不再默认追加第五批链接扫尾。若真实复核或验证命中明确缺口，再按同类问题成组修复。
2. **选择 P3-10 下一批产品 / 治理增量**
   - 前端敏感日志脱敏、支付口令哈希升级和 [支付与转账幂等治理](/guide/payment-idempotency-governance) 首批代码均已完成，不再作为下一步开发项。
   - [WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20) 已完成矩阵和候选排序；[WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20)、[WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20)、[WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20) 与 [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20) 均已确认并完成首批实现。
3. **把 P3-8-D 降级为维护与回拉线**
   - 移动 Web 公开页逐页打磨、Console 剩余页面迁移、购买 / 订单 / 背包重复复核、ID Phase A 广泛扫描不再作为默认日常主线。
   - 新增外部 ID 边界、扫描命中、真实编译错误或发布候选验收暴露问题时，再做定向治理。
4. **保留必要门禁，放宽过期约束**
   - Flutter 不再作为本批第一顺位；成熟 API 支撑的一组同一工作流动作可以在后续跨端承接批次进入受控写入。
   - 大页面重设计、端点级视觉治理和跨页面视觉体系变更仍先更新 Pencil；小范围功能、状态、表单、文案或行为等价修正不被 Pencil 阻塞。
   - 本地验证继续按风险分层；完整 baseline 默认放在准备合并到 `master` 前或发布部署节点。
   - 真实页面 smoke 不再按每个本地提交或每个低 / 中风险设置批次执行；默认在较大阶段推进完成、准备合并到 `master`、发布候选整备或用户可见页面明显变化时集中覆盖。

## 下一顺位

- `P3-10 后续治理专题：写操作可靠性与并发保护`
  - 新增 [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)，作为支付 / 转账幂等之后的写操作分级入口。
  - `WOG-1 写操作分级盘点` 已完成，结论见 [WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)。
  - [WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20) 已确认并完成首批实现：帖子 / 评论点赞关系唯一索引、历史重复数据清理、计数条件更新、点赞奖励触发边界和定向测试均已覆盖到代码批次。
  - [WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20) 已确认并完成首批实现；本批未扩展完整钱包、资产风控、浏览器通用 `sign`、字段级加密、安全会话、Redis 分布式锁平台或完整经济系统。
  - [WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20) 已确认并完成首批实现；本批未扩展完整经济系统、资产风控平台、Redis 分布式锁、Outbox 或通用奖励平台。
  - [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20) 已确认并完成首批实现；本批未扩展通用审批流、所有 Console CRUD 版本化、Redis 锁、Outbox 或分布式事务平台。
  - [WOG-6 跨端幂等契约补齐方案](/records/wog-6-cross-client-idempotency-contract-plan-2026-06-20) 已输出待确认，建议首批只补 Flutter 单商品购买的 `idempotencyKey`，暂不纳入 Flutter 转账、完整移动商城或服务端强制 key。
- `P3-10-D Web 信息流 / UI 结构整理`
  - 首日已完成 Web 默认入口、公开发现、论坛详情、公开文档详情、公开个人页、圈子、`/me` 与轻互动入口的结构整理和来源返回修正。
  - 首轮阶段级 Gateway PC / 移动收口复核已开始，并已修复公开文档详情重复 H1、正文内链公开 URL 口径、公开商城首页 / 列表 / 详情标题层级，以及公开榜单用户项 PublicId-only 跳转判断。
  - 第二至第四批已收口公开发现、公开文档、公开榜单、圈子、公开个人页、公开帖子状态卡、共享公开头部和 `/me` 最近访问的同类链接 / 来源语义问题：可导航动作提供真实 URL，普通点击保留当前壳层语义。
  - P3-10-D 合并前验证准备和 PR 合并判断已完成；用户明确本轮暂不创建 PR，后续只在恢复合并动作时复用 PR 准备记录。
  - 大页面结构、跨页面视觉体系或端点级视觉治理先更新设计源文件 / Pencil；小范围状态、文案、按钮或行为等价修正不被阻塞。
  - 不把首页瀑布流、个人圈子、推荐算法、联邦社交和完整 Flutter 承接一次性合并实施。
- `P3-10 后置安全治理评审：支付 / 转账幂等与重放边界`
  - 前端敏感日志脱敏、支付口令哈希升级和 [支付与转账幂等治理](/guide/payment-idempotency-governance) 首批代码 / 验证已完成。
  - 后续进入发布候选前回归线，不顺带启动浏览器通用 `sign`、字段级加密、安全会话、完整钱包、经济扩展或资产风控平台。
- `P3-10 阶段整理与自动化验证维护线`
  - B1-B10 已完成首批代码推进或阶段收束，阶段级真实联调 smoke 已完成；后续准备合并到 `master` 时再集中跑完整 baseline、identity、host runtime 和必要页面补验。
- `P3-10-B10 系统设置治理维护线`
  - 已推进设置定义注册表、默认值、覆盖值、风险等级、低风险编辑、恢复默认、修改原因 / 确认参数基础、变更审计历史、统一读取入口、帖子 / 评论 / 轻回应长度边界设置、帖子摘要长度设置、轻回应返回条数 / 冷却 / 去重窗口设置、神评 / 沙发稳定窗口 / 替换阈值设置、校验规则元数据和数字编辑控件约束；Console 不再把未注册 JSON 记录作为运营设置展示。
  - 当前开放 `Site.Branding.FaviconUrl`、`UserIdentity.LoginName.MinLength`、`UserIdentity.LoginName.MaxLength`、`UserIdentity.DisplayName.MinLength`、`UserIdentity.DisplayName.MaxLength`、`Content.PostTitle.MinLength`、`Content.PostTitle.MaxLength`、`Content.PostBody.MinLength`、`Content.PostBody.MaxLength`、`Content.PostSummary.MaxLength`、`Comment.Body.MinLength`、`Comment.Body.MaxLength`、`Comment.QuickReply.MaxContentLength`、`Comment.QuickReply.DefaultTake`、`Comment.QuickReply.MaxTake`、`Comment.QuickReply.PerPostCooldownSeconds`、`Comment.QuickReply.DuplicateWindowSeconds`、`Comment.Highlight.StabilityWindowMinutes`、`Comment.Highlight.ReplacementMinLikeDelta`；Medium 设置必须填写原因并确认风险等级 / 设置键，High / Critical 仍不开放编辑。
  - 第九批代码侧验证已完成；真实页面 smoke 改为较大阶段推进完成或发布候选前集中执行。低 / 中风险首轮治理当前阶段收束，不继续把候选参数默认排成第十批。
  - 不把评论 typing 节流、神评触发评论数量门槛、内容发布频率限制、论坛编辑历史策略、部署密钥、宠物经济数值、高危资产 / 会话设置或基础设施配置直接搬进 Console；论坛编辑历史如需治理，应作为编辑权限 / 历史保留专题独立评审。
- `P3-10-B9 用户身份语义与公开索引维护线`
  - 首批代码、自动化验证和 Gateway PC / 移动页面补验已完成；测试 / 生产上线前使用 `Deploy/sql/20260615_add_user_public_index.sql` 作为 PostgreSQL 版本化差异 SQL 审核入口。
  - 后续只在发布候选、跨端承接或真实缺口暴露时回拉，不把 `DisplayName#PublicIndex` 替代 `PublicId` 路由，不启动数据库主键迁移、邮箱通知系统、ActivityPub / WebFinger 或 Console 高风险账号字段治理。
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

- 先读取本页、[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20) 和 [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20)，必要时回看 WOG-2 / WOG-3 / WOG-4，确认当前仍处于 `P3-10 后续产品 / 治理增量选择`。
- 第一顺位：确认 `WOG-6 跨端幂等契约补齐` 方案；若确认首批范围，则进入 Flutter 单商品购买 `idempotencyKey` 接入代码实现。
- 第二顺位：跳过本轮 PR 创建步骤，保留 [P3-10-D PR 准备记录](/records/p3-10-d-web-feed-pr-prep-record-2026-06-19) 作为后续恢复合并动作的依据。
- 第三顺位：若真实 smoke、自动化验证或明确缺口重新命中 P3-10-D 阻断 / 清晰一致性问题，再定向回修；不再默认追加第五批链接语义扫尾。

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
