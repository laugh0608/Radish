# 多端客户端路线评估方案

> 状态：路线评估收口，主线已收敛为纯 Web + Flutter
>
> 最后更新：2026-05-25（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [开发路线图](/development-plan)
> - [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Flutter Android MVP RC 验收记录（2026-05-04）](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)

## 1. 当前结论

当前不推翻 Flutter，也不把 React WebView 路线扩大到移动端。

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论，说明 Flutter 线已经能稳定交付一条 Android 原生客户端 MVP。但这不自动等于“长期多端都必须继续用 Flutter”。

本轮路线复盘后，多端路线从“三端并行分工”进一步收敛为两条主线和两个保留 / 后置方向：

1. **Web 浏览器主线**：根路径 `/` 和默认浏览器入口转向纯 Web，适配 PC 浏览器与移动浏览器，定位为公开阅读、分享、SEO、轻交互和逐步承接登录后高价值链路
2. **Android / iOS 安装包主线**：继续以 Flutter 作为移动原生客户端路线；Android MVP 已完成第一轮，iOS 后续单独评估
3. **WebOS 保留入口**：`/desktop` 仅作为历史桌面工作台保留项，不再承接新增功能，既有高价值能力后续逐步迁移到纯 Web 或 Flutter
4. **Windows / macOS / Linux 安装包后置方向**：PC 客户端放到更后阶段评估；若重启，则使用 Tauri 增强纯 Web 体验，不再默认绑定 WebOS

Capacitor Android 已确认不进入移动端产品化主线；Tauri 不再被理解为“原生重写”，也不再作为 WebOS 桌面分发壳的默认路线。

## 2. 已完成前置

Flutter Android MVP RC 已完成：

- RC Gateway：`https://radishx.com`
- APK 类型：release APK
- 设备：小米 15S Pro
- Android 版本：16
- 测试账号：`test`
- 命令级验证：`flutter analyze`、`flutter test`、`smoke_test`、Android JVM 单测、签名检查、release APK 构建与 `git diff --check` 均通过
- 真机复核：登录、退出、会话恢复、`discover / forum / docs / profile`、forum detail、docs 搜索 / 内链、profile 复访、轻回应发布与最小 forum notification 回流均未发现 `P0 / P1`

因此，React spike 的目的不是补救 Flutter 失败，而是判断哪些平台适合复用 React Web 资产。当前结论更新为：Web 浏览器主线应强化纯 Web；移动安装包不适合回到 WebView 壳路线；PC 安装包不作为近期主线，未来若重启应让 Tauri 增强纯 Web，而不是继续分发 WebOS。

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
- 桌面端：`Tauri` 可承载 React 构建产物和桌面系统桥接；路线复盘后，后续若继续应增强纯 Web，而不是绑定 WebOS 工作台

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
3. 一份最终路线建议：Web 浏览器与 Flutter 成为主要投入方向，WebOS 转保留 / 迁移，桌面安装包后置且不再绑定 WebOS

对比维度固定包括：

| 维度 | Flutter Android MVP | Capacitor 移动壳 | Tauri 桌面壳 |
| --- | --- | --- | --- |
| 现有业务复用 | 已通过原生重写方式验证 | `/docs` 公开只读可复用 | 可复用 `radish.client` 的 Vite `dist`；后续正式评估应改为纯 Web 默认入口，而不是 WebOS / `/desktop` |
| `@radish/http` 复用 | 不能直接复用 TypeScript 客户端 | 公开读取可复用 | 可继续复用现有 TypeScript 客户端与运行时配置 |
| 登录 / 深链 | Android 已跑通 | 调试链路复杂，评估终止 | 命令级桥接与人工验收已完成：系统浏览器登录优先走 `http://127.0.0.1:48801/oidc/callback` loopback 并转 `/oidc/callback`，`radish://` 保留为兼容路径；测试后暂未发现问题 |
| 原生能力 | Android 已跑通最小链路 | 仅验证 WebView 壳层，不进入产品化 | 已接入 focus / resize / close requested 窗口生命周期事件；托盘、文件系统和自动更新待后续评估 |
| 包体 / 启动 | 已有 release APK 可测 | debug APK 可构建 | `cargo build --release` 生成 Windows exe，约 `9.15 MiB`；Web `dist` 约 `2.75 MiB`；启动速度未做 GUI 实测 |
| 调试体验 | 已有 Flutter 工具链 | Gateway / Auth / WebView / 证书 / 端口代理耦合过高 | 前端构建 + Cargo 编译链路清晰；首次依赖拉取需网络，沙盒内 crates.io 证书受限时需提权验证 |
| 长期维护成本 | 需要维护 Dart 原生移动页面 | 对登录态移动端不合适 | 桌面壳层可集中承接原生窗口、loopback 登录回跳、deep link 兼容与分发能力；但 PC 客户端已后置，后续不再默认分发 WebOS，正式采用前仍需重新评估纯 Web 承载、签名、自动更新、deep link 协议注册清理与分发验收 |

## 6. 决策门槛

最终按以下规则判断：

1. **移动安装包**
   - Android 继续保留 Flutter MVP 成果
   - iOS 后续按 Flutter 单独评估，不因 Tauri spike 改变移动端主线
   - Capacitor 不进入移动端产品化主线
2. **桌面安装包**
   - PC 客户端后置到纯 Web 与 Flutter 主线之后
   - 若重启，Tauri 负责系统壳增强，UI 默认承载纯 Web
   - 不把 WebOS 作为正式桌面客户端默认体验
3. **Web 浏览器**
   - 纯 Web 壳层继续面向 PC / 移动浏览器做响应式阅读和轻交互体验
   - 根路径 `/` 和默认浏览器入口转向纯 Web
   - 不把公开内容和新增功能塞回窗口系统

