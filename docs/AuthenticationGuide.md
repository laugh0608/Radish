# 鉴权与授权总览

Radish 后端的鉴权链路建立在 **JWT 持有者认证 + ASP.NET Core Authorization** 之上，结合数据库中的“角色-API”映射动态裁剪可访问资源。本文档梳理当前实现，以便快速定位策略或扩展。

## 1. 鉴权流程

1. 客户端调用 `GET /api/Login/GetJwtToken`，传入用户名/密码。
2. `LoginController` 验证账号（`Radish.Api/Controllers/LoginController.cs`），并为合法用户构造 `Claim` 列表（`Name`、`Jti`、`TenantId`、`Iat`、`Expiration`、`Role` 等）。
3. `JwtTokenGenerate.BuildJwtToken` 使用固定密钥、Issuer/Audience 等参数生成 JWT（`Radish.Extension/JwtTokenGenerate.cs`）。
4. 后续 API 通过 `Authorization: Bearer <token>` 头传递凭据。
5. `Program.cs` 注册的 `AddJwtBearer` 中间件校验 Token，同时 ASP.NET Core Authorization 根据策略决定是否放行。

## 2. JWT 配置

- **Issuer/Audience**：`Radish` / `luobo`，在 `Program.cs` 和 `JwtTokenGenerate` 中保持一致，防止解密失败。
- **SigningKey**：当前保存在源代码中（`wpH7A1jQRPu...`），建议在生产中改为环境变量或 Secret Manager。
- **Lifetime**：`TokenValidationParameters.ValidateLifetime = true`，`JwtTokenGenerate` 默认签发 12 小时有效期（`tokenTime = 60 * 60 * 12`）。
- **Claims 要求**：`PermissionRequirementHandler` 依赖 `ClaimTypes.Role`/`"role"` 判断访问范围，并读取 `ClaimTypes.Expiration` 监控过期，因此生成 Token 时必须写入这些项。

## 3. 登录接口说明

`LoginController.GetJwtToken` 做了以下工作：

- 使用 `Md5Helper.Md5Encrypt32` 对密码进行 32 位 MD5，加密匹配数据库。
- 调用 `IUserService.GetUserRoleNameStrAsync` 获取角色字符串，多角色会以逗号拼接并写入 `ClaimTypes.Role`。
- 调用 `JwtTokenGenerate` 返回 `TokenInfoVo`，包含 `TokenInfo`、`ExpiresIn`、`TokenType`。
- 认证失败会返回 `MessageModel<TokenInfoVo>.Failed("认证失败")`，便于前端提示。

若需扩展登录方式（如短信/三方登录），确保最终仍生成上述核心 Claims，或同步调整 Handler 的读取逻辑。

## 4. 授权策略整理

`builder.Services.AddAuthorizationBuilder()` 注册了多种策略（见 `Radish.Api/Program.cs`）：

- `Client`：`RequireClaim("iss","Radish")`，只校验签发者。
- `System`：`RequireRole("System")`，用于后台管理类接口。
- `SystemOrAdmin`：`RequireRole("System","Admin")`，兼容高权限运营场景。
- `RadishAuthPolicy`：自定义策略，借助 `PermissionRequirement` + `PermissionRequirementHandler` 做“角色-URL”匹配，是绝大多数业务接口挂载的策略。

控制器可通过 `[Authorize(Policy = "System")]` 等特性选用不同策略；若未声明，默认只执行 JWT 验证，不做额外授权判断。

## 5. PermissionRequirementHandler 细节

`Radish.Extension/PermissionExtension/PermissionRequirementHandler.cs` 负责 `RadishAuthPolicy` 的动态授权：

- 首次命中会调用 `IUserService.RoleModuleMaps()` 构建 `PermissionItem` 列表，按角色与 API（`ApiModule.LinkUrl`）缓存到 `PermissionRequirement.PermissionItems`，避免重复查询。
- 每个请求都会：
  - 使用 `Schemes.GetDefaultAuthenticateSchemeAsync()` + `AuthenticateAsync()` 解析 Token。
  - 若 `AppSettings.UseLoadTest` 为 `true`，跳过登录校验，用于压测或调试。
  - 检查 `ClaimTypes.Expiration` 是否仍大于当前时间，过期则 `context.Fail`。
  - 从 `ClaimTypes.Role` / `"role"` 收集角色，若未含 `System`，将当前 URL 与角色下的 `PermissionItem.Url` 通过正则匹配（忽略大小写、可支持 `/api/User/Get/\d+` 形式）。
  - 匹配失败时 `context.Fail()`，否则 `context.Succeed(requirement)`。

变更数据库路由或角色后，可清空应用缓存或重启以刷新 `PermissionItems`，或扩展 Handler 使其支持定期刷新。

## 6. 调试与实践建议

- **本地调试**：在 `Radish.Api/http-client.env.json` 中维护测试 Token，可配合 `Radish.Api.http` 直接调用。
- **密钥轮换**：同步更新 `Program.cs` 与 `JwtTokenGenerate` 中的 `IssuerSigningKey`，并记录在 `docs/DevelopmentLog.md`，必要时支持双 Token 验证过渡。
- **新增受控 API**：确保对应角色的 `RoleModuleMap` 数据存在，并在控制器上标记 `[Authorize(Policy = "RadishAuthPolicy")]`。
- **压测开关**：`appsettings.json` 的 `AppSettings.UseLoadTest` 置为 `true` 可跳过登录校验，但务必只在测试环境开启。

如需进一步扩展（多租户、Scope、刷新 Token 等），建议基于此文档罗列的触发点逐步演化。
