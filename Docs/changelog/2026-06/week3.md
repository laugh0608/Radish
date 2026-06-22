# 2026 年 6 月第 3 周开发日志

## 2026-06-15

- `P3-10-B8` 电子宠物 Phase B 首批代码已继续补体验：`/pet` 新增状态洞察、状态等级、照顾动作可用 / 冷却展示、成功反馈和属性变化提示，帮助用户明确当前该做什么与动作执行结果。
- Gateway 体验补漏联调已覆盖 `https://localhost:5000/pet` 的 PC `1920x1080 @ DPR 2` 与移动 `390x844`；移动 DPR 受 Browser 工具限制实际为 `1`，不写作高 DPR 完整通过。
- `/pet` 真实操作已验证一次 `清洁`：页面出现“清洁完成”、成长和属性变化反馈，清洁动作进入冷却，最近照顾流水刷新。
- 宠物后端契约测试完成补强：覆盖重复领取、只读查询不写入、照顾写流水、幂等重放、每日上限、冷却、状态上下限、动作状态和未领取日志空态。
- Phase B 数值规则完成收口判断：当前继续以 `PetService.CareRules` 作为照顾规则单点，不提前引入 Console 配置 UI、经济配置表、商城物品或社区任务奖励；Phase C 前再统一评审配置来源、经济和治理口径。
- 本批记录已补 [P3-10-B8 电子宠物 Phase B 契约与体验补漏记录](/records/p3-10-b8-pet-phase-b-contract-record-2026-06-15)，专题页同步当前结论。
- `P3-10-B8` 发布候选前批次级回归和合并前自动化总验证已完成：后端定向 / 完整测试、`radish.client` 测试 / 类型检查 / 构建、身份契约、结构同步、`validate:baseline`、`validate:identity`、`validate:baseline:host` 和 `git diff --check` 均通过；后续不继续启动经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件或公开个人主页默认展示。
- 本轮不合并 B8，直接进入 `P3-10-B9 用户身份语义与公开索引`。首批已落地 `User.PublicIndex`、公开句柄 `DisplayName#PublicIndex`、注册登录名 / 邮箱规范化、登录名或邮箱登录、公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户展示和 PostgreSQL / DbMigrate 迁移入口。
- B9 自动化验证已通过：后端完整测试 `443` 个、B9 定向测试、`radish.client` / `radish.console` 构建、`validate:identity`、`validate:baseline:quick`、`dotnet build Radish.slnx -c Debug`、`git diff --check` 与 repo hygiene 均通过。
- B9 Gateway 运行态页面 smoke 当前未闭合：提权后 `check:host-runtime` 仍显示 API/Auth `5100 / 5200` 未监听，`5000` 由 macOS `ControlCe` 占用并超时；待宿主恢复后补 PC / 移动页面验收。
- 收工前补 [2026-06-15 收工回顾与明日事项](/records/daily-handoff-2026-06-15)：明日先补 B9 运行态验收；若无阻断，再进入 `P3-10-B10 系统设置治理` 的方案与首批低风险能力评审。

## 2026-06-16

