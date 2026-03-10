# Console 技术架构

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 4 章

## 4. 技术架构

### 4.1 前端架构

#### 4.1.1 技术栈

| 技术 | 用途 |
|------|------|
| React 19 | UI 框架 |
| TypeScript | 类型系统 |
| React Router | 路由系统 |
| Vite / Rolldown | 构建工具 |
| `@radish/ui` | 统一 UI 组件与 HTTP 客户端能力 |

#### 4.1.2 当前目录关注点

```text
Frontend/radish.console/src/
├── api/                # 后台 API 封装
├── components/         # 共享布局与通用组件
├── constants/          # Console 权限常量等
├── hooks/              # usePermission、标题等 Hook
├── pages/              # 业务页面
├── router/             # 路由定义、权限元数据、RouteGuard
├── services/           # token / 认证上下文能力
└── utils/              # logger 等工具
```

#### 4.1.3 路由与权限元数据

当前 Console 已切换到 **React Router + 路由元数据驱动**：

- 路由定义中声明页面访问权限
- `RouteGuard` 统一处理页面访问边界
- 菜单和全局搜索复用同一份路由元数据
- 页面内部不再重复写页面级“无权限占位返回”

这解决了过去“菜单隐藏了，但搜索或直链还能进入”的裂缝。

#### 4.1.4 页面内部权限职责

页面内部当前只负责两类判断：

1. **按钮/操作级可见性**
   - 例如编辑、删除、重试、批量上传
2. **无访问权限时停止请求**
   - 避免仅由路由层兜底，但页面 effect 仍先发请求

### 4.2 API 客户端与特殊上传场景

#### 4.2.1 统一 API 客户端

普通 API 调用统一使用 `@radish/http` / `@radish/ui` 提供的客户端，不再维护 Console 自定义 fetch 封装。

统一收益包括：

- token 注入一致
- 响应解析一致
- 错误处理口径一致
- baseUrl 配置一致

#### 4.2.2 特殊场景：上传进度

像 `Sticker` 图片上传这类需要上传进度的场景，允许使用 `XMLHttpRequest`，但必须：

- 从 `getApiClientConfig()` 获取 `baseUrl` 与 token
- 只在上传等确有必要的场景使用
- 不再额外复制一套普通 HTTP 客户端逻辑

### 4.3 后端权限快照组装

后端当前通过以下步骤组装 Console 权限：

```text
CurrentUser roles
  ↓
System/Admin 默认权限全集
  ↓
RoleModulePermission 查询角色资源
  ↓
ApiModule.LinkUrl
  ↓
ConsolePermissions.GetPermissionsByApiUrl(...)
  ↓
CurrentUserVo.VoPermissions
```

### 4.4 `DbMigrate` 在权限治理中的职责

`DbMigrate` 当前承担两类工作：

1. 创建 Console 已依赖的 `ApiModule`
2. 为默认角色补齐 `RoleModulePermission` 种子

因此，新增一个 Console 能力时，至少要检查四处是否一致：

- 路由/页面是否真实调用
- 前端权限常量是否存在
- `ConsolePermissions` 是否有 URL 映射
- `DbMigrate` 是否有资源与默认授权种子

### 4.5 特殊入口

#### 4.5.1 Hangfire

`Hangfire` 不属于普通 React 页面资源，但它已经被纳入 Console 权限治理：

- 资源映射：`/hangfire(/.*)?`
- 权限键：`console.hangfire.view`
- 校验方式：`HangfireAuthorizationFilter` 显式消费当前用户权限快照

这类入口后续继续沿用“显式校验权限快照”的策略。

---

## 相关文档

- [Console 权限治理 V1](/guide/console-permission-governance)
- [Console 核心概念](/guide/console-core-concepts)
