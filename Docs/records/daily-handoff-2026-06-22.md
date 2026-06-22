# 2026-06-22 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-22 00:00 +0800"` 在本记录提交前回顾到今日 8 个提交；本记录自身作为收工文档提交追加。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `d35235cc docs(planning): 记录 B3 论坛作者态验收` | P3-12-B3 / 验收 | 补齐论坛作者态小阶段 Gateway PC / mobile smoke 和登录态发帖、编辑、历史、问答回答提交成功态记录。 |
| `28ce680a docs(planning): 裁决文档作者态归属` | P3-12-B4 / 设计 | 明确公开 `/docs` 只读、正式 Web 承接作者入口、Console 承接发布 / 权限 / 版本等治理动作。 |
| `08b0ab4d feat(client): 接入文档作者 Web 入口` | P3-12-B4-1 / Web 作者态 | 新增 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，内置文档编辑保持只读保护。 |
| `0ebc94ad docs(planning): 确认 B4-2 文档治理设计` | P3-12-B4-2 / 设计 | 明确 Console 文档治理动作、权限键、路由归属、API 授权资源、状态流转和验证口径。 |
| `cae304b1 feat(console): 接入文档治理入口` | P3-12-B4-2 / Console | 接入 `/console/documents`、治理专用读取 / 权限策略 API、Console 权限键、资源种子和权限覆盖矩阵。 |
| `1ec88e0b docs(planning): 启动 P3-12-D1 UI 设计准备` | P3-12-D1 / 设计准备 | 梳理 UI 设计页面矩阵、Pencil 设计源拆分和停止线；未进入视觉代码。 |
| `f1dd79b8 fix(console): 清理文档治理验收告警` | Console / 运行态修复 | 将 Console 文档治理页 `Space direction` 迁移为 `Space orientation`，消除阶段 smoke 中的浏览器告警。 |
| `50b1e09a docs(planning): 收口 P3-12 Web 入口与身份口径` | P3-12-B5 / B6 / 数据库口径 | 新增 B5 功能总入口设计和 B6 身份语义二次收口设计，删除未上线阶段历史发布脚本，统一上线前数据库结构口径。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录 B4 / B4-2 / B5 / B6 当前状态，并把明日事项调整为先实现 B5 `/workbench`，再推进 B6 代码前触点盘点与分批方案。
- 已同步 P3-12 专题：[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement) 已覆盖 B4 作者态、Console 文档治理、B5 功能总入口、B6 身份收口、D1 设计准备和发布候选数据库口径。
- 已同步 B4 设计与运行态事实：[P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)、[P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22) 和 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22) 均已对齐。
- 已同步 B5 / B6 专题：[P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22) 明确 `/workbench` 和 `/desktop` 边界；[P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22) 明确邮箱登录、`DisplayName`、`DisplayHandle`、`PublicId`、关注备注和旧字段退场。
- 已同步数据库结构说明：[数据库结构变更协作口径](/guide/database-schema-change-governance)、[架构框架](/architecture/framework)、[架构规范](/architecture/specifications)、[部署指南](/deployment/guide) 已统一为上线前以实体 + `Radish.DbMigrate` + 本地库重建为准；正式数据库存在后再生成发布 SQL。
- 已同步功能 / 治理说明：电子宠物、支付幂等、写操作可靠性和 WOG 历史记录已从旧脚本口径改为结构同步 / 正式数据库发布 SQL 条件口径。
- 本次收工补同步开发日志：[2026 年 6 月第 4 周开发日志](/changelog/2026-06/week4)、[2026 年 6 月开发日志](/changelog/2026-06)、[2026 年开发日志](/changelog/2026) 和 [记录与验收索引](/records/)。
- 已复核无需跟随更新范围：今天没有进入 UI 视觉实现、Flutter 新主线、PR、发布、完整 E2E、推荐算法、联邦社交、完整 PWA 或无关 WebOS 扩面清扫。

## 今日验证

代码 / 页面批次：

- B3 小阶段验收已覆盖 Gateway PC `1920x1080` 与移动 `390x844` CSS 视口，公开论坛列表、发帖登录回流、公开详情 canonical 和作者态 return path 通过；登录态发帖、作者编辑、编辑历史与问答回答提交成功态通过。
- B4 / D1 阶段 smoke 已覆盖公开文档、文档作者入口、Console 文档治理、`/messages` 和 `/desktop?app=chat`；Console 文档治理页告警修复后 `npm run type-check --workspace=radish.console` 通过。

文档收口批次：

- `git diff --check` 通过。
- `npm run check:repo-hygiene:changed` 通过文本卫生检查；仍提示 `Docs/architecture/specifications.md` 和 `Docs/deployment/guide.md` 既有篇幅超限。
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-b5-web-workbench-entry-design-2026-06-22.md Docs/records/p3-12-b6-identity-contract-convergence-design-2026-06-22.md` 通过。

运行态说明：

- 今日执行过 Gateway 真实页面 smoke，前提是用户明确说明前后端已启动。
- 后续真实 smoke 不沿用今天的运行状态；新会话需要用户再次明确说明前后端已启动后再执行。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)、[P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)、[P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22) 和本记录，确认当前第一顺位是 B5 `/workbench`。
2. 第一顺位：实现 `P3-12-B5` `/workbench` 功能总入口，按公开、登录态、Console 和桌面版历史入口组织能力清单；公共壳层“工作台”改指 `/workbench`，`/desktop` 改为“桌面版 / WebOS 历史入口”功能项。
3. B5 代码验证建议覆盖 `radish.client` 路由 / 壳层相关定向测试、`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client` 和 `git diff --check`；真实 Gateway PC / mobile smoke 等 B5 成组完成且用户明确说明前后端已启动后再执行。
4. 第二顺位：推进 `P3-12-B6` 代码前触点盘点，列出 Auth 注册登录、`User` 实体 / DTO、Bootstrap / CurrentUser、前端 store、论坛 / 聊天 / 搜索 / 艾特、Console 用户排障、种子数据和 DbMigrate 旧兼容逻辑的实际影响面。
5. B6 涉及接口、数据结构、权限设置或运行时行为变更，进入代码前先给出分批方案并等待确认；实现完成后提醒删除本地 SQLite 并重新初始化。
6. B5 / B6 收口前不进入 `P3-12-D` 视觉实现；后续 UI 专题仍按 Pencil 设计稿 -> 设计 / 说明文档 -> 代码实现 -> PC / mobile 复核推进。
