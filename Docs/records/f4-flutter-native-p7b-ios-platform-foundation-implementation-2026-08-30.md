# Flutter Native P7-B iOS platform foundation 实施记录

> 状态：`P7-B 已完成，Android / iOS 原生 build 门禁通过`
>
> 日期：2026-08-30（Asia/Shanghai）
>
> 前置：[P7-A iOS 平台 readiness](/records/f4-flutter-native-p7a-ios-platform-readiness-2026-08-30)

## 1. 当前结论

P7-B 已按项目所有者确认的架构方案建立 iOS 平台工程，并将敏感认证状态、非敏感本机偏好、Android 旧数据迁移和原生系统职责拆回明确 owner。Flutter / Dart 全量、Android JVM / Debug APK、iOS 无签名 Simulator build、Swift callback parser 与 Xcode 测试目标编译门禁均已通过，仓库不再把 iOS 退化为内存 session / auth / follow-up。

本批 **Native build gate Go**，P7-B 正式关闭。P7-C 尚未启动：本批没有启动 Gateway、Auth、API、Simulator 或 AVD，没有安装 App、登录账号或执行真实 Smoke；P7-C readiness、运行方案和服务 / Simulator 启动仍需作为下一批单独确认。

## 2. 依赖裁决

经单独授权执行：

```text
cd Clients/radish.flutter
flutter pub add 'flutter_secure_storage:^10.3.1'
```

裁决结果：

1. 直接依赖固定为 `flutter_secure_storage 10.3.1`，许可证为 BSD-3-Clause；lockfile 新增该插件及 Darwin、Linux、Web、Windows、JNI / FFI 等必要传递包，没有顺带升级既有锁定版本。
2. `10.3.1` 在 Android 默认使用 RSA-OAEP + AES-GCM，在 iOS 使用 Keychain；项目显式设置 Android `resetOnError: false`、`migrateWithBackup: true`、`storageNamespace: radish_auth_v1`，iOS 使用 `first_unlock_this_device`。
3. 未采用最新 `11.0.0`：该版本要求 Android `compileSdk 37`，而本机与当前 Flutter 工程只有 Android `35 / 36 / 36.1`；为本批强行安装 SDK 37 会无必要扩大 Android 工具链范围。
4. Android manifest 设置 `android:allowBackup="false"`，避免安全存储密文随应用备份迁移后与设备 Keystore 不匹配。

## 3. 平台 owner

### 3.1 敏感认证状态

- `SecureSessionStore` 以 `radish.auth.session.v1` 保存 session JSON。
- `SecureAuthorizationAttemptStore` 以 `radish.auth.oidc_attempt.v1` 保存 state、PKCE verifier、redirect URI 与开始时间，并保持先读取、再清除的一次性消费。
- Android 由 Keystore 支撑；iOS 由 Keychain 支撑。token、refresh token 与 verifier 不进入 `shared_preferences`。

### 3.2 非敏感本机偏好

- Forum recent targets、recent profile 与 pending post-login target 由 `PersistentForumFollowUpStore` 承担。
- Docs recent targets 由 `PersistentDocsFollowUpStore` 承担。
- Forum / Docs 各保持最多 `5` 条、最新优先、稳定去重；Android / iOS 共用 Dart JSON 契约。
- Android Intent pending forum handoff 仍由原生一次性入口提供；iOS 当前没有系统通知 handoff owner，平台 channel 明确返回空，不伪造能力。

### 3.3 Android 幂等迁移

`AndroidLegacyPersistenceMigrator` 在 `runApp` 前执行。每个状态只在新 owner 缺失时读取旧 Android `SharedPreferences`；写入新 owner 后必须回读并按业务身份验证，验证成功才清除对应旧值。安全写入或回读失败会抛出错误并保留旧值，下一次启动可重试。

Android `MainActivity` 不再接受 session / OIDC attempt 明文写入，只保留迁移期读取 / 清除、外部浏览器、callback、Intent handoff 与 Android task lifecycle 等原生职责。

### 3.4 显式平台选择

`RadishPlatformServices` 固定：

| 平台 | Session / OIDC | Follow-up | Auth bridge | Root lifecycle |
| --- | --- | --- | --- | --- |
| Android | 安全存储 + 旧值迁移 | Dart preferences + Intent pending | 原生浏览器 / callback | `moveTaskToBack` |
| iOS | 安全存储 | Dart preferences | 原生浏览器 / callback | 空实现，不模拟 Android task |
| 其他未产品化平台 | 内存开发壳 | 内存开发壳 | 内存开发壳 | 空实现 |

