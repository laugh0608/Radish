# Console 管理后台系统设计方案

> 版本：v1.3 | 最后更新：2026-03-24 | 状态：Console-ext 一期持续补边界，当前已扩展到分类、内容治理、胡萝卜与经验等级后台

## 拆分说明

Console 管理后台系统文档较长，为便于阅读已拆分为多篇文档；章节编号保持不变。

## 文档导航

- [2. 核心概念](/guide/console-core-concepts) - Console 角色定位、权限快照、资源映射模型
- [3. 功能模块](/guide/console-modules) - 模块状态、页面边界、权限归属
- [4. 技术架构](/guide/console-architecture) - React Router、RouteGuard、权限快照与 API 集成
- [5. 实施计划](/guide/console-roadmap) - 当前阶段、收口策略与后续治理入口
- [Console 权限治理 V1](/guide/console-permission-governance) - 当前最重要的治理专题文档
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix) - 路由 / 前端权限 / 后端映射 / 种子的对照表
- [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1) - `Console-ext` 第一阶段的专题设计文档

---

## 1. 系统概述

Console 管理后台是 Radish 社区的统一管理平台，面向具备授权的后台治理角色提供管理能力。当前阶段的重点不是继续横向扩张为独立后台群，而是在同一套 Console 内把既有管理页面的权限边界、前后端授权口径、资源种子和 Console 入口权限稳定收口，并把分类、内容治理、胡萝卜与经验等级等已落地后台能力纳入统一模型。

当前认证接入方式已经明确为：Console 通过独立的 `radish-console` OIDC 客户端接入 Auth，拥有独立的授权回调与登出回调链路；它不再沿用早期“作为 `radish-client` 子应用复用认证”的旧口径。

### 1.1 核心设计原则

- **安全优先**：前端负责可见性，后端负责最终授权，特殊入口按权限快照而非角色硬编码放行
- **入口与能力分离**：`console.access` 是 Console 入口标记，但不再单独构成可见性；普通角色必须同时具备至少一个真实的 `console.*` 业务权限才允许进入
- **边界清晰**：只把真实已落地能力纳入权限模型，不为未完成能力保留伪入口
- **文档同源**：规划、架构、README、开发日志对当前进展使用同一口径
- **渐进收口**：优先补齐真实页面依赖的资源映射与种子，而不是一次性设计完整后台权限平台
- **后台统一承载**：举报审核、治理日志、分类管理、标签管理、胡萝卜管理、经验等级等管理功能统一集成在同一 Console 内，不额外拆独立 App

### 1.2 当前业务边界

**当前已形成闭环的模块**：

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

**当前重点不是继续扩张的新模块**：

- 审计日志中心
- 系统监控总览
- 权限配置后台
- 独立的权限树编辑器

**当前明确按“首版已接入”收口的治理边界**：

- 内容治理当前已集成到 Console 的 `Moderation` 页面，不单独拆治理 App
- 当前接入对象仅包含：`Post`、`Comment`
- 商品、聊天室消息等举报 / 审核目标仍在后续阶段评估，不在这一轮首版范围内

### 1.3 当前阶段定义

当前 Console 处于：

- **所属里程碑**：`M12 社区功能冲刺`
- **当前主线**：`Console-ext 一期实现、补边界与回归`
- **阶段状态**：已完成角色授权页、资源映射、真实接口收口、`console.access` 入口权限收口，以及分类/标签分管、内容治理、胡萝卜、经验等级后台首版接入，当前进入联调与回归阶段

### 1.4 推荐阅读顺序

如果是首次接手 Console 权限治理，建议按以下顺序阅读：

1. [Console 权限治理 V1](/guide/console-permission-governance)
2. [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
3. [Console 核心概念](/guide/console-core-concepts)
4. [Console 技术架构](/guide/console-architecture)
5. [Console 功能模块](/guide/console-modules)
6. [当前进行中](/planning/current)
