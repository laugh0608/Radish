# 鉴权与授权指南

Radish 采用 **OIDC（OpenID Connect）** 架构实现统一身份认证，基于 **OpenIddict** 构建认证服务器。本文档梳理整体架构、配置要点与扩展方式。

> **相关文档**：本文档专注于认证的技术实现细节。关于 Gateway 架构下的统一认证规划（Phase 2），请参阅 [GatewayPlan.md](GatewayPlan.md#phase-2-认证集成)。

## 1. 架构概览

### 1.1 系统拓扑

```
┌─────────────────────────────────────────────────────────┐
│                  Radish.Auth (OIDC Server)              │
│  端口: https://localhost:7100                           │
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
│:7110     │       │client    │   │API Docs│  │Dashboard│
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
    ConsentType = ConsentTypes.Implicit, // 跳过授权确认页
    RedirectUris = {
        new Uri("https://localhost:7110/scalar/oauth2-callback")
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
    }
}
```

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

客户端存储在数据库中，支持通过后台管理界面动态配置。

#### 数据模型扩展

```csharp
// 扩展 OpenIddict Application 实体
public class RadishApplication
{
    // OpenIddict 基础字段
    public string Id { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? DisplayName { get; set; }
    // ...

    // 扩展字段
    public string? Logo { get; set; }
    public string? Description { get; set; }
    public string? DeveloperName { get; set; }
    public string? DeveloperEmail { get; set; }
    public ApplicationStatus Status { get; set; } // Active/Disabled
    public ApplicationType AppType { get; set; } // Internal/ThirdParty
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}

public enum ApplicationStatus
{
    Active,
    Disabled,
    PendingReview // 第三方应用审核中
}

public enum ApplicationType
{
    Internal,   // 内部应用
    ThirdParty  // 第三方应用
}
```

#### 管理 API

| 方法 | 端点 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/clients` | 获取客户端列表 | Admin |
| GET | `/api/clients/{id}` | 获取客户端详情 | Admin |
| POST | `/api/clients` | 创建客户端 | Admin |
| PUT | `/api/clients/{id}` | 更新客户端 | Admin |
| DELETE | `/api/clients/{id}` | 删除客户端 | Admin |
| POST | `/api/clients/{id}/reset-secret` | 重置 Secret | Admin |
| POST | `/api/clients/{id}/toggle-status` | 启用/禁用 | Admin |

#### 创建客户端示例

```http
POST /api/clients
Content-Type: application/json
Authorization: Bearer {admin_token}

{
  "clientId": "third-party-app",
  "displayName": "第三方游戏社区",
  "description": "某游戏社区论坛接入",
  "appType": "ThirdParty",
  "redirectUris": [
    "https://game-community.example.com/callback"
  ],
  "postLogoutRedirectUris": [
    "https://game-community.example.com"
  ],
  "permissions": {
    "grantTypes": ["authorization_code", "refresh_token"],
    "scopes": ["openid", "profile", "radish-api"]
  },
  "requirePkce": true
}
```

响应：

```json
{
  "id": "abc123",
  "clientId": "third-party-app",
  "clientSecret": "generated-random-secret",
  "displayName": "第三方游戏社区",
  "status": "Active",
  "createdAt": "2025-11-24T10:00:00Z"
}
```

> **注意**：ClientSecret 仅在创建时返回一次，请妥善保管。

## 5. 资源服务器配置

Radish.Api 作为资源服务器验证 Token：

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7100";
        options.Audience = "radish-api";
        options.RequireHttpsMetadata = true; // 生产环境必须为 true

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero // 严格过期时间
        };
    });

builder.Services.AddAuthorization(options =>
{
    // 保留现有策略
    options.AddPolicy("Client", policy =>
        policy.RequireClaim("iss", "https://localhost:7100"));

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

在 Radish.Api 中配置 Scalar 使用 OAuth：

```csharp
// Program.cs
app.MapScalarApiReference(options =>
{
    options.WithTitle("Radish API")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithOAuth2Configuration(oauth =>
        {
            oauth.ClientId = "radish-scalar";
            oauth.Scopes = new[] { "openid", "profile", "radish-api" };
        });
});
```

## 8. 多租户支持

### 8.1 租户识别

- 通过 Token 中的 `tenant_id` Claim 识别租户
- Auth Server 在签发 Token 时写入租户信息

### 8.2 租户隔离

```csharp
// 用户登录时写入租户 Claim
identity.SetClaim("tenant_id", user.TenantId.ToString());

// 资源服务器读取
var tenantId = User.FindFirstValue("tenant_id");
```

## 9. 权限与授权

### 9.1 角色-权限模型

延续第二阶段的 RBAC 模型：

```
User -> UserRole -> Role -> RolePermission -> Permission
                      ↓
                 RoleModuleMap -> ApiModule
```

### 9.2 PermissionRequirementHandler

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

## 10. 安全最佳实践

### 10.1 Token 安全

- Access Token 有效期：15-60 分钟
- Refresh Token 有效期：7-30 天
- 启用 Token 轮换：每次刷新生成新的 Refresh Token
- 支持 Token 撤销：用户登出时撤销所有 Token

### 10.2 客户端安全

- 公开客户端（SPA/Mobile）必须使用 PKCE
- 机密客户端使用强密钥，定期轮换
- 严格限制 RedirectUris，避免开放重定向攻击

### 10.3 传输安全

- 全程 HTTPS
- 启用 HSTS
- Cookie 设置 `Secure`、`HttpOnly`、`SameSite=Strict`

## 11. 调试与排障

### 11.1 发现文档

```bash
curl https://localhost:7100/.well-known/openid-configuration
```

### 11.2 获取 Token（测试）

```bash
# Authorization Code 流程需要浏览器交互
# Client Credentials 可直接调用
curl -X POST https://localhost:7100/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=radish-background-service" \
  -d "client_secret=xxx" \
  -d "scope=radish-api"
```

### 11.3 验证 Token

```bash
# 调用 userinfo 端点
curl https://localhost:7100/connect/userinfo \
  -H "Authorization: Bearer {access_token}"
```

### 11.4 常见问题

| 问题 | 可能原因 | 解决方案 |
| --- | --- | --- |
| `invalid_client` | ClientId 未注册或 Secret 错误 | 检查 DbSeed 客户端注册 |
| `invalid_redirect_uri` | RedirectUri 不匹配 | 确保与注册的 URI 完全一致 |
| `invalid_grant` | 授权码已使用或过期 | 授权码只能使用一次 |
| Token 验证失败 | Issuer/Audience 不匹配 | 检查资源服务器配置 |

## 12. 迁移指南

### 12.1 从旧版 JWT 迁移

如果从第二阶段的简单 JWT 认证迁移：

1. **保留数据模型**：User/Role/UserRole 等实体无需更改
2. **更新 Claims 来源**：从 `JwtTokenGenerate` 迁移到 OpenIddict Claims Principal
3. **调整 Token 验证**：从硬编码密钥改为 Authority 发现
4. **更新前端**：从手动存储 Token 改为 OIDC 库管理

### 12.2 旧版登录接口（临时兼容）

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

## 13. 扩展规划

- **第三方登录**：GitHub、微信、钉钉等 OAuth Provider
- **MFA**：TOTP、短信验证码
- **设备管理**：记住设备、异常登录检测
- **API Key**：为开发者提供静态 API 密钥访问

---

> 本文档随第三阶段开发持续更新，如有变更请同步修改 `DevelopmentLog.md`。
