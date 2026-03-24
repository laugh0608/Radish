# Console 权限 / 菜单 / 按钮管理一期设计方案

> 最后更新：2026-03-24
> 状态：一期已实现，进入联调与边界补齐

关联文档：
- [开发路线图](/development-plan)
- [当前进行中](/planning/current)
- [未来规划](/planning/backlog)
- [Console 管理后台系统设计方案](/guide/console-system)
- [Console 权限治理 V1](/guide/console-permission-governance)
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)

---

## 1. 背景

Console 权限治理 V1 已完成“页面可见性 + 路由访问边界 + 真实接口资源映射 + 默认角色种子”闭环，当前链路已经可以稳定支撑：

- 路由访问控制
- 左侧菜单可见性
- 全局搜索结果过滤
- 页面内按钮显隐
- `Hangfire` 等特殊入口权限收口

当前已解决的一期核心问题包括：

1. **角色授权配置已落地**
   - 已新增角色授权读取 / 保存接口
   - 已在 `Roles` 下提供角色权限配置页

2. **资源目录与接口映射已落地**
   - 已新增 `ConsoleResource / RoleConsoleResource / ConsoleResourceApiModule`
   - 已为当前已纳管模块补齐资源种子与接口映射种子

3. **真实接口授权已接入一期链路**
   - 当前已纳管的 Console 管理接口已从单纯 `SystemOrAdmin` 收口到“Client + Console 动态权限校验”

当前联调阶段仍需重点关注的问题是：

1. **环境构建问题仍待排查**
   - 本地 .NET SDK 存在 `MSB4276 workload resolver` 环境问题
   - 前端 `vite build` 在当前环境中存在 `spawn EPERM`

2. **需要补充 Console 入口权限联调**
   - 当前已补独立入口权限 `console.access`
   - 需要验证 WebOS Console 图标显示、Console SPA 访问与模块权限之间的联动
   - 需要验证“只有 `console.access` 但没有真实后台能力”的角色不会误见 Console

因此，当前已进入 `Console-ext` 一期联调阶段：在不重做整套权限平台的前提下，验证这套**最小可用、可落地、可维护**的授权配置能力。

---

## 2. 目标

一期只解决以下问题：

- 为现有 Console 模块补齐“角色授权配置”能力
- 统一菜单、页面、按钮、接口之间的授权关系
- 让非 `System/Admin` 的角色可以通过配置获得真实可用的 Console 能力
- 保持与现有 `ApiModule + ConsolePermissions + VoPermissions` 链路兼容
- 不引入新的、脱离现有项目约束的大而全权限平台

### 2.1 一期完成后的理想状态

完成后，应具备以下能力：

1. 可在 `角色管理` 下为角色配置 Console 页面与按钮权限
2. 菜单显示、路由访问、按钮显隐与接口放行使用同一套权限键
3. `GetUserByHttpContext` 返回的 `VoPermissions` 来源于真实角色授权数据
4. `Hangfire` 等特殊入口继续在统一资源模型下工作
5. 新增 Console 页面时，有明确的“资源 -> API -> 角色授权 -> 前端消费”标准流程

### 2.2 一期补充项：Console 入口权限

为避免 WebOS 将 Console 应用入口继续硬编码为 `Admin/System` 可见，一期补充引入独立入口权限：

- 权限键：`console.access`
- 作用：
  - 控制 `radish.client` 中 Console 应用图标是否可见
  - 控制 `radish.console` SPA 是否允许进入
- 与现有页面/按钮权限关系：
  - `console.access` 是入口标记，不再单独代表“允许进入 Console”
  - 普通角色必须同时拥有 `console.access` 与至少一个真实 `console.*` 业务权限，才会看到并允许进入 Console
  - 进入 Console 后，菜单、页面、按钮、接口仍由原有 `console.*` 细粒度权限控制

这样可以把“应用入口”和“模块能力”拆开，避免出现“已经配置了模块权限，但 WebOS 看不到 Console 入口”的问题。

---

