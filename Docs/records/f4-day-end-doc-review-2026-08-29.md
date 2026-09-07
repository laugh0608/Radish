# F4 2026-08-29 日终提交回顾与文档审阅

> 日期：2026-08-29（Asia/Shanghai）
>
> 范围：复核截至 `3877870c` 的今日三个提交，并以 P6-B 运行时代码 / 测试和真实 AVD 证据反查相关文档。本次状态校正文档提交自身不计入回顾范围。

## 今日结论

- 今日 Git 历史截至本次状态校正前共有三个提交：`4aa04ac3` 更新 dev-first 治理，`9087d562` 收口 P6-B 第一轮与日终文档，`3877870c` 独立提交 P6-B 运行时修正和回归测试。
- 该治理提交只修改 `AGENTS.md`、`CLAUDE.md`、`CONTRIBUTING.md` 与分支治理 ADR，共 `4` 个文件、`23` 行新增、`14` 行删除。两个 Agent 根入口正文同步，贡献指南与 ADR 的 `dev / master`、主题分支、PR、CI 和回灌口径一致。
- P6-B 第一轮真实运行暴露并修正五组契约：Flutter bearer 请求统一续签、公开资料切换 Public API、作者编辑携带 / 更新 content revision、轻回应上限收敛为 `10` 字，以及地址空字符串明确表达清空。
- 修正后 compact AVD 的会话恢复、地址清空、帖子 CAS 与 `10` 字轻回应成立；medium 匿名宽屏成立，但 fresh AVD 的 OIDC pending attempt / callback 未闭环，compact 根评论运行时 CAS 与新哈希剩余矩阵也未关闭。因此 P6-B 继续 `No-Go`，不能进入 P6-C。
- P6-B 的 `28` 个 tracked 非文档文件与 `2` 个新增 Flutter tests 已作为独立代码提交 `3877870c` 落库，没有混入 `9087d562` 文档提交；提交后工作树恢复干净。

## 今日全部已提交变更

| 提交 | 主题 | 审阅结论 |
| --- | --- | --- |
| `4aa04ac3` | `docs(governance): 改为 dev 优先开发拓扑` | 根入口、贡献指南与 ADR 同步把普通串行开发改为直接在 `dev` 进行；主题分支 / PR 改为按真实隔离与评审价值选择，`master` 稳定主线和回灌约束不变。 |
| `9087d562` | `docs(flutter): 收口 P6-B 首轮验收与日终回顾` | 新增 P6-B 第一轮与日终记录，更新当前阶段、路线图、Flutter / UI 专题、README、日志和索引；提交只包含文档。 |
| `3877870c` | `fix(flutter): 修正 P6-B 运行时契约` | 独立提交 Auth、Profile、Forum 的五组契约修正及 Flutter、Web、后端回归覆盖，共 `30` 个文件、`871` 行新增、`64` 行删除。 |

## 治理文档反查

### `dev` 常态开发边界

- `AGENTS.md` 与 `CLAUDE.md` 从第 4 行起保持逐字同步，均明确普通串行任务直接在 `dev` 开发 / 提交，Agent 不自动创建 `codex/*` 或额外 worktree。
- `CONTRIBUTING.md` 面向项目所有者、授权维护者与外部贡献者区分直接开发和主题分支场景，没有把“无需 PR”扩张到 `master`。
- ADR 已更新接受状态下的分支角色、合并策略、`dev` 规则、GitHub Settings 和代价；`master` 只经 PR、禁用 squash、回灌不使用 rebase / reset / force push 的长期边界未改变。
- [Agent 协作与执行规则](/guide/agent-collaboration)只定义按任务授权、实现与验证流程，没有要求普通任务必须创建主题分支；[验证基线](/guide/validation-baseline)按风险和合并阶段选择验证，也不依赖“所有改动必须经过 PR”。因此这两份稳定专题无需重复改写。

## 按当前 P6-B 代码反查文档

### Auth 与统一登录

- `SessionController` 新增临近过期判断、同 token 并发 refresh 合并、session epoch 迟到结果隔离和非致命 refresh issue；只有 Auth 明确返回 `invalid_grant` 才清空会话。
- `HttpRadishApiClient` 请求前统一解析 bearer token，并只对携带 token 的首次 `401` 强制 refresh 后重试一次；业务 Repository 不建立各自续签或无限重试。
- 系统浏览器 OIDC、Authorization Code + PKCE、未知 / 过期 callback 拒绝仍是正式边界；今天没有把账号密码表单迁入 App。fresh AVD pending attempt 丢失进入 2026-08-30 第一顺位。

