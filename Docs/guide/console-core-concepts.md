# Console 核心概念

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 2 章

## 2. 核心概念

### 2.1 角色定位

Console 是管理员专用后台，与面向普通用户的 WebOS 客户端在职责上严格分离：

| 对比项 | Console 管理后台 | WebOS 客户端 |
|--------|-----------------|--------------|
| 目标用户 | `System / Admin` 为主，也支持被授权的非默认角色 | 普通社区用户 |
| 访问路径 | `/console` | `/` |
| 权限载体 | `CurrentUserVo.VoPermissions` | 业务角色与前台授权策略 |
| 关注点 | 管理、治理、配置、后台运营 | 社区互动、消费、个人中心 |
| 当前主线 | 权限治理收口 | 社区主链路稳定维护 |

### 2.2 当前权限模型

Console 当前不是“完整后台权限平台”，而是 **基于权限快照的轻量治理模型**。

```text
角色名
  ↓
RoleModulePermission
  ↓
ApiModule.LinkUrl
  ↓
ConsolePermissions 映射
  ↓
CurrentUserVo.VoPermissions
  ↓
RouteGuard / usePermission
```

### 2.2.1 核心名词

**权限键**
- 使用 `console.*` 命名空间
- 例如：`console.roles.view`、`console.products.toggle-sale`

**资源 URL**
- 即后端 `ApiModule.LinkUrl`
- 需要与真实路由保持一致；带路径参数时写正则版本
- 例如：`/api/v1/Sticker/DeleteSticker/.+`

**权限快照**
- 指 `CurrentUserVo.VoPermissions`
- 由后端在登录态读取时统一组装
- 前端不自行拼装，也不再依赖本地 `Admin` 回退规则推导全部能力

### 2.2.2 前后端职责分工

**前端**：
- `RouteGuard` 处理页面访问
- `usePermission` 处理菜单、按钮、局部区块可见性
- 页面在无访问权限时停止首屏请求

**后端**：
- `ConsolePermissions` 维护权限键与资源映射
- `UserService` 负责把角色资源派生为权限快照
- `DbMigrate` 保证默认角色和种子资源能落地到数据库

### 2.2.3 默认角色策略

当前默认策略如下：

- `System` / `Admin`：拥有 Console 默认权限全集
- 非默认角色：依赖数据库中的 `RoleModulePermission + ApiModule.LinkUrl` 派生快照

这意味着：

- 新增一个 Console 能力时，不能只补前端常量
- 必须同时考虑：前端权限键、后端资源映射、`DbMigrate` 种子、页面真实调用链路

### 2.2.4 当前边界策略

**纳入模型的能力**：
- 页面访问权限
- 菜单/搜索可见性
- 真实存在的按钮级操作
- 特殊后台入口（如 `Hangfire`）

**暂不纳入模型的能力**：
- 尚未落地的操作
- 只要求登录态的共享接口（除非明确决定纳入）
- 完整的权限配置平台、权限树、授权 UI

### 2.3 设计原则

#### 2.3.1 最小真实面

只治理已经真实存在的能力，不为未来可能实现的能力预留权限入口。

#### 2.3.2 同源配置

菜单、搜索、路由、页面权限判断应尽量来自同一份元数据，而不是各写一套规则。

#### 2.3.3 先资源后页面

如果页面真实调用了新的后台接口，就优先补 `ConsolePermissions` 与 `DbMigrate`，再谈页面继续扩张。

#### 2.3.4 特殊入口显式校验

像 `Hangfire` 这类不走普通页面请求链路的入口，也要显式消费权限快照，而不是写死角色放行。

### 2.4 当前关注的边界点

#### 2.4.1 共享上传接口边界

`Attachment/UploadImage` 目前同时服务于 Sticker 与头像上传，它更像共享能力，而不是纯 Console 专属资源。

当前已明确的处理原则是：

- 共享 URL 本身不直接并入 `ConsolePermissions + DbMigrate`
- 仅对 `Sticker` / `StickerCover` 的 `businessType` 做最小权限收口
- 复用现有 Sticker 权限键，不新建独立上传权限族

这样既能补齐 Sticker 后台真实链路的后端授权边界，也不会影响 `Avatar` 等其他登录态上传场景。

---

## 相关文档

- [Console 权限治理 V1](/guide/console-permission-governance)
- [Console 技术架构](/guide/console-architecture)
- [认证与权限](/guide/authentication)