当前实际状态已从“三端分工”进一步收敛为“纯 Web + Flutter 主线”：WebOS 只保留 `/desktop`，PC/Tauri 延后且不再默认绑定 WebOS。

截至 2026-05-04，Capacitor Android 的阶段性结论是：公开只读页面复用成立，但一进入登录态、OIDC 回调、本机 Gateway/Auth 调试和 Android WebView 证书 / 端口代理，复杂度明显高于预期，不符合 Radish 当前“低成本 React 复用”的路线目标。Capacitor 因此不进入移动端产品化主线；截至 2026-05-16，`Frontend/radish.client/android`、`capacitor.config.ts`、`cap:*` scripts 与 Capacitor 依赖已从当前代码中清理，历史结论保留在 spike 记录中。

截至 2026-05-05，Tauri 桌面壳首轮命令级 spike、第二轮人工验收与 Windows NSIS installer 首轮验证已成立：`Clients/radish-tauri` 可复用既有 React 构建产物，`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run build --workspace=radish.client`、`cargo build`、`cargo build --release` 与 `cargo tauri build` 均已通过；Windows release exe 与 NSIS installer 可生成。Tauri 默认入口曾从 `/docs` 切到 `/desktop`，桌面登录 / 登出优先改为系统浏览器 + `127.0.0.1:48801` loopback 回跳以复用浏览器登录态并避免依赖 Windows 注册表；GUI 启动、WebOS 桌面布局、窗口生命周期观察、真实登录 / 登出回跳、installer 安装、启动、普通用户卸载与同身份覆盖安装测试后暂未发现问题；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复；当前本机普通用户安装未出现“未知发布者 / SmartScreen”提示，公开分发后仍需按下载来源、签名、信誉与系统策略复核。管理员安装后用普通权限卸载可能残留安装文件，当前归类为权限上下文不一致风险。当前已进入 `Radish` / `com.radish.desktop` 正式桌面包候选身份补验，并已生成 `Radish_0.1.0_x64-setup.exe`；生产候选构建默认指向 `https://radishx.com`，同时新增 `build:tauri-local` 本地 Auth 验收构建模式指向 `https://localhost:5000`，用于生产 Auth 客户端注册暂未更新时继续验证 loopback 登录。路线复盘后，PC/Tauri 不再作为近期主线，后续若重启应改为增强纯 Web，而不是继续以 WebOS 为默认体验。

## 7. 最终路线建议

### 7.1 Web 浏览器：纯 Web 主线

职责：

- 面向匿名访问、外链分享、搜索流量、移动浏览器与 PC 浏览器
- 重点承载 forum / docs / discover / leaderboard / shop / `u/:id` 的公开阅读
- 根路径 `/` 和默认浏览器入口转向纯 Web
- 继续做响应式适配、轻交互和登录后高价值链路迁移

不承担：

- WebOS 多窗口、Dock 或桌面级应用组织

### 7.2 Android / iOS：Flutter 原生安装包

职责：

- Android MVP 继续作为已完成移动主线保留
- iOS 后续若进入，应基于 Flutter 单独评估，而不是回到 Capacitor WebView 路线
- 移动端重点承载原生导航、原生生命周期、登录回跳、复访、轻互动和通知回流

不承担：

- 复刻 WebOS 桌面工作台
- 复用 React 页面作为移动端主体
- 在 Android MVP 完成后继续默认追加低增益微体验批次

### 7.3 WebOS：保留入口与迁移来源

职责：

- `/desktop` 继续保留，避免一次性删除既有桌面工作台能力
- 聊天、通知、个人中心、创作、继续使用等既有能力按价值逐步迁移到纯 Web 或 Flutter
- 迁移期间只做必要维护和高信号问题修复

不承担：

- 新功能默认承载
- 移动端体验
- PC 客户端默认 UI

### 7.4 Windows / macOS / Linux：Tauri 后置增强壳

职责：

- 放在纯 Web 与 Flutter 主线之后再评估
- 若重启，Tauri 负责系统窗口、系统浏览器 loopback 登录回跳、deep link 兼容、系统通知、托盘、文件选择、本地缓存、外部链接等原生桥能力
- UI 默认承载纯 Web，不再绑定 WebOS

不承担：

- 重新用 Rust 写原生 UI
- 把 WebOS 作为正式桌面 App 默认体验
- 与 Flutter Windows / Linux 扩平台混成同一条路线

## 8. 建议执行顺序

1. 把根路径 `/` 和默认浏览器入口切向纯 Web，先补齐移动 Web 公开视图验收矩阵暴露的主路径缺口
2. 保持 Android MVP 第一轮完成状态，不默认开启 Flutter 第 `24` 批低增益微调
3. 若继续移动端，优先做 Android 内测产品化深化、反馈闭环和 release 前验收材料；iOS 后续单独评估
4. WebOS 只保留 `/desktop`，后续新功能默认不进入 WebOS，既有高价值能力逐步迁移
5. PC/Tauri 放到最后再评估；若重启，基于纯 Web 做增强体验，不再以 WebOS 为默认体验
6. 不再推进 Capacitor Android 登录态能力

## 9. 当前不做

- 不把 Flutter Android MVP 回滚或废弃
- 不立即生成 iOS Flutter 平台工程
- 不把 Windows / macOS / Linux 作为 Flutter 默认扩平台目标
- 不把 React spike 扩成完整移动端重构
- 不把 Tauri 误判为原生 UI 重写路线
- 不把 WebOS 作为 Tauri 正式桌面体验
- 不把新功能默认加入 WebOS
- 不在路线评估期间扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理
