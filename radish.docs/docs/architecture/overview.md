# 架构总览

> 本文提供 Radish 的“架构一页纸”视角：项目组成、运行拓扑、分层边界与关键约定。更细的实现细节见 [开发框架说明](/architecture/framework)，前端交互与 WebOS 范式见 [前端设计](/frontend/design)，里程碑与当前进度见 [开发路线图](/development-plan)。

## 1. 文档导航（先看什么）

- **想快速理解系统怎么组成**：本页（架构总览）
- **想知道代码分层/约束/规范**：[开发规范](/architecture/specifications)
- **想看更细的后端/网关实现要点**：[开发框架说明](/architecture/framework)
- **想看当前迭代计划与进度**：[开发路线图](/development-plan) / [开发日志](/changelog/)
- **想理解桌面化前端（WebOS）**：[前端设计](/frontend/design)
- **想理解统一入口与路由转发**：[Gateway 服务网关](/guide/gateway)

## 2. 系统拓扑（运行视图）

```text
浏览器访问 Gateway (https://localhost:5000)
        │
        ├─ /            → radish.client   (WebOS 桌面)
        ├─ /docs        → radish.docs     (VitePress)
        ├─ /console     → radish.console  (管理控制台)
        ├─ /api         → Radish.Api      (REST API)
        ├─ /connect/*   → Radish.Auth     (OIDC 端点)
        └─ /scalar      → API 文档 (Scalar)
```

> 说明：Gateway 作为对外统一入口，负责反向代理与门户能力；服务间的具体路由、健康检查聚合与部署方式见 [Gateway 服务网关](/guide/gateway)。

## 3. 主要项目与职责边界

- `Radish.Api`：业务 API 宿主（Controller/DI/认证授权/全局异常/Scalar/HealthChecks）
- `Radish.Auth`：OIDC 认证中心（授权码流程、Token/Session、客户端配置）
- `Radish.Gateway`：统一入口与反向代理（门户页、路由转发、健康检查聚合等）
- `radish.client`：普通用户前台（WebOS 桌面 + 内置应用）
- `radish.console`：管理员控制台（独立 SPA，权限更高，建议独立部署/入口）
- `radish.ui`：共享 UI 组件库（client/console 复用）
- `radish.docs`：文档站（VitePress，文档“唯一真相源”）

## 4. 后端分层（代码组织视角）

Radish 采用自研分层架构，核心约束如下（更完整规范见 [开发规范](/architecture/specifications)）：

- `Radish.Common`：通用工具层（仅允许引用外部包，不依赖内部业务层）
- `Radish.Shared`：共享常量/枚举/Options
- `Radish.Model`：实体/DTO/Vo 等模型
- `Radish.Infrastructure`：多租户、连接解析、SqlSugar 基础设施
- `Radish.IRepository` + `Radish.Repository`：仓储契约与实现（返回实体）
- `Radish.IService` + `Radish.Service`：用例编排与业务规则（对外返回 DTO/Vo）
- `Radish.Extension`：宿主扩展（Autofac/AutoMapper/Redis/Serilog/Swagger/Scalar 等注册）
- `Radish.Api`：宿主层（只做入口与薄 Controller，业务落在 Service）

## 5. 实时能力（通知/在线/交互）

- 实时交互统一建议使用 **SignalR**（默认 WebSocket，具备自动降级/重连/分组能力）。
- “通知未读数推送”规划见：[通知系统实时推送方案](/guide/notification-realtime)。

## 6. 安全与配置（关键约定）

- **传输安全**：对外入口强制 HTTPS（TLS 提供传输加密）；前端不做自定义“二次加密”。
- **密码安全**：密码在传输层由 HTTPS 保护，后端使用安全哈希（见 [密码安全](/guide/password-security)）。
- **配置与密钥**：敏感信息放在 `appsettings.Local.json` / 环境变量，禁止提交到仓库（见 [配置管理](/guide/configuration)）。
- **认证与权限**：统一走 OIDC + JWT/Policy（见 [认证与权限](/guide/authentication)）。

## 7. 里程碑与当前进度

- 里程碑与按周计划：[开发路线图](/development-plan)
- 每周/月度变更记录：[开发日志](/changelog/)
