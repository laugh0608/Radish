# 多端客户端路线评估方案

> 状态：路线评估收口，三端分工方案已确认
>
> 最后更新：2026-05-04（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [开发路线图](/development-plan)
> - [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)

## 1. 当前结论

当前不推翻 Flutter，也不把 React WebView 路线扩大到移动端。

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论，说明 Flutter 线已经能稳定交付一条 Android 原生客户端 MVP。但这不自动等于“长期多端都必须继续用 Flutter”。

本轮 React 复用 spike 后，多端路线收束为三条产品线：

1. **Web 浏览器**：继续使用公开内容壳层，适配 PC 浏览器与移动浏览器，定位为公开阅读、分享、SEO 与轻交互
2. **Android / iOS 安装包**：继续以 Flutter 作为移动原生客户端路线；Android MVP 已完成第一轮，iOS 后续单独评估
3. **Windows / macOS / Linux 安装包**：优先评估 `Tauri 壳 + WebOS 桌面工作台`，Tauri 负责桌面系统能力与分发，WebOS 负责 Dock、窗口系统与工作台应用体验

Capacitor Android 已确认不进入移动端产品化主线；Tauri 不再被理解为“原生重写”，而是 React/WebOS 桌面分发壳。

## 2. 已完成前置

Flutter Android MVP RC 已完成：

- RC Gateway：`https://radishx.com`
- APK 类型：release APK
- 设备：小米 15S Pro
- Android 版本：16
- 测试账号：`test`
- 命令级验证：`flutter analyze`、`flutter test`、`smoke_test`、Android JVM 单测、签名检查、release APK 构建与 `git diff --check` 均通过
- 真机复核：登录、退出、会话恢复、`discover / forum / docs / profile`、forum detail、docs 搜索 / 内链、profile 复访、轻回应发布与最小 forum notification 回流均未发现 `P0 / P1`

因此，React spike 的目的不是补救 Flutter 失败，而是判断哪些平台适合复用 React Web 资产。当前结论是：桌面安装包适合继续评估 React + Tauri，移动安装包不适合回到 WebView 壳路线。

## 3. 冻结边界

评估期间固定不做：

1. 不在本轮启动 Flutter iOS 产品化工程，也不启动 Flutter Windows / macOS / Linux 桌面扩平台工程
2. 不继续追加 Flutter 第 `24` 批及以后低增益体验微调
3. 不把 React spike 做成完整产品重写
4. 不改后端 API、认证协议或 Gateway 架构来迁就某个壳层
5. 不推翻 WebOS 桌面工作台、公开内容壳层或 Flutter Android MVP 已完成事实

## 4. React 复用路线 spike

### 4.1 目标

用最小样例验证现有 React Web 资产是否能低成本进入移动 App 壳与桌面安装包壳，并据此明确长期分工。

本轮实际评估结果：

- 移动端：`Capacitor` 可承载公开只读页面，但登录 / OIDC / 本机调试复杂度过高，退出移动端主线
- 桌面端：`Tauri` 可承载 React 构建产物和桌面系统桥接，后续若继续应绑定 WebOS 工作台，而不是公开阅读页

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
3. 一份最终路线建议：移动端继续 Flutter，桌面安装包优先 `Tauri + WebOS`，Web 浏览继续公开内容壳层

对比维度固定包括：

| 维度 | Flutter Android MVP | Capacitor 移动壳 | Tauri 桌面壳 |
| --- | --- | --- | --- |
| 现有业务复用 | 已通过原生重写方式验证 | `/docs` 公开只读可复用 | 可复用 `radish.client` 的 Vite `dist`；后续正式评估应改为 WebOS / `/desktop`，而不是 `/docs` |
| `@radish/http` 复用 | 不能直接复用 TypeScript 客户端 | 公开读取可复用 | 可继续复用现有 TypeScript 客户端与运行时配置 |
| 登录 / 深链 | Android 已跑通 | 调试链路复杂，评估终止 | 命令级桥接与人工验收已完成：系统浏览器登录优先走 `http://127.0.0.1:48801/oidc/callback` loopback 并转 `/oidc/callback`，`radish://` 保留为兼容路径；测试后暂未发现问题 |
| 原生能力 | Android 已跑通最小链路 | 仅验证 WebView 壳层，不进入产品化 | 已接入 focus / resize / close requested 窗口生命周期事件；托盘、文件系统和自动更新待后续评估 |
| 包体 / 启动 | 已有 release APK 可测 | debug APK 可构建 | `cargo build --release` 生成 Windows exe，约 `9.15 MiB`；Web `dist` 约 `2.75 MiB`；启动速度未做 GUI 实测 |
| 调试体验 | 已有 Flutter 工具链 | Gateway / Auth / WebView / 证书 / 端口代理耦合过高 | 前端构建 + Cargo 编译链路清晰；首次依赖拉取需网络，沙盒内 crates.io 证书受限时需提权验证 |
| 长期维护成本 | 需要维护 Dart 原生移动页面 | 对登录态移动端不合适 | 桌面壳层可集中承接原生窗口、loopback 登录回跳、deep link 兼容与分发能力，WebOS 继续承接桌面 UI；Windows NSIS installer 安装 / 启动 / 普通用户卸载 / 同身份覆盖安装首轮已通过，本机 SmartScreen 未复现拦截；正式采用前仍需补签名、自动更新、deep link 协议注册与分发验收 |

