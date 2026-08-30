# F4 2026-08-30 日终提交回顾与文档审阅

> 日期：2026-08-30（Asia/Shanghai）
>
> 范围：复核今日截至 `fa012ce4` 的五个提交，并按实际代码、测试、构建与运行证据反查相关文档。本次日终文档提交自身不计入回顾范围。

## 今日结论

- 今日 Git 历史截至本次日终收口前共有五个提交，累计涉及 `82` 个唯一文件、`3,316` 行新增与 `176` 行删除；其中两笔 Flutter 运行时修正、两笔阶段文档提交和一笔 iOS 平台基座实现提交。
- P6-B 完成 compact / medium 双 API 35 AVD 运行态闭环；项目所有者确认该证据足以形成 `Android UI AVD RC Go`，P6-C 真机验收转为 Android 分发前后置门禁。
- P7-A 工具链 readiness 与 P7-B platform foundation 均已关闭：iOS 平台工程、安全认证存储、非敏感跨平台偏好、Android 幂等迁移和 iOS OIDC callback 已落地，Android / iOS 原生构建门禁通过。
- P7-C Simulator 运行态尚未启动。2026-08-31 第一顺位只做 readiness，先冻结 phone / tablet、认证、持久化、回访、失败停止线、证据和清理矩阵；真实运行仍需单独授权。

## 今日全部已提交变更

| 提交 | 主题 | 审阅结论 |
| --- | --- | --- |
| `a4a7ae09` | `fix(flutter): 延长 OIDC 授权尝试有效期` | OIDC authorization attempt 有效期由 `5` 分钟调整为 `15` 分钟，覆盖系统浏览器交互、开发证书确认和 Activity / Flutter owner 重建；state、PKCE、redirect、过期与重放继续 fail closed。 |
| `4acb7dbf` | `fix(flutter): 保持评论编辑器键盘焦点` | Forum Detail 按评论 ID 持有稳定编辑器 `GlobalKey`，避免输入法 `viewInsets` 触发 rebuild 时替换编辑子树；同一 `FocusNode` 和真实键盘焦点回归已覆盖。 |
| `f1c1b892` | `docs(flutter): 关闭 P6-B 双 AVD 验收` | 固化 compact / medium AVD 运行证据、修正验证、最终历史 APK 哈希和 P6-B `Go` 结论；没有把尚未执行的真机验收写成通过。 |
| `296f2814` | `docs(flutter): 进入 iOS 平台就绪阶段` | 记录项目所有者关闭 Android AVD 开发门禁的裁决，完成 P7-A 工具链与仓库缺口审计，并冻结 P7-B 建议边界。 |
| `fa012ce4` | `feat(flutter): 建立 iOS 平台与安全持久化` | 生成 iOS 工程并建立平台明确的 storage / preferences / migration / callback owners；完成 Flutter、Android JVM / APK 与 iOS 无签名构建门禁。 |

## 按代码反查的稳定事实

### OIDC 与编辑焦点

- OIDC attempt 自打开系统浏览器起有效 `15` 分钟；超过 TTL、state / verifier / redirect 不匹配或重复 callback 仍拒绝消费，不使用更长的无界兜底掩盖认证契约。
- Forum 根评论编辑器的 identity 与 focus 生命周期由 Forum Detail owner 稳定持有，输入法引起的布局重建不会再立即替换编辑子树或关闭键盘。

### 跨平台持久化与 iOS 壳层

- session token 与 OIDC attempt 使用 `flutter_secure_storage 10.3.1`：Android 由 Keystore 支撑，iOS 使用 Keychain 与 `first_unlock_this_device` 可访问级别。
- Forum / Docs recent、recent profile 与 pending post-login target 等非敏感状态继续由 Dart `shared_preferences` 承担；Android 旧明文值仅在新 owner 缺失时读取，写入并回读确认后才清除，失败保留旧值供下次重试。
- iOS 工程固定 `com.radish.client`、`Radish`、iOS `13.0+` 与 `radish` URL scheme；系统浏览器与 UIScene callback 是原生壳层职责，认证状态仍由 Dart controller 统一裁决。

