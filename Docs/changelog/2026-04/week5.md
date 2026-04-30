# 2026-04 第五周 (04-28 ~ 04-30)

## 2026-04-28 (周二)

### Flutter 第三批 Android 真机复核完成

- **Flutter MVP 第三批当前已完成一轮 Android 真机人工复核**：中文主文案、个人复访入口产品化与 forum detail 轻回应最小读写闭环在真实 Gateway 下确认正常，第三批不再停留在“代码已落地、待真实体验确认”的状态。
- **forum detail 轻回应链路当前通过真实设备确认**：详情页轻回应位于正文之后、评论区之前；匿名态可读取最近轻回应，已登录态可发布一句轻回应，并继续复用详情页原地登录续接。
- **个人复访入口当前可进入收口状态**：`profile` 内最近 forum 阅读与公开主页复访入口已按正式用户文案呈现，可作为后续第四批复访深化的基础，而不是继续保留开发态验证说明。

### 同一帖子详情重复打开回归修复

- **本轮真机复核发现并修复 forum handoff 去重状态问题**：从帖子详情返回列表后再次点击同一帖子，曾因 `ForumPage` 本地 `_handledHandoffSignature` 未随 Shell 消费 `handoffTarget` 清空而被误判为已处理目标，导致无法再次进入详情。
- **当前修复已收口到 handoff 消费边界**：当 `handoffTarget` 从非空变成 `null` 时，`ForumPage` 会同步清空已处理签名；点击其他帖子才能恢复进入的偶发现象不再作为正常行为残留。
- **本轮已补定向回归测试**：`forum_page_test.dart` 当前覆盖“同一 forum detail target 被消费后仍可再次打开”的场景，避免后续继续在 handoff 去重状态上回退。

### 验证与第四批判断

- **本轮定向验证已通过**：`Clients/radish.flutter` 下执行 `flutter test test/forum_page_test.dart` 已通过。
- **第四批当前建议从复访深化切入**：优先围绕个人页承载我发布的轻回应 / 最近轻回应上下文，并继续复用现有 forum detail handoff 回到原帖；不同时扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理。

### Flutter 第四批复访深化开始落地

- **Flutter 第四批当前已从“我的轻回应回看”切入**：已登录用户查看我的 `profile` 时，原生端会读取现有 `/api/v1/PostQuickReply/GetMine` 契约，展示最近轻回应、原帖标题和发布时间；公开主页不展示该个人区块。
- **我的轻回应当前复用既有 forum detail handoff 回到原帖**：轻回应条目不新建独立详情页，不复制 WebOS 工作台逻辑，而是新增 `myQuickReply` 来源标签后继续打开同一套原生帖子详情。
- **本轮边界继续收紧**：第四批首个小闭环只做回看与回到上下文，不开放删除、举报、完整评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test` 与 `flutter analyze` 均已通过；Android 真机复核也已确认我的轻回应读取、公开主页隐藏与回到原帖 handoff 正常。

### Flutter 第四批我的轻回应分页复访

- **我的轻回应回看已从首屏预览推进到分页复访**：已登录用户查看我的 `profile` 时，原生端仍复用 `/api/v1/PostQuickReply/GetMine`，当前可在“我的轻回应”区块继续加载更多轻回应。
- **分页失败保持区块内降级**：加载更多失败时只在“我的轻回应”区块展示局部错误，公开资料、公开帖子与公开评论仍保持 ready 状态。
- **回到原帖路径继续复用既有 handoff**：分页加载出的轻回应条目仍通过 `ForumDetailHandoffSource.myQuickReply` 回到同一套原生 forum detail，不新增独立详情页。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test`、`flutter analyze` 与仓库根目录 `git diff --check` 均已通过；Android 真机复核也已确认分页复访、公开主页隐藏与回到原帖 handoff 正常。

### Flutter 第四批最近公开评论分页复访

