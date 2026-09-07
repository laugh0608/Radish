# Flutter Native P7-A iOS 平台 readiness

> 状态：`P7-A 已完成`；可在方案确认后进入 `P7-B iOS platform foundation`
>
> 日期：2026-08-30（Asia/Shanghai）
>
> 前置裁决：[P6 Android AVD 门禁项目所有者关闭](/records/f4-flutter-native-p6-android-avd-gate-owner-closure-2026-08-30)

## 1. 结论

本机 iOS 工具链和 Simulator runtime 完整，可支持下一批平台工程生成、编译与模拟器验收；当前真正阻断不在 Xcode，而在仓库仍只有 Android 平台工程，且正式 bootstrap 对非 Android 明确使用内存会话、内存 OIDC、内存回访和空生命周期实现。

因此 P7-A readiness 结论为 **Go to confirmed implementation plan**，不等于 iOS App 已可构建或可运行。下一批必须先建立真实 iOS 平台 owner 和跨平台持久化 / 回调边界，不能只生成 `ios/` 后把能打开空壳当作完成。

## 2. 本机工具链事实

| 项目 | 结果 |
| --- | --- |
| 主机 | macOS `26.5.2 25F84` / Apple Silicon `arm64` |
| Flutter | stable `3.44.0` / Dart `3.12.0` / DevTools `2.57.0` |
| Xcode | `26.6` build `17F113` |
| CocoaPods | `1.16.2` |
| iOS SDK | device / Simulator `26.5` |
| Simulator runtime | iOS `26.5 (23F77)` |
| 可用 phone | iPhone 17 Pro、17 Pro Max、17e、Air、17 |
| 可用 tablet | iPad Pro、iPad mini、iPad Air、iPad |
| `flutter doctor -v` | Flutter、Xcode、CocoaPods、网络均通过；`No issues found` |

本批只列举 shutdown 设备和 runtime，没有启动 Simulator、安装 App、构建 iOS 产物或执行服务 Smoke。

## 3. 仓库事实

1. `Clients/radish.flutter` 当前只有 `android/`，没有 `ios/`，也没有 Flutter `.metadata`；README 同样明确 iOS 平台工程尚未生成。
2. `pubspec.yaml` 只有 `crypto`、`flex_color_scheme`、`lucide_icons_flutter` 与 `shared_preferences` 四个直接运行依赖；不存在 iOS 专用登录、浏览器或深链依赖。
3. lockfile 已包含 `shared_preferences_foundation 2.5.6`，说明现有依赖本身覆盖 iOS / macOS 的非敏感偏好与回访状态；它不适合承载 session token 或 OIDC verifier。
4. Android `MainActivity` 当前同时承载五组平台职责：session store、Forum follow-up、Docs follow-up、native auth、Android root Back / task lifecycle；其中 session、OIDC attempt 和 recent target 落入 Android 专属 `SharedPreferences` 文件。
5. `RadishBootstrap` 只在 `Platform.isAndroid` 时选择上述平台实现；iOS 会落到 `InMemorySessionStore`、`InMemoryNativeAuthGateway`、`InMemoryForumFollowUpStore`、`InMemoryDocsFollowUpStore` 与 `EmptyAppLifecycleGateway`。
6. 这意味着未经改造的 iOS 即使能编译，也无法完成系统浏览器 OIDC callback、会话冷启动恢复、pending login target、Forum / Docs 本机回访持久化；不能进入产品 Smoke。
7. Gateway 默认仍是 `https://localhost:5000`。iOS Simulator 可以访问主机 localhost，但本地开发证书放行目前只对 Android 自动开启；iOS 必须使用显式、仅 development 生效的编译参数，不扩大 production 证书边界。
8. [开发规范](/architecture/specifications)明确要求本地缓存不得明文存储账号、密码或令牌；P1 审计也已把 Android token 位于普通 `SharedPreferences` 记录为正式外部分发前必须处理的技术债。P7 不应把这项债务复制到 iOS 或普通 Dart preferences。

## 4. 架构裁决建议

