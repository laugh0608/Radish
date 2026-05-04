# React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）

- 记录日期：2026-05-04
- 记录人：laugh0608
- 范围：多端客户端路线评估第一轮 spike
- 当前阶段：Capacitor 移动壳最小验证

## 目标

本轮只验证 React 复用路线能否以较低成本承载移动 App 壳里的公开只读内容入口。

首个验证入口固定为 `/docs`：

1. 复用 `radish.client` 的 Vite 构建链路
2. 复用现有 `PublicEntry` 与 `PublicDocsApp`
3. 复用 `@radish/http`、`@radish/ui`、环境配置与公开 docs API
4. 在 Capacitor Android WebView 中优先打开 `/docs`

## 本轮不做

- 不做 Tauri 桌面壳
- 不做完整移动端产品
- 不做登录、OIDC 回调、通知、写入或系统分享
- 不改后端 API、Auth、Gateway 或数据库
- 不改变 WebOS 桌面入口

## 当前实现

- 依赖：`@capacitor/core`、`@capacitor/android`、`@capacitor/cli`
- 配置：`Frontend/radish.client/capacitor.config.ts`
- `webDir`：`dist`
- `appId`：`com.radish.client.spike`
- `appName`：`Radish React Spike`
- 原生壳启动路径：Capacitor 原生环境下，若当前路径为 `/`，前端入口会将路径收口到 `/docs`

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

Gateway 开发配置已在 `appsettings.Development.json` 中关闭 HTTPS 重定向，使 `http://localhost:5001` 不再自动跳到 `https://localhost:5000`。若 Gateway 已经启动，需要重启 Gateway 才会读取该配置。

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

## 风险记录

1. 当前 `.env.production` 仍是 `https://your-domain.com` 占位，若要真机访问 `radishx.com`，需要为 spike 构建提供明确的 `VITE_API_BASE_URL / VITE_AUTH_BASE_URL / VITE_SIGNALR_HUB_URL`
2. 本机 Gateway HTTPS 调试依赖 Android 侧对本机开发证书的信任；默认本机调试已改走 `http://localhost:5001`
3. Capacitor Android WebView 的 OIDC 登录回调尚未验证，后续如果进入登录 spike，需要单独处理 redirect URI 与深链
4. forum 公开页复用桌面论坛组件更多，不作为第一轮入口；docs 通过后再验证 forum
5. Tauri 桌面壳仍未开始，本记录不能作为桌面路线结论