## 6. 决策门槛

最终按以下规则判断：

1. **移动安装包**
   - Android 继续保留 Flutter MVP 成果
   - iOS 后续按 Flutter 单独评估，不因 Tauri spike 改变移动端主线
   - Capacitor 不进入移动端产品化主线
2. **桌面安装包**
   - 优先评估 `Tauri + WebOS`
   - Tauri 负责系统壳能力，WebOS 负责桌面工作台体验
   - 不把 `/docs` 公开阅读页作为正式桌面客户端默认体验
3. **Web 浏览器**
   - 公开内容壳层继续面向 PC / 移动浏览器做响应式阅读体验
   - 不把 WebOS 压缩成移动 Web，也不把公开内容塞回窗口系统

当前实际状态已落在“三端分工”方案：公开 Web、Flutter 移动端、Tauri + WebOS 桌面端各自承担不同产品形态。

截至 2026-05-04，Capacitor Android 的阶段性结论是：公开只读页面复用成立，但一进入登录态、OIDC 回调、本机 Gateway/Auth 调试和 Android WebView 证书 / 端口代理，复杂度明显高于预期，不符合 Radish 当前“低成本 React 复用”的路线目标。Capacitor 因此不进入移动端产品化主线。

截至 2026-05-05，Tauri 桌面壳首轮命令级 spike、第二轮人工验收与 Windows NSIS installer 首轮验证已成立：`Clients/radish-tauri` 可复用既有 React 构建产物，`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run build --workspace=radish.client`、`cargo build`、`cargo build --release` 与 `cargo tauri build` 均已通过；Windows release exe 与 NSIS installer 可生成。Tauri 默认入口已从 `/docs` 切到 `/desktop`，桌面登录 / 登出优先改为系统浏览器 + `127.0.0.1:48801` loopback 回跳以复用浏览器登录态并避免依赖 Windows 注册表；GUI 启动、WebOS 桌面布局、窗口生命周期观察、真实登录 / 登出回跳、installer 安装、启动、普通用户卸载与同身份覆盖安装测试后暂未发现问题；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复；当前本机普通用户安装未出现“未知发布者 / SmartScreen”提示，公开分发后仍需按下载来源、签名、信誉与系统策略复核。管理员安装后用普通权限卸载可能残留安装文件，当前归类为权限上下文不一致风险。但暂不直接切为桌面主线，仍需补正式产品身份、代码签名、自动更新、deep link 协议注册和分发链路验证。

## 7. 最终路线建议

### 7.1 Web 浏览器：公开内容壳层

职责：

- 面向匿名访问、外链分享、搜索流量、移动浏览器与 PC 浏览器
- 重点承载 forum / docs / discover / leaderboard / shop / `u/:id` 的公开阅读
- 继续做响应式适配，但保持轻交互、低治理、弱工作台语义

不承担：

- 完整通知中心、聊天、创作器、订单 / 背包、编辑 / 发布 / 版本治理
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

### 7.3 Windows / macOS / Linux：Tauri 壳 + WebOS 桌面工作台

职责：

- Tauri 负责桌面安装包、系统窗口、系统浏览器 loopback 登录回跳、deep link 兼容、菜单、托盘、自动更新、文件系统、外部链接等原生桥能力
- WebOS 负责 Dock、桌面图标、窗口系统、多应用容器、通知、商城、论坛、个人中心等桌面工作台体验
- 默认入口当前已切到 `/desktop`，而不是 `/docs` 公开阅读页

不承担：

- 重新用 Rust 写原生 UI
- 把公开内容壳层原样作为桌面 App 主页
- 与 Flutter Windows / Linux 扩平台混成同一条路线

## 8. 建议执行顺序

1. 保持 Android MVP 第一轮完成状态，不默认开启 Flutter 第 `24` 批低增益微调
2. 若继续移动端，优先做 Android 内测产品化深化、反馈闭环和 release 前验收材料；iOS 后续单独评估
3. 若继续桌面端，基于已通过的 `Tauri + WebOS` GUI / 登录回跳人工验收、Windows NSIS installer 首轮验证与本机 SmartScreen 观察，优先评估正式产品身份、签名、自动更新、deep link 协议注册与分发链路
4. 公开内容壳层保持稳定维护，只做必要响应式问题修复和公开阅读质量补洞
5. 不再推进 Capacitor Android 登录态能力

## 9. 当前不做

- 不把 Flutter Android MVP 回滚或废弃
- 不立即生成 iOS Flutter 平台工程
- 不把 Windows / macOS / Linux 作为 Flutter 默认扩平台目标
- 不把 React spike 扩成完整移动端重构
- 不把 Tauri 误判为原生 UI 重写路线
- 不把 `/docs` 公开阅读页作为 Tauri 正式桌面体验
- 不在路线评估期间扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理
