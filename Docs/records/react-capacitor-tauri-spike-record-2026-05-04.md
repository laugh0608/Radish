# React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）

- 记录日期：2026-05-04
- 记录人：laugh0608
- 范围：多端客户端路线评估第一轮 spike
- 当前阶段：Capacitor 移动壳评估收口，Tauri 桌面壳完成首轮命令级 spike

## 目标

### 第一轮 `/docs`

本轮只验证 React 复用路线能否以较低成本承载移动 App 壳里的公开只读内容入口。

首个验证入口固定为 `/docs`：

1. 复用 `radish.client` 的 Vite 构建链路
2. 复用现有 `PublicEntry` 与 `PublicDocsApp`
3. 复用 `@radish/http`、`@radish/ui`、环境配置与公开 docs API
4. 在 Capacitor Android WebView 中优先打开 `/docs`

### 第二轮登录 / OIDC 回调评估

在 `/docs` 与本机 Gateway 调试链路收口后，曾继续评估 Capacitor Android 是否适合承载与 Flutter Android MVP 等价的最小登录链路：

1. 使用系统浏览器打开 `/connect/authorize`
2. 使用 `radish://oidc/callback` deep link 回到 Android 原生壳
3. 复用 `@radish/http` 的授权码换 Token 能力
4. 复用 `radish.client` 现有 `tokenService` 保存 access token / refresh token 并触发刷新逻辑
5. 登录后读取 `/api/v1/User/GetUserByHttpContext`，并进入当前用户公开 `profile`
6. 退出时走 `/connect/endsession`，通过 `radish://oidc/logout-complete` 回到匿名态

评估过程中发现该链路需要同时处理 Android WebView HTTPS 证书、本机 Gateway HTTP 辅助端口、Auth antiforgery / cookie secure 策略、`adb reverse`、runtime config 覆盖、deep link 原生桥与系统浏览器往返。该复杂度已超出 Radish 当前多端路线评估对“低成本 React 复用”的预期，因此本轮登录 / OIDC 回调 spike 终止，相关临时代码与 Auth 开发态配置不保留。

### 第三轮 Tauri 桌面壳

Capacitor 终止后，本轮转向桌面壳评估，重点验证：

1. 复用 `radish.client` 的 React / Vite / `dist` 产物
2. 首轮先复用公开内容入口验证 Tauri 壳可行性，随后将根路径默认入口切换到 `/desktop`
3. 验证窗口生命周期事件接入，包括 focus、resize 与 close requested
4. 验证桌面系统浏览器 OIDC 登录 / 登出回跳的最小桥接
5. 记录 Windows 构建产物、包体和调试复杂度

## 本轮不做

- 不把 Tauri 桌面壳扩成正式产品线
- 不做完整移动端产品
- 不做登录链路产品化、通知、写入或系统分享
- 不改后端 API、Auth、Gateway 或数据库
- 不改变 WebOS 桌面入口
- 不引入完整移动端账号中心
- 首轮不执行 Tauri GUI 启动或人工登录验收，只做命令级构建与桥接逻辑验证；第二轮已补充 GUI 启动、WebOS 布局、窗口生命周期和系统浏览器登录 / 登出回跳人工验收

## 当前实现

- 依赖：`@capacitor/core`、`@capacitor/android`、`@capacitor/cli`
- 配置：`Frontend/radish.client/capacitor.config.ts`
- `webDir`：`dist`
- `appId`：`com.radish.client.spike`
- `appName`：`Radish React Spike`
- 原生壳启动路径：Capacitor 原生环境下，若当前路径为 `/`，前端入口会将路径收口到 `/docs`
- 登录 / OIDC 回调相关原生桥、deep link manifest、前端登录条与 Auth HTTP cookie 临时配置均未保留，避免影响既有 Web / Auth / Gateway 调试口径

### Tauri 桌面壳

