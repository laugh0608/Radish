# 前端多壳层策略

> 状态：执行中
>
> 最后更新：2026-07-12（Asia/Shanghai）
>
> 本页只保留壳层分工、归属判断和当前边界。公开入口落地流水、RC 记录和批次细节不写入本页。

## 结论

Radish 的前端正式主线收敛为 **纯 Web**。

纯 Web 承担浏览器默认入口、公开访问、登录后使用、SEO、分享以及 PC / 移动浏览器体验。Flutter 已完成的 Android MVP 保留为条件式维护资产，不继续默认扩展；Tauri 冻结为桌面实验资产。WebOS 不再作为未来功能扩展方向，只保留在 `/desktop` 作为历史桌面工作台入口。

当前前端策略固定为 **单一正式主线 + 条件维护 + 冻结实验**：

| 壳层 | 主要用户 | 主要设备 | 主要任务 | 交互形态 |
| --- | --- | --- | --- | --- |
| 纯 Web 壳层 | 游客、轻登录用户、PC / 移动浏览器用户 | 手机 / 平板 / 桌面浏览器 | 浏览帖子、查看详情、访问公开主页、阅读文档、轻互动、逐步承接登录后高价值功能 | 单任务、直达内容、响应式 Web |
| Flutter 条件维护壳层 | 已有 Android MVP 验证用户 | Android 已完成 MVP，iOS 不主动扩展 | 维护已落地主路径；只有原生价值被真实使用证据证明后才恢复开发 | 原生导航、原生手势、原生生命周期 |
| WebOS 保留入口 | 历史桌面工作台用户 | 桌面浏览器 | 继续访问既有桌面工作台能力，承接迁移过渡期 | Dock、窗口系统、工作台 |
| Tauri 冻结实验壳层 | 暂无正式目标用户 | Windows / macOS / Linux | 仅保留既有验证资产；出现明确桌面原生需求后再单独立项 | Tauri 系统壳 + Web UI |

这不是立即删除 WebOS，也不是要求把 WebOS 全部功能一次性迁移到纯 Web。WebOS 从“桌面主线”收缩为“保留入口和迁移来源”；后续新功能默认不进入 WebOS，已有能力按真实使用价值选择迁移、保留、后置或重做。

## 当前多端方向

1. **纯 Web 壳层**
   - 面向匿名访问、轻登录使用、外链分享、搜索流量、PC 浏览器与移动浏览器
   - 已完成 forum / docs / `u/:identifier` / leaderboard / shop / discover 首批公开入口收口；`/discover` 当前是公开内容流，公开 docs 详情已补只读元信息分组，公开个人页内容项已补明确打开动作，`/u/:identifier` 当前优先使用 `User.PublicId`，`/circle` 作为登录态关系链复访入口接入公开详情来源返回
   - 已完成 `/notifications`、`/circle`、`/me`、`/shop/*` 私域、`/messages`、`/pet` 等登录态私域入口，分别承接通知列表 / 目标分流、关系链复访、完整个人中心子路径、商城购买 / 订单 / 背包、会话 / 消息定位、电子宠物领取与照顾
   - 已完成 `/forum/compose` 和 `/forum/post/:postId?intent=answer|edit|history` 首批论坛作者态 Web 路径，发帖、问答回答 / 采纳、作者帖子编辑和帖子编辑历史查看不再必须进入 WebOS 论坛工作台
   - 已完成 `/workbench` 正式 Web 功能地图，用于让普通浏览器用户发现公开浏览、登录态私域、后台治理和 `/desktop` 历史桌面入口
   - 已按 `F02` 共享壳层契约落地 `radish.client` 代码：`components/web-shell/WebShellHeader` 承接 public / private PC header 与移动底栏，`WebStateSlot` 承接加载、空态、错误、权限和登录恢复状态；public / private 移动任务流已按真实内容优先、状态槽和底部留白规则完成首批收口
   - 后续作为 Web 浏览器默认入口继续补齐响应式体验、轻交互和登录后高价值链路
2. **WebOS 保留入口**
   - 仅面向已有 `/desktop` 桌面工作台能力和迁移过渡
   - 不再作为新增功能默认承载层
   - 既有聊天、通知、个人中心、创作、继续使用等能力按价值逐步迁移到纯 Web 或 Flutter