- 已完成圈子关系链公开句柄展示对齐和公开标识查询公开资料内容补点，B9 用户身份语义进入维护线；后续只在发布候选、跨端承接或真实缺口暴露时回拉。
- Console 反馈上下文完成修复，避免动态调用 `message` 等 AntD 反馈 API 时脱离 `App` 上下文。
- `P3-10-B10` 系统设置治理首批完成：系统设置从自由 key-value 收敛为代码级设置定义、默认值、覆盖值、风险等级、生效方式和低风险恢复默认入口；Console 只展示已注册设置，旧未注册 JSON 记录不作为运营设置展示。
- `P3-10-B10` 第二批完成：新增系统设置专用变更审计、修改原因 / 确认参数基础和 Console 历史查看入口；`Site.Branding.FaviconUrl` 仍作为首个低风险可编辑示例。
- `P3-10-B10` 第三批完成：新增 `ISystemSettingProvider` 统一读取入口，注册 `Content.PostTitle.MinLength`、`Content.PostBody.MinLength`、`Comment.Body.MinLength`，并将帖子 / 评论发布与编辑路径接入动态最小长度校验。
- Medium 设置编辑已开放受控写入：后端要求修改原因、确认风险等级和确认设置键，High / Critical 仍拒绝编辑；Console 编辑弹窗已补风险提示。
- 今日验证：`dotnet test Radish.Api.Tests` 通过 `456` 个测试，`npm run build --workspace=radish.console` 通过，`git diff --check` 通过。
- B10 Gateway 页面联调未闭合：当前本机 `https://localhost:5000` 不可达，`5000` 被 macOS `ControlCe` 占用，`3000 / 3100 / 5100 / 5200` 未发现 Radish 监听进程；待运行态恢复后补 PC / 移动页面验收。

## 2026-06-17

- `P3-10-B10` 第四批完成设置校验规则可视化：后端从设置定义暴露数值范围、整数约束和影响范围摘要，Console 数字控件按规则约束输入并展示规则说明；定向测试、`radish.console` 构建和 Gateway PC / 移动页面补验通过。
- `P3-10-B10` 第五批完成内容发布上限设置治理：帖子标题最大长度、正文最大长度、摘要最大长度和评论最大长度接入 `ISystemSettingProvider`，发布 / 编辑路径统一读取上下限并与 DTO / 实体硬边界对齐。
- 完成 SQLite 仓储操作串行化修复，解决系统设置连续测试中 SQLite 连接并发导致的间歇失败；后续仍保持 PostgreSQL 作为上线审核重点。
- Console 系统设置移动端表格完成优化，移动视图下设置键、当前值、风险等级、编辑入口和历史入口可读性提升。
- `P3-10-B10` 第六批完成轻回应内容最大长度设置：`Comment.QuickReply.MaxContentLength` 接入轻回应发布路径，旧 `ForumQuickReply.MaxContentLength` 宿主配置入口移除。
- `P3-10-B10` 第七批完成账号身份长度设置：登录名 / 展示名最小与最大长度接入 Auth 注册和 API 个人资料展示名校验；邮箱、登录凭证、高风险账号字段和 Console 账号变更动作仍不开放。
- `P3-10-B10` 第八批完成轻回应剩余运营参数：默认返回条数、最大返回条数、单帖冷却秒数和重复内容窗口秒数接入轻回应列表 / 发布路径；`ForumQuickReply.Enable` 功能开关仍保留在宿主配置，不开放到 Console。
- `P3-10-B10` 第九批完成神评 / 沙发稳定性运营参数：稳定窗口和替换阈值接入实时重算路径；任务启停、调度、扫描窗口、触发评论数量门槛和奖励数值仍不开放。
- 系统设置治理低 / 中风险首轮阶段收束：当前开放 19 个设置，覆盖品牌 favicon、账号身份长度、内容发布长度、轻回应运营参数、神评 / 沙发稳定窗口和替换阈值；评论 typing 节流、内容发布频率限制、基础设施、安全会话、资产、奖励和高风险账号字段转入后续独立评审或维护观察。
- 阶段级真实联调 smoke 已补：Gateway / Api / Auth 健康检查通过，论坛详情 / 文档详情 public head smoke 通过，PC `1920x1080` 与移动 `390x844` 视图覆盖 `/discover`、公开帖子详情、公开文档详情、`/leaderboard`、`/shop`、`/circle` 和 `/console/` 授权确认页；DPR 与 Console 深层设置页保留限制说明。
- 当前主线转入 `P3-10-D Web 信息流 / UI 结构整理`：明日优先复核 `/discover`、公开帖子详情、公开文档详情、公开个人页、`/circle`、`/me` 和轻互动入口的信息结构、视觉层级、真实内容密度、返回语义与验证入口；不一次性启动推荐算法、联邦社交、完整 Flutter 承接、完整 WebOS 迁移、系统设置扩面、经济扩展、完整聊天、完整钱包或 P3-8-D 重复筛查。
- 收工前补 [2026-06-17 收工回顾与明日事项](/records/daily-handoff-2026-06-17) 与 [P3-10 阶段真实联调 smoke 记录](/records/p3-10-stage-smoke-record-2026-06-17)，同步 current、development-plan、P3-10 专题、系统设置治理专题和开发日志入口。

