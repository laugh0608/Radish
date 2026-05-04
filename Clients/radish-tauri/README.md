# Radish Tauri

`radish-tauri` 是 Radish 的 Tauri 桌面安装包壳层，当前用于评估 `Tauri 壳 + WebOS 桌面工作台` 路线。

## 当前定位

- 复用 `Frontend/radish.client` 的 React / Vite 构建产物
- 承接 Windows / macOS / Linux 的系统窗口、系统浏览器登录回跳、deep link 兼容、外部浏览器打开与后续分发能力
- Tauri 环境下根路径 `/` 会进入 `/desktop`，由 WebOS 工作台承载默认 UI
- 不作为移动端路线，不替代 Flutter Android / iOS 移动安装包
- 不作为原生 UI 重写工程，UI 仍优先复用 WebOS 工作台

## 构建关系

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

## 后续评估重点

- WebOS Dock、桌面图标、窗口系统在 Tauri 固定窗口中的布局与滚动行为人工验收
- 桌面登录 / 登出回跳人工验收：当前优先使用 `http://127.0.0.1:48801/oidc/callback` loopback，不依赖 Windows URL Protocol 注册；`radish://` 仅保留为 deep link 兼容路径
- installer、签名、自动更新、托盘与系统菜单
