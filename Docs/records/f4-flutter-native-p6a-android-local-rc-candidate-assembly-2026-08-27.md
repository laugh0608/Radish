# Flutter Native P6-A Android 本地 RC 候选装配

> 状态：`P6-A` 已完成；本记录候选已被 P6-B 第一轮运行时修正取代
>
> 日期：2026-08-27（Asia/Shanghai）
>
> 前置记录：[P6 Android UI RC readiness](/records/f4-flutter-native-p6-android-ui-rc-readiness-2026-08-27)
>
> 后续结论：[P6-B Android AVD 第一轮运行态验收](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-2026-08-29) 已确认本哈希因运行时代码修正失效，不得继续作为当前候选。

## 1. 结论

P6-A 已按冻结边界完成。当前 `dev` HEAD `7eecd402` 的 Flutter 静态门禁、Android JVM 单测和 release APK 构建全部通过；候选显式使用 `development + https://localhost:5000`，包身份、版本、权限、OIDC 回跳、三 ABI 与 Android 平台契约一致。

候选 APK 使用 Android Debug 证书签署，但 manifest `debuggable=false`，属于“release 构建 + debug-signing fallback”。它可以进入本机 / 内部 P6-B、P6-C 验收，不具备外部分发资格。P6-A 完成只表示候选已装配，不表示 Android 新版 UI RC Go。

## 2. 固定候选

| 项目 | 结果 |
| --- | --- |
| 源码 | `dev` / `7eecd402` |
| 构建命令 | `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=development --dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000` |
| 产物 | `Clients/radish.flutter/build/app/outputs/flutter-apk/app-release.apk`；构建输出，不提交 Git |
| 构建时间 | `2026-08-27 22:55:37 +0800` |
| 字节大小 | `82,897,267` bytes |
| SHA-256 | `d7b1b9d1f12e5943bae7ddffe3daffcf6071d63ddb79a186ae16e05946234200` |
| Application ID / Label | `com.radish.client` / `Radish` |
| Version | `versionName 26.8.2` / `versionCode 1` |
| SDK | `minSdk 24` / `targetSdk 36` / `compileSdk 36` |
| ABI | `arm64-v8a`、`armeabi-v7a`、`x86_64`；每档均包含 `libapp.so` 与 `libflutter.so` |
| 权限 | `android.permission.INTERNET`；另有 AndroidX 生成的 app-scoped dynamic receiver permission |
| OIDC | `VIEW + DEFAULT + BROWSABLE`，`radish://oidc` |
| Debuggable | `false` |
| 签名 | 单 signer；`C=US, O=Android, CN=Android Debug`；证书 SHA-256 `5ae439a542a520b5baea7f4d969e9757f4c22b5714eaa7ec6255ae68a7474471` |
| 签名方案 | APK Signature Scheme v2 验证通过；v1 / v3 / v3.1 / v4 未使用 |

`lib/arm64-v8a/libapp.so` 的 AOT 字符串同时包含 `RADISH_ENVIRONMENT`、`AppEnvironment.developmentForCurrentPlatform`、`development`、`RADISH_GATEWAY_BASE_URL` 与 `https://localhost:5000`，因此候选不是沿用默认或旧环境配置生成。

## 3. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `flutter analyze` | 零问题 |
| `flutter test` | `419 / 419` |
| Android JVM `:app:testDebugUnitTest --offline --no-daemon` | `7 / 7` |
| release APK | `assembleRelease` 成功；最终一次构建 `136.1s` |
| `apksigner verify --verbose --print-certs` | 通过；证书与签名性质见 2 节 |
| `npm run check:long-id-safety` | `758` 个文件，零回归 |

Android JVM 阶段继续报告两类非阻断 warning：Flutter Gradle 内嵌 Kotlin `2.0.21` 与项目声明 Kotlin `2.2.20` 的兼容性提示，以及 Flutter Gradle task 在执行期访问 `Task.project`、面向 Gradle 10 的 deprecated feature 提示。单测和 release 构建均成功；本批没有通过升级 Kotlin、AGP、Gradle、Flutter 或吞警告改变结果。

首次 release 构建需要补齐当前 Flutter engine revision `4c525dac5ebe5971c5708ef73558ed8edcf4a362` 的 arm64 / x86_64 AAR。原 Wi-Fi 下 `127.0.0.1:10808` 代理大文件连接发生低速和一次 x86_64 从零重试；切换网络后仍通过同一代理，冻结命令原样在 `136.1s` 内成功。恢复期间下载并按官方 HTTP 元数据核验的临时 AAR 没有注入项目或 Gradle 缓存，最终候选完全来自原生 Flutter / Gradle 构建链；没有依赖版本漂移。

## 4. 边界与后续

- 本批没有修改 Dart、Android 平台工程、API、DTO、数据库、migration、依赖、lockfile 或 Pen；仓库只新增 / 更新文档。
- 没有启动 API / Auth / Gateway、AVD 或 Flutter 应用，没有安装 APK、登录、制造测试数据、截图或执行真实运行态 Smoke。
- 没有创建 AAB、tag、GitHub Release、镜像、部署或任何外部分发材料。
- P6-B 必须使用本记录固定 SHA-256 的 APK；若候选代码或构建产物变化，旧哈希不得继续继承，受影响矩阵需要重新验收。
- 下一顺位是 P6-B compact / medium AVD 运行态验收。开始前仍需单独授权 `./start.sh` 选项 `8`、ADB / AVD 启停、临时 medium AVD 创建 / 删除、APK 安装与运行态取证。
- P6-B 通过后仍需 P6-C 同哈希真机验收，三段全部通过才可记录 Android 新版 UI 本地 / 内部 RC Go；正式签名、外部分发、iOS 与 desktop 继续后置。
