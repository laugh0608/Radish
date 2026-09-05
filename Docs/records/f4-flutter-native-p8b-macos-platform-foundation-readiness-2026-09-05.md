# Flutter Native P8-B macOS local platform foundation readiness

> 日期：2026-09-05（Asia/Shanghai）
>
> 源码基线：`dev@ccc27515`
>
> 结论：`P8-B readiness Go`；实施仍为 `No-Go`，等待项目所有者确认后进入 `P8-B1 macOS platform foundation implementation`

## 1. 范围与停止线

本批只冻结 macOS 本地平台基座的工程身份、runtime owner、OIDC、安全存储、entitlement、窗口和验证边界：

- 不运行 `flutter create`，不生成 `macos/`，不修改 Dart / Swift 代码；
- 不新增或升级 Dart package，不修改 `pubspec.yaml` / `pubspec.lock`；
- 不启动 Radish 服务，不连接生产 Gateway，不执行真实登录或业务写入；
- 不读取 Apple Team、证书、provisioning 或 App Store Connect 信息；
- 不进入签名、公证、DMG / PKG、App Store、自动更新、托盘、多窗口或后台服务。

本批直接核对本机 Flutter `3.44.0` SDK template、锁定插件源码和现有 Android / iOS runtime；Flutter 官方说明既有项目可以按平台补充 desktop runner，但 runner 生成只是平台工程起点，不替代应用自己的原生能力与验收，参见 [Desktop support for Flutter](https://docs.flutter.dev/platform-integration/desktop)。

## 2. 固定实现决策

| 领域 | P8-B 固定方案 | 不进入本批 |
| --- | --- | --- |
| 平台工程 | 在 `Clients/radish.flutter/` 使用当前 Flutter SDK 只生成 `macos/`；保留生成模板的 Swift / Xcode 结构 | Windows / Linux runner、Tauri / WebOS 复用 |
| 产品身份 | `PRODUCT_NAME = Radish`、`PRODUCT_BUNDLE_IDENTIFIER = com.radish.client`、URL scheme `radish`；版本继续唯一读取 `pubspec.yaml` 的 `26.8.2+1` | Apple Team、正式签名身份、商店 metadata |
| 系统版本 | 保持当前 Flutter `3.44.0` template 的 `MACOSX_DEPLOYMENT_TARGET = 10.15`；本批只在当前 ARM64 macOS `26.6.2` 宿主验收，不宣称旧系统兼容已通过 | 跨 macOS 版本兼容矩阵、Intel 候选 |
| App Icon | 以仓库现有品牌源 `Docs/images/RadishAcg-1024.png` 机械生成 macOS `16–1024` AppIcon；不使用 Flutter 默认图标 | Android / iOS 历史默认图标治理、全平台品牌资产重做 |
| runtime 选择 | `RadishPlatformKind` 增加 `macos` 并检测 `Platform.isMacOS`；macOS 使用真实安全 session、OIDC attempt、Forum / Docs / theme preference owner | Windows / Linux 继续保持显式 `unsupported`，直到 P8-C / D |
| 原生认证 | 继续复用 `radish.flutter/native_auth` MethodChannel；macOS 用 `NSWorkspace` 打开唯一允许的 HTTP(S) 认证 URL，用 `CFBundleURLTypes` + `NSApplicationDelegate.application(_:open:)` 接收 `radish://oidc/*` | 新 OIDC package、内嵌 WebView、loopback callback server |
| 安全存储 | 继续使用锁定的 `flutter_secure_storage 10.3.1`；macOS 显式使用 `MacOsOptions(accessibility: first_unlock_this_device, synchronizable: false, usesDataProtectionKeychain: true)` | iCloud Keychain 同步、App Group、Secure Enclave、生物识别门禁 |
| 非敏感偏好 | 继续使用 `SharedPreferencesAsync` 保存 Forum / Docs recent、recent profile、pending post-login target 与主题偏好 | 自建数据库或新增 desktop storage 抽象 |
| 生命周期 | macOS 不调用 Android `moveTaskToBack` channel；现有 `EmptyAppLifecycleGateway` 保持根返回 no-op | 后台常驻、托盘、隐藏到菜单栏 |
| 窗口 | 单窗口；首次内容区 `1280 × 800`，最小内容区 `390 × 600`，允许缩放、最小化和全屏；保存主窗口 frame；关闭最后窗口即退出 | 多窗口、独立内容窗口、自定义标题栏 |
| 菜单 | 保留模板标准 App / Edit / View / Window / Help 菜单与系统快捷键；移除没有 owner 的空 `Preferences…` 入口 | 自定义功能菜单、菜单栏常驻图标 |

`com.radish.client` 与 `radish` scheme 沿用现有 iOS 产品身份，避免为同一 Native 产品人为引入第二套身份。macOS `10.15` 只是当前 SDK template 与锁定安全存储实现共同支持的工程下限；只有当前宿主会形成 P8-B 运行态证据。

## 3. macOS runtime owner

### 3.1 Dart 侧

`RadishPlatformServices.forPlatform(RadishPlatformKind.macos)` 固定返回：

- `SecureSessionStore`；
- `PlatformNativeAuthGateway` + `SecureAuthorizationAttemptStore`；
- `PersistentForumFollowUpStore`；
- `PersistentDocsFollowUpStore`；
- `EmptyAppLifecycleGateway`；
- 无 Android legacy migration。

`FlutterSecureValueStore` 同时显式配置 iOS 与 macOS 的 `first_unlock_this_device`。两端都保持 `synchronizable: false`，OIDC attempt 与 refresh token 不进入 iCloud 同步。Windows / Linux 仍不得借 macOS 改动提前脱离 `unsupported`。

### 3.2 Swift 侧

macOS 原生认证拆为三个职责：

1. `MainFlutterWindow` 创建 Flutter controller 后，用其 binary messenger 注册 `radish.flutter/native_auth`；Flutter 官方的 macOS channel 示例也在 `MainFlutterWindow.swift` 取得 messenger，参见 [Writing custom platform-specific code](https://docs.flutter.dev/platform-integration/platform-channels)。
2. `AppDelegate` 持有单一 pending callback slot，实现 HTTP(S) 浏览器打开、一次性 `takePendingCallback`、custom-scheme 冷 / 热接收，并在收到合法 callback 后激活主窗口。
3. `RadishNativeAuthCallbackPayloads` 只接受 `radish://oidc/callback` 与 `radish://oidc/logout-complete`，复用 iOS 既有 JSON 契约；错误、code、state 与 `error_description` 继续规范化，其他 scheme / host / path fail closed。

Apple 的 AppKit 契约要求在 `Info.plist` 声明 `CFBundleURLTypes` 后通过 `application(_:open:)` 接收 URL；系统浏览器打开使用 `NSWorkspace.open(_:)` 的布尔结果转成成功或 `open_failed`，参见 [application(_:open:)](https://developer.apple.com/documentation/appkit/nsapplicationdelegate/application%28_%3Aopen%3A%29) 与 [NSWorkspace.open(_:)](https://developer.apple.com/documentation/appkit/nsworkspace/open%28_%3A%29)。

pending callback 只保留一个值符合当前“同一时刻只有一个 authorization attempt”的 owner；Dart 侧取出后立即清空，state / PKCE / redirect / `15` 分钟超时仍由现有 `NativeAuthController` fail closed。

## 4. entitlement 与本地 Gateway

生成模板的 App Sandbox 保留。两个 entitlement 文件都增加：

- `com.apple.security.network.client = true`；
- `keychain-access-groups = []`。

`DebugProfile.entitlements` 原有 `com.apple.security.cs.allow-jit` 与 `com.apple.security.network.server` 必须保留；Release 不增加 network server。Flutter 官方明确要求沙盒应用访问网络时添加 `network.client`，并建议共同能力在 DebugProfile / Release 两份文件对称维护，参见 [Building macOS apps with Flutter](https://docs.flutter.dev/platform-integration/macos/building)。锁定的 `flutter_secure_storage` 同时要求两份文件存在空 `keychain-access-groups`。

P8-B2 真实运行固定使用宿主本地 Gateway：

```text
RADISH_ENVIRONMENT=development
RADISH_GATEWAY_BASE_URL=https://localhost:5000
RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true
```

现有环境守卫只允许 development + loopback + 精确 host / port 绕过本地开发证书，因此不扩大 testing / production TLS 边界。P7-D 对生产 Gateway 的临时风险批准不自动沿用到 desktop；P8-B 不连接生产数据。

## 5. 窗口与 desktop input

首次启动以 expanded `1280 × 800` 呈现，窗口缩窄时继续经过既有 `>=1024 / 600–1023 / <600` 三档布局。`390 × 600` 最小内容区保证 compact 设计仍可在桌面窄窗验证；主窗口 frame 使用稳定 autosave name 恢复。关闭红色按钮或最后一个窗口会退出进程，`Cmd + Q` 保持系统标准退出；根页面 Escape / back 不隐藏进程。

P8-B 的代表输入门禁固定为：

- pointer：导航与代表按钮 hover / tooltip、点击、右侧内容滚轮或触控板滚动；
- focus：Tab / Shift+Tab 连续遍历可操作控件，焦点样式可见；
- activation：Enter / Space 激活当前按钮，不重复提交；
- navigation：`Cmd + 1..5` 与既有 `Ctrl + 1..5` 切换主入口；Escape 先关闭菜单 / Dialog / 表单保护层，再返回 inline detail，根层 no-op；
- window：首次尺寸、最小尺寸、跨三档 resize、最小化 / 恢复、全屏 / 恢复、关闭退出、重启 frame 恢复；
- scrolling：滚轮 / 触控板和键盘滚动必须可用；scrollbar 可见性遵循 macOS 系统偏好，不强制常驻。

优先依赖 Flutter Material / Widgets 的原生 hover、focus、DismissIntent 与 ScrollBehavior；只有真实或 widget 证据失败时才增加共享 UI 代码，不为“桌面感”先做全量重构。

## 6. 实施文件边界

P8-B1 预计只修改：

- 新增 `Clients/radish.flutter/macos/` runner、AppIcon 与 RunnerTests；
- `lib/app/platform_services.dart`；
- `lib/core/storage/secure_value_store.dart`；
- `test/platform_services_test.dart` 与必要的 desktop 输入定向测试；
- Flutter README、专题、当前规划、记录和 changelog。

不修改后端接口、Web、数据库、Android / iOS runtime 行为或依赖版本。macOS callback parser 与 iOS 当前契约保持镜像；本批不为了共享少量 Swift 代码而改动已关闭的 iOS Xcode 工程。

## 7. 验证与退出条件

### P8-B1：工程与无服务构建

1. `flutter analyze` 零问题；
2. platform services / persistence / auth / adaptive navigation 定向测试通过；
3. Flutter 全量测试通过；
4. macOS RunnerTests 覆盖 callback login / logout / error / 非法 URL 与一次性 pending 语义；
5. `flutter build macos --debug` 与 `flutter build macos --release` 通过；
6. DebugProfile / Release entitlement、bundle id、version、URL scheme 和 AppIcon 静态检查通过；
7. Android / iOS 定向平台回归不退化。

B1 不需要启动 Radish 服务，也不需要 Apple Developer Program。构建产物只作为本机未分发候选，不生成 DMG / PKG 或外部发布结论。

### P8-B2：本地真实运行

B1 关闭后，先单独说明并取得项目启动授权，再启动本地 Gateway / Auth / API 和 macOS app。B2 至少关闭：

- guest 公开读取与本地 TLS 精确边界；
- 系统浏览器 OIDC 的热 callback 与完全退出后的冷 callback；
- Keychain session / OIDC attempt、SharedPreferences theme / recent 在退出重启后的恢复；
- 登出清理与 callback 重放 fail closed；
- expanded / medium / compact 窗口、四主题、键鼠 / focus / scroll 代表矩阵；
- 测试账号、数据库写入、进程和构建候选的精确清理记录。

B2 结束只能给出 `macOS local platform foundation Go`；公证、签名、安装器、更新、商店与旧系统兼容仍保持 `No-Go`。

## 8. 下一步

等待项目所有者确认 P8-B1 实施。确认后：

1. 只生成 macOS runner，不运行 package 安装或依赖更新；
2. 按本记录建立 identity、AppIcon、entitlement、OIDC、持久化与窗口 owner；
3. 完成无服务测试和 Debug / Release 本机构建；
4. 提交 P8-B1 实施记录；
5. 再单独申请 P8-B2 所需的项目启动授权。
