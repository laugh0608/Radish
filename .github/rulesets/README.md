# GitHub Rulesets

本目录存放仓库规则模板，当前以 `master` / `main` 分支保护为主。

## 建议流程

1. 日常开发提交到 `dev` 或功能分支。
2. 发版时从 `dev` 发起到默认分支（当前为 `master`，如切换可适配 `main`）的 Pull Request。
3. PR 需通过仓库质量检查，再按 `merge` 或 `rebase` 方式合并。
4. 合并到默认分支后，先把最新默认分支回灌并推送到 `dev`；可快进时优先 fast-forward，否则使用普通 merge。
5. 完成回灌后，再独立决定是否创建版本标签或发布 Release。

## 默认分支规则说明

- 禁止直接推送到受保护的默认分支（`master` / `main`）
- 禁止 force push
- 禁止删除分支
- 仅允许通过 Pull Request 合并
- 单人维护阶段不要求额外审批，但仍要求已解决会话
- 仅要求聚合检查 `Candidate Quality` 通过；它会汇总 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`、`Dependency Security`、`Backend Guard`、`Identity Guard` 六个组件结果
- 允许 `merge` 与 `rebase` 两种合并方式，禁用 `squash`
- 管理员仅可通过 Pull Request 方式绕过规则，不开放直接 push

## 现阶段策略

仓库历史中还存在一批已审计的 BOM / 末尾换行 / 尾随空格问题，因此远程 `Repo Hygiene` 使用全仓预算扫描：既允许 baseline 内的历史问题，也阻断任何新增问题。日常本地 `validate:ci` 仍使用 changed-only 入口，避免连续开发反复扫描无关文件。

远程 `Frontend Lint` 执行四个 Web workspace 的全量零 warning lint；本地 `validate:ci` 仍只检查本轮变更文件。发布前不再由第二个 PR workflow 重复执行同一套 lint。

`Backend Guard` 与 `Identity Guard` 会先按统一变更规则判定影响面；未命中时组件仍会以显式成功结束。后端命中时固定连接 PostgreSQL 17，执行 warning-as-error 构建与完整后端测试。

`Dependency Security` 是联网的 CI-only 组件：固定审计 npm 生产依赖与 NuGet 直接 / 传递依赖，存在 High / Critical、审计源不可用或 JSON 无法解析时均失败。它暂不并入日常本地 `validate:ci`，准备合并时可单独执行 `npm run check:dependency-security`。

`.github/workflows/candidate-quality.yml` 不再直接监听 PR；它只供手动候选与 Docker tag 发布复用。PR 由 `repo-quality.yml` 内的聚合 `Candidate Quality` 收口，因此同一提交只产生一个 workflow 结论和一封失败通知。

## 应用方式

如果仓库当前还没有对应 ruleset，可以使用 GitHub CLI 或 REST API 导入：

```bash
gh api repos/<owner>/<repo>/rulesets --method POST --input .github/rulesets/master-protection.json
```

如果仓库已经存在旧 ruleset，建议改用 `PUT /repos/{owner}/{repo}/rulesets/{ruleset_id}` 更新。

本目录模板还包含 Conventional Commits 的远端校验规则。当前远端 ruleset 未启用该规则；仅调整 Actions 触发策略、required checks 或合并方式时，应基于远端现状构造精确更新，不直接导入完整模板扩大门禁范围。

导入或更新前，请确认 `master-protection.json` 只要求 `.github/workflows/repo-quality.yml` 的聚合 job `Candidate Quality`；六个组件仍由仓库 contract 校验，不能从聚合依赖中静默移除。

`master-protection.json` 中的 `actor_id: 5` 按“RepositoryRole = Admin”模板生成，用于管理员仅通过 PR 绕过规则。
如果你的仓库类型或角色映射不同，导入前请按实际角色重新调整。

## 配套仓库设置

- 仓库 Merge options 中启用 `Rebase merging`
- 仓库 Merge options 中启用 `Merge commits`
- 关闭 `Squash merging`
- 如果后续增加 CODEOWNERS，再把 ruleset 中 `require_code_owner_review` 调整为 `true`
- 如果后续形成稳定的多人评审安排，再提高 `required_approving_review_count`；单人阶段不应把管理员 bypass 当作每次合并的常规路径