## 2026-06-18

- `P3-10-D` 首日公开页整理完成：`/discover` 收敛公开信息入口，公开帖子详情优化阅读顺序与标题层级，公开文档详情补访问 / 文档 / 时间线元信息分组，公开个人页补内容项主次与入口动作。
- `/circle` 与 `/me` 完成纯 Web 私域入口的信息结构整理，明确公开主页、个人圈子、个人状态与 WebOS 保留入口的边界。
- 公开详情轻互动入口完成结构整理，轻回应、根评论、登录参与和来源返回语义继续沿公开壳层契约处理。
- 公开壳层阶段词已清理，公开论坛列表 / 搜索 / 标签 / 类型流、榜单、商城和个人页不再展示内部批次标识。
- 公开详情 canonical / 规范化跳转已保留来源返回状态，避免从发现、圈子或公开个人页进入详情后被规范化替换覆盖返回语义。
- 今日验证按开发中风险分层执行：`radish.client` 构建、`git diff --check` 和 `npm run check:repo-hygiene:changed` 已覆盖代码批次；真实页面 smoke 未执行，留到阶段收口、准备合并或用户确认前后端已启动后集中覆盖。
- 收工前补 [2026-06-18 收工回顾与明日事项](/records/daily-handoff-2026-06-18)，同步 current、P3-10 专题、月 / 周 / 年开发日志和记录索引。

## 2026-06-19

- `P3-10-D` 阶段收束完成：公开文档、公开商城、公开榜单、公开个人页、公开论坛浏览、公开壳层头部和 `/me` 最近访问来源语义完成多批链接 / 标识 / 来源契约治理；公开用户路由 ID 回退收敛为字符串契约。
- `P3-10-D` 合并前验证与 PR 准备判断完成：`validate:baseline`、`validate:identity`、`check:host-runtime -- --details`、`radish.client` build、Gateway PC / 移动真实页面复核和 `validate:ci -- --report` 已通过；用户明确暂不创建 PR，D 批次转维护回拉。
- 前端敏感日志治理完成：`radish.client`、`radish.console` 与 `@radish/http` 统一敏感字段脱敏，三端测试、type-check、`validate:baseline:quick`、`validate:ci`、lint 和仓库卫生检查均已通过。
- 支付口令哈希升级完成：新支付口令写入 Argon2id v2，历史 SHA256 支付口令验证成功后自动升级，null 旧口令仍要求重置。
- 支付 / 转账幂等治理完成首批代码：商城购买与萝卜币转账接入 `idempotencyKey`、请求摘要绑定和终态响应重放；新增幂等记录实体、服务、测试和 PostgreSQL 差异 SQL。
- warning 清理完成：SQLitePCLRaw 安全版本、`CoinController` XML 参数说明、Console 兼容权限扫描豁免和 Forum 评论 chunk warning 已收口，后端构建、client build、`validate:ci`、NuGet vulnerability scan 和仓库卫生检查均已通过。
- 新增 [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)，作为支付 / 转账幂等之后的治理入口；明日第一顺位是 `WOG-1 写操作分级盘点`，先输出写入口矩阵、候选排序和不做范围，再决定代码批次。
- 收工前补 [2026-06-19 收工回顾与明日事项](/records/daily-handoff-2026-06-19)，同步 current、P3-10 专题、路线图、月 / 周 / 年开发日志和记录索引。

