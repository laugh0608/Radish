# 开发日志

## 第三阶段

> OIDC 认证中心与前端框架搭建

### 2025.12.09

- **chore(auth/openiddict)**: 移除尚未接入的自定义 SqlSugar Store，并确认继续采用 EF Core 存储
  - 删除 `Radish.Auth/OpenIddict/Stores` 下的临时实现（RadishApplicationStore/RadishAuthorizationStore/RadishScopeStore/RadishTokenStore）及 `OpenIddictSqlSugarExtensions`，避免误导
  - 保留 `Radish.Model/OpenIddict/*` 实体，继续作为客户端管理 API 的 DTO/视图模型来源
  - `Radish.Auth/Program.cs` 仍通过 `AuthOpenIddictDbContext` + SQLite (`DataBases/RadishAuth.OpenIddict.db`) 持久化 OpenIddict 数据；Api 项目仅共享 `IOpenIddictApplicationManager` 访问该库
  - 在 DevelopmentPlan/AuthenticationGuide 中更新说明：Auth 负责 OpenIddict 数据库的创建与迁移，Api 只消费，不再计划切换 SqlSugar Store

### 2025.12.08

- **feat(log)**: 统一日志输出到解决方案根目录
  - **问题**：每个项目（Api、Auth、Gateway）都在自己的目录下生成 Log 文件夹，分散且不便管理
  - **解决方案**：修改 Serilog 配置，统一输出到解决方案根目录的 `Log/` 文件夹
    - `Radish.Common/LogTool/LogContextTool.cs`：
      - 新增 `GetSolutionLogDirectory()`：向上查找 .slnx/.sln 文件定位解决方案根目录
      - 新增 `GetProjectName()`：从 .csproj 文件自动识别当前项目名称
      - `BaseLogs` 改为动态计算的解决方案根目录路径
      - 添加 `ProjectName` 静态属性用于区分不同项目
    - `Radish.Extension/SerilogExtension/LogConfigExtension.cs`：
      - 修改日志路径包含项目名称：`Log/{ProjectName}/Log.txt`、`Log/{ProjectName}/AopSql/AopSql.txt`
    - `Radish.Extension/SerilogExtension/SerilogSetup.cs`：
      - Serilog 内部调试日志路径：`Log/{ProjectName}/SerilogDebug/Serilog{date}.txt`
  - **新的日志结构**：
    ```
    Log/
    ├── Radish.Api/
    │   ├── Log20251208.txt
    │   ├── AopSql/AopSql20251208.txt
    │   └── SerilogDebug/Serilog20251208.txt
    ├── Radish.Gateway/...
    └── Radish.Auth/...
    ```
  - **优势**：所有项目日志集中管理，便于查看和归档；自动识别项目名称，无需手动配置

- **feat(dbmigrate)**: seed 命令自动检测并初始化表结构
  - **问题**：用户直接运行 `seed` 时，如果表不存在会报错 `no such table: Role`
  - **解决方案**：
    - `Radish.DbMigrate/Program.cs`：
      - `RunSeedAsync` 中添加表结构检查，使用 `db.DbMaintenance.IsAnyTable("Role", false)` 检测
      - 如果表不存在，自动执行 `RunInitAsync` 创建表结构
      - 优化输出信息，添加状态标识（✓ 和 ⚠️）
    - `start.ps1` 和 `start.sh`：
      - 更新 DbMigrate 菜单说明，默认命令改为 `seed`（更常用）
      - 添加命令说明：init - 仅初始化表结构；seed - 智能初始化（自动检测表结构）
  - **优势**：简化操作流程，新用户只需运行 `seed` 即可完成所有初始化，无需先 `init` 再 `seed`

### 2025.12.07

- **feat(i18n/unified-language-codes)**: 统一前后端语言代码为 zh 和 en
  - **问题背景**：
    - 前端 i18next 使用 `en` 和 `zh-CN`
    - Auth 后端配置了 `zh-CN`、`en`、`en-US`
    - API 后端配置了 `zh-CN`、`zh-Hans`、`en`、`en-US`
    - 资源文件命名不一致：`Errors.zh-CN.resx`、`Errors.zh-Hans.resx`、`Errors.en-US.resx`
    - 导致前端传递 `en` 时后端无法匹配，回退到默认中文
  - **解决方案**：统一使用中性语言代码 `zh` 和 `en`
    - 前端（`radish.client`）：
      - `i18n.ts`：将 `zh-CN` 改为 `zh`，更新 `fallbackLng` 和 `supportedLngs`
      - `App.tsx`：所有语言相关代码统一使用 `zh` 和 `en`（语言切换、URL 参数、Accept-Language 等）
    - Auth 项目（`Radish.Auth`）：
      - `Program.cs`：`SupportedCultures` 只保留 `zh` 和 `en`
      - `AuthorizationController.cs`：在重定向到登录页时提取 culture 参数，确保语言参数在登录页 URL 上而非 ReturnUrl 内
      - `Login.cshtml`：语言切换按钮改为 `zh` 和 `en`
      - 资源文件：删除 `Errors.zh-CN.resx` 和 `Errors.en-US.resx`，重命名为 `Errors.zh.resx` 和 `Errors.en.resx`
    - API 项目（`Radish.Api`）：
      - `Program.cs`：`SupportedCultures` 只保留 `zh` 和 `en`
      - 资源文件：删除 `Errors.zh-CN.resx`、`Errors.zh-Hans.resx`、`Errors.en-US.resx`，只保留 `Errors.zh.resx` 和 `Errors.en.resx`
  - **中间件顺序修复**（`Radish.Auth/Program.cs`）：
    - 将 `UseRequestLocalization` 移到 `UseRouting` 之前，确保在路由和控制器执行前设置 Culture
    - 修复前：`UseStaticFiles → UseRouting → UseCors → UseRequestLocalization`（❌ Culture 设置太晚）
    - 修复后：`UseStaticFiles → UseRequestLocalization → UseRouting → UseCors`（✅ 正确顺序）
  - **优势**：
    - 前后端语言代码完全一致，无需映射转换
    - 简洁明了，避免 zh-CN/zh-Hans/en-US/en 混乱
    - 资源文件结构清晰：`Errors.resx`（默认）、`Errors.zh.resx`、`Errors.en.resx`
    - 保留扩展性：未来可按需添加 `zh-TW`（繁体中文）、`en-GB`（英式英语）等

### 2025.12.06

- **feat(api/client-management)**: 实现 OIDC 客户端管理 API
  - **ClientController**（`Radish.Api/Controllers/ClientController.cs`）：
    - 实现完整的 CRUD API：`GetClients`（分页+搜索）、`GetClient`、`CreateClient`、`UpdateClient`、`DeleteClient`、`ResetClientSecret`
    - 使用 `IOpenIddictApplicationManager` 直接操作 OpenIddict 数据库
    - 软删除实现：使用 OpenIddict Properties 字段存储 `IsDeleted`、`CreatedAt/By`、`UpdatedAt/By`、`DeletedAt/By`
    - 所有接口需要 `System` 或 `Admin` 角色权限（`[Authorize(Roles = "System,Admin")]`）
    - API 路由遵循项目规范：`/api/v1/Client/[action]`
  - **数据模型**（`Radish.Model/ViewModels/Client/`）：
    - `ClientVo`：客户端视图模型（列表和详情）
    - `CreateClientDto`：创建客户端请求 DTO
    - `UpdateClientDto`：更新客户端请求 DTO
    - `ClientSecretVo`：客户端密钥返回模型（仅在创建/重置时返回一次）
  - **分页模型**（`Radish.Model/PageModel.cs`）：
    - 新增通用分页模型 `PageModel<T>`，包含 `Page`、`PageSize`、`DataCount`、`PageCount`、`Data`
  - **项目依赖**（`Radish.Api/Radish.Api.csproj`）：
    - API 项目新增对 Auth 项目的引用，以共享 `AuthOpenIddictDbContext`
    - API 和 Auth 项目共享同一个 OpenIddict 数据库

