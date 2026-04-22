# 2026-04 第四周 (04-20 ~ 04-27)

## 2026-04-20 (周一)

### `Phase 2-3` 公开只读页面第二批接线继续推进

- **Flutter `discover / docs / profile` 当前已完成最小真实页面接线**：原生壳层不再只停留在占位说明，`discover` 已接通真实摘要分发入口，`docs` 已接通公开文档列表读取，`profile` 已接通公开资料读取，第二批真实业务接线范围继续保持在高价值只读边界内。
- **Flutter 业务接线当前继续刻意保持“先收口，再扩张”**：forum、discover、docs、profile 目前都只承载公开阅读，不同时进入详情树、编辑、创作器、商城工作台或完整桌面语义，避免在 Android 起步阶段把 Flutter 拉成“移动版 WebOS”。

### Android 平台目录与 Gateway 联调入口收口

- **Flutter Android 平台目录当前已正式生成并纳入仓库**：Android debug APK 构建已通过，`Clients/radish.flutter` 不再停留在“只有 Dart 壳层，没有 Android 工程”的状态。
- **Android 模拟器联调入口当前已统一对齐到 Gateway `https://localhost:5000`**：Flutter 开发态不再默认直连 `5100 / 5200`；API、Auth 与 Gateway 基址当前都统一收口到 Gateway 对外入口。
- **Android 模拟器联调排障口径当前已明确写入文档**：启动应用前需要先执行 `adb reverse tcp:5000 tcp:5000`，以保持模拟器侧 `localhost:5000` 映射到宿主机 Gateway，并避免 `10.0.2.2` 与本地开发 HTTPS 证书主机名不一致导致 TLS 握手失败。

### 最小会话恢复边界从内存占位推进到真实持久化

- **Flutter 会话恢复链路当前已完成第一轮真实闭环**：原生壳层已补齐 Android 本地会话持久化、应用启动恢复、Access Token 过期判断，以及 refresh token 刷新失败后的匿名回落，会话恢复不再只是 `InMemorySessionStore` 占位。
- **当前仍刻意不进入完整登录 UI 与浏览器 OIDC 回调全链路**：本轮只补“已有会话如何恢复”的真实边界，显式登录入口、完整 OIDC 浏览器跳转、登出治理与更多账号动作继续留待下一轮推进，避免同时拉入 `intent-filter`、外部浏览器回调与更多原生集成风险。

### 文档、验证与下一顺位

- **规划与说明文档当前已完成一轮同步**：Flutter README、Gateway 指南、`planning/current`、`phase-two-community-multiplatform` 与 `phase-two-flutter-client-mvp` 当前都已回写 Android 联调与会话恢复的最新事实。
- **本轮最小验证已通过**：`flutter analyze`、`flutter test` 与 `flutter build apk --debug` 当前均已通过；测试侧已覆盖“无会话 / 有效会话 / 过期刷新成功 / 过期刷新失败”这四条主路径。
- **下一步主线当前已进一步收束**：在“会话能存、能恢复、能刷新”之后，后续更适合优先补最小登录发起与回写闭环，让 Flutter 会话边界从“能恢复已有会话”推进到“能真实获得会话”。

## 2026-04-21 (周二)

### Flutter forum 公开只读阅读链路继续推进

- **Flutter forum 当前已从“帖子详情只读阅读”继续推进到“评论链路只读阅读”**：根评论分页、子评论分页、基础加载 / 错误态，以及作者 / 评论作者原生公开 profile 跳转当前都已落地，原生端不再只停在“能看帖子正文”的阶段。
- **原生 forum 阅读边界当前继续保持刻意收紧**：本轮仍只承载公开只读阅读，不开放评论提交、点赞、投票、编辑或登录治理，也不为 Flutter 单独设计新的 BFF。

### 评论精确定位与外部 handoff 最小闭环收口

