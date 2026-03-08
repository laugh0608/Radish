# Console 核心概念

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 2 章

## 2. 核心概念

### 2.1 角色定位

Console 管理后台是 Radish 社区的**管理员专用平台**，与面向普通用户的 WebOS 客户端形成明确的职责分离：

| 对比项 | Console 管理后台 | WebOS 客户端 |
|--------|-----------------|--------------|
| **目标用户** | 系统管理员、运维人员 | 普通社区用户 |
| **访问路径** | `/console/` | `/` |
| **认证方式** | OIDC (需管理员权限) | OIDC (普通用户) |
| **核心功能** | 用户管理、权限控制、系统监控 | 社区互动、内容浏览、个人中心 |
| **UI 风格** | 传统后台布局 (侧边栏+顶栏) | 桌面化 UI (WebOS) |
| **技术栈** | React 19 + @radish/ui | React 19 + @radish/ui + WebOS |

### 2.2 权限模型

Console 采用 **RBAC (基于角色的访问控制)** 模型：

```
用户 (User)
  ↓ 拥有
角色 (Role)
  ↓ 包含
权限 (Permission)
  ↓ 控制
资源 (Resource)
```

#### 2.2.1 预定义角色

| 角色 | 代码 | 说明 | 典型权限 |
|------|------|------|---------|
| **超级管理员** | `SuperAdmin` | 系统最高权限 | 所有权限 |
| **系统管理员** | `SystemAdmin` | 系统配置管理 | 用户管理、角色管理、系统配置 |
| **运维管理员** | `OpsAdmin` | 运维监控 | 系统监控、日志查询、定时任务 |
| **应用管理员** | `AppAdmin` | 应用管理 | OIDC 客户端管理 |
| **审计员** | `Auditor` | 只读审计 | 查看审计日志、导出报表 |

#### 2.2.2 权限粒度

权限采用 **资源:操作** 的命名规范：

```
格式: {Resource}:{Action}

示例:
- User:Read        # 查看用户
- User:Create      # 创建用户
- User:Update      # 更新用户
- User:Delete      # 删除用户
- Role:Assign      # 分配角色
- System:Monitor   # 系统监控
- Audit:Export     # 导出审计日志
```

#### 2.2.3 权限验证流程

```
用户请求 API
       ↓
Gateway 验证 access_token
       ↓
提取用户 ID 和角色
       ↓
查询用户权限列表
       ↓
       ├── 有权限 → 放行请求
       └── 无权限 → 返回 403 Forbidden
```

### 2.3 设计原则

#### 2.3.1 安全优先

- **最小权限原则**：默认无权限，显式授权
- **操作审计**：所有敏感操作记录审计日志
- **会话管理**：Token 过期自动跳转登录，支持 Single Sign-Out
- **敏感信息保护**：Client Secret 仅创建/重置时显示一次

#### 2.3.2 易用性

- **清晰的导航**：侧边栏菜单分类明确，支持折叠
- **即时反馈**：操作成功/失败立即提示
- **二次确认**：删除等危险操作需要确认
- **表单验证**：前端实时验证，后端二次验证

#### 2.3.3 可维护性

- **组件复用**：基于 @radish/ui 统一组件库
- **代码分层**：页面 (Pages) → 组件 (Components) → API (api/) → 工具 (utils/)
- **类型安全**：TypeScript 严格模式，完整的类型定义
- **错误处理**：统一的错误处理机制，友好的错误提示

#### 2.3.4 可扩展性

- **模块化设计**：功能模块独立，易于添加新模块
- **配置化菜单**：菜单项配置化，支持动态显示/隐藏
- **插件化架构**：预留插件接口，支持第三方扩展

### 2.4 核心实体

#### 2.4.1 用户 (User)

```typescript
interface User {
  id: string;                    // 用户 ID (雪花 ID)
  username: string;              // 用户名
  email: string;                 // 邮箱
  phoneNumber?: string;          // 手机号
  avatar?: string;               // 头像 URL
  status: UserStatus;            // 状态: Active | Disabled | Locked
  roles: Role[];                 // 用户角色列表
  createdAt: Date;               // 创建时间
  lastLoginAt?: Date;            // 最后登录时间
}

enum UserStatus {
  Active = 'Active',             // 正常
  Disabled = 'Disabled',         // 禁用
  Locked = 'Locked',             // 锁定
}
```

#### 2.4.2 角色 (Role)

```typescript
interface Role {
  id: string;                    // 角色 ID
  code: string;                  // 角色代码 (唯一)
  name: string;                  // 角色名称
  description?: string;          // 角色描述
  permissions: Permission[];     // 权限列表
  isSystem: boolean;             // 是否系统角色 (不可删除)
  createdAt: Date;               // 创建时间
}
```

#### 2.4.3 权限 (Permission)

```typescript
interface Permission {
  id: string;                    // 权限 ID
  code: string;                  // 权限代码 (Resource:Action)
  name: string;                  // 权限名称
  resource: string;              // 资源名称
  action: string;                // 操作类型
  description?: string;          // 权限描述
}
```

#### 2.4.4 OIDC 客户端 (OidcClient)

```typescript
interface OidcClient {
  id: string;                    // 客户端 ID
  clientId: string;              // Client ID (唯一)
  displayName: string;           // 显示名称
  description?: string;          // 描述
  type: ClientType;              // 类型: Internal | ThirdParty
  status: ClientStatus;          // 状态: Active | Disabled
  redirectUris: string[];        // 回调 URI 列表
  postLogoutRedirectUris: string[]; // 登出回调 URI 列表
  developerName?: string;        // 开发者名称
  developerEmail?: string;       // 开发者邮箱
  createdAt: Date;               // 创建时间
}

enum ClientType {
  Internal = 'Internal',         // 内部应用
  ThirdParty = 'ThirdParty',     // 第三方应用
}

enum ClientStatus {
  Active = 'Active',             // 启用
  Disabled = 'Disabled',         // 禁用
}
```

#### 2.4.5 审计日志 (AuditLog)

```typescript
interface AuditLog {
  id: string;                    // 日志 ID
  userId: string;                // 操作用户 ID
  username: string;              // 操作用户名
  action: string;                // 操作类型 (Create/Update/Delete)
  resource: string;              // 资源类型 (User/Role/Client)
  resourceId: string;            // 资源 ID
  details?: Record<string, any>; // 操作详情 (JSON)
  ipAddress: string;             // IP 地址
  userAgent: string;             // User Agent
  createdAt: Date;               // 操作时间
}
```

### 2.5 状态管理

Console 采用 **React Hooks + Context** 进行状态管理：

```typescript
// 全局状态
interface AppState {
  user: User | null;             // 当前登录用户
  permissions: string[];         // 用户权限列表
  isLoading: boolean;            // 全局加载状态
}

// 页面级状态
// 使用 useState/useReducer 管理页面状态
// 使用 useEffect 处理副作用
```

---

## 相关文档

- [功能模块](/guide/console-modules) - 详细功能说明
- [技术架构](/guide/console-architecture) - 技术实现细节
- [实施计划](/guide/console-roadmap) - 开发计划
