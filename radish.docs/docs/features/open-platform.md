# 开放平台设计文档

本文档描述 Radish 开放平台的设计目标、架构和实现计划，支持内部应用统一认证和第三方应用接入。

## 1. 设计目标

### 1.1 核心需求

1. **单点登录（SSO）**：用户登录一次即可访问所有内部应用
2. **统一认证**：所有应用通过 OIDC 协议进行身份认证
3. **动态管理**：后台可动态配置客户端应用
4. **第三方接入**：未来支持第三方应用接入 Radish 生态

### 1.2 应用矩阵

| 应用 | ClientId | 类型 | 说明 |
|------|----------|------|------|
| WebOS 前端 | `radish-client` | Internal | 桌面系统 + 所有子应用（论坛/聊天/商城/后台管理） |
| API 文档 | `radish-scalar` | Internal | Scalar API 文档 |
| 后台控制台 | `radish-console` | Internal | 管理控制台 |
| 商城应用 | `radish-shop` | Internal | 商城应用（占位，未来实现） |
| 第三方应用 | `{custom}` | ThirdParty | 动态注册 |

**说明**：
- `radish-client` 是一个 WebOS 超级应用，包含所有子应用
- 后台管理不需要独立的 OIDC 客户端，它是 `radish-client` 内的一个子应用
- 通过前端应用注册表（AppRegistry）+ 权限控制决定用户可见的子应用

