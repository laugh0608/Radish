# 鉴权与授权指南

Radish 采用 **OIDC（OpenID Connect）** 架构实现统一身份认证，基于 **OpenIddict** 构建认证服务器。本文档梳理整体架构、配置要点与扩展方式。

> **相关文档**：本文档专注于认证的技术实现细节。关于密码安全与用户密码存储策略，请参阅 [PasswordSecurity.md](PasswordSecurity.md)。关于 Gateway 架构下的统一认证规划（Phase 2），请参阅 [GatewayPlan.md](GatewayPlan.md#phase-2-认证集成)。

## 1. 架构概览

### 1.1 系统拓扑

```
┌─────────────────────────────────────────────────────────┐
│                  Radish.Auth (OIDC Server)              │
│  端口: http://localhost:5200 （本地开发）               │
│  ─────────────────────────────────────────────────────  │
│  • 用户管理（注册/登录/密码重置）                         │
│  • 角色与权限管理                                        │
│  • 租户管理（多租户支持）                                 │
│  • Token 签发/刷新/撤销                                  │
│  • OIDC 端点                                            │
└──────────────┬──────────────────────────────────────────┘
               │
    ┌──────────┴──────────┬────────────┬───────────┐
    ▼                     ▼            ▼           ▼
┌─────────┐        ┌──────────┐   ┌────────┐  ┌─────────┐
│Radish.Api│       │radish.   │   │ Scalar │  │ Admin   │
│:5100     │       │client    │   │API Docs│  │Dashboard│
│(资源服务器)│       │:3000    │   │        │  │(预留)    │
└─────────┘        └──────────┘   └────────┘  └─────────┘
```

### 1.2 核心优势

- **单点登录（SSO）**：一次登录可访问所有已注册的客户端应用
- **标准化协议**：遵循 OAuth 2.0 / OIDC 规范，便于集成第三方身份提供商
- **安全性**：支持 PKCE、Refresh Token 轮换、Token 撤销
- **可扩展性**：认证服务可独立水平扩展

## 2. OIDC 端点

Radish.Auth 提供以下标准 OIDC 端点：

| 端点 | 路径 | 说明 |
| --- | --- | --- |
| 发现文档 | `/.well-known/openid-configuration` | OIDC 配置元数据 |
| 授权 | `/connect/authorize` | 用户授权入口（浏览器重定向） |
| Token | `/connect/token` | 获取/刷新 Access Token |
| 用户信息 | `/connect/userinfo` | 获取当前用户信息 |
| 登出 | `/connect/logout` | 结束会话 |
| 撤销 | `/connect/revoke` | 撤销 Token |
| 内省 | `/connect/introspect` | Token 验证（资源服务器使用） |

## 3. 授权类型

### 3.1 Authorization Code + PKCE（推荐）

适用于前端 SPA、移动 App 等公开客户端。

```
┌────────┐                              ┌────────────┐                    ┌──────────┐
│ Client │                              │ Auth Server│                    │   API    │
└───┬────┘                              └─────┬──────┘                    └────┬─────┘
    │                                         │                                │
    │ 1. 生成 code_verifier + code_challenge  │                                │
    │ ──────────────────────────────────────> │                                │
    │ 2. 重定向到 /connect/authorize          │                                │
    │                                         │                                │
    │ 3. 用户登录并授权                        │                                │
    │ <────────────────────────────────────── │                                │
    │ 4. 返回 authorization_code              │                                │
    │                                         │                                │
    │ 5. POST /connect/token                  │                                │
    │    (code + code_verifier)               │                                │
    │ ──────────────────────────────────────> │                                │
    │ 6. 返回 access_token + refresh_token    │                                │
    │ <────────────────────────────────────── │                                │
    │                                         │                                │
    │ 7. 调用 API (Bearer token)              │                                │
    │ ────────────────────────────────────────────────────────────────────────>│
    │ 8. 返回资源                              │                                │
    │ <────────────────────────────────────────────────────────────────────────│
```

### 3.2 Client Credentials

适用于服务间调用（无用户参与）。

```csharp
// 请求示例
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=radish-background-service
&client_secret=xxx
&scope=radish-api
```

### 3.3 Refresh Token

用于无感续期 Access Token。

```csharp
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token
&client_id=radish-client
&refresh_token=xxx
```

## 4. 客户端注册

所有客户端需在 Auth Server 中注册。通过 DbSeed 初始化：

### 4.1 Scalar API 文档

```csharp
new OpenIddictApplicationDescriptor
{
    ClientId = "radish-scalar",
    DisplayName = "Radish API Documentation",
    ConsentType = ConsentTypes.Explicit, // 显示授权确认页（用于测试）
    RedirectUris = {
        new Uri("https://localhost:5000/scalar/oauth2-callback")
    },
    Permissions =
    {
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.ResponseTypes.Code,
        Permissions.Prefixes.Scope + "openid",
        Permissions.Prefixes.Scope + "profile",
        Permissions.Prefixes.Scope + "radish-api"
    }
}
```

**使用方式**：
1. 访问 `https://localhost:5000/scalar`（通过 Gateway）
2. 点击右上角 **Authenticate** 按钮
3. 选择 **oauth2** 认证方式，点击 **Authorize**
4. 使用测试账号登录（用户名：`test`，密码：`P@ssw0rd!`）
5. 确认授权后，所有 API 请求将自动携带 Bearer Token

### 4.2 前端 Web 客户端

```csharp
new OpenIddictApplicationDescriptor
{
    ClientId = "radish-client",
    DisplayName = "Radish Web Client",
    ConsentType = ConsentTypes.Explicit, // 显示授权确认页
    RedirectUris = {
        new Uri("http://localhost:3000/callback"),
        new Uri("http://localhost:3000/silent-renew")
    },
    PostLogoutRedirectUris = {
        new Uri("http://localhost:3000")
    },
    Permissions =
    {
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.Endpoints.Logout,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.OpenId,
        Permissions.Scopes.Profile,
        Permissions.Scopes.OfflineAccess, // 启用 refresh_token
        Permissions.Prefixes.Scope + "radish-api"
    },
    Requirements =
    {
        Requirements.Features.ProofKeyForCodeExchange // 强制 PKCE
    }
}
```

### 4.3 后台服务（Client Credentials）

```csharp
new OpenIddictApplicationDescriptor
{
    ClientId = "radish-background-service",
    ClientSecret = "generated-secret", // 生产环境使用强密钥
    DisplayName = "Radish Background Service",
    Permissions =
    {
        Permissions.Endpoints.Token,
        Permissions.GrantTypes.ClientCredentials,
        Permissions.Prefixes.Scope + "radish-api"
    }
}
```

### 4.4 客户端动态管理

客户端存储在 OpenIddict 数据库中，支持通过 API 动态管理。

#### 数据存储

客户端数据使用 **OpenIddict 的 Properties 字段**（`ImmutableDictionary<string, JsonElement>`）存储扩展信息：

**系统字段**（用于客户端管理）：
- **IsDeleted**：软删除标记（`"true"` / `"false"`）
- **CreatedAt** / **CreatedBy**：创建时间和创建者 ID
- **UpdatedAt** / **UpdatedBy**：更新时间和更新者 ID
- **DeletedAt** / **DeletedBy**：删除时间和删除者 ID

**展示字段**（用于登录页面和客户端信息展示）：
- **description**：客户端描述信息，如"Radish 社区平台前端应用"
- **developerName**：开发者/团队名称，如"Radish Team"
- **logo**：客户端图标 URL（可选）

#### 客户端扩展属性配置

在客户端初始化时，可通过 `OpenIddictApplicationDescriptor.Properties` 设置扩展属性：

```csharp
var descriptor = new OpenIddictApplicationDescriptor
{
    ClientId = "radish-client",
    DisplayName = "Radish Web Client",
    ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
    // ... 其他配置 ...
};

// 添加扩展属性（存储为 JSON 元素）
descriptor.Properties["description"] = JsonSerializer.SerializeToElement("Radish 社区平台前端应用");
descriptor.Properties["developerName"] = JsonSerializer.SerializeToElement("Radish Team");
descriptor.Properties["logo"] = JsonSerializer.SerializeToElement("https://example.com/logo.png");

await applicationManager.CreateAsync(descriptor);
```

**登录页面展示**：

当用户访问登录页时，Auth Server 会从 `ReturnUrl` 中提取 `client_id` 参数，查询客户端信息并展示：
- 客户端图标（logo）或首字母缩写
- 客户端显示名称（DisplayName）
- 客户端 ID（ClientId）
- 客户端描述（description）
- 开发者名称（developerName）

如果无法解析 `client_id` 或客户端不存在，将显示"未知的客户端"。

**注意事项**：
- URL 解析时不应对整个 ReturnUrl 解码（避免 `redirect_uri` 参数干扰）
- 使用 `QueryHelpers.ParseQuery` 解析查询字符串（自动处理 URL 编码）
- 扩展属性通过 `IOpenIddictApplicationManager.GetPropertiesAsync` 获取

所有审计字段使用 ISO 8601 格式存储（`DateTime.UtcNow.ToString("O")`）。

#### 管理 API

客户端管理 API 位于 **Radish.Api** 项目（`/api/v1/Client/*`），需要 `System` 或 `Admin` 角色权限。

| 方法 | 端点 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/Client/GetClients` | 获取客户端列表（分页、搜索） | System/Admin |
| GET | `/api/v1/Client/GetClient/{id}` | 获取客户端详情 | System/Admin |
| POST | `/api/v1/Client/CreateClient` | 创建客户端 | System/Admin |
| PUT | `/api/v1/Client/UpdateClient/{id}` | 更新客户端 | System/Admin |
| DELETE | `/api/v1/Client/DeleteClient/{id}` | 删除客户端（软删除） | System/Admin |
| POST | `/api/v1/Client/ResetClientSecret/{id}` | 重置 Secret | System/Admin |

#### 创建客户端示例

```http
POST https://localhost:5000/api/v1/Client/CreateClient
Content-Type: application/json
Authorization: Bearer {admin_token}

{
  "clientId": "third-party-app",
  "displayName": "第三方游戏社区",
  "consentType": "Explicit",
  "requireClientSecret": true,
  "requirePkce": true,
  "redirectUris": [
    "https://game-community.example.com/callback"
  ],
  "postLogoutRedirectUris": [
    "https://game-community.example.com"
  ],
  "grantTypes": [
    "authorization_code",
    "refresh_token"
  ],
  "scopes": [
    "openid",
    "profile",
    "radish-api"
  ]
}
```

响应：

```json
{
  "success": true,
  "msg": "创建成功",
  "response": {
    "clientId": "third-party-app",
    "clientSecret": "vK8x2mP9nQ4wR7tY5uI3oL6aS1dF0gH8jK2lZ4xC9vB3nM7qW5eR1tY8uI6oP4aS",
    "message": "请妥善保管此密钥，关闭后将无法再次查看"
  }
}
```

> **重要提示**：
> - ClientSecret 仅在创建时返回一次，无法再次查看
> - 如果丢失密钥，需要使用 `/api/v1/Client/ResetClientSecret/{id}` 重置
> - 公开客户端（`requireClientSecret: false`）不需要密钥

## 5. 资源服务器配置

Radish.Api 作为资源服务器验证 Token（当前本地开发配置已经与 Radish.Auth 对接）：

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 本地开发：直接信任 Radish.Auth 的 OpenIddict Server
        options.Authority = "http://localhost:5200";
        // 生产环境部署到网关后，可切换为网关暴露的 https 地址
        // options.Authority = "https://your-gateway-domain";

        // 本地开发阶段先关闭 Audience 校验，等待后续统一约定
        // options.Audience = "radish-api";
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // 若生产环境统一配置 Issuer，可开启严格校验
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Client 策略：基于 scope=radish-api 控制访问资源服务器
    options.AddPolicy("Client", policy =>
        policy.RequireClaim("scope", "radish-api"));

    options.AddPolicy("System", policy =>
        policy.RequireRole("System"));

    options.AddPolicy("SystemOrAdmin", policy =>
        policy.RequireRole("System", "Admin"));

    // 动态权限策略
    options.AddPolicy("RadishAuthPolicy", policy =>
        policy.Requirements.Add(new PermissionRequirement()));
});
```

## 6. 前端集成

> 当前仓库中，为了先打通端到端调试链路，在 `radish.client/src/App.tsx` 中实现了一套极简 OIDC 流程：
> - 前端统一通过 Gateway 访问：`https://localhost:5000`；
> - 首页 `App` 组件提供“通过 OIDC 登录 / 退出登录”按钮，登录重定向到 `${apiBaseUrl}/connect/authorize`，回调地址为 `${window.location.origin}/oidc/callback`；
> - 回调页 `/oidc/callback` 调用 `${apiBaseUrl}/connect/token` 换取 Token，并将 `access_token/refresh_token` 持久化到浏览器；
> - 首页挂载时调用 `${apiBaseUrl}/api/v1/User/GetUserByHttpContext` 获取当前用户 `userId/userName/tenantId`，用以验证 Auth ↔ Api ↔ Db 的数据映射。
>
> 这套实现仅用于当前阶段的本地开发与调试，后续计划按本节 6.1–6.3 所述方式，引入 `oidc-client-ts` 或 `react-oidc-context` 等专用库接管 Token 生命周期与自动续期。

### 6.1 配置

使用 `oidc-client-ts` 或 `react-oidc-context`：

```typescript
// shared/auth/oidc-config.ts
import { UserManagerSettings } from 'oidc-client-ts';

export const oidcConfig: UserManagerSettings = {
  authority: 'https://localhost:7100',
  client_id: 'radish-client',
  redirect_uri: 'http://localhost:3000/callback',
  post_logout_redirect_uri: 'http://localhost:3000',
  silent_redirect_uri: 'http://localhost:3000/silent-renew',
  scope: 'openid profile radish-api offline_access',
  response_type: 'code',
  automaticSilentRenew: true,
  // PKCE 默认启用（生产环境请使用真实 HTTPS 域名）
};
```

### 6.2 认证流程

```typescript
// shared/auth/auth-provider.tsx
import { AuthProvider } from 'react-oidc-context';
import { oidcConfig } from './oidc-config';

export const AppAuthProvider = ({ children }) => (
  <AuthProvider {...oidcConfig}>
    {children}
  </AuthProvider>
);

// 使用
import { useAuth } from 'react-oidc-context';

const LoginButton = () => {
  const auth = useAuth();

  if (auth.isLoading) return <div>Loading...</div>;
  if (auth.error) return <div>Error: {auth.error.message}</div>;

  if (auth.isAuthenticated) {
    return (
      <div>
        Welcome, {auth.user?.profile.name}
        <button onClick={() => auth.signoutRedirect()}>Logout</button>
      </div>
    );
  }

  return <button onClick={() => auth.signinRedirect()}>Login</button>;
};
```

### 6.3 API 调用

```typescript
// shared/api/client.ts
import { useAuth } from 'react-oidc-context';

export const useApiClient = () => {
  const auth = useAuth();

  return async (url: string, options?: RequestInit) => {
    const headers = new Headers(options?.headers);

    if (auth.user?.access_token) {
      headers.set('Authorization', `Bearer ${auth.user.access_token}`);
    }

    return fetch(url, { ...options, headers });
  };
};
```

## 7. Scalar OAuth 配置

Scalar API 文档已集成 OIDC 认证，配置位于 `Radish.Extension/OpenApiExtension/ScalarSetup.cs`：

### 7.1 OpenAPI Security Scheme

```csharp
// 在 OpenAPI 文档中定义 OAuth2 Security Scheme
document.Components ??= new OpenApiComponents();
document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.OAuth2,
    Description = "通过 OIDC 认证服务器获取 Access Token",
    Flows = new OpenApiOAuthFlows
    {
        AuthorizationCode = new OpenApiOAuthFlow
        {
            AuthorizationUrl = new Uri("https://localhost:5000/connect/authorize"),
            TokenUrl = new Uri("https://localhost:5000/connect/token"),
            Scopes = new Dictionary<string, string>
            {
                ["openid"] = "OpenID Connect 身份认证",
                ["profile"] = "用户基本信息",
                ["radish-api"] = "Radish API 访问权限"
            }
        }
    }
};
```

### 7.2 Scalar UI 配置

```csharp
app.MapScalarApiReference(options =>
{
    options.WithTitle("Radish API Documentation")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.Node, ScalarClient.Axios)
        // 配置 OAuth2 认证
        .AddPreferredSecuritySchemes("oauth2")
        .AddOAuth2Flows("oauth2", flows =>
        {
            flows.AuthorizationCode = new AuthorizationCodeFlow
            {
                ClientId = "radish-scalar",
                RedirectUri = "https://localhost:5000/scalar/oauth2-callback"
            };
        })
        .AddDefaultScopes("oauth2", ["openid", "profile", "radish-api"]);
});
```

### 7.3 服务器配置

Scalar 支持多个服务器选项，方便在不同环境下测试：

```csharp
document.Servers.Clear();
document.Servers.Add(new()
{
    Url = "https://localhost:5000",
    Description = "本地开发环境 (Gateway HTTPS)"
});
document.Servers.Add(new()
{
    Url = "http://localhost:5001",
    Description = "本地开发环境 (Gateway HTTP)"
});
document.Servers.Add(new()
{
    Url = "http://localhost:5100",
    Description = "本地开发环境 (API 直连)"
});
```

### 7.4 Auth Server Scopes 注册

**重要**：Auth Server 必须注册允许的 Scopes，否则会报 `invalid_scope` 错误：

```csharp
// Radish.Auth/Program.cs
builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        // 注册允许的 Scopes
        options.RegisterScopes("openid", "profile", "offline_access", "radish-api");
        // ...
    });
```

## 8. Claim 与内部用户模型映射约定

访问令牌（Access Token）由 Radish.Auth 负责签发，Radish.Api 及领域层通过 `IHttpContextUser` 获取“当前用户视图”。
本节约定 Token 内各 Claim 与内部模型之间的对应关系和解析规则，避免各处自行约定字段含义。

### 8.1 核心 Claim 约定

| Claim 名称     | 说明                 | 对应实体字段           | 备注                                       |
|----------------|----------------------|------------------------|--------------------------------------------|
| `sub`          | 用户唯一标识         | `User.Id`              | OIDC 标准 Claim，推荐作为唯一用户 Id       |
| `name`         | 用户显示名           | `User.UserName` 等     | 显示用名称，可根据业务组合昵称             |
| `tenant_id`    | 租户 Id              | `User.TenantId`        | 多租户隔离核心字段                         |
| `TenantId`     | 租户 Id（兼容字段）  | `User.TenantId`        | 过渡期内与 `tenant_id` 同值，便于兼容旧代码 |
| `role`         | 角色名               | `Role.RoleName`        | 可多值，需与 `Role.RoleName` 完全一致      |
| `scope`        | 授权范围             | —                      | 如 `openid profile radish-api`             |
| `iss`/`aud`…   | 标准 Token 元数据    | —                      | 由 OpenIddict 负责                         |

> 兼容说明：历史版本的自建 JWT 仅将用户 Id 写入 `jti`、租户 Id 写入 `TenantId`；当前实现已经在旧登录接口中同时补充了 `sub/tenant_id` 等 OIDC 风格 Claim。
> Api 端通过 `IHttpContextUser` 同时兼容 `sub` 与 `jti`、`tenant_id` 与 `TenantId`，后续会逐步过渡到以 `sub/tenant_id` 为唯一来源。

### 8.2 Api 侧解析规则（IHttpContextUser）

Api 侧不直接到处解析 `ClaimsPrincipal`，而是统一通过 `IHttpContextUser` 暴露“当前用户视图”。
解析规则约定如下（伪代码，仅示意逻辑）：

```csharp
// UserId：优先使用 OIDC 标准的 sub，兼容旧版 jti
long userId =
    GetLongClaim("sub") ??
    GetLongClaim(JwtRegisteredClaimNames.Jti) ??
    0;

// TenantId：优先 tenant_id，其次 TenantId
long tenantId =
    GetLongClaim("tenant_id") ??
    GetLongClaim("TenantId") ??
    0;

// UserName：优先 OIDC Name，其次 ClaimTypes.Name / Identity.Name
string userName =
    User.FindFirst(OpenIddictConstants.Claims.Name)?.Value ??
    User.FindFirst(ClaimTypes.Name)?.Value ??
    User.Identity?.Name ??
    string.Empty;

// Roles：从 ClaimTypes.Role 和 "role" 汇总
var roles = User.FindAll(ClaimTypes.Role)
    .Select(c => c.Value)
    .Concat(User.FindAll("role").Select(c => c.Value))
    .Distinct()
    .ToList();

// Scopes：从 scope 拆分空格
var scopes = User.FindAll("scope")
    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Distinct()
    .ToList();
```

在此基础上，`IHttpContextUser` 对外暴露统一的“当前用户视图”（示意）：

- `UserId: long`
- `UserName: string`
- `TenantId: long`
- `Roles: IReadOnlyList<string>`
- `IsAuthenticated(): bool`
- `GetClaimsIdentity(): IEnumerable<Claim>`
- （可选）`GetUserInfoFromToken(string claimType)` 等

多租户、仓储、日志等基础设施应尽量依赖这一视图，而不是自己从 Claims 中取值。

### 8.3 与多租户 / 权限体系的关系

- **多租户（Tenant）**
  - Auth 在签发 Token 时必须写入正确的 `tenant_id`（以及兼容的 `TenantId`）；
  - Api 通过上述规则解析出 `TenantId`，`RepositorySetting`/`BaseRepository` 会基于该值：
    - 对实现 `ITenantEntity` 的实体增加 QueryFilter；
    - 根据租户切换数据库或分表后缀。

- **角色与权限（RBAC）**
  - Auth 需将用户角色写入 `role`/`ClaimTypes.Role`，值必须与 `Role.RoleName` 一致；
  - Api 侧授权策略：
    - `System` / `SystemOrAdmin` 通过角色 Claim 做静态角色判断；
    - `RadishAuthPolicy` 通过角色 Claim 与 `ApiModule.LinkUrl`（正则）构建的 `PermissionItem` 集合做 URL 级权限校验。

- **当前用户信息接口**
  - `UserController.GetUserByHttpContext` 等接口只依赖 `IHttpContextUser` 暴露的视图，而不关心 Token 的具体格式；
  - 旧 JWT 模式与新 OIDC 模式可以并行一段时间，只要遵循本节的 Claim 映射规则，调用方不需要感知差异。

> 总体原则：
> - Auth 统一负责 **Claims 的内容与命名**；
> - Api 统一通过 `IHttpContextUser` 解析 Claims 并向上提供"当前用户视图"；
> - 领域层及仓储层只依赖 `UserId`、`TenantId`、`Roles` 等抽象，不直接操作 Token。

### 8.4 基于 Scope 的动态 Claim Destination 分配

为了符合 OIDC 规范并优化 Token 大小，Radish.Auth 实现了基于请求 scope 的动态 claim destination 分配机制。

#### 8.4.1 设计原则

- **ID Token**：用于身份认证，包含用户身份信息，由客户端消费
- **Access Token**：用于资源访问授权，包含授权信息，由资源服务器消费
- **按需分配**：根据客户端请求的 scope 决定哪些 claim 应该包含在哪个 token 中

#### 8.4.2 Claim Destination 规则

| Claim 类型 | Access Token | ID Token | 条件 |
|-----------|--------------|----------|------|
| `sub` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `openid` scope |
| `name` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `openid` 或 `profile` scope |
| `email` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `email` scope |
| `role` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `profile` scope |
| `tenant_id` | ✅ 始终包含 | ❌ 不包含 | 业务相关，仅用于授权 |
| `preferred_username` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `profile` scope |
| `given_name` | ✅ 始终包含 | ✅ 条件包含 | 请求了 `profile` scope |
| 其他标准 OIDC claims | ✅ 始终包含 | ✅ 条件包含 | 请求了 `profile` scope |

#### 8.4.3 实现位置

**AuthorizationController.cs** 中的 `GetClaimDestinations` 方法：

```csharp
private static IEnumerable<string> GetClaimDestinations(Claim claim, ImmutableArray<string> scopes)
{
    // sub 是必需的标识符
    if (claim.Type == OpenIddictConstants.Claims.Subject)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
        if (scopes.Contains(OpenIddictConstants.Scopes.OpenId))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
        yield break;
    }

    // name claim
    if (claim.Type == OpenIddictConstants.Claims.Name)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
        if (scopes.Contains(OpenIddictConstants.Scopes.OpenId) ||
            scopes.Contains(OpenIddictConstants.Scopes.Profile))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
        yield break;
    }

    // email claim
    if (claim.Type == OpenIddictConstants.Claims.Email)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
        if (scopes.Contains(OpenIddictConstants.Scopes.Email))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
        yield break;
    }

    // role claim
    if (claim.Type == OpenIddictConstants.Claims.Role ||
        claim.Type == ClaimTypes.Role)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
        if (scopes.Contains(OpenIddictConstants.Scopes.Profile))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
        yield break;
    }

    // tenant_id: 仅用于授权
    if (claim.Type == "tenant_id" || claim.Type == "TenantId")
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
        yield break;
    }

    // 默认：其他所有 claims 只包含在 access_token 中
    yield return OpenIddictConstants.Destinations.AccessToken;
}
```

#### 8.4.4 登录时设置的 Claims

**AccountController.cs** 在用户登录时设置以下 claims：

```csharp
var claims = new List<Claim>
{
    // 标准身份标识
    new(ClaimTypes.NameIdentifier, userId),
    new(ClaimTypes.Name, username),

    // OIDC 标准 claims
    new(OpenIddictConstants.Claims.Subject, userId),
    new(OpenIddictConstants.Claims.Name, username),
    new(OpenIddictConstants.Claims.PreferredUsername, user.VoLoName),

    // 多租户标识
    new("tenant_id", tenantId)
};

// Email claim (如果存在)
if (!string.IsNullOrWhiteSpace(user.VoUsEmail))
{
    claims.Add(new Claim(ClaimTypes.Email, user.VoUsEmail));
    claims.Add(new Claim(OpenIddictConstants.Claims.Email, user.VoUsEmail));
}

// 真实姓名 (如果存在)
if (!string.IsNullOrWhiteSpace(user.VoReNa))
{
    claims.Add(new Claim(OpenIddictConstants.Claims.GivenName, user.VoReNa));
}

// 角色 claims
foreach (var role in roleNames)
{
    claims.Add(new Claim(ClaimTypes.Role, role));
    claims.Add(new Claim(OpenIddictConstants.Claims.Role, role));
}
```

#### 8.4.5 支持的 Scopes

Auth Server 注册的 scopes（`Radish.Auth/Program.cs`）：

```csharp
options.RegisterScopes("openid", "profile", "email", "offline_access", "radish-api");
```

- **openid**：启用 OIDC 身份认证，`sub` claim 会包含在 id_token 中
- **profile**：请求用户基本信息（name, preferred_username, given_name, role 等）
- **email**：请求用户邮箱信息
- **offline_access**：启用 refresh_token
- **radish-api**：访问 Radish API 的权限

#### 8.4.6 使用示例

**请求最小权限**（仅访问 API）：
```
scope=radish-api
```
- Access Token 包含：sub, name, tenant_id, role
- ID Token：不生成（未请求 openid）

**请求身份认证**：
```
scope=openid profile radish-api
```
- Access Token 包含：sub, name, preferred_username, tenant_id, role
- ID Token 包含：sub, name, preferred_username, role

**请求完整信息**：
```
scope=openid profile email radish-api offline_access
```
- Access Token 包含：sub, name, email, preferred_username, tenant_id, role
- ID Token 包含：sub, name, email, preferred_username, role
- 同时获得 refresh_token

#### 8.4.7 优势

1. **符合 OIDC 规范**：ID Token 和 Access Token 职责分离
2. **优化 Token 大小**：客户端只获取需要的信息
3. **提升安全性**：敏感信息（如 tenant_id）不会泄露到客户端
4. **灵活性**：支持不同客户端按需请求不同的用户信息

## 9. 多租户支持

### 9.1 租户识别

- 通过 Token 中的 `tenant_id` Claim 识别租户
- Auth Server 在签发 Token 时写入租户信息

### 9.2 租户隔离

```csharp
// 用户登录时写入租户 Claim
identity.SetClaim("tenant_id", user.TenantId.ToString());

// 资源服务器读取
var tenantId = User.FindFirstValue("tenant_id");
```

## 10. 权限与授权

### 10.1 角色-权限模型

延续第二阶段的 RBAC 模型：

```
User -> UserRole -> Role -> RolePermission -> Permission
                      ↓
                 RoleModuleMap -> ApiModule
```

### 10.2 PermissionRequirementHandler

保留动态权限校验逻辑，从 Token Claims 获取角色：

```csharp
// 从 OIDC Token 读取角色
var roles = context.User.FindAll(ClaimTypes.Role)
    .Select(c => c.Value)
    .ToList();

// 匹配 URL 与角色权限
var url = httpContext.Request.Path.Value;
var hasPermission = PermissionItems
    .Any(p => roles.Contains(p.Role) &&
              Regex.IsMatch(url, p.Url, RegexOptions.IgnoreCase));
```

## 11. 安全最佳实践

### 11.1 Token 安全

- Access Token 有效期：15-60 分钟
- Refresh Token 有效期：7-30 天
- 启用 Token 轮换：每次刷新生成新的 Refresh Token
- 支持 Token 撤销：用户登出时撤销所有 Token

### 11.2 客户端安全

- 公开客户端（SPA/Mobile）必须使用 PKCE
- 机密客户端使用强密钥，定期轮换
- 严格限制 RedirectUris，避免开放重定向攻击

### 11.3 传输安全

- 全程 HTTPS
- 启用 HSTS
- Cookie 设置 `Secure`、`HttpOnly`、`SameSite=Strict`

### 11.4 OIDC 证书管理

- 仓库根目录新增 `Certs/` 文件夹，默认提供一个仅供开发/联调使用的 `dev-auth-cert.pfx`，密码为 `RadishDevCert123!`（已加入 `.gitignore`，只保留示例证书）。
- `Radish.Auth/appsettings.json` 预置了以下配置，并由 `Program.cs` 自动加载证书（注意路径区分大小写，Linux 环境需保持 `Certs` 首字母大写）：

```json
"OpenIddict": {
  "Encryption": {
    "UseDevelopmentKeys": false,
    "SigningCertificatePath": "../Certs/dev-auth-cert.pfx",
    "SigningCertificatePassword": "RadishDevCert123!",
    "EncryptionCertificatePath": "../Certs/dev-auth-cert.pfx",
    "EncryptionCertificatePassword": "RadishDevCert123!"
  }
}
```

#### 证书生成示例

1. **生成签名证书（Signing）**：
   ```bash
   openssl genrsa -out auth-signing.key 4096
   openssl req -new -key auth-signing.key -out auth-signing.csr -subj "/CN=radish-auth-signing"
   openssl x509 -req -in auth-signing.csr -signkey auth-signing.key -days 365 -out auth-signing.crt
   openssl pkcs12 -export -out auth-signing.pfx -inkey auth-signing.key -in auth-signing.crt -password pass:ChangeMe!
   ```
2. **生成加密证书（Encryption，可与签名证书分离）**：
   ```bash
   openssl genrsa -out auth-encryption.key 4096
   openssl req -new -key auth-encryption.key -out auth-encryption.csr -subj "/CN=radish-auth-encryption"
   openssl x509 -req -in auth-encryption.csr -signkey auth-encryption.key -days 365 -out auth-encryption.crt
   openssl pkcs12 -export -out auth-encryption.pfx -inkey auth-encryption.key -in auth-encryption.crt -password pass:ChangeMeToo!
   ```
3. **安全处理明文文件**：生成 `.pfx` 后，立即销毁 `.key/.csr/.crt` 明文文件或转存到安全密钥库，仅保留 `.pfx` 与随机密码。
4. **验证内容**：
   ```bash
   openssl pkcs12 -info -in auth-signing.pfx -nokeys
   ```
   确保 PFX 中包含私钥且未过期。

> 可以使用同一个 `.pfx` 兼作签名与加密，但生产环境推荐拆分，以便后续按不同生命周期轮换。

Auth 启动时会在 `Program.cs` 中调用 `LoadOpenIddictCertificate`，相对路径会基于 `builder.Environment.ContentRootPath` 解析成绝对路径，因此在生产环境只需确保证书被挂载到容器内并设置正确的 `OpenIddict__Encryption__*` 环境变量即可。

#### 部署与覆盖配置

1. **放置证书**：将 `auth-signing.pfx`、`auth-encryption.pfx` 拷贝到宿主机安全目录（例如 `/etc/radish/certs/`），以 `600` 权限挂载到容器（`/app/certs`）。
2. **覆盖配置**：在 `appsettings.Production.json` 或环境变量中设置：
   ```
   OpenIddict__Encryption__UseDevelopmentKeys=false
   OpenIddict__Encryption__SigningCertificatePath=/app/certs/auth-signing.pfx
   OpenIddict__Encryption__SigningCertificatePassword=<生产密码>
   OpenIddict__Encryption__EncryptionCertificatePath=/app/certs/auth-encryption.pfx
   OpenIddict__Encryption__EncryptionCertificatePassword=<生产密码>
   ```
3. **滚动重启 Auth**：逐台/逐 Pod 重启 Auth 服务，发布后访问 `/.well-known/openid-configuration` 与 `/.well-known/jwks`，确认 `kid` 已切换至新证书；旧 Token 应该立即验签失败。

#### 滚动更新与密钥轮换

1. **提前生成下一版证书**，建议以“`auth-signing-2025Q1.pfx`”等命名区分版本。
2. **上传并挂载新证书**，但暂不更新环境变量，确保文件可被容器访问。
3. **更新配置指向新证书**：
   ```bash
   export AUTH_CERT_PASSWORD=<new-password>
   docker compose exec auth bash -c 'printenv | grep OpenIddict__Encryption'
   # 修改部署清单或 Compose/Helm values，指向新的 .pfx 与密码
   ```
4. **按批次重启 Auth**（Kubernetes 可 `kubectl rollout restart deploy/radish-auth`；Compose 可 `docker compose up -d auth`）。
5. **验证 JWKS**：
   ```bash
   curl https://radish.com/.well-known/jwks | jq '.keys[].kid'
   ```
   确保新 `kid` 已生效，再次调用受保护 API 验证 Token。
6. **清理旧证书**：确认所有客户端已获取新 Token 后，删除旧 `.pfx` 文件并吊销旧密码，避免被继续使用。

- **注意**：`dev-auth-cert.pfx` 只能用于本地开发，生产环境一定要替换证书与密码，并限制证书文件的访问权限。

## 12. 调试与排障

### 12.1 发现文档

```bash
curl https://localhost:7100/.well-known/openid-configuration
```

### 12.2 获取 Token（测试）

```bash
# Authorization Code 流程需要浏览器交互
# Client Credentials 可直接调用
curl -X POST https://localhost:7100/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=radish-background-service" \
  -d "client_secret=xxx" \
  -d "scope=radish-api"
```

### 12.3 验证 Token

```bash
# 调用 userinfo 端点
curl https://localhost:7100/connect/userinfo \
  -H "Authorization: Bearer {access_token}"
```

### 12.4 常见问题

| 问题 | 可能原因 | 解决方案 |
| --- | --- | --- |
| `invalid_client` | ClientId 未注册或 Secret 错误 | 检查 `OpenIddictSeedHostedService` 中的客户端注册，确认 Id/Secret 与请求一致 |
| `invalid_redirect_uri` | RedirectUri 不匹配 | 确保回调地址与客户端注册的 URI 完全一致（包括协议、端口、路径、末尾 `/`） |
| `invalid_scope` | 请求的 scope 未在 Auth Server 注册，或客户端未声明相应权限 | 在 `Radish.Auth/Program.cs` 中通过 `options.RegisterScopes("openid", "profile", "offline_access", "radish-api")` 注册 scope，并在客户端的 `Permissions` 中添加对应 `Permissions.Prefixes.Scope + "radish-api"` 等配置 |
| `invalid_grant` | 授权码已使用、过期或与当前客户端/redirect_uri 不匹配 | 授权码只能使用一次，确保 `client_id` 与 `redirect_uri` 与原授权请求一致，避免重复使用旧 code |
| Token 验证失败 | Issuer/Audience 或签名配置不匹配；使用了旧 JWT Token 调用 OIDC 资源 | 检查资源服务器的 `Authority`/`TokenValidationParameters` 配置，确保指向 `Radish.Auth`；通过 `.oidc.http` 或前端 OIDC 流程重新获取 Access Token，避免混用旧 JWT 与 OIDC Token |
| 调用需要 `Client` 策略的 API 返回 403 | Access Token 中缺少 `scope=radish-api`；或使用的是不带 scope 的旧登录方式 | 调用 `/connect/authorize` + `/connect/token` 时明确申请 `scope=radish-api`，并使用通过 Gateway/OIDC 获取的 Token 访问需要 `Client` 策略的接口 |
| `/connect/userinfo` 中 `tenant_id` 为空 | 使用了未写入 `tenant_id` 的旧 Token，或 Auth 登录流程未正确设置租户 | 确保通过最新的 Auth 登录入口（`/Account/Login` 或前端 OIDC 流程）获取 Token，并确认登录用户绑定了有效的 TenantId |

## 13. 迁移指南

### 13.1 从旧版 JWT 迁移

如果从第二阶段的简单 JWT 认证迁移：

1. **保留数据模型**：User/Role/UserRole 等实体无需更改
2. **更新 Claims 来源**：从 `JwtTokenGenerate` 迁移到 OpenIddict Claims Principal
3. **调整 Token 验证**：从硬编码密钥改为 Authority 发现
4. **更新前端**：从手动存储 Token 改为 OIDC 库管理

### 13.2 旧版登录接口（临时兼容）

在完全迁移前，可保留 `/api/Login/GetJwtToken` 作为兼容层：

```csharp
[AllowAnonymous]
[HttpPost]
[Obsolete("请使用 OIDC /connect/token 端点")]
public async Task<IActionResult> GetJwtToken(LoginInput input)
{
    // 旧逻辑保留，但建议客户端尽快迁移
}
```

## 14. 数据库配置

### 14.1 OpenIddict 数据库

OpenIddict 使用独立的 SQLite 数据库存储客户端、授权、Token 等信息。

> 说明：此前仓库中曾探索过自定义 SqlSugar Store，但未正式接入。现阶段统一由 EF Core (`AuthOpenIddictDbContext`) 管理 OpenIddict 数据库，后续也不再维护那套实验性实现，避免不同 ORM 并行带来的维护成本。

#### 数据库位置

所有数据库文件统一存放在**解决方案根目录**的 `DataBases/` 文件夹：

```
Radish/
└── DataBases/
    ├── Radish.db                    # 业务主数据库（SqlSugar，API 和 Auth 共享）
    ├── RadishLog.db                 # 业务日志数据库（SqlSugar，API 和 Auth 共享）
    └── RadishAuth.OpenIddict.db     # OpenIddict 数据库（EF Core，Auth 专用）
```

**重要说明 - 数据库共享机制**：
- **业务数据库共享**：`Radish.db` 和 `RadishLog.db` 被 **Radish.Api** 和 **Radish.Auth** 两个项目共同使用
  - 存储用户、角色、权限、租户等业务数据
  - Auth 项目需要访问这些数据来验证用户身份和权限
  - API 项目需要访问这些数据来提供业务功能
- **OpenIddict 数据库独立**：`RadishAuth.OpenIddict.db` 仅由 **Radish.Auth** 项目使用
  - 存储 OIDC 认证相关数据（客户端、授权码、令牌、Scope 等）
  - 使用 EF Core 管理（而不是 SqlSugar）
  - API 项目通过 `IOpenIddictApplicationManager` 访问此数据库，实现客户端管理 API

#### 共享机制

- **Auth 项目**：启动时创建数据库并初始化种子数据（客户端、测试用户等）
- **API 项目**：通过 `IOpenIddictApplicationManager` 访问同一数据库，实现客户端管理 API

两个项目通过查找 `Radish.slnx` 文件定位解决方案根目录，确保使用相同的数据库路径。

#### 配置方式

**方式 1：使用默认路径（推荐）**

不配置 `ConnectionStrings:OpenIddict`，系统自动使用：
```
{SolutionRoot}/DataBases/RadishAuth.OpenIddict.db
```

**方式 2：自定义路径**

在 `appsettings.Local.json` 中配置：

```json
{
  "ConnectionStrings": {
    "OpenIddict": "Data Source=/custom/path/RadishAuth.OpenIddict.db"
  }
}
```

### 14.2 业务数据库

业务数据（用户、角色、内容等）使用 SqlSugar 管理，配置在 `Databases` 数组中：

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "Radish.db"
    },
    {
      "ConnId": "Log",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "RadishLog.db"
    }
  ]
}
```

SQLite 数据库文件名会自动拼接到 `DataBases/` 文件夹下。

### 14.3 数据库迁移

#### OpenIddict 数据库

使用 EF Core Migrations：

```bash
# 添加迁移
cd Radish.Auth
dotnet ef migrations add InitialCreate --context AuthOpenIddictDbContext