## 3. 一期范围

### 3.1 本阶段包含

- 现有 Console 模块的资源目录建模
- 角色授权读取与保存能力
- `角色管理` 下的授权配置页
- 菜单 / 页面 / 按钮 / 接口关系说明与数据落地
- Console 真实使用接口的动态授权收口
- 资源种子、授权种子与回归校验文档同步

### 3.2 本阶段不包含

- 动态菜单设计器
- 权限树编辑器
- 用户-角色分配后台
- 独立“权限中心”多菜单平台
- 所有登录态共享接口的一次性纳管
- `authOnly` 页面（如 `/profile`、`/settings`）纳入 Console 专属权限模型
- 审计中心、监控总览等新模块扩张

### 3.3 边界原则

- **菜单不是独立权限体系**：菜单只是页面权限的展示方式
- **Console 入口权限独立于菜单权限**：`console.access` 只作为入口标记，不参与菜单树生成，也不再单独放行
- **按钮权限继续复用 `console.*` 命名**：不新开第二套按钮命名规则
- **接口不让运营侧手工勾 URL**：接口映射由后端维护，授权页只配置页面/按钮资源
- **先覆盖当前已治理模块**：不超前设计未来未落地模块

---

## 4. 现状梳理

### 4.1 当前前端链路

当前 Console 前端已经具备：

- `routeMeta` 定义页面访问权限
- `RouteGuard` 统一拦截页面访问
- 菜单 / 搜索复用同一份路由元数据
- 页面内部通过 `usePermission` 控制按钮显隐

现状落点：

- `Frontend/radish.console/src/router/routeMeta.ts`
- `Frontend/radish.console/src/router/index.tsx`
- `Frontend/radish.console/src/hooks/usePermission.ts`
- `Frontend/radish.console/src/constants/permissions.ts`

### 4.2 当前后端链路

当前 Console 后端权限链路为：

```text
Role / UserRole
  ↓
RoleModulePermission + ApiModule
  ↓
ConsolePermissions.GetPermissionsByApiUrl(...)
  ↓
UserService.GetPermissionKeysByRolesAsync(...)
  ↓
CurrentUserVo.VoPermissions
  ↓
RouteGuard / usePermission
```

现状落点：

- `Radish.Common/PermissionTool/ConsolePermissions.cs`
- `Radish.Service/UserService.cs`
- `Radish.DbMigrate/InitialDataSeeder.Identity.cs`
- `Radish.Api/Filters/HangfireAuthorizationFilter.cs`

### 4.3 当前缺口

当前缺口主要有三类：

1. **授权配置缺口**
   - 没有角色授权 UI
   - 没有资源目录与授权快照的专门服务

2. **资源语义缺口**
   - `ApiModule` 主要表达接口 URL
   - 菜单、页面、按钮仍主要停留在前端常量与文档层

3. **最终授权缺口**
   - 许多 Console 控制器仍使用 `SystemOrAdmin`
   - 无法真正支撑“已授权普通角色”的后端调用放行

---

## 5. 核心设计

### 5.1 统一资源视角

一期引入统一的 Console 资源目录，但不替换现有 `ApiModule` 的接口资源职责。

建议新增三类概念：

- **入口访问权限**
  - 表示“能否看到并进入 Console 应用”
  - 使用独立权限键 `console.access`
  - 不对应普通 React 页面，也不生成侧边栏菜单
  - 对普通角色而言，还必须与至少一个真实 `console.*` 业务权限联动才生效

- **页面资源**
  - 表示“能否访问某个 Console 页面”
  - 对应路由、菜单、搜索入口
  - 例如：`console.roles.view`

- **按钮资源**
  - 表示“能否在页面内执行某个操作”
  - 对应页面中的新增、编辑、删除、启停、重试等按钮
  - 例如：`console.roles.edit`

- **入口资源**
  - 表示“不属于普通 React 页面，但需要纳管的后台入口”
  - 例如：`console.hangfire.view`

接口资源仍然由 `ApiModule` 表示。