## 2. 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                      Radish 开放平台                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                 Radish.Auth                          │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │ OIDC Server │  │ 用户管理    │  │ 客户端管理  │  │   │
│  │  │ /connect/*  │  │ /api/users  │  │ /api/clients│  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────┘   │
│                            │                                │
│         ┌──────────────────┼──────────────────┐            │
│         ▼                  ▼                  ▼            │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐       │
│  │radish-client│   │ radish-     │   │ 第三方应用   │       │
│  │ (WebOS)     │   │ scalar等    │   │ (动态注册)  │       │
│  │  ├─论坛     │   │             │   │             │       │
│  │  ├─聊天室   │   │             │   │             │       │
│  │  ├─商城     │   │             │   │             │       │
│  │  └─后台管理 │   │             │   │             │       │
│  └─────────────┘   └─────────────┘   └─────────────┘       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 3. 客户端类型

### 3.1 内部应用（Internal）

- 由 Radish 团队开发和维护
- 通过 DbSeed 预置
- 默认启用，无需审核
- 可访问所有 Scope

### 3.2 第三方应用（ThirdParty）

- 由外部开发者创建
- 需要审核后才能使用
- 限制可访问的 Scope
- 需要遵守开放平台协议

## 4. 数据模型

### 4.1 应用实体

```csharp
public class RadishApplication
{
    // 基础信息
    public string Id { get; set; }
    public string ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Logo { get; set; }

    // 分类
    public ApplicationType AppType { get; set; }
    public ApplicationStatus Status { get; set; }

    // 开发者信息
    public string? DeveloperName { get; set; }
    public string? DeveloperEmail { get; set; }
    public string? DeveloperWebsite { get; set; }

    // 配置
    public List<string> RedirectUris { get; set; }
    public List<string> PostLogoutRedirectUris { get; set; }
    public List<string> Permissions { get; set; }
    public bool RequirePkce { get; set; }
    public string? ConsentType { get; set; }

    // 统计
    public long AuthorizationCount { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // 审计
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
```

### 4.2 枚举定义

```csharp
public enum ApplicationType
{
    Internal = 0,    // 内部应用
    ThirdParty = 1   // 第三方应用
}

public enum ApplicationStatus
{
    Active = 0,        // 正常
    Disabled = 1,      // 已禁用
    PendingReview = 2, // 待审核
    Rejected = 3       // 已拒绝
}
```

## 5. API 设计

### 5.1 客户端管理

#### 列表查询

```http
GET /api/clients?page=1&pageSize=20&appType=Internal&status=Active
Authorization: Bearer {admin_token}
```

响应：
```json
{
  "items": [
    {
      "id": "abc123",
      "clientId": "radish-client",
      "displayName": "Radish Web Client",
      "appType": "Internal",
      "status": "Active",
      "authorizationCount": 1234,
      "lastUsedAt": "2025-11-24T10:00:00Z"
    }
  ],
  "total": 4,
  "page": 1,
  "pageSize": 20
}
```

#### 创建应用

```http
POST /api/clients
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "clientId": "my-app",
  "displayName": "我的应用",
  "description": "应用描述",
  "appType": "ThirdParty",
  "redirectUris": ["https://my-app.com/callback"],
  "postLogoutRedirectUris": ["https://my-app.com"],
  "permissions": {
    "grantTypes": ["authorization_code", "refresh_token"],
    "scopes": ["openid", "profile"]
  },
  "requirePkce": true,
  "developerName": "开发者名称",
  "developerEmail": "dev@example.com"
}
```

#### 重置 Secret

```http
POST /api/clients/{id}/reset-secret
Authorization: Bearer {admin_token}
```

响应：
```json
{
  "clientSecret": "new-generated-secret"
}
```

#### 启用/禁用

```http
POST /api/clients/{id}/toggle-status
Authorization: Bearer {admin_token}
```

### 5.2 权限要求

| 操作 | 所需角色 |
|------|---------|
| 查看列表 | Admin |
| 查看详情 | Admin |
| 创建应用 | Admin |
| 编辑应用 | Admin |
| 删除应用 | System |
| 重置 Secret | Admin |
| 启用/禁用 | Admin |

## 6. 后台管理界面

### 6.1 应用管理模块

```
后台管理 → 开放平台 → 应用管理
├── 应用列表
│   ├── 筛选：类型（内部/第三方）、状态
│   ├── 搜索：ClientId、名称
│   └── 操作：查看、编辑、禁用
├── 创建应用
│   ├── 基础信息（名称、描述、Logo）
│   ├── 应用类型
│   ├── 回调配置（RedirectUris）
│   ├── 权限配置（GrantTypes、Scopes）
│   └── 开发者信息
├── 应用详情
│   ├── 凭证信息（ClientId、Secret）
│   ├── 使用统计
│   └── 操作日志
└── 应用设置
    ├── 编辑配置
    ├── 重置 Secret
    └── 启用/禁用
```

### 6.2 界面原型

#### 应用列表页

| ClientId | 名称 | 类型 | 状态 | 授权次数 | 最后使用 | 操作 |
|----------|------|------|------|---------|---------|------|
| radish-client | Radish Web | 内部 | 正常 | 12,345 | 2分钟前 | 查看 \| 编辑 |
| radish-admin | Radish Admin | 内部 | 正常 | 567 | 1小时前 | 查看 \| 编辑 |
| third-party-1 | 游戏社区 | 第三方 | 正常 | 89 | 3天前 | 查看 \| 编辑 \| 禁用 |

#### 创建应用表单

```
┌─────────────────────────────────────────┐
│ 创建应用                                 │
├─────────────────────────────────────────┤
│ 基础信息                                 │
│ ┌─────────────────────────────────────┐ │
│ │ 应用名称 *    [________________]    │ │
│ │ 应用描述      [________________]    │ │
│ │ 应用 Logo     [上传]                │ │
│ │ 应用类型 *    ○ 内部  ● 第三方      │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ 回调配置                                 │
│ ┌─────────────────────────────────────┐ │
│ │ 回调地址 *    [________________] [+]│ │
│ │ 登出回调      [________________] [+]│ │
│ └─────────────────────────────────────┘ │
│                                         │
│ 权限配置                                 │
│ ┌─────────────────────────────────────┐ │
│ │ 授权类型      ☑ Authorization Code  │ │
│ │               ☑ Refresh Token       │ │
│ │               ☐ Client Credentials  │ │
│ │ 作用域        ☑ openid ☑ profile   │ │
│ │               ☐ email  ☑ radish-api│ │
│ │ 强制 PKCE     ☑                     │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ 开发者信息                               │
│ ┌─────────────────────────────────────┐ │
│ │ 开发者名称    [________________]    │ │
│ │ 联系邮箱      [________________]    │ │
│ │ 官方网站      [________________]    │ │
│ └─────────────────────────────────────┘ │
│                                         │
│              [取消]  [创建]              │
└─────────────────────────────────────────┘
```

## 7. Scope 设计

### 7.1 标准 Scope

| Scope | 说明 | 返回的 Claims |
|-------|------|--------------|
| `openid` | OIDC 必需 | `sub` |
| `profile` | 用户基本信息 | `name`, `nickname`, `picture` |
| `email` | 邮箱 | `email`, `email_verified` |
| `phone` | 手机号 | `phone_number`, `phone_number_verified` |
| `offline_access` | 允许 refresh_token | - |

### 7.2 自定义 Scope

| Scope | 说明 | 权限 |
|-------|------|------|
| `radish-api` | 访问业务 API | 所有业务接口 |
| `radish-admin` | 访问管理 API | 管理接口（需要 Admin 角色） |
| `radish-profile:write` | 修改个人信息 | 更新昵称、头像等 |

### 7.3 第三方应用限制

第三方应用默认只能申请：
- `openid`
- `profile`
- `radish-api`（受限，不含敏感操作）

敏感 Scope 需要额外审核：
- `email`
- `phone`
- `radish-profile:write`

## 8. 安全策略

### 8.1 客户端安全

- **Secret 管理**：
  - 使用加密存储（不明文保存）
  - 创建时生成高强度随机 Secret
  - 重置后旧 Secret 立即失效

- **PKCE 要求**：
  - 第三方应用强制启用 PKCE
  - 内部 Web 应用建议启用

- **RedirectUri 验证**：
  - 严格匹配，不支持通配符
  - 必须使用 HTTPS（生产环境）

### 8.2 访问控制

- **第三方应用审核**：
  - 创建后默认为 `PendingReview` 状态
  - 管理员审核通过后变为 `Active`

- **配额限制**（未来）：
  - 每日授权次数限制
  - API 调用频率限制

### 8.3 审计日志

记录以下操作：
- 应用创建/编辑/删除
- Secret 重置
- 状态变更
- 异常授权行为

## 9. 实现计划

### 阶段一：M3（OIDC 基础）

- [x] OpenIddict 配置
- [ ] 客户端数据库存储
- [ ] DbSeed 预置内部应用
- [ ] 客户端管理 API
- [ ] Scalar OAuth 集成

### 阶段二：M4（后台管理）

- [ ] 后台应用管理界面
- [ ] 创建/编辑/删除功能
- [ ] Secret 重置功能
- [ ] 启用/禁用功能

### 阶段三：M9+（开放平台）

- [ ] 第三方开发者注册
- [ ] 应用审核流程
- [ ] Scope 精细化控制
- [ ] 使用统计/配额
- [ ] 开发者文档/SDK
- [ ] Webhook 通知

## 10. 第三方接入指南（规划）

### 10.1 接入流程

```
1. 开发者注册
   ↓
2. 创建应用（填写信息、配置回调）
   ↓
3. 提交审核
   ↓
4. 审核通过，获取凭证
   ↓
5. 集成 SDK / 实现 OIDC 流程
   ↓
6. 测试验证
   ↓
7. 上线运营
```

### 10.2 SDK 规划

- JavaScript/TypeScript SDK
- .NET SDK
- 示例代码和文档

### 10.3 文档内容

- 快速开始
- 认证流程详解
- API 参考
- 最佳实践
- 常见问题

---

## 11. 与其他模块的关系

- **AuthenticationGuide.md**：OIDC 技术实现细节
- **DevelopmentPlan.md**：开发计划和里程碑
- **FrontendDesign.md**：后台管理界面设计规范
- **GatewayPlan.md**：未来 Gateway 可能需要感知客户端信息

---

> 本文档随开放平台功能开发持续更新，如有变更请同步修改 `DevelopmentLog.md`。