- **profile 最近公开评论已补齐分页复访**：公开主页与我的主页的“最近公开评论”区块当前可继续加载更多公开评论，仍复用现有 `/api/v1/Comment/GetUserComments` 契约。
- **评论回看继续落到原生 forum detail**：分页追加出的评论条目仍通过 `ForumDetailHandoffSource.publicProfileComment` 回到对应帖子与评论上下文，不新增独立评论详情页。
- **目标评论定位已补异步渲染重试**：真机复核发现 profile 评论条目能打开对应帖子但默认停在顶部；本轮修复 forum detail 在目标根评论页 / 子评论页异步补载完成后的滚动时机，不改 profile handoff 与后端契约。
- **分页失败保持区块内降级**：加载更多评论失败时只在“最近公开评论”区块展示局部错误，不拖垮公开资料、最近公开帖子或我的轻回应区块。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test`、`flutter analyze` 与仓库根目录 `git diff --check` 均已通过；Android 真机复核也已确认公开评论条目可打开对应帖子，并自动定位到目标根评论或子评论位置。

## 2026-04-29 (周三)

### Flutter 第四批最近公开帖子分页复访

- **profile 最近公开帖子已补齐分页复访**：公开主页与我的主页的“最近公开帖子”区块当前可继续加载更多公开帖子，仍复用现有 `/api/v1/Post/GetUserPosts` 契约。
- **帖子回看继续落到原生 forum detail**：分页追加出的帖子条目仍通过 `ForumDetailHandoffSource.publicProfilePost` 回到同一套原生帖子详情，不新增独立详情页或个人页专属打开路径。
- **分页失败保持区块内降级**：加载更多帖子失败时只在“最近公开帖子”区块展示局部错误，不拖垮公开资料、最近公开评论或我的轻回应区块。
- **profile 来源返回栈已收口**：从“最近公开帖子 / 最近公开评论 / 我的轻回应”打开原生帖子详情时，Shell 当前保留“我的”tab 上下文；详情返回后会回到 profile，而不是落到论坛首页。
- **三块复访区 footer 行为已对齐**：最近公开帖子、最近公开评论和我的轻回应当前只在还有更多、正在加载或局部失败时展示加载 footer；加载到最后一页后不再保留无动作的“已显示 x / x”尾巴。
- **本轮边界继续收紧**：仍只做“回看 + 继续加载 + 回到上下文”的复访闭环，不开放发帖、评论提交、点赞、删除、举报、编辑治理、完整通知中心或系统通知栏推送。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test` 与 `flutter analyze` 均已通过；仓库根目录 `git diff --check` 已通过。Android 真机复核已确认最近公开帖子、最近公开评论与我的轻回应三条 profile 复访路线均可从详情返回到 profile。

### Flutter 第四批最近阅读上下文入口

- **profile 最近阅读上下文已接入**：已登录用户查看我的 `profile` 时，当前会展示最近一次 forum 阅读目标，可直接继续打开对应帖子或评论上下文。
- **最近阅读继续复用既有 handoff 与存储**：本轮不新增存储协议，仍复用壳层已有最近阅读目标；从 profile 内点击时会临时使用 `profileRecentBrowse` 来源，以便详情返回后仍回到 profile。
- **本轮边界继续收紧**：最近阅读只保留单个上下文入口，不扩展完整浏览历史中心、多条记录列表、删除、清空、同步治理或系统通知栏能力。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test`、`flutter analyze` 与仓库根目录 `git diff --check` 均已通过；Android 真机复核已确认最近阅读入口可回到对应详情，返回后仍留在 profile。

### Flutter 第四批 docs 最近阅读与直达复访

- **docs 最近阅读已接入原生壳层**：`discover` 文档精选可直接打开 Flutter 原生 docs 详情，我的 `profile` 当前也会展示单个最近公开文档入口，方便继续阅读。
- **docs 详情来源返回栈已补齐**：从 discover 打开文档会返回 discover，从 profile 最近文档打开会返回 profile，从 docs 列表打开仍回到 docs 列表；最近文档 target 已在 Android 侧持久化。
- **本轮边界继续保持只读**：不扩展文档搜索增强、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心、多条记录列表或删除 / 清空治理。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test`、`flutter analyze` 与仓库根目录 `git diff --check` 均已通过；Android 真机复核确认 discover 文档直达、docs 列表详情返回、profile 最近文档复访与重启恢复正常。

