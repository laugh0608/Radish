# Flutter Native P6 Android UI RC readiness

- 记录日期：2026-08-27
- 适用版本：`26.8.2+1`
- 记录性质：Android 新版 UI RC 实施前审计与方案冻结
- 当前结论：`Ready for P6-A`；尚未构建候选 APK、启动服务、执行 AVD / 真机 Smoke 或形成 Android UI RC Go

## 1. 结论摘要

P1–P5 已关闭 Flutter Native 首轮主题、页面族与成组静态门禁，Android 平台工程、Gateway 同源配置、OIDC callback、原生 handoff、release signing 回落和平台单测入口仍完整。当前机器的 Flutter / Android SDK / JBR / Gradle 链路可用，Android JVM 单测 `7 / 7` 通过，进入 P6 不需要安装新依赖。

P6 实施固定拆为三段：

1. `P6-A Android local RC candidate assembly`：不启动服务，重跑静态门禁、构建显式指向本机 Gateway 的 debug-signing release APK，并冻结 APK 哈希、包身份、权限、版本和签名性质。
2. `P6-B Android AVD runtime acceptance`：获单独运行授权后，以 Android API 35 compact phone 与 medium tablet AVD 覆盖当前源码对齐的本机 Gateway；默认主题跑完整真实链路，四主题跑代表视觉矩阵。
3. `P6-C Android physical-device RC acceptance`：把与 P6-B 相同 SHA-256 的 APK 安装到真实 Android 设备，复核安装、OIDC 浏览器往返、会话 / Back / 冷启动与高风险写入，才允许给出“Android 新版 UI RC Go”。

本轮目标是**本地 / 内部 Android UI RC**，不是外部分发或商店发布。当前没有真实 release signing 材料，因此可继续使用既有 debug-signing fallback 构建本地候选，但外部分发保持 `No-Go`。iOS、Windows / macOS / Linux、AAB、商店、正式签名、更新与分发继续后置。

## 2. 现状证据

### 2.1 已继承的静态基线

- P5-E 已完成 P4 / P5 `29` 个代表测试入口 `396 / 396`、Shell `51 / 51`、Flutter 全量 `419 / 419` 与 `flutter analyze` 零问题。
- P5-E 只新增 `13` 个 widget tests，没有修改 `lib/`；因此本次 readiness 期间没有需要重新裁决的 Dart 运行时差异。
- Android 包身份保持 `com.radish.client`，应用名保持 `Radish`，主 manifest 继续声明 `INTERNET` 权限和 `radish://oidc/*` 浏览器回跳。
- `RADISH_GATEWAY_BASE_URL` 继续统一派生 API / Auth / Gateway；Android + `localhost` 开发入口只对该目标允许本地开发证书，并要求 `adb reverse tcp:5000 tcp:5000`。

### 2.2 2026-08-27 本机预检

| 项目 | 结果 | 结论 |
| --- | --- | --- |
| Flutter / Dart | Flutter `3.44.0` stable、Dart `3.12.0` | 可用 |
| Android toolchain | SDK `36.1.0`、build-tools `36.1.0`、licenses accepted | 可用 |
| Android Studio JBR | OpenJDK `21.0.10` | 可用；平台命令固定显式使用此 JBR |
| Gradle | wrapper `8.14` 已缓存 | 可离线复现 |
| Android JVM 单测 | `:app:testDebugUnitTest --offline --no-daemon`，`7 / 7` | 通过 |
| AVD | `Pixel_9_Pro` API 35、`Pixel_10_Pro` API 37.1，均为约 `427dp` compact | compact 条件可用；medium AVD 尚未创建 |
| 在线 Android 目标 | `flutter doctor -v` 只发现 macOS / Chrome | 当前没有在线模拟器或真机 |
| 正式签名 | `android/key.properties` 与 `upload-keystore.jks` 均不存在且被 Git ignore | 本地 debug-signing candidate 可行；外部分发阻塞 |
| 旧候选 APK | `build/app/outputs/flutter-apk/app-release.apk` 不存在 | P6 必须重新构建，不能误用旧包 |
| 本机数据 | Developer seed 已启用，SQLite 数据文件存在 | 可准备本机测试账号 / 数据；凭据仍由项目所有者手工输入 |
| APK 检查工具 | `apkanalyzer`、`aapt` / `aapt2`、`apksigner` 已安装 | 可冻结产物元数据 |