- **Flutter forum 评论精确定位最小闭环当前已落地**：带 `commentId` 打开帖子详情后，原生端会先复用公开评论定位接口解析目标位置，再自动补载目标根评论页与子评论页，最后滚动到目标评论。
- **Flutter forum 外部详情 handoff 当前也已完成最小收口**：原生壳层已支持从 shell 层直接透传 `postId + commentId` 打开 forum 详情，并复用既有评论定位链路落到目标评论，不需要为这一批单独重做新的路由系统。
- **这批 handoff 当前仍明确只做“入口贯通”，不做“来源泛化”**：通知、浏览回跳、公开 profile 评论入口等真实来源会放到下一批继续统一接线，本轮先把 shell 内部的 handoff 目标和详情页参数贯通收口。

### 当日提交、文档与验证留痕

- **今日 Flutter 提交当前集中围绕 `Phase 2-3` forum 高价值阅读链路展开**：`a79c565e`、`c7ec714d`、`f74508d8`、`986f72f4`、`94a4293c`、`e76aca80`、`a068f009`、`276a3e08`、`55e60c3f` 与 `09d3acd1` 已把 Flutter 第二批从 discover / docs / profile 首批真实页面，继续推进到 forum 详情、评论阅读、评论定位与外部 handoff。
- **规划与日志文档当前已按同一口径同步**：`planning/current`、`development-plan` 与 `phase-two-flutter-client-mvp` 当前都已回写 Flutter forum 的最新评论阅读能力、评论精确定位闭环与外部详情 handoff 的最新事实，避免规划页仍停留在“先补 discover / docs / profile”的旧口径。
- **当日验证当前已通过**：在 `Clients/radish.flutter` 下执行 `flutter test` 已通过，确认 forum 外部 handoff、评论精确定位，以及既有 discover / docs / profile / session smoke tests 当前都保持通过。

## 2026-04-22 (周三)

### Flutter forum 原生 handoff 继续统一到真实来源

- **Flutter public profile 当前已不再发散出独立 forum 详情跳转**：最近帖子与最近评论入口当前都已统一复用共享 `ForumDetailHandoffTarget`，并继续保持 `postId / commentId` 字符串口径，不回退到数值型外部 ID 解析。
- **discover 最小来源入口当前只保留为过渡验证事实，不再继续承担长期来源职责**：通知 / 浏览回跳此前已先经 discover 最小入口卡完成一轮共享 handoff 验证，但这层演示入口当前已从 Flutter 壳层回收，不再作为长期产品形态保留。

### 通知宿主 handoff 与 browse history 壳层续接落地

- **Android 宿主当前已支持以启动 handoff 透传 forum 通知来源**：`MainActivity` 当前已新增 forum follow-up channel，可在应用启动或前台重新唤起时读取 `postId + commentId + source`，并直接落到原生 forum detail。
- **原生壳层当前也已补齐“最近阅读继续”这一条 browse history 续接链路**：forum detail 打开后会把最近一次阅读目标收口到壳层 follow-up store，后续可通过壳层 `Resume forum` 轻入口继续阅读，而不需要先做完整浏览记录页。
- **forum detail 打开路径当前也已完成一轮统一回收**：forum feed、public profile、notification 宿主 handoff 与 browse history 壳层续接当前都已统一接到同一套原生 handoff 目标，不再并存多套 detail 打开路径。

### 当日提交、文档与验证留痕

- **今日 Flutter 提交当前形成三段式收口**：`85d22f66` 已统一 profile 的 forum handoff，`98131dc3` 已补 discover 的最小来源验证入口，后续代码批次当前已进一步把 notification / browseHistory 从 discover 演示层推进到宿主 / 壳层真实接线。
- **规划与日志文档当前已按最终收口状态同步**：`planning/current`、`development-plan`、`phase-two-flutter-client-mvp` 与本周 / 本月 changelog 当前都已回写“profile 统一 handoff、discover 过渡验证、宿主 / 壳层真实接线收口”的最新事实。
- **当日验证当前已通过**：在 `Clients/radish.flutter` 下执行 `flutter test test/discover_page_test.dart test/profile_page_test.dart test/forum_page_test.dart test/forum_detail_page_test.dart test/smoke_test.dart` 与 `flutter build apk --debug` 当前均已通过。
