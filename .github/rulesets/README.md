# GitHub Rulesets

本目录存放仓库规则模板，当前以 `master` 分支保护为主。

## 建议流程

1. 日常开发提交到 `dev` 或功能分支。
2. 发版时从 `dev` 发起到 `master` 的 Pull Request。
3. PR 需通过仓库质量检查，再执行 `squash` 或 `rebase` 合并。
4. 合并到 `master` 后再创建版本标签或发布 Release。

## master 规则说明

- 禁止直接推送到 `master`
- 禁止 force push
- 禁止删除分支
- 仅允许通过 Pull Request 合并
- 要求 1 个审批和已解决会话
- 要求 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`、`Identity Guard` 四个检查通过
- 限制合并方式为 `squash` / `rebase`
- 管理员仅可通过 Pull Request 方式绕过规则，不开放直接 push

## 现阶段策略

仓库历史中还存在一批旧文件的 BOM / 末尾换行 / 尾随空格问题，因此当前 `Repo Hygiene` 在 CI 中只检查本次变更文件。
这可以先把新增问题拦住，不会因为历史债务导致所有 PR 无法合并。

前端 `Frontend Lint` 也采用同样策略，只校验本次变更涉及的前端脚本文件。

`Identity Guard` 同样属于必需状态检查，但会先按变更文件判定是否命中身份语义影响面；未命中时 job 仍会以显式 skip 成功结束，确保 ruleset、workflow 与本地 `validate:ci` 口径一致。

## 应用方式

如果仓库当前还没有对应 ruleset，可以使用 GitHub CLI 或 REST API 导入：

```bash
gh api repos/<owner>/<repo>/rulesets --method POST --input .github/rulesets/master-protection.json
```

如果仓库已经存在旧 ruleset，建议改用 `PUT /repos/{owner}/{repo}/rulesets/{ruleset_id}` 更新。

导入或更新前，请确认 `master-protection.json` 中的 required check 名称与 `.github/workflows/repo-quality.yml` 的 job 名完全一致；`Repo Hygiene`、`Frontend Lint`、`Baseline Quick`、`Identity Guard` 任一名称漂移，都会导致 ruleset 无法正确识别通过状态。

`master-protection.json` 中的 `actor_id: 5` 按“RepositoryRole = Admin”模板生成，用于管理员仅通过 PR 绕过规则。
如果你的仓库类型或角色映射不同，导入前请按实际角色重新调整。

## 配套仓库设置

- 仓库 Merge options 中启用 `Squash merging` 与 `Rebase merging`
- 关闭 `Merge commits`
- 如果后续增加 CODEOWNERS，再把 ruleset 中 `require_code_owner_review` 调整为 `true`
