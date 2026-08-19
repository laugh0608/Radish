# Flutter Native P1 全页面事实审计与代表分级

> 日期：2026-08-19（Asia/Shanghai）
>
> 基线：`dev` / `3a010194`
>
> 范围：`Clients/radish.flutter` 的 app、shell、认证、全部页面、shared widgets、自动化测试与 Android bridge
>
> 结论：`P1` 已完成；本批只形成事实、页面族归属和 `P2` 输入，未修改 Dart、依赖、平台工程或 Pencil，未启动服务、模拟器或浏览器

## 1. 摘要

Flutter 当前不是空壳，也不适合推倒重写。现有 Android MVP 已建立真实的 Gateway / API 调用、OIDC `state + PKCE`、会话恢复、论坛写入幂等、单商品购买幂等、分页、来源返回、通知回流和 Android Back；这些 owner 与行为契约应保留。

当前主要缺口位于产品呈现和公共基础：

- `MaterialApp` 只消费固定亮色 `ThemeData`，没有主题 ID、Theme Controller、`ThemeMode`、`ThemeExtension`、字体资产、主题持久化或四主题权益接线。
- Shell 始终使用五项底部 `NavigationBar`，没有 compact / medium / expanded 窗口等级、`NavigationRail`、桌面侧栏、键盘 / 焦点 / hover / 滚轮或窗口缩放 owner。
- 绝大多数页面是单列 `ListView + Card + Wrap`，页面内还保留大量 MVP 范围说明 `PhaseScopeCard`；这属于早期验收表面，不是长期产品 UI。
- 现有页面状态质量不一致：Discover、Docs、Forum 列表能在刷新失败时保留旧快照；通知会在刷新失败时清空旧列表；Profile 主资料 / 统计 / 帖子 / 评论仍整批成功或失败；背包、Wallet、Experience 也将多个资源绑定为一次整批结果。
- 当前 Flutter 全部产品文案为中文硬编码，`MaterialApp` 没有 locale owner；无 reduced-motion、语义焦点、快捷键或桌面输入实现。
- `ShopUserBenefit` 没有保留主题映射需要的 `voBenefitValue` 等字段；代码中的 `ShopRepository` 只有权益读取，没有激活 / 停用方法。后续不能假设主题权益写入链已经接好。

因此 P2 应做一项完整、可退出的基座 spike：**Theme Foundation + Adaptive Shell + Discover + Forum Detail**。它用于裁决唯一主题 / 组件基础和窗口结构，不扩后端能力、不整页重写全部业务。

## 2. 审计范围与方法

本批静态核对：

- `65` 个 `lib/*.dart`，约 `28,222` 行；
- `17` 个 Dart 测试文件，约 `16,145` 行、`209` 个 `test / testWidgets`；
- Android `MainActivity`、native intent payload 与 `7` 个 Kotlin 单元测试；
- `pubspec.yaml`、环境配置、HTTP 信封、OIDC、平台 MethodChannel、页面跳转与本地 follow-up store；
- 所有公开 `Page`、内部详情 route、通知 sheet、资料编辑 dialog 与 Launch Gate。

本批没有把 README 的历史批次清单当作页面事实；最终裁决以当前代码 owner、调用链和测试为准。

## 3. 全局 owner 与调用链

```text
main.dart
  -> RadishBootstrap
       -> AppEnvironment
       -> Platform / InMemory session、auth、follow-up、lifecycle adapters
       -> HttpRadishApiClient + RadishApiEndpoints
       -> 各领域 HttpRepository
  -> RadishApp
       -> SessionController.restore()
       -> MaterialApp(theme: buildRadishTheme())
       -> Launch Gate 或 RadishFlutterShell
  -> RadishFlutterShell
       -> IndexedStack 五个主入口
       -> Navigator.push 承接详情与私域派生页
       -> 本地 follow-up store 承接登录、论坛、文档和公开主页来源
```

全局事实：

