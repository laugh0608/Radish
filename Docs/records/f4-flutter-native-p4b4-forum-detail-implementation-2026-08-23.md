# Flutter Native P4-B4 Forum Detail 拆分与代表页实现记录

> 状态：`P4-B4` 已完成；后续 `P4-B5` 已完成，P4 静态退出门禁关闭
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置实现：[P4-B3 Discover 实现记录](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23)

## 1. 本批结论

P4-B4 已在不改变后端 API、业务状态机或导航契约的前提下，完成 Forum Detail 页面与测试 owner 拆分，并落地 compact 连续阅读、medium 单主轴以及 `1440px` 代表尺寸下 `220 / 820 / 250` 社区导航—连续正文—线程索引三栏。

主要结果：

1. 原 `3768` 行 `forum_detail_page.dart` 已按页面编排、正文与回答、轻回应、评论与子评论、context rail 和共享状态拆为六个职责文件；协调 owner 降至 `1342` 行，其余文件均低于 `1000` 行。
2. 原 `2232` 行 `forum_detail_page_test.dart` 已按阅读 / 状态、轻回应 / 回答、评论 / 编辑、定位 / handoff 和 fixtures 拆分；所有测试文件均低于 `800` 行，既有 `25` 个行为用例完整保留。
3. compact 使用单一连续阅读表面和“社区 / 本帖”紧凑入口；正文、回答、轻回应、评论与子评论保持同一纵向任务流，滚动时可收起键盘。
4. expanded 在 `1440px` 代表尺寸使用 `220px` 社区导航、`820px` 连续阅读主轴与 `250px` 线程索引；宽度不足以容纳完整三栏的 expanded 窗口保留阅读主轴与线程索引，社区 / 来源上下文回落到正文内，不压缩为不可读窄列。
5. loading、empty、unavailable 和边界说明接入 P4-B1 `RadishStateSlot / RadishSectionSurface / RadishStateChip`；主要章节以 Divider 和留白形成连续阅读，不再由一个外层 Card 套多组同形摘要 Card。

## 2. Owner 拆分

### 页面

| owner | 行数 | 职责 |
| --- | ---: | --- |
| `forum_detail_page.dart` | 1342 | controller 生命周期、提交状态、登录回流、定位滚动与页面编排 |
| `forum_detail_content.dart` | 855 | 正文元数据、公开链接、问题回答与回答 composer |
| `forum_detail_quick_reply_section.dart` | 272 | 轻回应墙、发布与登录停止线 |
| `forum_detail_comment_section.dart` | 984 | 根评论、回复、编辑、子评论分页与定位目标 |
| `forum_detail_context_rails.dart` | 328 | compact 本帖入口、expanded 社区导航与线程索引 |
| `forum_detail_shared_widgets.dart` | 264 | navigation / inline issue / success / metadata 等局部原语 |

### 测试

| owner | 行数 | 职责 |
| --- | ---: | --- |
| `forum_detail_page_test.dart` | 69 | 测试入口、产品主题壳与共享 finder |
| `forum_detail_page_reading_cases.dart` | 454 | 读取、错误、三档布局、长内容与 reduced-motion |
| `forum_detail_page_quick_answer_cases.dart` | 406 | 轻回应、回答、登录回流与幂等重试 |
| `forum_detail_page_comment_edit_cases.dart` | 472 | 评论 / 回复、帖子编辑、评论编辑与幂等重试 |
| `forum_detail_page_navigation_cases.dart` | 308 | profile handoff、元数据隐藏与目标评论定位 |
| `forum_detail_page_test_support.dart` | 737 | 受控 repositories、session / auth 与请求记录 fixtures |

`forum_page_test.dart` 继续承接列表—详情 handoff 回归，当前 `1467` 行；本批修改的 Dart 文件全部低于仓库 `1500` 行硬上限。

## 3. 布局与交互契约

| 窗口 | 页面结构 |
| --- | --- |
| compact `< 600px` | 单一连续阅读表面；“社区 / 本帖”入口提供刷新、正文、回答、轻回应和评论定位；保持系统返回、键盘避让与来源上下文 |
| medium `600–1023px` | 受控单主轴，继续使用 inline context，不引入全局或页面级永久 rail |
| expanded `≥ 1024px` | `1440px` 代表尺寸精确为 `220 / 820 / 250`；较窄 expanded 使用阅读主轴 + `250px` 线程索引降级，不把三栏机械挤压 |

以下既有行为保持不变：

- Forum 列表、Discover、Profile、通知与最近阅读继续复用 `ForumDetailHandoffTarget` 和来源返回。
- 目标评论先解析 root / child page，再按现有 generation 与延迟滚动机制定位；失败只显示定位提示，不清空帖子。
- 回答、轻回应、根评论、回复、帖子正文编辑与根评论编辑继续使用原幂等 key、局部更新和失败保留策略。
- 匿名登录回流继续分别回到回答、轻回应或评论 composer；未扩建采纳、点赞、投票、子评论编辑、审核治理或富文本能力。

## 4. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `dart format` | 通过，页面、拆分测试与关联 Forum 测试无格式漂移 |
| Forum Detail 定向 | `29 / 29` 通过；原 `25` 个行为用例全部保留，新增 compact / medium / expanded、长正文 / 长评论与 reduced-motion `4` 个用例 |
| Forum 列表 / handoff | `20 / 20` 通过 |
| Shell Smoke | `51 / 51` 通过 |
| `flutter analyze` | 零问题 |
| `flutter test` | `241 / 241` 通过；相较 P4-B3 基线新增 `4` 个净用例 |
| 文件边界 | 本批修改的 Dart 文件最大为 `forum_page_test.dart` `1467` 行，页面和拆分测试 owner 均低于 `1500` 行 |
| 文档与差异卫生 | `npm run check:docs`、`git diff --check` 通过 |

本批只执行静态、unit 与 widget / Shell Smoke；没有启动 API / Auth / Gateway 或 Flutter 应用，不表述为真实 Gateway、设备或桌面平台运行态验收。

## 5. 停止线与下一步

- 本批未读取、使用或修改 `radish-flutter-native-ui-v1.pen`；实现只消费已确认的 P3 信息层级与几何。
- 未新增或修改后端 API、数据库、权限、依赖、lockfile 或平台工程。
- 未进入其他页面族、服务启动、真实运行态 Smoke、签名或分发。
- 后续 `P4-B5 代表范围成组静态门禁` 已按独立授权完成，详见 [P4-B5 门禁记录](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)；P4 静态退出门禁关闭。下一顺位等待 P5 页面族成组重构拆批，不自动包含真实 Gateway / Android RC Smoke。
