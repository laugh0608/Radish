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

## 验收标准

第一轮 Capacitor spike 通过标准：

1. `radish.client` 类型检查通过
2. `radish.client` 生产构建通过
3. Capacitor Android 平台可生成并同步 Web 产物
4. Android 原生壳启动后默认进入 `/docs`
5. `/docs` 列表、详情、搜索和文档内链具备进一步真机验证条件

当前自动化验证已覆盖第 1-3 项；第 4-5 项仍需安装到真机或模拟器后人工复核。

## 风险记录

1. 当前 `.env.production` 仍是 `https://your-domain.com` 占位，若要真机访问 `radishx.com`，需要为 spike 构建提供明确的 `VITE_API_BASE_URL / VITE_AUTH_BASE_URL / VITE_SIGNALR_HUB_URL`
2. Capacitor Android WebView 的 OIDC 登录回调尚未验证，后续如果进入登录 spike，需要单独处理 redirect URI 与深链
3. forum 公开页复用桌面论坛组件更多，不作为第一轮入口；docs 通过后再验证 forum
4. Tauri 桌面壳仍未开始，本记录不能作为桌面路线结论