### 5.2 菜单 / 页面 / 按钮 / 接口关系

一期明确采用以下关系：

```text
页面资源（决定菜单、搜索、路由访问）
  ├─ 按钮资源（决定页面内操作显隐）
  └─ 关联的接口资源 ApiModule
```

更具体地说：

1. **Console 入口**
   - 不属于普通菜单项
   - 由独立权限 `console.access` 作为入口标记
   - WebOS 显示 Console 图标、Console SPA 本身放行，除 `Admin/System` 外还依赖至少一个真实 Console 业务权限

1. **菜单**
   - 不单独配置权限
   - 菜单是否显示，取决于页面资源是否已授权

2. **页面**
   - 页面资源代表“访问这个页面”的权限
   - 页面资源同时关联页面首屏所依赖的查看类接口

3. **按钮**
   - 按钮资源代表“执行这个动作”的权限
   - 按钮资源关联动作对应的写接口，必要时可复用查看详情接口

4. **接口**
   - 不直接暴露给授权页让用户逐条勾选
   - 由后端维护“资源 -> ApiModule”映射
   - 请求到达接口时，由接口映射反查所需资源，再校验当前用户是否具备对应权限键

### 5.3 典型例子

以 `角色管理` 为例：

- 页面资源：`console.roles.view`
  - 关联：
  - `Role/GetRoleList`
  - `Role/GetRoleById`

- 按钮资源：`console.roles.create`
  - 关联：
  - `Role/CreateRole`

- 按钮资源：`console.roles.edit`
  - 关联：
  - `Role/GetRoleById`
  - `Role/UpdateRole`

- 按钮资源：`console.roles.delete`
  - 关联：
  - `Role/DeleteRole`

因此：

- 左侧菜单“角色管理”显示，由 `console.roles.view` 决定
- `/roles` 页面可进入，由 `console.roles.view` 决定
- 页面中的“编辑”按钮是否显示，由 `console.roles.edit` 决定
- `UpdateRole` 是否可调用，由 `console.roles.edit` 最终决定

---

## 6. 后端方案

### 6.1 数据模型

一期建议新增三张表。

### 6.1.1 `ConsoleResource`

用于维护 Console 侧的资源目录。

建议字段：

- `Id`
- `ResourceKey`
- `ResourceName`
- `ResourceType`
- `ParentId`
- `ModuleKey`
- `RoutePath`
- `Icon`
- `OrderSort`
- `ShowInSidebar`
- `ShowInSearch`
- `Description`
- `IsEnabled`
- `IsDeleted`
- 审计字段

建议资源类型枚举：

- `Page`
- `Button`
- `Entry`

说明：

- `Page` 资源可显示在授权树中，并可用于菜单、路由、搜索
- `Button` 资源挂在 `Page` 之下
- `Entry` 用于 `Hangfire` 之类特殊入口

### 6.1.2 `RoleConsoleResource`

用于维护角色与 Console 资源的授权关系。

建议字段：

- `Id`
- `RoleId`
- `ConsoleResourceId`
- `IsDeleted`
- 审计字段

### 6.1.3 `ConsoleResourceApiModule`

用于维护 Console 资源与后端接口资源的映射关系。

建议字段：

- `Id`
- `ConsoleResourceId`
- `ApiModuleId`
- `RelationType`
- `IsDeleted`
- 审计字段

`RelationType` 一期可选值：

- `View`
- `Action`

该字段主要用于辅助理解和调试，一期不承载复杂业务语义。

### 6.2 与现有模型的关系

### 保留不变

- `ApiModule`：继续表示后端接口 URL 资源
- `RoleModulePermission`：继续保留，作为兼容层存在
- `ConsolePermissions`：继续保留，作为权限键常量来源

### 新旧职责划分

- `ConsoleResource` 负责表达 Console 页面/按钮/入口资源
- `ApiModule` 负责表达后端接口资源
- `ConsoleResourceApiModule` 负责把二者关联起来
- `RoleConsoleResource` 负责角色授权