### Profile 与公开身份

- Flutter 公开资料、统计、帖子和评论改用现有 Public API 与 `identifier`，不再调用要求本人 / 管理权限的旧路径。
- `UpdateMyProfile.Address` 的稳定语义为：字段省略或 `null` 保持原值，空字符串清空，非空字符串修剪后写入；Flutter 与 Web 编辑器均保留用户主动清空意图。

### Forum 写入

- 帖子和评论模型消费 `voContentRevision`；作者帖子 / 根评论编辑提交 `expectedContentRevision`，成功后使用响应返回的新 revision 更新本地节点，不盲写覆盖冲突。
- Flutter 轻回应输入上限从 `24` 收敛到服务端权威上限 `10`；原有失败重试 `clientSubmissionId` 生命周期保持不变。

### 验证与运行事实

- Flutter 定向 `120 / 120`、全量 `427 / 427`、analyze 零问题；后端身份 / Forum 定向 `52 / 52`；Web 全量 `557 / 557`、type-check / lint 通过。
- `Radish.Api`、`Radish.Gateway`、`Radish.Auth` 独立构建均 `0 warning / 0 error`；release APK 构建通过。`Radish.slnx` 聚合构建约 `5:01` 返回失败但只打印 `0 warning / 0 error`，按本机聚合工程异常如实保留，不记为通过。
- 受控帖子树、资料临时地址、ADB reverse / forward、三宿主、两台 AVD、临时 medium AVD、设备临时 CA、主机私钥 / 证书与原始取证目录均已按授权清理。

## 文档更新结论

- [当前规划](/planning/current)、[开发路线图](/development-plan)、[Flutter 专题](/features/flutter-native-product-ui-design)和根 README 已推进到“P6-B 第一轮 `No-Go`”；P6-A 旧哈希明确失效，不再描述为当前可直接验收候选。
- [Flutter README](../../Clients/radish.flutter/README.md)与[移动端 handoff](/guide/flutter-mobile-handoff)已补统一续签、Public API、revision CAS、轻回应 `10` 字和地址清空稳定契约。
- UI 差异附录、Flutter 设计源索引和 Web 代表页审计已记录 P6-B 第一轮没有暴露新的共享视觉结构偏差；当前阻断不触发 Pencil 修改。
- 八月日志与记录索引已补 P6-B 第一轮和本次日终回顾入口；`AGENTS.md` / `CLAUDE.md` 不再修改，因为今日治理提交已经完整同步且没有新增第二组跨任务规则。

## 明日事项（2026-08-30）

1. 新会话先读取[当前进行中](/planning/current)、本记录、[P6-B 第一轮](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-2026-08-29)、[Flutter 专题](/features/flutter-native-product-ui-design)、[移动端 handoff](/guide/flutter-mobile-handoff)、[验证基线](/guide/validation-baseline)、[运行手册](/guide/operations-runbook)和 [Flutter README](../../Clients/radish.flutter/README.md)，并确认 P6-B 当前代码基线为 `3877870c`。
2. 第一顺位只审计 OIDC pending attempt 的 `state / verifier` owner、App lifecycle / Activity recreation、Chrome 本地证书中断和 `radish://oidc/callback` 回流；先补稳定复现测试，不先改成 App 内账号密码登录。
3. 根因与最小修正方案明确后，说明认证运行时影响并等待项目所有者批准；修复后重跑 Auth 定向、Flutter 全量与 release 构建，冻结新的唯一 APK 哈希。
4. 新哈希先关闭 medium authenticated 回流与 compact 根评论 CAS，再补齐受影响的 default / 三套非默认主题矩阵；P6-B 完整关闭前不进入 P6-C。
5. 服务、AVD、APK 安装和真实 Smoke 仍需明日单独授权；正式签名、外部分发、iOS 与 desktop 继续后置。

## 日终验证与提交边界

- 日终不重复运行今天已通过的全量代码回归，也不重新启动服务或 AVD。
- 文档批执行 `npm run check:docs`、`npm run check:repo-hygiene:staged`、`git diff --check` 与 staged 边界检查。
- `9087d562` 只包含 README 与 `Docs/` 文档；P6-B 代码 / 测试随后以 `3877870c` 独立提交。本次只校正提交状态，不改变 P6-B `No-Go` 结论或明日认证修正边界。
