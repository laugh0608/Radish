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
      text: 当前进行中
      link: /planning/current
    - theme: alt
      text: 完整目录
      link: /README
features:
  - icon: ⚙️
    title: 一体化技术栈
    details: ASP.NET Core 10 + SQLSugar + PostgreSQL/SQLite + React 19 + Vite + TypeScript。
  - icon: 🧱
    title: 清晰的分层架构
    details: Common / Shared / Model / Repository / Service / Api 分层明确，便于协作与维护。
  - icon: 🖥️
    title: WebOS 桌面体验
    details: Desktop Shell、Dock、窗口系统统一承载论坛、商城、后台等应用。
  - icon: 🗂️
    title: 低噪音入口
    details: 首页只保留高频入口；详细目录、规划和日志拆分到独立页面，减少查找与上下文噪音。
---

## 快速入口

- [快速开始](/guide/getting-started) - 本地环境、依赖与启动方式
- [架构总览](/architecture/overview) - 系统拓扑、项目职责与关键约定
- [开发规范](/architecture/specifications) - 分层约束、编码规范与协作规则
- [前端设计](/frontend/design) - WebOS 架构与应用集成方式

## 当前协作

- [开发路线图总览](/development-plan) - 当前里程碑、主线与里程碑表
- [首版 dev 边界](/planning/dev-first-scope) - 首版 dev 的范围、完成标准与明确后置项
- [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix) - 首版范围内每条主线当前处于什么状态
- [当前进行中](/planning/current) - 现在真正要做的事
- [未来规划](/planning/backlog) - 后续候选节点与进入条件
- [开发日志总索引](/changelog/) - 年度 / 月度 / 周志入口

## 常用专题

- [认证与权限](/guide/authentication)
- [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
- [配置管理](/guide/configuration)
- [服务网关](/guide/gateway)
- [国际化指南](/architecture/i18n)
- [文档系统方案](/guide/document-system)
- [论坛投票 MVP 设计方案](/features/forum-poll-mvp)
- [论坛问答 MVP 设计方案](/features/forum-qa-mvp)

## 完整入口

- [完整文档目录](/README) - 查看更完整的固定文档入口
- [2026 年开发日志](/changelog/2026) - 当前技术栈阶段的年度记录
- [2025 年开发日志](/changelog/2025) - 新架构起点与历史方案记录

::: tip 阅读建议
如果是新会话或新同事接手，建议按这个顺序看：

1. [架构总览](/architecture/overview)
2. [开发规范](/architecture/specifications)
3. [当前进行中](/planning/current)
4. 需要回顾时再看 [开发日志总索引](/changelog/)
:::
