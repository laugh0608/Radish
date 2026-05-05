# 当前进行中

> 本页只维护 **当前主线、下一顺位、并行维护项、近期不做项**。
>
> 详细历史事实请看 [开发日志](/changelog/)；阶段拆分请看 [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)。

## 当前主线

- **阶段**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`Phase 2-3 Android MVP 第一轮完成后的多端路线收口`
- **当前阶段**：截至 `2026-05-04`，第一开发阶段已于 `2026-04-06` 通过 `v26.3.2-release` 完成真实发布收口；第二开发阶段现已正式启动。`Phase 2-1 社区深化第一批` 已完成论坛轻回应墙 Phase 1 与最小回流链路收口；`Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口，并转入稳定维护；`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论。随后完成 React 复用路线评估：Capacitor Android 因登录 / OIDC 与本机调试复杂度终止，不进入移动端产品化主线；Tauri 桌面壳命令级 spike 成立，但其合理定位是 `Tauri 壳 + WebOS 桌面工作台`，不是移动端替代方案，也不是原生 UI 重写路线。当前多端开发口径收束为三条线：Web 浏览器使用公开内容壳层，Android / iOS 安装包使用 Flutter 移动原生路线，Windows / macOS / Linux 安装包优先评估 Tauri + WebOS。
- **复核日期**：`2026-05-04`

## 当前执行入口

- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [前端多壳层策略](/frontend/shell-strategy)
- [论坛轻回应墙 Phase 1 设计](/features/forum-quick-reaction-wall)
- [论坛应用功能说明](/features/forum-features)
- [前端设计文档](/frontend/design)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [Flutter Android MVP RC 验收清单（2026-05-03）](/guide/flutter-android-mvp-rc-acceptance-checklist-2026-05-03)
- [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- [验证基线说明](/guide/validation-baseline)

## 当前已确认事实

- `v26.3.2-release` 已于 `2026-04-06` 完成首版真实发布，第一开发阶段正式结束
- 论坛、关系链、聊天、通知、商城、个人能力都已有一版基础，第二阶段不需要重新从零定义社区
- 发布、部署、回滚、验证和留痕仍然重要，但当前已降为并行维护线
- `radish.client` 当前仍是桌面 / WebOS 优先架构，仓库里没有独立命名、完整扩展中的 `MobileShell` 实现
- WebOS 当前继续保留，但角色已经明确收束为“桌面工作台”，不再承担所有场景唯一入口
- 仓库当前已建立 `Clients/radish.flutter` Flutter 客户端骨架并完成 Android MVP 第一轮；Flutter 当前作为 Android / iOS 移动安装包路线，不再默认承担 Windows / macOS / Linux 桌面安装包扩平台
- Windows / macOS / Linux 桌面安装包当前优先评估 `Tauri 壳 + WebOS 桌面工作台`；Tauri 负责系统窗口、系统浏览器 loopback 登录回跳、deep link 兼容、菜单、托盘、自动更新、文件系统与分发能力，WebOS 继续负责桌面 UI 与工作台业务体验
- Web 浏览器继续使用公开内容壳层，覆盖 PC 浏览器与移动浏览器响应式阅读，不承担完整工作台治理能力
- 论坛轻回应墙 `Phase 1` 当前已落下独立模型、独立接口、帖子详情页插入位、举报接入、配置化治理边界与首版前端展示闭环
- 论坛轻回应墙当前已不再是“设计是否成立”的问题，而是“基础链路已落地，是否能形成回流与复访闭环”的问题
- 轻回应相关的“个人内容回看 -> 跳回帖子详情”最小联动能力已落地到个人主页“我的轻回应”入口
- 论坛现有帖子 / 评论类通知当前已具备“从通知中心跳回帖子详情”的最小回跳能力
- 论坛评论精确定位最小闭环已落地：带 `commentId` 打开帖子详情后，当前可在评论加载完成后自动定位到目标评论，并给出一次性高亮提示
- 轻回应专属通知最小闭环已落地：他人在你的帖子下发布轻回应时，当前会通过通知中心给帖子作者发出一条最小论坛通知，并可跳回帖子详情
- `Phase 2-1 社区深化第一批` 当前已达到可收口状态，后续默认转入稳定维护，不再继续沿论坛回跳与轻回应细节扩张
- `Phase 2-2 移动 Web 形态` 当前正式进入主线，首批只冻结 forum 公开列表与帖子详情的移动访问形态
- forum 公开移动入口首批已落地：当前已新增 `/forum` 公开列表、`/forum/category/:categoryId` 公开分类页与 `/forum/post/:postId` 公开帖子详情入口，详情页首批承载正文、轻回应墙展示与评论阅读
- forum 公开移动入口当前已完成第一轮阅读体验收口：列表状态已支持 URL 回写与“详情 -> 返回列表”上下文保留，详情页已补齐只读阅读摘要
- forum 公开分类首批当前已完成首轮收口：分类直达会稳定回写到 `/forum/category/:categoryId`，公开分类页会补齐分类标题、简介与帖子数等只读上下文；旧 `/forum?category=...` 链接继续兼容并收口到新路径
- forum 公开标签首批当前已完成首轮收口：标签直达会稳定回写到 `/forum/tag/:tagSlug`，公开标签页会补齐标签名称、简介与帖子数等只读上下文；标签 slug 当前已统一收口为 canonical ASCII 口径，旧中文标签名链接进入后也会回写到新路径；从标签列表进入详情后，返回仍会保留原标签、排序与分页状态
- forum 公开结构化类型列表当前也已接入 `/forum/question`、`/forum/poll` 与 `/forum/lottery`，公开卡片与详情里的结构化徽标会统一回落到对应只读类型列表
- forum 公开搜索首批当前已接入 `/forum/search`：公开壳层会承载关键词检索、时间范围过滤、排序分页与“详情 -> 返回搜索结果”上下文，继续保持只读阅读边界
- 公开社区分发页首批当前已接入 `/discover`：首批会把 forum、docs、leaderboard 与 shop 的高价值公开入口收口到同一条移动分发路径中，继续保持只读边界，不回头扩桌面工作台语义
- 公开内容壳层当前已完成一轮“总入口 + 回流入口 + 来源返回”收口：forum / docs / leaderboard / shop / `u/:id` 顶部都已补齐回到 `/discover` 的轻入口；从 `/discover`、`/u/:id`、`/leaderboard` 与其他公开来源进入 forum / docs / shop 详情或公开个人页后，返回动作会优先回到原来源页，而不是退回专题默认列表
- 公开内容壳层首批当前已完成一轮批量稳定性验收：`/docs/search`、`/leaderboard`、`/shop/products` 与 `/discover` 的 replace 路由同步、越界参数规范化、来源返回与窄屏头部当前都已收口到统一口径，不再继续放大循环更新与回写抖动
- 公开内容壳层当前已形成共享头部品牌口径：forum / docs / discover / leaderboard / shop / `u/:id` 在窄屏下统一使用同一套品牌字、图标与按钮 token，不再出现“同一公开壳层、不同专题各自一套主题色”的割裂
- 公开社区分发页当前已继续补一轮“高价值只读阅读增强”：forum / docs / leaderboard / shop 四张摘要卡默认优先预览本页对应区块，并保留明确的“直接进入公开页”动作；摘要区自身也会同步展示 ready / loading / error / empty 状态
- `/discover` 当前已接通 `section` 级来源回记：摘要卡预览、区块内“查看全部”和内容跳转会记住最近一次公开分发区块，后续从各公开专题页顶部回到“社区发现”时，会优先回到原区块而不是页首
- forum 公开阅读首批当前已完成一轮人工验收：公开列表、帖子详情、返回上下文、只读状态页、评论分页与子评论分页阅读当前已达到可收口状态；通知/浏览回跳等跨入口联调项后续再按入口条件补验
- forum 公开阅读链路当前已进入“外部 ID 字符串化补洞”稳定性收口：公开直链、通知回跳、窗口参数与深链解析不再允许把对外对象 ID 当作前端 `number`
- forum 当前主线直接相关的外部 ID 补洞已继续收口到通知 `extData` 与个人浏览回跳：新生成的 forum 通知导航 JSON 已改为字符串 ID，浏览记录中的 forum 帖子回跳也不再依赖前端 `Number(...)` 解析
- forum 公开阅读链路当前已继续补齐“批量神评预览”这一类列表补查口径：公开列表、桌面列表与搜索结果读取批量神评返回值时，帖子 ID 键不再通过前端 `Number(...)` 转回数值映射，避免大整数帖子在只读阅读体验里出现预览命中丢失
- forum 主线相关通知 payload 当前已继续补齐抽奖通知这一处例外：`LotteryWon` 自行序列化的 `ExtData` 也已改为字符串 ID，不再让 `postId / lotteryId` 以内部 JSON number 形式残留
- forum 公开内容壳层当前仍明确保持“只读阅读”边界：不开放发帖、评论提交、投票提交、点赞或编辑等桌面交互动作
- 公开详情评论阅读链当前已补齐根评论分页与子评论分页阅读；移动端下“加载更多子评论”已在公开内容壳层接通
- forum 公开列表卡片当前已形成分层口径：手机窄屏继续以单列 `publicCompact` 阅读节奏为主，较宽窄屏与平板进入轻量双栏摘要布局
- forum 当前主线直接相关的外部 ID 字符串化残留已完成本轮收口：公开阅读、通知跳转、浏览回跳与深链 / 窗口参数这些入口当前都已不再依赖前端数值型外部 ID
- 文档阅读当前已进入公开内容壳层首批收口：`/docs` 与 `/docs/:slug` 已可直接进入公开文档目录与正文阅读，不再要求先进入桌面文档应用
- 文档公开阅读首批当前明确保持“只读阅读”边界：只承载目录浏览、关键词搜索、正文阅读、复制公开链接、返回浏览态与文档内链跳转，不开放编辑、发布、回收站、版本历史或其他桌面治理能力
- 文档公开阅读首批当前已完成一轮人工验收与问题收口：返回目录会保持原目录滚动位置，正文内链与旧 `__documents__` 文档链接也会继续落在公开 docs 壳层
- docs 公开搜索首批当前已接入 `/docs/search`：公开壳层会承载关键词检索、结果分页与“详情 -> 返回搜索结果”上下文，继续保持只读阅读边界
- docs 公开分享入口首批当前已接入详情阅读页：首批只提供“复制当前公开链接”的最小能力，不扩展海报、分享统计或其他治理语义
- 个人公开页首批当前已完成公开壳层收口：`/u/:id` 已可直接查看公开资料、公开统计、公开帖子与公开评论，公开 forum 中作者点击也已开始稳定回落到该入口
- 公开榜单首批当前已接入公开内容壳层：`/leaderboard` 与 `/leaderboard/:type` 已可直接进入公开榜单阅读；首批只承载榜单切换、分页、登录用户“我的排名”增强，以及用户榜单到公开个人页的跳转
- 公开榜单当前已完成默认经验榜单页（`/leaderboard`，兼容 `/leaderboard/experience` 收口）的“经验体系公开展示”说明区收口；若继续扩展，应优先补公开壳层的真实联调复核与高价值只读说明增强，而不是直接开放“我的等级 / 经验交易记录”这类强登录个人明细
- 公开榜单首批当前明确保持只读边界：商品榜单首批只做展示，不直接跳商品详情或商城购买链路；等级明细、商城、订单、背包与其他工作台动作继续留在桌面壳层
- 公开商城浏览首批当前已接入公开内容壳层：`/shop`、`/shop/products` 与 `/shop/product/:productId` 已可直接进入公开商城首页、商品列表与商品详情阅读
- 公开商城浏览首批当前明确保持只读边界：购买确认、订单、背包、权益使用、举报与其他“我的”能力继续留在桌面壳层；公开详情页只承载商品信息阅读与返回桌面工作台的导向
- 附件公开资源读取当前已补齐引用检查与异常自愈：仍被商品、榜单与社区分发等业务引用的附件不会再被后台清理误删；若附件记录状态异常，下载链路也会按业务引用自动恢复公开访问
- 当前仍保持增量迁移口径：`/` 与 `/desktop` 的根入口关系未调整，仓库里也仍没有真正实现的 `MobileShell`
- `Phase 2-3 Flutter 客户端 MVP` 已完成 Android MVP 第一轮 RC 验收；Flutter 后续定位收束为 Android / iOS 移动安装包路线，Android 转入产品化深化，iOS 单独评估，Windows / macOS / Linux 不再作为 Flutter 默认扩平台方向
- `Clients/radish.flutter` 当前已具备应用启动会话恢复三态：原生壳层会先经过启动恢复 gate，再进入匿名态或已恢复会话态；`profile` 当前也已不再通过页面级临时读取会话存储来判断登录边界
- Flutter 当前已补齐 Android 本地会话持久化、Access Token 过期判断与 refresh token 恢复回落；会话恢复不再停留在内存态占位
- Flutter forum 当前已不再停留在占位页：原生壳层已开始复用现有 `/api/v1/Post/GetList` 公开只读契约，支持匿名读取 forum feed、`latest / hottest` 排序、基础分页、加载态与错误态
- Flutter `discover / forum / docs / profile` 四个主 tab 当前已完成一轮 Android 真机人工联调；`discover -> docs` 与 `discover -> profile` 跳转链路当前可正常完成，期间暴露的 `Profile` 统计区与壳层状态区窄屏溢出也已完成修复和复测
- Flutter forum 当前已继续从公开只读列表推进到公开只读帖子详情：列表卡片可原生进入详情页，详情页当前承载正文、作者/分类/时间、基础统计与原生返回，继续保持不开放评论提交、点赞、投票和编辑治理
- Flutter `discover / docs / profile` 当前都已不再停留在占位页：原生壳层已分别接通公开分发摘要、公开文档阅读与公开个人资料读取，第二批真实业务接线已覆盖四个主 tab 的首批高价值只读页面
- Flutter forum 当前已继续从“帖子详情只读阅读”推进到“评论链路只读阅读”：根评论分页、子评论分页、作者 / 评论作者原生公开 profile 跳转都已落地，仍明确保持不开放评论提交、点赞、投票、编辑或登录治理
- Flutter forum 评论精确定位最小闭环当前也已落地：带 `commentId` 打开帖子详情后，原生端会先解析公开评论定位信息，再自动补载目标根评论页与子评论页并滚动到目标评论
- Flutter forum 外部详情 handoff 当前已完成最小收口：原生壳层已经可以从 shell 层直接透传 `postId + commentId` 打开 forum 详情，并复用现有评论定位链路落到目标评论，而不需要重新设计独立路由系统
- Flutter forum 外部 handoff 当前已继续先收口到 `public profile` 的最近帖子 / 最近评论：原生 profile 页不再自带独立详情跳转，而是统一复用共享 `ForumDetailHandoffTarget` 打开帖子详情；`postId / commentId` 继续保持字符串口径
- Flutter forum 真实来源接线当前已进一步从 discover 的最小演示入口推进到更接近真实来源的壳层 / 宿主层：Android 宿主当前已支持以启动 handoff 透传 forum 通知来源，原生壳层也已支持基于最近阅读记录续接 forum 浏览回跳
- Flutter forum detail 打开路径当前也已完成一轮统一回收：forum feed、public profile、notification 宿主 handoff 与 browse history 壳层续接当前都已统一接到同一套原生 handoff 目标，不再并存多套 detail 打开路径
- Flutter discover 当前已不再承担 `notification / browseHistory` 的长期伪来源入口：此前最小入口卡只作为过渡验证手段，现已在原生端收口回宿主 / 壳层真实接线
- Flutter 当前已补齐最小登录 UI、显式登出与 Android 浏览器 OIDC 回调闭环：原生壳层会通过系统浏览器发起登录，以 `radish://oidc/callback` 回跳并完成授权码换 Token；显式登出当前也已统一走 `/connect/endsession` + `radish://oidc/logout-complete`
- Flutter Android 最小登录连续性当前也已完成一轮收口：浏览器取消登录后，原生壳层会显式提示 `Sign-in needs attention`，壳层状态区已从 `AppBar` 收口到可换行状态条，`profile` 与 forum 详情来源下的登录发起当前也会在成功后自动续接回原始 tab / handoff 目标
- Flutter forum detail 当前也已补齐最小原地登录入口：匿名用户可直接在详情页发起登录，登录目标会以可持久化 follow-up 状态保留 `postId / commentId`，浏览器往返或壳层重建后仍能回到当前帖子 / 评论上下文
- Flutter forum detail 登录链路当前也已完成一轮 Android 真机人工联调：详情页登录、取消登录后的显式提示、重试登录与成功回到当前 detail 上下文这几条主路径当前均已通过
- Android MVP 当前人工验证范围已收口到真实可测链路：登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed、forum detail、评论阅读、评论分页与 detail 原地登录续接，以及已登录壳层最小 forum notification 回流都已纳入当前可验收面
- Android MVP 当前可测链路已完成一轮人工验收：登录和退出逻辑确认正常，forum 评论区显示问题已修复并在真机确认；最小 forum notification 回流也已完成真机人工联调
- Flutter 当前已补齐一个最小可测 forum notification 来源：已登录壳层会读取当前用户最新可跳 forum 的通知，解析 `voExtData` 中字符串化的 `postId / commentId`，并通过既有 `ForumDetailHandoffTarget(source: notification)` 打开原生 forum detail
- 最小 forum notification 回流当前已完成真机人工联调：`Forum notification` 入口回到 forum detail / 评论上下文的逻辑确认正常，系统通知栏推送与完整通知中心继续不纳入当前批次
- Flutter Android release 身份当前已从模板值收口到 `com.radish.client` / `Radish`，Kotlin 原生代码与平台单测 package 也已同步到正式包身份
- Flutter Android release signing 当前已完成安全边界收口：Gradle 会读取 `android/key.properties` 配置正式签名，未配置时回落到 debug signing 以保留本地 RC 构建能力；真实 `key.properties`、`.jks` 与 `.keystore` 不进入版本库
- Flutter Android release APK 当前已补齐 main manifest 的 `INTERNET` 权限，并完成一轮真机安装与本机 Gateway 联调复核；登录、基础读取与样式显示均已确认正常
- Flutter 环境切换能力当前已完成首轮收口：原生客户端可通过 `--dart-define=RADISH_ENVIRONMENT=...` 与 `--dart-define=RADISH_GATEWAY_BASE_URL=...` 指定本机 / 测试 / 正式 Gateway，API / Auth / Gateway 继续保持同源，不引入 Flutter 专属 BFF
- Android RC 签名配置诊断当前已完成首轮收口：`key.properties` 不存在时本地 release 构建继续回落 debug signing；一旦存在签名配置，Gradle 会检查必填字段、示例占位值与 keystore 文件存在性，并提供 `:app:checkReleaseSigningConfig` 作为外部分发前置检查入口；对应清单见 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)
- Flutter 第三批中文文案基线当前已完成：`discover / forum / docs / profile` 主 tab、壳层登录态、登录提示、forum / docs / profile / discover 的标题、空态与错误态已统一到中文主文案，不引入完整 i18n 框架
- Flutter 个人复访入口当前已产品化：`profile` 不再只展示开发态回流信息，而是承载最近 forum 阅读与公开主页复访入口，继续复用既有 handoff 和 follow-up 状态
- Flutter forum detail 当前已接入轻回应墙最小闭环：详情页按“正文 -> 轻回应 -> 评论区”展示，支持匿名读取最近轻回应、已登录发布一句轻回应，并复用详情页原地登录续接；删除、举报、完整评论提交、点赞、投票与通知中心仍不纳入当前批次
- Flutter 第三批 Android 真机复核当前已完成：中文主文案、个人复访入口与 forum detail 轻回应发布在真实 Gateway 下确认正常；同一帖子详情返回列表后再次点击无法进入的 handoff 去重问题已修复并补定向回归测试
- Flutter 第四批首个小闭环当前已落到 `profile` 我的轻回应回看：已登录且查看我的主页时会读取 `/api/v1/PostQuickReply/GetMine`，展示最近轻回应与原帖标题，并复用既有 forum detail handoff 回到原帖；公开主页不展示该个人区块；本轮 Android 真机复核确认正常
- Flutter 第四批“我的轻回应回看”已继续补齐分页复访：`profile` 中该区块当前支持继续加载更多轻回应，分页失败只在该区块内提示，不拖垮公开资料、公开帖子和公开评论读取；本轮 Android 真机复核已确认正常；仍不开放删除、举报、完整评论提交、点赞、投票或编辑治理
- Flutter 第四批“最近公开评论回看”已补齐分页复访：`profile` 中最近公开评论当前支持继续加载更多，并继续通过 `publicProfileComment` handoff 回到对应帖子与评论上下文；真机复核发现的“能打开帖子但停在顶部”问题已收口到 forum detail 目标评论异步渲染后的滚动重试，当前自动化验证与 Android 真机定位复核均已通过；分页失败只在评论区块内提示，不拖垮公开资料、公开帖子或我的轻回应区块；仍不开放评论提交、点赞、删除、举报或编辑治理
- Flutter 第四批“最近公开帖子回看”已补齐分页复访：`profile` 中最近公开帖子当前支持继续加载更多，并继续通过 `publicProfilePost` handoff 回到对应帖子详情；加载更多失败只在帖子区块内提示，不拖垮公开资料、最近公开评论或我的轻回应区块；三条 profile 复访路线从详情返回后均会回到 profile，不再落到论坛首页；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与 Android 真机复核；仍不开放发帖、评论提交、点赞、删除、举报或编辑治理
- Flutter 第四批“最近阅读上下文”已接入我的 `profile`：已登录态可在我的主页继续打开最近一次 forum 阅读目标，并以 `profileRecentBrowse` 来源保留 profile 返回上下文；当前已通过自动化验证与 Android 真机复核；该入口仍只承载单个最近阅读上下文，不扩展完整浏览历史中心、删除、清空、同步治理或多条记录列表
- Flutter 第五批“profile 最近阅读轻量多条列表”已完成收口：已登录态我的 `profile` 当前可展示最近多条 forum 阅读上下文，继续复用 `ForumDetailHandoffTarget` 与本地最近阅读语义；Android 本地持久化兼容旧单条记录，新列表最多保留 5 条并按 `postId + commentId` 去重；壳层状态条仍只显示最新一条“继续阅读论坛”；当前已通过自动化验证与 Android 真机复核；仍不扩展完整浏览历史中心、删除、清空、筛选、跨端同步治理或 docs / forum 混合时间线
- Flutter 第六批“forum detail 轻回应发布后局部体验补强”已完成收口：已登录用户发布轻回应成功后不刷新帖子详情或评论区，不打断当前阅读位置；新轻回应会即时前插到轻回应墙，并在轻回应区给出明确成功反馈；发布失败只在轻回应区局部提示，正文与评论阅读继续保留；仍复用现有 `PostQuickReply` 契约、`ForumDetailHandoffTarget`、detail 原地登录续接与最近阅读语义；当前已通过自动化验证与 Android 真机复核
- Flutter 第七批首个小闭环“docs 搜索体验增强”已完成收口：搜索关键词提交会先 trim 并同步输入框为实际搜索词；搜索、清除与翻页会回到结果顶部；从搜索结果打开内联文档详情后返回会恢复原搜索结果附近的滚动上下文；真机复核发现的 Android Back 退到桌面问题已收口为 docs 内联详情优先消费根层返回；当前已通过自动化验证与 Android 真机复核
- Android MVP 内测分发前置整理当前已完成文档与本机预检收口：内测 RC 候选链路成立，真实签名材料已由用户本机补齐并通过 `:app:checkReleaseSigningConfig`；个人开发阶段暂缓 testing Gateway、测试账号 / 测试数据、分发对象、反馈回收方式与真机安装复核，正式 release 包发布前再按 RC 清单补齐；对应记录见 [Flutter Android MVP 内测分发前置整理记录（2026-05-01）](/guide/flutter-android-internal-rc-prep-record-2026-05-01)
- Flutter 第八批首个小闭环“profile 最近文档轻量多条列表”已完成代码、自动化验证与收口复核：我的 `profile` 中最近文档从单条入口扩展为最多 5 条的轻量列表，按 `slug` 去重并保留最新打开优先；Android 本地持久化兼容旧单条 `docs_recent_document_target` 并新增列表存储；壳层状态条仍只展示最新一条“继续阅读文档”；真机 APK 安装与 testing Gateway 验收暂缓到正式 release 包发布前
- Flutter 第九批首个小闭环“profile 复访区块体验整理”已完成代码与自动化验证：`profile` 中最近文档、最近阅读、我的轻回应、最近公开帖子与最近公开评论区块已统一标题图标、空态和局部加载失败提示；我的主页在没有最近 forum / docs 记录时会在公开内容之后给出轻量空态，公开主页不会显示个人复访区块；仍不扩展完整浏览历史中心、删除、清空、跨端同步或新的后端 API
- Flutter 第十批首个小闭环“profile 区块顺序与信息密度微调”已完成代码与自动化验证：我的主页复访顺序收口为最近复访 / 最近文档 / 最近阅读 / 我的轻回应 / 最近公开帖子 / 最近公开评论；公开主页只保留最近公开帖子与最近公开评论；无最近 forum / docs 记录时使用紧凑“最近复访”空态卡，避免挤压公开帖子与评论回跳入口；仍不扩完整浏览历史中心或新的治理动作
- Flutter 第十一批首个小闭环“docs 详情只读上下文补强”已完成代码与自动化验证：docs 详情页当前补齐只读上下文、来源、公开地址与边界提示，长 slug 在详情芯片与上下文中保持窄屏受控显示，详情错误态会明确目标 `/docs/:slug` 与返回来源后重试口径；仍不扩目录树、编辑、发布、版本历史、完整 Markdown 引擎或新的后端 API
- Flutter 第十二批首个小闭环“forum detail 来源上下文与错误态补强”已完成代码与自动化验证：forum detail 正文卡当前补齐只读上下文、来源、公开地址、可选评论定位目标与边界提示；详情错误态会明确目标 `/forum/post/:postId`、来源与可选 `commentId`，评论定位失败会作为局部提示保留帖子正文阅读；长帖子地址与 meta 文本在窄屏保持受控显示；仍不扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票、编辑治理或新的后端 API
- Flutter 第十三批首个小闭环“discover 只读分发上下文补强”已完成代码与自动化验证：发现页当前补齐来源 `/discover`、公开只读分发边界和 forum / docs / profile 阅读入口上下文；摘要条目的长标题、长 slug、长 meta 与 chip 在窄屏保持受控显示；仍不扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票、编辑治理、购买、订单、背包或新的后端 API
- Flutter 第十四批首个小闭环“discover 聚合摘要局部失败降级”已完成代码与自动化验证：发现页 forum / docs / shop 三路摘要在原生聚合层独立保护，单一区块失败时记录局部 issue 并继续展示其他可用区块；仓储整体抛错的整页错误态继续保留；仍不新增后端 API、Flutter 专属 BFF、独立重试队列或跨端同步治理
- Flutter 第十五批首个小闭环“discover 刷新体验与局部 issue 清理复核”已完成代码与自动化验证：发现页已有摘要刷新时会保留上次可用内容并展示轻量刷新态；刷新失败只作为局部提示保留旧内容；刷新成功后局部 issue 按新 snapshot 清理；三路摘要全部降级时仍展示 issue notice，不误判为普通空态；仍不新增后端 API、Flutter 专属 BFF、独立重试队列、下拉刷新体系或跨端同步治理
- Flutter 第十六批首个小闭环“forum / docs 主列表刷新体验一致性”已完成代码与自动化验证：forum feed 与 docs 列表在已有内容刷新时会保留上次可用列表并展示轻量刷新态；刷新失败只作为局部提示保留旧内容；刷新成功后清理旧刷新失败提示；分页、搜索、排序切换仍保持原有加载语义；仍不新增后端 API、Flutter 专属 BFF、下拉刷新体系或跨端同步治理
- Flutter 第十七批首个小闭环“profile 主资料刷新体验一致性”已完成代码与自动化验证：profile 已有公开资料刷新时会保留上次可用资料、公开帖子、公开评论、我的轻回应与复访区块，并展示轻量刷新态；刷新失败只作为局部提示保留旧内容；刷新成功后清理旧刷新失败提示；加载更多局部失败逻辑保持不变；仍不新增后端 API、Flutter 专属 BFF、完整浏览历史、资料编辑、关注或治理能力
- Flutter 第十八批“刷新体验收口复核 / 模拟器验证清单整理”已完成文档口径收口：`Clients/radish.flutter/README.md` 与 [Flutter Android MVP 刷新体验开发阶段验证清单（2026-05-02）](/guide/flutter-android-mvp-refresh-experience-checklist-2026-05-02) 已明确 `discover / forum / docs / profile` 四个主 tab 的刷新中旧内容保留、刷新失败局部提示、刷新成功清理旧提示的开发阶段模拟器检查口径；不新增 Dart 业务代码，不替代正式 release 前真机 APK 安装验收
- Flutter 第十九批“profile 空态人称与文档口径复核”已完成代码与自动化验证：我的主页最近公开帖子 / 最近公开评论为空时改用第一人称空态，公开主页继续保留第三人称空态；同步修正第十七 / 十八批文档条目错位；不新增后端 API、Flutter 专属 BFF、资料编辑、关注、完整浏览历史或治理能力
- Flutter 第二十批“profile 公开主页长文本窄屏显示复核”已完成代码与自动化验证：公开资料标题、用户名、用户 ID、最近文档 slug、最近阅读 `postId + commentId`、公开帖子标题 / 摘要 / 分类与公开评论快照均已补受控显示；新增窄屏长文本 widget 测试；不新增后端 API、Flutter 专属 BFF、资料编辑、关注、完整浏览历史或治理能力
- Flutter 第二十一批“Android MVP RC 补验评估”已完成文档口径收口：[Flutter Android MVP RC 补验评估记录（2026-05-02）](/guide/flutter-android-mvp-rc-supplemental-assessment-2026-05-02) 已明确当前已具备的 RC 候选能力、开发阶段可先补的预检 / 材料整理，以及必须等 testing Gateway、release APK 与真机安装后才能完成的 release 前验收；`https://radishx.com` 正式域名临时 smoke 已纳入评估，Android Studio 虚拟机与 Android 真机均已确认帖子、文档和用户公开信息基础只读读取未见异常；不新增 Flutter / Android 业务代码，不改变 API、签名配置或 Gateway 切换逻辑
- Flutter 第二十二批“第八至第二十批验证索引整理”已完成文档口径收口：[Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/guide/flutter-android-mvp-validation-index-2026-05-02) 已按批次整理 Dart 定向测试、`smoke_test`、`flutter analyze`、Android JVM 单测、文档型验证与 release 前缺口；不新增 Flutter / Android 业务代码，不重新执行测试，不替代正式 release 前 testing Gateway、release APK、真机安装或外部分发回归
- Flutter 第二十三批“第十九 / 第二十批命令级回归记录补洞”已完成文档口径收口：新增 [profile 空态人称与文档口径复核记录（2026-05-02）](/guide/flutter-android-mvp-profile-empty-copy-record-2026-05-02) 与 [profile 公开主页长文本窄屏显示复核记录（2026-05-02）](/guide/flutter-android-mvp-profile-long-text-record-2026-05-02)；本轮补跑 `flutter test test/profile_page_test.dart test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`，均已通过；不新增 Flutter / Android 业务代码，不改变 API、签名配置或 Gateway 切换逻辑
- Android MVP RC 验收已完成：本轮接受 `https://radishx.com` 作为 RC 验收 Gateway，release APK 同等参数包已在小米 15S Pro / Android 16 上由 `test` 账号完成真机人工复核；本机命令级验证通过 `flutter analyze`、`flutter test`、`flutter test test/smoke_test.dart`、Android Studio JBR 下的 `.\gradlew.bat :app:testDebugUnitTest`、`.\gradlew.bat :app:checkReleaseSigningConfig`、`flutter build apk --release --dart-define=RADISH_ENVIRONMENT=production --dart-define=RADISH_GATEWAY_BASE_URL=https://radishx.com` 与 `git diff --check`；本轮未发现 `P0 / P1` 阻断，记录见 [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- Flutter 第四批“最近文档阅读”已接入原生壳层：`discover` 文档精选可直接打开 docs 详情，详情以来源 route 返回 discover / profile / docs 列表；我的 `profile` 可展示单个最近文档入口并继续打开公开文档详情；Android 侧已补本地最近文档 target 持久化；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与 Android 真机复核；仍不扩展文档搜索增强、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心或多条记录治理
- Flutter 第四批“docs 正文内链跳转”已接入原生详情：docs 正文中的 `/docs/:slug` 与 Markdown 文档链接可继续打开 Flutter 原生 docs 详情；列表内联详情会在当前 docs 页切换文档，discover / profile 等来源 route 会继续 push 新详情页并保留返回上一层详情的上下文；真机复核发现的长 slug 窄屏溢出与搜索详情返回退出问题已完成修复并复测通过，当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与定向 docs / smoke 回归；仍不扩展外部浏览器打开、完整 Markdown 引擎、目录树、编辑、发布、版本历史或完整浏览历史治理
- Flutter 第四批“docs 关键词搜索复访”已接入原生 docs 列表：docs tab 当前可通过现有 `/api/v1/Wiki/GetList?keyword=...` 搜索公开文档，搜索结果继续复用原生列表卡、分页、刷新与 docs 详情打开路径；真机复核发现的长 slug 窄屏溢出与搜索详情返回退出问题已完成修复并复测通过；调试态根层返回后重新打开卡启动页的问题已收口为 Android 根层 Back 退后台；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与定向 docs / smoke 回归；仍不扩展搜索建议、历史搜索、全文高亮、目录树、编辑、发布、版本历史或后端搜索改造
- Flutter 第四批“discover 论坛精选直达”已接入原生壳层：discover 中的论坛精选帖子可直接打开同一套原生 forum detail，并以 `discover` 来源保留返回发现页的上下文；真机复核发现的 discover “进入论坛 / 进入文档”列表根层 Back 直接退桌面问题已完成代码修复，当前这两个快捷入口会先返回 discover；当前已补 `discover_page_test.dart` 与 `smoke_test.dart` 定向覆盖，并通过 `flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` 与 Android 真机复核；仍不开放发帖、评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送

## 当前批次目标

1. **Android MVP 第一轮完成收口**
   - Android MVP RC 验收已给出 Go 结论，`Phase 2-3 Android MVP` 当前可标记为“第一轮完成”
   - 后续不再默认追加第 `24` 批及以后低增益 Flutter 微体验修补
2. **下一阶段优先级评估**
   - [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation) 已收口到三端分工方案：Web 浏览器公开内容壳层、Android / iOS Flutter 移动安装包、Windows / macOS / Linux Tauri + WebOS 桌面安装包
   - Capacitor Android 已完成 `/docs` 与本机 Gateway 调试链路验证，但登录 / OIDC 回调评估因本机调试复杂度、Auth secure cookie、Android WebView 证书、`adb reverse`、runtime config 与 deep link 原生桥耦合成本过高而终止；相关临时代码与 Auth 开发态配置已回滚，Capacitor 不进入当前移动端产品化主线
   - Tauri 桌面壳已完成首轮命令级 spike、第二轮人工验收与 Windows NSIS installer 首轮验证：`radish.client` 可复用 React / Vite `dist`，Tauri 壳层可接入窗口生命周期、系统浏览器 loopback 登录回跳、`radish://` deep link 兼容、Windows release exe 与 NSIS installer 构建；默认入口已从 `/docs` 切到 `/desktop`，GUI 启动、WebOS 桌面布局、登录 / 登出浏览器回跳、installer 安装、启动、普通用户卸载与同身份覆盖安装测试后暂未发现问题；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复；当前本机普通用户安装未出现“未知发布者 / SmartScreen”提示，公开分发后仍需按下载来源、签名、信誉与系统策略复核；管理员安装后用普通权限卸载可能残留安装文件，当前归类为权限上下文不一致风险；后续若继续桌面端，应转入正式产品身份、签名、自动更新、deep link 协议注册与分发链路评估
   - 下一步优先在 Android 内测产品化深化与 Tauri + WebOS 桌面安装包第二轮评估之间选择，不再按“Flutter 扩所有平台”或“React WebView 统一所有端”规划
3. **维护线继续保留**
   - 若后续发现 `P0 / P1` 阻断，只按阻断项定点修复
   - 系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票、编辑治理、Flutter 专属 BFF、Tauri 桌面分发仍需重新评估后再进入建设

## 下一顺位

- `Phase 2-3` Android MVP 第一轮完成后的下一阶段评估
  - 当前多端路线已收口为三端分工：Web 公开内容壳层、Flutter 移动安装包、Tauri + WebOS 桌面安装包
  - Flutter 当前执行面只保留 Android MVP 完成线；iOS 后续按移动端价值单独评估，不启动 Windows / macOS / Linux Flutter 扩平台
  - Android 深化若进入执行，应以测试对象、反馈回收、已知问题列表、版本说明和发布留痕为主，不默认扩完整通知中心、系统推送、发帖、完整评论提交、点赞、投票或编辑治理
  - Windows / macOS / Linux 若进入执行，应基于已通过的 `Tauri + WebOS` GUI / 登录回跳人工验收、Windows NSIS installer 首轮验证与本机 SmartScreen 观察，继续评估正式产品身份、签名、自动更新、deep link 协议注册与分发方式，不与 Android 已完成 MVP 混成一批

- `Phase 2-2` 稳定维护项
  - 公开内容壳层保留必要联调复核与问题修复，但不再继续新增公开入口或细节增强

## 并行维护项

- `M14` 宿主运行与最小可观测性基线：继续作为部署 / 宿主问题的默认回归入口
- `M15` 最小交付与部署基线：继续作为发布、部署、发布后最小复核与回滚的默认入口
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`：继续维持统一执行面
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力：默认转入稳定维护

## 文档更新规则

- 没有主线切换、优先级变化或新的关键事实，不改本页
- 功能开发细节不在本页展开，统一回到对应专题文档
- 若移动 Web、Flutter 移动端或 Tauri + WebOS 桌面端成为正式主线，必须先同步：
  - [开发路线图](/development-plan)
  - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
  - [当前进行中](/planning/current)
  - [前端多壳层策略](/frontend/shell-strategy)

## 当前不做

- 继续沿 `Phase 2-1` 扩张论坛回跳、轻回应或通知尾项
- 在 Android MVP 第一轮完成后未经重新评估主动开启 `第 24 批` 及以后低增益 Flutter 体验微调
- 未经单独评估启动 Flutter iOS 产品化工程
- 把 Windows / macOS / Linux 作为 Flutter 默认扩平台方向
- 继续扩大 Capacitor Android 登录态或移动端产品化能力
- 把 Tauri 当作原生 UI 重写路线
- 把 `/docs` 公开阅读页作为 Tauri 桌面安装包正式默认入口
- “移动版 WebOS”
- 在 `Phase 2-3` 第一批以 Flutter 同时铺 Windows / Linux
- 在 `Phase 2-3` 第一批复刻桌面工作台
- `Gateway & BFF` 深化
- `Console-ext Phase 2+`
- 开放平台第三方接入 / SDK
- 邮件通知系统
- 完整 `PWA / 离线能力 / 推送闭环`
- 完整 Playwright / E2E 平台
- 完整可观测性平台或大而全运维平台
