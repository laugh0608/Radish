# Console 权限治理 V1

> 最后更新：2026-03-24
> 状态：进行中（持续补边界，进入回归阶段）

本文档用于统一 Console 权限治理的设计口径、当前完成范围、剩余清单与退出条件。

## 1. 目标

Console 权限治理 V1 只解决以下问题：

- 页面、菜单、全局搜索、直链访问的权限口径统一
- 前端可见性控制与后端资源授权形成最小闭环
- 非默认角色可通过 `ApiModule + RoleModulePermission` 派生出稳定的 Console 权限快照
- 未落地能力不再以按钮、入口、权限常量等形式继续暴露

V1 **不追求**完整的后台 RBAC 平台，也不在本阶段展开权限配置 UI、权限树编辑器、批量授权面板等扩张项。

## 2. 当前实现链路

当前 Console 权限链路如下：

```text
ApiModule.LinkUrl + RoleModulePermission
        ↓
UserService 汇总角色资源
        ↓
ConsolePermissions.GetPermissionsByApiUrl(...)
        ↓
CurrentUserVo.VoPermissions
        ↓
Route Meta / RouteGuard / usePermission
        ↓
菜单、搜索、页面、按钮可见性控制
```

### 2.1 前端职责

- `RouteGuard` 负责页面访问边界，阻止手输 URL 越权进入
- 菜单与全局搜索复用同一份路由权限元数据
- 页面内部只保留按钮级 / 操作级权限控制
- 页面在无访问权限时停止首屏请求，避免“按钮隐藏但接口照打”

### 2.2 后端职责

- `ConsolePermissions` 负责 Console 权限键与资源 URL 的映射
- `UserService` 负责把角色资源映射为 `VoPermissions`
- `DbMigrate` 负责种子化 `ApiModule` 与默认角色授权，保证非默认角色也可通过数据库授权获得能力
- `HangfireAuthorizationFilter` 等特殊入口负责消费权限快照，而不是硬编码角色放行

### 2.3 `console.access` 当前语义

`console.access` 当前不再等同于“只要拥有就能看见 Console”。实际规则如下：

- `Admin / System` 角色仍直接具备 Console 可见性
- 其他角色必须同时满足：
  - 拥有 `console.access`
  - 至少拥有一个真实的 Console 页面访问权限（即路由元数据中的 `requiredPermission`）
- 后端权限快照会自动补齐 / 剔除 `console.access`
  - 若角色已拥有任一真实 Console 页面访问权限，则自动补上 `console.access`
  - 若角色只剩 `console.access`、没有任何真实页面访问权限，则自动移除该入口权限
- 角色授权保存时，若勾选了任一非入口 Console 资源，也会自动补上入口资源，避免产生脏授权

## 3. 已完成范围

截至 2026-03-24，以下事项已完成：

### 3.1 路由与前端可见性

- 路由入口统一接入 `RouteGuard`
- 菜单 / 搜索 / 路由同源化
- 页面内重复页面级权限判断已收口到路由层
- 首页无 Dashboard 权限时自动回退到首个可访问页面

### 3.2 已形成闭环的模块

- `Dashboard`
- `Applications`
- `Users`
- `Roles`
- `Categories`
- `Products`
- `Orders`
- `Tags`
- `Stickers`
- `Moderation`
- `Coins`
- `Experience`
- `SystemConfig`
- `Hangfire`

### 3.3 已完成的资源种子补齐

近期几轮已完成的补齐点包括：

1. 角色管理首批闭环
2. 第二批页面闭环：`Users / Applications / SystemConfig`
3. 第三批页面闭环：`Products / Orders / Tags / Stickers`
4. 路由级守卫收口
5. 页面内重复判断清理
6. `Dashboard` 权限种子闭环
7. `Hangfire` 权限种子闭环
8. `Users` 误暴露权限入口收口
9. `Products / Stickers` 辅助接口资源种子补齐
10. `Categories / Moderation / Coins / Experience` 首版资源映射与种子补齐
11. `console.access` 从“单独放行”收口为“入口标记 + 真实页面权限联动”

## 4. 当前明确边界

### 4.1 已下线的伪能力

`Users` 页中以下能力已从前后端权限口径中移除：

- 创建用户
- 更新用户状态
- 重置密码
- 强制下线
- 删除用户

原因是这些能力当前没有稳定的后端落地点，继续暴露只会制造“看得见但不可用”的伪能力入口。

### 4.2 当前仍不纳入 V1 的能力

以下事项不纳入本阶段：

- 权限配置 UI / 权限树编辑器
- 共享上传能力的完整权限平台化
- 审计日志查询、系统监控、日志检索等新模块扩张
- 未在 Console 页面真实调用的后端接口资源补齐

### 4.3 内容治理边界

内容治理当前已明确采用“**集成在 Console 中，不单独拆独立治理 App**”的方案。

