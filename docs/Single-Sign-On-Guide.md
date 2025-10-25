# 单点登录（SSO）配置与行为说明（ABP Host + Angular 管理端）

本文说明本仓库在“Host 门户（ABP/OpenIddict） + Angular 管理端（@abp/ng.oauth + angular-oauth2-oidc）/React（Vite）”组合下的登录/登出行为、常见配置与联调要点。

## 架构概览
- 身份提供者（IdP）：`src/Radish.HttpApi.Host`（OpenIddict，ABP Account UI）。
- 客户端（RP）：`angular/` 管理端，使用 `@abp/ng.oauth` 封装的 `angular-oauth2-oidc`；`react/` 为 Vite 示例应用。
- 推荐授权模式：Authorization Code（带 PKCE）。

## 关键配置

- Angular 侧：`angular/src/environments/environment.ts`
  - `oAuthConfig.issuer`: Host 地址（例 `https://localhost:44342/`）。
  - `oAuthConfig.redirectUri`: Angular 站点根（例 `https://localhost:4200`）。
  - `oAuthConfig.responseType`: `code`。
  - `oAuthConfig.scope`: 至少包含 `offline_access Radish`（`offline_access` 用于刷新令牌）。
  - `oAuthConfig.requireHttps`：
    - 推荐本地也启用 HTTPS（见下文证书统一方案），保持 `true`；如确需 HTTP，可临时设为 `false`。

- Host 侧（OpenIddict 应用）需与前端一致：
  - `ClientId`: `Radish_Console`（与 SPA 环境一致）。
  - `RedirectUris`: `https://localhost:4200/`（Angular）与 `https://localhost:5173/`（React）。
  - `PostLogoutRedirectUris`: 同上。
  - CORS：允许上述来源（使用 `.env` 的 `App__CorsOrigins` 配置，见下）。

### 在 DbMigrator 中配置并落库
- 文件：`src/Radish.DbMigrator/appsettings.json`
- 位置：`OpenIddict:Applications:Radish_Console`
- 示例：

```
"OpenIddict": {
  "Applications": {
    "Radish_Console": {
      "ClientId": "Radish_Console",
      "RootUrl": "http://localhost:4200"
    }
  }
}
```

- 运行：
  - `cd src/Radish.DbMigrator && dotnet run`
  - 将在数据库中创建/更新对应的 OpenIddict 应用（见 `OpenIddictDataSeedContributor`）。

### Host 侧与前端对齐的其他设置
- CORS 允许来源通过 `.env` 设置，而非 appsettings：
  - `src/Radish.HttpApi.Host/.env`：`App__CorsOrigins=https://localhost:4200,https://localhost:5173`
  - 启动后端会打印 “CORS allowed origins: ...” 便于确认。
- 其他：
  - `src/Radish.HttpApi.Host/appsettings.json` 中的 `App:AngularUrl`、`App:RedirectAllowedUrls` 仍可按需维护或通过环境变量覆盖。
  - `AuthServer:Authority` 与 Angular `issuer` 保持一致。

## Angular 管理端的 SSO 初始化

- 初始化代码：`angular/src/app/auth.auto-login.ts`
  - 在 APP 启动时执行 `loadDiscoveryDocumentAndTryLogin()` 用于处理回跳（不强制跳转登录）。
  - 若已有有效令牌，则开启 `setupAutomaticSilentRefresh()`；否则保持匿名状态。
  - 监听 `session_terminated` 事件（若 IdP 支持 front-channel/logout），本地触发 `logOut()`。
- 注册位置：`angular/src/app/app.config.ts` 中的 `...AUTO_LOGIN_PROVIDER`。
- 路由守卫：仅对需要保护的路由（例如 `books`）使用 `authGuard`；首页可匿名访问。

## 常见现象与说明