- **refactor(database/unified-path)**: 统一所有数据库文件到 DataBases 文件夹
  - **OpenIddict 数据库共享**（`Radish.Api/Program.cs` + `Radish.Auth/Program.cs`）：
    - 两个项目通过查找 `Radish.slnx` 文件定位解决方案根目录
    - 默认数据库路径：`{SolutionRoot}/DataBases/RadishAuth.OpenIddict.db`
    - Auth 项目启动时创建数据库并初始化种子数据
    - API 项目通过 `IOpenIddictApplicationManager` 访问同一数据库
  - **SqlSugar 数据库路径**（`Radish.Common/DbTool/BaseDbConfig.cs`）：
    - 修改 `SpecialDbString` 方法，SQLite 数据库自动存储到 `{SolutionRoot}/DataBases/`
    - 新增 `FindSolutionRoot()` 方法，通过查找 `Radish.slnx` 定位解决方案根目录
    - 配置文件中只需填写文件名（如 `Radish.db`），路径自动拼接
  - **最终数据库布局**：
    ```
    Radish/
    └── DataBases/
        ├── Radish.db                    # API 主数据库（SqlSugar）
        ├── RadishLog.db                 # API 日志数据库（SqlSugar）
        └── RadishAuth.OpenIddict.db     # OpenIddict 数据库（EF Core，Auth + API 共享）
    ```

- **refactor(docs/folder-rename)**: 重命名根目录 docs 文件夹为 Docs
  - 将 `docs/` 重命名为 `Docs/`，与 `radish.docs/` 区分
  - 更新 `README.md`、`CLAUDE.md`、`AGENTS.md` 中的相关引用
  - `Docs/` 作为文档入口，提供跳转链接到 `radish.docs/docs/` 的实际文档内容

- **feat(scalar+auth/oidc-integration)**: Scalar API 文档集成 OIDC 认证，优化 Auth 服务配置
  - **Scalar OAuth2 配置**（`Radish.Extension/OpenApiExtension/ScalarSetup.cs`）：
    - 在 OpenAPI 文档中添加 OAuth2 Security Scheme，定义 Authorization Code Flow
    - 配置 Scalar UI 的 OAuth2 认证：`AddPreferredSecuritySchemes("oauth2")` + `AddOAuth2Flows`
    - 设置 `ClientId="radish-scalar"`，`RedirectUri="https://localhost:5000/scalar/oauth2-callback"`
    - 默认 Scopes：`openid`、`profile`、`radish-api`
    - 修复服务器列表显示问题：清空默认列表，添加 Gateway HTTPS/HTTP + API 直连三个选项
  - **Auth Server Scopes 注册**（`Radish.Auth/Program.cs`）：
    - 添加 `options.RegisterScopes("openid", "profile", "offline_access", "radish-api")`
    - 解决 `invalid_scope` 错误：OpenIddict Server 必须显式注册允许使用的 scopes
  - **客户端授权类型调整**（`Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs`）：
    - 将 `radish-scalar` 的 `ConsentType` 从 `Implicit` 改为 `Explicit`
    - 现在每次授权都会显示授权确认页面，方便测试和调试
  - **使用方式**：
    1. 访问 `https://localhost:5000/scalar`（通过 Gateway）
    2. 点击右上角 **Authenticate** 按钮
    3. 选择 **oauth2** 认证方式，点击 **Authorize**
    4. 使用测试账号登录（用户名：`test`，密码：`P@ssw0rd!`）
    5. 确认授权后，所有 API 请求自动携带 Bearer Token

- **docs(auth+database)**: 更新认证和数据库文档
  - **AuthenticationGuide.md**：
    - 更新第 4.4 节"客户端动态管理"：详细说明软删除和审计字段的实现
    - 更新管理 API 表格：使用实际的 API 路由（`/api/v1/Client/*`）
    - 更新创建客户端示例：使用实际的请求/响应格式
    - 新增第 13 章"数据库配置"：详细说明 OpenIddict 和 SqlSugar 数据库的配置方式
    - 新增第 14 章"Scalar API 文档集成"：详细说明 OIDC 认证配置和使用方式

### 2025.12.03

- **feat(gateway+client/oidc-through-gateway)**: 通过 Gateway 打通 Auth + Api + 前端的完整 OIDC 授权码链路
  - `Radish.Api/Radish.Api.oidc.http` 增补“6. （推荐）全量通过 Gateway 的 OIDC 测试流程”，统一使用 `https://localhost:5000` 作为 OIDC 入口（登录 / 授权码 / 换取 Token / 当前用户接口）。
  - `Radish.Gateway/appsettings.Local.json` 中为 `/Account/{**catch-all}` 与 `/connect/{**catch-all}` 新增 `auth-account-route` / `auth-connect-route`，将 OIDC 相关端点通过 Gateway 转发到 `Radish.Auth`，前端与 `.http` 示例均不再直连 5200 端口。
  - `radish.client/src/App.tsx` 新增最小 OIDC Demo：
    - 在首页“Authentication” 区块提供“通过 OIDC 登录”与“退出登录”按钮，登录走 `GET {apiBaseUrl}/connect/authorize`，退出调用 `POST {apiBaseUrl}/Account/Logout` 并清理 `localStorage` 中的 access_token/refresh_token。
    - 实现 `OidcCallback` 回调组件，处理 `/oidc/callback?code=xxx`，调用 `POST {apiBaseUrl}/connect/token` 换取 Token，持久化到 `localStorage` 后跳转回首页。
    - 首页挂载时通过 `GET {apiBaseUrl}/api/v1/User/GetUserByHttpContext` 拉取当前用户信息，并在界面显示 `userId/userName/tenantId`，验证 Auth ↔ Api ↔ Db 的映射配置。
  - 前端增加轻量级 `apiFetch` 封装，统一附带 `Accept: application/json` 与可选的 Authorization 头，为后续 WebOS 子应用复用网关 API 提供最小示例。

- **fix(dbmigrate/role-permission)**: 记录 `RoleModulePermission` 表缺失导致本地 SQLite 抛出 `SQLite Error 1: 'no such table: RoleModulePermission'` 时的推荐修复路径
  - 建议在本地环境遇到该错误时执行：
    - `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init`
    - `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed`
  - `init` 通过 `CodeFirst.InitTables` 为所有实体（包含 `RoleModulePermission`）建表，`seed` 则按约定灌入 System/Admin 与 `GetUserByHttpContext` 相关的角色权限种子数据，确保权限表结构与数据与当前 OIDC/Auth 策略保持一致。

