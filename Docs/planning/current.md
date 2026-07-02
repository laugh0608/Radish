# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-12 Web 完全化与 WebOS 收束`
- **复核日期**：`2026-07-02`
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
  - 当前继续第三开发阶段，不进入生产稳定运营；`P3-10` 已完成 Web-first 信息架构、长期契约治理和阶段收束准备，`P3-11` 已完成暂缓 PR 后的阶段收束，下一主线为 `P3-12 Web 完全化与 WebOS 收束`。
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
  - `P3-10-B8` 首轮联调前暴露本地主库缺少 `PetProfile` / `PetStatLog` 的结构同步问题；当时经 `DbMigrate init/apply` 同步后已验证恢复；上线前阶段性结构脚本已在 `P3-12-B6` 口径清理中移除。
  - `P3-10-B8` Phase B 体验补漏与契约测试已完成：`/pet` 已补状态洞察、状态等级、动作冷却展示和照顾反馈；后端定向测试覆盖重复领取、只读查询、幂等、每日上限、冷却、状态边界、动作状态和日志空态；当前转入发布候选前批次级回归线，不直接启动经济、商城、社区任务奖励或 Flutter 承接。
  - `P3-10-B8` 发布候选前批次级回归已完成：后端定向 / 完整测试、`radish.client` 测试 / 类型检查 / 构建、身份契约、结构同步自检和 Gateway PC / 移动页面补验均通过；已补 `/pet` 登录回流契约测试，未发现需要阻断的真实缺口。
  - `P3-10-B8` 合并前自动化总验证已完成：`validate:baseline`、`validate:identity`、`validate:baseline:host`、DbMigrate 基线重放、`git diff --check` 均通过；最新运行态健康复查因本机 API/Auth 未监听未闭合，如 PR 需要最新健康端点结论，重启宿主后复跑 `check:host-runtime`。
  - 本轮不合并 B8，按整体计划直接进入 `P3-10-B9 用户身份语义与公开索引`。
  - `P3-10-B9` 首批代码、自动化验证和 Gateway PC / 移动页面补验已完成：新增 `User.PublicIndex` / `DisplayHandle`，注册登录名和邮箱规范化，登录支持登录名或邮箱，公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户排障展示切换为公开句柄，`DbMigrate` 补齐开发库公开索引入口；后续转入维护线。
  - `P3-10-B10` 已进入首批实现：系统设置从自由 key-value 收敛为代码级设置定义、默认值、覆盖值、风险等级、生效方式和低风险恢复默认入口；不把部署密钥、宠物经济数值或高危资产 / 会话设置搬进 Console。
  - `P3-10-B10` 第二批已补系统设置变更审计、修改原因 / 确认参数基础和 Console 历史查看入口；可编辑范围仍只保留低风险 `Site.Branding.FaviconUrl`，Medium / High / Critical 未开放编辑。
  - `P3-10-B10` 第三批已推进统一读取入口与首批内容长度设置：新增 `ISystemSettingProvider`，开放 `Content.PostTitle.MinLength`、`Content.PostBody.MinLength`、`Comment.Body.MinLength`，帖子 / 评论发布与编辑路径改为读取代码默认值叠加覆盖值后的强类型设置；High / Critical 仍不开放编辑。
  - `P3-10-B10` 第四批已推进设置校验规则可视化与编辑控件收敛：后端从代码级设置定义暴露数值范围、整数约束和影响范围摘要，Console 数字控件按规则限制输入并展示校验规则 / 影响范围；后端定向测试、`radish.console` 构建和 Gateway PC / 移动页面补验已通过。
  - `P3-10-B10` 第五批已推进内容发布上限设置：开放帖子标题最大长度、帖子正文最大长度、帖子摘要最大长度和评论内容最大长度，帖子 / 评论发布与编辑路径统一读取上下限设置并与 DTO / 实体硬边界对齐；后端构建、定向测试、完整测试和 `radish.console` 构建已通过，Gateway PC / 移动页面补验因本机 5000 / 3100 / 5100 端口未监听未闭合。
  - `P3-10-B10` 第六批已推进论坛轻回应内容最大长度设置：新增 `Comment.QuickReply.MaxContentLength`，轻回应发布路径改为通过 `ISystemSettingProvider` 读取上限，旧 `ForumQuickReply.MaxContentLength` 配置入口已移除；后端测试、Debug 构建和 Console 新增设置项的 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第七批曾推进账号身份长度设置；`P3-12-B6-5` 后登录名字段和 `UserIdentity.LoginName.*` 设置已退场，当前账号身份设置只保留展示名长度、展示名改名频率和 PublicIndex 保留号治理。Auth 注册和 API 个人资料展示名校验继续通过 `ISystemSettingProvider` 读取上下限；本批不开放邮箱、登录凭证、高风险账号字段或 Console 账号变更动作，后端定向测试、完整测试、Debug 构建、`radish.console` 构建和 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第八批已推进论坛轻回应剩余运营参数：新增 `Comment.QuickReply.DefaultTake`、`Comment.QuickReply.MaxTake`、`Comment.QuickReply.PerPostCooldownSeconds`、`Comment.QuickReply.DuplicateWindowSeconds`，轻回应列表返回条数、单帖冷却和重复内容窗口改为通过 `ISystemSettingProvider` 读取；旧 `ForumQuickReply` 数值配置从宿主配置移除，`ForumQuickReply.Enable` 功能开关仍保留在 `appsettings`，不开放到 Console。后端定向测试、完整测试、Debug 构建、`radish.console` 构建和 Gateway PC / 移动页面补验均已通过。
  - `P3-10-B10` 第九批已推进神评 / 沙发稳定性运营参数：新增 `Comment.Highlight.StabilityWindowMinutes`、`Comment.Highlight.ReplacementMinLikeDelta`，神评 / 沙发实时重算稳定窗口和替换阈值改为通过 `ISystemSettingProvider` 读取；旧 `CommentHighlight` 稳定窗口数值配置从宿主配置移除，任务启停、调度、扫描窗口、触发评论数量门槛和奖励数值仍不开放到 Console。后端定向测试、完整测试、Debug 构建和 `radish.console` 构建均已通过；真实页面 smoke 改为较大阶段推进完成或发布候选前集中执行，不作为每个系统设置批次的默认要求。
  - `P3-10-B10` 低 / 中风险系统设置首轮治理已阶段收束：B10 首轮开放 19 个设置，覆盖品牌 favicon、账号身份长度、内容发布长度、轻回应运营参数、神评 / 沙发稳定窗口和替换阈值；`P3-12-B6-4` 后当前注册设置增至 24 个，新增展示名改名冷却 / 滚动窗口 / 窗口内最大次数、PublicIndex 显式保留号和靓号规则。第九批后不继续默认追加第十批设置；评论 typing 节流、神评触发评论数量门槛、内容发布频率限制、论坛编辑历史策略、基础设施、安全会话、资产、奖励和高风险账号字段均转入后续独立评审或维护观察。
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
  - `WOG-1 写操作分级盘点` 已完成只读代码和文档盘点：资产 / 库存、奖励 / 玩法、内容互动、管理治理、通知 / 消息五类写入口已形成矩阵和候选排序；`WOG-2` 至 `WOG-6` 已按排序完成首批治理。
  - `WOG-2 内容互动关系写入与计数一致性` 已按确认方案完成首批代码实现：帖子 / 评论点赞关系增加用户-目标唯一索引，历史重复关系清理与计数校准进入 DbMigrate 开发库结构补丁，服务层按 `Delta` 控制奖励、通知和高亮重算。
  - `WOG-3 背包 / 权益发放可靠性` 已按确认方案完成首批代码实现：订单级发放幂等、背包聚合唯一键、消耗品发放流水、条件扣减和道具使用事务边界已落地。
  - `WOG-4 奖励业务键唯一性` 已按确认方案完成首批代码实现：奖励流水和经验流水新增业务去重键，互动奖励、经验奖励、神评 / 沙发基础奖励、点赞加成和保留奖励已收敛到服务端业务键唯一真值。
  - `WOG-5 管理覆盖类写入版本语义` 已按确认方案完成首批代码实现：系统设置覆盖版本、Console 授权旧快照拦截、商品管理版本条件更新和内容举报审核状态条件更新已落地。
  - `WOG-6 跨端幂等契约补齐` 已按确认方案完成首批代码实现：Flutter 单商品购买已生成并提交 `shop:` 幂等键，失败后直接重试复用同一 key，成功或购买意图重置后生成新 key；本批不扩展 Flutter 转账、完整移动商城或服务端强制 key。
  - `P3-10 后续产品 / 治理增量选择` 已重新筛选下一批候选，推荐先推进 [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)：围绕 `clientSubmissionId`、内容指纹短窗口、分层频率限制和既有 `PostEditHistory` / `CommentEditHistory` 真值形成治理方案。
  - `论坛内容发布可靠性与编辑历史治理` 创建链路、首批编辑重试幂等、Flutter 当前论坛写入口承接和创建类频率限制已按确认方案完成：新增 `ContentSubmissionRecord` 内容提交意图记录，Web 发帖 / 评论 / 回答 / 帖子编辑 / 评论编辑生成并复用 `clientSubmissionId`，Flutter 纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑生成并复用同一字段；[P3-10 后续产品 / 治理增量评审记录](/records/p3-10-next-product-governance-review-2026-06-21) 已确认 Flutter 子评论编辑和回答编辑不进入下一批默认代码，P3-10 下一步转向阶段收束准备。
  - `P3-10` 阶段收束准备已完成完整批次范围、验证结论和剩余风险整理；当前暂缓 PR，不继续默认追加功能入口或链接扫尾。
  - `P3-11-A / B / D` 已完成发布候选验收矩阵、轻量复访缺口只读审计和阶段收束决策；暂不恢复 `dev -> master` PR，不创建 tag，不进入 M15 测试 / 生产部署流程，`P3-11-C` 定向回修未触发。
  - 下一主线切换到 `P3-12 Web 完全化与 WebOS 收束`：目标是在 PC / mobile 浏览器中完成项目正式版主路径，让 `/desktop` 退为历史兼容入口；Flutter 暂时后移，等 Web 正式版主路径稳定后再承接受控移动原生增强。
  - `P3-12-A` 功能资产盘点与迁移矩阵已完成只读审计：浏览器根路径和公开阅读路径已基本 Web 化，账户资产、商城购买、订单、库存和资产流水等正式版 Web 缺口已被拆入后续 B 组迁移，记录见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)。
  - `P3-12-B1` 账户资产与商城交易 Web 化方案与首批代码侧主路径已完成：新增正式 Web 资产与商城交易 return path，接入 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`，把 `/me` 完整钱包转向 `/me/assets/transactions`，公开商品详情购买改为 `/shop/product/:productId?intent=purchase` 登录回流并在购买成功后进入 `/shop/order/:orderId`；纯 Web 通知页的订单通知目标也已从桌面深链切到正式 Web 订单路由。`P3-12-C1` 首轮 B1 直接残留清理已完成，后续只在验收或新增阻断命中时回拉；真实 PC / mobile Gateway 复核放到小阶段准备验收时，在用户确认前后端已启动后集中执行。统一 UI 设计后置到页面迁移完成后的 `P3-12-D`，记录见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)。
  - `P3-12-B2` 完整个人中心 Web 化首批代码与正式链接语义补口已完成：新增 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 正式 Web 路由、登录回流、页面接入和 API helper 收口；我的内容与浏览历史列表已补真实公开 `href`，关注关系继续以 `/circle` 为权威入口。
  - `P3-12-B3` 论坛作者态 Web 化首批代码与小阶段验收已完成：新增 `/forum/compose`，扩展 `/forum/post/:postId?intent=answer|edit|history`，接入发帖、问答回答 / 采纳、作者帖子编辑和帖子编辑历史查看；登录回流走正式 Web 路径，写入继续复用 `clientSubmissionId`，WebOS 三栏工作台、Dock、窗口参数和 `openApp` 语义不迁入正式 Web。2026-06-22 已补 Gateway PC `1920x1080` 与移动 `390x844` CSS 视口 smoke，公开列表、发帖登录回流、公开详情 canonical 和作者态 `edit/history` return path 均通过；已使用 Browser 插件和种子账号 `admin` 覆盖已登录发帖、作者编辑、编辑历史与问答回答提交成功态，三类 `ContentSubmissionRecord` 均为 `Succeeded`，记录见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)。
  - `P3-12-B4` 文档作者态归属裁决已完成只读盘点和方案记录；`B4-1` 正式 Web 文档作者入口首批代码已完成，新增 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，公开 `/docs` 保持阅读 / 搜索 / 分享；`B4-2` Console 文档治理首批代码已完成，新增 `/console/documents` 治理入口、治理专用 API、权限键、资源种子和权限覆盖矩阵；Gateway PC / mobile 阶段 smoke 已完成，记录见 [P3-12-B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)。
  - `/messages` 已验证为正式 Web 消息 / 聊天入口；`P3-12-B5` Web 功能总入口首批代码与 Gateway PC / mobile smoke 已完成：新增 `/workbench`，公共壳层“工作台”默认指向正式 Web 功能地图，`/desktop` 保留为“WebOS 桌面版”兼容入口，记录见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)。`P3-12-D1` 已完成设计范围、页面矩阵和 Pencil 设计源拆分；`P3-12-D2` 已将 `public-web-unified-experience.pen` 重构并二次强化为 `P01-P16` 公开社区 App 页面族，覆盖公开首页、发现流、论坛列表 / 详情、评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页和移动公开任务流，记录见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)，实现口径见 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)；`P3-12-D3` 已将 `private-web-workflows.pen` 二次重构为 `P01-P30` 真实路由驱动的私域 / 作者态页面族，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和移动端 10 个单任务页面，记录见 [P3-12-D3 私域与作者态 Web 工作流设计源记录](/records/p3-12-d3-private-web-workflows-design-source-2026-06-25)，实现口径见 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)；`P3-12-D4` 已新增并继续扩展 `web-ui-foundation.pen` 共享 UI 基座至 `F01-F02`，补齐 client 公共壳层组件契约，记录见 [P3-12-D4 Web UI 共享基座设计源记录](/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25)，实现口径见 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)；`P3-12-D7` 已统一四个设计源的移动底栏胶囊样式，并在复审后修正公开移动 `P13` 工作台、Private 移动页密度和 Console `P13` 纸色运维页，记录见 [P3-12-D7 移动导航设计源统一记录](/records/p3-12-d7-mobile-navigation-design-source-alignment-2026-06-28)。
  - `P3-12-D5` 已完成 `console-governance-workbench.pen` 的 `P00-P18` 设计源收口：公共 Console 壳层、浅色图标侧栏、内容审核、经验台账、治理调度、表格 CRUD、设置策略、商业运营、文档治理、权限矩阵、运维任务和移动端任务流已对齐共享基座，记录见 [P3-12-D5 Console 治理工作台设计源重构记录](/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27)，实现口径见 [Console 治理工作台设计端点](/frontend/console-governance-workbench-design)。
  - `P3-12-D6` Console 视觉代码实现前盘点已完成，记录见 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27)；按最新设计源完整性审计，public / private 业务设计源已具备进入 `radish.client` 视觉实现的设计基础，Console 代码实现后移。
  - `P3-12-D8` `radish.client` 视觉实现首批已完成：新增共享 `WebShellHeader` / `WebStateSlot`，首批接入私域复访页、作者态页、公开 forum 状态入口和 discover / docs / leaderboard / shop / profile 公开状态槽；补公开内容宽度 token 与移动底部导航留白。`radish.client` 构建、仓库卫生检查和 `git diff --check` 通过；真实 Gateway PC / mobile smoke 待阶段验收或用户明确要求时，在用户确认前后端已启动后执行。记录见 [P3-12-D8 radish.client 视觉实现首批记录](/records/p3-12-d8-radish-client-visual-first-implementation-2026-06-28)。
  - `P3-12-B6` 身份语义二次收口设计、代码前盘点、分批方案、`B6-1` 至 `B6-6` 已完成代码侧与启动前验证收口，记录见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)。当前已固定 Auth 邮箱 + 密码登录、注册 / Bootstrap 必填 `DisplayName`、OIDC / CurrentUser 普通显示身份不再输出登录名，把论坛、聊天、榜单、圈子、公开个人页、转账搜索、资产流水和 Console 用户治理收敛到 `DisplayName / DisplayHandle` 口径，把个人资料改名接入服务端冷却 / 滚动窗口 / 审计记录，让普通注册和首个管理员初始化跳过已配置的 PublicIndex 保留号，并物理移除 `LoginName` / `UserRealName` 后端字段、个人资料真实姓名输入、登录名系统设置和 DbMigrate 旧身份回填逻辑。`validate:baseline`、`validate:identity`、`validate:baseline:host` 均已通过；Gateway PC / mobile 页面 smoke 待用户明确前后端已启动后再补。

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)
- [P3-11 发布候选整备与轻量复访补齐](/planning/p3-11-release-candidate-light-revisit)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [Radish 电子宠物开发计划](/features/radish-pet-roadmap)
- [用户身份语义与公开索引](/architecture/user-identity-semantics)
- [系统设置治理专题](/guide/system-settings-governance)
- [支付与转账幂等治理](/guide/payment-idempotency-governance)
- [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)
- [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)
- [WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)
- [WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20)
- [WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20)
- [WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20)
- [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20)
- [WOG-6 跨端幂等契约补齐方案](/records/wog-6-cross-client-idempotency-contract-plan-2026-06-20)
- [P3-10-B9 用户身份语义首批记录](/records/p3-10-b9-user-identity-first-batch-record-2026-06-15)
- [P3-10-D 合并前验证记录](/records/p3-10-d-pre-merge-validation-record-2026-06-19)
- [P3-10-D PR 准备记录](/records/p3-10-d-web-feed-pr-prep-record-2026-06-19)
- [P3-10 后续产品 / 治理增量评审记录](/records/p3-10-next-product-governance-review-2026-06-21)
- [P3-10 阶段收束准备记录](/records/p3-10-stage-closure-prep-record-2026-06-21)
- [P3-11 发布候选验收矩阵](/records/p3-11-release-candidate-acceptance-matrix-2026-06-21)
- [P3-11-B 轻量复访缺口只读审计记录](/records/p3-11-light-revisit-gap-audit-2026-06-21)
- [P3-11 阶段收束决策记录](/records/p3-11-stage-closure-decision-record-2026-06-21)
- [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)
- [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)
- [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)
- [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)
- [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)
- [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)
- [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)
- [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)
- [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)
- [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)
- [个人圈子](/features/circle)
- [Token 不活跃过期治理](/guide/auth-idle-session)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **启动 P3-12 Web 完全化与 WebOS 收束**
   - P3-11 已按“暂缓 PR、不发布、不创建 tag”收束；不再把 PR 决策作为当前开发主线。
   - `P3-12-A` 已完成功能资产盘点与迁移矩阵，按“正式版必需 / 发布前建议 / WebOS 保留 / 后置评审”分类 WebOS 与纯 Web 能力。
   - `P3-12-B1` 方案、路由 / 登录回流契约、商城私域正式 Web 入口、资产正式入口和公开购买回流已完成；`P3-12-C1` 首轮代码侧残留清理已完成，后续只在验收或新增阻断命中时回拉。
   - `P3-12-B2` 完整个人中心 Web 化首批代码已接入 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience`，并已补正式公开 `href` 导航语义。
   - `P3-12-B3` 论坛作者态 Web 化首批代码和小阶段验收已完成：`/forum/compose`、详情 `intent=answer|edit|history`、正式 Web 登录回流、发帖 / 回答 / 编辑 / 历史查看和 `clientSubmissionId` 延续均已收口。
   - `P3-12-B4` 文档作者态归属裁决与 `B4-1` 正式 Web 作者入口首批代码已完成；公开 `/docs` 保持阅读态，正式 Web 承接登录态作者页，WebOS Wiki 退为历史维护入口；`B4-2` Console 文档治理首批代码与阶段运行态 smoke 已完成。
   - `P3-12-B5` Web 功能总入口首批代码与 Gateway PC / mobile smoke 已完成：`/workbench` 和公共壳层入口调整已落地，正式 Web 已迁移功能可被集中发现，`/desktop` 退为桌面版兼容入口。
   - `P3-12-B6` 身份语义二次收口设计、代码前盘点、分批方案、`B6-1` 至 `B6-6` 代码侧与启动前验证已完成；该专题触达 Auth、注册登录、公开展示、搜索 / 艾特、资产流水、Console 设置和数据库结构口径。运行态 Gateway PC / mobile 页面 smoke 待用户明确前后端已启动后补验。
  - `P3-12-D1` 统一 UI 设计准备已启动；页面级 UI 设计与美化必须统一使用 Pencil 先做设计稿，再更新设计 / 说明文档，最后进入视觉实现。`P3-12-D2` 已将 `public-web-unified-experience.pen` 重构并强化为 `P01-P16` 公开社区 App 页面族，覆盖公开首页、发现内容流、论坛列表 / 详情、紧凑评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页和移动公开任务流。`P3-12-D3` 已将 `private-web-workflows.pen` 二次重构为 `P01-P30` 真实路由驱动的私域 / 作者态页面族，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和移动端 10 个单任务页面；`P3-12-D4` 已新增 `web-ui-foundation.pen` 共享 UI 基座，并补 `F02` client 公共壳层组件契约，用于约束跨 public / private / console 的 header、按钮、卡片、状态槽、移动 tab 和 client shell；`P3-12-D5` 已完成 `console-governance-workbench.pen` 的 `P00-P18` Console 设计源收口；`P3-12-D6` 已完成 Console 视觉代码实现前盘点；`P3-12-D7` 已统一 public / private / console / foundation 的移动底栏样式，并补齐公开移动工作台、Private 移动页密度和 Console `P13` 纸色运维页；`P3-12-D8` 已完成 `radish.client` 共享壳层、公开 / 私域状态槽、公开内容宽度 token 和移动底部留白首批代码对齐。当前下一步继续推进 `radish.client` 第二批视觉实现，优先私域 / 作者态真实数据面和移动单列任务流；Console 代码实现后移承接。
  - `P3-12-D9` 已完成 `radish.client` 私域交易数据面第二批视觉实现：资产流水、订单列表、订单详情和背包入口补共享状态槽、私域数据卡片节奏和移动单列任务流；`radish.client` 构建、仓库卫生检查和 `git diff --check` 通过。下一步继续通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现。
  - `P3-12-D10` 已完成 `radish.client` 通知 / 消息任务面视觉实现：`/notifications` 与 `/messages` 增加私域任务摘要、入口级状态槽容器、Web 宽高约束和移动单列布局；本批保留通知中心、聊天协议和 SignalR 逻辑不变，记录见 [P3-12-D10 radish.client 通知与消息任务面视觉实现记录](/records/p3-12-d10-radish-client-notification-message-visual-implementation-2026-06-29)。下一步继续圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现。
  - `P3-12-D11` 已完成 `radish.client` 圈子 / 宠物任务面视觉实现：`/circle` 补私域摘要卡、共享状态槽容器和移动单列任务流，`/pet` 补成长 / 流水 / 公开状态指标卡与入口状态槽容器；本批不修改关注关系、公开来源返回、宠物动作幂等或后端契约，记录见 [P3-12-D11 radish.client 圈子与宠物任务面视觉实现记录](/records/p3-12-d11-radish-client-circle-pet-visual-implementation-2026-06-29)。下一步继续论坛作者态和 Docs 作者态视觉实现。
  - `P3-12-D12` 已完成 `radish.client` 作者态任务面视觉实现：`/forum/compose` 补发帖作者任务摘要，Docs 作者台补作者任务摘要和共享 `WebStateSlot` 状态面板；本批不修改论坛发布器、提交幂等、Docs 保存 / 修订、Markdown 编辑器、权限判断或后端契约，记录见 [P3-12-D12 radish.client 作者态任务面视觉实现记录](/records/p3-12-d12-radish-client-author-workflow-visual-implementation-2026-06-29)。下一步进入 D9-D12 私域 / 作者态第二批收口检查与阶段验收准备。
  - `P3-12-D13` 已完成 D9-D12 私域 / 作者态第二批静态收口检查与 D9-D13 成组 Gateway PC / mobile smoke：移除通知 / 消息 / 圈子 / 宠物状态槽外层重复卡片，统一论坛发帖 / Docs 作者摘要卡 `8px` 半径；`radish.client` 构建、仓库卫生检查、`git diff --check` 和真实联调均通过，记录见 [P3-12-D13 radish.client 私域 / 作者态第二批视觉收口检查记录](/records/p3-12-d13-radish-client-private-author-visual-closure-2026-06-29)。
  - `P3-12-D14` 已完成 `radish.console` 视觉代码实现首批：扩展 Console 路由分组、侧栏 icon 和排序元数据，侧栏按总览 / 商业与资产 / 内容与文档 / 治理与权限 / 系统工具分组渲染；新增 `ConsolePageHeader`、`ConsoleStatusChip`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleToolbar`，并将 `SystemConfigList` 的页头、指标和筛选工具条迁入语义组件；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D14 radish.console 视觉代码实现首批记录](/records/p3-12-d14-radish-console-visual-first-implementation-2026-06-29)。下一步继续迁移订单 / 用户等表格代表页，确认语义组件在高频治理页面中的复用边界。
  - `P3-12-D15` 已完成 `radish.console` 订单表格代表页视觉迁移：`OrderList` 的页头、指标和筛选工具条已迁入 D14 语义组件，保留 URL 查询、来源返回、详情抽屉、失败重试、备注权限、跨用户 / 商品 / 流水跳转和表格密度不变；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D15 radish.console 订单表格代表页视觉迁移记录](/records/p3-12-d15-radish-console-order-table-visual-migration-2026-06-29)。下一步继续迁移用户 / 商品 / 文档等表格 CRUD 页，治理页迁移前先确认工作台区块和权限反馈边界。
  - `P3-12-D16` 已完成 `radish.console` 用户表格代表页视觉迁移：`UserList` 的页头、指标和筛选工具条已迁入 D14 语义组件，保留用户管理 API、查看权限、关键词 / 状态 / 角色筛选、分页、详情路由、表格列和对象摘要侧栏不变；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D16 radish.console 用户表格代表页视觉迁移记录](/records/p3-12-d16-radish-console-user-table-visual-migration-2026-06-29)。下一步继续迁移商品 / 文档等表格 CRUD 页，权限矩阵和治理工作台继续后置到区块边界明确后推进。
  - `P3-12-D17` 已完成 `radish.console` 商品表格代表页视觉迁移：`ProductList` 的页头、指标和筛选工具条已迁入 D14 语义组件，保留商品 API、权限、URL 查询、分页、表格列、详情抽屉、创建 / 编辑、上下架、删除、关联订单跳转和对象摘要侧栏不变；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D17 radish.console 商品表格代表页视觉迁移记录](/records/p3-12-d17-radish-console-product-table-visual-migration-2026-06-29)。下一步评估文档治理页是否先拆分治理工作台区块，再迁入语义组件；标签、分类、贴纸等低风险列表页可作为普通 CRUD 收尾候选。
  - `P3-12-D18` 已完成 `radish.console` 文档治理页区块边界与首批语义迁移：`DocumentGovernancePage` 的页头、指标和筛选区已迁入 D14 语义组件，首屏右侧补治理区块摘要，明确列表定位、详情证据、访问策略和版本回滚边界；文档治理 API、权限、筛选、分页、发布 / 下架 / 归档、访问策略、导入导出、删除 / 恢复、版本列表和回滚契约保持不变；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D18 radish.console 文档治理页区块边界与首批语义迁移记录](/records/p3-12-d18-radish-console-document-governance-visual-boundary-2026-06-29)。下一步可继续评估文档治理详情 / 访问策略 / 版本治理是否拆出稳定工作台区块，或先迁移标签 / 分类 / 贴纸等低风险普通列表页做 Console 表格视觉收口。
  - `P3-12-D19` 已完成 `radish.console` 标签 / 分类普通列表视觉迁移：`TagList` 与 `CategoryList` 的页头、指标和筛选工具条已迁入 D14 语义组件，保留标签 / 分类 API、权限、关键词 / 类型 / 状态 / 回收站筛选、分页、批量删除、排序、启停、固定标签、删除 / 恢复和表格密度不变；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，Gateway PC `1920x1080` 与移动 `390x844` CSS 视图真实联调已覆盖 Console 登录回流、标签 / 分类页面渲染和关键词筛选，记录见 [P3-12-D19 radish.console 标签与分类列表视觉迁移记录](/records/p3-12-d19-radish-console-taxonomy-list-visual-migration-2026-06-29)。
  - `P3-12-D20` 已完成 `radish.console` 贴纸分组 / 分组表情列表视觉迁移：`StickerGroupList` 与 `StickerList` 的页头、指标和筛选工具条已迁入 D14 语义组件，保留贴纸 API、权限、路由、图片预览、分组详情跳转、单个新增、批量上传、批量排序、删除确认和表格密度不变；本批确认贴纸页当前仍按普通 CRUD 外层迁移，不拆媒体资产工作台；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D20 radish.console 贴纸列表视觉迁移记录](/records/p3-12-d20-radish-console-sticker-visual-migration-2026-06-30)。
  - `P3-12-D21` 已完成 Console D14-D20 表格视觉成组静态收口检查：系统设置、订单、用户、商品、文档治理首屏、标签 / 分类和贴纸类页面均保持语义页头、指标网格、工具条、表格和摘要侧栏扫描顺序；本批移除 `SystemConfigList` 旧页头移动样式残留，并将 `StickerGroupList` 封面占位硬编码颜色改为 Console 语义 token；`radish.console` type-check / build、仓库卫生检查和 `git diff --check` 通过，记录见 [P3-12-D21 radish.console 表格视觉成组静态收口记录](/records/p3-12-d21-radish-console-table-visual-static-closure-2026-06-30)。下一步评估角色权限、内容治理、经验治理和运维任务页的页面类型边界。
  - `P3-12-D22` 已完成 Console 复杂页面类型边界评估：角色管理与角色权限配置归入 `P12` 权限矩阵首批候选；内容治理归入 `P02` 治理工作台；经验治理归入 `P03` 经验台账工作台；系统设置已在 D21 收口，`/hangfire` 当前仅为外部 iframe 运维入口，不扩展为项目内任务调度平台；记录见 [P3-12-D22 Console 复杂页面类型边界评估记录](/records/p3-12-d22-console-complex-page-boundary-2026-06-30)。下一步优先推进 `P3-12-D23` 角色权限外层语义迁移。
  - `P3-12-D23` 已完成 `radish.console` 角色权限外层语义迁移：`RoleList` 与 `RolePermissionPage` 已接入 D14 语义页头、指标和上下文工具区，保留角色 API、授权资源树、勾选继承、保存载荷、权限键 / 接口映射预览、路由守卫和表格动作不变；`radish.console` type-check / build 通过，记录见 [P3-12-D23 radish.console 角色权限外层语义迁移记录](/records/p3-12-d23-radish-console-role-permission-visual-migration-2026-06-30)。下一步评估并推进内容治理与经验治理工作台外层语义收口。
  - `P3-12-D24` 已完成 `radish.console` 内容治理与经验治理工作台外层语义收口：`ModerationPage` 和 `ExperienceAdminPage` 已接入 D14 语义页头、状态 chip 和工作台指标，内容治理页局部硬编码颜色改为 Console 语义 token；保留举报审核、手动治理、治理日志、经验复核、调经验、冻结 / 解冻和等级配置 API 不变；记录见 [P3-12-D24 radish.console 治理工作台外层语义收口记录](/records/p3-12-d24-radish-console-governance-workbench-outer-visual-convergence-2026-06-30)。下一步继续治理工作台内部区块样式收口。
  - `P3-12-D25` 已完成 `radish.console` 治理工作台内部区块样式收口首批：`ExperienceObservationSummary`、`ExperienceTransactionSection`、`ExperienceGovernanceReviewSection` 以及内容治理内部提示 / 筛选区已迁出目标 inline 样式与硬编码色，新增经验治理专用 CSS 并复用 Console token；保留业务 API、表单字段、表格列、URL 状态和提交动作不变；记录见 [P3-12-D25 radish.console 治理工作台内部区块样式收口记录](/records/p3-12-d25-radish-console-governance-workbench-internal-style-convergence-2026-06-30)。下一步进入 D23-D25 成组静态检查。
  - `P3-12-D26` 已完成 `radish.console` 角色权限、内容治理和经验治理页面成组静态收口：表格列弱文本 / 分组 / 正负状态、治理表单控件、角色权限树缩进和移动端缩进规则已迁入 CSS；目标目录不再命中 `style=`、硬编码十六进制色或 `rgba(...)`；保留角色权限、举报治理和经验治理业务契约不变；记录见 [P3-12-D26 radish.console 治理页面成组静态收口记录](/records/p3-12-d26-radish-console-governance-static-closure-2026-06-30)。随后进入系统工具 / 运维外壳收口。
  - `P3-12-D27` 已完成 `radish.console` 系统工具与运维外壳收口：`/hangfire` 已从路由临时组件迁入 `SystemTools/HangfirePage`，外层接入 D14 语义页头、指标和状态组件，iframe 容器样式迁入 CSS；`SystemConfigList` 继续作为当前内部系统设置代表页，`/hangfire` 仍仅承载受保护的外部 Hangfire Dashboard，不扩展为项目内任务队列 / 失败重试 / 运行审计平台；记录见 [P3-12-D27 radish.console 系统工具与运维外壳收口记录](/records/p3-12-d27-radish-console-system-ops-shell-2026-06-30)。随后进入 D28 阶段静态收口；如需内部运维平台，先补数据来源与 API 契约设计。
  - `P3-12-D28` 已完成 `radish.console` D14-D27 阶段静态收口：路由认证中、无 Console 权限和懒加载状态的旧 inline 样式已迁入 `routerComponents.css`，`routerComponents` 与 `SystemTools` 不再命中目标样式残留；阶段记录已归档深层表单、详情抽屉和批量上传弹窗的剩余静态风险，记录见 [P3-12-D28 radish.console 阶段静态收口记录](/records/p3-12-d28-radish-console-stage-static-closure-2026-06-30)。
  - `P3-12-D29 / D30` 已完成 `radish.console` 深层表单与详情 / 抽屉静态收口：商品、分类、贴纸和贴纸分组表单的上传预览、隐藏输入、控件宽度、弱提示文本和弹窗 footer 样式已迁入 `adminForm.css`；`OrderDetail`、`ProductDetail`、`DocumentGovernancePage` 的危险色、图片展示、隐藏输入和抽屉全宽布局已迁入 CSS，`StickerBatchUploadModal.css` 历史提示色同步改为 Console token；业务 API、权限、路由、上传、备注、访问策略和版本回滚动作保持不变，记录见 [P3-12-D29 radish.console 深层表单静态收口记录](/records/p3-12-d29-radish-console-deep-form-static-closure-2026-06-30) 与 [P3-12-D30 radish.console 详情 / 抽屉静态收口记录](/records/p3-12-d30-radish-console-detail-drawer-static-closure-2026-06-30)。
  - `P3-12-D31-D35` 已完成 `radish.console` 阶段运行态复核、数据补验和表格交互代码侧收口：Gateway 下已覆盖 Console 登录回流、商品详情、文档详情 / 版本治理、订单空态、表情分组空态，以及补安全测试数据后的 `OrderDetail`、分组表情列表和 `StickerBatchUploadModal` PC / mobile CSS 视口；D33-D35 已完成操作列换行、运维 / 治理静态残留、Dashboard 最近订单、用户详情内嵌表格、系统设置历史、文档版本弹窗和贴纸批量上传表格滚动 / 分页布局收口；贴纸弹窗 AntD `Alert.message` 告警与 Auth `wwwroot` 缺失启动告警已收口。D35 只是 Console 表格交互代码侧收口，不代表 `P3-12-D` UI 专题完成，记录见 [P3-12-D31 运行态复核](/records/p3-12-d31-radish-console-stage-smoke-2026-06-30)、[P3-12-D32 数据补验](/records/p3-12-d32-radish-console-data-smoke-and-auth-webroot-2026-06-30)、[P3-12-D33 表格可读性首批](/records/p3-12-d33-radish-console-table-readability-first-closure-2026-06-30)、[P3-12-D34 运维与治理表格静态收口](/records/p3-12-d34-radish-console-ops-table-static-closure-2026-06-30) 与 [P3-12-D35 表格交互代码侧收口](/records/p3-12-d35-radish-console-table-interaction-code-closure-2026-06-30)。
  - `P3-12-D36-D50` 已完成 UI 专题差距口径、四类设计源差距矩阵、边界裁决、阶段验收清单、Gateway PC / mobile 阶段验收、纠偏、页面缺口核对、Docs 作者态动作补漏、Public / Private 主路径代码侧复核、Console 深层管理动作复核、移动响应式抽样、Pencil UI 实现完成度复核、UI 实现证据收口、候选前启动前验证、Gateway 真实页面复核和 D49 后口径纠偏；D49 不能作为 UI 专题退出结论，下一步继续留在 `P3-12-D` 推进 UI 实现。
2. **保持 P3-10 可恢复合并状态**
   - `P3-10-D` 已完成公开页整理、四批入口语义治理、合并前验证和 PR 合并判断；不再默认追加第五批链接扫尾。
   - 前端敏感日志脱敏、支付口令升级、支付 / 转账幂等、`WOG-1` 至 `WOG-6`、论坛内容发布可靠性和 Flutter 作者编辑承接已纳入完整批次范围。
   - 若恢复 `dev -> master`，必须先刷新 `master..dev` 范围；无新增影响面时复用 P3-10 阶段收束准备记录。
3. **把 P3-8-D 降级为维护与回拉线**
   - 移动 Web 公开页逐页打磨、Console 剩余页面迁移、购买 / 订单 / 背包重复复核、ID Phase A 广泛扫描不再作为默认日常主线。
   - 新增外部 ID 边界、扫描命中、真实编译错误或发布候选验收暴露问题时，再做定向治理。
4. **保留必要门禁，放宽过期约束**
   - Flutter 不再作为本批第一顺位；待 Web 正式版主路径稳定后，再承接移动原生复访、通知、消息和轻互动。
   - `P3-12` UI 设计与美化专题内的页面级、端点级和跨页面视觉改造必须先更新 Pencil；明确 bug、低风险文案和单点状态修正不归入 UI 美化专题。
   - 本地验证继续按风险分层；完整 baseline 默认放在准备合并到 `master` 前或发布部署节点。
   - 真实页面 smoke 不再按每个本地提交或每个低 / 中风险设置批次执行；默认在较大阶段推进完成、准备合并到 `master`、发布候选整备或用户可见页面明显变化时集中覆盖。

## 下一顺位

- `P3-12 Web 完全化与 WebOS 收束`
  - 新增 [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)，承接 P3-11 暂缓 PR 后的正式开发主线。
  - `P3-12-A` 已完成只读盘点，结论见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)。
  - `P3-12-B1` 方案、路由 / 登录回流契约、商城私域正式 Web 入口、资产正式入口、公开购买动作和交易回流替换见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)；`P3-12-C1` 首轮残留清理见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)，真实 Gateway PC / mobile 复核后置到小阶段验收。
  - `P3-12-B2` 首批代码已补 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 的路由、登录回流和正式 Web 导航语义，方案见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)；`P3-12-B3` 首批代码与小阶段验收已完成，见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)；`P3-12-B4` 归属裁决、`B4-1` 正式 Web 作者入口、`B4-2` Console 文档治理和阶段运行态 smoke 已完成，见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)、[P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22) 与 [P3-12-B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)。`P3-12-B5` `/workbench` 首批代码与 Gateway PC / mobile smoke 已完成；`P3-12-B6` 代码侧与启动前验证已完成；`P3-12-D2-D8` 已完成公开 Web、私域 / 作者态、共享 UI 基座、Console 治理设计源、Console 实现前盘点、移动导航统一和 `radish.client` 首批共享壳层代码对齐；`P3-12-D9-D13` 已完成私域 / 作者态第二批视觉实现、静态收口和 Gateway PC / mobile 成组验收；`P3-12-D14-D35` 已完成 Console 首轮视觉迁移、静态收口、局部运行态复核、数据补验和表格交互代码侧治理；`P3-12-D36-D50` 已确认 D49 只代表当前已实现页面无阻断，不代表 UI 专题退出，下一步进入 D51 私域 / 作者态移动任务流 UI 对齐首批。
  - 功能迁移只迁移正式版产品能力，不迁移 WebOS Dock、窗口系统、桌面背景、窗口几何记忆或桌面 app 外壳；B1 替代路径可用后，只清理与默认产品路径直接冲突的 `/desktop` 回跳。
  - `P3-12-D` 继续进行；下一步进入 `P3-12-D51 radish.client 私域 / 作者态移动任务流 UI 对齐首批`，不进入 `P3-12-E`。
- `P3-11 发布候选整备与轻量复访补齐维护线`
  - `P3-11-A / B / D` 已完成；当前不恢复 PR、不发布、不创建 tag，`P3-11-C` 未触发。
  - 若后续真实验收命中明确阻断，再回拉定向修复；否则不继续围绕 P3-11 决策停留。
- `P3-10 阶段收束准备维护线`
  - [P3-10 后续产品 / 治理增量评审记录](/records/p3-10-next-product-governance-review-2026-06-21) 与 [P3-10 阶段收束准备记录](/records/p3-10-stage-closure-prep-record-2026-06-21) 已完成当前后续候选和完整批次验证判断。
  - 批次级验证已完成：`validate:ci -- --report`、`validate:baseline`、`validate:identity`、`flutter analyze`、`flutter test`、`check:host-runtime -- --details` 均通过；Gateway 已覆盖匿名公开入口、Console 登录回流和公开详情直链的 PC / 移动真实页面复核。
- `P3-10 后续治理专题：论坛内容发布可靠性与编辑历史治理`
  - 新增 [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)，承接 WOG 首轮收束后的下一批产品 / 治理增量选择。
  - 已按确认方案推进 `PublishPostDto`、`CreateCommentDto`、`CreateAnswerDto`、`UpdatePostDto` 和 `UpdateCommentDto`：Web 端生成并复用 `clientSubmissionId`，Flutter 当前已有纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑生成并复用同一字段，服务端用 `ContentSubmissionRecord`、请求摘要、短窗口内容指纹和近期成功写入窗口防重复提交与创建类刷屏。
  - 编辑历史继续以既有 `PostEditHistory` / `CommentEditHistory` 为真值，不新增通用编辑历史表；帖子 / 评论编辑无变化不写历史、不递增编辑次数，同 key 成功重放不重复执行更新。
  - 本批已确认新增 `ContentSubmissionRecord`、先覆盖创建链路和首批编辑重试、采用默认短窗口和创建类限频默认窗口、`Pending` 重试直接返回“正在提交”，Flutter 已承接当前存在的纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑入口。
  - 本批不启动独立频率限制平台、完整反垃圾系统、Redis 分布式锁、完整审核平台、轻回应重做、Flutter 转账、完整移动商城或服务端强制资产写入口必须传 key。
- `P3-10 后续治理专题：写操作可靠性与并发保护`
  - 新增 [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)，作为支付 / 转账幂等之后的写操作分级入口。
  - `WOG-1 写操作分级盘点` 已完成，结论见 [WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)。
  - [WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20) 已确认并完成首批实现：帖子 / 评论点赞关系唯一索引、历史重复数据清理、计数条件更新、点赞奖励触发边界和定向测试均已覆盖到代码批次。
  - [WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20) 已确认并完成首批实现；本批未扩展完整钱包、资产风控、浏览器通用 `sign`、字段级加密、安全会话、Redis 分布式锁平台或完整经济系统。
  - [WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20) 已确认并完成首批实现；本批未扩展完整经济系统、资产风控平台、Redis 分布式锁、Outbox 或通用奖励平台。
  - [WOG-5 管理覆盖类写入版本语义方案](/records/wog-5-management-write-version-semantics-plan-2026-06-20) 已确认并完成首批实现；本批未扩展通用审批流、所有 Console CRUD 版本化、Redis 锁、Outbox 或分布式事务平台。
  - [WOG-6 跨端幂等契约补齐方案](/records/wog-6-cross-client-idempotency-contract-plan-2026-06-20) 已确认并完成首批实现：Flutter 单商品购买已生成并提交 `shop:` 幂等键，失败后直接重试复用同一 key；本批未纳入 Flutter 转账、完整移动商城或服务端强制 key。
  - `WOG-1` 至 `WOG-6` 首轮写操作治理已阶段收束；当前后续产品 / 治理增量评审已完成，后续进入发布候选前回归线或按真实缺口定向回拉。
- `P3-10-D Web 信息流 / UI 结构整理`
  - 首日已完成 Web 默认入口、公开发现、论坛详情、公开文档详情、公开个人页、圈子、`/me` 与轻互动入口的结构整理和来源返回修正。
  - 首轮阶段级 Gateway PC / 移动收口复核已开始，并已修复公开文档详情重复 H1、正文内链公开 URL 口径、公开商城首页 / 列表 / 详情标题层级，以及公开榜单用户项 PublicId-only 跳转判断。
  - 第二至第四批已收口公开发现、公开文档、公开榜单、圈子、公开个人页、公开帖子状态卡、共享公开头部和 `/me` 最近访问的同类链接 / 来源语义问题：可导航动作提供真实 URL，普通点击保留当前壳层语义。
  - P3-10-D 合并前验证准备和 PR 合并判断已完成；用户明确本轮暂不创建 PR，后续只在恢复合并动作时复用 PR 准备记录。
  - 大页面结构、跨页面视觉体系或端点级视觉治理先更新 Pencil 和设计 / 说明文档；小范围状态、文案、按钮或行为等价修正不归入 UI 美化专题。
  - 不把首页瀑布流、个人圈子、推荐算法、联邦社交和完整 Flutter 承接一次性合并实施。
- `P3-10 后置安全治理评审：支付 / 转账幂等与重放边界`
  - 前端敏感日志脱敏、支付口令哈希升级和 [支付与转账幂等治理](/guide/payment-idempotency-governance) 首批代码 / 验证已完成。
  - 后续进入发布候选前回归线，不顺带启动浏览器通用 `sign`、字段级加密、安全会话、完整钱包、经济扩展或资产风控平台。
- `P3-10 阶段整理与自动化验证维护线`
  - B1-B10 已完成首批代码推进或阶段收束，阶段级真实联调 smoke 已完成；后续准备合并到 `master` 时再集中跑完整 baseline、identity、host runtime 和必要页面补验。
- `P3-10-B10 系统设置治理维护线`
  - 已推进设置定义注册表、默认值、覆盖值、风险等级、低风险编辑、恢复默认、修改原因 / 确认参数基础、变更审计历史、统一读取入口、帖子 / 评论 / 轻回应长度边界设置、帖子摘要长度设置、轻回应返回条数 / 冷却 / 去重窗口设置、神评 / 沙发稳定窗口 / 替换阈值设置、校验规则元数据和数字编辑控件约束；Console 不再把未注册 JSON 记录作为运营设置展示。
  - 当前开放 `Site.Branding.FaviconUrl`、`UserIdentity.DisplayName.MinLength`、`UserIdentity.DisplayName.MaxLength`、`UserIdentity.DisplayName.ChangeCooldownDays`、`UserIdentity.DisplayName.ChangeWindowDays`、`UserIdentity.DisplayName.ChangeWindowMaxCount`、`UserIdentity.PublicIndex.ReservedIndexes`、`UserIdentity.PublicIndex.VanityRules`、`Content.PostTitle.MinLength`、`Content.PostTitle.MaxLength`、`Content.PostBody.MinLength`、`Content.PostBody.MaxLength`、`Content.PostSummary.MaxLength`、`Comment.Body.MinLength`、`Comment.Body.MaxLength`、`Comment.QuickReply.MaxContentLength`、`Comment.QuickReply.DefaultTake`、`Comment.QuickReply.MaxTake`、`Comment.QuickReply.PerPostCooldownSeconds`、`Comment.QuickReply.DuplicateWindowSeconds`、`Comment.Highlight.StabilityWindowMinutes`、`Comment.Highlight.ReplacementMinLikeDelta`；Medium 设置必须填写原因并确认风险等级 / 设置键，High / Critical 仍不开放编辑。
  - 第九批代码侧验证已完成；真实页面 smoke 改为较大阶段推进完成或发布候选前集中执行。低 / 中风险首轮治理当前阶段收束，不继续把候选参数默认排成第十批。
  - 不把评论 typing 节流、神评触发评论数量门槛、内容发布频率限制、论坛编辑历史策略、部署密钥、宠物经济数值、高危资产 / 会话设置或基础设施配置直接搬进 Console；论坛编辑历史如需治理，应作为编辑权限 / 历史保留专题独立评审。
- `P3-10-B9 用户身份语义与公开索引维护线`
  - 首批代码、自动化验证和 Gateway PC / 移动页面补验已完成；`P3-12-B6` 会替换登录名和旧展示字段语义，上线前不再把 B9 阶段性结构脚本作为当前执行入口。
  - 后续只在发布候选、跨端承接或真实缺口暴露时回拉，不把 `DisplayName#PublicIndex` 替代 `PublicId` 路由，不启动数据库主键迁移、邮箱通知系统、ActivityPub / WebFinger 或 Console 高风险账号字段治理。