P7-B 不复制一整套 Android `MainActivity` 存储代码到 Swift。为了让后续 iOS 和 desktop 共用同一长期边界，建议采用以下 owner 划分：

1. **敏感认证安全存储 owner**：session token 与 OIDC authorization attempt 使用跨 Android / iOS 的系统安全存储抽象，Android 落入 Keystore 支撑的安全能力，iOS 落入 Keychain；候选 Flutter 安全存储依赖的精确版本、许可证、平台选项与 lockfile 影响必须在安装前单独核定并获得依赖授权。
2. **非敏感 Dart 偏好 owner**：Forum / Docs recent target、recent profile 与 pending post-login target 基于既有 `shared_preferences`；保持 JSON 契约、最多 `5` 条、稳定去重与账号 / target 边界，不把 token 或 verifier 混入普通偏好。
3. **Android 幂等迁移适配**：Android 首次升级时只在新安全 key / preference key 缺失的情况下读取现有 MethodChannel / legacy `SharedPreferences`，写入新 owner 并回读确认后清除已迁移旧值，避免会话、授权尝试和回访历史无提示丢失；失败保留旧值并可重试。
4. **原生最小桥接 owner**：Android / iOS 只保留无法由 Dart 独立承担的系统职责，包括打开外部授权 URL、接收并一次性消费 `radish://oidc/*` callback，以及平台特有 lifecycle / deep-link 入口。
5. **平台分支显式化**：iOS 使用真实 native auth bridge、安全认证存储与非敏感偏好；Android 保持 `moveTaskToBack`；iOS 根返回不伪造 Android task 行为。
6. **开发证书 fail closed**：iOS Simulator 仅在明确传入 `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true` 且 Gateway 为 loopback 时允许本地开发证书；production 和非 loopback 继续拒绝。

这一方案会改变持久化 owner、bootstrap 和平台运行时行为，必须在实施前由项目所有者确认。若实施中发现 Flutter 生成模板、Swift Package Manager / CocoaPods 或 UIKit / UIScene 回调事实与本记录不一致，应先回写方案，不以临时双实现掩盖契约偏差。

## 5. P7 拆批

### P7-B：iOS platform foundation

- 生成并审阅 `ios/` 平台工程，固定 bundle ID `com.radish.client`、显示名 `Radish`、最低系统版本和 `radish` URL scheme。
- 经单独依赖授权后落地敏感认证安全存储；使用既有 `shared_preferences` 承载非敏感回访状态，并实现 Android 幂等迁移适配和 iOS native auth callback bridge。
- 补 session / authorization attempt / recent target 迁移与持久化测试、iOS callback 解析测试、bootstrap 平台选择测试。
- 执行 Flutter 定向 / 全量测试、analyze、Android JVM 回归、iOS Simulator 无签名构建和 `git diff --check`。

### P7-C：iOS Simulator runtime acceptance

- 单独确认服务启动、Simulator 启动、构建安装和真实 Smoke 授权。
- compact phone + tablet / large phone 覆盖启动、系统浏览器 OIDC、取消 / 成功 / 登出、会话恢复、冷启动、返回、键盘、安全区、五入口、代表高风险写入与四主题。
- 受控数据精确恢复，服务和 Simulator 状态按授权边界清理。

### P7-D：iOS physical-device / distribution gate

- 真实 iPhone、签名、provisioning、archive / IPA、分发与商店流程继续独立后置。
- P7-C 通过不自动给出真机、TestFlight、App Store 或 production `Go`。

## 6. 当前停止线

- 未确认 P7-B 架构方案前，不生成 `ios/`，不修改 Dart / Swift / Android 运行时代码。
- 未核定并单独授权安全存储依赖的精确版本、许可证、命令与 lockfile 影响前，不执行 `flutter pub add` 或等价依赖变更。
- 未获得当次运行授权前，不启动 Simulator、服务或真实 Smoke。
- 不检查、读取或提交真实签名证书、Apple Team、provisioning profile 或敏感账号材料。
- 不借 iOS 批次恢复 Flutter Web、Tauri、WebOS 新功能或完整移动功能追平。