- **docs(auth+gateway+frontend)**: 同步认证与 Gateway 文档，标记当前实现状态
  - `AuthenticationGuide.md` 中说明当前仓库已在 `radish.client/src/App.tsx` 中实现一套通过 Gateway 的极简 OIDC 流程，本节其余 `oidc-client-ts/react-oidc-context` 内容为后续正式接入方案。
  - `GatewayPlan.md` Phase 0 验收标准中补充“通过 Gateway 反向代理 Radish.Auth 的 `/Account` 与 `/connect` 端点”，明确本地 OIDC 调试入口统一为 Gateway (`https://localhost:5000`)。
  - `FrontendDesign.md` 在 M4 迭代计划中说明：当前仍暂时保留 `src/App.tsx` 作为 WeatherForecast + Gateway OIDC 登录/退出的 Demo 页，未来将由 WebOS 桌面 Shell 取代。

### 2025.12.02

- **feat(auth+api/oidc-minimal)**: 打通 Radish.Auth 与 Radish.Api 的最小 OIDC 授权码 + 资源服务器链路
  - 在 `Radish.Auth` 中接入 OpenIddict EF Core 集成：
    - 新增 `AuthOpenIddictDbContext`（/Radish.Auth/OpenIddict/AuthOpenIddictDbContext.cs），专门承载 OpenIddict 的 Application/Authorization/Scope/Token 实体，使用 Sqlite 本地文件 `RadishAuth.OpenIddict.db`
    - `Program.cs` 中通过 `AddDbContext<AuthOpenIddictDbContext>()` + `.AddOpenIddict().AddCore().UseEntityFrameworkCore().UseDbContext<AuthOpenIddictDbContext>()` 正式启用 EF Core 存储
    - 启动时调用 `db.Database.EnsureCreated()` 自动建表
  - 配置 OpenIddict Server 仅对 access_token 使用签名 JWT，不再加密：
    - 使用 `options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate()` 配置开发环境的加密/签名证书
    - 调用 `options.DisableAccessTokenEncryption()`，保留内部票据（授权码/RefreshToken）的加密，但 access_token 统一发出 3 段 JWS，方便 `Radish.Api` 通过 `JwtBearer` 验签
    - Issuer 从配置读取：`OpenIddict:Server:Issuer = http://localhost:5200`，与本地 Auth 服务地址保持一致
  - 落地最小可用 OIDC 控制器：
    - `AccountController`（/Radish.Auth/Controllers/AccountController.cs）
      - `GET /Account/Login`：返回极简 HTML 表单，预填测试账号 `test / P@ssw0rd!`，方便浏览器直接登录
      - `POST /Account/Login`：校验固定账号，写入 Cookie 认证会话，Claims 中包含 `sub=1`、`name=test`、`role=System`
    - `AuthorizationController`（/Radish.Auth/Controllers/AuthorizationController.cs）
      - `~/connect/authorize`：
        - 未登录 → 通过 Cookie 认证方案 `Challenge` 到 `/Account/Login?returnUrl=...`
        - 已登录 → 从 `HttpContext.GetOpenIddictServerRequest()` 读取 client_id、redirect_uri、scope 等信息
        - 构造 `ClaimsPrincipal`，确保存在 `sub`，然后 `principal.SetScopes(request.GetScopes())` + `SetResources("radish-api")`，交给 OpenIddict 生成授权码
    - `UserInfoController`：实现 `~/connect/userinfo` 基于当前用户 Claims 返回基本信息（sub/name/role 等）
  - 种子数据（Scope + Client）：
    - `OpenIddictSeedHostedService`（/Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs）：
      - Scope：`radish-api`（Name=radish-api，Resources=["radish-api"]）
      - Client：`radish-client`：
        - ClientId="radish-client"，DisplayName="Radish Web Client"
        - RedirectUris=["https://localhost:5000/oidc/callback"], PostLogoutRedirectUris=["https://localhost:5000"]
        - Permissions：Authorization Endpoint、Token Endpoint、AuthorizationCode、RefreshToken、ResponseTypes.Code、`scope:radish-api`
        - 移除强制 PKCE 要求（便于目前手工使用 `.http` 调试），后续前端接入后再根据需要重新开启
  - `Radish.Api` 作为资源服务器信任 `Radish.Auth` 发出的 access_token：
    - `Program.cs`（/Radish.Api/Program.cs）：
      - 配置 JwtBearer：
        - `options.Authority = "http://localhost:5200";`
        - 暂不设置 `Audience`，并在 `TokenValidationParameters` 中关闭 `ValidateAudience`，只验证签名 + 时效：
          ```csharp
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidateAudience = false,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ClockSkew = TimeSpan.Zero
          };
          ```
        - `options.RequireHttpsMetadata = false`（本地使用 http 调试，后续通过 Gateway 暴露 https）
      - 中间件顺序：`app.UseAuthentication();` 在 `UseAuthorization()` 之前，确保 JWT 认证实际生效
    - 授权策略：
      - `Client` 策略改为基于 `scope=radish-api` 控制访问资源服务器：
        ```csharp
        .AddPolicy("Client", policy =>
            policy.RequireClaim("scope", "radish-api").Build())
        ```
  - 用于验证链路的调试接口与 .http 脚本：
    - `UserController.GetUserByHttpContext`（/Radish.Api/Controllers/UserController.cs）
      - 控制器级别：`[Authorize]`（只要求已认证）
      - 方法级别：`[Authorize(Policy = "Client")]`，只要 access_token 里有 `scope=radish-api` 即可访问
      - 从 `IHttpContextUser` 读取 `UserId/UserName/TenantId` 并返回，目前由于 Claim 映射仍按旧 JWT 方案实现，返回的是 `0/""/0`，后续单独补齐映射逻辑
    - 新增 `Radish.Api/Radish.Api.oidc.http`，用于手动验证整个 OIDC 流程：
      1. 浏览器访问 `http://localhost:5200/Account/Login`，使用 `test / P@ssw0rd!` 登录
      2. 浏览器访问 `http://localhost:5200/connect/authorize?response_type=code&client_id=radish-client&redirect_uri=https%3A%2F%2Flocalhost%3A5000%2Foidc%2Fcallback&scope=radish-api`，从回调 URL 中复制 `code`
      3. 使用 `.http` 中的 `POST http://localhost:5200/connect/token` 请求，用 `grant_type=authorization_code&client_id=radish-client&code=...&redirect_uri=...` 换取 access_token（为 3 段 JWT）
      4. 在 `.http` 中使用 `Authorization: Bearer {access_token}` 调用 `GET http://localhost:5100/api/v1/User/GetUserByHttpContext`，确认返回 200 表示 Auth+Api 最小 OIDC 流程已经打通

### 2025.12.01

- **feat(auth/project)**: 创建 Radish.Auth OIDC 认证服务器项目
  - 集成 OpenIddict 7.2.0 框架，配置 OIDC 标准端点（/connect/authorize、/connect/token、/connect/userinfo、/connect/introspect、/connect/revoke）
  - 配置服务端口 `http://localhost:5200`（内部端口，对外通过 Gateway 暴露）
  - 支持三种授权流程：Authorization Code Flow（授权码流程）、Refresh Token Flow（刷新令牌）、Client Credentials Flow（客户端凭证）
  - 配置开发/生产环境密钥管理：开发环境使用临时密钥，生产环境强制使用固定加密密钥
  - 集成 Cookie 认证（用于登录页面会话管理）
  - 完整的配置文件模板：appsettings.json、appsettings.Local.example.json
  - WorkId 约定：Auth 服务使用 WorkId=2（API=0, Gateway=1, Auth=2）
