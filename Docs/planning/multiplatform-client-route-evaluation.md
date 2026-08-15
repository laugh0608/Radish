# 多端客户端路线评估方案

> 状态：历史路线评估已收口；`2026-08-15` 当前裁决为 Web / Flutter Native 两条产品线、Tauri 正式弃用
>
> 最后更新：2026-08-15（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [开发路线图](/development-plan)
> - [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Flutter Native 产品化与 UI 重构](/features/flutter-native-product-ui-design)
> - [Flutter Android MVP RC 验收记录（2026-05-04）](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)

## 1. 当前结论

当前不删除 Flutter 或 Tauri 已有代码。Flutter Native 作为次级原生安装包产品线，长期覆盖 Android / iOS / Windows / macOS / Linux，mobile-first、desktop stage-gated；Tauri 正式弃用，只保留历史验证资产。

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论，说明 Flutter 线已经能稳定交付一条 Android 原生客户端 MVP。但这不自动等于“长期多端都必须继续用 Flutter”。

`2026-08-15` 路线裁决后，多端投入顺位固定为：

1. **Web 正式优先主线**：根路径 `/` 和默认浏览器入口使用正式 Web，适配 PC 与移动浏览器，承接公开阅读、分享、SEO、登录后高价值链路和后续新功能
2. **Flutter Native 次级原生产品线**：保留已验收的 Android MVP，移动优先重建产品级 UI；iOS 与 Windows / macOS / Linux 在共享 UI 和平台门禁成立后分别产品化，不机械追平 Web，不引入 Flutter Web
3. **WebOS 保留入口**：`/desktop` 仅作为历史桌面工作台保留项，不再承接新增功能，既有高价值能力优先迁移到纯 Web
4. **Tauri 正式弃用资产**：保留 Windows / macOS / Linux 壳层验证成果，不进入开发、UI、CI、构建、发布或验收门禁；不设置自动重评节点

Capacitor Android 已确认不进入移动端产品化主线；Tauri 不再被理解为“原生重写”，也不再作为 WebOS 或正式 Web 的桌面分发壳路线。Flutter Native 承接长期桌面安装包目标。

## 2. 已完成前置

Flutter Android MVP RC 已完成：

- RC Gateway：`https://radishx.com`
- APK 类型：release APK
- 设备：小米 15S Pro
- Android 版本：16
- 测试账号：`test`
- 命令级验证：`flutter analyze`、`flutter test`、`smoke_test`、Android JVM 单测、签名检查、release APK 构建与 `git diff --check` 均通过
- 真机复核：登录、退出、会话恢复、`discover / forum / docs / profile`、forum detail、docs 搜索 / 内链、profile 复访、轻回应发布与最小 forum notification 回流均未发现 `P0 / P1`

因此，React spike 的目的不是补救 Flutter 失败，而是判断哪些平台适合复用 React Web 资产。当前结论更新为：产品能力先在 Web 成立；具有明确原生价值时由 Flutter Native 次级产品线承接；移动安装包不回到 WebView 壳路线；Tauri 正式弃用，不继续分发正式 Web 或 WebOS。

## 3. 冻结边界

评估期间固定不做：

1. 不在本轮启动 Flutter iOS 产品化工程，也不启动 Flutter Windows / macOS / Linux 桌面扩平台工程
2. 不继续追加 Flutter 第 `24` 批及以后低增益体验微调
3. 不把 React spike 做成完整产品重写
4. 不改后端 API、认证协议或 Gateway 架构来迁就某个壳层
5. 不删除 WebOS 既有 `/desktop` 保留入口，不推翻纯 Web 公开入口或 Flutter Android MVP 已完成事实

## 4. React 复用路线 spike

### 4.1 目标

用最小样例验证现有 React Web 资产是否能低成本进入移动 App 壳与桌面安装包壳，并据此明确长期分工。

本轮实际评估结果：

- 移动端：`Capacitor` 可承载公开只读页面，但登录 / OIDC / 本机调试复杂度过高，退出移动端主线
- 桌面端：`Tauri` 当时已证明可承载 React 构建产物和桌面系统桥接；该结果保留为历史事实，现行路线不再继续

本轮只做技术风险验证，不做正式产品交付。

### 4.2 验证问题

1. 现有 `React Web` 页面能否以较低改造成本跑在移动 App 壳里
2. `@radish/http`、登录态、Gateway 配置、环境变量访问方式能否复用
3. Android / iOS 的深链、登录回调、文件、分享、通知能力接入成本是否适合 WebView 复用
4. Windows / macOS / Linux 的窗口、托盘、自动更新、文件系统能力是否适合由 Tauri 壳承接
5. 打包体积、启动速度、调试体验与发布链路是否明显优于或劣于 Flutter 路线

### 4.3 最小样例范围

移动 App 壳只验证：

