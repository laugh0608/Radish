# Radish Tauri

`radish-tauri` 是 Radish 的 Tauri 桌面安装包壳层，当前用于评估 `Tauri 壳 + WebOS 桌面工作台` 路线。

## 当前定位

- 复用 `Frontend/radish.client` 的 React / Vite 构建产物
- 承接 Windows / macOS / Linux 的系统窗口、系统浏览器登录回跳、deep link 兼容、外部浏览器打开与后续分发能力
- Tauri 环境下根路径 `/` 会进入 `/desktop`，由 WebOS 工作台承载默认 UI
- 不作为移动端路线，不替代 Flutter Android / iOS 移动安装包
- 不作为原生 UI 重写工程，UI 仍优先复用 WebOS 工作台

## 构建关系

当前工程使用 Tauri v2 的平铺结构，`Cargo.toml`、`tauri.conf.json`、`src/`、`capabilities/` 均位于 `Clients/radish-tauri/` 下，不使用 `src-tauri/` 子目录。

Tauri 配置中的 `frontendDist` 指向：

```text
../../Frontend/radish.client/dist
```

因此执行 Tauri 构建前，应先在仓库根目录构建 WebOS 前端：

```powershell
npm run build --workspace=radish.client
```

若要生成指向本机 Gateway / Auth 的桌面安装包，用于绕开生产 Auth 客户端注册未更新的问题，应改用本地验收构建模式：

```powershell
npm run build:tauri-local --workspace=radish.client
```

该模式使用 `Frontend/radish.client/.env.tauri-local`，默认 Gateway / Auth / SignalR 基址均为 `https://localhost:5000`。它只用于本机联调和安装包验收，不作为公开分发配置；生产候选构建仍使用 `.env.production` 的 `https://radishx.com`。

再进入本目录执行 Rust / Tauri 侧验证：

```powershell
cd Clients/radish-tauri
cargo build
cargo build --release
```

`cargo build --release` 只生成 release exe，用于确认 Rust / Tauri 壳可编译；它不能替代正式 installer 验证。生成 Windows installer bundle 需要安装 Tauri CLI 后执行：

```powershell
cd Clients/radish-tauri
cargo tauri build
```

当前本机已安装 `cargo-tauri` CLI，并已通过 `cargo tauri build` 生成 NSIS installer。若其他环境执行 `cargo tauri --version` 返回 `no such command: tauri`，说明只能验证到 `cargo build` / `cargo build --release`，不能给出 installer、卸载、升级、签名或正式分发体积结论。

## 产品身份边界

当前 `tauri.conf.json` 已进入正式桌面包候选身份：

```text
productName = Radish
identifier = com.radish.desktop
```

该身份切换只用于验证安装目录、卸载项、同身份覆盖安装与 `radish://` 协议注册清理，不等同于公开发布版。代码签名、自动更新、公钥、发布源、托盘、菜单和后台驻留仍保持后置。

## 后续评估重点

- WebOS Dock、桌面图标、窗口系统在 Tauri 固定窗口中的布局与滚动行为人工验收已通过，测试后暂未发现问题
- 桌面登录 / 登出回跳人工验收已通过：当前优先使用 `http://127.0.0.1:48801/oidc/callback` loopback，不依赖 Windows URL Protocol 注册；`radish://` 仅保留为 deep link 兼容路径
- 桌面候选包生产构建默认指向 `https://radishx.com`，避免 installer 继续使用 `https://your-domain.com` 占位地址
- 本地 Auth 验收包可通过 `npm run build:tauri-local --workspace=radish.client` 指向 `https://localhost:5000`，用于配合本机 Gateway / Auth 验证 loopback 登录；该包不得作为公开分发候选
- 浏览器登录页被关闭后，同一路径的后续登录点击会复用等待中的 loopback listener，并重新打开系统浏览器；listener 成功回跳或超时后释放
- Windows NSIS installer 已完成首轮本机安装与启动验证；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复
- 正式身份候选包仍需补普通用户安装 / 启动 / 卸载、同身份覆盖安装、`radish://` 协议注册与卸载清理验证
- 签名、Windows SmartScreen、自动更新、托盘与系统菜单

分发评估清单见：[Tauri + WebOS 桌面安装包第二轮分发评估清单（2026-05-05）](../../Docs/guide/tauri-webos-desktop-distribution-evaluation-2026-05-05.md)。