- App 依赖采用构造注入，没有全局 service locator 或第二套状态容器。
- 公开 / 私域 API 统一通过 `HttpRadishApiClient` 消费 Radish 响应信封，没有 Flutter 专属 BFF。
- 主导航与跨页面来源返回由 Shell 手工编排；页面内部继续使用 `Navigator.push`。
- Android 使用 MethodChannel + `SharedPreferences` 保存会话、授权尝试和最近阅读；非 Android 目前全部回退内存实现，重启后不持久。
- Android `MainActivity` 负责 OIDC callback、论坛 intent handoff、根层退后台和本地存储；Manifest 只注册 `radish://oidc/*` 浏览器回流。

## 4. 页面与表面事实矩阵

| 表面 | 当前 owner / 调用 | 真实能力与状态 | 主要技术债 | 唯一页面族 |
| --- | --- | --- | --- | --- |
| Launch Gate | `RadishApp` + `SessionController` | 恢复未过期会话；过期会话走 refresh，失败清会话并回游客 | 固定 loading UI；没有主题恢复、离线快照或恢复超时表面 | Shell / Auth |
| 主 Shell | `RadishFlutterShell` | 五 tab、`IndexedStack` 保活、Android Back、登录 / 退出、来源返回、最近阅读、私域 route 调度 | 仅底部导航；状态 chip 和环境标识偏开发面；单文件 `1892` 行 | Shell / Auth |
| OIDC | `NativeAuthController` + platform gateway | 密码学随机 `state`、PKCE S256、五分钟授权尝试、一次性 callback、稳定授权错误 | iOS / desktop 没有平台 adapter；token 仍由 Android 普通 SharedPreferences 保存 | Shell / Auth |
| 通知 sheet | Shell + `NotificationRepository` | 最近 `20` 条、单条已读、Forum 通知跳帖子 / 评论；不可跳通知只读 | 不是独立页面；刷新失败会清空旧列表；无分页、stale、筛选或桌面通知面 | Shell / Notification |
| Discover | `DiscoverFeedController` + `DiscoverRepository` | Forum / Docs / Shop 三段并行读取，单段失败局部化；旧快照刷新保留；打开各领域详情 | 单列摘要卡；没有真实响应式重排；页面“当前不支持购买”与已存在商品购买链路不一致 | Discover |
| Forum 列表 / 发帖 | `ForumFeedController` + `ForumPage` + `ForumRepository` | 最新 / 热门、分页、刷新保留、分类读取、纯文本发帖、草稿留在页面、登录回流、失败重试复用 submission key | Composer 与列表同一长页；草稿只随 widget 存活；没有搜索 / 分类页产品结构；没有 medium / expanded 布局 | Forum Feed / Compose |
| Forum 详情 / 互动 | `ForumDetailPage` + detail / comment / child / quick-reply controllers | 正文、问答、轻回应、根 / 子评论、评论定位、发回答 / 评论 / 回复、作者编辑帖子 / 根评论、登录原位回流、各写入局部反馈 | 单文件 `3637` 行；视觉、状态与领域编排过度集中；详情刷新不保留旧正文；自定义滚动动效不尊重 reduced-motion | Forum Detail / Interaction |
| Docs 列表 / 内联详情 | `DocsPage` + feed / detail controllers | 搜索、分页、刷新保留、列表滚动恢复、内联详情、正文 docs 链接跳转、Android Back 返回搜索结果 | list/detail 只在 compact 单任务切换；详情 refresh 进入 loading；没有目录 / 阅读双栏 | Docs Reader |
| Docs handoff 详情 | `_DocsDetailRoutePage` | Discover / Profile / 正文链接来源进入独立 route，支持逐层返回与 recent store | 与内联详情存在两套 route 外壳；后续需共用同一阅读 owner，不复制 UI | Docs Reader |
| Profile 游客 / 公开 / 我的 | `ProfilePage` + `ProfileController` | 游客边界、公开资料 / 统计 / 帖子 / 评论、我的轻回应、最近论坛 / 文档、加载更多、公开主页回流 | 主资料 / 统计 / 帖子 / 评论使用 `Future.wait`，任一失败导致整页 unavailable；单文件 `1904` 行；入口按钮堆在同一 Wrap | Identity / Revisit |
| Profile 编辑 | `ProfileEditDialog` + `ProfileRepository` | 独立读取 / 保存展示名、邮箱、年龄、地址；成功后刷新公开资料 | 固定宽 `520` dialog；没有 dirty 离开保护、请求代际或结构化字段错误；不是 compact 原生任务流 | Identity / Revisit |
| 最近访问 | `BrowseHistoryPage` + `ProfileRepository` | 服务端分页读取 Post / Wiki / Product，按原生目标打开，刷新保留旧列表 | 与 Shell 本地 recent store 是两类不同来源，产品文案需区分；无去重防重叠页；只有单列卡片 | Identity / Revisit |
| Shop 商品列表 | `ShopProductListPage` + `ShopRepository` | 公开商品分页、刷新保留、详情返回 | 只有单列；没有 medium / expanded grid；无筛选能力，不应在 UI 重构中伪造 | Commerce Browse |
| Shop 商品详情 / 购买 | `ShopProductDetailPage` + Shop / Wallet repositories | 公开详情、余额、资格检查、六位支付口令、一次购买、幂等 key 失败重用、登录回流、订单结果 | 多资源状态全在 widget；支付草稿无离开提示；视觉顺序和宽屏购买侧栏尚未建立 | Commerce Transaction |
| 订单列表 | `ShopOrderListPage` | 登录态真实分页、刷新保留、详情跳转 | 单列卡；无 medium / expanded 列表—详情；无页重叠去重 | Commerce Private |
| 订单详情 | `ShopOrderDetailPage` | 订单时间线、来源商品、订单扣款流水、背包发放确认 | 只读单列；与列表分离 route，宽屏不能并置 | Commerce Private |
| 背包 / 权益 | `ShopInventoryPage` | 并行读取权益和道具，来源订单 / 商品返回 | 两个请求 `Future.wait` 整批失败；只读；权益 DTO 缺主题值，Repository 无激活 / 停用接口 | Commerce Private |
| Wallet | `WalletPage` + `WalletRepository` | 余额、真实分页流水、订单业务筛选、刷新保留 | 余额与流水整批成功 / 失败；只有单列；无独立局部 unavailable | Asset / Progression |
| Experience | `ExperiencePage` + `ExperienceRepository` | 等级、进度、冻结状态、真实分页流水、刷新保留 | 概要与流水整批成功 / 失败；只有单列 | Asset / Progression |
| Leaderboard | `LeaderboardPage` + `LeaderboardRepository` | 经验榜首屏 `20` 条、刷新保留、公开主页回流 | 只读第一页；服务端 `themeColor` 直接进入页面颜色；无分页或宽屏排名结构 | Asset / Progression |

