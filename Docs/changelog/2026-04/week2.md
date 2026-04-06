# 2026-04 第二周 (04-06 ~ 04-12)

## 2026-04-06 (周一)

### M14 第一轮真实基线复核

- **启动前主路径已完成一轮真实通过验证**：用户已生成 `.tmp/baseline-host-report.md`，当前确认 `npm run validate:baseline:host` 全部通过；前端 `type-check`、`radish.client` 最小测试、Console 权限扫描、Repo Quality contract 自校验、身份语义影响面判定自校验、身份语义防回归扫描、后端构建 / 测试，以及 `DbMigrate doctor / verify` 均已通过。
- **启动后主路径已完成一轮真实通过验证**：用户已生成 `.tmp/host-runtime-report.md`，当前确认 `npm run check:host-runtime` 通过；`Gateway / Api / Auth` 最小运行态健康检查均返回 `200`，当前环境下 `M14` 的启动前、启动后两段默认主路径已闭合。
- **当前工程判断进一步收束**：截至 `2026-04-06`，`M14` 不再只停留在“入口、报告口径与文档已收口”，而是已经完成一轮真实环境下的基线通过验证；当前可将其判断更新为“默认执行入口已可用、最小宿主链可验证、后续转入日常维护与观察池跟踪”。

### 文档口径补对齐

- **首版状态矩阵已同步当前主线事实**：`planning/dev-first-status-matrix.md` 当前已从旧的“`M14` 仍是后续候选”修正为“`M14` 已是当前正式主线”，并同步把“下一阶段主线”描述从身份语义维护切换为宿主运行与最小可观测性基线，避免首版状态矩阵继续滞后于 `planning/current.md` 与 `development-plan.md`。
- **今日留痕已补到周志**：本次真实 `M14` 基线复核结果已沉淀到 `2026-04` 第二周日志，后续协作可直接引用本次记录，而不必再依赖会话上下文判断“当前主线路径是否真实跑通过”。

### `GetCommentTree` 观察池收缩

- **仓库内未使用的旧树方法已先行清理**：`ICommentService.GetCommentTreeAsync` 及其在 `CommentService` 中的实现当前已移除；仓库内主链仍保留的旧评论树相关资产，现已收缩到兼容控制器入口 `GetCommentTree`、`GetCommentTreeWithLikeStatusAsync` 与两个“仅用于观察旧调用”的 `HttpTest` 条目。
- **正式删除窗口的退出条件已明确**：`planning/current.md` 当前已把 `GetCommentTree` 的正式删除前置条件收束为三项同时成立：仓库内主链 / 前端 / `HttpTest` 已无必需依赖、兼容入口命中观测持续无有效命中、仓库外调用方依赖已完成事实确认；在此之前继续维持兼容观察态，不提前删除对外入口。
- **退场清单已补成独立指南**：当前已新增 [`GetCommentTree` 兼容入口退场清单](/guide/comment-tree-compat-retirement-checklist)，把观察窗口、日志检查、仓库外事实确认与正式删除顺序收束成同一份执行入口，避免后续继续依赖会话上下文判断“现在能不能删”。
