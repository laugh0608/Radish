# Flutter Native P5-D2 Leaderboard 实现记录

> 状态：`P5-D2` 已完成；下一顺位进入 `P5-D3 Browse History readiness`
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-D2 实施就绪与方案冻结](/records/f4-flutter-native-p5d2-leaderboard-readiness-2026-08-24)

## 1. 结论

P5-D2 已按冻结边界完成。Flutter 继续只读取匿名公开经验榜第一页 20 条，没有新增后端 API、登录态、其他榜单、类型元数据、“我的排名”或分页能力。

页面状态已从 widget 内的 page、busy、错误字符串和 request ID 组合迁入独立 `LeaderboardController`，明确提交 idle / loading / ready / empty / unavailable / stale，并完成 refresh 保留、repository owner 重建、generation 与 dispose 隔离。首次空榜是权威 empty；已有 ready 或 empty 首屏刷新失败时保留原快照并进入 stale。

统一排行榜 VO 已补映射 `VoUserPublicId / VoUserDisplayName / VoUserDisplayHandle`。公开主页 target 优先合法且标准化的 `usr_` PublicId，缺失时兼容正整数数字 ID；无合法 target 时隐藏动作，不再把任意非空内部字符串当作公开身份。

compact / medium / expanded 已形成连续紧凑排名、受控密集列表和 `<=904 + 24 + 280–300` 排名主轴 / 公开主页上下文。服务端 `themeColor` 只接受严格 `#RRGGBB` 并用于无文字 accent 色条；rank、文字、按钮、状态和可交互 surface 全部使用四主题语义 token。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `leaderboard_page.dart` | route、controller 生命周期、repository 变化、刷新与 Profile handoff | `141` |
| `leaderboard_controller.dart` | 首屏权威 snapshot、结构化状态、generation / dispose 隔离 | `137` |
| `leaderboard_issue.dart` | API、响应格式与请求错误分类 | `39` |
| `leaderboard_surface.dart` | 页面状态、连续排名、公开主页上下文与三档结构 | `516` |
| `leaderboard_models.dart` | 榜单分页、公共身份映射、显示与 target fallback | `218` |

原 `520` 行页面已按 route、远端状态和呈现职责拆分；实施后运行时 Dart owner 最大 `516` 行。测试 owner 为 page `244`、controller `147`、responsive `184` 行，全部低于 `1500` 行硬上限。

## 3. 状态、身份与导航

- `LeaderboardController` 固定请求 `type=1&pageIndex=1&pageSize=20`，不接 access token；refresh 成功整体替换第一页，失败只在已有快照上进入 stale。
- `open()` 与 dispose 都推进 generation；旧 repository owner 的迟到响应不能提交或通知。页面收到新 repository 时销毁旧 owner 并创建新 owner。
- 展示名优先 `VoUserDisplayName → VoUserName → 合法 PublicId / 数字 ID fallback → 匿名用户`；公开句柄独立呈现，不重新拼装权威身份。
- `profileTarget` 只接受 `usr_` + 32 位十六进制或正整数数字 ID 字符串，继续由既有 Shell Public Profile owner 打开并保留 Leaderboard 来源。
- Shell Smoke 的历史 `user-9` 榜单 fixture 已改为生产契约允许的数字 fallback `9`；这只修正测试数据，不改变其他 Discover / Forum fixture 或正式 API。
- Leaderboard → Public Profile → 帖子 / 评论详情 → Back 与 Leaderboard → Public Profile → Back 均保持真实 Navigator / Shell 来源回流。

## 4. 自适应与主题

- compact `<600px`：页面头、状态、rank、身份、等级、指标与可见公开主页动作按连续单列任务呈现；长身份、句柄、等级和大数值自然换行。
- medium `600–1023px`：不增加 rail，排名使用受控主轴和密集列表行，身份、等级、指标与 `48px` 级动作保持同一行可扫读。
- expanded `>=1024px`：排名主轴不超过 `904px`，右侧以 `24px` 间距承接 `280–300px` 公开主页上下文，只复用当前首屏第一个合法 target，不发起额外请求。
- rank surface 使用 `surfaceMuted / border / text`；业务色只进入 `4px` 无文字 accent。alpha、缺少 `#`、非法十六进制或其他格式统一回落 `border` token。
- 页面统一使用 `RadishContentFrame`、`RadishWindowClass`、`RadishSectionSurface`、`RadishStateSlot`、`RadishStateChip` 与 Theme token，没有新增断点、主题 ID 分支或远程头像读取。

## 5. 验证

- P5-D2 controller：`6 / 6`，覆盖 initial unavailable / recover、refresh 替换、ready / empty stale、新 generation 与 dispose 迟到响应。
- P5-D2 page / model / repository：`6 / 6`，覆盖公开经验榜、PublicId 优先跳转、数字 ID fallback、非法 target、匿名 endpoint 与固定 query。
- P5-D2 responsive：`10 / 10`，精确覆盖 `599 / 600 / 1024 / 1280`、四主题、compact 长内容和合法 / alpha 业务色语义边界。
- P5-D2 定向合计 `22 / 22`；Shell Smoke `51 / 51`；成组定向合计 `73 / 73`。
- Flutter 全量：`374 / 374`；`flutter analyze`：零问题。
- `dart format`、改动 Dart owner `<1500` 与 `git diff --check` 通过；文档和仓库卫生门禁随本记录收口执行。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 6. 下一顺位

P5-D2 关闭。下一顺位进入 `P5-D3 Browse History readiness`：先反查服务端完整浏览历史、设备 recent shortcut、分页 / 去重、原生 Forum / Docs handoff、三档连续历史和当前测试基线，再冻结独立实施边界。P5-D3 实现、P5-E、平台工程、服务启动与真实运行态 Smoke 不随本记录自动授权。