### Flutter 第四批 docs 正文内链与关键词搜索复访

- **docs 正文内公开文档链接已接入原生跳转**：公开文档正文里的 Markdown 文档链接与裸 `/docs/:slug` 路径会继续打开 Flutter 原生 docs 详情；代码块里的文本仍保持只读，不参与内链跳转。
- **docs tab 已接入关键词搜索复访**：原生 docs 列表当前通过现有 `/api/v1/Wiki/GetList?keyword=...` 查询公开文档，搜索结果继续复用原生卡片、分页、刷新与详情打开路径。
- **搜索与详情返回真机问题已完成修复**：Android 真机复核发现搜索结果长 slug 在窄屏溢出、搜索详情返回会退出到桌面；本轮已补长 slug 省略展示、docs 内联详情系统返回拦截与回归测试，复测确认搜索 -> 详情 -> 返回可回到搜索结果。
- **调试态根层返回已按原生生命周期收口**：Flutter Android debug 期间，根层按返回键回桌面后再次点图标曾卡在原生启动页；本轮新增 `app_lifecycle` platform channel，将根层 Back 收口为 Android `moveTaskToBack(true)`，避免直接 finish Activity。
- **本轮验证已通过**：`Clients/radish.flutter` 下执行 `flutter test test/docs_page_test.dart test/smoke_test.dart`、`flutter analyze`、`flutter test`、仓库根目录 `git diff --check` 与 Android 平台 `.\gradlew.bat :app:testDebugUnitTest` 均已通过。

## 2026-04-30 (周四)

### Flutter 第四批 discover 论坛精选直达

- **discover 论坛精选当前已可直达原生帖子详情**：发现页中的论坛精选帖子新增“打开帖子”动作，点击后会生成 `ForumDetailHandoffSource.discover` 来源的 `ForumDetailHandoffTarget`，继续复用同一套原生 forum detail，而不是新建 discover 专属详情页。
- **发现页来源返回栈已对齐 docs 直达口径**：从 discover 打开 forum detail 时，Shell 会保留当前发现页 tab；详情返回后仍落回 discover，不会丢到论坛首页。
- **discover 快捷入口根层返回已补齐**：真机复核发现从 discover 点击“进入论坛 / 进入文档”后，在列表根层按 Android 返回键会直接退到桌面；本轮已为这两个 discover 快捷入口补轻量返回目标，Back 会先回 discover，底部导航手动切 tab 仍保持原行为。
- **本轮边界继续收紧**：仍只做公开只读阅读复访，不开放发帖、评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送。
- **本轮验证已通过**：已补 `discover_page_test.dart` 与 `smoke_test.dart` 定向覆盖；当前已通过 `flutter test test/smoke_test.dart`、`flutter analyze` 与仓库根目录 `git diff --check`。

### Flutter 第四批收口结论

- **第四批“复访深化 + 已登录轻互动回看”当前可收口**：`profile` 我的轻回应、最近公开评论、最近公开帖子、最近 forum 阅读、最近 docs 阅读、docs 正文内链、docs 关键词搜索、discover 文档直达、discover 论坛精选直达与 discover 快捷入口返回上下文均已完成代码、自动化验证与 Android 真机复核。
- **本批真机问题已全部回写为修复与测试**：目标评论定位时机、docs 搜索详情返回、长 slug 溢出、调试态根层返回重开卡启动页，以及 discover 论坛 / 文档快捷入口根层 Back 直接退桌面问题，均已完成收口。
- **第五批只进入候选判断池**：后续候选为 `profile` 最近阅读轻量多条列表、docs 搜索体验小增强、forum detail 轻回应发布后局部体验补强、Android MVP 收口复核与 RC 前置清单整理；当前不把发帖、完整评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送拉入下一批默认范围。
