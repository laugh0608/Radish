# Console 实施计划

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 5 章

## 5. 实施计划

Console 当前不再适合用“Phase 1/2/3 大而全功能开发”描述。更准确的口径是：**基础框架已完成，当前处于 M12-P1 权限治理 V1 的收口阶段**。

## 5.1 已完成基础能力

以下基础能力已稳定落地：

- React 19 + TypeScript + Vite
- OIDC 登录、回调、登出
- React Router 路由系统
- `RouteGuard` 路由守卫
- `usePermission` 权限 Hook
- Dashboard / Applications / Users / Roles / Products / Orders / Tags / Stickers / SystemConfig / Hangfire 页面主链路
- 基于 `CurrentUserVo.VoPermissions` 的前端权限消费

## 5.2 当前主线：权限治理 V1

### 5.2.1 当前目标

- 统一页面访问、菜单、搜索与按钮权限口径
- 补齐真实页面依赖的后端资源映射与 `DbMigrate` 种子
- 清理未落地能力的伪入口
- 为本阶段定义清晰的退出条件

### 5.2.2 已完成事项

- [x] 路由级权限守卫收口
- [x] 页面内重复页面级判断清理
- [x] `Dashboard` 权限种子闭环
- [x] `Hangfire` 权限种子闭环
- [x] `Users` 误暴露权限入口收口
- [x] `Products / Stickers` 辅助接口资源种子补齐
- [x] 第二批、第三批页面权限闭环完成

### 5.2.3 当前待办

- [x] 文档、规划、README 口径统一
- [x] 共享接口边界决策：`Attachment/UploadImage`
- [x] 形成权限覆盖矩阵（路由 / 前端常量 / 后端映射 / `DbMigrate`）

### 5.2.4 当前不做

- [ ] 新建权限配置后台
- [ ] 扩张系统监控总览
- [ ] 引入独立权限树编辑器
- [ ] 将所有登录态共享接口一次性纳入 Console 权限模型

## 5.3 V1 退出条件

满足以下条件即可认为 V1 完成：

1. 已治理页面的前后端权限边界稳定
2. 真实调用的 Console 专属接口全部完成资源与种子对齐
3. 共享接口边界至少完成一次明确决策
4. 文档与规划口径统一，不再保留大面积过期待办

## 5.4 V1 之后的建议方向

V1 收口后，建议从下面两条路径中选择一条进入下一阶段：

### 路径 A：治理收尾工具化

- 权限覆盖矩阵
- 简单扫描脚本
- 防回归校验规则

### 路径 B：边界明确后的能力扩展

- 共享上传权限策略平台化（当前仅完成 Sticker 链路最小收口）
- 真正落地的用户详情 / 用户统计后台链路
- 更完整的后台运营能力，但必须建立在 V1 冻结之后

## 5.5 关联专题

- [Console 权限治理 V1](/guide/console-permission-governance)
- [当前进行中](/planning/current)
- [开发路线图](/development-plan)