3. **移动原生客户端壳层**
   - Flutter Android MVP 已完成第一轮 RC 验收
   - 当前已补齐榜单、轻量 forum 通知列表、公开主页来源返回、forum 评论发布 / 回复、纯文本发帖、公开商城列表与详情、登录态单商品购买、订单 / 背包 / 钱包回流、公开详情链接复制和 docs 原生阅读内链等高价值移动主路径
   - 当前只修复阻断既有 MVP 使用、认证兼容或安全边界的问题，不默认补新页面、追平 Web 功能或扩 iOS
   - 只有受控试用证明系统推送、后台任务、商店分发、原生生命周期或原生交互能解决明确高价值问题时，才重新立项
   - 不把 Windows / macOS / Linux 作为 Flutter 默认扩平台目标
4. **PC 增强壳层**
   - Tauri 当前冻结，不进入日常开发、候选 CI 必需矩阵、签名或分发流程
   - Tauri 只负责系统窗口、系统浏览器 loopback 登录回跳、deep link 兼容、系统通知、托盘、文件选择、本地缓存、外链打开等增强能力
   - UI 默认承载纯 Web，不再绑定 WebOS 桌面工作台
   - 只有用户需求明确依赖 Web 难以提供的托盘、文件系统、协议唤起、自动启动等桌面原生能力，并且具备签名、更新和分发维护预算时，才允许重启

## 功能归属

### 优先进入纯 Web 壳层

- 论坛列表、帖子详情、公开评论阅读
- 轻回应墙与轻量互动展示
- 公开个人主页
- 文档阅读与搜索
- 公开榜单、公开商城浏览、社区发现分发
- 登录后轻量个人链路、通知复访、会话 / 消息定位、个人状态 / 成长 / 资产流水、个人内容 / 浏览历史 / 附件 / 经验详情、购买 / 订单 / 背包等适合浏览器直达的功能
- 论坛作者态中的发帖、问答回答、作者编辑和编辑历史查看

### WebOS 仅保留和迁移

- 聊天室
- 通知中心完整管理
- 个人中心历史窗口能力
- 桌面首页“继续使用”复访入口，例如最近应用、最近浏览、我的轻回应
- 复杂创作器、更重的编辑体验和工作台级批量操作
- 多窗口切换与工作台级使用场景

上述能力不代表继续在 WebOS 扩展。它们只是现有保留项，后续若仍有长期价值，应优先迁移到纯 Web；只有明确依赖原生能力时才条件承接到 Flutter。

### Flutter 已有资产与重启条件

以下条目描述已经落地的移动 MVP 资产，不再构成默认新增功能清单：

- 移动原生导航、Android Back 来源返回、系统浏览器 OIDC 回跳和本地会话恢复
- forum / docs / shop 等公开详情的原生阅读、公开链接展示与复制
- forum detail 中适合移动原生上下文的评论发布 / 回复、轻回应发布和登录回流
- 已登录态最近少量 forum 通知列表到帖子 / 评论上下文的回流
- 发现页、榜单、公开主页、商城列表与详情之间的移动来源返回
- 登录态“我的”页中的基础个人资料编辑、最近访问、订单 / 背包、胡萝卜资产和经验记录等移动私域复访入口
- 后续移动底部栏按 `发现 / 论坛 / 聊天 / 更多 / 我的` 收敛：`聊天` 只承接会话、私聊和频道，`通知` 不进入底部主导航而作为右上角动作或通知中心入口，`更多` 承接文档、商城、榜单、规则和兼容入口，`我的` 聚焦个人资料、收藏、订单、背包、钱包和设置

Flutter 当前只维护上述已落地主路径，不复刻 WebOS 工作台，也不扩完整通知中心、完整商城工作台、系统分享 SDK、发帖创作器、完整账号设置或桌面治理能力。若未来满足重启条件，移动端信息架构仍不要求 Web / PC / Tauri 照搬；跨端一致性优先体现在任务归属、入口命名、登录恢复、返回语义和错误状态上。

### 按价值再判断是否双端承载

- 商城购买与登录后链路
- 排行榜 / 经验展示
- 萝卜坑的部分公开 / 轻量能力
- 更深的社区分发页

