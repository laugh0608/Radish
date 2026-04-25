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

## 2026-04-23 (周四)

### Flutter Android 原生 OIDC 闭环进入可人工收口状态

- **Flutter 当前已完成最小原生 OIDC 登录 / 登出闭环**：`58305fa7` 当前已补齐 Android 浏览器授权码登录、`radish://oidc/callback` 回跳换 Token、显式登出与 `radish://oidc/logout-complete` 回跳，原生端不再只停留在“能恢复已有会话”的阶段。
- **Android 模拟器人工联调当前已完成一轮真实收口**：登录成功、退出登录、浏览器取消登录、登录后杀后台重启会话恢复，以及登录后的用户信息读取当前都已验证通过；过程中暴露的模拟器 `Lost connection to device` 当前确认属于虚拟机侧瞬态问题，重启模拟器后已恢复。

### 壳层认证连续性与窄屏稳定性继续收口

- **Flutter 壳层当前已补齐登录取消提示与登录后原始目标续接**：`ccbfc890` 当前已让浏览器取消登录后显式展示 `Sign-in needs attention`；从 `profile` 或 forum detail 来源发起登录后，成功返回时会自动续接回原始 tab / handoff 目标，不再把登录做成孤立动作。
- **窄屏 Android 壳层状态区当前已完成一轮结构性修复**：登录状态、环境标识、`Resume forum` 与认证动作当前都已从 `AppBar.actions` 收口到页面顶部可换行状态条，避免再次出现 `RenderFlex overflow`。

### 文档与验证留痕

- **规划、鉴权说明与变更日志当前已按同一口径同步**：`planning/current`、`phase-two-flutter-client-mvp`、`guide/authentication` 与本周 changelog 当前都已回写“原生 OIDC 闭环、登录取消提示、窄屏壳层收口、登录后续接原目标”的最新事实。
- **当日验证当前已通过**：`Clients/radish.flutter` 下 `flutter test` 当前已通过；新增 smoke tests 也已覆盖 `profile` 登录后回原 tab 与 forum detail 登录取消后重试回原目标这两条关键路径。

## 2026-04-24 (周五)

### Flutter forum detail 最小原地登录入口落地

- **Flutter forum detail 当前已补齐可手工触发的最小原地登录入口**：匿名用户当前可直接在详情页发起 OIDC 登录，不再只能依赖壳层状态条或 profile 页间接触发登录。
- **详情来源登录目标当前也已提升为可持久化续接状态**：forum detail 发起登录时，原生壳层会把 `postId / commentId` 收口到持久化 follow-up 目标；浏览器往返、应用 `resume` 或壳层重建后，当前都能继续回到原帖子 / 原评论上下文。
- **原地登录与壳层续接之间的职责边界当前也已收口**：若详情页仍在前台，登录成功后会在原 detail 内完成状态切换并清理待续接目标，避免壳层重复再推入一层详情页。

### Android 真机人工联调收口

- **Android 真机当前已完成一轮围绕 detail 登录入口的手工联调**：forum detail 内登录、取消登录后的 `Sign-in needs attention` 提示、重试登录，以及成功后继续停留在当前 detail 上下文这几条主路径当前均已通过。
- **本轮人工联调当前也已额外确认大整数详情上下文未回退**：`postId / commentId` 当前继续保持字符串口径，登录往返后不会因对象 ID 被错误当作前端数值而丢失当前 detail 目标。

### 文档与验证留痕

- **规划、设计与进度文档当前已按同一口径同步**：`planning/current`、`development-plan`、`phase-two-flutter-client-mvp` 与 `frontend/design` 当前都已回写“forum detail 原地登录入口、持久化续接目标与真机联调通过”的最新事实。
- **当日验证当前已通过**：`Clients/radish.flutter` 下执行 `flutter test test/smoke_test.dart` 当前已通过；新增 smoke test 已覆盖 forum detail 内登录、取消后重试、成功回到当前帖子上下文这条关键路径。

## 2026-04-25 (周六)

### Android MVP 当前可测链路完成一轮人工验收

- **Android MVP 当前人工验证范围已正式收口到真实可测链路**：登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed、forum detail、评论阅读、评论分页与 detail 原地登录续接当前都纳入本轮验收。
- **本轮 Android 人工联调当前已通过**：用户侧确认登录和退出逻辑正常，forum 评论不显示的问题已修复并在真机确认；`notification / commentId` 宿主深链在本轮先从阻断项降级，随后已补最小 forum notification 来源并完成真机联调。
- **Android MVP 当前可测面已从“继续修主链问题”转入“选择下一批主线”**：后续不再继续沿当前 checklist 反复扩项，下一步应在扩展高价值只读页面与 Android MVP 打包 / 发布候选收口之间择一。

### 文档与验证留痕

- **Flutter Android 人工验收口径当前已回写到文档**：`Clients/radish.flutter/README.md` 已补当前真机 checklist，`planning/current`、`phase-two-flutter-client-mvp` 与 `validation-baseline` 已同步记录 Android MVP 当前可测链路与最小 forum notification 回流的验收口径。
- **本轮文档提交已完成**：`b61f22a0` 已提交 `docs(flutter): 收口 Android MVP 人工验证范围`，用于沉淀 Android MVP 当前可测链路与待补验边界。

### 最小 forum notification 来源开始接入

- **Flutter 已登录壳层当前已补齐一个最小 forum notification 来源**：原生端会读取当前用户最新通知列表，筛出 `voExtData.app = forum` 的通知，并把字符串化 `postId / commentId` 转成既有 `ForumDetailHandoffTarget(source: notification)`。
- **本轮继续保持“可测来源”边界，不扩完整通知中心**：当前只提供壳层轻入口 `Forum notification`，点击后打开 forum detail 与评论上下文；系统通知栏推送、通知列表管理、标记已读、删除通知和通知设置仍不纳入本批。
- **最小验证与人工联调当前已通过**：`Clients/radish.flutter` 下 `flutter test` 已通过；Android 平台侧使用 Android Studio JBR 执行 `.\gradlew.bat :app:testDebugUnitTest` 已通过；真机人工联调已确认 `Forum notification` 回到 forum detail / 评论上下文的逻辑正常。
