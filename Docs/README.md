# Docs 文档目录

Radish 项目的固定文档统一维护在 `Docs/` 目录。

## 入门与总览

- [文档首页](./index.md)
- [快速开始](./guide/getting-started.md)
- [页面真实联调与浏览器 Smoke 规则](./guide/browser-smoke.md)
- [开发路线图总览](./development-plan.md)
- [第三开发阶段：真实使用增长与长期契约治理](./planning/phase-three-real-usage-contract-governance.md)
- [P3-11 发布候选整备与轻量复访补齐](./planning/p3-11-release-candidate-light-revisit.md)
- [P3-12 Web 完全化与 WebOS 收束](./planning/p3-12-web-completion-webos-retirement.md)
- [P3-12-E 正式产品成熟度与质量硬化](./planning/p3-12-product-maturity-quality-hardening.md)
- [首版 dev 边界](./planning/dev-first-scope.md)
- [首版 dev 功能矩阵状态表](./planning/dev-first-status-matrix.md)
- [当前进行中](./planning/current.md)
- [Console 权限治理 V1](./guide/console-permission-governance.md)
- [Console 权限覆盖矩阵](./guide/console-permission-coverage-matrix.md)
- [Console 权限 / 菜单 / 按钮管理一期设计方案](./guide/console-authorization-phase1.md)
- [未来规划](./planning/backlog.md)
- [已完成摘要](./planning/archive.md)

## 架构与规范

- [Guide 手册索引](./guide/index.md)
- [架构总览](./architecture/overview.md)
- [开发规范](./architecture/specifications.md)
- [架构设计](./architecture/framework.md)
- [用户身份语义与公开索引](./architecture/user-identity-semantics.md)
- [API 说明索引](./guide/api-index.md)
- [数据库总览](./guide/database-overview.md)
- [认证与权限](./guide/authentication.md)
- [Token 不活跃过期治理](./guide/auth-idle-session.md)
- [运行时配置边界与系统设置](./guide/runtime-configuration-boundaries.md)
- [系统设置治理专题](./guide/system-settings-governance.md)
- [论坛内容发布可靠性与编辑历史治理](./guide/forum-content-write-reliability-governance.md)
- [服务网关](./guide/gateway.md)
- [部署指南](./deployment/guide.md)
- [本地运行与排障手册](./guide/operations-runbook.md)
- [文档篇幅治理](./guide/document-governance.md)

## 前端与交互

- [前端设计](./frontend/design.md)
- [前端多壳层策略](./frontend/shell-strategy.md)
- [Web UI 共享基座设计说明](./frontend/web-ui-foundation-design.md)
- [公开 Web 统一体验设计说明](./frontend/public-web-unified-experience-design.md)
- [私域与作者态 Web 工作流设计说明](./frontend/private-web-workflows-design.md)
- [Pencil 设计源目录](./frontend/design-sources/README.md)
- [纯 Web 私域复访入口](./frontend/private-web-revisit.md)
- [Flutter 移动端 handoff 与回流说明](./guide/flutter-mobile-handoff.md)
- [公开内容 SEO 与分享基线](./frontend/public-seo-sharing.md)
- [Radish 电子宠物](./features/radish-pet-roadmap.md)
- [Console 样式与 Token 使用说明](./frontend/console-style-guide.md)
- [视觉主题规范](./frontend/visual-theme-spec.md)
- [视觉颜色参考](./frontend/visual-color-reference.md)
- [UI 设计灵感参考](./frontend/ui-design-inspiration.md)
- [文档系统方案](./guide/document-system.md)
- [个人圈子](./features/circle.md)
- [论坛投票 MVP 设计方案](./features/forum-poll-mvp.md)
- [论坛问答 MVP 设计方案](./features/forum-qa-mvp.md)

## 记录与验收

- [记录与验收索引](./records/index.md)
- [首版 dev 总回归与发布前检查单](./records/dev-first-regression-checklist.md)
- [变更回归记录模板](./records/change-regression-record-template.md)
- [人工验收模板](./records/manual-acceptance-template.md)

## 日志与回顾

- [开发日志总索引](./changelog/)
- [2026 年开发日志](./changelog/2026.md)
- [2025 年开发日志](./changelog/2025.md)

## 说明

- `Docs/` 是项目固定文档的唯一真相源
- 固定文档会在 API 启动时自动同步到前端“文档”应用
- 在线文档由用户或管理员在应用内新建，内容存储在数据库
- 规划类文档已拆分为“总览 / 当前 / Backlog / 已完成摘要”，避免单页持续膨胀
- `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 等关键入口必须尽可能简约，只描述最近阶段、当前进度、执行入口和必要约束
- 文档篇幅按类型治理：入口 / 索引建议不超过 `300` 行、硬上限 `500` 行；架构 / 规范 / 设计建议不超过 `600` 行、硬上限 `900` 行；专题深度文档建议不超过 `800` 行、硬上限 `1200` 行
- `Docs/changelog/`、`Docs/records/` 和归档资料可放宽篇幅限制，但必须按日期、阶段或批次拆分并提供索引；超过 `1200` 行时应优先拆分或归档
- 历史批次、命令级验证流水、实现细节和长背景应写入 `Docs/changelog/`、`Docs/planning/archive.md` 或对应专题文档，避免 AI / Agent 在新会话中读取无关背景浪费上下文