## 2026-06-20

- `WOG-1` 写操作分级盘点完成，输出资产 / 库存、奖励 / 玩法、内容互动、管理治理、通知 / 消息写入口矩阵和候选排序。
- `WOG-2` 至 `WOG-6` 首轮写操作治理完成：论坛点赞关系与计数一致性、商城订单背包 / 权益发放一致性、奖励业务键唯一性、管理覆盖版本语义和 Flutter 单商品购买幂等 key 承接均已落地。
- 新增并推进 [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)：Web 发帖、评论、回答、帖子编辑和评论编辑已接入 `clientSubmissionId` 与 `ContentSubmissionRecord`，创建类写入口补短窗口内容指纹和频率限制，编辑无变化不写历史、不递增 `EditCount`。
- Flutter 原生论坛写入口已承接当前已有能力：纯文本发帖、根评论 / 回复和问答回答均生成并复用 `clientSubmissionId`；失败重试复用同一提交意图 key，成功、对象变化、草稿变化或账号变化后生成新 key。
- 今日验证按影响面完成：WOG 相关后端 / Flutter 批次测试、`flutter analyze`、`flutter test`、`dotnet test Radish.Api.Tests`、相关构建、`git diff --check` 和仓库卫生检查按批次通过；未启动服务，未执行真实 smoke。
- 收工前补 [2026-06-20 收工回顾与明日事项](/records/daily-handoff-2026-06-20)，同步 current、P3-10 专题、路线图、写操作治理说明、月 / 周 / 年开发日志和记录索引；明日先评审 Flutter 帖子编辑 / 评论编辑入口是否新增产品能力承接，或切换到下一批 P3-10 产品 / 治理候选。

## 2026-06-21

- Flutter 作者编辑承接完成：原生端已接入作者帖子正文编辑和作者根评论编辑的 `clientSubmissionId` 复用规则，Flutter 子评论编辑和回答编辑经评审不进入下一批默认代码。
- `P3-10` 阶段收束准备完成：整理完整批次范围、验证结论和剩余风险；当前暂缓 PR，不创建 tag，不进入发布部署流程。
- `P3-11` 发布候选整备完成：发布候选验收矩阵、轻量复访缺口只读审计和阶段收束决策均已记录，`P3-11-C` 定向回修未触发。
- `P3-12 Web 完全化与 WebOS 收束` 启动并完成 `P3-12-A` 功能资产盘点，明确正式 Web 主路径优先，`/desktop` 退为历史兼容入口，Flutter 暂时后移。
- `P3-12-B1` 账户资产与商城交易 Web 化完成首批代码：正式 Web 资产流水、订单、库存、商城私域入口、公开购买登录回流和订单成功回流均已接入。
- `P3-12-C1` B1 直接残留首轮清理完成：公开商城、公开账号动作和订单通知中的旧 `/desktop` 语义已收口，后续只在验收或新增阻断命中时回拉。
- `P3-12-B2` 完整个人中心 Web 化首批代码完成：`/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 已接入，个人中心内容和浏览历史列表补齐正式公开链接语义。
- `P3-12-B3` 论坛作者态 Web 化首批代码完成：新增 `/forum/compose`，扩展 `/forum/post/:postId?intent=answer|edit|history`，发帖、回答、采纳、作者编辑和编辑历史查看进入正式 Web 路由与登录回流。
- 今日验证按批次完成：P3-12-B2 / B3 覆盖定向 Node 契约测试、`radish.client` type-check / build、`git diff --check` 和仓库卫生检查；未启动 dev server，未执行真实 Gateway smoke。
- 收工前补 [2026-06-21 收工回顾与明日事项](/records/daily-handoff-2026-06-21)，同步 current、路线图、P3-12 专题、月 / 周 / 年开发日志和记录索引；明日优先推进 B3 小阶段验收准备，若暂不做运行态验收，则进入 `P3-12-B4` 文档作者态归属裁决。