- **feat(auth/models)**: 创建 OIDC 数据模型与 ViewModels
  - 新增 `UserClaim` 实体：存储 OIDC 声明和自定义用户声明
  - 新增 OpenIddict 自定义实体（位于 `Radish.Model/Models/OpenIddict/`）：
    - `RadishApplication`：OAuth 客户端应用管理，包含状态（Active/Disabled/PendingReview）和类型（Internal/ThirdParty）枚举
    - `RadishAuthorization`：用户授权记录
    - `RadishScope`：OAuth 作用域定义
    - `RadishToken`：令牌存储（access_token、refresh_token、authorization_code）
  - 对应的 ViewModels：UserClaimVo、VoOidcApp、VoOidcAuth、VoOidcScope、VoOidcToken
  - AutoMapper 映射配置：`OidcProfile.cs`，特殊处理 ClientSecret 隐私保护和 PayloadPreview 截断
- **feat(auth/startup)**: 完成 Program.cs 配置
  - Autofac 容器集成（AutofacModuleRegister + AutofacPropertyModuleReg）
  - Serilog 日志配置（AppSettingsTool 前置注册）
  - SqlSugar ORM + Snowflake ID 配置
  - Redis/内存缓存切换支持
  - CORS 跨域配置（允许 Gateway、前端、文档等来源）
  - OpenIddict Server 端点透传（EnableAuthorizationEndpointPassthrough、EnableTokenEndpointPassthrough、EnableUserInfoEndpointPassthrough）
  - 开发环境禁用 HTTPS 要求（DisableTransportSecurityRequirement）
  - 启动日志输出（与 API/Gateway 风格统一）
- **chore(auth/test)**: 验证项目编译和启动
  - 编译成功，无警告和错误
  - 服务成功启动在 http://localhost:5200
  - 日志输出正常，显示监听地址和 CORS 配置
- **plan(auth/next)**: 规划 Auth 项目后续工作（按优先级）
  1. 创建 OIDC 端点控制器（AuthorizationController、TokenController、UserInfoController、AccountController）
  2. 实现 OpenIddict 自定义 SqlSugar Store（替代当前的内存存储，支持生产环境持久化）（2025.12.09 更新：本计划已取消，统一改为长期使用 EF Core `AuthOpenIddictDbContext`）
  3. 创建 Radish.DbSeed 项目（数据库初始化、预注册 OIDC 客户端：radish-client、radish-scalar、radish-rust-ext）
  4. 实现客户端管理 API（CRUD 接口管理 OIDC 客户端应用）
  5. 配置 Radish.Api 为资源服务器（添加 JWT Bearer 验证，从 Auth 服务验证访问令牌）
  6. 前端集成（WebOS 前端对接 OIDC 登录流程）

### 2025.11.27

- **feat(gateway/portal)**: 优化 Gateway 门户页面 URL 显示与配置管理
- **feat(gateway/status)**: Gateway Phase 0 调整为仅负责 `/server` 简单欢迎页与健康检查透传，其余服务总览迁移到 Console 控制台页面；控制台通过 Gateway `/console` 访问，并在内部展示前端、API、Docs、Console 的统一状态（所有探活均基于 Gateway 路径 `/`、`/docs`、`/api/health`、`/console`）。
- **chore(dev/ports)**: 统一本地开发端口约定：`Radish.Api` 使用 `http://localhost:5100`（内部），`Radish.Gateway` 使用 `https://localhost:5000` 与 `http://localhost:5001` 作为唯一对外入口；前端 `radish.client` 使用 `http://localhost:3000`，Docs 使用 `http://localhost:3001`（`base=/docs/`），Console 使用 `http://localhost:3002`。更新 `start.sh`/`start.ps1` 菜单文案与 ASPNETCORE_URLS，所有前端/API 对外访问统一通过 Gateway 转发（如 `/api`、`/docs`、`/console`）。

  - 修复服务卡片 URL 溢出问题：添加 `word-break`、`overflow-wrap` 自动换行支持
  - 增加服务卡片最小宽度至 280px，为 URL 提供更多显示空间
  - 实现从配置文件读取服务 URL：新增 `GatewayService.PublicUrl` 配置项
  - 移除 HTTP 端口显示，每个服务仅显示主 URL（开发环境使用 HTTPS 端口）
  - JavaScript 健康检查 URL 从配置动态读取，通过 `data-*` 属性传递
- **feat(gateway/config)**: 创建生产环境配置示例 `appsettings.Production.example.json`
  - 说明反向代理场景下的配置方式（公网域名 + 内网 HTTP 端口）
  - 提供生产环境 CORS、服务地址等配置示例
- **docs(deployment)**: 明确反向代理最佳实践：TLS 终止在反向代理层，后端服务使用 HTTP 端口
- **refactor(gateway/portal)**: Gateway 门户页面配置化改造，为生产环境部署做好准备

### 2025.11.25

- **decision(gateway)**: 复盘 Gateway 与 Auth 的优先级，确认当前阶段仅有 `Radish.Api + Radish.Auth` 两个宿主，复杂度有限，Gateway 投入收益不高；因此 Gateway 项目整体移至 M9（暂缓），只在接口/配置中保留将来透传所需的 Header/Trace/Token 字段。
- **plan(auth-first)**: 将“Radish.Auth + OIDC 客户端集成”明确为 M3-M4 的唯一主线，后续 WebOS/桌面子应用、Scalar OAuth、客户端管理均依赖该服务，团队资源集中支持 Auth 交付。
- **docs(plan/framework/gateway)**: `DevelopmentPlan.md` 更新里程碑表与周计划提示，强调 Gateway 暂缓、Auth 为前置；`DevelopmentFramework.md` 与 `GatewayPlan.md` 追加状态说明及执行条件，统一文档口径。

### 2025.11.24

- **arch(auth)**: 决定采用 OIDC（OpenID Connect）架构替代原有的简单 JWT 认证，使用 OpenIddict 作为认证服务器实现。主要考量：
  - 一次登录可访问多个客户端（Scalar API 文档、前端、后台管理、未来的商城等）
  - 标准化协议，便于未来扩展第三方登录
  - 支持 Authorization Code + PKCE 流程，更安全
  - 可独立扩展认证服务
- **plan(phase3)**: 重新规划第三阶段工作顺序，调整里程碑 M3/M4 内容：
  - M3 聚焦 OIDC 认证中心：创建 Radish.Auth 项目、设计身份数据模型、实现 DbSeed 初始化、配置 OpenIddict 端点、集成 Scalar OAuth
  - M4 聚焦前端框架与认证：搭建桌面模式骨架、基础组件库、OIDC 客户端集成、资源服务器配置
- **design(identity)**: 规划身份数据模型目录结构 `Radish.Model/Models/Identity/`，包含 User、Role、UserRole、UserClaim、Tenant、Permission、RolePermission 等实体
- **design(clients)**: 预定义 OIDC 客户端注册：
  - `radish-client` - WebOS 前端客户端（包含论坛/聊天/商城/后台管理子应用）
  - `radish-scalar` - API 文档客户端
  - `radish-rust-ext` - Rust 扩展项目客户端
- **docs**: 更新 DevelopmentPlan.md 里程碑概览与周计划，详细描述第 3-4 周的 OIDC 相关任务与验收标准
- **arch(open-platform)**: 确认开放平台需求，OIDC 客户端需支持后台动态配置：
  - 内部应用：radish-client（WebOS）、radish-scalar、radish-rust-ext
  - 第三方应用：未来支持动态注册和接入
  - 后台管理（作为 WebOS 子应用）需提供应用管理界面（CRUD、重置 Secret、启用/禁用）
