# Flutter Native P5-B2 Identity / Revisit 实施就绪与方案冻结

> 状态：`P5-B2 readiness` 已完成；等待 Identity / Revisit 实施确认
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-B1 Forum Feed / Compose 实现](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23)

## 1. 本批结论

P5-B2 可以在不新增后端 API、不改变 Shell handoff 和不扩建账号能力的前提下实施。现有 Profile 已覆盖游客边界、公开主页、我的主页、公开统计、公开帖子 / 评论、我的轻回应、设备最近 Forum / Docs、公开链接复制、资料编辑和原生来源返回；主要问题不是功能缺失，而是页面 / 测试超出硬上限、多个公开资源被一次 `Future.wait` 整批裁决、所有分页共享同一请求代际，以及资料编辑缺少权威草稿与离开保护。

本批方案固定为：公开身份、统计、帖子、评论和我的轻回应分别持有权威快照、请求代际与结构化 issue；主身份是页面成立的必要资源，其余区块局部 `loading / unavailable / stale`，不得用零值掩盖失败。设备最近 Forum / Docs 继续由 Shell 提供，完整服务端最近访问仍归 `P5-D3 Browse History`。资料编辑只使用既有 `GetMyProfile + UpdateMyProfile`，以独立权威草稿实现 dirty、busy、失败保留和离开保护。

本次只完成只读事实审计与方案冻结，没有修改 Dart、API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与规模

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `profile_page.dart` | 页面生命周期、session / target、所有页面结构与区块 widget | `1904` 行 |
| `profile_controller.dart` | 公开资料、统计、帖子、评论、我的轻回应和三个分页状态 | `626` 行 |
| `profile_edit_dialog.dart` | `GetMyProfile`、表单、校验、`UpdateMyProfile` 与固定 Dialog | `351` 行 |
| `browse_history_page.dart` | 服务端完整最近访问派生页 | `600` 行；不进入 P5-B2 |
| `profile_page_test.dart` | Profile 全部 widget、repository stub 与 fixture | `2598` 行 |

`profile_page.dart` 与 `profile_page_test.dart` 已超过 `1500` 行硬上限，必须先按真实职责拆分，不能在原文件继续叠加响应式和状态代码。

### 2.2 当前状态缺口

- `ProfileController._load` 用一次 `Future.wait` 绑定 `GetPublicProfile / GetUserStats / GetUserPosts / GetUserComments`；统计或任一活动失败会拖垮整个身份页。
- 主资料、统计、帖子、评论与我的轻回应共用 `_requestVersion`；不同列表同时加载更多时，后发请求会让先发请求失效，无法形成真正独立的分页 owner。
- 公开刷新能够保留整页旧快照，但只有一条字符串 `refreshIssueMessage`；无法表达某一区块首次 unavailable 或旧快照 stale，也丢失 API `code / statusCode`。
- 帖子、评论和轻回应加载更多已按稳定字符串 ID 去重，LongId / PublicId 没有数值化；这一契约应保留并改为各列表独立请求代际。
- 编辑器每次打开独立读取 `GetMyProfile`，但没有请求代际、dirty 判断、保存中返回保护或丢弃确认；固定 `520px` AlertDialog 不是 compact 原生任务面。
- 页面只有统计卡内部 `LayoutBuilder`，没有页面级 compact / medium / expanded 结构；能力说明和所有动作堆在同一长 `ListView + Wrap`。

### 2.3 测试基线

改造前 `flutter test test/profile_page_test.dart` 为 `32 / 32`。已有覆盖包括游客 / 登录态、公开 / 我的、编辑成功与失败、窄屏长文本、刷新旧快照与恢复、公开链接复制、公开主页复访、Forum / Docs handoff、帖子 / 评论 / 轻回应分页及 append 失败、局部轻回应失败、登录与退出。

当前没有直接覆盖：统计 / 帖子 / 评论首次局部失败、各区块独立 stale、跨用户迟到响应、并行分页代际、重复页去重、三档页面结构、四主题同构结构，以及编辑 dirty / busy / 系统返回 / 丢弃确认。

## 3. 既有 API 与 target mapping

P5-B2 只消费以下既有契约：