- “Host 登录后进入 Angular，首页仍匿名”：
  - 原因多为浏览器开始默认拦截第三方 Cookie，导致从 Angular（不同源/不同 Scheme）以 iframe 方式静默获取授权时，IdP 会话不可见，返回 `login_required`。
  - 处理：本仓库已实现两种路径，任选其一即可：
    - 首选：本地也用 HTTPS 启动 Angular/React，使其与 Host 同为 `https://localhost`，成为 same-site，上述限制不再命中。
      - 统一证书：运行根目录脚本生成并信任本地证书
        - Bash: `./scripts/ssl-setup.sh`
        - PowerShell: `./scripts/ssl-setup.ps1`
      - Angular：`yarn start`/`npm start` 以 `https://localhost:4200` 启动。
      - React：`npm run dev` 以 `https://localhost:5173` 启动（优先使用根目录 `dev-certs/`）。
      - CORS：在 `src/Radish.HttpApi.Host/.env` 配置 `App__CorsOrigins=https://localhost:4200,https://localhost:5173`，后端启动日志可见。
    - 或者：通过“顶层跳转”触发 Code Flow。Host 门户卡片链接已追加 `?sso=1`，Angular 启动时检测到后会调用 `initCodeFlow()`，利用 IdP 现有登录会话无感回跳，从而实现 SSO。
- “Host 注销后，Angular 刷新仍显示已登录”：
  - 原因：SPA 本地持有 access/refresh token；Host 的登出仅清除了 IdP 会话，不会强制使 SPA 的令牌失效。
  - 何时会失效：刷新令牌被吊销/过期；或启用前/后通道登出（见下）。

## 实现“真正的单点登出”的方案（择需）

1) 启用前/后通道登出（Front/Back-Channel Logout）
- 目标：当用户在 Host 登出时，通知所有 RP（含 Angular）主动清空本地令牌并跳转。
- 参考要点：
  - OpenIddict 支持 RP 发起的 `end_session`；要从 IdP 主动通知 RP，可结合前通道 iFrame 或后通道回调（框架与浏览器限制较多，需根据部署域名与安全策略评估）。
  - Angular 侧已监听 `session_terminated`（若浏览器收到该事件），并会本地登出。

2) 吊销刷新令牌（Refresh Token Revocation）
- 在 Host 的登出流程中，统一吊销该客户端的 refresh token：
  - 吊销后，Angular 的静默续期会失败，ABP OAuth 包会清理本地并引导重新登录。
 - 参考实现（伪代码示意）：

```
// 在自定义的 Logout 应用服务或控制器中：
public class SignOutAppService : ApplicationService
{
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly ICurrentUser _currentUser;

    public SignOutAppService(IOpenIddictTokenManager tokenManager, ICurrentUser currentUser)
    {
        _tokenManager = tokenManager;
        _currentUser = currentUser;
    }

    public async Task RevokeAllForCurrentUserAsync()
    {
        // 根据主体查找并吊销当前用户的刷新令牌（可限定 ClientId）
        await foreach (var token in _tokenManager.FindBySubjectAsync(_currentUser.GetId().ToString()))
        {
            if (await _tokenManager.HasTypeAsync(token, OpenIddictConstants.TokenTypeHints.RefreshToken))
            {
                await _tokenManager.TryRevokeAsync(token);
            }
        }
    }
}
```

> 注意：生产中应按需限定 ClientId、TenantId 等范围，并结合业务登出流程调用该接口。

3) 降低令牌时效或关闭刷新
- 通过较短存活时间缩短不同步窗口；或不发放 `offline_access`（需权衡用户体验）。

## 开发联调 Checklist