- 工程：`Clients/radish-tauri`
- `identifier`：`com.radish.tauri.spike`
- `productName`：`Radish Tauri Spike`
- `frontendDist`：`../../Frontend/radish.client/dist`
- 窗口：`main`，默认 `1280x800`，最小 `960x640`
- 插件：`tauri-plugin-deep-link`、`tauri-plugin-single-instance`、`tauri-plugin-opener`
- deep link scheme：`radish`
- 前端桥接：`Frontend/radish.client/src/platform/tauriBridge.ts`
- Tauri 环境下根路径 `/` 当前收口到 `/desktop`，进入 WebOS 桌面工作台；Capacitor Android 技术参考入口仍收口到 `/docs`
- Tauri 环境下登录使用系统浏览器打开 `/connect/authorize`，`redirect_uri` 优先切换为 `http://127.0.0.1:48801/oidc/callback`
- Tauri 环境下登出使用系统浏览器打开 `/connect/endsession`，`post_logout_redirect_uri` 优先切换为 `http://127.0.0.1:48801/oidc/logout-complete`
- Rust 壳层会在登录 / 登出发起前启动一次性 loopback listener，收到浏览器回跳后通过 `radish-oidc-loopback` 事件透传给前端，并转换为既有浏览器回调路径：`http://127.0.0.1:48801/oidc/callback?...` -> `/oidc/callback?...`
- `radish://oidc/callback` 与 `radish://oidc/logout-complete` 仍作为 deep link 兼容路径保留，但不再作为 Tauri 桌面登录的优先方案
- Rust 壳层会记录 `Focused`、`Resized` 与 `CloseRequested` 窗口事件，并通过事件桥把 OIDC 回跳透传给前端
- 图标复用仓库已有 `Docs/images/RadishAcg-256.png`，生成 spike 专用 `icons/icon.ico` / `icons/icon.png`

## 验证命令

```powershell
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run test --workspace=radish.client
npm run cap:add:android --workspace=radish.client
npm run cap:sync --workspace=radish.client

$env:JAVA_HOME='D:\Program Files\JetBrains\Android Studio\jbr'
$env:ANDROID_HOME='D:\MyKits\android'
$env:ANDROID_SDK_ROOT='D:\MyKits\android'
.\gradlew.bat assembleDebug

cargo build
cargo build --release
```

## 验证结果

- `npm run type-check --workspace=radish.client`：通过
- `npm run test --workspace=radish.client`：通过，90 个测试通过
- `npm run build --workspace=radish.client`：通过
- `npm run cap:add:android --workspace=radish.client`：通过，生成 Capacitor Android 工程
- `npm run cap:sync --workspace=radish.client`：通过，Web 产物同步到 Android 工程
- `.\gradlew.bat assembleDebug`：通过，临时使用 `D:\MyKits\android` 作为 `ANDROID_HOME` / `ANDROID_SDK_ROOT`
- Debug APK 产物：`Frontend/radish.client/android/app/build/outputs/apk/debug/app-debug.apk`
- Android Studio Pixel 9 Pro API 35 模拟器人工复核：通过，`/docs` 可通过本机 Gateway 正常加载公开文档内容
- 2026-05-04 补充：登录 / OIDC 回调评估在本机调试复杂度确认后终止，未进入人工登录验收；相关临时代码与配置已回滚，最终保留范围仍是 Capacitor Android `/docs` 本机 Gateway 调试链路
- `npm run type-check --workspace=radish.client`：Tauri 桥接后通过
- `npm run test --workspace=radish.client`：Tauri 桥接后通过，96 个测试通过，新增 `tauriBridge` deep link 与 loopback 转换测试
- `npm run build --workspace=radish.client`：Tauri 桥接后通过；第一次沙盒内执行因 Windows `spawn EPERM` 失败，提权后通过
- `cargo build`：通过；第一次沙盒内访问 `crates.io` 因 `schannel: SEC_E_NO_CREDENTIALS` 失败，提权后依赖拉取与编译通过
- `cargo build --release`：通过
- `dotnet build Radish.Auth/Radish.Auth.csproj -v minimal -p:OutDir=D:\Code\Radish\.codex-build\Radish.Auth\`：通过；默认输出目录曾被运行中的 `Radish.Auth` 进程锁定，因此使用独立输出目录验证 OpenIddict loopback 回调白名单
- Tauri release 可执行文件：迁移到 `Clients/radish-tauri` 后新产物名为 `Clients/radish-tauri/target/release/radish-tauri.exe`
- Tauri release exe 大小：`9,590,272 bytes`，约 `9.15 MiB`
- 复用的 Web `dist` 文件数：`100`
- 复用的 Web `dist` 总大小：`2,887,287 bytes`，约 `2.75 MiB`
- 2026-05-04 补充：Tauri 默认入口已从 `/docs` 切换为 `/desktop`，桌面登录 / 登出优先改为系统浏览器 + `127.0.0.1:48801` loopback 回跳，登出完成后回到 `/desktop`
- 2026-05-04 补充：Tauri 第二轮人工验收已通过，覆盖 GUI 启动、WebOS 桌面布局、窗口生命周期观察、系统浏览器登录 / 登出 loopback 回跳；测试后暂未发现问题

## 本机 Gateway 调试

Android Studio / 模拟器调试本机 Gateway 时，`adb reverse` 只会影响设备侧访问对应端口的 `localhost` 请求。若 Capacitor assets 中仍写入 `https://radishx.com` 或 `.env.production` 的占位域名，则端口反向代理不会生效。