| 既有能力 | P5-B2 用途 | 裁决 |
| --- | --- | --- |
| `User/GetPublicProfile?userId=...` | 公开身份主快照 | 页面必要资源；不新增聚合 API |
| `User/GetUserStats?userId=...` | 公开统计 | 独立快照；失败不得伪装为 `0` |
| `Post/GetUserPosts` | 最近公开帖子与页码 | 独立分页、旧快照与稳定 ID 去重 |
| `Comment/GetUserComments` | 最近公开评论与页码 | 独立分页、旧快照与稳定 ID 去重 |
| `PostQuickReply/GetMine` | 我的轻回应 | 只在本人登录态读取；局部失败不影响公开资源 |
| `User/GetMyProfile` | 可编辑资料权威快照 | 只在编辑任务中读取 |
| `User/UpdateMyProfile` | 用户名、邮箱、年龄、地址保存 | 本批唯一高风险写入；不添加第二套提交 API |
| Shell recent Forum / Docs targets | 设备最近复访 | 保持输入 props 与最多 5 条去重；不迁入 Profile controller |
| `ForumDetailHandoffTarget` | 帖子、评论、轻回应、最近 Forum | 帖子优先 `postPublicId`，缺失时字符串 `postId`；评论保留字符串 `commentId` |
| `DocsDetailHandoffTarget` | 最近文档 | 保留 slug、来源和标题；打开时改写为 Profile recent source |

公开资料当前仍按既有 `userId` 参数和 `voUserId` 字符串模型读取；P5-B2 不顺带改造 Web canonical / SEO PublicId 聚合。公开主页来源记录、回到我的主页、Android Back 和同一 target 再次打开继续由 Shell owner 负责。

完整服务端 `User/GetMyBrowseHistory`、Post / Wiki / Product 连续历史和其去重属于 `P5-D3`，本批只保留现有“查看最近访问”入口，不能把设备 recent shortcut 冒充完整服务端历史。

## 4. 权威状态模型

### 4.1 页面族本地状态

`ProfileController` 保留为 Profile 页面族唯一远端读取 owner，但拆为以下独立状态：

- target：标准化目标 ID、本人 / 公开模式和 target epoch；目标、session 或 repository 变化时一次性使旧用户全部响应失效；
- identity：`PublicProfileSummary` 主快照、状态、issue 与独立请求代际；
- stats：`PublicProfileStats` 独立快照、状态、issue 与独立请求代际；
- posts：第一页快照、分页、append busy / issue 与独立请求代际；
- comments：第一页快照、分页、append busy / issue 与独立请求代际；
- my quick replies：本人态独立快照、分页、append busy / issue 与独立请求代际。

状态值在 Profile 页面族内统一为 `idle / loading / ready / unavailable / stale`。issue 沿用 P4-B3 / P5-B1 已验证的结构，至少保留 `kind / message / code / statusCode`；`FormatException` 明确归为 invalid response，不把所有失败压成字符串。该模型只服务 Profile，不上升为新的全局状态框架。

### 4.2 提交与渲染规则

- 首次 identity 失败时页面进入 unavailable 并提供重试；已有 identity 刷新失败时保留身份与全部可用区块，identity 标记 stale。
- stats、posts、comments 或 quick replies 无快照失败时只在对应区块显示 unavailable；已有快照刷新失败时只把对应区块标记 stale。
- 页面刷新同时请求当前 target 的各个资源，但每个响应独立提交；某个区块成功不得清除其他区块的 issue。
- 帖子、评论和轻回应 refresh 保留旧列表，成功后以第一页权威结果替换；append 继续展示已加载项，失败保留列表和可重试 issue。
- 三种列表分别按稳定字符串 `id` 去重；并行 append、刷新或切换用户时只由本列表 request generation 与 target epoch 裁决，不互相取消。
- 统计服务明确返回 `0` 时才显示 `0`；读取失败显示 unavailable / stale，不使用默认零值伪造事实。
- 本人态退出、账号切换或公开 target 变化立即清除不属于新身份的 quick replies 与私域动作，不保留跨账号快照。

## 5. Owner 拆分

进入页面实现前按以下真实职责拆分：

| Owner | 冻结职责 |
| --- | --- |
| `profile_page.dart` | 生命周期、session / target 同步、编辑任务入口、Shell handoff 编排 |
| `profile_controller.dart` | target epoch、五类独立远端快照、刷新、分页与结构化 issue |
| `profile_surface.dart` | compact / medium / expanded 页面级结构、根状态和动作编排 |
| `profile_identity_surface.dart` | 身份 hero、统计、公开链接与身份上下文 rail |
| `profile_activity_surface.dart` | 设备 recent、我的轻回应、公开帖子 / 评论连续区块和分页 |
| `profile_edit_dialog.dart` | 编辑任务入口与 compact / bounded route shell |
| `profile_edit_controller.dart` | `GetMyProfile` 权威草稿、dirty、saving busy、校验提交与 issue |

`profile_page_test.dart` 只保留 library 入口，按 repository contract、controller / states、adaptive、edit protection、navigation / handoff 与 support / fixtures 拆分。拆分不得减少现有 32 个用例，所有改动 Dart owner 必须低于 `1500` 行。

## 6. 三档结构