- 确认 Host 应用中 OpenIddict 的应用配置（ClientId、重定向地址、PostLogoutRedirectUris、CORS）。
- Angular 环境文件的 `issuer/redirectUri/scope/requireHttps` 与 Host 对齐。
- 如切换到 HTTPS 开发：
  - 在仓库根运行一次证书脚本生成并信任（见上）。
  - Angular：`yarn start` 以 `https://localhost:4200` 启动；React：`npm run dev` 以 `https://localhost:5173` 启动。
  - 运行一次迁移以更新 OpenIddict 应用：`cd src/Radish.DbMigrator && dotnet run`。
  - 后端 `.env` 的 `App__CorsOrigins` 包含 `https://localhost:4200,https://localhost:5173`；启动日志可见。
- 受保护页面使用 `authGuard`；首页是否匿名根据需求配置。
- Dev 环境若不启用 TLS：
  - `requireHttps=false`；浏览器若阻止第三方 Cookie，回跳后可能无法读取会话，请在同源或启用受信任证书下测试。

## 常见报错速查

- `Cannot find the zh-Hans locale file`：
  - 使用 esbuild 的项目需在 `app.config.ts` 中使用 `registerLocaleForEsBuild()`（本项目已配置）。
- `issuer must use HTTPS (with TLS)`：
  - Dev 场景可在 `environment.ts` 中把 `requireHttps` 设为 `false`；生产必须保持 `true`。
- `NG0203: inject() must be called from an injection context`：
  - APP 初始化请使用标准 `APP_INITIALIZER + deps` 注入依赖，避免在非注入上下文直接 `inject()`（本项目已修正）。

## 相关文件
- Angular 环境：`angular/src/environments/environment.ts`
- SSO 初始化：`angular/src/app/auth.auto-login.ts`
- App 配置：`angular/src/app/app.config.ts`
- 受保护路由示例：`angular/src/app/app.routes.ts`
- OpenIddict 应用种子：`src/Radish.Domain/OpenIddict/OpenIddictDataSeedContributor.cs`
- DbMigrator 配置：`src/Radish.DbMigrator/appsettings.json`

## 可直接调用的撤销接口（Host 示例）

- 控制器：`src/Radish.HttpApi/Controllers/ConventionalControllers/V1/SsoController.cs`
- 路由（V1）：`POST /api/v1/sso/RevokeMyTokens`
  - Query 参数：
    - `clientId`（可选）：仅撤销这个客户端颁发的令牌，例如 `Radish_Console`。
    - `includeAccessTokens`（可选，默认 false）：是否同时撤销访问令牌（默认只撤销刷新令牌）。
  - 认证：需要已登录（Bearer/Cookies 均可）。
  - 返回：`{ revoked: number }` 撤销数量。

使用示例（Bearer）：

```
curl -X POST \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  "https://localhost:44342/api/v1/sso/RevokeMyTokens?clientId=Radish_Console&includeAccessTokens=false"
```

> 注意：该接口未集成到现有登出流程。需要时手动调用；如需后台登出时同步吊销刷新令牌，可在自己的登出逻辑中调用相应能力。

## 构造前通道登出地址（Host 示例）

- 控制器：`src/Radish.HttpApi/Controllers/ConventionalControllers/V1/SsoController.cs`
- 路由（V1）：`GET /api/v1/sso/GetEndSessionUrl`
  - Query 参数：
    - `redirectUri`（可选）：登出后回跳地址，默认取 `App:AngularUrl`。
    - `state`（可选）：自定义状态，原样透传给回跳地址。
  - 返回：`{ url, authority, postLogoutRedirectUri }`。
  - 说明：内部根据 `AuthServer:Authority` 构造 `<authority>/connect/logout?post_logout_redirect_uri=...`；如未配置，则基于当前请求 Scheme/Host 推断。

示例：

```
curl "https://localhost:44342/api/v1/sso/GetEndSessionUrl?redirectUri=https://localhost:4200"
```

前端可直接 `window.location.href = url` 触发前通道登出并回跳。

---

如需我在 Host 侧补充 OpenIddict 的示例配置（创建应用、登记重定向、吊销刷新令牌等），请提出你的运行环境与版本，我将补充对应片段。