- **arch(frontend/webos)**: 确认采用 **WebOS/超级应用** 架构，Radish 是运行在浏览器中的操作系统：
  - 单体应用 `radish.client`，不分离前后台
  - 桌面系统（Desktop Shell）：状态栏、图标网格、Dock、窗口管理器
  - 所有功能作为桌面应用：论坛、聊天室、商城、后台管理、API 文档
  - 应用注册表（AppRegistry）：统一管理应用元信息（类型、权限、图标）
  - 权限控制：根据用户角色动态显示/隐藏应用图标
  - 窗口类型：window（可拖拽）、fullscreen（全屏）、iframe（嵌入外部）
  - 后台管理作为 `apps/admin/` 子应用，全屏模式运行，使用 Ant Design 组件
- **arch(gateway)**: 决定 Gateway 暂缓，理由：
  - 当前后端服务仅 Auth + Api，复杂度不高
  - OIDC 已解决认证统一问题
  - 待服务数量增加或需要 BFF 聚合时再引入
- **docs**: 更新 DevelopmentPlan.md M3/M4 任务，添加客户端管理 API 和后台应用管理界面
- **docs(frontend)**: 完全重写 `FrontendDesign.md` 为 WebOS/超级应用架构文档：
  - 详细描述桌面系统、应用注册表、窗口管理器、子应用开发规范
  - 包含完整代码示例：AppRegistry、WindowManager、权限控制
  - 移动端适配策略、技术栈、性能优化、迭代计划
- **docs(plan)**: 调整 `DevelopmentPlan.md` M4 任务为 WebOS 架构：
  - 去掉前后台分离的 Monorepo 结构
  - 改为单体应用 + 桌面系统 + 子应用架构
  - 后台管理作为 `apps/admin/` 子应用，全屏模式
- **docs(open-platform)**: 更新 `OpenPlatformGuide.md` 应用矩阵：
  - 去掉 `radish-admin` 独立客户端
  - `radish-client` 包含所有子应用（论坛/聊天/商城/后台管理）
  - 调整系统架构图反映 WebOS 结构

## 第二阶段

### 阶段总结

- **后端框架落地**：完成 `Radish.Api` 最小宿主与分层架构（Common/Core/Extension/Service/Repository/I* 项目）搭建，Autofac 容器、ConfigurableOptions、`AppSettingsTool`、`App.ConfigureApplication()` 四步绑定、SqlSugarScope、Snowflake ID、Serilog、Redis/内存缓存、泛型仓储与服务、AutoMapper、JWT + PermissionRequirement 等核心能力全部串联，可在开发/生产配置文件中无缝切换并支持多租户、多数据库、日志分流。
- **基础设施增强**：实现 SqlSugar 多库与日志库分离、AOP SQL 日志、`LogContextTool`/`SerilogSetup` 的统一输出策略、`SqlSugarCache`/Redis 缓存、`ServiceAop` 拦截、`TenantController` 的字段/表/库隔离示例、`WeatherForecast`/`RustTest` 的 DI/原生能力验证，确保关键横切关注点（配置、日志、安全、缓存、租户、测试样例）在进入业务阶段前就位。
- **模型与权限体系**：补齐 `ApiModule`、`RoleModulePermission`、`UserRole` 等实体与视图模型，`PermissionExtension` + `PermissionRequirementHandler` 动态拼装 `RadishAuthPolicy`，`IBaseRepository/IBaseService` 支持 Snowflake ID 返回、可空 Where、三表联查等增强，为 RBAC 与业务表 CRUD 提供统一基座。
- **文档与前端计划**：同步更新 DevelopmentFramework/Specifications/AuthenticationGuide/FrontendDesign 等文档，记录配置加载、日志策略、SqlSugar 多库、前端桌面化与标准化组件规划，为第三阶段（领域建模、应用服务、桌面化前端组件、DevOps 补强）提供一致的事实来源与验证 checklist。

### 2025.11.23

- feat(log): 引入 `Radish.Extension.SerilogExtension`（`SerilogSetup` + `LogConfigExtension`），Program 通过 `builder.Host.AddSerilogSetup()` 安装 Serilog，统一输出到控制台与 `Log/` 目录，并用 `LogContextTool.LogSource` 区分普通日志与 SqlSugar AOP 日志；日志落盘使用 `WriteTo.Async()` 防止阻塞请求线程，同时在 `Log/SerilogDebug` 下收集 SelfLog。
- chore(common): `LogContextTool` 重命名并扩展 `SerilogDebug` 常量，供 Serilog 内部日志复用；`SqlSugarAop` 不再直接 `Console.WriteLine`，统一交给 Serilog 输出。
- chore(api/weather): `WeatherForecastController` 更新为注入 `ILogger<WeatherForecastController>`，演示同时使用 `_logger` 与 `Serilog.Log` 输出示例日志，避免 Autofac 无法解析非泛型 `ILogger` 的问题。

### 2025.11.22

- feat(config): `Program` 的 Host 配置阶段在清空默认配置源后同时加载 `appsettings.json` 与 `appsettings.{Environment}.json`，并在 SqlSugar 注册后读取 `Snowflake.WorkId/DataCenterId` 设置 `SnowFlakeSingle`，确保多实例按环境文件划分唯一 WorkId 且公共默认值仍写在基础文件兜底。
- docs(config/snowflake): DevelopmentFramework 与 DevelopmentSpecifications 说明新增环境配置加载顺序与 `Snowflake` 配置段的使用方式，明确所有服务器需在各自的环境文件里维护不同的 WorkId/DatacenterId，避免雪花 ID 冲突。

### 2025.11.21

- chore(host/startup): Program.cs 在执行 `app.Run()` 前输出 “Radish  --by luobo” ASCII 标识，方便在控制台明确当前运行实例与版本信息。
- docs(spec): 补充 `ApiModule.LinkUrl` 的正则写法与 `/` 前缀要求，开发规范强调包含路径参数的 API 必须在表中登记正则化 URL，避免授权策略匹配遗漏。

### 2025.11.20

- feat(infra): 新增 `Radish.Infrastructure` 项目沉淀 SqlSugar/租户相关的基础设施（`Tenant.RepositorySetting`、`TenantUtil` 等），由 Extension/Repository 统一引用，避免跨层循环依赖并集中维护多租户路由逻辑。
- feat(tenant): `TenantController` 与 `Radish.Api.http` 补充字段隔离、分表隔离与分库隔离示例接口，模型层新增 `BusinessTable`、`MultiBusinessTable`、`SubLibBusinessTable` 及其 Vo，Repository 通过 `TenantUtil.GetConnectionConfig()` 自动切换租户库。
- feat(automapper): `CustomProfile` 注册 Business/MultiBusiness/SubLibBusiness 的 Vo 映射，保障多租户示例无需手写转换即可返回 DTO。
- docs(auth): `AuthenticationGuide.md` 汇总当前 JWT 发行、授权策略与 PermissionRequirementHandler 流程，文档化登录接口的 Claim 组合与调试建议，方便新成员快速理解鉴权链路。

### 2025.11.19