1. 加载一个现有公开内容入口，例如 forum 列表或 docs 列表
2. 复用 `@radish/http` 发起 Gateway 请求
3. 验证登录回调或深链入口的最小闭环
4. 记录 Android 包体、启动时间、调试体验与构建复杂度

桌面安装包壳只验证：

1. 加载一个现有桌面或公开内容入口
2. 验证窗口生命周期、托盘或文件系统能力中至少一项
3. 记录 Windows 构建产物、启动速度、自动更新方案可行性与调试体验

## 5. 评估输出

React spike 完成后已产出：

1. 一份 spike 记录，包含命令、环境、样例路径、结论与关键输出摘要
2. 一张 Flutter 与 React 复用路线对比表
3. 一份当时的路线建议：纯 Web 成为唯一正式投入方向，Flutter 条件维护，WebOS 转保留 / 迁移，Tauri 冻结且不再绑定 WebOS；该建议已由 `2026-08-15` 最新裁决更新为 Web / Flutter Native 两条产品线、Tauri 正式弃用

对比维度固定包括：

| 维度 | Flutter Android MVP | Capacitor 移动壳 | Tauri 桌面壳 |
| --- | --- | --- | --- |
| 现有业务复用 | 已通过原生重写方式验证 | `/docs` 公开只读可复用 | 可复用 `radish.client` 的 Vite `dist`；后续正式评估应改为纯 Web 默认入口，而不是 WebOS / `/desktop` |
| `@radish/http` 复用 | 不能直接复用 TypeScript 客户端 | 公开读取可复用 | 可继续复用现有 TypeScript 客户端与运行时配置 |
| 登录 / 深链 | Android 已跑通 | 调试链路复杂，评估终止 | 命令级桥接与人工验收已完成：系统浏览器登录优先走 `http://127.0.0.1:48801/oidc/callback` loopback 并转 `/oidc/callback`，`radish://` 保留为兼容路径；测试后暂未发现问题 |
| 原生能力 | Android 已跑通最小链路 | 仅验证 WebView 壳层，不进入产品化 | 已接入 focus / resize / close requested 窗口生命周期事件；托盘、文件系统和自动更新待后续评估 |
| 包体 / 启动 | 已有 release APK 可测 | debug APK 可构建 | `cargo build --release` 生成 Windows exe，约 `9.15 MiB`；Web `dist` 约 `2.75 MiB`；启动速度未做 GUI 实测 |
| 调试体验 | 已有 Flutter 工具链 | Gateway / Auth / WebView / 证书 / 端口代理耦合过高 | 前端构建 + Cargo 编译链路清晰；首次依赖拉取需网络，沙盒内 crates.io 证书受限时需提权验证 |
| 长期维护成本 | 需要维护 Dart 原生页面；现行路线通过同一自适应 UI 承接 PC / mobile | 对登录态移动端不合适 | 当时可集中承接窗口、loopback 登录和分发能力；现行路线已正式弃用，不再进入采用评估 |

## 6. 决策门槛

最终按以下规则判断：

1. **移动安装包**
   - Android 继续复用 Flutter MVP 成果，先完成产品级 UI 重构
   - iOS 在共享 UI 稳定后单独完成平台工程与分发门禁
   - Capacitor 不进入移动端产品化主线
2. **桌面安装包**
   - Windows / macOS / Linux 由 Flutter Native 承接，必须先通过 expanded 布局、键鼠、焦点、滚动和窗口门禁
   - 各桌面平台的工程、签名、更新和分发分别立项，不因 Dart UI 可运行自动完成
   - Tauri 正式弃用；不把 WebOS 作为 Flutter desktop 默认体验
3. **Web 浏览器**
   - 纯 Web 壳层继续面向 PC / 移动浏览器做响应式阅读和轻交互体验
   - 根路径 `/` 和默认浏览器入口转向纯 Web
   - 不把公开内容和新增功能塞回窗口系统

当前实际状态为 Web 优先、Flutter Native 次级：WebOS 只保留 `/desktop`，Tauri 正式弃用，Flutter Web 不进入路线。

截至 2026-05-04，Capacitor Android 的阶段性结论是：公开只读页面复用成立，但一进入登录态、OIDC 回调、本机 Gateway/Auth 调试和 Android WebView 证书 / 端口代理，复杂度明显高于预期，不符合 Radish 当前“低成本 React 复用”的路线目标。Capacitor 因此不进入移动端产品化主线；截至 2026-05-16，`Frontend/radish.client/android`、`capacitor.config.ts`、`cap:*` scripts 与 Capacitor 依赖已从当前代码中清理，历史结论保留在 spike 记录中。

