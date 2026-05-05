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

当前个人开发阶段不默认安装 `cargo-tauri`。如果本机执行 `cargo tauri --version` 返回 `no such command: tauri`，说明只能验证到 `cargo build` / `cargo build --release`，不能给出 installer、卸载、升级、签名或正式分发体积结论。

## 产品身份边界

当前 `tauri.conf.json` 仍保留 spike 身份：

```text
productName = Radish Tauri Spike
identifier = com.radish.tauri.spike
```

在 installer、卸载、升级、签名与 deep link 注册路径完成验证前，不建议提前改为正式桌面包身份，避免污染系统安装记录、协议注册和后续升级判断。

## 后续评估重点

- WebOS Dock、桌面图标、窗口系统在 Tauri 固定窗口中的布局与滚动行为人工验收已通过，测试后暂未发现问题
- 桌面登录 / 登出回跳人工验收已通过：当前优先使用 `http://127.0.0.1:48801/oidc/callback` loopback，不依赖 Windows URL Protocol 注册；`radish://` 仅保留为 deep link 兼容路径
- installer、签名、Windows SmartScreen、自动更新、托盘与系统菜单

分发评估清单见：[Tauri + WebOS 桌面安装包第二轮分发评估清单（2026-05-05）](../../Docs/guide/tauri-webos-desktop-distribution-evaluation-2026-05-05.md)。