- feat(auth): 将鉴权类型拆分到 `PermissionExtension`，新增 `PermissionItem` 与 `PermissionRequirementHandler`，运行时按“角色-API”关系动态组装 `RadishAuthPolicy`，并在 `RoleController`、`UserController` 等控制器上启用策略。
- feat(api/user): `UserController` 改为统一返回 `MessageModel`，新增 `GetUserById` 示例，`Radish.Api.http`/`http-client.env.json` 补充用户接口示例与最新 JWT，便于本地调试。
- feat(service/repo): `IBaseRepository`/`IBaseService` 增加 `QueryMuchAsync` 三表联查封装，`UserService.RoleModuleMaps()` 直接基于 SqlSugar Join 构建 `RoleModulePermission`，为权限处理器提供实时数据。
- chore(config): 轮换 JWT 密钥，`JwtTokenGenerate` 与 `Program` 保持一致；`appsettings.json` 新增 `AppSettings.UseLoadTest`，供鉴权测试时跳过登录校验；`PermissionRequirement` 仅保留配置，授权逻辑迁至 handler。
- test(api): `LoginControllerTest` 适配新的命名空间与假数据，实现 `RoleModuleMaps`/`QueryMuchAsync` 桩方法，继续验证登录流程。

### 2025.11.18

- feat(model/auth): 新增 `ApiModule`、`RoleModulePermission`、`UserRole` 实体与 `UserRoleVo`/`TokenInfoVo` 视图模型，补完 API 模块、角色-权限-按钮及用户-角色关联建模，支撑下一步的 RBAC 鉴权。
- refactor(model/user): User/MessageModel/RoleVo/UserVo 默认值与字段全面校准（含 Uuid、Vo* 命名、性别/年龄/状态默认值、消息默认提示等），保证接口契约在登录与下游消费场景下更一致。
- refactor(service/repo): `IBaseRepository/IBaseService` 的 `Query*` 支持可空 `Expression<Func<...>>`，Repository/Service 的实现亦同步泛化，`UserService` 的种子数据示例也调整为正式命名。
- feat(api/automapper): `LoginController` 标记为标准 API 控制器并注入 `IUserService`，AutoMapper Profile 为 User/Role/UserRole 映射补齐前缀识别与字段对应，便于后续直接复用 DTO。

### 2025.11.17

- feat(repo/db): `BaseRepository` 改由 `SqlSugarScope` + `IUnitOfWorkManage` 承载数据库实例，并读取实体上的 `[Tenant(configId)]` 特性动态切换连接，默认走主库，`AuditSqlLog` 等标注了 `configId="Log"` 的实体自动写入日志库。
- feat(api/tests): WeatherForecast 控制器及其 xUnit 示例同步验证多种依赖解析方式，同时注入 `IBaseService<AuditSqlLog, AuditSqlLogVo>` 以演示日志库的查询链路，便于之后参考缓存、属性注入与多库访问的组合用法。
- feat(api): WeatherForecastController 新增 `[HttpGet("{id}")]` 的 `GetById` 示例，可通过 `api/WeatherForecast/GetById/1` 直接验证路径参数绑定，后续 Controller 添加 REST 风格路由时可参照该写法。

### 2025.11.16

- feat(log): 新增 `Radish.Model.LogModels.BaseLog` 与 `AuditSqlLog`，默认挂载 `Tenant(configId: "log")` 并按月分表落库，同时提供 `AuditSqlLogVo` 作为对外视图模型，保证查询日志时依旧走 DTO。
- fix(automapper): `AutoMapperConfig` 拆分 `RegisterCustomProfile()`，`AutoMapperSetup` 在构建配置时先注册自定义 profile，再集中挂载 `Role/User/AuditSqlLog` 映射；`AuditSqlLogProfile` 中启用了 `RecognizePrefixes/RecognizeDestinationPrefixes("Vo")`，作为首个 Vo 前缀双向映射样例。
- docs(todo): 启动阶段待接入 `AssertConfigurationIsValid()` 以确保所有 profile 均通过 AutoMapper 15 的配置校验，避免运行期才发现字段缺失。

### 2025.11.15

- feat(db): `Radish.Common.DbTool` + `Radish.Extension.SqlSugarExtension` 接入 SqlSugarScope，`BaseDbConfig` 统一读取 `MainDb` 与 `Databases` 列表，默认提供 `Main`（业务库）与 `Log`（日志库）两个 SQLite 示例，并在 Program 中以 `AddSqlSugarSetup()` 自动注入多库配置。
- feat(log/cache): 新增 `SqlSugarCache`、`SqlSugarAop` 与 `LogContextHelper`，将 SqlSugar 内置缓存委托至 `ICaching` 并把 SQL 日志推送到 Serilog，上下文可通过 `LogSource=AopSql` 快速检索。
- feat(model): `Radish.Model.RootEntityTKey<TKey>` 统一约束实体主键，`Role` 等实体继承该基类；`BaseRepository`/`BaseService` 暴露 `ISqlSugarClient` 实例以便服务层调试 Hash，对应接口同步更新。

### 2025.11.14

- feat(core/native): 在 `Radish.Core/test_lib` 引入首个 Rust `cdylib` 示例，通过 `cargo build --release` 输出 `test_lib`，封装累加、斐波那契模拟、埃拉托斯特尼筛及并行质数计算，配合 Rayon/num_cpus 快速验证 CPU 密集算法表现。
- feat(api): 新建 `RustTest` 控制器，提供 `/api/RustTest/TestSum{1-4}` 基准接口，对比 C# 与 Rust 的执行耗时，并统一以 `DllImport("test_lib")` 调用共享库。
- docs(native): 当前 Rust demo 仅用于验证流程，正式扩展需迁至解决方案根目录（如 `native/rust/*`），并通过脚本在构建后自动拷贝 DLL/SO/Dylib，避免继续把 Core 层当成原生模块的承载目录。

### 2025.11.13

- feat(cache): 引入 `Radish.Common.CacheTool` + `Radish.Extension.RedisExtension`，配置项默认启用 Redis，可在 `Redis.Enable` 为 false 时自动退回内存缓存；提供 `ICaching` 与 `IRedisBasketRepository` 两层 API，并在 Program 中统一调用 `AddCacheSetup()` 完成注入。
- refactor(ext): Extension 与 Common 的通用组件重新按功能拆分到 `*Tool`、`AutofacExtension`、`AutoMapperExtension` 等目录，便于后续独立引用；AppSetting/WeatherForecast 控制器与单测同步调整引用路径。
- feat(core): 新增 `Radish.Common.Core.App/InternalApp`，集中感知 Host/Configuration/RootServices，并提供 `GetService*` 与 `GetOptions*` 辅助方法，便于在非 DI 管道内按需解析服务或读取配置。
- chore(host): Program.cs 引入 `ConfigureApplication()` 四步绑定（Host 配置 → Builder → App → `UseApplicationSetup`），运行期即可维护 `App.IsRun` 状态并在停止时刷新 Serilog。
- feat(api): WeatherForecastController 演示 `IServiceScopeFactory` + `App.GetService` 多种解析方式，AppSettingController 也补充 `App.GetOptions<T>` 示例，方便验证配置绑定是否生效。
- docs(config): DevelopmentFramework/Specifications 同步说明新的 App 服务入口使用方式与注意事项，确保后续贡献者遵循统一模式。

### 2025.11.12

- feat(config): `AppSettings.App` 更名为 `AppSettings.RadishApp`，统一入口避免与系统方法混淆，并同步更新 AutoMapper 与示例代码。
- feat(options): 新增 `ConfigurableOptions` + `AllOptionRegister`，集中注册所有实现 `IConfigurableOptions` 的配置项，Redis 配置抽象为 `Radish.Common.Option.RedisOptions` 并通过 `IOptions<T>` 注入。
- feat(api): 新建 `AppSettingController` 演示三种读取配置的方式（`RadishApp`、`GetValue`、`IOptions`），同时为 `appsettings.json` 增补注释与默认前缀。

