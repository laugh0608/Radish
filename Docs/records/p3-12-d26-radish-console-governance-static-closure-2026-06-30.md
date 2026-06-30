# P3-12-D26 radish.console 治理页面成组静态收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/pages/Roles/RolePermissionPage.tsx`、`Frontend/radish.console/src/pages/Roles/RolePermissionPage.css`、`Frontend/radish.console/src/pages/Roles/RoleForm.tsx`、`Frontend/radish.console/src/pages/Roles/RoleList.css`、`Frontend/radish.console/src/pages/Moderation/moderationPageColumns.tsx`、`Frontend/radish.console/src/pages/Moderation/index.css`、`Frontend/radish.console/src/pages/Experience/experienceAdminColumns.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceUserQuerySummary.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceGovernanceActionForms.tsx`、`Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.css`。

## 目标

承接 D23-D25 后的成组静态检查，复核角色权限、内容治理和经验治理页面的剩余 inline 样式、硬编码色、移动端宽度约束和语义组件扫描顺序。该批按扫描命中定向收口，不扩大到后端契约、权限逻辑、表单语义或真实运行态 smoke。

## 代码变更

- `experienceAdminColumns` 将表格列内弱文本、行内分组、观察摘要、正负经验变动、证据栈和预换行备注迁入 `ExperienceAdminPage.css`。
- `ExperienceUserQuerySummary` 和 `ExperienceGovernanceActionForms` 将查询区、指标区、冻结状态、全宽控件和按钮组迁入经验治理样式类。
- `moderationPageColumns` 将举报队列和治理日志表格列内弱文本迁入 `Moderation/index.css`。
- `RolePermissionPage` 移除资源树按层级计算的 inline padding，改由子节点容器和接口映射区 CSS 承载缩进，并在移动端收窄缩进。
- `RoleForm` 将排序数字输入的全宽样式迁入 `RoleList.css`。
- D26 目标目录已不再命中 `style=`、硬编码十六进制色或 `rgba(...)`。

## 停止线

- 不改角色 API、授权资源树、勾选继承、保存载荷、权限键 / 接口映射预览和路由守卫。
- 不改举报审核、手动治理、治理动作日志、经验统计、经验流水、复核、调经验、冻结 / 解冻或等级配置 API。
- 不改表单字段、筛选参数、分页、表格列、权限判断、URL 状态和提交动作。
- 不执行真实 Gateway PC / mobile smoke；本批属于静态代码侧样式收口，真实页面复核留到阶段验收或用户明确要求时执行。

## 验证

- `rg -n "style=|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\(" Frontend/radish.console/src/pages/Roles Frontend/radish.console/src/pages/Moderation Frontend/radish.console/src/pages/Experience`：无命中
- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d26-radish-console-governance-static-closure-2026-06-30.md`
- `git diff --check`

## 下一步

继续判断 Console 视觉代码实现是否需要进入运维任务外壳 / 系统工具成组检查，或先对 D14-D26 做一次更大范围静态收口与阶段记录；真实 Gateway PC / mobile smoke 仍只在阶段验收或用户明确要求且前后端已启动后执行。
