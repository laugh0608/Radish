# 身份语义收敛与 Claim 治理设计

> 本文定义 Radish 在 OIDC/JWT 体系下的“唯一身份语义层”，用于彻底消除运行时代码分散解析 Claim 的遗留问题。
>
> 相关文档：
> - [鉴权与授权指南](/guide/authentication)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [开发框架说明](/architecture/framework)
> - [开发规范](/architecture/specifications)

## 1. 背景与问题

Radish 在 OIDC 接入、历史 JWT 兼容、多租户演进的过程中，逐步形成了多套并存的身份读取口径：

- OIDC/JWT 标准 Claim：`sub`、`name`、`role`、`scope`、`tenant_id`
- .NET 映射 Claim：`ClaimTypes.NameIdentifier`、`ClaimTypes.Name`、`ClaimTypes.Role`
- 历史兼容 Claim：`jti`、`TenantId`
- 运行时代码分散读取：`FindFirst(...)`、`FindAll(...)`、`User.IsInRole(...)`
- 公共接口逃逸口：`GetClaimValueByType(string)`、`GetUserInfoFromToken(string)`

这类并存会带来以下问题：

- **语义漂移**：不同模块对“当前用户”含义理解不一致。
- **兼容逻辑扩散**：每多一个入口，就多一套 `sub/jti`、`tenant_id/TenantId` 的兜底判断。
- **难以治理**：扫描时只能持续“补漏”，无法真正从架构上封死回归。
- **协议污染业务**：业务/基础设施层直接依赖 Claim 名，导致认证细节上浮。

因此，本专项的目标不是继续“收口几个 `FindFirst`”，而是建立**唯一身份语义层**，让运行时代码不再直接理解 Claim 结构。

## 2. 设计目标

### 2.1 目标

1. **业务与基础设施代码不再直接解析 Claim**。
2. **兼容逻辑只保留一处**，由统一的身份标准化组件负责。
3. **协议边界显式保留**，签发端、`userinfo`、JWT/OIDC 配置仍允许操作 Claim。
4. **新代码默认只依赖当前用户上下文**，而不是 `ClaimsPrincipal`。
5. **通过规则防回归**，避免后续重新长出分散解析。

### 2.2 非目标

- 本次不改变 OIDC 授权码流、客户端注册、`/connect/*` 端点语义。
- 本次不立即移除所有历史兼容 Claim，而是先压缩到唯一转换点。
- 本次不重做权限模型；角色/Scope 仍沿用现有授权策略与业务语义。

## 3. 总体原则

### 3.1 协议边界与运行时边界分离

Claim 只允许存在于以下两类边界：

- **协议输出边界**：Auth 签发 Cookie/Token、设置 claim destinations。
- **协议输入边界**：JWT/OIDC 认证配置、`/connect/userinfo`、唯一标准化转换器。

除此之外，运行时代码统一只消费 **CurrentUser** 语义对象。

### 3.2 标准化优先于兼容

- 新签发统一以 OIDC 风格 claim 为准：`sub`、`name`、`role`、`scope`、`tenant_id`
- 历史 Claim 仅作为兼容输入：`ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId`、`jti`
- 兼容逻辑只允许存在于统一标准化组件，不允许下沉到控制器、Hub、中间件、Filter、Repository、Service

### 3.3 运行时只允许读取“当前用户视图”

运行时代码只能依赖如下抽象：

- `CurrentUser`
- `ICurrentUserAccessor`
- `CurrentUser` 的角色/Scope 判断能力

禁止直接读取原始 claim 名字符串。

## 4. 目标架构

### 4.1 核心对象

#### `CurrentUser`

统一的当前用户上下文，只承载业务真正需要的身份语义：

```csharp
public sealed class CurrentUser
{
    public bool IsAuthenticated { get; init; }
    public long UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public long TenantId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    public bool IsInRole(string role);
    public bool HasScope(string scope);
}
```

该对象是 **运行时唯一身份视图**。

#### `ICurrentUserAccessor`

负责为当前请求提供 `CurrentUser` 实例：

```csharp
public interface ICurrentUserAccessor
{
    CurrentUser Current { get; }
}
```

控制器、Hub、中间件、Filter、授权辅助组件都优先依赖该接口。

#### `IClaimsPrincipalNormalizer`

负责把 `ClaimsPrincipal` / Bearer Token 统一转换为 `CurrentUser`：

```csharp
public interface IClaimsPrincipalNormalizer
{
    CurrentUser Normalize(ClaimsPrincipal? principal, string? token = null);
}
```