### 2025.11.10

- feat(aop): 引入 `ServiceAop` + `AopLogInfo`，结合 Autofac 动态代理为泛型服务开启接口拦截，输出统一的请求/响应日志。
- docs(spec): 在 DevelopmentSpecifications 中新增 “AOP 与日志” 章节，说明拦截器、日志模型及扩展方式。
- chore(di): `AutofacModuleRegister` 启用服务/仓储拦截，并确保与 AutoMapperSetup、扩展模块协同；后续若添加新拦截器按此模式接入。

### 2025.11.9

- refactor(di): `AutofacPropertyModuleReg` 改为由宿主传入 `Assembly`，避免引用 `Program` 造成循环依赖，同时扩展层负责注册 Service/Repository 泛型实现。
- chore(project): 调整 Radish.Service 与 Radish.Extension 的引用方向，确保 IoC/扩展层仅被宿主引用，其余业务项目不再依赖扩展层。
- docs(spec): 在 DevelopmentSpecifications 中说明新的分层依赖约束与模块传参约定，方便后续贡献者遵循。

### 2025.11.8

- 重新创建项目，完全舍弃之前的代码，包括 ABP 框架与 MongoDB
- 重新使用 .NET 10 + React + Entity Framework Core/SqlSugar + PostgreSQL 技术栈
- 重新设计项目架构与模块划分

## 第一阶段

> 以下内容为历史记录，保留 ABP + Angular + MongoDB 方案的决策脉络以供参考，但不再作为现行实现依据。

### 2025.10.29

- feat(openiddict): 同时允许 http/https 回调与登出回调（含静默刷新），前端切换协议无需改配置。
- docs(readme): 重写根 README，整合 docs 内容，补充快速开始、HTTPS/CORS/SSO 指南与常用脚本。
- docs: 合并 DevelopmentBoard 至 DevelopmentPlan，统一为“开发计划与看板”，并更新索引与前端文档引用。
- docs(framework): 在 DevelopmentFramework.md 顶部新增“功能期望与范围”（功能/非功能、里程碑、范围外）。
- docs(index): docs/README.md 索引项更名为“项目总体功能与需求（含范围与里程碑）”。
- docs(framework): 增补“非目标与边界（Non-Goals & Boundaries）”详细清单，明确暂不覆盖项与阶段边界。

### 2025.10.28

- feat(host/mobile): 移除 Hero 区小屏“更多”下拉，统一使用下方移动端功能面板。
- feat(host/mobile): 重构移动端功能面板为两列栅格；语言入口改为下拉；“登录”在未登录时独占整行以提高可见性。
- style(host/mobile): 暗色模式适配移动功能卡片（半透明毛玻璃、清晰边框与阴影），并增强下拉与描边按钮在暗色下的对比度；主题切换、我的账户、注销统一为描边样式。
- style(host/mobile): “卡片密度”从整行分段控件调整为“标签 + 三按钮”，最终并入为“一体式分段控件（含标签的不可点击段）”，与其他按钮视觉一致。
- fix(host/mobile): 解决 Razor 编译错误（string 与 LocalizedHtmlString 混用），语言按钮使用 `L["Language"].Value`。
- fix(host/mobile): 分段控件按钮被全局 100% 宽度规则挤压的问题，显式设置 `width:auto` 恢复等分显示。
- feat(host/mobile): 分段控件支持“当前密度”选中高亮，并与本地存储 `radish.density` 联动初始化与更新。

### 2025.10.27

- feat(host): 新增“收藏”分区，将已收藏的应用卡片集中展示；无收藏时自动隐藏分区。
- feat(host): 卡片工具区固定两行两列（2x2），避免在窄宽度时遮挡标题/链接。
- feat(host): 最近访问仅保留最新 2 条，减少信息噪音。
- style(host): 缩小应用/收藏网格的纵向间距（--bs-gutter-y=1rem），两排卡片更紧凑。
- fix(host): 解决全屏点击“密度”按钮时下拉被 Hero 的 overflow 裁剪，展开时允许溢出、收起后恢复。
- docs(host): 同步 Host 首页功能说明（新增“收藏”分区、最近仅 2 条、工具区 2x2、间距与下拉裁剪修复说明）。

### 2025.10.26

- feat(angular/mobile): 新增移动端底部导航（自动从路由构建）、BottomSheet 子菜单与“更多”分组；滚动隐藏/显示；加入“管理”虚拟组；语言入口归入“更多”；桌面端新增明暗主题切换工具；移动端列表卡片化与小屏模态全屏。
- fix(angular/mobile): 修复子菜单交互与导航可靠性、语言切换即时生效并设置 html[lang]、Toolbar 注入与按钮丢失、并发请求降噪、分组检测等问题。
- chore(dev): 本地联调默认策略统一为“Host 以 HTTPS 提供 API(44342)，前端默认 HTTP(4200/5173)”，CORS 建议双协议且 Host 自动补全；为避免 44342 冲突，取消同端口双协议监听。配套文档与示例环境更新。
- docs(backend/frontend/SSO): 同步本地联调与证书信任说明、CORS/Redirect 示例、预检自测命令等；根 README_CN/EN 增补策略说明。
- fix(host,angular): 切换字体资源到国内镜像并增加全局字体回退，修复移动端叠层问题。
- feat(host): 重构 Host 首页（Radish Landing）：Hero 渐变与轻动效、响应式网格卡片、浅/深主题与持久化、移动端功能面板、最近访问、密度切换（含自动）、健康检查按钮、拖拽排序编辑、隐藏与显隐管理、区块标题与 I18N。
- fix(host): 修正 Hero full‑bleed/边缘过渡与容器布局细节；优化移动端操作区排布与卡片工具栏遮挡。
- docs(host): 新增 Host 首页功能说明文档（docs/Host-Home-Features.md），并在 docs/README.md 加入链接，完善导航。

### 2025.10.25

- feat(auth): Angular 端为主要路由加 authGuard，未登录访问自动跳转登录；保留 account 路由匿名访问，避免循环重定向。
- fix(host): 首页 Swagger/Scalar 卡片点击仅“原地刷新”——按应用 ClientId 自动补全 /swagger、/scalar。
- feat(openiddict): 数据种子将 Swagger/Scalar 的 ClientUri 初始化到具体文档页（/swagger、/scalar），并在迁移时同步更新已有记录；保留正确的 redirectUri（/swagger/oauth2-redirect.html、/scalar/...）。
- feat(backend): 启动时打印 CORS 允许来源；通过 .env 的 App__CorsOrigins 管理跨域来源，.env.example 预置本地来源示例（5173/4200）。
- refactor(config): 注释弃用 appsettings.json 的 App:CorsOrigins，统一改为 .env 的 App__CorsOrigins 管理。
- docs(backend/frontend/SSO): 同步统一 HTTPS 与 CORS 方案，完善 dev-certs 脚本与启动日志确认要点；提供 Host/DbMigrator 的 .env.example；为 React/Angular 指南补充配置要点。
- chore(dev): 统一本地 HTTPS 证书目录与引用；为 Angular/React 的 serve 配置证书。

### 2025.10.24

