# Flutter Native P5-D3 Browse History 实施就绪与方案冻结

> 状态：`P5-D3 readiness` 已完成；等待实施确认
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-D2 Leaderboard 实现](/records/f4-flutter-native-p5d2-leaderboard-implementation-2026-08-24)

## 1. 本批结论

P5-D3 可以完全复用既有私域 `User/GetMyBrowseHistory`、服务端 `UserBrowseHistory` 聚合记录和现有 Forum / Docs / Shop 原生详情 handoff 完成，不需要新增后端 API、DTO、权限、数据库、migration、移动端 BFF、依赖或平台通道。

服务端完整浏览历史与设备 recent shortcut 是两类不同产品数据：前者属于当前账号、需要登录、覆盖 Post / Wiki / Product、支持分页并由服务端按最后访问时间排序；后者只是本机 Forum / Docs 最多各 `5` 条的快速回访上下文，没有商品、服务端总数、分页或跨设备同步语义。P5-D3 只改完整浏览历史 route，不读取、合并、迁移、清空或重写设备 recent store。用户从完整历史主动打开 Forum / Docs 后，既有原生详情链路可以像其他入口一样自然更新设备快捷记录，但页面不主动建立双向同步。

服务端已经按 `(UserId, TargetType, TargetId)` 聚合同一目标：重复访问更新标题 / 摘要 / 封面 / route 快照、递增 `ViewCount` 并刷新 `LastViewTime`；分页固定按 `LastViewTime DESC, Id DESC`。因此客户端不得再按标题、route 或领域 target 做第二次内容合并。append 只以服务端记录 `VoId` 作为稳定身份去重，解决动态时间排序导致相邻分页重叠；refresh 仍整体替换第一页并重置分页。

主要缺口位于 Flutter owner 与呈现：当前 `600` 行页面同时承担请求代际、分页、错误字符串、导航和全部 widget；append 直接 `addAll`，账号 / credential 与 dispose 隔离没有独立测试；固定单列 Card 覆盖所有宽度，原始 route 和时间字符串直接回显，也没有明确说明“账号浏览历史”与“设备最近阅读”的差别。

本次只完成代码 / API / 数据 / 导航事实审计、改造前测试基线与方案冻结，没有修改 Dart、后端 API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与服务契约

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `browse_history_page.dart` | 首次读取、refresh / append、request ID、错误字符串、三类详情导航与全部 widget | `600` 行 |
| `profile_models.dart` | Profile 全部模型；Browse History item / page、route 末段 fallback 与可打开判断 | `549` 行 |
| `profile_repository.dart` | Profile 公开 / 私域读取与资料更新；浏览历史带 bearer token 分页读取 | `206` 行 |
| `browse_history_page_test.dart` | 三类渲染 / handoff 与刷新失败保留 | `237` 行、`2` 个用例 |
| `UserBrowseHistoryServiceTest.cs` | 新增快照、重复访问聚合与 Post PublicId route 更新 | `170` 行、`3` 个用例 |

P5-D3 只消费：

```text
GET /api/v1/User/GetMyBrowseHistory?pageIndex={n}&pageSize=20
Authorization: Bearer {accessToken}
```

- endpoint 使用 `AuthorizationPolicies.Client`，只读取 `Current.UserId`，Flutter 不传用户 ID query。
- Controller 与 Service 都把页码下限收敛为 `1`，page size 收敛到 `1–100`；Flutter 固定使用 `20`。
- 响应继续使用 `VoPagedResult<UserBrowseHistoryVo>` 的 `VoItems / VoTotal / VoPageIndex / VoPageSize`；Flutter 可据此计算 `pageCount`，不新增 cursor。
- `VoId / VoTargetId` 是后端 `long`，Flutter 保持十进制字符串并校验正值，不转换为可能截断的普通数值。
- `VoCoverImage` 继续只映射，不在 P5-D3 新增远程图片读取、缓存、占位或失败 owner。

服务端定向 `3 / 3` 已验证新增记录标准化、同目标更新并递增次数，以及旧数字 Post route 更新为 PublicId route；本批不改这些服务端事实。

### 2.2 两类 recent 的边界

