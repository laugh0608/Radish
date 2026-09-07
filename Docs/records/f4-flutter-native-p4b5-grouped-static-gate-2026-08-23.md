# Flutter Native P4-B5 代表范围成组静态门禁记录

> 状态：`P4-B5` 已完成；P4 代表实现静态退出门禁已关闭
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置实现：[P4-B4 Forum Detail 实现记录](/records/f4-flutter-native-p4b4-forum-detail-implementation-2026-08-23)

## 1. 本批结论

P4-B5 已在不新增页面能力、不改变 API / 业务 owner、也不启动服务或设备的边界内，完成 P4-B1–B4 代表范围成组静态复核。Theme / Shared、Adaptive Shell、Discover 与 Forum Detail 的冻结契约均有对应自动化覆盖，`flutter analyze` 和全量 `flutter test` 通过，P4 期间改动的 Dart owner 全部回到仓库 `1500` 行硬上限内。

审计期间发现 `test/smoke_test.dart` 虽持续保持 `51 / 51`，但仍有 `5144` 行，属于 B2 / B3 已改动却未关闭的文件边界阻断。本批只做机械职责拆分：入口保留注册编排，测试按 Shell、Discover、Auth、handoff、通知 / 最近访问拆分，fixtures 按 Discover / Commerce、Docs、Forum、Profile / Auth 拆分；测试名称、断言、fixtures 数据和产品运行时代码均未改变。

## 2. B1–B4 代表契约复核

| 批次 | 静态复核结论 |
| --- | --- |
| P4-B1 Theme / Shared | 四主题语义 token、Noto typography、density / surface / focus / motion、主题权益与预览—确认、共享状态原语均由定向测试覆盖；字体声明、OFL / SHA 记录和精确版本 `lucide_icons_flutter 3.1.15` 保持 |
| P4-B2 Adaptive Shell | `390 / 800 / 1440` 三档壳层、安全区胶囊底栏、顶部全局栏、键盘 / 焦点 / reduced-motion、五入口、Android Back、OIDC、通知与来源返回覆盖保持 |
| P4-B3 Discover | 既有 `PublicDiscover/GetFeed` cursor 读模型、解析、请求代际、旧快照、分页去重、结构化错误、target mapping、compact 连续流与 expanded `904px` 主轴覆盖保持 |
| P4-B4 Forum Detail | compact 连续阅读、medium 单主轴、expanded `220 / 820 / 250`、长正文 / 评论、定位、分页、登录回流、编辑与幂等覆盖保持 |

本批没有新增后端 API、Flutter Chat、页面级数据状态或兼容兜底；B1–B4 仍共享原业务 controller、repository、handoff target 和来源返回契约。

## 3. Smoke owner 拆分

| owner | 行数 | 职责 |
| --- | ---: | --- |
| `smoke_test.dart` | 81 | library 入口、共享 finder / menu helper 与用例注册 |
| `smoke_shell_cases.dart` | 339 | 游客 / 登录壳层、compact、根返回与私有账户入口 |
| `smoke_discover_cases.dart` | 712 | Discover、Shop、Leaderboard 与公开 Profile 回流 |
| `smoke_auth_cases.dart` | 963 | 会话恢复、OIDC、登录意图、Forum composer 回流 |
| `smoke_handoff_cases.dart` | 694 | Forum / Profile / Docs 原生 handoff 与来源返回 |
| `smoke_notification_recent_cases.dart` | 485 | 通知、标记已读、失败状态与最近访问 |
| `smoke_discover_commerce_support.dart` | 504 | Auth builder、Discover / Shop / Wallet / Experience fixtures |
| `smoke_docs_support.dart` | 101 | Docs fixtures |
| `smoke_forum_support.dart` | 743 | Forum fixtures、幂等请求记录与 LongId 数据 |
| `smoke_profile_auth_support.dart` | 557 | Profile、Leaderboard、Notification、Session / Auth fixtures |

拆分后最大 Smoke owner 为 `963` 行，原 `51` 个用例全部保留并通过。

## 4. 文件边界

以 P4-B1 前的 P3 确认提交 `93f5f3eb` 为基准，结合当前未跟踪的 B4 / B5 拆分文件检查所有 Flutter Dart 改动：

- 最大测试 owner：`forum_page_test.dart`，`1467` 行；
- 最大运行时 owner：`radish_flutter_shell.dart`，`1410` 行；
- Forum Detail 页面编排 owner：`1342` 行；
- Discover 页面 owner：`943` 行；
- 新拆分 Smoke owner 最大 `963` 行；
- 所有 P4 改动 Dart 文件均低于 `1500` 行，没有以机械切片继续堆叠巨型 owner。

## 5. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `dart format` | 通过，B4 与 B5 拆分 Dart 文件无格式漂移 |
| Shell Smoke 拆分回归 | `51 / 51` 通过；用例数与拆分前一致 |
| P4-B1–B4 成组代表测试 | `138 / 138` 通过；覆盖主题 / 权益、共享组件、Shell、Discover、Forum Detail、Forum 列表与 Shell Smoke |
| `flutter analyze` | 零问题 |
| `flutter test` | `241 / 241` 通过；相较 P2 `228 / 228` 基线净增 `13` 个用例，拆分没有减少覆盖 |
| 文件边界 | P4 改动 Dart owner 最大 `1467` 行，全部低于 `1500` 行 |
| 文档与差异卫生 | `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` 通过 |

## 6. 阶段退出与后续边界

- P4-B1–B5 均已按独立授权完成，P4 的主题 / 共享基座、代表 Shell、Discover、Forum Detail 与成组静态门禁退出条件满足。
- 本批未读取、使用或修改 Pen，未改后端 API、数据库、权限、依赖、lockfile、其他页面族或平台工程。
- 本批未启动 API / Auth / Gateway 或 Flutter 应用，未执行真实 Gateway PC / mobile、四主题、Android RC、桌面平台、签名或分发验收；这些不能由静态门禁结果替代。
- 后续 `P5-A 页面族成组重构拆批与首批就绪审计`、[P5-B1 Forum Feed / Compose](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23) 与 [P5-B2 Identity / Revisit](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23) 已完成。当前等待 `P5-C1 Docs Reader` 方案确认；P4-B5 不自动授权 P5 后续实现或运行态 Smoke。