这是 **Claim 兼容逻辑唯一允许存在的地方**。

#### `UserClaimTypes`

统一维护 Claim 常量：

- `Sub`
- `Name`
- `PreferredUsername`
- `Role`
- `Scope`
- `TenantId`
- `LegacyTenantId`
- `LegacyNameIdentifier`
- `LegacyRole`
- `LegacyJti`

#### `UserRoles` / `UserScopes` / `AuthorizationPolicies`

角色、Scope 与授权策略也应统一常量化，避免业务层再散落 `"Admin"`、`"System"`、`"radish-api"`、`"Client"` 等字面量。

### 4.2 目标依赖关系

```text
ClaimsPrincipal / Token
        │
        ▼
IClaimsPrincipalNormalizer
        │
        ▼
CurrentUser
        │
        ▼
ICurrentUserAccessor
        │
        ├─ Controller
        ├─ Hub
        ├─ Middleware
        ├─ Authorization Helper / Filter
        └─ 其他运行时代码
```

从此以后：

- 运行时代码不再了解 `sub/jti/tenant_id/TenantId/role/scope` 的兼容细节
- 历史 Claim 兼容集中在标准化层
- 业务代码只理解 `CurrentUser`

## 5. 分层落点建议

结合当前 Radish 分层约束，建议如下落位：

| 组件 | 建议位置 | 原因 |
| --- | --- | --- |
| `UserClaimTypes` | `Radish.Common.HttpContextTool` | 与当前 Claim 读取工具同域，避免字符串散落 |
| `CurrentUser` | `Radish.Common.HttpContextTool` | 作为运行时上下文 DTO，无业务实体依赖 |
| `ICurrentUserAccessor` | `Radish.Common.HttpContextTool` | 控制器/Hub/扩展层都可安全依赖 |
| `IClaimsPrincipalNormalizer` | `Radish.Common.HttpContextTool` | 统一承载兼容逻辑 |
| `UserRoles` / `UserScopes` / `AuthorizationPolicies` | `Radish.Common.HttpContextTool` | 当前已在统一身份工具域内落地，便于宿主与运行时共同复用 |
| OIDC Claim 签发逻辑 | `Radish.Auth` | 协议边界，应保留在 Auth |
| JWT 验证/策略配置 | `Radish.Api` | 资源服务器边界，应保留在宿主 |

## 6. Claim 标准化矩阵

| 语义 | 首选来源 | 兼容来源 | 最终去向 |
| --- | --- | --- | --- |
| 用户标识 | `sub` | `ClaimTypes.NameIdentifier`、`jti` | `CurrentUser.UserId` |
| 用户名 | `name` | `ClaimTypes.Name`、`preferred_username` | `CurrentUser.UserName` |
| 租户 | `tenant_id` | `TenantId` | `CurrentUser.TenantId` |
| 角色 | `role` | `ClaimTypes.Role` | `CurrentUser.Roles` |
| Scope | `scope` | 无 | `CurrentUser.Scopes` |

约束如下：

- **新签发只新增标准 Claim**，历史 Claim 不再继续扩散。
- **旧 Claim 只作为输入兼容**，不允许再成为新代码的直接依赖。
- **`CurrentUser` 是唯一输出模型**。

## 7. 协议边界保留清单

以下位置允许继续显式操作 Claim，但必须使用统一常量：

1. `Radish.Auth` 登录/授权时构造 claim 集合。
2. `Radish.Auth` 根据 scope/claim 决定 destinations。
3. `Radish.Auth` 的 `/connect/userinfo` 输出。
4. `Radish.Api` 的 JWT/OIDC `TokenValidationParameters` 配置。
5. 统一标准化转换器 `IClaimsPrincipalNormalizer`。

除上述边界外，其余运行时代码一律不得直接解析 Claim。

## 8. 禁止项

在 `Radish.Api`、`Radish.Extension`、`Radish.Common`、`Radish.Gateway` 的运行时代码中，禁止新增：

- `FindFirst(...)`
- `FindAll(...)`
- `ClaimTypes.*`
- `User.IsInRole(...)`
- 手工读取 `"sub"` / `"jti"` / `"tenant_id"` / `"TenantId"` / `"role"` / `"scope"`

唯一例外：

- 协议边界文件
- 统一标准化组件
- 面向协议的测试/联调资产

## 9. 旧接口处理策略

现有 `IHttpContextUser` 中的以下接口属于“原始读取逃逸口”：

