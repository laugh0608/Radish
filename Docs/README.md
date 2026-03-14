# Docs 文档目录

Radish 项目的固定文档统一维护在 `Docs/` 目录。

## 入门与总览

- [文档首页](./index.md)
- [开发路线图总览](./development-plan.md)
- [当前进行中](./planning/current.md)
- [Console 权限治理 V1](./guide/console-permission-governance.md)
- [Console 权限覆盖矩阵](./guide/console-permission-coverage-matrix.md)
- [未来规划](./planning/backlog.md)
- [已完成摘要](./planning/archive.md)

## 架构与规范

- [开发规范](./architecture/specifications.md)
- [架构设计](./architecture/framework.md)
- [认证与权限](./guide/authentication.md)
- [服务网关](./guide/gateway.md)
- [部署指南](./deployment/guide.md)

## 前端与交互

- [前端设计](./frontend/design.md)
- [文档系统方案](./guide/document-system.md)
- [论坛投票 MVP 设计方案](./features/forum-poll-mvp.md)
- [论坛问答 MVP 设计方案](./features/forum-qa-mvp.md)

## 日志与回顾

- [开发日志总索引](./changelog/)
- [2026 年开发日志](./changelog/2026.md)
- [2025 年开发日志](./changelog/2025.md)

## 说明

- `Docs/` 是项目固定文档的唯一真相源
- 固定文档会在 API 启动时自动同步到前端“文档”应用
- 在线文档由用户或管理员在应用内新建，内容存储在数据库
- 规划类文档已拆分为“总览 / 当前 / Backlog / 已完成摘要”，避免单页持续膨胀