### 为什么不直接扩展 `ApiModule`

原因如下：

1. `ApiModule` 当前已被广泛用于 URL 级资源种子与正则匹配
2. 页面/按钮/菜单不应与接口 URL 混在同一语义层
3. 若强行复用，会让“一个资源到底是路由、按钮还是接口”更加混乱

因此一期应采用“**ConsoleResource 管前端资源，ApiModule 管接口资源**”的双层模型。

### 6.3 Vo / DTO 设计

建议新增以下对象。

### ViewModel

#### `ConsoleResourceVo`

- `VoId`
- `VoResourceKey`
- `VoResourceName`
- `VoResourceType`
- `VoParentId`
- `VoModuleKey`
- `VoRoutePath`
- `VoIcon`
- `VoOrderSort`
- `VoShowInSidebar`
- `VoShowInSearch`
- `VoDescription`
- `VoIsEnabled`

#### `ConsoleResourceTreeNodeVo`

- `VoId`
- `VoTitle`
- `VoResourceKey`
- `VoResourceType`
- `VoChecked`
- `VoIndeterminate`
- `VoChildren`

#### `RoleAuthorizationSnapshotVo`

- `VoRoleId`
- `VoRoleName`
- `VoGrantedResourceIds`
- `VoGrantedPermissionKeys`
- `VoDerivedApiModules`
- `VoLastModifyTime`

#### `ResourceApiBindingVo`

- `VoResourceId`
- `VoResourceKey`
- `VoApiModuleId`
- `VoApiModuleName`
- `VoLinkUrl`

### DTO

#### `SaveRoleAuthorizationDto`

- `RoleId`
- `ResourceIds`
- `ExpectedModifyTime`

一期建议只做“整页覆盖保存”，不做复杂增量 patch。

### 6.4 Repository / Service / Controller

### Repository

建议新增：

- `IConsoleResourceRepository`
- `IRoleConsoleResourceRepository`
- `IConsoleResourceApiModuleRepository`

如第一轮复杂度较低，也可先基于 `IBaseRepository<TEntity>` 起步，再视复杂查询补专属仓储。

### Service

建议新增：

- `IConsoleResourceService`
  - 查询资源树
  - 查询资源与接口映射
  - 处理资源同步与只读组装

- `IRoleAuthorizationService`
  - 读取角色授权快照
  - 保存角色授权
  - 校验父子节点关系

- `IConsolePermissionSnapshotService`
  - 根据当前角色集合计算权限键列表
  - 根据接口 URL 反查所需 Console 资源

### Controller

建议新增 `ConsoleAuthorizationController`，仅承载授权后台相关接口。

建议接口：

- `GET /api/v1/ConsoleAuthorization/GetResourceTree`
- `GET /api/v1/ConsoleAuthorization/GetRoleAuthorization`
- `POST /api/v1/ConsoleAuthorization/SaveRoleAuthorization`
- `GET /api/v1/ConsoleAuthorization/GetRolePermissionPreview`

一期不提供资源目录的增删改接口，资源目录仍由代码 + 文档 + 种子维护。

### 6.5 权限快照组装

一期完成后，`VoPermissions` 的推荐来源为：

```text
CurrentUser roles
  ↓
System/Admin 默认权限全集
  ↓
RoleConsoleResource
  ↓
ConsoleResource.ResourceKey
  ↓
CurrentUserVo.VoPermissions
```

接口授权则通过：

```text
Request Path
  ↓
ApiModule
  ↓
ConsoleResourceApiModule
  ↓
ConsoleResource.ResourceKey
  ↓
CurrentUser permissions
```

### 兼容策略

一期迁移期建议采用“新模型优先，旧模型兜底”：

- `VoPermissions` 优先从 `RoleConsoleResource` 派生
- 对于尚未迁移的链路，保留 `RoleModulePermission + ApiModule -> ConsolePermissions` 兼容
- 迁移完成后，再考虑是否逐步收缩旧链路职责

