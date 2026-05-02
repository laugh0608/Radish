# Flutter Android MVP 第八至第二十批验证索引

- 记录日期：2026-05-02
- 记录人：laugh0608
- 整理范围：`Phase 2-3 Flutter 客户端 MVP` 第八批至第二十批的开发阶段验证资产
- 记录性质：验证索引整理，不新增 Flutter / Android 业务功能

## 整理摘要

本页把第八批至第二十批已经散落在专题回归记录、Flutter MVP 规划和当前主线文档中的验证事实收束成一张索引，方便后续 release 前做批次级回归留痕时快速定位验证入口。

当前结论：

1. 第八至第十七批均已有明确的 Dart 定向测试、`smoke_test`、`flutter analyze` 与 `git diff --check` 结论
2. 第八批因触达 Android 本地存储桥，已额外覆盖 Android Studio JBR 下的 `.\gradlew.bat :app:testDebugUnitTest`
3. 第十八批是文档型收口，验证资产是刷新体验开发阶段清单，不替代 release 前真机验收
4. 第十九批与第二十批在规划文档中记录为已完成代码与自动化验证，但尚未沉淀独立批次级回归记录；release 前建议补齐命令级留痕
5. 第八至第二十批均未执行 testing Gateway release APK 构建、真机 APK 安装或外部分发验收；这些仍按 RC 清单留到正式 release 包发布前补

## 验证索引

| 批次 | 范围 | 已记录验证 | 记录来源 | release 前缺口 |
| --- | --- | --- | --- | --- |
| 第八批 | `profile` 最近文档轻量多条列表 | `flutter test test/profile_page_test.dart test/smoke_test.dart`、`flutter test test/docs_page_test.dart`、`flutter test`、`flutter analyze`、Android JBR `.\gradlew.bat :app:testDebugUnitTest`、`git diff --check`；后续复核再次确认 profile / docs 定向测试、`flutter analyze` 与 Android JVM 单测通过 | [profile 最近文档轻量多条列表变更回归记录](/guide/flutter-android-mvp-profile-recent-docs-record-2026-05-01) | testing Gateway、release APK、真机安装与外部分发回归 |
| 第九批 | `profile` 复访区块体验整理 | `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [profile 复访区块体验整理回归记录](/guide/flutter-android-mvp-profile-revisit-sections-record-2026-05-01) | testing Gateway、release APK、真机安装与外部分发回归 |
| 第十批 | `profile` 区块顺序与信息密度微调 | `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [profile 区块顺序与信息密度微调回归记录](/guide/flutter-android-mvp-profile-section-density-record-2026-05-01) | testing Gateway、release APK、真机安装与外部分发回归 |
| 第十一批 | docs 详情只读上下文补强 | `flutter test test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check`；提交前文档门禁 `check:repo-hygiene:staged` 与 `check:identity-impact:staged` 通过 | [docs 详情只读上下文补强记录](/guide/flutter-android-mvp-docs-detail-readonly-context-record-2026-05-01) | testing Gateway、release APK、真机安装与外部分发回归 |
| 第十二批 | forum detail 来源上下文与错误态补强 | `flutter test test/forum_detail_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [forum detail 来源上下文与错误态补强记录](/guide/flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01) | testing Gateway、release APK、真机安装与外部分发回归 |
| 第十三批 | discover 只读分发上下文补强 | `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、针对本次变更文件的 `git diff --check` | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 独立批次级回归记录、testing Gateway、release APK、真机安装与外部分发回归 |
| 第十四批 | discover 聚合摘要局部失败降级 | `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、针对本次变更文件的 `git diff --check` | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 独立批次级回归记录、testing Gateway、release APK、真机安装与外部分发回归 |
| 第十五批 | discover 刷新体验与局部 issue 清理复核 | `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 独立批次级回归记录、testing Gateway、release APK、真机安装与外部分发回归 |
| 第十六批 | forum / docs 主列表刷新体验一致性 | `flutter test test/forum_page_test.dart test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 独立批次级回归记录、testing Gateway、release APK、真机安装与外部分发回归 |
| 第十七批 | profile 主资料刷新体验一致性 | `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 独立批次级回归记录、testing Gateway、release APK、真机安装与外部分发回归 |
| 第十八批 | 刷新体验收口复核 / 模拟器验证清单整理 | 文档型收口；新增刷新体验开发阶段验证清单，复用第十五至第十七批已有自动化作为代码层保障 | [刷新体验开发阶段验证清单](/guide/flutter-android-mvp-refresh-experience-checklist-2026-05-02)、[Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp) | 若 release 前仍需复核刷新体验，需在 testing Gateway + release APK + 真机安装后按清单执行 |
| 第十九批 | profile 空态人称与文档口径复核 | 规划文档记录为已完成代码与自动化验证；未在 `Docs/guide` 下沉淀独立命令级记录 | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)、[当前进行中](/planning/current) | 补命令级批次记录；testing Gateway、release APK、真机安装与外部分发回归 |
| 第二十批 | profile 公开主页长文本窄屏显示复核 | 规划文档记录为已完成代码与自动化验证，新增窄屏 widget 测试；未在 `Docs/guide` 下沉淀独立命令级记录 | [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)、[当前进行中](/planning/current) | 补命令级批次记录；testing Gateway、release APK、真机安装与外部分发回归 |

## release 前建议补齐

正式 release 包发布前，建议按以下顺序把本索引转换为批次级回归留痕：

1. 针对当前代码重新执行 `flutter analyze` 与 `flutter test`
2. 按改动风险补跑定向测试：`profile_page_test.dart`、`docs_page_test.dart`、`forum_page_test.dart`、`forum_detail_page_test.dart`、`discover_page_test.dart` 与 `smoke_test.dart`
3. 使用 Android Studio JBR 执行 `.\gradlew.bat :app:testDebugUnitTest`
4. 执行 `.\gradlew.bat :app:checkReleaseSigningConfig`
5. 使用 testing Gateway 构建 release APK
6. 在真机安装 release APK，并按 `Clients/radish.flutter/README.md` 的 Android 真机人工验证 checklist 复核
7. 将执行结果、Gateway 基址、APK 构建参数、真机结论、已知非阻断项与未执行项汇总为 release 前回归记录

## 结论

第二十二批可收口为“第八至第二十批验证索引整理”。本轮只整理已有验证资产，不新增业务功能，不改变 Android MVP RC 阻断范围。

后续若继续做 Flutter MVP 产品小闭环，仍建议选择窄范围复访或只读体验补强；若准备 release / RC 外部分发，则应优先按本索引和 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 补齐 release 前验证。
