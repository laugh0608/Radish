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
      link: /guide/getting-started
    - theme: alt
      text: 架构总览
      link: /architecture/framework
    - theme: alt
      text: 前端 WebOS 设计
      link: /frontend/design
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

- [快速开始](/guide/getting-started) - 开发环境搭建和基础概念
- [开发规范](/architecture/specifications) - 代码风格、目录结构与分层约定
- [开发框架说明](/architecture/framework) - 整体架构与技术栈介绍
- [认证与权限](/guide/authentication) - OIDC 认证流程与权限体系
- [配置管理](/guide/configuration) - 配置文件结构与最佳实践

### 前端开发

- [前端设计](/frontend/design) - WebOS 架构与应用集成方式
- [UI 组件库](/frontend/ui-library) - @radish/ui 统一组件库概览
- [API 客户端](/frontend/api-client) - 统一的 API 请求封装
- [错误处理](/frontend/error-handling) - 统一的错误处理机制
- [DataTable 组件](/frontend/data-table) - 数据表格组件详细文档
- [组件开发指南](/frontend/development) - 组件开发工作流与热更新

### 架构与规划

- [Gateway 规划](/architecture/gateway-plan) - API 网关设计与实施计划
- [国际化指南](/architecture/i18n) - 多语言支持方案
- [开放平台](/features/open-platform) - 第三方应用接入指南

### 部署与运维

- [部署指南](/deployment/guide) - 容器化部署与生产环境配置
- [开发计划](/development-plan) - 项目里程碑与迭代计划
- [开发日志](/changelog/) - 按月份记录的开发历程

::: tip 如何开始？
如果你是第一次接触 Radish，推荐阅读顺序：

1. 浏览[开发框架说明](/architecture/framework)了解整体架构
2. 阅读[开发规范](/architecture/specifications)掌握代码风格与约定
3. 根据[快速开始](/guide/getting-started)启动本地环境
4. 参考[前端设计](/frontend/design)熟悉 WebOS 桌面交互
:::
