# Flutter Android MVP RC 验收记录（2026-05-04）

- 验收日期：2026-05-04
- 记录人：laugh0608
- 验收范围：`Phase 2-3 Flutter 客户端 MVP` Android MVP 第一轮 RC 收口
- 验收结论：Go

## 验收上下文

本轮接受 `https://radishx.com` 作为 Android MVP RC 验收 Gateway，不再要求另行准备独立 testing Gateway 后重复同一轮验收。

- `RADISH_ENVIRONMENT`：`production`
- `RADISH_GATEWAY_BASE_URL`：`https://radishx.com`
- APK 类型：release APK
- APK 构建命令：`flutter build apk --release --dart-define=RADISH_ENVIRONMENT=production --dart-define=RADISH_GATEWAY_BASE_URL=https://radishx.com`
- APK 产物：`Clients/radish.flutter/build/app/outputs/flutter-apk/app-release.apk`
- APK 大小：`47.3MB`
- 测试设备：小米 15S Pro
- Android 版本：16
- 测试账号：`test`

前置说明：用户确认前天人工验收使用的是同等参数 release APK，并确认本轮接受 `radishx.com` 作为 RC 验收 Gateway。

## 命令级验收

以下命令均已在 `Clients/radish.flutter` 或其 `android` 子目录完成：

| 验证项 | 结果 | 关键摘要 |
| --- | --- | --- |
| `flutter analyze` | 通过 | `No issues found!` |
| `flutter test` | 通过 | `117` 个测试全部通过 |
| `flutter test test/smoke_test.dart` | 通过 | `36` 个 smoke 测试全部通过 |
| `.\gradlew.bat :app:testDebugUnitTest` | 通过 | `BUILD SUCCESSFUL` |
| `.\gradlew.bat :app:checkReleaseSigningConfig` | 通过 | `Android release signing material is ready: upload-keystore.jks` |
| `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=production --dart-define=RADISH_GATEWAY_BASE_URL=https://radishx.com` | 通过 | 已产出 `app-release.apk` |
| `git diff --check` | 通过 | 未发现空白错误 |

Android JVM 单测与签名检查均使用 Android Studio JBR 执行。

## 真机验收结论

用户确认小米 15S Pro / Android 16 上的 release APK 人工复核无问题，覆盖以下核心链路：

1. 登录、退出与会话恢复
2. `discover / forum / docs / profile` 四个主 tab 真实读取
3. forum 详情、评论分页、`commentId` 定位与来源返回
4. docs 搜索、详情、正文内链与来源返回
5. profile 最近阅读、最近文档与我的轻回应回看
6. forum detail 轻回应发布
7. 最小 forum notification 回流

## 已知问题

- 本轮未发现 `P0 / P1` 阻断。
- 系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票、编辑治理、聊天、完整商城工作台、Windows / Linux 平台扩展与 Flutter 专属 BFF 仍按既定范围后置，不作为本轮 RC 阻断项。

## Go / No-Go

Go。

Android MVP 高价值链路已通过 RC 验收，当前可将 `Phase 2-3 Android MVP` 标记为“第一轮完成”。后续不再默认开启第 `24` 批低增益 Flutter 微体验补丁，应先评估 Android 内测产品化深化、分发反馈闭环或 Windows / Linux 平台扩展的下一阶段优先级。