## 5. 业务行为保留矩阵

### 5.1 必须保留

- `SessionController` 的 restore / refresh / anonymous 收敛及页面构造注入。
- Native OIDC 的 `state + PKCE`、一次性授权尝试和稳定 callback 错误。
- Forum / Docs / Profile 的来源返回、Shell post-login target、最近阅读去重和 Android Back。
- Forum 发帖、回答、评论、回复、帖子编辑、根评论编辑的 submission fingerprint 与失败重试 key。
- Shop 单商品购买的资格检查、支付口令校验、购买幂等 key、订单结果和规范 LongId 字符串边界。
- Discover 分段失败、列表刷新保留、load-more 局部失败以及详情区独立失败的既有行为。
- HTTP 信封、Repository 接口边界和现有服务端权限 / 状态机。

### 5.2 调整编排

- Shell 的五项主入口、通知入口、登录 / 退出和 recent actions 按窗口等级重排，但不改变来源语义。
- Profile 将身份、统计、公开活动、我的复访和私域入口拆成清晰任务区；主资料、统计、帖子、评论分别维护权威快照。
- Forum Feed 的浏览与发帖 composer 分层；Forum Detail 的正文、回答、轻回应、评论和作者编辑保留独立 owner。
- Docs 使用同一详情 owner 承接 compact route 与 medium / expanded 并置阅读。
- Shop 商品详情把购买作为明确的敏感动作区；订单、背包、Wallet 和 Experience 继续只读，不扩业务。
- 通知读取保留旧快照，首次失败 unavailable、刷新失败 stale；不可跳通知仍只读。

### 5.3 重做呈现

