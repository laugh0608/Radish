# Flutter Native P4-B3 Discover 正式读模型与代表页实现记录

> 状态：`P4-B3` 已完成；下一步等待 `P4-B4 Forum Detail 拆分与代表页` 实施授权
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置实现：[P4-B2 Adaptive Shell 实现记录](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23)

## 1. 本批结论

P4-B3 已将 Flutter Discover 从 Forum / Docs / Shop 三接口客户端聚合迁移到后端既有 `/api/v1/PublicDiscover/GetFeed` 统一公开 cursor 读模型，并按 P3 冻结稿完成 compact 连续信息流与 expanded `904px` 主轴 + 社区洞察。服务端 API、资格、排序、游标和撤回语义均未改变。

主要结果：

1. Flutter models 完整解析 `VoItems / VoPulse / VoNextCursor / VoHasMore / VoGeneratedAtUtc`，对 item、actor、metric 和 `Messages / Docs / ForumPost` target 使用显式 enum 与按类型必填标识校验；Long count 保持字符串边界。
2. Repository 只请求既有 `PublicDiscover/GetFeed`，不再并行读取 `Post/GetList`、`Wiki/GetList` 或 `Shop/GetProducts`，客户端不重建跨来源排序、资格或推荐逻辑。
3. Controller 保留旧快照刷新、请求代际和结构化错误，并新增 cursor 续页；刷新会失效在途旧请求，分页按稳定 `VoKey` 去除重叠项，续页失败不清空当前内容。
4. `ForumPost` 与 `Docs` 分别映射到现有 `ForumDetailHandoffTarget`、`DocsDetailHandoffTarget`，继续保留 `discover` 来源和 comment / slug 上下文；`Messages` 不提供 Flutter 点击动作，只读说明 Web 能力边界。
5. Shop 与 Leaderboard 不再作为伪摘要来源，只保留从属上下文入口；既有原生商城列表、详情、购买和回流能力继续由原 owner 承载。

## 2. 数据与状态 owner

| owner | P4-B3 后职责 |
| --- | --- |
| `discover_models.dart` | 统一公开 feed、pulse、actor、metric、target 与 cursor 响应解析；按 target kind 校验唯一原生 / Web 目标字段 |
| `discover_repository.dart` | 单一调用 `GET /api/v1/PublicDiscover/GetFeed`，传递 `pageSize` 与 opaque cursor |
| `discover_feed_controller.dart` | 首次读取、旧快照刷新、generation 隔离、cursor 续页、稳定 key 去重和结构化 initial / refresh / load-more issue |
| `discover_page.dart` | compact / expanded 页面编排、关键状态、原生 handoff 与 Messages 只读边界 |
| Shell | 继续拥有 Forum / Docs / Profile / Shop / Leaderboard 的真实导航与来源返回，不复制 Discover 数据状态 |

初次失败会保留 `code / statusCode / message` 并区分 unavailable 与其他请求错误；刷新失败进入 stale 表面，续页失败只落在列表尾部。返回格式不满足 enum、timestamp、count、cursor 或 target 必填约束时，作为显式格式错误处理，不用缺省值掩盖契约问题。

## 3. 页面与 target 契约

### Compact

- 使用一个连续内容表面承载标题、上下文入口、焦点 item、后续时间流和续页，不回退为 Forum / Docs / Shop 三组同形 Card。
- 首项形成明确焦点层级，后续 item 以分隔线保持连续扫描；长标题、摘要、actor、metric、登录要求和 target 边界均在同一 item 内收口。
- loading、empty、unavailable 和 stale 复用 `RadishStateSlot`；刷新时继续展示旧内容。

### Expanded

- 在足够宽的 expanded 窗口中固定 `904px` 主轴，右侧为社区洞察。
- 洞察区只消费统一 feed 已返回的 `VoPulse` 和 actor，不新增统计接口或客户端伪指标；展示公开频道、近 24 小时合格 item、知识贡献和近期贡献者。
- Forum、Docs、Leaderboard、Shop 作为相邻原生任务入口；Messages 单独说明由 Web 提供，不扩建 Chat。

### Target mapping

| target | Flutter 行为 |
| --- | --- |
| `ForumPost` | 构造 `ForumDetailHandoffTarget(postId: VoPostPublicId, commentId: VoCommentId, source: discover)` |
| `Docs` | 构造 `DocsDetailHandoffTarget(slug: VoDocumentSlug, source: discover)` |
| `Messages` | 展示 `Web 提供` 与只读边界；无 onTap、无频道历史或登录回流实现 |

## 4. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `dart format` | 通过，目标 Dart 与测试文件无格式漂移 |
| Discover 定向测试 | `11 / 11` 通过；覆盖解析、cursor、Long count、target 必填、API path、分页去重、请求代际、结构化错误、旧快照、compact / expanded、target mapping 与关键状态 |
| Shell Smoke | `51 / 51` 通过；Forum / Docs / Profile / Shop / Leaderboard handoff、返回和既有 Shell 行为保持 |
| `flutter analyze` | 零问题 |
| `flutter test` | `237 / 237` 通过；相较 P4-B2 基线新增 `1` 个净用例 |
| 文件边界 | Discover page `943` 行、models `379` 行、controller `255` 行；Shell `1410` 行，均低于 `1500` 行硬上限 |
| 文档与差异卫生 | `npm run check:docs`、`git diff --check` 通过 |

本批只执行静态、unit 与 widget / Shell Smoke；没有启动 API / Auth / Gateway 或 Flutter 应用，不表述为真实 Gateway、设备或桌面平台运行态验收。

## 5. 停止线与下一步

- 本批未读取、使用或修改 `radish-flutter-native-ui-v1.pen`；实现只消费已冻结的 P3 信息层级。
- 未新增或修改后端 API、数据库、权限、依赖、lockfile 或平台工程。
- 未进入 `P4-B4 Forum Detail`、其他页面族、服务启动或真实运行态 Smoke。
- 下一顺位为 `P4-B4 Forum Detail 拆分与代表页`：先按页面编排、正文 / 回答、轻回应、评论 / 子评论和 context rail 拆分超限 owner，再实施 compact 连续阅读与 expanded `220 / 820 / 250` 页面级三栏；需另行授权后实施。
