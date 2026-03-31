# ADR 0001: Branch And PR Governance

更新时间：2026-03-31

## 状态

Accepted

## 背景

`Radish` 当前已经进入持续开发、跨层联调与发布后回归并行推进的阶段。  
仓库内已经存在以下治理资产：

- `dev` 作为当前阶段的日常集成分支
- `master` 的 ruleset 模板
- PR 模板
- `Repo Quality` GitHub Actions 工作流
- `Docs/guide/validation-baseline.md` 中统一的最小验证基线
- `AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 三份长期同步的 AI 协作文件

如果这些规则只零散存在于脚本、workflow、协作文件和口头约定中，而没有一份正式决策文档统一收口，后续容易出现以下问题：

- 分支角色与 PR 目标分支口径再次漂移
- GitHub ruleset、PR 模板与仓库文档逐步失配
- 多人或多 Agent 协作时，对“什么改动该进 `dev`、什么情况下才能面向 `master`”理解不一致
- 发布前 required checks、验证基线与协作规范无法稳定复用

因此需要用 ADR 将当前仓库的分支与 PR 治理策略正式固化。

## 决策

仓库采用以下分支与 PR 治理策略：

### 分支角色

- `master`: 稳定主线，只接受 Pull Request 合并
- `dev`: 当前阶段默认集成分支，功能、文档、规范、治理类改动默认合并到这里
- `feature/*`: 功能开发分支
- `docs/*`: 文档、规范、ADR、流程口径调整分支
- `chore/*`: 基础设施、脚本、CI、仓库治理分支
- `hotfix/*`: 仅用于必须直接修复稳定主线问题的紧急分支

### 合并策略

- 默认开发流程为 `feature/*` / `docs/*` / `chore/*` -> `dev`
- 阶段性稳定后，再通过 PR 将 `dev` 合并到 `master`
- 仅在必须修复稳定主线问题时，才允许 `hotfix/*` 直接向 `master` 发 PR
- 常规功能、文档、规范与治理类改动，不直接面向 `master`

### `master` 规则

- 禁止直接 push
- 禁止 force push
- 禁止删除分支
- 必须通过 PR 合并
- 必须通过仓库检查
- 管理员仅可通过 PR 方式绕过规则，不开放直接 push
- 合并方式限制为 `squash` / `rebase`
- 提交信息需符合 Conventional Commits

### `dev` 规则

- 允许作为当前阶段默认目标分支
- 当前阶段不强制启用 branch protection
- 仍建议保持分支开发与 PR 合并习惯，避免重新回到直接堆叠提交的模式
- 如后续 `dev` 成为多人并行开发主集成面，再评估是否逐步补强保护规则

## 需要在 GitHub 仓库设置中完成的动作

以下规则不能仅靠仓库文件完全强制，需要仓库管理员在 GitHub Settings 中启用或持续维护：

1. 确保远端存在 `dev` 分支，并作为当前默认开发集成面
2. 默认开发 PR 目标分支保持为 `dev`
3. 对 `master` 启用 branch protection / ruleset
4. 对 `master` 要求通过 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 三个状态检查
5. 对 `master` 开启 “Require a pull request before merging”
6. 限制 `master` 的合并方式为 `squash` / `rebase`
7. 管理员仅通过 Pull Request 方式绕过规则，不开放直接 push
8. `dev` 当前不作为强保护分支处理

## 仓库内已落地的支撑项

为配合该决策，仓库内当前已同步具备以下支撑资产：

- PR 模板
  - `.github/PULL_REQUEST_TEMPLATE.md`
  - 已收口变更说明、验证记录、文档/配置/数据库影响、风险与后续事项
- GitHub Ruleset 模板
  - `.github/rulesets/master-protection.json`
  - `.github/rulesets/README.md`
- GitHub Actions PR 检查工作流
  - `.github/workflows/repo-quality.yml`
  - 当前包含 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 三个 job
  - 当前在 `pull_request -> master/dev` 上触发，其中 `master` ruleset 使用的 required check 名称按 job 名配置
- 统一验证基线说明
  - `Docs/guide/validation-baseline.md`
  - 已明确 `validate:baseline:quick`、`validate:baseline`、`validate:baseline:host` 的使用边界
- 多入口 AI 协作文件
  - `AGENTS.md`、`CLAUDE.md`、`GEMINI.md`
  - 当前已明确要求三份文件保持“基本复制”和长期同步，避免协作规范分叉

## 影响

正面影响：

- `master` 可以继续作为稳定主线，而不是日常开发堆叠区
- `dev` 可以作为当前阶段真实的集成面，承接频繁迭代
- PR 模板、验证基线、ruleset 与 workflow 之间的口径更容易长期保持一致
- 多人或多 Agent 协作时，分支目标与 required checks 的判断成本更低
- 发布前 required checks、验证基线和协作规则可以围绕同一套治理口径复用

代价：

- 开发流程从“直接提交”切换为“分支 + PR + 检查”
- 需要持续维护 GitHub Settings 中的 ruleset / protection 配置
- 随着验证基线或 workflow 演进，ADR、PR 模板、ruleset README 与协作文件也需要同步维护

## 后续约束

- 若后续调整分支角色、PR 目标分支、required checks 或 `master` 保护策略，应同步更新本 ADR
- 若验证基线、协作文件或 PR 模板发生影响分支治理口径的变化，也应评估是否需要同步修订本 ADR
- 新增同类治理规则时，优先延续“仓库文件声明 + GitHub 设置落地 + ADR 固化”的三层收口方式，避免重新回到只靠口头约定的状态
