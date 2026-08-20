# 参与 Radish

感谢你关注 Radish。项目采用文档先行、分层架构和风险分级验证的协作方式；贡献应优先保证需求完整、边界清晰、实现可维护，以及结论与实际证据一致。

## 开始之前

建议按以下顺序阅读：

1. [当前进行中](Docs/planning/current.md)
2. [文档入口](Docs/README.md)
3. [架构规范](Docs/architecture/specifications.md)
4. [验证基线](Docs/guide/validation-baseline.md)
5. 与改动直接相关的专题文档

同时请确认：

- 安全漏洞不要提交公开 Issue 或 Pull Request，请按[安全策略](SECURITY.md)私下报告。
- 参与 Issue、评审和其他项目交流时，请遵循[社区行为准则](CODE_OF_CONDUCT.md)。
- 架构、规则、公共接口、依赖、运行时行为或范围不清的变更，应先通过 Issue 或设计讨论确认方案；小规模、低风险且不改变既有边界的文档、配置和清理类变更可直接提交。
- 不提交真实凭据、个人数据、生产连接串、未经脱敏的日志或无法确认授权的第三方材料。
- 本仓库采用 [Radish Source-Available License](LICENSE)，不是开放源码许可证。参与前应取得仓库所有者允许；提交贡献即表示接受 `LICENSE` 第 4 节的贡献授权条款。

## 分支、提交与 Pull Request

1. `dev` 是日常集成分支，普通变更从主题分支向 `dev` 发起 Pull Request。
2. `master` 是受保护的稳定主线，只通过 Pull Request 接收阶段性 `dev` 晋级或明确的紧急修复；禁止直接推送和 force push。
3. 主题分支宜使用 `feature/*`、`fix/*`、`docs/*`、`refactor/*`、`test/*`、`chore/*` 或 `hotfix/*` 等能表达意图的名称。
4. 提交遵循 Conventional Commits，例如 `feat(forum): add post bookmarks`、`fix(auth): reject invalid callback state`、`docs(repo): add contribution guide`。
5. 使用贡献者自己的 Git 身份，不在提交信息中添加 AI 协作者署名。
6. `master` 允许 merge commit 与 rebase merge，禁止 squash merge。合并到 `master` 后，开始下一轮开发前必须把最新 `origin/master` 回灌 `dev`；可快进时优先 fast-forward，否则使用普通 merge，禁止使用 rebase、reset 或 force push 伪造同步。
7. Pull Request 应聚焦单一目标，避免夹带无关重构；评审意见应全部解决或明确记录处理结论。

## 实现与文档要求

- `Docs/` 是项目文档唯一真相源。长期功能、重要页面、跨模块能力或阶段性目标进入实现前，应先确认对应专题文档存在且边界清楚。
- 后端保持 `Controller -> Service -> Repository` 分层：Controller 不直接注入仓储，Repository 返回实体，Service 对外映射为 `Vo`。
- 前端统一使用 `@radish/http` 访问 API，通用组件优先放入 `@radish/ui`；不要新增平行的 HTTP 封装或无真实复用价值的抽象。
- Web 是优先产品线并覆盖 PC / mobile 浏览器；Flutter Native 是次级原生产品线。已弃用的 Tauri 资产不进入常规开发、构建和验收范围。
- 业务逻辑、错误处理和兼容策略应有明确原因，不以层层 fallback、吞异常或临时占位掩盖契约问题。
- 新增或改变架构、接口、迁移、配置、权限、安全边界、视觉规则或用户可见行为时，应同步更新相关专题文档和测试。
- 第三方代码、字体、图标、模型、数据和其他资产必须标明来源，并确认其许可证允许项目使用。

更完整的协作约束见 [AGENTS.md](AGENTS.md)。

## 本地验证

验证应与改动风险匹配，并以[验证基线](Docs/guide/validation-baseline.md)为准。常用入口包括：

```bash
# 文档或小型配置变更
npm run check:repo-hygiene:changed
git diff --check

# 日常改动的本地最小门禁
npm run validate:ci

# 后端 / API 或身份语义专题
npm run validate:backend
npm run validate:identity

# 准备候选版本
npm run validate:candidate
```

按改动范围也可以执行 `dotnet test Radish.Api.Tests`、`npm run type-check`、`npm run lint` 或对应 workspace 的测试与构建。真实页面联调和浏览器 smoke 不作为每次提交的默认步骤；成组功能准备验收时，应按[浏览器 Smoke 规则](Docs/guide/browser-smoke.md)通过 Gateway 同时复核 PC 与移动端视图。

Pull Request 只记录实际执行过的验证。未执行、因环境受阻、被测试条件跳过或需要人工完成的项目必须显式说明，不得把静态检查、SQLite 结果或模拟结果写成 PostgreSQL、运行态或生产环境已经验证。

## Pull Request 说明

请使用仓库 Pull Request 模板，并覆盖以下适用内容：

- 目标、范围、原因和明确非目标；
- 关联 Issue、规划项或专题文档；
- 架构、接口、数据迁移、权限、安全、兼容性和多端影响；
- 实际验证结果、未验证范围和测试环境；
- 已知风险、失败模式和回滚方式；
- 目标为 `master` 时的候选门禁，以及合并后的 `master -> dev` 回灌安排。

## 许可证

仓库内容受 [Radish Source-Available License](LICENSE) 约束。除非另有明确书面约定，提交贡献即表示你有权提供相关内容，并按 `LICENSE` 第 4 节向版权所有者授予贡献许可；这不改变仓库其余内容的许可范围。
