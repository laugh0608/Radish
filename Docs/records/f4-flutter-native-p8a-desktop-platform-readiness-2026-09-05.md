# Flutter Native P8-A desktop platform readiness

> 日期：2026-09-05（Asia/Shanghai）
>
> 源码基线：`dev@3034b635`
>
> 结论：`P8-A readiness Go`；桌面产品化仍为 `No-Go`，下一顺位进入 `P8-B macOS local platform foundation readiness`

## 1. 范围与停止线

本批只确认 Flutter Native 扩展到 Windows、macOS 与 Linux 所需的仓库能力、共享 UI 门禁、宿主工具链和虚拟机边界：

- 不运行 `flutter create --platforms=... .`，不生成 `windows/`、`macos/` 或 `linux/`；
- 不修改 Dart / 原生 runtime，不新增依赖，不安装 SDK、Visual Studio workload 或 Linux 系统包；
- 不启动 Radish 服务，不连接生产 Gateway，不产生业务写入；
- 只启动日常 Windows VM 做无敏感工具链清点，`Windows11-ARM64-CleanBase` 保持停止且未修改；
- 不恢复 Tauri，不把 WebOS `/desktop` 当作 Flutter desktop UI。

Flutter 官方支持向既有项目补充 desktop 平台目录，但该操作会生成三个原生 runner，属于后续经确认的实施批次而不是 readiness 动作，参见 [Desktop support for Flutter](https://docs.flutter.dev/platform-integration/desktop)。

## 2. 仓库事实

| 领域 | 当前事实 | P8 影响 |
| --- | --- | --- |
| 平台工程 | `Clients/radish.flutter/` 只有 `android/` 与 `ios/` | 三个 desktop runner 均未建立，不能构建桌面 App |
| runtime 选择 | `RadishPlatformKind` 只有 `android / ios / unsupported`；Windows、macOS、Linux 当前全部进入 `unsupported` | desktop 会回退到内存 session、内存 OIDC attempt / follow-up、空 lifecycle，重启即丢失，登录 URL 也不会由系统浏览器打开 |
| 原生认证 | `PlatformNativeAuthGateway` 依赖 `radish.flutter/native_auth` MethodChannel；当前只有 Android / iOS handler | desktop 必须建立系统浏览器打开、`radish://oidc` 回调、冷启动 / 已运行实例交付与一次性消费 owner |
| 安全存储 | 锁定 `flutter_secure_storage 10.3.1`，解析到 Darwin `0.3.2`、Windows `4.2.2`、Linux `3.0.2` | 插件声明支持三桌面端，但 macOS 需 Keychain entitlements，Windows 需 C++ ATL，Linux 需 libsecret 与 keyring；不能继续使用 `unsupported` 内存 fallback |
| 非敏感偏好 | `shared_preferences 2.5.5` 解析到 foundation `2.5.7`、Windows / Linux `2.4.1` | 三桌面实现已在 lockfile 中，可复用现有 forum / docs / theme owner |
| Gateway | development 默认 `https://localhost:5000`；非 Android 默认不接受本地开发证书 | macOS 可使用宿主 localhost 并显式 opt-in；Windows / Linux VM 的 localhost 指向客户机，必须另行冻结同机服务或宿主路由 / 证书方案，不能自动沿用生产 Gateway 风险批准 |
| 共享 UI | compact / medium / expanded 页面族已有大量 `1024+` widget 证据；定向平台 / 导航测试 `11 / 11` 通过 | 结构可继承，但直接桌面证据只覆盖 `Ctrl + 1..5` 与一次 Tab 焦点；尚无成组 hover、滚轮 / scrollbar、方向键、Enter / Space、Escape、连续 Tab 顺序、真实窗口缩放与关闭 / 重启证据 |

`flutter analyze` 在本批为零问题。现状说明 desktop 不是“补三个目录即可完成”：必须先关闭持久化、认证、输入和窗口 owner。

## 3. macOS 宿主事实

本机 `flutter doctor -v`：

- macOS `26.6.2` ARM64；
- Flutter `3.44.0` stable、Dart `3.12.0`；
- Xcode `26.6`、CocoaPods `1.16.2`；
- `macOS (desktop)` device 可发现；Xcode / macOS toolchain 通过；
- 唯一提示为 `NO_PROXY` 尚未包含 `::1`，不阻断当前工程生成或非 IPv6 loopback 构建。

macOS 是当前唯一已经具备原生构建工具链的 desktop 平台，因此优先进入 P8-B。Flutter 官方要求 Xcode 与 CocoaPods，并以 `flutter doctor -v` 和 macOS device 作为 setup 核对入口；当前宿主满足这些基础条件，参见 [Set up macOS development](https://docs.flutter.dev/platform-integration/macos/setup)。

P8-B 仍须单独冻结：

- Runner identity、最低系统版本、应用名 / 图标和版本 owner；
- App Sandbox 的 `network.client`、Keychain Sharing、DebugProfile / Release entitlement 对称性；
- 系统浏览器 OIDC 与 custom scheme 的冷 / 热回调；
- 本机开发 Gateway 与本地证书 opt-in；
- 窗口初始 / 最小尺寸、缩放、关闭、重启恢复和桌面输入矩阵。

macOS 分发签名、公证、DMG / PKG、App Store 与自动更新均不进入 P8-B；Flutter 官方也将 build、sandbox entitlement 与 distribution / notarization 分开处理，参见 [Building macOS apps with Flutter](https://docs.flutter.dev/platform-integration/macos/building)。

## 4. Windows VM 事实

UTM 当前有两台 Windows 11 ARM64 VM：

- `Windows11-ARM64`：日常开发测试 VM；本批短暂启动并完成只读清点；
- `Windows11-ARM64-CleanBase`：干净基线；本批未启动、未修改。

日常 VM 的实时清点结果：

| 项目 | 结果 |
| --- | --- |
| OS | Windows build `26200`，ARM64 |
| Git | `2.55.0.windows.3`，可用 |
| Windows SDK | `10.0.26100.0` 存在 |
| Flutter / Dart | 未安装或不在 PATH |
| CMake / Ninja / clang-cl | 未安装或不在 PATH |
| Visual Studio | 存在 `2022/BuildTools` 目录，但 `vswhere` 对 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 返回空列表，Flutter 所需 C++ workload 未成立 |
| Radish workspace | 常见本地目录未发现；UTM 未配置项目共享目录 |

Flutter 官方要求 Windows 宿主安装 Visual Studio 的 `Desktop development with C++` workload，并以 `flutter doctor -v` 与 `flutter devices` 关闭工具链门禁，参见 [Set up Windows development](https://docs.flutter.dev/platform-integration/windows/setup)。当前 VM 因 Flutter SDK 与 C++ Desktop workload 缺失而为 `No-Go`。

锁定的 `flutter_secure_storage` 还要求 Visual Studio Build Tools 中的 C++ ATL optional component，因此 P8-C 工具链准备不能只安装最小 CMake / MSVC。任何 Flutter SDK、Visual Studio workload / ATL、CMake / Ninja 安装均属于依赖 / 工具安装，必须另行说明版本、体积、VM 影响与清理方式后授权。

本批发现 Windows guest agent 能执行命令与传输文件，但 stdout 未直接回传；后续自动化固定使用“VM 内生成无敏感报告 → `utmctl file pull` → 精确删除”的证据方式。清点完成后 VM 内三份临时文件已删除，两台 Windows VM 均已确认停止。

## 5. Linux VM 事实

UTM 已有 `Debian13-ARM64` 与 `Debian13-ARM64-CleanBase`，本批均保持停止，没有取得当前 Flutter / GTK / compiler / libsecret / keyring 实时证据。因此 Linux 不能写作工具链就绪。

Flutter 官方的 Debian 系工具链至少包括 `clang`、`cmake`、`ninja-build`、`pkg-config`、`libgtk-3-dev` 与 `libstdc++-12-dev`，并要求 `flutter doctor -v` 和 Linux device 成立，参见 [Set up Linux development](https://docs.flutter.dev/platform-integration/linux/setup)。Radish 现有安全存储还额外要求 `libsecret-1-dev` / `libsecret-1-0` 和可用 keyring service。

P8-D 应先使用日常 Debian VM 做只读清点；需要安装时单独授权。`Debian13-ARM64-CleanBase` 只在候选构建与安装验收阶段使用，避免把准备态污染为干净基线。

## 6. P8 分批顺序

1. `P8-A desktop platform readiness`：本记录关闭；明确 desktop 共同阻断、三宿主工具链与 VM 边界。
2. `P8-B macOS local platform foundation`：先完成 readiness，再经确认生成 `macos/`、建立真实 desktop platform services / OIDC / storage / entitlement / window owner，并在本机完成未分发候选。
3. `P8-C Windows toolchain + platform foundation`：先经安装授权补齐 Flutter `3.44.x` 对齐工具链、Visual Studio C++ Desktop workload / ATL，再使用日常 VM 生成、构建和运行；CleanBase 后置候选验收。
4. `P8-D Linux toolchain + platform foundation`：先实时审计 Debian 日常 VM，再按授权补齐 Flutter、GTK / compiler、libsecret / keyring 和 Linux runner；CleanBase 后置候选验收。
5. `P8-E grouped desktop gate`：同一 Dart 源码在 macOS、Windows 与 Linux 关闭四主题、键鼠、焦点、滚动、窗口、OIDC、持久化、Gateway、构建制品和精确清理矩阵。

每个平台的本地 `Go` 只代表开发 / 内部候选；签名、公证、安装器、商店、自动更新与外部分发必须另开 readiness，不从本地构建结果推导。

## 7. 下一步建议

下一顺位只进入 `P8-B macOS local platform foundation readiness`，先冻结方案，不修改代码。建议边界是：

- 不新增 Dart package，优先延续现有 MethodChannel 与安全存储 owner；如果 readiness 证明新依赖明显更可靠，再单独提依赖授权；
- macOS 第一批只建立单窗口、系统浏览器 OIDC、持久会话 / 偏好、网络访问和现有自适应 UI，不扩多窗口、托盘、菜单栏、后台服务或系统通知；
- 不复用 WebOS UI，不恢复 Tauri；
- 不进入签名、公证、DMG / PKG 或外部分发；
- 实施前须由项目所有者确认平台工程、runtime owner 与验证矩阵。