Gradle 当前会报告“Flutter Gradle 内嵌 Kotlin `2.0.21` 与项目声明 Kotlin `2.2.20`”的兼容性 warning，以及 Gradle 9 前的 deprecated feature 提示；本次平台单测仍成功。P6-A 记录完整 warning，若 release 构建失败再按根因回到依赖 / 构建链方案确认，不在 readiness 中升级 Kotlin、AGP、Gradle 或 Flutter。

## 3. 旧 Android RC 证据归属

2026-05-04 的 Android MVP RC Go 仍是有效历史事实：当时 release APK 已在小米 15S Pro / Android 16 上通过安装、OIDC、会话、主 tab、Forum / Docs / Profile 与最小通知回流复核。

以下内容可以继承为执行方法：

- `com.radish.client` / `Radish` / `INTERNET` / `radish://oidc/*` 平台契约；
- Android Studio JBR、Gradle 单测、显式 Gateway define、`adb reverse`、APK 签名检查与真机优先的验证分层；
- 服务、测试账号、数据、设备、证据与清理必须在具体 RC 批次重新记录。

以下结论不能继承为新版 UI RC 结果：

- 旧 `117` 个 Dart tests、旧 APK 哈希 / 构建时间和旧服务版本；
- 2026-05-04 的页面视觉、真机截图与人工 Go；
- 当时尚未进入范围的完整 Forum 写入 / 编辑、Commerce 私域、Wallet / Experience、Leaderboard、账号浏览历史、四主题和三档自适应页面族。

P1–P5 改动规模已使旧人工结果失效；P6 必须从当前 `dev` HEAD 重新装配并记录同一候选哈希。

## 4. P6-A：Android local RC candidate assembly

### 4.1 范围

P6-A 不启动 API / Auth / Gateway、不启动模拟器或 Flutter 应用，也不安装 APK。只执行：

1. `flutter analyze` 与 `flutter test`；预期保持零问题与 `419 / 419`。
2. Android Studio JBR 下执行 `./gradlew :app:testDebugUnitTest --offline --no-daemon`；预期 `7 / 7`。
3. 构建当前源码对齐的本地 release candidate：

```bash
flutter build apk --release \
  --dart-define=RADISH_ENVIRONMENT=development \
  --dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000
```

4. 记录 `app-release.apk` 的 SHA-256、字节大小、构建时间、`versionName / versionCode`、application id、应用名、`INTERNET` 权限和签名证书摘要。
5. 明确产物使用 debug signing fallback，只能进入本机 / 内部 UI RC；不得上传 Release、外发、创建 AAB、tag、镜像或部署。
6. 执行 LongId、Docs、changed repo hygiene 与 `git diff --check`，形成 P6-A 记录。

### 4.2 退出条件

- 自动化、Android JVM 单测和 release APK 构建通过；
- APK 元数据与项目契约一致，哈希唯一且记录完整；
- warning 已分类，未通过吞警告或依赖漂移取得结果；
- 没有服务、设备、登录、测试数据或外部分发副作用。

P6-A 完成只表示“可运行候选已装配”，不表示 Android UI RC Go。

## 5. P6-B：Android AVD runtime acceptance

### 5.1 设备与服务

