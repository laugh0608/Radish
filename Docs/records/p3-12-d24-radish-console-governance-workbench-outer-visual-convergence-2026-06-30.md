# P3-12-D24 radish.console 治理工作台外层语义收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`、`Frontend/radish.console/src/pages/Moderation/index.css`、`Frontend/radish.console/src/pages/Experience/ExperienceAdminHeader.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.tsx`。

## 目标

承接 D23 之后的 `P02 / P03` 工作台迁移顺位，将内容治理和经验治理从旧页头收敛到 D14 语义页头、状态 chip 与指标网格。该批只处理工作台外层识别、首屏指标和内容治理页局部 token，保留审核、治理、经验复核和冻结等业务契约。

## 代码变更

- `ModerationPage` 接入 `ConsolePageHeader`、`ConsoleStatusChip` 和 `ConsoleMetricGrid / ConsoleMetricCard`，首屏展示举报队列、本页举报、治理日志和筛选条件指标。
- `ModerationPage` 保留举报队列、手动治理动作、治理日志、审核弹窗、URL preset、筛选参数、分页和表格列不变。
- `Moderation/index.css` 将内容治理目标展示、审核预览、手动治理状态和状态卡的硬编码颜色替换为 Console 语义 token。
- `ExperienceAdminHeader` 改为复用 `ConsolePageHeader` 和 `ConsoleStatusChip`，按当前账号权限展示只读、可调经验或可复核冻结状态。
- `ExperienceAdminPage` 增加经验治理工作台指标，展示当前用户、总经验、异常命中和治理留痕。
- `ExperienceAdminPage` 保留用户查询、每日统计、异常规则、经验流水、复核、调经验、冻结 / 解冻和等级配置 API 不变。

## 停止线

- 不改内容审核、手动治理、治理动作日志或 URL 状态契约。
- 不改经验发放、复核、调经验、冻结 / 解冻和等级配置 API。
- 不在本批清完所有治理工作台内部 inline 样式；复杂区块内部样式继续按后续批次收口。
- 不执行真实 Gateway PC / mobile smoke；本批属于代码侧外层视觉收口，真实页面复核留到成组验收或用户明确要求时执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d24-radish-console-governance-workbench-outer-visual-convergence-2026-06-30.md`
- `git diff --check`

## 下一步

继续推进治理工作台内部区块样式收口，优先处理 `ExperienceObservationSummary`、`ExperienceTransactionSection`、`ExperienceGovernanceReviewSection` 和内容治理工作台内部提示 / 筛选区的 inline 样式与硬编码色；完成后再做 D23-D25 成组静态收口。