当前首版只覆盖：

- 举报提交
- 审核队列
- 审核联动禁言 / 封禁
- 治理动作日志
- `Post / Comment / ChatMessage / Product` 四类目标

当前暂不覆盖：

- 批量治理、敏感词、自动化策略

### 4.4 默认测试角色边界

为避免“普通用户误见 Console”与“普通用户继承后台权限”两类脏授权，默认 `Test` 角色当前额外执行以下收口：

- 回收全部 `RoleConsoleResource`
- 回收除 `/api/v1/User/GetUserByHttpContext` 之外的默认 `RoleModulePermission`
- 不再允许通过种子数据默认获得 `Tags`、`Moderation`、`Coins`、`Experience` 等后台能力
- 若旧开发库仍残留历史 `RoleConsoleResource / RoleModulePermission`，需重新执行种子或手工清理后再验证入口可见性

## 5. 边界决策记录

### 5.1 `Attachment/UploadImage` 已完成边界决策

当前 `Stickers` 与 `UserProfile` 均直接调用 `/api/v1/Attachment/UploadImage`，但该接口不适合直接进入 Console 共享 URL 资源映射与种子体系。

该问题已在 V1 收口阶段完成决策，并按最小改动落地到后端 `businessType` 级别授权边界。

决策备选曾包括：

- **方案 A：不纳入 V1**
  - 保持共享上传接口为登录态能力
  - V1 只维护 Console 专属业务资源映射
- **方案 B：纳入 V1，但仅收口 Sticker 业务链路**
  - 以 `console.stickers.create/edit/batch-upload` 归属上传能力
  - 不额外新增“上传”独立权限键
- **方案 C：建立独立上传权限键**
  - 进入下一阶段，不建议在 V1 收口末期引入

当前推荐：**优先采用方案 A 或 B，不建议在 V1 尾声引入新的权限族**。

已确认并落地：**方案 B（最小实现）**

- 仅对 `Attachment/UploadImage` 的 `Sticker` / `StickerCover` 业务类型做收口
- 复用既有 `console.stickers.create/edit/batch-upload` 权限键
- 不新增独立“上传”权限键
- 不把共享上传 URL 直接并入通用 `ConsolePermissions + DbMigrate` 资源映射，避免影响 `Avatar` 与用户侧 `General` 上传

## 6. V1 收口清单

### 6.1 已完成

- [x] 路由、菜单、搜索权限同源化
- [x] 页面级重复判断收口到 `RouteGuard`
- [x] `Dashboard / Hangfire / Applications / Users / Roles / Products / Orders / Tags / Stickers / SystemConfig` 页面权限闭环
- [x] `Categories / Moderation / Coins / Experience` 页面权限闭环
- [x] `Dashboard` 资源映射与种子闭环
- [x] `Hangfire` 资源映射与种子闭环
- [x] `Products / Stickers` 真实在用辅助接口资源补齐
- [x] `Users` 伪能力入口与无效权限常量清理
- [x] `console.access` 入口权限与真实页面权限联动收口

### 6.2 进行中

- [x] 共享接口边界决策：`Attachment/UploadImage`
- [x] 文档、规划、README 口径统一
- [x] 形成权限覆盖矩阵（路由 / 前端常量 / 后端映射 / `DbMigrate`）
- [x] 轻量扫描脚本：`npm run check:console-permissions`

### 6.3 本阶段不做

- [ ] 新增权限配置后台
- [ ] 新增完整审计日志模块
- [ ] 新增监控报表、趋势图、资源监控页
- [ ] 把所有登录态接口都一并纳入 Console 权限模型

## 7. V1 退出条件

满足以下条件即可认为 Console 权限治理 V1 收口完成：

1. 已接入权限治理的 Console 页面全部具备稳定的页面访问边界
2. 已真实调用的 Console 专属后端接口全部完成 `ConsolePermissions + DbMigrate` 对齐
3. 共享接口边界至少完成一次明确决策并记录
4. 不再继续出现“页面入口已显示，但接口未落地 / 未授权”的新增裂缝
5. 规划文档与专题文档对“当前已完成 / 当前不做 / 下一步收尾项”达成统一口径
6. Console 入口权限不再出现“测试用户仅因拥有 `console.access` 就看见 Console”的脏授权裂缝

## 8. 参考文档

- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)

## 9. 下一步建议

当前建议已从“补工具”切换为“冻结边界后的回归维护”：

1. 权限链路变更后先运行 `npm run check:console-permissions`
2. 只在真实新增页面/接口时增量补矩阵与文档
3. 内容治理保持集成在 Console，后续只对真实新增的治理对象增量补矩阵与文档
4. 不再继续横向扩张新的权限族或共享接口映射

扫描脚本落地后，Console 权限治理 V1 应进入“冻结边界、只做回归维护”的阶段。
