# P3-12-D25 radish.console 治理工作台内部区块样式收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.css`、`Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceObservationSummary.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceTransactionSection.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceGovernanceReviewSection.tsx`、`Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`、`Frontend/radish.console/src/pages/Moderation/ManualModerationActionSection.tsx`、`Frontend/radish.console/src/pages/Moderation/index.css`。

## 目标

承接 D24 外层语义收口，继续处理规划中点名的治理工作台内部区块样式。该批优先收口经验观察摘要、经验流水、经验复核区，以及内容治理内部提示 / 筛选区的 inline 样式与硬编码色，不改治理业务契约。

## 代码变更

- 新增 `ExperienceAdminPage.css`，承载经验治理内部动作组、区块间距、规则摘要卡、空态提示、筛选控件和表格间距样式。
- `ExperienceObservationSummary` 移除目标区块内的 inline layout、硬编码文本色、`rgba` 边框和白色背景，异常规则摘要卡统一使用 Console token。
- `ExperienceTransactionSection` 将筛选控件宽度、提示条间距、表格间距和空态颜色迁入样式类。
- `ExperienceGovernanceReviewSection` 将目标说明、草稿提示、复核表单、动作按钮组、治理留痕标题和表格间距迁入样式类。
- `ModerationPage` 和 `ManualModerationActionSection` 将举报队列 / 日志筛选控件宽度、上下文提示条间距和时长输入宽度迁入 `Moderation/index.css`。
- 点名目标组件已不再命中 `style=`、硬编码十六进制色或 `rgba(...)`。

## 停止线

- 不改举报审核、手动治理、治理动作日志、经验统计、经验流水、复核、冻结 / 解冻、调经验或等级配置 API。
- 不改表单字段、筛选参数、分页、表格列、权限判断、URL 状态和提交动作。
- 不在本批处理经验表格列定义、用户查询摘要、治理动作表单等剩余 inline 样式；这些留给后续成组静态检查按命中情况收口。
- 不执行真实 Gateway PC / mobile smoke；本批属于样式承载迁移，真实页面复核留到成组验收或用户明确要求时执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d25-radish-console-governance-workbench-internal-style-convergence-2026-06-30.md`
- `git diff --check`

## 下一步

推进 D23-D25 成组静态检查，重点复核角色权限、内容治理和经验治理页面的剩余 inline 样式、硬编码色、移动端宽度约束和语义组件扫描顺序；真实 Gateway PC / mobile smoke 继续留到阶段验收或用户明确要求时执行。