本轮新增 `cap:sync:android:local`，用于在 `cap sync android` 后覆盖 Android assets 内的 `runtime-config.js`，让 debug 包通过 HTTP 访问本机 Gateway，避开 Android WebView 对 ASP.NET Core 本机开发证书的信任问题：

```powershell
cd D:\Code\Radish

npm run build --workspace=radish.client
adb reverse tcp:5001 tcp:5001
npm run cap:sync:android:local --workspace=radish.client
```

默认本机 Gateway 地址为 `http://localhost:5001`。如需临时改用其他地址，可设置：

```powershell
$env:CAPACITOR_LOCAL_GATEWAY_URL='http://localhost:5001'
npm run cap:sync:android:local --workspace=radish.client
```

Gateway 开发配置已在 `appsettings.Development.json` 中关闭 HTTPS 重定向，使 `http://localhost:5001` 不再自动跳到 `https://localhost:5000`。若 Gateway 已经启动，需要重启 Gateway 才会读取该配置。Auth cookie 与 antiforgery 安全策略保持原有口径，不为 Capacitor 登录 spike 额外放宽。

Android `debug` 变体已加入调试专用 `networkSecurityConfig`：

- 允许 debug 包信任系统证书与用户安装证书
- 允许 `localhost`、`127.0.0.1`、`10.0.2.2` 明文调试域
- Capacitor Android 配置已开启 `android.allowMixedContent`，允许 `https://localhost` 壳层在 debug 调试中请求 `http://localhost:5001`
- 不改变 `release` 变体安全口径

若改回 `https://localhost:5000` 后仍出现 `ERR_CERT_AUTHORITY_INVALID` 或 Chromium `net_error -202`，说明 Android 设备 / 模拟器仍未信任本机 ASP.NET Core 开发证书；此时应优先使用 `http://localhost:5001` 调试本机 Gateway，或使用 `https://radishx.com` 验证真实环境。

## 验收标准

第一轮 Capacitor spike 通过标准：

1. `radish.client` 类型检查通过
2. `radish.client` 生产构建通过
3. Capacitor Android 平台可生成并同步 Web 产物
4. Android 原生壳启动后默认进入 `/docs`
5. `/docs` 列表、详情、搜索和文档内链具备进一步真机验证条件

当前自动化验证已覆盖第 1-3 项；第 4-5 项仍需安装到真机或模拟器后人工复核。

2026-05-04 补充：第 4-5 项已通过 Android Studio Pixel 9 Pro API 35 模拟器人工复核。本轮 Capacitor Android `/docs` 本机 Gateway 调试链路可收口。

Tauri 桌面壳首轮命令级通过标准：

1. `radish.client` 类型检查通过
2. `radish.client` 测试通过，并覆盖 deep link 到浏览器回调路径的转换
3. `radish.client` 生产构建通过，可生成供 Tauri 复用的 `dist`
4. `Clients/radish-tauri` 可通过 `cargo build`
5. `Clients/radish-tauri` 可通过 `cargo build --release`
6. Windows release exe 可生成，并能明确记录产物大小