- 固定亮色橙棕 Theme、全局 `24px` card 圆角、默认 Material 控件堆叠。
- 每页重复的标题、能力说明、环境说明、返回按钮和开发阶段 `PhaseScopeCard`。
- Shell 顶部环境 / 会话 chip 带和所有宽度下固定底部导航。
- 单列大卡片、卡片套卡片以及用 `Wrap` 代替真实 medium / expanded 布局。
- Profile 编辑固定 dialog、通知固定 bottom sheet 等只适合 compact 的唯一形态。

### 5.4 明确后置

- Console、完整 Author、聊天、完整通知治理、系统推送、购物车、退款、权益使用、完整资产中心。
- 商品评价、关注 / 私信、完整浏览历史治理等当前 Flutter 未承接能力；不能借 UI 重构从 Web 机械搬入。
- iOS / Windows / macOS / Linux 平台工程、签名、更新和分发。
- Flutter Web、Tauri 恢复和 WebOS 扩展。

## 6. 页面族唯一归属与代表分级

| 页面族 | 包含表面 | 等级 | 继承关系与设计要求 |
| --- | --- | --- | --- |
| Shell / Auth / Notification | Launch Gate、Shell、OIDC banner、通知 sheet | R1 | compact 与 expanded 正式代表；medium 补导航和通知结构差异；主题、状态、焦点从此族下沉 |
| Discover | Discover hero、三类摘要、局部失败 | R1 | compact 与 expanded 正式代表；证明跨域摘要、空 / 错 / stale 和主题识别 |
| Forum Detail / Interaction | 正文、问答、轻回应、评论树、定位、作者编辑 | R1 | compact 与 expanded 正式代表；高风险写入和长内容主代表 |
| Identity / Revisit | 游客、公开主页、我的、编辑资料、recent sections | R1 | Profile / 我的维护 compact 与 expanded；编辑和 recent 派生面按关键差异继承 |
| Forum Feed / Compose | 列表、排序、分页、纯文本发帖 | R2 | 继承 Discover 内容流和 Forum Detail 互动语义；维护 composer 与宽屏列表关键区块 |
| Docs Reader | 搜索、目录、列表、正文、内链、来源返回 | R2 | 继承 Forum 长内容 / 状态；维护 compact 单任务与 medium / expanded 目录—正文差异 |
| Commerce Browse / Transaction | 商品列表、详情、资格、余额、购买、订单结果 | R2 | 维护商品信息—敏感购买区和登录 / 幂等 / 错误关键状态，不复制 Web 商品页 |
| Theme / Shared State | 主题设置、loading / empty / error / stale、共享导航 / 表单 | R2 | 作为跨页面关键区块存在，不成为万能页面框架 |
| Commerce Private | 订单列表 / 详情、背包 / 权益 | R3 | 继承 Commerce R2；通过真实窗口复核，保持只读边界 |
| Asset / Progression | Wallet、Experience、Leaderboard | R3 | 继承 Profile summary + 连续记录结构；不新增治理操作 |
| Browse History | 服务端最近访问 | R3 | 继承 Profile / 我的和连续列表；区分服务端记录与设备 recent shortcut |

分级与 P0 方向一致，但本批补充了 Profile 编辑、最近访问、Commerce 私域和通知 sheet 的唯一归属，不再留下“按文件名决定”的空白。

## 7. compact / medium / expanded 结构矩阵

下表固定任务结构，不冻结具体像素断点；断点数值由 P2 spike 和 P3 代表设计确认。

