# Flutter Native P8-B1 macOS platform foundation implementation

> 日期：2026-09-05（Asia/Shanghai）
>
> 源码基线：`dev@9868733f`
>
> 结论：`P8-B1 platform foundation Go`；`P8-B2 local runtime acceptance No-Go`，等待项目启动授权

## 1. 范围与停止线

本批按 [P8-B readiness](/records/f4-flutter-native-p8b-macos-platform-foundation-readiness-2026-09-05) 完成 macOS runner、平台 runtime owner、原生 OIDC、安全存储配置、窗口与无服务构建：

- 只生成 `Clients/radish.flutter/macos/`，没有生成 Windows / Linux runner；
- 没有新增或升级 Dart package，`pubspec.yaml` / `pubspec.lock` 保持不变；
- 没有启动 Gateway、Auth、API、macOS app 或虚拟机，没有连接生产 Gateway、执行真实登录或产生业务写入；
- 没有读取 Apple Team、证书、provisioning 或 App Store Connect 信息，也没有生成 archive、DMG / PKG 或外部分发状态。

## 2. 平台工程与产品身份

macOS runner 由当前 Flutter `3.44.0` template 生成，并收口为同一 Native 产品身份：

- `PRODUCT_NAME = Radish`；
- `PRODUCT_BUNDLE_IDENTIFIER = com.radish.client`；
- `CFBundleURLTypes` 只注册 `radish`，URL name 为 `com.radish.client.oidc`；
- 版本继续来自 `pubspec.yaml` 的 `26.8.2+1`；
- 工程下限保持 `macOS 10.15`；
- `Docs/images/RadishAcg-1024.png` 机械生成 `16 / 32 / 64 / 128 / 256 / 512 / 1024` AppIcon；Android / iOS 图标没有在本批扩改。

Release 产物的 `Info.plist` 实际确认 `Radish`、`com.radish.client`、`26.8.2 (1)`、`radish` scheme 与 `LSMinimumSystemVersion = 10.15`。本机构建产出 arm64 / x86_64 通用可执行文件，但本批只在当前 ARM64 macOS `26.6.2` 宿主形成编译与原生单测证据，不据此宣称 Intel 或旧 macOS 运行态已通过。

## 3. Dart runtime owner

`RadishPlatformKind` 新增 `macos` 并由 `Platform.isMacOS` 显式选择。macOS 现在使用：

- `SecureSessionStore`；
- `SecureAuthorizationAttemptStore` + `PlatformNativeAuthGateway`；
- `PersistentForumFollowUpStore` 与 `PersistentDocsFollowUpStore`；
- 既有 SharedPreferences theme owner；
- `EmptyAppLifecycleGateway`，不复用 Android `moveTaskToBack`。

`FlutterSecureValueStore` 为 macOS 显式配置 `first_unlock_this_device`、`synchronizable: false` 与 `usesDataProtectionKeychain: true`；iOS 同步显式固定 `synchronizable: false`，未改变原可访问级别。Windows / Linux 仍落入 `unsupported` 内存 shell，没有被本批提前产品化。

## 4. 原生 OIDC 与窗口 owner

macOS 复用 `radish.flutter/native_auth` MethodChannel：

- `MainFlutterWindow` 使用当前 Flutter engine binary messenger 注册 handler；
- `NSWorkspace.shared.open` 只接受 HTTP(S) authorization / logout URL，非法 scheme 返回 `invalid_url`，系统拒绝打开返回 `open_failed`；
- `AppDelegate.application(_:open:)` 拦截 `radish` URL，非 `radish` URL继续交给 Flutter；非法 `radish` URL fail closed，不下放为普通 deep link；
- parser 只接受 `radish://oidc/callback` 与 `radish://oidc/logout-complete`，规范化 `code / state / error / error_description`；
- AppDelegate 独占单一 callback store，首个合法回调进入 pending slot，Dart 读取后立即清空；合法回调会拉起主窗口并激活应用。

主窗口首次内容区为 `1280 × 800`，最小内容区为 `390 × 600`，使用 `RadishMainWindow` 保存 frame；模板的缩放、最小化与全屏能力保留，关闭最后窗口退出进程。没有 owner 的 `Preferences…` 菜单已经移除；未增加托盘、多窗口、后台常驻或自定义标题栏。

## 5. entitlement 实施修正

DebugProfile 与 Release 都保留 App Sandbox 并启用 `com.apple.security.network.client`；DebugProfile 继续保留 JIT 与 `network.server`，Release 不包含 `network.server`。

readiness 原计划按插件 README 在两份 macOS entitlement 中加入空 `keychain-access-groups`。实际 `flutter build macos --debug` 在 Xcode `26.6` 明确失败：该 restricted entitlement 即使为空也要求 development certificate，与项目所有者“当前没有 Apple Developer Program 会员”的既定条件冲突。锁定的 `flutter_secure_storage_darwin 0.3.2` 源码同时确认 `kSecAttrAccessGroup` 只在 iOS 分支加入查询，macOS 当前并不读取 `groupId`。因此 B1 移除 macOS 的空 Keychain Sharing capability，保留默认 macOS Keychain、Data Protection Keychain 与不参与 iCloud 同步的配置。

这项修正关闭了无会员本机构建阻断，但安全存储的真实写入、退出重启恢复和清理仍必须由 P8-B2 运行态验证，不以编译成功替代。

## 6. 验证结果

| 验证 | 结果 |
| --- | --- |
| `flutter analyze` | 通过，0 问题 |
| platform services / persistence / auth / adaptive navigation 定向测试 | `22 / 22` 通过 |
| `flutter test` | `438 / 438` 通过 |
| macOS RunnerTests | `5 / 5` 通过；覆盖 login、browser cancellation、logout、非法 URL 与一次性 pending |
| `flutter build macos --debug` | 通过，生成 `build/macos/Build/Products/Debug/Radish.app` |
| `flutter build macos --release` | 通过，生成 `build/macos/Build/Products/Release/Radish.app`，约 `93.0 MB` |
| plist / XIB / pbxproj / scheme | plist 与 pbxproj 通过 `plutil`；XIB 与 scheme 通过 `xmllint` |
| 产品身份 / 版本 / URL / 图标 | 实际 Release app 静态检查通过 |
| 签名后 entitlement | Debug 为 sandbox + JIT + client/server；Release 为 sandbox + client，无 server |
| `git diff --check` | 通过 |

Release 构建仍报告 `objective_c` code asset 的跨架构 framework name 警告，但双架构产物成功生成；这是锁定 package 的构建 hook 警告，本批没有升级依赖或用本地补丁掩盖。Xcode 的 Flutter Assemble run-script dependency-analysis 警告同样不影响构建与测试结果。

本机 Release 为 ad-hoc 本地候选，实际签名带有开发调试属性；它不构成 Developer ID、notarization、App Store 或可分发 Release 证据。

## 7. 结论与下一步

P8-B1 已关闭：macOS 不再是 `unsupported` 内存壳，平台工程、真实 runtime owner、原生 OIDC、品牌身份、窗口和无服务构建门禁均已落地。

下一步为 P8-B2 本地真实运行。进入前必须单独说明并获得 Gateway / Auth / API 与 macOS app 的启动授权；B2 使用 `development + https://localhost:5000 + RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true`，只连接本地数据，并验证 OIDC 冷 / 热 callback、Keychain / preferences 重启恢复、登出清理、回调重放、三档窗口、四主题、键鼠 / focus / scroll 与精确清理。P8-B2 未完成前，不给出 `macOS local platform foundation Go`。
