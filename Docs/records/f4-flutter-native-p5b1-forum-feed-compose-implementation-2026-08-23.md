# Flutter Native P5-B1 Forum Feed / Compose 实现记录

> 状态：`P5-B1` 已完成；后续 [P5-B2 Identity / Revisit](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23) 已完成
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置审计：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)

## 1. 本批结论

P5-B1 已按冻结边界完成 Forum Feed / Compose 页面族重构。Forum 列表继续使用既有 `Post/GetList` 页码读模型，没有新增后端 API；最新 / 热门、真实分页、请求代际、刷新旧快照和结构化错误保持。帖子与作者继续走现有原生 Forum Detail / Profile handoff，列表没有预取详情，也没有把发帖分类伪装成浏览筛选。

页面已从常驻 composer + Card 堆叠改为 compact / medium / expanded 三档连续帖子流：compact 使用单一信息流和全高可滚动发帖任务，medium 使用受控单主轴与 bounded composer，expanded 在 `1440px` 下采用 `904px` 主轴和社区洞察 rail。标题、分类、标签、正文 controller 与 `ForumSubmissionState` 仍由同一 `ForumPage` State 持有，因此登录回流、失败保留和同草稿重试不会换草稿实例。

本批只包含 `Post/Publish` 一个高风险写入域；没有修改 Forum Detail、Discover、Shell 导航、后端、权限、数据库、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / Android RC Smoke。

## 2. Owner 拆分结果

原 `1306` 行 `forum_page.dart` 已按真实职责拆为：

| Owner | 职责 | 完成后规模 |
| --- | --- | ---: |
| `forum_page.dart` | 生命周期、session / handoff、页面编排、草稿 owner | `547` 行 |
| `forum_feed_surface.dart` | 三档连续流、排序 / refresh、分页、帖子 / 作者动作、expanded rail | `633` 行 |
| `forum_post_composer.dart` | compact / bounded 发帖任务、分类、校验与局部反馈 | `424` 行 |
| `forum_feed_shared_widgets.dart` | stale / loading meta、轻量动作与格式化 | `122` 行 |
| `forum_feed_controller.dart` | 页码读模型、请求代际、旧快照和结构化 issue | `266` 行 |

原 `1467` 行 `forum_page_test.dart` 现只保留 `61` 行 library 入口；repository contract、adaptive、feed / states、composer、navigation / handoff 与 support 分开维护，最大测试 owner 为 `702` 行。所有本批 Dart owner 均低于 `1500` 行硬上限。

## 3. 页面与状态实现

### 3.1 三档结构

| 窗口 | 实现事实 |
| --- | --- |
| compact `390px` | 单一连续帖子流；头部只保留最新 / 热门、刷新和发布动作；composer 为全高 `Dialog.fullscreen`，正文区域可滚动且键盘安全 |
| medium `800px` | 受控单主轴；没有全局 rail；composer 为最大 `680px` 宽、受视口高度约束的 bounded task surface |
| expanded `1440px` | `904px` 连续帖子主轴 + 社区洞察；rail 只显示真实发布入口、当前排序 / 页码 / 总量和能力边界 |

帖子使用一个 `RadishSectionSurface`、Divider 和留白形成连续扫描，不再由独立 Card 网格承载。标题、摘要、分类、作者和计数均设置明确文本边界；LongId / PublicId 保持字符串。

### 3.2 Feed 状态

- `ForumFeedController` 保留请求代际，迟到响应不会覆盖新请求。
- 初次 loading、empty、unavailable / error 使用共享 `RadishStateSlot`。
- refresh 时继续展示旧页；失败转为 stale slot，并保留结构化 `kind / message / code / statusCode`。
- 最新 / 热门和上一页 / 下一页仍调用既有 `Post/GetList` 参数，不改为 cursor、搜索或分类筛选。

### 3.3 Composer 状态

- `Category/GetTopCategories` 只在发帖任务中消费；分类失败留在 composer 局部并可重试。
- 标题、分类、正文和 `1–5` 标签校验保持；没有新增富文本、附件、投票、抽奖、草稿箱或点赞。
- `forum-post:` fingerprint 与 submission key 规则保持；同草稿失败重试复用同一 key。
- 匿名提交触发既有 OIDC 登录，回流后仍显示同一 controller 草稿；成功清理草稿、刷新列表并以服务端返回 ID 打开详情。
- composer 只使用 fade transition；`disableAnimations` 下持续时间归零，不依赖位移动效表达状态。

## 4. 既有 API 与 target mapping

| 既有能力 | P5-B1 用途 | 裁决 |
| --- | --- | --- |
| `Post/GetList` | 最新 / 热门、页码、公开帖子摘要 | 保留，不新增列表 API |
| `Category/GetTopCategories` | 发帖分类 | 只在 composer 使用，不作为浏览筛选 |
| `Post/Publish` | 纯文本发帖 | 本批唯一高风险写入 |
| `ForumDetailHandoffTarget` | 列表项、发布成功、外部 Shell target | 复用；支持消费后再次打开同一 target |
| Profile user handoff | 作者动作 | 复用，不在 Forum 内复制 Profile 页面 |

Repository contract 测试固定 `Post/GetList?pageIndex=2&pageSize=20&sortBy=hottest&postType=all`，并验证帖子、分类、作者的超长 LongId 均保持字符串。

## 5. 验证结果

| 验证 | 结果 |
| --- | --- |
| 改造前 Forum 基线 | `20 / 20` |
| P5-B1 Forum 定向 | `33 / 33` |
| Shell 静态 Smoke | `51 / 51` |
| `flutter analyze` | 零问题 |
| `flutter test` | `254 / 254` |
| Dart owner 边界 | 本批最大 `702` 行，全部 `< 1500` |

新增覆盖包括 `390 / 800 / 1440`、expanded `904px` 主轴、compact 键盘 + reduced-motion、长文本无溢出、loading / empty / unavailable / refresh stale、分类失败、发布失败保留、同键重试、登录回流、成功详情 handoff、四主题代表结构与 LongId target mapping。

## 6. 后续顺位

`P5-B2 Identity / Revisit` 已按独立授权完成，详见 [P5-B2 实现记录](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23)。当前顺位转入 `P5-C1 Docs Reader readiness`，不由 P5-B1 / B2 自动授权。