| 页面族 | compact | medium | expanded |
| --- | --- | --- | --- |
| Shell | 底部主导航；单任务全屏；通知 / 主题用 sheet | Navigation Rail + 单主面；详情可覆盖或双栏 | 固定侧栏 + 受控主内容宽度 + 按需上下文栏；支持键盘和窗口缩放 |
| Auth / Notification | 壳层内 banner；通知 bottom sheet | 壳层 banner；通知侧 sheet / 次栏 | 账号动作进入侧栏；通知用可关闭上下文面，不新增虚假独立 route |
| Discover | 单列主摘要，主入口前置 | 主摘要 + 两列领域卡 | 受控主轴 + 辅助概览栏；三领域仍消费同一 snapshot |
| Forum Feed / Compose | 连续帖子流；发帖为明确单任务 sheet / page | 列表 + 可选预览；composer 独立层 | 列表—详情协同；composer 保留同一草稿 owner |
| Forum Detail | 正文—回答—轻回应—评论单轴 | 正文 + 评论 / 上下文双区 | 受控阅读宽度 + 互动上下文；不把所有区块铺成仪表盘 |
| Docs | 列表 / 详情单任务切换 | 目录 / 列表—正文双栏 | 目录—正文—阅读上下文；正文宽度受控 |
| Profile / 我的 | 身份—主动作—摘要—活动—复访 | 身份摘要 + 活动双区 | 身份 / 统计与连续活动分区；私域入口不做 WebOS 工作台 |
| Shop 列表 | 连续商品卡 | 自适应两列卡 | 受控多列目录；当前无筛选 API，不伪造筛选 rail |
| Shop 详情 | 商品—价格—资格—购买—说明单任务 | 商品信息 + 固定购买摘要双区 | 受控详情 + 敏感购买侧栏；登录和幂等状态保持同一 owner |
| 订单 / 背包 | 列表与详情逐层 route | 列表—详情或双列资源卡 | 列表—详情协同；背包使用内容网格，保持只读 |
| Wallet / Experience | 概要先于连续流水 | 概要 + 流水双区 | 固定概要栏 + 受控流水主轴 |
| Leaderboard | 连续排名卡 | 紧凑排名列表 | 受控排名表面 + 公开主页上下文；仍只读 |
| Browse History | 连续访问卡 | 类型分组后的双列或列表—预览 | 受控历史主轴；不新增删除 / 推荐治理 |

## 8. 主题、组件与平台事实

### 8.1 主题断点

- `radish_theme.dart` 只有 `ColorScheme.fromSeed(#B76536, light)` 和固定 `#F6F1EA` 背景。
- 页面大多已使用 `Theme.of(context).colorScheme / textTheme`，硬编码页面颜色很少，说明从主题 owner 治理具有高收益。
- 现有 `ShopUserBenefit` 只保留 `benefitType / name / source / active / expired` 等字段，没有 `benefitValue` 和完整状态。
- `ShopRepository` 只定义 `getMyBenefits()`，没有激活 / 停用方法；P0 专题中“已有激活 / 停用 API 调用入口”的表述不符合当前代码，应以本记录修正。
- Leaderboard 直接解析服务端 `themeColor`，P2 需明确“业务内容 accent”与 Radish 主题语义的边界及对比度规则。

### 8.2 共享组件事实

当前 `shared/widgets` 只有：

- `PhaseScopeCard`：MVP 范围说明，不应成为正式产品组件；
- `PublicLinkCopyPanel`：可复用的公开链接展示 / 复制 owner；
- `ReadOnlyMarkdownView`：轻量只读 Markdown 与 docs 内链 owner。

不存在共享 Button、Surface、Field、Chip、State、Section、Navigation、motion 或 adaptive layout 组件。P2 只能抽取代表场景里已发生的复用，不建立万能页面框架。

### 8.3 平台与可访问性事实

- 只有 Android 平台工程；bootstrap 对非 Android 的 session / auth / recent stores 使用内存实现。
- 没有 `MediaQuery.disableAnimations`、motion token、`ThemeMode`、系统暗色、locale owner、Focus / Shortcuts / Actions、hover 或语义测试。
- 只有 Shell、Forum Detail 和 Launch Gate 明确使用 `SafeArea`；其他 route 依赖 Scaffold / 系统 inset 的默认行为。
- Profile 唯一的 `LayoutBuilder` 只用于统计卡两列，不构成页面级自适应系统。
- Android 会话 token 保存于普通 `SharedPreferences`；这不阻断 P2 UI spike，但正式外部分发前需要独立安全存储评估，不能因 UI 完成而忽略。

## 9. 状态与错误边界裁决

### 9.1 已具备、应复用

- Discover、Docs Feed、Forum Feed、Profile、Shop、订单、背包、Wallet、Experience 等普遍使用 request version / request ID 丢弃过期响应。
- 多数列表能区分首次失败与刷新失败，并在刷新失败时保留已加载内容。
- Forum Detail 已把正文、评论、子评论、轻回应和写入反馈拆为多个 controller / 局部状态。
- Shop purchase 将资格、余额、提交和订单结果分开，失败重试保留幂等 key。