# 应用迁移
dotnet ef database update --context AuthOpenIddictDbContext
```

#### 业务数据库

SqlSugar 使用 CodeFirst 自动创建表结构，无需手动迁移。

## 15. Scalar API 文档集成

### 15.1 OIDC 认证配置

Scalar API 文档已集成 OIDC 认证，支持在文档界面直接登录并测试 API。

#### 配置位置

`Radish.Extension/OpenApiExtension/ScalarSetup.cs`：

```csharp
// 添加 OAuth2 Security Scheme
document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.OAuth2,
    Flows = new OpenApiOAuthFlows
    {
        AuthorizationCode = new OpenApiOAuthFlow
        {
            AuthorizationUrl = new Uri("https://localhost:5000/connect/authorize"),
            TokenUrl = new Uri("https://localhost:5000/connect/token"),
            Scopes = new Dictionary<string, string>
            {
                ["openid"] = "OpenID Connect 身份认证",
                ["profile"] = "用户基本信息",
                ["radish-api"] = "Radish API 访问权限"
            }
        }
    }
};

// 配置 Scalar UI
options.AddPreferredSecuritySchemes("oauth2")
    .AddOAuth2Flows("oauth2", flows => {
        flows.AuthorizationCode = new AuthorizationCodeFlow {
            ClientId = "radish-scalar",
            RedirectUri = "https://localhost:5000/scalar/oauth2-callback"
        };
    })
    .AddDefaultScopes("oauth2", ["openid", "profile", "radish-api"]);