| 维度 | 服务端完整浏览历史 | 设备 recent shortcut |
| --- | --- | --- |
| 当前入口 | 我的 → 查看账号浏览历史 | 我的页面内“最近阅读 / 最近文档”（设备快捷） |
| 数据 owner | `UserBrowseHistoryService` + `ProfileRepository` | `ForumFollowUpStore` / `DocsFollowUpStore` |
| 范围 | Post / Wiki / Product | Forum 最多 `5` 条、Docs 最多 `5` 条 |
| 身份与排序 | 当前登录账号；`LastViewTime DESC, Id DESC` | 本机最近写入顺序；领域 target 去重 |
| 数量与分页 | 服务端 total + `20` 条分页 | 固定小列表，无服务端 total / page |
| P5-D3 行为 | 独立 route 内读取、刷新、append、原生打开 | 只通过文案说明，不读、不并、不写、不清 |

Profile 内现有“最近阅读”区继续属于设备快捷上下文。完整历史 route 的产品标题统一为“账号浏览历史”，说明其可在登录设备间读取；不再让两个入口都只以“最近访问”命名。这个文案差异不改变 Store、Shell tab 或详情来源返回 owner。

### 2.3 改造前测试基线

执行：

```text
flutter test test/browse_history_page_test.dart test/smoke_test.dart
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter FullyQualifiedName~UserBrowseHistoryServiceTest --verbosity minimal
```

Browse History `2 / 2`、Shell Smoke `51 / 51`，Flutter 合计 `53 / 53`；服务端契约 `3 / 3`。现有 Shell 覆盖我的 → 完整历史、Forum / Docs / Product 详情与 Android Back 回流，以及设备 Forum / Docs recent store 的最新优先和领域去重。

当前没有直接覆盖：独立 owner 的 initial / empty / unavailable / recover；ready / empty refresh stale；append issue / retry 与跨页稳定 ID 去重；account / credential generation、repository 重建与 dispose 迟到响应；三类 typed target 的合法 / fallback / 非法边界；`599 / 600 / 1024 / 1280` 三档结构、四主题同构、超长标题 / 摘要 / route / 指标与无横向溢出。

## 3. 目标解析与原生 handoff 冻结

Browse History item 保留完整服务端快照，但“可打开目标”改为按领域显式解析，不再把任意 route 最后一个片段视为通用目标：

- Post：优先识别内部 `/forum/post/{id}` route 中合法 `pst_` PublicId 或正整数 ID，其次回落正整数 `VoTargetId`；任意外部 URL、空值或非法标识不进入 Forum handoff。
- Wiki：优先非空 `VoTargetSlug`，其次只接受内部 `/docs/{slug}` 或历史 `/wiki/doc/{slug}` route；缺少合法 slug 时禁用打开，不把数字 `VoTargetId` 错当 `GetBySlug` 参数。
- Product：只使用正整数 `VoTargetId`，route 仅是服务端展示快照，不覆盖权威商品 ID。
- 未知类型或目标无效时仍保留历史行和服务端快照，但动作显示不可用原因；不静默丢记录，也不提交空 target。

内部 route / LongId 不再作为普通可读正文持续展示。surface 用领域标签、标题、摘要、浏览次数和本地可读的最后访问时间表达记录；合法目标提供“打开帖子 / 文档 / 商品”动作。时间先解析服务端 ISO 值并按本地时间呈现，非法值回落稳定的“时间未知”，不把格式错误扩散为页面崩溃。

导航继续复用既有 owner：

- Post 使用 `ForumDetailHandoffTarget` 和 `profileRecentBrowse` 来源；
- Wiki 使用 `DocsDetailHandoffTarget` 和 `profileRecentDocument` 来源；
- Product push 既有 `ShopProductDetailPage`，沿用 Shop / Wallet repository、当前 credential 与购买能力边界；
- Android Back / Navigator pop 返回同一账号历史 route，不新建详情实现、嵌套 Navigator 或 Web URL fallback。

## 4. 权威状态与 owner 冻结

新增页面级 `BrowseHistoryController`，状态固定包含 account target、items、page、page size、page count、data count、initial issue、refresh issue、append issue，以及 loading / refreshing / appending：

- `idle / loading / ready / unavailable` 是基础状态；ready 空列表派生 empty，ready + refresh issue 派生 stale，不再由 widget 内多个 bool 和字符串隐式拼接。
- 首次成功空页是权威 empty；ready 或 empty snapshot 刷新失败都保留原 snapshot 与分页并呈现 stale，恢复成功整体替换第一页。
- append 成功按 `VoId` 稳定去重：保留已有顺序，重复 ID 不追加，page 仍推进到服务端返回页；不按 title、target、route 或时间重排本地快照。
- append 失败保持 ready snapshot 与当前 page，只提交独立 append issue；用户可重试同一下一页。
- `openAccount` 显式接收 `accessToken + accountId`。同账号 credential 更新使用保留快照的 refresh；账号变化清空旧快照并进入 initial；Shell 必须传当前 session user ID，缺失时才以 credential 作为 owner fallback。
- 每次请求、账号 / credential target 变化、repository owner 重建与 dispose 都推进 generation；旧账号、旧 repository 或已销毁 owner 的迟到响应不得提交或通知。
- 同一 owner 同时只允许一个 initial / refresh / append 请求，避免页码竞态；页面只负责生命周期、动作与 handoff，surface 只消费状态。