截至 2026-05-05，Tauri 桌面壳首轮命令级 spike、第二轮人工验收与 Windows NSIS installer 首轮验证已成立：`Clients/radish-tauri` 可复用既有 React 构建产物，`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run build --workspace=radish.client`、`cargo build`、`cargo build --release` 与 `cargo tauri build` 均已通过；Windows release exe 与 NSIS installer 可生成。Tauri 默认入口曾从 `/docs` 切到 `/desktop`，桌面登录 / 登出优先改为系统浏览器 + `127.0.0.1:48801` loopback 回跳以复用浏览器登录态并避免依赖 Windows 注册表；GUI 启动、WebOS 桌面布局、窗口生命周期观察、真实登录 / 登出回跳、installer 安装、启动、普通用户卸载与同身份覆盖安装测试后暂未发现问题；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复；当时本机普通用户安装未出现“未知发布者 / SmartScreen”提示，公开分发仍需按下载来源、签名、信誉与系统策略复核。管理员安装后用普通权限卸载可能残留安装文件，归类为权限上下文不一致风险。该阶段曾进入 `Radish` / `com.radish.desktop` 正式桌面包候选身份补验，并生成 `Radish_0.1.0_x64-setup.exe`；生产候选构建默认指向 `https://radishx.com`，另有 `build:tauri-local` 本地 Auth 验收构建模式。以上只保留历史 spike 事实；`2026-08-15` 起 Tauri 正式弃用，不构成重启条件，桌面原生安装包由 Flutter Native 在阶段门禁后承接。

## 7. 最终路线建议

### 7.1 Web 浏览器：正式优先主线

职责：

- 面向匿名访问、外链分享、搜索流量、移动浏览器与 PC 浏览器
- 重点承载 forum / docs / discover / leaderboard / shop / `u/:id` 的公开阅读
- 根路径 `/` 和默认浏览器入口转向纯 Web
- 继续做响应式适配、轻交互和登录后高价值链路迁移

不承担：

- WebOS 多窗口、Dock 或桌面级应用组织

### 7.2 Android / iOS / Windows / macOS / Linux：Flutter Native 原生安装包

职责：

- Android MVP 作为已完成业务基线继续复用，先完成 mobile 产品级 UI 重构
- iOS 在共享 UI 稳定后单独完成平台工程、设备验收与分发门禁，不回到 Capacitor WebView 路线
- Windows / macOS / Linux 在 expanded UI、键鼠、焦点、滚动和窗口门禁通过后分平台产品化
- 系统推送、后台任务、签名、自动更新、商店分发或原生生命周期能力按各自价值单独定义批次

不承担：

- 复刻 WebOS 桌面工作台
- 复用 React 页面作为移动端主体
- 在 Android MVP 完成后继续默认追加低增益微体验批次
- 提供 Flutter Web、独立 Native Console、SEO 或完整 Author 副本

### 7.3 WebOS：保留入口与迁移来源

职责：

- `/desktop` 继续保留，避免一次性删除既有桌面工作台能力
- 聊天、通知、个人中心、创作、继续使用等既有能力按价值优先迁移到纯 Web
- 迁移期间只做必要维护和高信号问题修复

不承担：

- 新功能默认承载
- 移动端体验
- PC 客户端默认 UI

### 7.4 Tauri：正式弃用的历史资产

职责：

- 保留历史验证资产，不进入开发、UI、CI、构建、签名、分发或验收流程
- 不设置自动恢复或重新评估条件
- 历史验证结论只用于追溯，不构成当前架构输入

不承担：

- 重新用 Rust 写原生 UI
- 把 WebOS 作为正式桌面 App 默认体验
- 与 Flutter desktop 产品线并行存在

## 8. 建议执行顺序

1. Web 继续承接根路径 `/`、默认浏览器入口、新功能边界和 PC / mobile 验收
2. Flutter 复用 Android MVP 业务基线，先完成全页面审计、技术基座、代表设计和 mobile 页面族重构
3. iOS 与 Windows / macOS / Linux 在共享 UI 阶段门禁后分别完成平台产品化；系统能力和分发能力分别做价值判断
4. WebOS 只保留 `/desktop`，后续新功能默认不进入 WebOS，既有高价值能力逐步迁移
5. Tauri 保持正式弃用，只保留历史资产
6. 不再推进 Capacitor Android 登录态能力

## 9. 当前不做

- 不把 Flutter Android MVP 回滚或废弃
- 不让 Flutter 机械追平 Web 或脱离原生价值扩张
- 不在共享 UI 与平台门禁完成前生成 iOS / Windows / macOS / Linux 平台工程
- 不把 React spike 扩成完整移动端重构
- 不恢复 Tauri 或把它纳入候选构建、签名、分发门禁
- 不把 WebOS 作为 Flutter desktop 正式体验
- 不引入 Flutter Web 或独立 Native Console
- 不把新功能默认加入 WebOS
- 不在路线评估期间扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理
