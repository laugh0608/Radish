# PR CI / CD 质量门禁治理记录（2026-08-15）

## 结论

本批没有降低质量门槛，而是把 PR 检查从“两个 workflow 重复执行、七个直接 required context、单人维护仍强制他人审批”收敛为“一条 PR workflow、六个可定位组件、一个稳定聚合结论”。Tag 发布继续复用完整 Candidate Quality，不改变镜像构建、Trivy 策略或部署授权边界。

## 根因与治理

| 原问题 | 根因 | 治理结果 |
| --- | --- | --- |
| 同一 PR 同时触发 `Repo Quality` 与完整 `Candidate Quality` | 候选 workflow 兼任 PR 与 tag 复用，造成 lint、baseline、后端测试重复 | 候选 workflow 只保留 `workflow_call / workflow_dispatch`；PR 只运行 `Repo Quality` |
| 多个 required context 容易随 job 调整漂移 | ruleset 直接绑定实现级 job 名 | `Candidate Quality` 在同一 workflow 中聚合六个组件，ruleset 只绑定该稳定结论 |
| 单人维护每次合并都需要管理员 bypass | `required_approving_review_count = 1` 与实际协作方式冲突 | 审批数改为 `0`，仍要求 PR、会话解决、禁止删除与非快进 |
| PR 对卫生与 Lint 使用 changed-only，复杂合并范围可能漏判或收集不稳定 | 远端门禁复用了本地连续开发的加速策略 | 远端改为全仓预算卫生和四 workspace 全量零 warning lint；本地仍保留 changed-only |
| Backend Guard 使用默认 SQLite，不能提前覆盖 PostgreSQL 候选差异 | PR 后端专题环境与发布候选环境不一致 | 命中后端影响面时固定 PostgreSQL 17，并以 warning-as-error 构建后执行完整测试 |
| 通用规划、ruleset 与 PR 模板修改会机械触发后端 / 身份全量回归 | impact 规则把描述门禁的文档等同于改变运行门禁 | 只保留真实运行入口、专题契约和实际门禁资产；自测固定正负样例 |

## 现行 PR 门禁

`Repo Quality` 保留六个独立组件，便于直接定位失败：

1. `Repo Hygiene`
2. `Frontend Lint`
3. `Baseline Quick`
4. `Dependency Security`
5. `Backend Guard`
6. `Identity Guard`

最终 `Candidate Quality` 使用 `if: always()` 汇总所有组件；任一组件出现 `failure / cancelled / skipped` 都会失败，并在 Actions Summary 输出结果表。聚合不会吞掉失败，也不建立第二套检查实现。

## 验证事实

- `npm run check:repo-quality-contract`：通过；ruleset 模板只要求聚合检查，六组件及聚合依赖完整。
- `npm run check:identity-impact:self-test`：`10` 个正样例、`14` 个负样例、`5` 类规则全部通过。
- `npm run validate:baseline:quick`：通过；版本、部署、镜像、HTTP、UI、Client `557`、Console `138`、身份与 LongId 门禁均通过。
- `npm run lint`：四个 Web workspace 全量通过。
- `npm run check:repo-hygiene:candidate`：全仓预算扫描通过，未新增卫生问题。
- `dotnet build Radish.slnx -c Debug --no-restore --warnaserror`：`0 warning / 0 error`。
- PostgreSQL 17 完整后端测试：`1321 passed / 0 failed / 0 skipped`；临时容器验证后已停止并自动删除。
- workflow YAML：三份相关文件均完成 YAML 解析复核。

## 边界与后续观察

- 未启动应用服务、未使用浏览器、未安装或更新依赖、未触发生产证据采集。
- 未修改 tag、镜像、生产固定 tag 或部署状态。
- 首个承载本批 workflow 的真实 PR 仍需观察聚合 context 是否稳定上报，以及 GitHub 邮件是否收敛为单 workflow 结论；这属于配置生效复核，不是重新启动生产证据采集。