- feat(config): 按环境加载 .env，抽离敏感项并提供示例文件（Host/DbMigrator 支持 .env.development 与 .env.product，忽略本地 .env.development）。
- fix(swagger): 未登录访问 /swagger 时使用 Cookie 挑战，避免回调 HTML 导致 YAML 解析错误；允许已登录会话访问 Swagger，修正 Swagger/Scalar 客户端 RootUrl。
- docs(swagger): 保留 OIDC 所需的 /connect 路径并隐藏不必要的 ABP Schemas，对文档仅保留项目 API，隐藏基础设施端点。
- chore(frontend): CI 严格安装策略与贡献者设置收尾。

### 2025.10.23

- 导航/搜索：顶部导航中部改为搜索框，移动端抽屉提供搜索输入。
- Dock 底部栏：新增玻璃态半透明栏，宽度自适应，窄屏逐级隐藏；向下滚动隐藏、停止/向上滚动显示，提供返回顶部按钮。
- 交互/动效：桌面端支持 Dock 鱼眼放大动效；图标悬浮提示气泡、通知角标示例、右键/长按菜单。
- 布局/样式：主内容区根据 Dock 实际高度自适应底部留白；glass 效果与高光描边、投影；通过 ResizeObserver 写入 `--dock-inset-bottom` 并用于 body padding。
- 实现细节：useScrollDirection 阈值 4px、最小触发高度 80px、空闲显示延迟 260ms；仅 hover 设备通过 `matchMedia('(hover: hover)')` 启用动效；鱼眼放大使用高斯影响（最大缩放约 1.65，半径约 120px）。
- 影响范围：NavBar、BottomBar、DockToggle、StickyStack、全局样式 `index.css`；i18n 新增 `dock.*` 与 `menu.*` 文案。
- 今日提交：无（以上为工作区变更摘要）。

### 2025.10.20

refactor: 将react项目从js重构为ts

### 2025.10.8

feat: 暂时完成了 API 手动和自动控制器的版本控制

* feat: 完成了 Swagger 的多版本配置（鉴权认证还没做）
* feat: 完成了 Scalar 的多版本配置（鉴权认证还没做，而且还读取的是 `/swagger/xx/swagger.json` ）
* 目前主要是 ABP 框架的不兼容导致的，后面再研究怎么手动来实现：
  * 鉴权和 OIDC 认证
  * API 文档的注释
  * API 文档和 Schme 的过滤显示

### 2025.10.6

feat: 增加了 HttpApi.Host 项目首页的项目展示

### 2025.10.5

feat: 增加了 Scalar 配置

### 2025.10.4

docs: 完成了项目目录架构的初版

### 2025.9.29

build: 在 ABP Studio 中增加了 React 项目的启动脚本

### 2025.9.28

docs: 写了开发大纲

### 2025.9.27

feat: 决定使用 Radish 项目名称，创建 ABP 项目
### 2025.11.25

#### feat: 完善 Scalar OpenAPI 文档配置

- **创建 OpenAPI 扩展模块**: 新建 `Radish.Extension/OpenApiExtension/ScalarSetup.cs`，统一管理 OpenAPI 和 Scalar 配置
- **简化 Program.cs**: 通过 `AddScalarSetup()` 和 `UseScalarUI()` 扩展方法简化配置代码
- **完善 MessageModel 文档**: 为 `MessageModel<T>` 添加完整的 XML 注释、使用示例和业务场景说明
- **增强 Controller 文档**: 
  - LoginController: 添加详细的登录流程、请求/响应示例
  - UserController: 为所有方法添加业务场景和参数说明
  - 使用 `[Tags]` 特性进行 API 分组（"认证管理"、"用户管理"）
  - 使用 `[ProducesResponseType]` 明确声明所有可能的响应状态码
- **JWT 认证文档化**: 在 OpenAPI 文档中添加 JWT Bearer 认证说明和使用指南
- **项目配置优化**: 
  - 为 `Radish.Model` 和 `Radish.Api` 启用 XML 文档生成
  - 配置多版本 API 文档支持
- **Scalar UI 优化**: 使用 BluePlanet 主题、强制暗色模式、启用操作 ID 显示、展开所有标签、按字母排序
- **Bug 修复**: 修复 SerilogDebug 日志目录不存在导致的启动异常

#### chore: 配置调试启动时自动打开 API 文档

- 修改 `launchSettings.json`，为所有 profile 启用 `launchBrowser`
- 默认打开 `/api/docs` (Scalar 文档页面)
- Visual Studio 中按 F5 启动调试时，浏览器会自动打开并导航到 API 文档
- 关闭浏览器窗口时，Visual Studio 会自动停止调试会话

#### feat: 实现基于 URL 路径的 API 版本控制

- **版本控制核心配置**: 
  - 安装 `Asp.Versioning.Mvc` 和 `Asp.Versioning.Mvc.ApiExplorer` (8.1.0)
  - 配置 URL 路径版本控制，格式为 `/api/v{version}/[controller]/[action]`
  - 设置默认版本为 1.0，未指定版本时自动使用默认版本
  - 启用 API 版本报告和自动版本替换

- **版本划分**: 
  - **v1 (稳定版本)**: LoginController、UserController
  - **v2 (预览版本)**: AppSettingController、RustTestController

- **Controllers 更新**: 
  - 添加 `[ApiVersion("x.0")]` 特性声明版本
  - 路由模板更新为 `api/v{version:apiVersion}/[controller]/[action]`
  - 完善 XML 注释和响应类型声明
  - 为 v2 Controller 添加 Tags 分类（"系统管理"、"性能测试"）

- **OpenAPI 动态文档生成**: 
  - 使用 `IApiVersionDescriptionProvider` 自动发现所有 API 版本
  - 为每个版本动态创建独立的 OpenAPI 文档
  - 每个版本文档包含专属的描述、示例和接口列表
  - 支持版本弃用标记和警告提示

- **Scalar UI 版本切换**: 
  - 自动生成版本选择下拉菜单
  - v1 文档只显示 v1 接口，v2 文档只显示 v2 接口
  - 按版本号升序排列，首个版本默认选中
  - 显示版本状态（如 "V1 (1.0)"、"V2 (2.0)"）

- **文档隔离**: 
  - v1 文档描述: 包含认证管理、用户管理接口，当前稳定版本
  - v2 文档描述: 包含系统管理、性能测试接口，新功能预览版本
  - 每个版本包含独立的 URL 格式说明和迁移指南

#### 技术要点

- **URL 格式**: 
  - v1: `/api/v1/Login/GetJwtToken`, `/api/v1/User/GetUserList`
  - v2: `/api/v2/AppSetting/GetRedisConfig`, `/api/v2/RustTest/TestSum1`

- **特色功能**: 
  - ✅ URL 路径版本控制（业界标准）
  - ✅ 文档自动隔离（按版本过滤接口）
  - ✅ 默认版本支持
  - ✅ 版本报告（响应头包含支持的版本信息）
  - ✅ 弃用支持（可标记和提示已弃用版本）
  - ✅ 完全向后兼容

- **配置文件更新**: 
  - Radish.Api.csproj: 添加 Asp.Versioning.* 包引用、启用 XML 文档生成
  - Radish.Extension.csproj: 添加 ApiExplorer 包支持、添加 OpenAPI 相关包
  - Radish.Model.csproj: 启用 XML 文档生成

#### Git 提交记录

- `df6ca3d` - feat: 完善 Scalar OpenAPI 文档配置
- `9d3ca4a` - chore: 配置调试启动时自动打开 API 文档
- `9f3085b` - feat: 实现基于 URL 路径的 API 版本控制