页面继续消费既有 `RadishWindowClass`、`RadishContentFrame`、`RadishSectionSurface`、`RadishStateSlot` 和四主题 token，不建立第二套断点或页面壳层。

| 窗口 | 冻结结构 |
| --- | --- |
| compact `390px` | 单一连续信息流：身份 hero 与关键动作在前，统计状态随后；“我的”先展示设备最近与轻回应，再接公开帖子 / 评论。私域入口按任务分组，不保留长按钮 Wrap；无横向滚动。 |
| medium `800px` | 受控单主轴，身份、复访和公开活动按区块连续排列；动作可分行但不引入全局 rail，不把宽度浪费为多列卡片网格。 |
| expanded `1440px` | 继承 Discover / Forum Feed 的 `904px` 连续主轴；主轴承载身份与连续活动，右侧身份上下文只放公开统计 / 链接、编辑与刷新、真实私域去向和能力边界。Profile 不变成 WebOS 多窗口工作台。 |

公开主页不显示我的 quick replies、设备 recent 或私域入口；登录用户访问公开主页时只保留“回到我的主页”。“我的”与公开主页共享身份和公开活动组件，但文案、人称和动作显式区分。

## 7. 资料编辑任务

### 7.1 权威草稿

- 打开编辑任务后独立读取 `GetMyProfile`；读取成功前不使用公开摘要或空值拼装可写表单。
- dirty 由当前字段归一化后的 `UpdateMyProfileRequest` 与 `GetMyProfile` 基线精确比较：用户名 / 邮箱 / 地址按提交规则 trim，空地址归一为 `null`，未填写年龄与服务端未设置年龄保持同义。
- `UpdateMyProfile` 返回 `void`，不得用提交草稿直接改写公开页；保存成功关闭任务后由 Profile controller 重新读取公开身份和相关区块。
- 保存失败保留全部输入、dirty 和结构化 issue；不生成 API 未支持的 idempotency key，也不自动重复提交。

### 7.2 busy 与离开保护

- saving 时禁用字段、取消和重复保存；系统返回、手势返回与 route pop 均不能在请求未完成时销毁任务。
- 非 busy 且 dirty 时，取消、关闭和系统返回统一进入“丢弃更改”确认；确认后才退出，继续编辑则保留同一 controller 与焦点上下文。
- 无 dirty 时可直接退出；首次读取失败允许重试或安全关闭。
- compact 使用安全区内全屏可滚动任务面，键盘出现时仍可访问最后字段与保存动作；medium / expanded 使用最大约 `680px` 的 bounded task surface，并限制视口高度。
- 不依赖位移动效表达 dirty、busy 或错误；`disableAnimations` 下保护与反馈语义完整。

## 8. 实施门禁

1. 改造前 `32 / 32` Profile 用例全部保留；拆测试不得减少既有行为覆盖。
2. 新增 identity / stats / posts / comments / quick replies 首次 unavailable 与 refresh stale，确认某一区块失败不拖垮其他快照。
3. 覆盖跨用户迟到响应、本人退出 / 账号切换隔离、三种分页独立代际、重复页稳定 ID 去重和 append 失败保留。
4. 覆盖 `390 / 800 / 1440`、expanded `904px` 主轴、compact 键盘、长 ID / 名称 / 内容、四主题同构结构和无横向溢出。
5. 覆盖编辑 loading / unavailable / retry、dirty 精确判断、无改动直接退出、丢弃确认、saving busy 阻止 pop / 重复提交、保存失败保留和成功后权威刷新。
6. 保留公开链接复制、公开 / 我的复访、Forum / Docs target mapping、登录 / 退出和 Android Back / 来源返回现有契约。
7. 运行 Profile 定向、Shell 静态 Smoke、`flutter analyze`、全量 `flutter test`、文件行数、`npm run check:docs`、仓库卫生与 `git diff --check`。

## 9. 停止线

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖或 lockfile。
- 不新增头像上传、展示名新字段、密码修改、邮箱验证、关注 / 屏蔽 / 私信、宠物资料、账号设置或 Flutter Chat。
- 不改 Shell 导航、recent store、Forum Detail、Docs Reader、Browse History、Shop、Wallet、Experience、Leaderboard 或其他页面族。
- 不把完整服务端 Browse History 并入 Profile，也不把设备 recent shortcut 写回服务端。
- 不读取或修改 Pen；P5-B2 直接继承 P3 Identity / Revisit、P4 Theme / Shared 与 P5-B1 连续流。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；运行态验收继续独立授权。

按以上边界，P5-B2 的下一步是独立实施确认；若实现中发现必须改变 API、Shell target ownership、资料字段或上述范围，先停止并重新说明影响。