## 4. iOS 工程

1. 基于 Flutter 3.44 当前 UIScene / Swift Package Manager 模板生成 `ios/`，最低 iOS 为 `13.0`。
2. Runner 固定 bundle ID `com.radish.client`、显示名 / bundle name `Radish`、`radish` URL scheme；RunnerTests 为 `com.radish.client.RunnerTests`。
3. `Debug / Profile` 使用 `DebugProfile.entitlements`，Release 使用 `Release.entitlements`，均声明 Keychain access groups。
4. `AppDelegate` 只注册插件、native auth 与当前为空的 iOS forum pending channel；`SceneDelegate` 承接冷启动和运行中 URL context。
5. callback parser 只接受 `radish://oidc/callback` 与 `radish://oidc/logout-complete`，登录 payload 只转发 code、state、error 与 error description；最终 state、TTL、PKCE、错误映射与重放校验仍由 Dart owner fail closed。
6. Flutter 生成器曾自动探测本机签名身份；生成后立即移除全部 `DEVELOPMENT_TEAM` 设置。仓库未读取、记录或提交 Apple Team、证书、provisioning profile 或账号材料。

## 5. 已取得证据

| 验证 | 结果 |
| --- | --- |
| 新增持久化 / 平台选择 / 认证定向 | `18 / 18` |
| Flutter 全量 | `435 / 435` |
| `flutter analyze` | 零问题 |
| iOS plist / entitlements `plutil -lint` | 全部通过 |
| Swift callback parser standalone compile + smoke | 登录、取消、登出、不可信 URI 全部通过 |
| `xcodebuild -list -json` | Runner / RunnerTests、FlutterFramework、secure storage Darwin 与 shared preferences Foundation scheme 可解析 |
| Android JVM `:app:testDebugUnitTest --no-daemon` | `7 / 7`，`BUILD SUCCESSFUL` |
| Android `flutter build apk --debug` | 新 APK `190,234,671` bytes；SHA-256 `b44fbc568cbd8317f40617873fd41c869fd28f5f06aafbc2d3d3dbf90883ecb3` |
| iOS `flutter build ios --simulator --debug --no-codesign` | Xcode build `21.5s`；生成 `build/ios/iphonesimulator/Runner.app`，bundle ID `com.radish.client` |
| iOS `xcodebuild build-for-testing ... CODE_SIGNING_ALLOWED=NO` | Runner 与 RunnerTests 无签名编译通过，生成 `RunnerTests.xctest`，未启动 Simulator |

全量测试新增覆盖：

- secure session round-trip / clear；
- OIDC attempt 一次性消费；
- Forum / Docs 五条上限与稳定去重；
- Android 全量旧状态迁移、回读验证后清除与再次执行幂等；
- 安全写入失败时旧 session 保留；
- Android / iOS / unsupported bootstrap owner 选择。

原生门禁收口过程同时确认：

- Android 首条未限定项目的 `testDebugUnitTest` 会连带解析插件子工程测试；发现后中止过宽任务，按验证基线改用 `:app:testDebugUnitTest --no-daemon`，不把插件下载等待冒充应用门禁。
- 官方 iOS engine `ios / ios-profile / ios-release` 三包使用同一 Flutter revision `4c525dac5ebe5971c5708ef73558ed8edcf4a362`，按官方 `Content-Length` 与 MD5 全部校验后进入 Flutter 缓存；没有切换镜像或修改 Flutter 源码。
- 首次 `build-for-testing` 暴露 RunnerTests 未导入宿主模块，补 `@testable import Runner` 后重跑通过。
- Xcode 报告 `objective_c` code asset 跨架构 framework 名称 warning；来源为依赖包，未阻断 Flutter app、Runner 或 RunnerTests 编译，本批不通过修改第三方缓存吞掉 warning。

## 6. 后续停止线

- 没有启动 Gateway、Auth、API、Simulator 或 AVD，没有安装 App、登录账号或执行真实 Smoke。
- P7-C 必须先完成 Simulator runtime acceptance readiness，并单独确认服务、Simulator、安装、测试账号与清理边界后才能运行。
- iOS 真机、Apple 签名、TestFlight、App Store 与 production 继续属于 P7-D，不因 P7-B / P7-C 自动放行。