```

### 15.2 使用方式

1. 访问 `https://localhost:5000/scalar`（通过 Gateway）
2. 点击右上角 **Authenticate** 按钮
3. 选择 **oauth2** 认证方式
4. 点击 **Authorize** 按钮
5. 使用测试账号登录：
   - 用户名：`test`
   - 密码：`P@ssw0rd!`
6. 确认授权后，所有 API 请求将自动携带 Bearer Token

### 15.3 客户端配置

Scalar 使用的客户端配置（在 Auth 项目的 `OpenIddictSeedHostedService` 中初始化）：

```csharp
new OpenIddictApplicationDescriptor
{
    ClientId = "radish-scalar",
    DisplayName = "Radish API Documentation",
    ConsentType = ConsentTypes.Explicit,
    RedirectUris = {
        new Uri("https://localhost:5000/scalar/oauth2-callback")
    },
    Permissions =
    {
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.OpenId,
        Permissions.Scopes.Profile,
        Permissions.Prefixes.Scope + "radish-api"
    },
    Requirements =
    {
        Requirements.Features.ProofKeyForCodeExchange // 强制 PKCE
    }
}
```

## 16. 扩展规划

- **第三方登录**：GitHub、微信、钉钉等 OAuth Provider
- **MFA**：TOTP、短信验证码
- **设备管理**：记住设备、异常登录检测
- **API Key**：为开发者提供静态 API 密钥访问

---

> 本文档随第三阶段开发持续更新，如有变更请同步修改 `DevelopmentLog.md`。