- `P3-10-B8 Radish 电子宠物维护线`
  - B8 已完成开发前冻结口径、Phase B 首批代码、Gateway PC / 移动首轮联调、首批体验补漏、后端契约测试补强、发布候选前批次级回归和合并前自动化总验证；本轮不继续合并动作，转入维护回拉。
  - 经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件和公开个人主页默认展示继续后置。
- `P3-10-B7 WebOS 功能迁移图收口`
  - `/notifications`、`/me`、`/messages` 三个纯 Web 私域复访入口已完成首批代码和 Gateway PC / 移动 smoke；后续只在新真实缺口、发布候选回归或 WebOS 保留入口阻断时回拉。
  - 论坛、公开详情、发现、圈子、榜单、文档阅读、商城公开浏览、个人状态、通知和消息已进入维护补漏；完整钱包已由 P3-12-B1 拆出资产流水正式路径，完整个人中心已进入 P3-12-B2。Flutter 系统通知继续后置，正式 Web 功能发现入口进入 P3-12-B5。
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

## 后续事项
- 先读取本页、[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)、[P3-12-D50 UI 实现缺口复盘与下一批实现排序](/records/p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02)，确认下一步仍在 `P3-12-D` UI 实现内。
- 第一顺位进入 `P3-12-D51 radish.client 私域 / 作者态移动任务流 UI 对齐首批`：覆盖 `/me` 系列、资产 / 订单 / 背包、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态的移动任务流 UI。
- D51 不新增业务 API、权限键、数据库结构或保存载荷；完成后按风险执行 `radish.client` 定向构建 / 类型检查和必要 PC / mobile 页面复核。
- 当前不直接创建 tag，不进入 `P3-12-E`、M15 测试或生产部署流程。

## 并行维护项

- 公开 head smoke、动态 sitemap、head snapshot 缓存与 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生产域名配置维护。
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护。
- Console 新增或改动页面继续优先复用 `@radish/ui` 组件、交互反馈和主题 token。

## 当前不做

- 不把维护观察继续当作唯一主线。
- 不等待真实用户反馈出现后才继续开发未完成功能。
- 不恢复 P3-11 PR 决策作为当前开发主线。
- 不创建本轮发布 tag，不进入 M15 测试 / 生产部署流程。
- 不把跳过发布误判为进入生产稳定运营。
- 不继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 不继续把移动 Web 公开页逐页打磨、Console 剩余 CSS / 历史页面筛查作为默认主线。
- 不在 P3-12 UI 设计与美化专题中绕过 Pencil 设计稿直接实现页面级或跨页面视觉改造。
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
- `P3-10` 细节统一写入 [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)；`P3-11` 收束事实统一写入 [P3-11 发布候选整备与轻量复访补齐](/planning/p3-11-release-candidate-light-revisit)；`P3-12` 范围统一写入 [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)。
