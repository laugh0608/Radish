---
layout: home
title: Radish 文档
titleTemplate: 现代社区与多端平台
hero:
  name: Radish
  text: 现代社区平台与多端文档
  tagline: 基于 .NET 10 + SQLSugar + PostgreSQL + React 19 的纯 Web、Flutter 与工作台体验
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
    title: 纯 Web 与工作台入口
    details: 普通浏览器默认进入纯 Web 公开入口，/desktop 保留 WebOS 工作台能力。
  - icon: 🗂️
    title: 低噪音入口
    details: 首页只保留高频入口；详细目录、规划和日志拆分到独立页面，减少查找与上下文噪音。
---

## 快速入口

- [快速开始](/guide/getting-started) - 环境、初始化、验证与启动方式
- [页面真实联调与浏览器 Smoke 规则](/guide/browser-smoke) - 页面联调默认覆盖 PC 与移动端视图
- [架构总览](/architecture/overview) - 系统拓扑、项目职责与关键约定
- [开发规范](/architecture/specifications) - 分层约束、编码规范与协作规则
- [前端设计](/frontend/design) - 纯 Web、Flutter、WebOS 保留入口与应用集成方式
- [前端多壳层策略](/frontend/shell-strategy) - 多端壳层职责分工
- [Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff) - 移动原生来源返回、登录回流与公开链接口径

## 当前协作

- [开发路线图总览](/development-plan) - 当前主线、下一顺位与维护线
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance) - `P3-2 PublicId` 首批实现与下一顺位入口
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform) - 已归档阶段总目标、拆分与优先级
- [前端多壳层策略](/frontend/shell-strategy) - 公开内容、桌面工作台与 Flutter 客户端的职责分工
- [当前进行中](/planning/current) - 现在真正要做的正式主线
- [未来规划](/planning/backlog) - 当前明确后置的事项
- [已完成摘要](/planning/archive) - 第一开发阶段与历史里程碑结论
- [首版 dev 边界](/planning/dev-first-scope) - 第一开发阶段归档参考
- [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix) - 第一开发阶段归档参考
- [开发日志总索引](/changelog/) - 年度 / 月度 / 周志入口

## 常用专题

- [Guide 手册索引](/guide/)
- [认证与权限](/guide/authentication)
- [Token 不活跃过期治理](/guide/auth-idle-session)
- [API 说明索引](/guide/api-index)
- [数据库总览](/guide/database-overview)
- [本地运行与排障手册](/guide/operations-runbook)
- [文档篇幅治理](/guide/document-governance)
- [记录与验收索引](/records/)
- [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
- [配置管理](/guide/configuration)
- [服务网关](/guide/gateway)
- [视觉主题规范](/frontend/visual-theme-spec)
- [视觉颜色参考](/frontend/visual-color-reference)
- [国际化指南](/architecture/i18n)
- [文档系统方案](/guide/document-system)
- [公开内容 SEO 与分享基线](/frontend/public-seo-sharing)
- [个人圈子](/features/circle)
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

::: warning 入口文档约束
关键入口文档只保留最近阶段、当前进度、执行入口和必要约束，建议不超过 300 行，硬上限 500 行。架构 / 规范 / 设计类文档建议不超过 600 行，硬上限 900 行；专题深度文档建议不超过 800 行，硬上限 1200 行。历史批次、命令级验证流水、实现细节和长背景应放到日志、记录、归档或专题文档，避免新会话读取过多无关上下文。
:::
