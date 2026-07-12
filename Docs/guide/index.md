# Guide 手册索引

本页是 `Docs/guide/` 的长期说明入口；批次记录、验收清单、模板和一次性评估已统一迁到 `Docs/records/`，避免说明书与留痕材料继续混放。

阅读原则：

- 想理解系统当前怎么工作，优先看本页的“长期说明”
- 想知道改了某个模块后该怎么验证，优先看 [专题回归索引](/guide/regression-index)
- 想找历史验收、批次记录、清单和模板，优先看 [记录与验收索引](/records/)

## 快速入口

- [快速开始](/guide/getting-started)
- [本地运行与排障手册](/guide/operations-runbook)
- [API 说明索引](/guide/api-index)
- [数据库总览](/guide/database-overview)
- [记录与验收索引](/records/)

## 一、长期说明

这些页面默认视为当前仍应持续维护的说明书入口。

### 基础运行与协作

- [快速开始](/guide/getting-started)
- [本地运行与排障手册](/guide/operations-runbook)
- [验证基线说明](/guide/validation-baseline)
- [页面真实联调与浏览器 Smoke 规则](/guide/browser-smoke)
- [专题回归索引](/guide/regression-index)
- [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)
- [产品版本与发布标识治理](/guide/version-governance)
- [时间语义与业务自然日](/guide/time-semantics)

### 基础设施与安全

- [配置管理](/guide/configuration)
- [运行时配置边界与系统设置](/guide/runtime-configuration-boundaries)
- [鉴权与授权指南](/guide/authentication)
- [OpenIddict 数据库与迁移](/guide/authentication-openiddict-database)
- [认证服务实现说明](/guide/authentication-service)
- [Auth 授权确认页信息层级说明](/guide/auth-consent-page)
- [Token 不活跃过期治理](/guide/auth-idle-session)
- [Gateway 服务网关](/guide/gateway)
- [数据库总览](/guide/database-overview)
- [数据库连接管理](/guide/database-connection)
- [数据库结构变更协作口径](/guide/database-schema-change-governance)
- [日志规范与实现说明](/guide/logging)
- [前端日志与敏感字段脱敏](/guide/frontend-logging)
- [密码安全](/guide/password-security)
- [密码传输与请求签名临时评审](/guide/password-transport-and-request-signature)
- [用户承诺与公开边界](/guide/user-commitments)
- [支付与转账幂等治理](/guide/payment-idempotency-governance)
- [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)
- [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)
- [速率限制](/guide/rate-limiting)

### 接口与发布运行

- [API 说明索引](/guide/api-index)
- [M14 宿主运行与最小可观测性基线](/guide/m14-host-runtime-observability-baseline)
- [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
- [产品版本与发布标识治理](/guide/version-governance)

### 文档、通知与控制台

- [文档系统方案](/guide/document-system)
- [通知系统 API 文档](/guide/notification-api)
- [通知中心设计说明](/guide/notification-center)
- [通知系统前端说明](/guide/notification-frontend)
- [Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff)
- [通知系统实现说明](/guide/notification-implementation)
- [通知系统实时推送方案](/guide/notification-realtime)
- [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit)
- [Console 权限治理 V1](/guide/console-permission-governance)
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)
- [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
- [Console 架构说明](/guide/console-architecture)
- [Console 系统说明](/guide/console-system)
- [系统设置治理专题](/guide/system-settings-governance)
- [Console 样式与 Token 使用说明](/frontend/console-style-guide)

### 领域专题说明

- Experience：
  - [experience-level-system](/guide/experience-level-system)
  - [experience-level-core-concepts](/guide/experience-level-core-concepts)
  - [experience-level-backend](/guide/experience-level-backend)
  - [experience-level-frontend](/guide/experience-level-frontend)
- Shop：
  - [shop-system](/guide/shop-system)
  - [shop-core-concepts](/guide/shop-core-concepts)
  - [shop-order](/guide/shop-order)
  - [shop-inventory](/guide/shop-inventory)
  - [shop-backend](/guide/shop-backend)
  - [shop-frontend](/guide/shop-frontend)
  - [shop-web-return-paths](/guide/shop-web-return-paths)
  - [shop-workspace-revisit](/guide/shop-workspace-revisit)
- Radish Coin：
  - [radish-coin-system](/guide/radish-coin-system)
  - [radish-coin-core-concepts](/guide/radish-coin-core-concepts)
  - [radish-coin-mechanisms](/guide/radish-coin-mechanisms)
- Radish Pit：
  - [radish-pit-system](/guide/radish-pit-system)
  - [radish-pit-core-concepts](/guide/radish-pit-core-concepts)
  - [radish-pit-backend](/guide/radish-pit-backend)
  - [radish-pit-frontend](/guide/radish-pit-frontend)
- Radish Pet：
  - [radish-pet-roadmap](/features/radish-pet-roadmap)

## 二、专项评审与迁移说明

这些页面不是日常快速上手入口，但仍属于“解释当前边界”的说明文档，而不是单纯历史记录。

- [identity-claim-migration](/guide/identity-claim-migration)
- [identity-claim-phase4-readiness](/guide/identity-claim-phase4-readiness)
- [identity-claim-phase4-rollout-window](/guide/identity-claim-phase4-rollout-window)
- [identity-claim-phase4-start-review](/guide/identity-claim-phase4-start-review)
- [identity-claim-protocol-consumers](/guide/identity-claim-protocol-consumers)
- [identity-claim-retention-matrix](/guide/identity-claim-retention-matrix)
- [identity-claim-external-compat-first-pass](/guide/identity-claim-external-compat-first-pass)

分发前置清单、安装包评估和批次级验证事实已统一转入 [记录与验收索引](/records/)，不再作为 `guide/` 默认入口。

### 阶段性路线与实现评审

以下页面保留实现阶段的路线判断、技术补充或评审结论，但默认不应作为“当前正式说明书入口”：

- [console-roadmap](/guide/console-roadmap)
- [experience-level-roadmap](/guide/experience-level-roadmap)
- [shop-roadmap](/guide/shop-roadmap)
- [radish-coin-roadmap](/guide/radish-coin-roadmap)
- [radish-pit-roadmap](/guide/radish-pit-roadmap)
- [shop-technical-enhancements](/guide/shop-technical-enhancements)
- [radish-coin-implementation-review-backend](/guide/radish-coin-implementation-review-backend)
- [radish-coin-implementation-review-frontend](/guide/radish-coin-implementation-review-frontend)

## 三、记录与验收

以下内容不建议作为“先看什么”的默认入口：

- 批次级变更记录
- 人工验收记录
- checklist / template
- 一次性 spike 观察
- 发布 / 回滚记录

统一入口见：

- [记录与验收索引](/records/)

## 相关文档

- [文档首页](/)
- [架构总览](/architecture/overview)
- [开发规范](/architecture/specifications)