## 今日验证证据复核

- P6-B：Forum Detail `33 / 33`、Flutter 全量 `428 / 428`、`flutter analyze` 零问题与 release APK 构建通过；compact / medium 双 AVD 完成系统浏览器 OIDC、私域 / 主题、冷启动、真实输入法与连续根评论 CAS。最终历史 APK SHA-256 为 `b08d0f5e0aea5d873bf61018e1ba8c1b654971fa94567e40343c9396fb2cc174`。
- P7-B：Flutter 定向 `18 / 18`、全量 `435 / 435`、analyze、Android JVM `7 / 7`、新 Debug APK、iOS 无签名 `Runner.app`、Swift callback parser 与 RunnerTests build-for-testing 均通过。
- P7-B 没有启动 Gateway / Auth / API、Simulator / AVD 或真实 Smoke，也没有读取或提交 Apple Team、证书、签名或 provisioning 材料；因此这些构建证据不能表述为 P7-C Simulator 运行态通过。

## 文档审阅结论

- [当前规划](/planning/current)、[开发路线图](/development-plan)、[Flutter 专题](/features/flutter-native-product-ui-design)、[移动端 handoff](/guide/flutter-mobile-handoff)、UI 差异附录、视觉主题与设计源索引中的 P6 关闭、P7-B 实施和 P7-C 停止线已与代码一致，无需重复改写。
- 根 README 仍停在“P7-B 方案确认”，本次推进到 P7-C readiness，并补 P7-B 已完成的工程与验证事实。
- [验证基线](/guide/validation-baseline)此前只有 Flutter Android 平台测试说明，本次新增 iOS 无签名 Simulator build 与 RunnerTests build-for-testing 的稳定入口，并明确 build-only 不等于 Simulator 运行态验收。
- [Flutter README](../../Clients/radish.flutter/README.md)同步 P7-B 关闭、P7-C readiness 与 iOS 原生构建入口。`AGENTS.md` / `CLAUDE.md`、API、数据库、架构和 Pencil 没有新增长期规则或事实变化，不需要更新。

## 明日事项（2026-08-31）

1. 新会话先读取[当前进行中](/planning/current)、本记录、[P7-B 实施记录](/records/f4-flutter-native-p7b-ios-platform-foundation-implementation-2026-08-30)、[P7-A readiness](/records/f4-flutter-native-p7a-ios-platform-readiness-2026-08-30)、[Flutter 专题](/features/flutter-native-product-ui-design)、[移动端 handoff](/guide/flutter-mobile-handoff)、[验证基线](/guide/validation-baseline)、[运行手册](/guide/operations-runbook)和 [Flutter README](../../Clients/radish.flutter/README.md)。
2. 第一顺位只做 P7-C readiness：反查当前 shell、UIScene callback、Keychain / preferences、iOS 本地开发证书显式 opt-in、测试账号与 phone / tablet Simulator 事实。
3. 冻结匿名启动、系统浏览器 OIDC callback、登录恢复 / 冷启动、session / PKCE 安全持久化、Forum / Docs recent 与 phone / tablet 代表布局矩阵，同时明确失败停止线、证据目录和清理方式。
4. readiness 方案确认后，再单独申请 Gateway / Auth / API、Simulator、安装与真实 Smoke 授权；不以 Android 结果替代 iOS 运行证据。
5. P7-D 的真机、Apple 签名、TestFlight、App Store 与 production 继续后置，不在 P7-C readiness 中读取或扩入。

## 日终验证与提交边界

- 日终不重复运行今天已通过的代码测试 / 构建，不启动服务、AVD 或 Simulator。
- 文档批执行 `npm run check:docs`、`npm run check:repo-hygiene:staged`、`git diff --check` 与 staged 文件边界复核。
- 本次提交只包含 README 与 `Docs/` 文档，不修改运行时代码、平台工程、依赖、API、数据库或 Pencil。