当前第 1-6 项均已完成。Tauri 默认入口切到 WebOS 桌面工作台的代码与命令级构建已完成；系统浏览器登录 / 登出已从 `radish://` 优先切换为 loopback 回跳；第二轮 GUI 启动、WebOS 桌面布局、窗口生命周期观察和真实登录 / 登出回跳人工验收已通过，测试后暂未发现问题。installer bundle、签名分发与自动更新链路仍未纳入本轮验收。

## 风险记录

1. 当时 `.env.production` 仍是 `https://your-domain.com` 占位，若要真机访问 `radishx.com`，需要为 spike 构建提供明确的 `VITE_API_BASE_URL / VITE_AUTH_BASE_URL / VITE_SIGNALR_HUB_URL`；该占位已在后续正式桌面包候选身份补验中收口为 `https://radishx.com`
2. 本机 Gateway HTTPS 调试依赖 Android 侧对本机开发证书的信任；默认本机调试已改走 `http://localhost:5001`
3. Capacitor Android WebView 的 OIDC 登录回调评估已终止：本机调试需要同时处理证书、Gateway HTTP 辅助端口、Auth secure cookie、`adb reverse`、runtime config 与 deep link 原生桥，长期维护成本不符合当前路线目标
4. forum 公开页复用桌面论坛组件更多，不作为第一轮入口；docs 通过后再验证 forum
5. Tauri 桌面壳命令级构建复杂度低于 Capacitor Android 登录链路；桌面登录已优先采用系统浏览器 + loopback callback，避免依赖 Windows 注册表；本轮 GUI 人工验证确认浏览器登录态复用、回跳与登出回到 WebOS 的主路径暂未发现问题，端口占用提示仍属于后续健壮性治理项
6. 2026-05-05 补充：Tauri 当前已通过 `cargo tauri build` 生成 Windows NSIS installer，并完成一轮本机安装、启动、普通用户卸载与同身份覆盖安装验证；release 启动伴随命令行窗口的问题已通过 `windows_subsystem = "windows"` 修复；当前本机普通用户安装未出现“未知发布者 / SmartScreen”提示，公开分发后仍需按下载来源、签名、信誉与系统策略复核。管理员安装后用普通权限卸载可能残留安装文件，当前归类为权限上下文不一致风险。自动更新、代码签名、托盘、后台驻留与 deep link 协议注册仍未完成验证
7. Tauri release exe 体积约 `9.15 MiB`；2026-05-05 生成的 Windows NSIS installer 约 `2.54 MiB`，后续正式身份、签名或 updater 接入后需重新记录

## 路线结论

Capacitor Android 可以低成本承载 `/docs` 这类公开只读 React 页面，但不适合作为 Radish 当前移动端产品化主线。主要原因是登录态、本机联调、系统浏览器 OIDC 回调、Auth cookie 安全策略、Android WebView 证书与端口代理之间的耦合复杂度过高。

因此：

1. Flutter Android MVP 继续作为当前已完成移动端主线保留。
2. Capacitor 不进入移动端产品化路线，只保留公开只读页面复用和技术参考价值。
3. Tauri 桌面壳首轮命令级 spike 成立：React / `@radish/http` / `@radish/ui` / Vite `dist` 复用成本低，窗口生命周期与系统浏览器 loopback 登录回跳可用 Rust 原生壳集中承接。
4. Tauri 尚不足以直接切为桌面主线：默认入口已切到 WebOS 桌面工作台，GUI 启动、登录 / 登出回跳、Windows NSIS installer 安装 / 启动 / 普通用户卸载 / 同身份覆盖安装人工验收已通过，本机 SmartScreen 未复现拦截；但仍缺自动更新、签名、deep link 协议注册与正式分发验证。
5. 最终多端开发口径收束为三条线：
   - Web 浏览器：公开内容壳层，适配 PC / 移动浏览器，偏阅读、分享、SEO 和轻交互
   - Android / iOS：Flutter 移动原生安装包，Android MVP 已完成第一轮，iOS 后续单独评估
   - Windows / macOS / Linux：Tauri 壳 + WebOS 桌面工作台，Tauri 承接系统壳能力，WebOS 承接 Dock、窗口系统与桌面业务体验
6. React 复用路线若继续，应优先补 Tauri + WebOS 桌面人工验收和分发链路，而不是回头扩大 Capacitor Android 登录态能力。