### 9.2 P4 / P5 前必须治理

- Profile 的主资料 / 统计 / 帖子 / 评论改为独立快照；统计或活动失败不得拖垮主身份。
- Notification refresh 失败保留旧列表并标记 stale，不再清空为零条。
- Inventory 的权益 / 道具、Wallet 的余额 / 流水、Experience 的概要 / 流水改为局部权威状态。
- Docs / Forum 详情刷新在已有正文时保持可读，错误进入局部 stale，而不是回到整块 loading。
- Profile 编辑补 dirty / busy / 离开保护；Forum / Shop 草稿的页面生命周期和宽屏重排必须保持同一实例。
- 分页列表在 append 时按稳定 ID 去重，防止边界数据移动产生重复项。

这些治理按领域 owner 分批完成，不引入跨业务万能状态机。

## 10. 测试事实与缺口

现有 `209` 个 Dart 测试和 `7` 个 Android Kotlin 测试重点覆盖：

- API 信封、环境配置、OIDC `state + PKCE`、callback 错误、会话恢复；
- Discover / Docs / Forum / Profile 的读、写、刷新、分页、来源返回和窄屏溢出；
- Forum 五类 submission key、评论定位和局部更新；
- Shop LongId、购买幂等、订单 / 背包 / Wallet 来源返回；
- 通知解析、单条已读和 Forum handoff；
- Android intent payload、根层退后台和 recent store。

明确缺口：

- 没有四主题、Theme Controller、权益主题解析或主题持久化测试；
- 没有 medium / expanded 结构测试，现有大量 `1200px` surface test 仍只渲染移动单列结构；
- 没有 keyboard / focus / hover / scroll / resize、reduced-motion 和语义标签矩阵；
- 没有本地字体 / golden 稳定策略；
- `WalletPage`、`ExperiencePage`、订单列表和背包的独立 widget 覆盖较弱，主要依赖商城组合测试或 smoke；
- 没有 Profile 多资源局部失败、通知 stale、权益主题字段解析测试。

测试文件本身也存在体积债：`smoke_test.dart` 约 `5092` 行、`profile_page_test.dart` 约 `2598` 行、`forum_detail_page_test.dart` 约 `2225` 行。后续按页面族拆分 fixture / scenario，但不在 P2 spike 中顺手重写全部测试。

## 11. 文件所有权与体积风险

运行时代码超过项目 `1500` 行硬上限的文件：

- `forum_detail_page.dart`：约 `3637` 行；
- `profile_page.dart`：约 `1904` 行；
- `radish_flutter_shell.dart`：约 `1892` 行。

治理边界：

- P2 只为 Theme / Adaptive Shell spike 拆出确有稳定 owner 的壳层、导航和状态组件。
- Forum Detail 在进入 R1 正式实现批时按正文、问答、轻回应、评论和编辑表面拆分；保留现有 controller，不引入空泛 coordinator。
- Profile 在进入 R1 正式实现批时按身份、统计、公开活动、我的复访与编辑任务拆分，并先关闭多资源整批失败。
- 不为压行数制造只转发参数或隐藏简单逻辑的工具类。

## 12. P2 首批完整代表场景

P2 固定为一个可单独回滚的技术基座 spike：

**Flutter Theme Foundation + Adaptive Shell + Discover + Forum Detail**

### 12.1 必须覆盖的状态组合

- 匿名 / 已登录 / 会话恢复失败；
- 内建 `default / guofeng`；有权 / 无权 / 失效的 `theme-dark-night / theme-sakura`；
- compact / medium / expanded；
- Discover 全部成功、单段失败、全部失败、旧快照刷新失败；
- Forum Detail 正文、问题回答、轻回应、评论、评论定位、登录回流和至少一类失败重试写入；
- loading / empty / error / stale / permission；
- reduced-motion、键盘焦点、触控目标和窗口缩放。

### 12.2 依赖评估输入

