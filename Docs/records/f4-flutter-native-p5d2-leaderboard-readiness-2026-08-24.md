# Flutter Native P5-D2 Leaderboard 实施就绪与方案冻结

> 状态：`P5-D2 readiness` 已完成；等待项目所有者确认实施
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-D1 Wallet / Experience 实现](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)

## 1. 本批结论

P5-D2 可以完全复用既有匿名 `Leaderboard/GetLeaderboard`、统一排行榜 VO、Shell Leaderboard → Public Profile handoff 与 Android Back 回流完成，不需要新增后端 API、DTO、权限、数据库、migration、登录态或移动端 BFF。

Flutter 的长期产品边界继续服从 [F4-S 公开排行榜治理](/features/leaderboard)：只请求 `Experience = 1`、`pageIndex = 1`、`pageSize = 20`，只提供公开只读经验榜首屏和公开主页回访；不新增 Web 已有的其他榜单 Tab、类型元数据、“我的排名”、分页或热门商品，不把 Flutter 机械追平 Web。

主要缺口在页面状态、公开身份映射和自适应呈现。当前 `leaderboard_page.dart` 以 widget 字段同时承担请求代际、刷新、错误和全部 UI；同一固定单列 Card 结构覆盖所有宽度。服务端 `VoThemeColor` 被直接用作含文字的实心排名徽章背景，而统一 VO 已返回的 `VoUserPublicId / VoUserDisplayName / VoUserDisplayHandle` 尚未映射，公开主页仍优先收到内部数字 ID。

本次只完成代码 / API / 导航 / 主题事实审计、改造前测试基线与方案冻结，没有修改 Dart、后端 API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与行为

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `leaderboard_page.dart` | 首次读取、刷新、request ID、错误字符串、全部榜单 widget 与颜色解析 | `520` 行 |
| `leaderboard_repository.dart` | 匿名请求经验榜第一页 | `55` 行 |
| `leaderboard_models.dart` | 榜单项与经典分页模型 | `173` 行 |
| `leaderboard_page_test.dart` | 成功、公开主页动作、失败与 HTTP query | `199` 行、`4` 个用例 |

当前页面固定请求经验榜第一页 20 条，首次失败会显示整页错误；已有快照刷新失败时仍保留列表，但 busy、stale 与错误都由 widget 字段组合表达。request ID 和 `mounted` 已能拒绝同一 widget 的旧响应，repository 变化会触发重读；这些行为需要迁入独立 owner 并补足可验证状态，而不是重写为全局 store。

当前模型只映射 `voUserId / voUserName / level / avatar / themeColor / primary / secondary`。服务端统一 VO 还稳定返回：

- `VoUserPublicId`：合法时为 `usr_` + 32 位十六进制，公开 Profile 应优先消费；
- `VoUserDisplayName`：当前公开展示名；
- `VoUserDisplayHandle`：公开显示句柄；
- 数字 `VoUserId`：只作为缺失公共标识时的兼容入口，继续按字符串保留。

`UserService.GetPublicUserByIdentifierAsync` 同时接受合法 PublicId 与正整数内部 ID，因此模型修正不需要修改 Profile API 或 Shell route。头像 URL 当前虽已映射但未加载；P5-D2 不借布局改造新增远程媒体读取、缓存、占位或失败策略。

### 2.2 改造前测试基线

执行：

```text
flutter test test/leaderboard_page_test.dart test/smoke_test.dart
```

Leaderboard `4 / 4`、Shell Smoke `51 / 51`，合计 `55 / 55`。现有覆盖保留经验榜渲染、刷新失败、`type=1&pageIndex=1&pageSize=20` 和 Leaderboard → Public Profile → Back 的真实 Shell 回流。

当前没有直接覆盖：独立 owner 的 initial / ready / empty / unavailable / stale / recover；repository generation 与 dispose 迟到响应；PublicId / display name / handle 映射和数字 ID fallback；`599 / 600 / 1024 / 1280` 三档结构；四主题同构；非法 / alpha / 极端业务色；超长排名、身份、等级与指标无横向溢出。

## 3. API、身份与导航冻结

P5-D2 只消费：

```text
GET /api/v1/Leaderboard/GetLeaderboard?type=1&pageIndex=1&pageSize=20
```

- endpoint 保持匿名，不接 access token，不建立当前账号或“我的排名”状态。
- 不请求 `GetTypes` 或 `GetMyRank`；不新增 load more、页码、筛选、其他榜单或商品卡。
- 页面摘要可以展示“当前 20 条 / 共 N 位”，但不能把服务端 `dataCount / pageCount` 解释为已经实现分页。
- `LeaderboardItem` 补映射 public identity 字段。展示名优先级固定为 `userDisplayName → userName → 合法 PublicId / 数字 userId fallback → 匿名用户`；句柄独立呈现，不与展示名拼成新的权威身份。
- `profileTarget` 优先合法且标准化小写的 `usr_` PublicId，其次为正整数数字 ID 字符串；两者都无效时隐藏公开主页动作，不能传空值或错误标识。
- Shell 继续把 Leaderboard 作为来源 tab，打开既有 Public Profile owner；Android Back 返回 Leaderboard，不建立第二套 Navigator 或 Profile 详情页。