### 6.6 接口授权策略调整

一期中，已接入 Console 权限治理的接口，建议逐步从 `SystemOrAdmin` 迁移为“登录 + Console 动态权限校验”。

迁移目标包括：

- `RoleController`
- `ClientController`
- `UserController` 中 Console 管理相关接口
- `ShopController` 的 Console 管理接口
- `TagController`
- `StickerController`
- `SystemConfigController`
- `StatisticsController`
- `HangfireAuthorizationFilter`

保留规则：

- `System/Admin` 仍有默认全集，作为高权限兜底
- 但非默认角色的授权应通过角色授权配置真实生效

---

## 7. 前端方案

### 7.1 页面入口

一期不新增独立“权限中心”菜单，而是挂在现有 `角色管理` 之下。

建议新增路由：

- `/roles/:roleId/permissions`

特点：

- 不显示在侧边栏
- 不进入全局搜索
- 入口由 `RoleList` 中的“权限配置”按钮进入

### 7.2 页面结构

建议页面名称：`RolePermissionPage`

页面结构建议：

1. 顶部角色信息卡
   - 角色名称
   - 描述
   - 状态
   - 权限范围

2. 中部双栏区
   - 左侧：资源授权树
   - 右侧：当前勾选资源对应的接口预览

3. 底部操作区
   - 保存
   - 重置
   - 返回角色列表

### 7.3 授权树规则

授权树按模块组织，例如：

- Dashboard
- Applications
- Users
- Roles
- Products
- Orders
- Tags
- Stickers
- SystemConfig
- Hangfire

每个模块下：

- 页面资源作为一级节点
- 按钮资源作为子节点

交互规则：

1. 勾选按钮时，自动勾选所属页面
2. 取消页面时，自动取消全部子按钮
3. 页面部分子按钮被选中时，页面显示半选
4. 没有按钮的入口资源直接单节点展示

### 7.4 菜单与搜索策略

一期不改现有菜单生成方式：

- 侧边栏仍由 `routeMeta` 生成
- 全局搜索仍由 `routeMeta` 生成
- 授权页只是改变 `VoPermissions`

因此：

- 只要某角色获得某页面资源，前端菜单、搜索、路由守卫就会自然生效
- 不需要额外维护一份数据库驱动菜单配置

---

## 8. 一期接口设计

建议最小接口集如下。

### 8.1 获取资源树

`GET /api/v1/ConsoleAuthorization/GetResourceTree`

用途：

- 页面初始化时读取完整资源树
- 前端据此渲染授权树

返回：

- `ConsoleResourceTreeNodeVo[]`

### 8.2 获取角色授权快照

`GET /api/v1/ConsoleAuthorization/GetRoleAuthorization?roleId=`

用途：

- 回显某角色当前已授权的资源

返回：

- `RoleAuthorizationSnapshotVo`

### 8.3 保存角色授权

`POST /api/v1/ConsoleAuthorization/SaveRoleAuthorization`

请求：

- `SaveRoleAuthorizationDto`

效果：

- 覆盖该角色当前一期纳管范围内的 Console 授权

### 8.4 获取接口预览

`GET /api/v1/ConsoleAuthorization/GetRolePermissionPreview?roleId=`

用途：

- 帮助前端展示“这个角色当前能访问哪些接口”
- 用于授权页右侧预览与调试

---

## 9. 数据与种子策略

### 9.1 资源目录来源

一期资源目录不开放后台自助维护，统一通过以下来源生成：

1. `ConsolePermissions` 权限键常量
2. `routeMeta` 当前已接入的页面
3. `Console 权限覆盖矩阵`
4. `DbMigrate` 种子

### 9.2 资源初始化原则

只初始化当前已纳管模块：

- Dashboard
- Applications
- Users
- Roles
- Products
- Orders
- Tags
- Stickers
- SystemConfig
- Hangfire

不初始化：

- `/profile`
- `/settings`
- `/theme-test`
- 当前未真实落地的新模块

### 9.3 默认角色授权