原则不是“所有能力都双端同步”，而是“所有新能力先在纯 Web 中成立；只有已证明的原生价值才进入 Flutter”。WebOS 与 Tauri 都不再作为新功能候选默认项。

## 路由与迁移策略

产品端与 Console 的跨应用真实 URL、`returnTo / backTo` 分工、OIDC 往返保持、UI 过渡和原生外部承接统一遵循 [Client 与 Console 跨应用导航契约](/frontend/client-console-navigation-contract)。

- 当前保持增量迁移，不立即删除 WebOS 路由结构
- 普通浏览器根路径 `/` 已切向纯 Web 公开分发页 `/discover`；Tauri 当前仍保留 `/desktop`；历史 `?demo` 认证测试页已移除，`/oidc/callback` 保留为独立正式回调入口
- `/workbench` 固定作为正式 Web “更多”功能地图，并作为产品级主导航中的一个普通页面；`/desktop` 只作为功能地图中的 WebOS 历史入口和旧深链承接点
- 公开内容直达路由已经覆盖 `/discover`、`/forum`、`/forum/post/:id`、`/docs`、`/docs/:slug`、`/u/:identifier`、`/leaderboard`、`/shop` 等首批路径；forum detail 优先使用 `Post.PublicId`，公开个人页优先使用 `User.PublicId`，旧 LongId 字符串只保留兼容读取
- 公开内容主路由坚持使用服务端可见的真实路径，不采用 `/shell#page` 或 `/desktop#...` 这类 hash shell 作为公开资源地址
- 公开壳层中的可导航动作必须提供真实 `href`：普通点击可以由 React 拦截并写入 `history.state` 保留来源返回，新开标签、复制链接、辅助点击、canonical、OpenGraph、JSON-LD 和 sitemap 只能依赖公开真实路径
- 公开页头部、发现流、论坛列表 / 搜索 / 标签 / 类型流、文档目录 / 搜索 / 详情、榜单、商城和公开个人页都遵循同一链接契约；不再用无 `href` 的按钮承担页面跳转语义
- 论坛详情中的轻回应、评论、问答回答、作者编辑和历史入口也必须保留对应 `intent` 真实链接；普通点击可恢复当前页工作区，新开标签或复制链接仍能表达同一登录回流意图
- 公开详情页的来源返回状态保存在 `history.state`，用于刷新或浏览器历史恢复后保留返回语义；该状态不进入 canonical、分享链接或 sitemap
- 公开详情加载后如果要规范化旧标识或非标准路径，例如 forum 旧 long、公开个人页旧 LongId 或 docs slug 归一，应用内 `replace` 必须保留当前标签页的来源返回状态，不能让 canonical 替换清掉“返回社区发现 / 我的圈子 / 我的状态 / 聊天”等语义
- 登录态个人圈子固定使用 `/circle`，用于承接当前用户关注动态、关注列表和粉丝列表；它不是公开 SEO 路由，不进入 sitemap，也不复刻论坛详情、评论、点赞或推荐算法
- 从 `/circle` 普通点击进入公开帖子详情或公开个人页时，使用当前标签页一次性来源转交补足返回“我的圈子”；新开标签、复制链接、canonical、分享链接和 sitemap 仍只使用公开 URL
- 登录态私域复访固定使用 `/notifications`、`/circle`、`/me`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`、`/messages`、`/pet` 承接轻量通知、关系链复访、个人状态与个人中心子路径、商城订单 / 背包、会话定位和电子宠物照顾；这些路由都不进入公开 sitemap，也不替代 `/desktop` 的完整工作台能力
- 公开商品详情继续购买使用 `/shop/product/:productId?intent=purchase` 表达登录后购买意图；canonical、分享链接和 sitemap 仍使用 `/shop/product/:productId`
- 论坛作者态正式 Web 路径固定使用 `/forum/compose` 与 `/forum/post/:postId?intent=answer|edit|history`；这些 intent 只用于登录回流和作者态现场恢复，不进入公开 canonical、OpenGraph、JSON-LD 或 sitemap
- 从 `/me`、`/messages` 或 `/pet` 普通点击进入公开个人页时，使用当前标签页一次性来源转交补足返回“我的状态”、“聊天”或“电子宠物”；分享链接和公开 canonical 仍只使用 `/u/usr_...`
- 私域复访入口的路由、登录恢复、通知目标分流和验证口径见 [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit)
- 公共页与登录态页面共享同一套产品级主导航；不再把公开页、私域页和工作台页拆成三套全局入口。“更多”进入 `/workbench`，`/desktop` 保留为历史工作台入口但不作为新增功能主动作
- Flutter 原生详情复制公开链接时沿用同一组 Web 公开路由：forum 使用 `/forum/post/:publicId`，docs 使用 `/docs/:slug`，shop 使用 `/shop/product/:productId`；应用内来源、评论定位和 tab 状态不进入复制链接
- `/desktop` 仅作为 WebOS 保留入口，但允许作为历史工作台深链承接点
- WebOS 可在 `/desktop` 内部使用 hash、query 或本地状态表达窗口恢复和局内导航，但这些状态不构成公开 URL 契约
- 当前允许的 `/desktop` 外部承接参数包括：
  - `?app=shop&productId=...`：打开 WebOS 商城商品详情；正式 Web 购买回流优先使用 `/shop/product/:productId?intent=purchase`
  - `?app=shop&orderId=...`：打开商城订单详情，要求已登录后消费
  - `?app=shop&view=orders`：打开商城订单列表，要求已登录后消费
  - `?app=shop&view=inventory`：打开商城背包，要求已登录后消费
  - `?app=chat&channelId=...&messageId=...`：保留现有聊天深链，要求已登录后消费；纯 Web 通知里的聊天目标默认转入 `/messages?channelId=...&messageId=...`
- 未来如重启 Tauri PC 客户端，默认入口应承载纯 Web，不再默认进入 `/desktop`

## 纯 Web 共享壳层规则

`radish.client` 的纯 Web public / private 页面不再各自维护一套 header、移动底栏和状态卡。当前代码入口固定为：

```text
Frontend/radish.client/src/components/web-shell/
├── WebShellHeader.tsx
├── WebShellHeader.module.css
├── WebStateSlot.tsx
├── WebStateSlot.module.css
└── index.ts
```

使用规则：

- Public / Private 页面默认共用一套产品级主导航：PC 导航固定为 `发现 / 论坛 / 聊天 / 更多`，右侧固定为 `通知 / 登录注册或头像用户名`；移动底栏固定为 `发现 / 论坛 / 聊天 / 更多 / 我的`。
- `/me`、`/circle`、`/pet` 和 `/me/*` 归属“我的”；`/messages` 归属“聊天”；`/notifications` 归属右侧“通知”；Docs 作者态、论坛作者态、订单 / 背包、Console、规则页、榜单、公开文档、公开商城和 `/desktop` 兼容入口归属“更多”。
- 右侧登录态账号动作展示真实 `userName`，不展示公开句柄、`displayHandle` 或 `name#index` 组合名；公开句柄只在个人主页、榜单、作者信息等需要区分公开身份的内容区展示。
- 页面可以传入 `navItems / actionItems / mobileNavItems` 覆盖具体动作，但这只能用于同一产品级导航内的定向收敛；不能绕过共享组件另写平行壳层，也不能把“更多”做成突出于主导航之外的大 CTA。
- 聊天详情页在移动端允许隐藏底部栏，优先保证消息列表、输入框和键盘空间；聊天列表页仍使用统一底部栏。聊天左侧会话列表按 `互相关注 / 陌生人私信 / 好友群组 / 公共频道` 分区呈现；当前 `ChannelVo` 已预留 `voConversationKind` 用于后端补齐关系和群组分类，在缺少该字段时 `Public` / `Announcement` 归入公共频道，`Private` 归入陌生人私信，不用名称或描述猜测关系。
- `WebStateSlot` 统一承接 `loading / empty / error / notFound / permission / auth / info` 状态；状态动作如果会导航，必须优先给出真实 `href`，普通点击再按页面语义拦截。
- 移动单列页面需要给内容区保留 `--rx-mobile-shell-offset` 底部空间，避免浮动底栏遮挡最后一屏内容。
- 移动首屏优先展示真实任务内容；公开说明卡、经验说明、作者边界提示和轻量引导只能作为辅助区，不应压住列表、正文、商品、榜单、公开内容或私域任务队列。
- 公开内容宽度使用 `--rx-content-max-width`、`--rx-content-reading-width`、`--rx-content-narrow-width` 这些语义 token，不在页面 CSS 中持续新增硬编码宽度。

这些规则只覆盖正式 Web public / private 壳层；WebOS `/desktop` 的 Dock、窗口系统和窗口几何记忆继续留在 WebOS 维护线，Console 仍按 Console 专用 shell 与后台信息密度实现。

## WebOS 能力迁移判断

迁移判断不按“应用数量”推进，而按用户路径价值推进：

| 类别 | 判断 | 当前例子 |
| --- | --- | --- |
| 已有纯 Web 主路径 | 进入维护补漏，不重复迁移 | `/discover`、`/forum` 公开列表 / 详情、`/docs` 阅读、`/leaderboard`、`/shop` 公开浏览、`/u/usr_...` |
| 已完成首批纯 Web 私域迁移 | 登录后复访、通知回流、个人状态、完整个人中心子路径、商城交易、会话 / 消息定位、电子宠物照顾和正式 Web 功能发现 | `/notifications`、`/circle`、`/me`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`、`/messages`、`/pet`、`/workbench` |
| 已完成首批纯 Web 作者态迁移 | 浏览器直达的论坛创作与作者反馈，不复刻 WebOS 三栏工作台 | `/forum/compose`、`/forum/post/:postId?intent=answer|edit|history` |
| 应优先评估纯 Web | 后续仍有真实浏览器直达价值、但不适合直接复刻 WebOS 的轻量能力 | 轻任务复访、受控的资产 / 经验联动展示、宠物公开名片与经济联动 |
| Flutter 条件承接 | 受控试用已证明必须依赖强移动生命周期、系统通知或原生交互 | 经证据确认的移动消息、通知复访或后台任务 |
| 保留在 `/desktop` | 需要多窗口、工作台、开发者工具或当前低频桌面语义 | 组件库预览、Scalar 入口、复杂工作台级编辑 |
| 后置或重做 | 只是历史 WebOS 形态遗留，或需要新产品边界先明确 | 完整聊天平台重构、完整创作器、完整钱包安全设置 |

首批迁移决策已完成 `/notifications`、`/circle`、`/me`、`/shop/*` 私域、`/messages`、`/pet` 和论坛作者态正式 Web 路径；后续不把聊天、通知、钱包安全设置、商城高风险能力、完整创作器和宠物经济系统同时搬迁，也不把完整聊天平台、完整资产风控、宠物公开名片或 Console 数值配置作为同一批前置门槛。

## 当前明确不做

- 不做“移动版 WebOS”
- 不做“所有页面都同时支持桌面窗口和移动壳层”
- 不把 WebOS 全部功能一次性迁移到纯 Web
- 不把公开内容强行塞回窗口系统
- 不把新功能默认加入 WebOS
- 不默认扩展 Flutter MVP、追平 Web 功能或启动 iOS 套件
- 不把 Capacitor 作为移动端产品化路线
- 不把 Tauri 理解为原生 UI 重写路线
- 不把 WebOS 作为 Tauri 桌面安装包正式默认体验
- 不在个人开发阶段继续消耗主线精力处理 Tauri 签名、自动更新、SmartScreen、生产 Auth 或公开分发链路

## 判断规则

讨论某个功能放哪里时，优先回答四个问题：

1. 它是“看内容”还是“做事情”？
2. 它主要发生在公开访问，还是登录后的长期使用？
3. 它在手机上是否应该一跳直达？
4. 它是否真的需要多窗口和工作台语义？

- 如果偏公开、偏直达，优先进入纯 Web 壳层
- 如果是登录后轻量链路，优先进入纯 Web，不因已有 Flutter 代码重复实现
- 如果是 WebOS 既有多任务能力，只做维护或迁移评估
- 如果真实使用证据证明必须依赖移动安装包、系统推送、后台任务或原生生命周期，再评估重启 Flutter
- 如果真实需求必须依赖桌面安装包、系统菜单、托盘、自动更新或文件系统能力，再单独评估解冻 Tauri，UI 默认复用纯 Web