## 4. 权威状态与 owner 冻结

新增页面级 `LeaderboardController`，只拥有一个公开首屏 snapshot：

- 状态为 `idle / loading / ready / empty / unavailable / stale`，并独立表达 refresh busy 与结构化 `LeaderboardIssue`；不再以可空 page、多个 bool 和字符串错误隐式拼状态。
- 首次成功的空列表是权威 `empty`；同 repository 刷新失败保留最后 ready 或 empty 快照并进入 `stale`，恢复成功替换整个第一页。
- repository 变化或显式 reopen 增加 generation 并清空旧 target；dispose 增加 generation，旧请求不得提交或通知。
- 页面只负责 controller 生命周期、repository 变化、刷新和公开主页 handoff；surface 只消费状态并呈现。
- 不为单页引入通用分页基类、跨页面状态机或全局 store。P5-D2 的首屏只读边界应直接体现在 owner 命名和 API 上。

实施 owner 固定为：

| Owner | 目标职责 |
| --- | --- |
| `leaderboard_page.dart` | route、controller 生命周期、repository reopen、刷新与 Profile handoff |
| `leaderboard_controller.dart` | 首屏权威 snapshot、结构化状态、generation / dispose 隔离 |
| `leaderboard_issue.dart` | API、响应格式与请求错误的稳定分类 |
| `leaderboard_surface.dart` | 页面头、状态、摘要、连续排名与三档结构 |
| `leaderboard_models.dart` | 公共身份映射、展示 fallback 与 profile target |

## 5. 三档结构与主题边界

| 窗口 | 冻结结构 |
| --- | --- |
| compact `<600px` | 页面头、刷新 / 状态摘要和连续紧凑排名按单列任务顺序呈现；每行保持 rank、身份、等级、指标和不小于 `48px` 的公开主页动作，长值允许换行 |
| medium `600–1023px` | 主轴受控居中，排名改为密集列表行并保持完整身份 / 等级 / 指标；不在 `600px` 强塞上下文 rail |
| expanded `>=1024px` | 不超过约 `904px` 的排名主轴 + `24px` 间距 + `280–300px` 榜首公开身份上下文；上下文只复用第一页首个合法 Profile target，不发起新请求，列表仍可打开任一合法公开主页 |

页面统一消费 `RadishWindowClass`、`RadishContentFrame`、Theme token、`RadishSectionSurface`、`RadishStateSlot` 与 `RadishStateChip`，不建立第二套断点或主题分支。

`VoThemeColor` 只允许严格 `#RRGGBB`。合法值只作为无文字的小型装饰 accent，例如身份标记边线 / 色点；非法值、alpha 色或其他格式直接回落到语义 token。rank、文字、按钮、状态与可交互 surface 全部使用主题语义颜色，业务色不得成为含文字的实心背景、焦点、错误、成功或选中状态。这样既保留等级主题提示，又不让服务端颜色绕过四主题对比度边界。

## 6. 实施门禁

1. 先补 controller 的 initial success / empty / unavailable / recover、ready / empty 刷新 stale、repository generation 与 dispose 迟到响应。
2. repository / model 覆盖匿名 endpoint、固定 `type=1 / pageIndex=1 / pageSize=20`，以及 PublicId、display name、display handle、数字 ID fallback 与非法 target。
3. 精确覆盖 `599 / 600 / 1024 / 1280`，并覆盖四主题、compact 超长 rank / identity / handle / level / metric 无横向溢出。
4. 覆盖合法极端 `#RRGGBB`、非法字符串和 alpha 色，证明业务色不进入含文字 / 交互 surface；四主题中排名与动作仍由语义 token 承载。
5. 现有 Leaderboard `4 / 4` 与 Shell Smoke `51 / 51` 不得减少；实施后执行 P5-D2 定向、Shell、Flutter 全量与 `flutter analyze`。
6. `dart format`、改动 Dart owner `<1500`、文档、仓库卫生与 `git diff --check` 通过。

## 7. 停止线与下一步

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile 或移动端 BFF。
- 不新增其他榜单 Tab、`GetTypes`、`GetMyRank`、分页 / load more、搜索、筛选、赛季、奖励、分享、关注、治理或写入。
- 不公开余额、消费、购买数据，不扩私域经验详情，不加载远程头像或等级媒体。
- 不提前进入 P5-D3 Browse History、P5-E 成组门禁、平台工程或分发。
- 不读取或修改 Pen；P5-D2 直接继承 P3 方向和 P4 Theme / Shared / Shell。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；阶段运行态验收继续独立授权。

P5-D2 readiness 已关闭。下一步等待项目所有者确认后，按本记录先补模型 / controller 测试，再拆页面 owner、实现三档 surface 并完成静态回归。P5-D3、P5-E、服务启动与真实运行态 Smoke 不随该确认自动扩张。