实施 owner 固定为：

| Owner | 目标职责 |
| --- | --- |
| `browse_history_page.dart` | route、controller 生命周期、account reopen、refresh / append 与三类原生 handoff |
| `browse_history_controller.dart` | 私域分页 snapshot、稳定 ID merge、结构化状态、generation / dispose 隔离 |
| `browse_history_issue.dart` | API、响应格式与请求错误的稳定分类 |
| `browse_history_surface.dart` | 数据来源说明、状态、连续历史行、分页动作与三档结构 |
| `profile_models.dart` | Browse History 正 LongId、typed target、时间与展示 fallback |

不修改 `ForumFollowUpStore`、`DocsFollowUpStore`、Forum / Docs reader controller、Shop 详情 owner 或平台 MethodChannel。

## 5. 三档结构与主题边界

| 窗口 | 冻结结构 |
| --- | --- |
| compact `<600px` | 页面头、明确的“账号服务端历史”说明、返回 / 刷新和按服务端顺序排列的单列连续历史；每行保持领域、标题、可选摘要、次数、时间与不小于 `48px` 的详情动作，不做双列卡片 |
| medium `600–1023px` | 不破坏时间顺序的受控密集列表，meta 与动作形成稳定列；不按类型重排，不增加详情预取或伪造预览栏 |
| expanded `>=1024px` | 不超过约 `904px` 的连续历史主轴 + `24px` 间距 + `280–300px` 数据来源上下文 rail；rail 只解释账号历史 / 设备快捷边界并汇总当前已加载 snapshot，不发起新请求 |

页面统一消费 `RadishWindowClass`、`RadishContentFrame`、Theme token、`RadishSectionSurface`、`RadishStateSlot` 与 `RadishStateChip`。时间轴、领域标记、文字、状态、按钮与焦点全部使用语义 token，不按主题 ID 分叉，不新增业务色或 Card 套 Card。四主题共享相同信息结构，compact 长内容允许换行并保持无横向溢出。

## 6. 实施门禁

1. 先补 controller 的 initial success / empty / unavailable / recover，ready / empty refresh stale / recover，append success / issue / retry 与跨页重复 `VoId` 去重。
2. 覆盖 account / credential 切换、repository 重建、generation 与 dispose 迟到响应；Shell 传递明确 session user ID。
3. model / repository 覆盖 bearer endpoint、固定 page size、正 LongId 字符串、Post PublicId / numeric fallback、Wiki slug / legacy internal route、Product targetId 与未知 / 非法目标。
4. 精确覆盖 `599 / 600 / 1024 / 1280`，并覆盖四主题、compact 超长标题 / 摘要 / 计数 / 时间、非法时间、不可打开记录和无横向溢出。
5. 现有 Browse History `2 / 2`、Shell Smoke `51 / 51` 与服务端契约 `3 / 3` 不得减少；实施后执行 P5-D3 定向、Shell、Flutter 全量、`flutter analyze` 与服务端定向。
6. `dart format`、改动 Dart owner `<1500`、文档、仓库卫生与 `git diff --check` 通过。

## 7. 停止线与下一步

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、移动端 BFF 或平台通道。
- 不新增删除、清空、批量治理、筛选、搜索、推荐排序、按类型重新分组、导入、导出或设备 recent 的跨账号 / 跨设备同步。
- 不把服务端完整历史和设备 recent shortcut 合并为同一 owner，不改设备 Store 上限、去重键、清理时机或 Profile recent sections。
- 不新增详情预取、远程封面 / 头像读取、Web URL fallback 或新的 Forum / Docs / Shop 详情 owner。
- 不提前进入 P5-E 成组门禁、平台工程或分发。
- 不读取或修改 Pen；P5-D3 直接继承 P3 Identity / Revisit、P4 Theme / Shared / Shell 和 P5-B2 Profile。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；阶段运行态验收继续独立授权。

P5-D3 readiness 已关闭。下一步等待项目所有者确认后，按本记录补 controller / model 测试、拆分页面 owner、实现三档 surface 并完成静态回归；P5-D3 实现、P5-E、服务启动与真实运行态 Smoke 不随本记录自动授权。
