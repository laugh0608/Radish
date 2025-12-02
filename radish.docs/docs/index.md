---
layout: home
title: Radish 文档
titleTemplate: 现代社区与 WebOS 平台
hero:
  name: Radish
  text: 现代社区平台与 WebOS 文档
  tagline: 基于 .NET 10 + SQLSugar + PostgreSQL + React 19 的社区与桌面化体验
  actions:
    - theme: brand
      text: 开发快速上手
      link: /DevelopmentSpecifications
    - theme: alt
      text: 架构总览
      link: /DevelopmentFramework
    - theme: alt
      text: 前端 WebOS 设计
      link: /FrontendDesign
features:
  - icon: ⚙️
    title: 一体化技术栈
    details: 后端采用 ASP.NET Core 10 + SQLSugar + PostgreSQL，前端使用 React 19 + Vite + TypeScript，内建测试与日志体系。
  - icon: 🧱
    title: 清晰的分层架构
    details: 从 Common / Shared 到 Api / Gateway 的分层模型，配合 Repository + Service 模式，确保职责边界与可维护性。
  - icon: 🖥️
    title: WebOS 桌面体验
    details: 采用 Desktop Shell + Dock + 窗口系统的交互范式，统一承载论坛、商城、后台等多应用。
  - icon: 🚀
    title: 生产可用基线
    details: 预置健康检查、日志追踪、配置体系与部署规范，支持本地 SQLite 与生产 PostgreSQL。
---

## 文档地图

按角色和使用场景整理的推荐阅读路径：

### 面向开发者

- 开发规范：[/DevelopmentSpecifications](/DevelopmentSpecifications)
- 开发框架说明：[/DevelopmentFramework](/DevelopmentFramework)
- 前端设计说明：[/FrontendDesign](/FrontendDesign)
- Gateway 规划：[/GatewayPlan](/GatewayPlan)

### 认证与安全

- 认证与权限指南：[/AuthenticationGuide](/AuthenticationGuide)

### 部署与运维

- 部署指南：[/DeploymentGuide](/DeploymentGuide)
- 开发计划：[/DevelopmentPlan](/DevelopmentPlan)
- 开发日志：[/DevelopmentLog](/DevelopmentLog)

::: tip 如何开始？
如果你是第一次接触 Radish，推荐阅读顺序：

1. 浏览《开发框架说明》了解整体架构
2. 阅读《开发规范》掌握代码风格与约定
3. 根据《部署指南》启动本地环境
4. 参考《前端设计说明》熟悉 WebOS 桌面交互
:::