- 保留 Material 3 作为原生输入和可访问性底座。
- 评估 `flex_color_scheme` 作为 `ThemeData` / component theme 生成器；不得让预设方案成为品牌真相源。
- 必须建立自有 `ThemeExtension`，显式映射 Radish surface、text、border、brand、action、state、radius、spacing 和 motion。
- 字体优先本地资产和许可证可追溯；若评估 `google_fonts`，生产必须禁用运行时网络获取。
- 评估跨平台非敏感主题偏好的持久化 owner；不要继续为每个平台复制 Android MethodChannel。
- 不默认引入 Router 或全局状态框架。当前构造注入、Controller 和来源 target 足以先完成 spike；只有真实结构无法维持时再提出依赖。
- `shadcn_ui` 不作为全局基础；只有在代表场景出现 Material / Flex 无法表达的明确缺口时，才允许做单个隔离比较并预设退出条件。
- 动效包不是 P2 前置；先用 motion token 和 `MediaQuery.disableAnimations` 证明契约。

版本、许可证、维护活跃度、传递依赖和 lockfile 影响必须在安装前重新核对，并按项目规则单独说明命令、目标版本和变更面后取得授权。

### 12.3 预计影响文件

- `pubspec.yaml`、`pubspec.lock`：仅在依赖授权后修改；
- `lib/core/theme/`：主题注册表、ThemeExtension、typography、Theme Controller、持久化接口；
- `lib/app/app.dart`、`bootstrap.dart`：注入并消费主题 owner；
- `lib/features/shop/data/shop_models.dart`、`shop_repository.dart`：补全主题权益读取与既有激活契约接线；不改服务端状态机；
- `lib/features/shell/presentation/`：窗口等级、导航变体、主题 / 通知入口和壳层状态；
- `lib/features/discover/presentation/`：代表内容流与局部状态；
- `lib/features/forum/presentation/`：Forum Detail 代表阅读 / 互动结构；
- `lib/shared/widgets/`：只增加代表场景真实复用的 Surface、State、Button、Field、Navigation 和 adaptive primitives；
- `test/`：主题、权益、窗口结构、reduced-motion、焦点和代表场景测试。

### 12.4 验证矩阵

1. `flutter analyze`；
2. `flutter test` 全量及 Theme / Shell / Discover / Forum Detail 定向测试；
3. compact、medium、expanded 固定 surface 结构测试；
4. 四主题、匿名 / 登录、权益有效 / 失效、刷新 stale；
5. reduced-motion 下关键状态静态可见；
6. compact 安全区、系统返回、键盘避让和触控目标；
7. expanded Tab 焦点、方向键、Enter / Space、Escape、hover、滚轮和窗口缩放；
8. 字体稳定后再决定 golden，不把不稳定 golden 作为首个门禁；
9. 专题准备验收且服务启动另获授权后，才进行 Android / desktop 真实 smoke。

## 13. P2 停止线与退出条件

停止线：

- 不改后端接口、权限、业务状态机、幂等和来源返回契约；若权益 DTO 的服务端投影确实不足，先形成窄契约方案并等待确认。
- 不解析 Web CSS，不运行时共享 CSS token，不建立第二套主题权益真相。
- 不重写全部页面，不把 medium / expanded 等同于把 compact 页面拉宽。
- 不生成新平台目录，不恢复 Tauri，不扩展 WebOS，不引入 Flutter Web。
- 不同时保留两套全局组件框架。
- 不用 fallback 掩盖权益字段、状态版本或平台 adapter 缺失。

P2 退出条件：

- 唯一全局主题 / 组件基础已选定；
- 四主题身份、权益收敛、窗口等级和 reduced-motion 有自动化证据；
- Shell + Discover + Forum Detail 能以同一业务 owner 在 compact 与 expanded 正确重排；
- 依赖、字体资产、许可证、持久化、回滚和 P3 代表设计输入明确；
- 审计中列出的行为契约未回退。

## 14. P1 完成结论

- 所有当前页面和非 Page 表面已有唯一页面族归属；
- compact / medium / expanded 任务结构已经建立；
- R1 / R2 / R3 与继承路径已经裁决；
- “保留行为、调整编排、重做呈现、明确后置”已经逐族固定；
- P2 首批完整代表场景、依赖评估输入、影响文件、验证矩阵和停止线均已给出；
- 当前下一顺位进入 **P2 方案确认**，未获确认前不修改依赖、Dart、平台工程或 Pencil。