默认角色策略：

- `System`
  - 仍默认拥有一期资源全集

- `Admin`
  - 仍默认拥有一期资源全集

- `console.access`
  - 默认授予 `System/Admin`
  - 其他角色通过显式授权或权限快照联动补齐
  - 若其他角色没有任何真实 Console 业务权限，则不应仅保留该入口权限

- 其他角色
  - 默认为空
  - 通过授权页显式配置

### 9.4 幂等要求

`DbMigrate` 中新增的一期种子必须满足：

- 可重复执行
- 可修正旧数据
- 不因字段顺序或显示文案变化产生重复资源

---

## 10. 实施拆分

建议按四步推进。

### 第 1 步：数据与资源建模

- 新增 `ConsoleResource`
- 新增 `RoleConsoleResource`
- 新增 `ConsoleResourceApiModule`
- 完成一期资源与映射种子

### 第 2 步：后端读取与保存能力

- 新增授权读取/保存 Service
- 调整 `GetUserByHttpContext` 的 `VoPermissions` 组装逻辑
- 新增 `ConsoleAuthorizationController`

### 第 3 步：前端授权页面

- `RoleList` 增加“权限配置”按钮
- 新增 `/roles/:roleId/permissions`
- 完成授权树、保存、回显、接口预览

### 第 4 步：接口最终授权迁移

- 把当前已纳管的 Console 接口切到动态权限校验
- `HangfireAuthorizationFilter` 接入统一授权服务
- 更新权限覆盖矩阵与回归脚本口径

---

## 11. 验收标准

满足以下条件即可认为一期完成：

1. `Roles` 页面可进入角色授权配置页
2. 角色授权可回显、可保存、可重新加载
3. 菜单显示与路由访问由页面资源驱动
4. 页面按钮显隐由按钮资源驱动
5. 已纳管 Console 接口能根据角色授权真实放行或拒绝
6. `System/Admin` 默认全集不受回归影响
7. `Hangfire` 仍可通过统一权限模型控制
8. 文档、种子、接口实现与前端页面口径保持一致

---

## 12. 风险与决策记录

### 12.1 为什么一期不做动态菜单设计器

因为当前菜单已经和 `routeMeta`、`RouteGuard`、全局搜索形成稳定同源关系。

若一期就把菜单改成数据库驱动，会同时影响：

- 路由配置
- 菜单结构
- 搜索入口
- 文档与覆盖矩阵

风险过高，不适合作为首轮落地方式。

### 12.2 为什么不让授权页直接配置接口 URL

因为：

- URL 正则配置对使用者不友好
- 容易误配
- 违背当前 Console“按页面和按钮理解权限”的产品心智

因此接口应由后端维护映射，授权页只展示预览，不直接让用户逐条勾 URL。

### 12.3 为什么不立即推翻 `RoleModulePermission`

因为当前系统已经围绕它存在：

- `PermissionRequirementHandler`
- 若干种子数据
- 既有权限文档

一期应优先补齐新能力并保持兼容，不在同一轮里做大规模删除与清理。

---

## 13. 二期以后预留

如果一期落地稳定，后续可再考虑：

- 独立权限中心菜单
- 用户与角色分配后台
- 更完整的资源目录维护
- 共享接口的更平台化治理
- 审计日志与授权变更记录
- 更完整的权限树编辑与批量授权

这些都不应成为一期的前置条件。

---

## 14. 结论

Console 权限 / 菜单 / 按钮管理一期的推荐落地方式是：

- 继续沿用现有 `console.*` 权限键
- 把菜单视为页面资源的展示结果，而不是独立权限体系
- 把按钮视为页面下的操作资源
- 把接口资源继续留在 `ApiModule`
- 通过“Console 资源目录 + 角色授权 + 资源到接口映射”补齐最小可用授权后台

这样可以在不打碎现有 Console 权限治理 V1 成果的前提下，平滑进入 `Console-ext` 第一阶段，并为后续真正可配置的授权能力建立稳定基线。