- compact：既有 `Pixel_9_Pro`，Android API 35、ARM64、约 `427dp`。
- medium：复用已安装的 Android API 35 ARM64 system image，新建临时 `Radish_Medium_API_35`，device profile 使用 `medium_tablet`；不下载新 SDK / image。
- 服务：仓库根 `./start.sh` 选择 `8`，以 Debug 启动 Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200`；不启动 Client / Console。
- 设备接线：每个 AVD 建立 `adb reverse tcp:5000 tcp:5000`，安装 P6-A 固定哈希 APK。
- 身份与数据：只使用本机 developer seed 和项目所有者提供的测试凭据，不在文档、日志、截图或 shell 输出中记录密码 / token / 支付口令。

上述服务启动与设备运行都要在 P6-B 开始前单独授权。`./start.sh` 由同一前台会话持有，验收结束按 `Ctrl+C` 触发其进程组清理；medium AVD 在证据完成后关闭并删除，compact AVD 关闭，移除 `adb reverse tcp:5000`。

### 5.2 默认主题完整矩阵

| 领域 | 必须覆盖 |
| --- | --- |
| 平台 / Auth | 首装启动、匿名态、登录取消、OIDC 成功回跳、登出、会话恢复、冷启动、根层 Back 退后台、再打开恢复 |
| Shell / Theme | 五个主入口、主题切换与重启持久化、compact Bottom Sheet、medium Dialog、无系统栏 / 键盘遮挡 |
| Discover | 首屏、刷新旧数据保留、Forum / Docs / Shop handoff、来源返回 |
| Forum | feed / 分页 / 刷新、Compose 草稿与发布、detail、回答、轻回应、根评论 / 回复、作者正文 / 根评论编辑、失败保留、通知已读与评论定位 |
| Docs | 列表、搜索、分页、reader、原生内链、长 slug、逐层返回 |
| Identity / Revisit | 我的页、公开主页、资料编辑、最近 Forum / Docs、账号完整浏览历史、Leaderboard 公共身份回流 |
| Commerce | 商品列表 / 详情、登录回流、资格 / 余额 / 支付口令、购买、订单详情、订单列表、权益 / 道具来源返回 |
| Wallet / Experience | 概要与流水、订单筛选、分页 / 空态 / 局部失败、返回上下文 |
| 状态与布局 | loading / empty / unavailable / stale / append issue、长 ID / 长标题 / 长链接、compact 与 medium 无横向溢出 |

会制造业务事实的发帖、回答、评论 / 回复、轻回应、编辑、资料更新和购买只在本机数据上执行。所有新事实使用 `P6-RC` 可识别前缀并在记录中保留目标 ID；不得直接删库、重置 SQLite 或伪造成功响应。

### 5.3 四主题代表矩阵

四主题不与完整业务矩阵做笛卡尔积。`default` 承担 5.2 的完整链路；`guofeng / theme-dark-night / theme-sakura` 在 compact 与 medium 各覆盖：

1. Discover + Shell / 主题选择器；
2. Forum Detail 连续阅读与互动区；
3. Docs Reader 长正文；
4. Shop Product Detail 购买双区；
5. Profile 或 Wallet 私域数据面。

每个代表面确认语义前景、品牌 / 操作色、卡片层级、字体、长文本、滚动、输入与无溢出。静态 tests 已守卫的状态机与几何不在运行态重复扩成全页面 × 四主题矩阵。

### 5.4 证据

- 固定 `dev` commit、APK SHA-256、Gateway 基址和三宿主健康结果；
- 两个 AVD 的 API、ABI、logical size / density、安装包版本与签名摘要；
- 自动化日志、完整矩阵逐项结论、已知问题与未执行项；
- compact / medium、四主题和五类代表面的精选截图，原始临时证据与拟提交证据分开；
- 测试数据目标 ID、服务 / AVD / reverse 清理结果。

P6-B 通过仍只表示“Android AVD 新版 UI 候选成立”，不替代真机 RC。

## 6. P6-C：Android physical-device RC acceptance

### 6.1 前置

- 真实 Android 设备已连接并允许 ADB，记录机型、Android 版本、ABI、logical size / density；
- 安装的 APK SHA-256 必须与 P6-B 相同；若 P6-B 后修复代码并重建，旧运行证据全部标记为 superseded，并从新哈希重跑受影响矩阵；
- 安装前先只读确认设备是否已有 `com.radish.client`。若已有，不默认覆盖、清数据或卸载，先由项目所有者确认保留 / 替换策略；
- 服务、Gateway、测试账号和 `adb reverse` 与 P6-B 同口径。

### 6.2 真机主证据

真机至少复核：

1. APK 安装、图标 / 应用名、首装启动、权限和系统返回；
2. OIDC 登录取消 / 成功、浏览器回跳、登出、会话恢复、冷启动；
3. default 下五入口、Discover / Forum / Docs / Profile / Commerce / Wallet / Experience / Leaderboard / Browse History 的主链路与来源返回；
4. 发帖、回答、评论 / 回复、轻回应、作者编辑、通知已读、资料编辑和单商品购买等高风险写入各一条；
5. 四主题切换、持久化及 Discover / Forum Detail / Docs Reader / Product Detail / Profile 代表视觉；
6. 长内容、输入法、系统栏、滚动、Back 和 compact 无横向溢出。

P6-C 的人工判断由项目所有者确认；自动化或模拟器不能代替真实设备视觉、浏览器 OIDC、输入法和安装行为。

### 6.3 Go / No-Go

只有 P6-A、P6-B、P6-C 均通过，且没有 `P0 / P1`、认证 / 写入契约错误、不可恢复导航、关键内容不可见、横向溢出、签名 / 安装异常，才能记录“Android 新版 UI 本地 / 内部 RC Go”。

以下仍不随 P6-C 自动成立：

- 正式签名与外部分发；
- AAB、Play Console、商店合规、升级迁移和生产发布；
- iOS 或 desktop 平台完成；
- test tag、GitHub Release、镜像、生产部署。

## 7. 授权与清理边界

### 7.1 当前尚未授权的动作

- P6-A 的 release APK candidate build；
- `./start.sh` 选项 `8` 的三宿主启动；
- ADB server、AVD 创建 / 启动、APK 安装、reverse 和截图；
- 真实设备覆盖安装、清数据或卸载；
- 任何真实 Gateway、登录、写入或购买 Smoke。

不需要授权或安装任何新 package / SDK / image；若实施时发现缺包或需要更新 Flutter、Gradle、AGP、Kotlin、Android SDK 或 Dart 依赖，必须停止并重新说明影响。

### 7.2 清理规则

1. 服务只由 `./start.sh` 当前会话管理，结束时 `Ctrl+C`，复核 `5000 / 5100 / 5200` 不再监听。
2. 每台设备执行 `adb reverse --remove tcp:5000`；关闭 AVD，删除本批临时 medium AVD。
3. 不自动清理真实设备 app / data；卸载或 `pm clear` 属于破坏性动作，必须另行确认。
4. 不直接改删 SQLite；测试写入保留 `P6-RC` 前缀与 ID，后续只经已有正式能力清理或由项目所有者决定保留。
5. 不提交 `key.properties`、keystore、密码、token、支付口令、浏览器会话或原始隐私截图。
6. APK、原始日志和未筛选截图不进入 Git；只提交必要的脱敏证据与文字记录。

## 8. 停止线与下一顺位

- readiness 没有修改 Dart / Kotlin / Gradle、API、数据库、依赖、lockfile、Pen 或平台工程。
- readiness 没有启动 API / Auth / Gateway、AVD 或 Flutter 应用，没有构建 / 安装 APK，也没有执行真实 Gateway / 设备 Smoke。
- P6 中若发现真实产品缺陷，先按根因修复并重跑对应静态门禁；不得用人工备注、重试兜底或跳过断言取得 Go。
- 平台 warning 若升级为构建失败，先形成精确依赖 / 工具链方案并重新确认，不顺手升级整条 Android 构建链。
- 下一顺位固定为 `P6-A Android local RC candidate assembly`，完成后再次确认 P6-B 服务与设备运行授权；不跨批提前宣称 Android RC 完成。