- `GetClaimsIdentity()`
- `GetClaimValueByType(string)`
- `GetUserInfoFromToken(string)`

处理策略：

1. **阶段一**：标记为 `[Obsolete]`，禁止新增使用。
2. **阶段二**：仓库内调用点清零。
3. **阶段三**：降为内部实现或彻底删除。

`IHttpContextUser` 可在迁移阶段作为兼容外观保留，但内部实现应逐步转向 `CurrentUser`。

## 10. 防回归策略

为 M12 之后的工程化阶段预留静态扫描规则口径：

```bash
rg -n --glob '*.cs' \
  -g '!Radish.Auth/Controllers/AccountController.cs' \
  -g '!Radish.Auth/Controllers/AuthorizationController.cs' \
  -g '!Radish.Auth/Controllers/UserInfoController.cs' \
  -g '!**/CurrentUser*' \
  -g '!**/*ClaimsPrincipalNormalizer*' \
  -e 'FindFirst\\s*\\(' \
  -e 'FindAll\\s*\\(' \
  -e 'ClaimTypes\\.' \
  -e 'IsInRole\\s*\\(' \
  -e '"sub"|"jti"|"tenant_id"|"TenantId"|"role"|"scope"'
```

只要出现新增命中，就视为架构回归。

当前仓库已补统一入口：

```bash
npm run check:identity-claims
```

并已纳入：

```bash
npm run validate:baseline
npm run validate:baseline:quick
```

## 11. 当前实施状态（2026-03-07）

当前专项已进入“兼容层冻结”阶段，运行时主路径基本完成收敛：

- `CurrentUser` / `ICurrentUserAccessor` 已成为 `Radish.Api`、`Radish.Extension`、`Radish.Repository`、`Radish.Service`、`Radish.Infrastructure` 的默认身份读取入口。
- `IClaimsPrincipalNormalizer` + `UserClaimReader` 已成为 Claim 兼容输入的唯一标准化入口。
- `UserRoles`、`UserScopes`、`AuthorizationPolicies` 已完成运行时常量化，`Client` / `System` / `SystemOrAdmin` / `RadishAuthPolicy` 不再依赖字符串散点。
- `App.CurrentUser` 已成为底层静态读取口，`App.HttpContextUser` 仅作为兼容属性保留，且已标记 `[Obsolete]`。
- `IHttpContextUser` 当前仅保留兼容职责；`GetClaimsIdentity()`、`GetClaimValueByType(string)`、`GetUserInfoFromToken(string)`、`GetToken()` 与兼容扩展均已标记 `[Obsolete]`。
- `Radish.Api` 运行时代码中，`IHttpContextUser` 的直接使用已收束到 `Program.cs` 的兼容注册；非协议边界已不再新增依赖。

当前明确保留的边界如下：

1. **协议边界保留项**：`Radish.Auth/Controllers/AccountController.cs`、`Radish.Auth/Controllers/AuthorizationController.cs`、`Radish.Auth/Controllers/UserInfoController.cs`。
2. **协议配置保留项**：`Radish.Api/Program.cs` 中 JWT/OIDC 验证、策略配置与兼容注册。
3. **标准化保留项**：`ClaimsPrincipalNormalizer`、`UserClaimReader`、`CurrentUserAccessor`。

仍待后续阶段完成的事项：

- 将静态扫描规则纳入 M12 之后的工程化规划，在 M13 阶段再接入脚本或流水线。
- 评估 `IHttpContextUser` 兼容层的最终删除时机。
- 在外部客户端确认完成前，不推动协议边界语义变更或历史 Claim 输出清退。

## 12. 验收标准

专项完成后，应满足以下条件：

1. 运行时代码默认只依赖 `CurrentUser` / `ICurrentUserAccessor`。
2. Claim 兼容逻辑只存在一处。
3. `IHttpContextUser` 的原始逃逸接口全部废弃或移除。
4. `Auth` 侧 Claim 签发与 `Api` 侧读取口径统一为 OIDC 标准 Claim。
5. 在后续工程化阶段，仓库扫描能够稳定拦截非协议边界的原始 Claim 解析回归。

## 13. 后续路线

本设计落地分两步：

1. **迁移阶段**：引入 `CurrentUser` 抽象、统一标准化、替换运行时入口、加废弃标记。
2. **收敛阶段**：停止双写 `ClaimTypes.*` 与 `TenantId`，最终只保留标准 Claim。

详细执行顺序见：[身份语义收敛迁移计划](/guide/identity-claim-migration)
