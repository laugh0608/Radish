# 2026-04 第一周 (04-02 ~ 04-05)

## 2026-04-02 (周四)

### 社区主链回归收口

- **社区主链多轮回归已完成**：论坛、聊天室、通知中心、认证登录稳定性、附件访问边界，以及论坛评论分页等本轮触达范围当前均无新的明显阻塞问题，社区主线正式转入稳定维护。
- **首版 dev 文档口径已统一**：`planning/current.md`、`development-plan.md`、`planning/dev-first-scope.md`、`planning/dev-first-status-matrix.md`、总回归检查单与总回归记录，当前已统一把“等待总回归确认”收束为“总回归已完成，转入稳定维护”。
- **当前主线已切换**：本轮不再继续扩张社区体验尾项，下一步转入“下一里程碑入口重审”，优先确认身份语义 Phase 4 的启动前提与外部客户端兼容边界。

### 规划结论更新

- **首版 dev 结论保持不变**：当前仍维持“可发内部开发版”的工程判断，不因社区主链回归收口而回退为继续观察。
- **下一阶段入口已收束**：若身份语义 Phase 4 的启动前提明确，下一条正式主线优先进入协议输出收敛；若前提暂不满足，则先重定义 `M14` 的宿主运行与最小可观测性基线，而不是继续沿用旧阶段名直接开工。
- **`M14` 重定义文档已新增**：新增 `guide/m14-host-runtime-observability-baseline.md`，把宿主配置一致性、`DbMigrate doctor / verify`、健康检查、启动日志与测试 / 生产部署最小复核口径收束为同一份阶段定义，并明确当前不扩成完整可观测性平台工程。

### 身份语义 Phase 4 入口补齐

- **启动前提确认文档已新增**：新增 `guide/identity-claim-phase4-readiness.md`，把“现在能不能启动协议输出收敛”从会话判断收束为文档事实。
- **当前结论已明确**：身份语义 Phase 4 当前进入“启动前提确认”阶段，而不是直接进入实施；先确认协议消费者清单、历史 Claim 保留矩阵与回滚方案，再决定是否启动。
- **协议消费者矩阵已新增**：新增 `guide/identity-claim-protocol-consumers.md`，把官方客户端、仓库内 `.http` 联调资产、共享前端 OIDC 基础设施与仓库外待确认边界收束到同一份事实清单。
- **历史 Claim 保留矩阵已新增**：新增 `guide/identity-claim-retention-matrix.md`，把 `sub / name / preferred_username / role / scope / tenant_id`、`ClaimTypes.*`、`TenantId`、`jti` 的保留策略收束为单一口径。
- **实施与回滚窗口已新增**：新增 `guide/identity-claim-phase4-rollout-window.md`，把输出收缩顺序、官方回归顺序与默认回滚优先级收束为同一份执行口径。
- **当前状态更新为前置资产已完成 3/3**：协议消费者清单、历史 Claim 保留矩阵与实施 / 回滚窗口当前均已落文档；下一步进入最终启动评审，而不是直接把事项改写为“Phase 4 实施中”。
- **最终启动评审已新增**：新增 `guide/identity-claim-phase4-start-review.md`，把三份前置资产收束为一次正式评审结论。
- **当前评审结论已明确**：仓库内启动输入当前已经齐备，但仓库外兼容边界仍未被事实关闭，因此当前不正式启动身份语义 Phase 4；若短期内仍无法确认外部边界，则下一步转入 `M14` 宿主运行与最小可观测性基线重定义。
- **仓库外兼容边界确认清单已新增**：新增 `guide/identity-claim-external-compat-checklist.md`，把测试 / 生产环境脚本、外部网关映射、仓库外调用方与第三方客户端接入拆成逐项可确认的检查清单。

## 2026-04-03 (周五)

### 身份语义 Phase 4 外部边界首轮盘点

- **仓库资产侧首轮执行记录已新增**：新增 `guide/identity-claim-external-compat-first-pass.md`，把仓库交付的 Nginx 样例、Compose 编排、部署指南、`HttpTest` 联调资产与默认客户端种子做了一轮事实化排查。
- **仓库内可排除范围已明确**：当前可确认仓库交付的反向代理样例与 Compose 口径未发现 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId`、`jti` 等旧 Claim 字段依赖；`userinfo` 的仓库内直接消费者当前主要收束在 `Radish.Api.AuthFlow.http`。
- **生产环境事实已补齐**：当前现网仅有 1 套 `v26.3.2-release` Docker 部署，外层使用 1Panel 默认 HTTPS 反向代理，无仓库外换 Token / 联调 / 巡检脚本，OpenIddict 仅有默认种子数据。
- **最终启动评审结论已更新**：基于仓库资产侧与当前生产环境侧事实，身份语义 `Phase 4` 当前已从“暂不启动”更新为“允许启动”；后续主线转入协议输出收敛与官方回归顺序执行，不再默认切向 `M14`。

### 身份语义 Phase 4 首轮实施与仓库内回归资产收口

- **Auth 输出双写已完成首轮收缩**：`AccountController / AuthorizationController` 当前已停止 `ClaimTypes.NameIdentifier`、`ClaimTypes.Name`、`ClaimTypes.Role` 与 `TenantId` 等历史双写输出；对应最小控制器测试已补齐，当前最小 .NET 验证通过。
- **`userinfo` 已完成最小对齐**：`UserInfoController` 当前已在保持 `sub / name / email / role / tenant_id` 对外结构稳定的前提下，完成标准字段优先、legacy 输入 fallback 的内部对齐；对应控制器测试已补齐并通过。
- **官方回归资产已完成首轮同步**：`Radish.Api.AuthFlow.http` 当前已统一为可闭环验证授权码、`/connect/userinfo` 与 refresh token 的脚本；`Scalar` 联调提示与鉴权文档中的默认测试账号也已统一回仓库真实种子值 `test / test123456`。
- **前端直接字段消费者已锁定测试边界**：`radish-client / radish-console` 当前已把 Token Claim 解析提取为可测试模块，并通过最小测试明确锁定“标准优先 + 输入兼容 fallback”的现行口径，避免后续回归把旧字段重新恢复成默认主口径。
- **仓库内最小验证已补齐**：本轮已完成 `Radish.Api.Tests` 相关控制器最小测试、`radish-client / radish-console` Token 解析测试、两个前端 workspace 类型检查，以及身份语义扫描；当前均无新的阻塞错误。

## 2026-04-04 (周六)

### 身份语义 Phase 4 官方回归收口

- **官方顺序真实回归已完成**：`radish-client`、`radish-console`、`radish-scalar` 当前均已完成实际回归，登录、回调、刷新、权限与调试入口未发现新的阻塞问题。
- **`Radish.Api.AuthFlow.http` 授权流程已确认闭环**：授权码、`/connect/token`、`/connect/userinfo`、refresh token 与受保护接口调用当前均可按最新 Phase 4 口径走通。
- **本轮无需触发回滚**：本次官方回归窗口内未发现需要恢复 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role / TenantId` 的阻塞事实；当前保持“输出收缩完成、输入兼容保留”的既定边界不变。

### 规划口径收束

- **当前结论已更新**：身份语义 `Phase 4` 当前不再停留在“继续做官方回归”的状态，而是明确收口为“首轮实施与官方顺序真实回归已完成，转入稳定维护”。
- **后续重点已切换**：下一步优先进入 `Phase 5` 防回归资产接入脚本 / 校验流程准备，以及下一里程碑入口重审；当前不在同一窗口提前删除 `UserClaimReader / CurrentUser` 的输入兼容。
- **`Phase 5` 首轮工程化入口已落地**：当前已补 `check:identity-runtime`、`check:identity-protocol-output`、`validate:identity`、`Identity Guard` CI 门禁，以及 `guide/identity-claim-regression-playbook.md`；身份语义相关改动后默认必跑项、官方顺序回归与本地 / CI 分层口径已统一。

### 身份语义 Phase 5 工程一致性补强

- **impact 判定当前已收口到单一规则源**：`check:identity-impact` 当前不再由脚本内部散落维护路径清单，而是改为统一复用单一规则源；除身份语义核心代码、前端 Token 解析与 `Radish.Api.AuthFlow.http` 外，也已把 `validation-baseline / regression-index / dev-first-regression-record / development-plan / planning/current / PR template` 等默认执行面文档与门禁资产纳入同一判定范围。
- **本地 `validate:ci` 已与真实 CI 对齐**：当前会先执行 `check:repo-hygiene:changed`、`lint:changed`、`validate:baseline:quick`，再仅在命中身份语义影响面时追加 `validate:identity`，不再无条件跑完整身份语义专题。
- **已补轻量自校验**：`validate:baseline` / `validate:baseline:quick` 当前都会先校验 identity impact 判定样本，避免 `Identity Guard` 的 changed-only 触发边界再次无声漂移。

### 默认执行面入口继续收口

- **pre-commit 已改为统一 staged 入口**：`.githooks/pre-commit` 当前不再手写 `git diff --cached` 管道，而是直接调用 `check:repo-hygiene:staged` 与 `lint:staged`，提交前入口已与统一 collector 保持同源。
- **PR 与回归记录模板已同步当前事实**：`PULL_REQUEST_TEMPLATE`、`change-regression-record-template` 与 `regression-index` 当前已纳入 `validate:ci` 与 staged 入口口径，避免“脚本事实已更新，但记录模板仍沿用旧写法”。

### required checks 治理真相源对齐

- **ruleset 模板已补 `Identity Guard`**：`.github/rulesets/master-protection.json` 当前已把 required checks 从三项补齐到四项，正式与 `Repo Quality` workflow 的 job 名保持一致。
- **ruleset README、ADR 与部署文档已完成同步**：`master` 分支保护说明、分支治理 ADR、部署发版指南与首版总回归检查单 / 记录当前均已统一为 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`、`Identity Guard` 四项门禁。
- **历史事实与现行口径已拆分记录**：`2026-03-26` 的三项门禁闭环继续保留为历史事实；截至 `2026-04-04`，后续稳定主线合并与 required checks 维护均以四项门禁为准。

### Repo Quality contract 自校验

- **新增 contract 自校验入口**：新增 `check:repo-quality-contract`，当前会自动核对 `.github/workflows/repo-quality.yml`、`.github/rulesets/master-protection.json` 与本地 `validate:ci` 的门禁契约是否一致。
- **本地门禁定义已改为共享 contract**：`validate:ci` 当前不再手写三段本地门禁标题与脚本参数，而是直接复用同一份 `Repo Quality` contract，降低 workflow / ruleset / 本地入口三处继续分叉的概率。
- **默认基线已接入轻量守卫**：`validate:baseline` / `validate:baseline:quick` 当前都会先做 contract 自校验，再继续执行 identity impact 自校验与后续基线步骤。
- **workflow 执行语义也已纳入守卫**：contract 当前不只检查 required check 名称，还会校验 `repo-quality.yml` 中四个 job 的关键命令片段，避免 changed-only、impact 判定或条件 `validate:identity` 在名称不变时悄悄漂移。

### Windows 共享执行层兼容性

- **共享执行层已收口**：新增 `Scripts/process-runner.mjs`，当前 `run-with-changed-files`、`validate:ci`、`validate:baseline`、`validate:identity` 与 changed-only lint 入口已统一复用，尽量减少对 `cmd.exe /c` 的依赖。
- **受限环境提示已统一**：如果当前 Windows 沙盒禁止 Node 脚本再次拉起外部进程，脚本现在会明确提示这是环境边界，而不是把原始 `EPERM / EINVAL` 直接抛给使用者。

### Repo Quality 故障分诊入口

- **维护侧单一分诊入口已新增**：新增 `guide/repo-quality-troubleshooting.md`，把 `check:repo-quality-contract`、`validate:ci`、身份语义条件触发、受限环境边界与历史 warnings / DLL 锁的排查口径收口到同一页。
- **默认入口文档已同步**：`guide/validation-baseline.md`、`guide/regression-index.md` 与 `guide/identity-claim-regression-playbook.md` 当前都已补回该手册入口，后续维护者不必再从脚本实现里自己反推失败归类。

### Phase 5 记录闭环补齐

- **impact 判定当前会直接输出命中原因类别**：`check:identity-impact` 已从单纯给出“是否命中”升级为同时输出命中文件与原因类别，当前统一收口为 `身份运行时入口 / Auth 协议输出 / 官方协议消费者 / Token 解析 / 默认执行面文档 / 门禁资产` 四类。
- **本地 `validate:ci` 已同步显示命中理由**：当 changed-only 判定命中身份语义影响面时，`validate:ci` 当前会直接打印原因类别与命中文件明细，维护者不必再手工比对脚本规则去解释 `Identity Guard` 为什么触发。
- **默认记录入口已补“命中原因 + 失败归类”双字段**：`PULL_REQUEST_TEMPLATE`、`change-regression-record-template`、`regression-result-template` 当前都已补齐结构化字段，用于统一表达“为什么命中身份语义影响面”以及“失败属于 contract 漂移 / 默认执行面失败 / 身份语义专题失败 / 受限环境边界中的哪一类”。
- **记录模板与分诊口径已纳入同一维护组**：`repo-quality-troubleshooting`、`validation-baseline`、`identity-claim-regression-playbook`、回归记录模板与 PR 模板当前已同步改为复用同一套原因/归类语义，`Phase 5` 工程化维护链路已从“脚本 + 手册”收口到“脚本 + 默认记录入口 + 分诊口径”闭环。

## 2026-04-05 (周日)

### 规划与阶段口径收口

- **今天先完成阶段切换，再进入 `M14` 第一轮入口**：当前不再继续扩张身份语义 `Phase 4`，而是先把已收口专题转入维护池，再把主线正式切到 `M14`。
- **维护池 / 观察池 / 后置池已整理**：`Phase 4` 首轮实施结果、聊天室 `P1`、通知中心、`Console-ext` 一期、投票 / 问答 / 抽奖 MVP、浏览记录与 `DbMigrate` 当前统一归入维护池；旧 `GetCommentTree`、身份语义外部兼容边界，以及 `Repo Quality / validate:ci / Identity Guard` 默认执行面继续留在观察池；`P3-ext / P4-ext / P5-ext / Console-ext Phase 2+ / P2-ext Auto / 邮件通知系统` 等继续留在后置池。
- **当前主线已正式切换到 `M14`**：当前按 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline) 作为正式主线，并把 `M13` 与 `M15` 保持在候选状态。

### 文档真相源继续对齐

- **规划页与总览页已同步今天结论**：`planning/current.md` 与 `development-plan.md` 当前都已明确“Phase 4 正式收口并转入维护态、`M14` 已切为当前主线、默认执行入口已明确”的最新判断。
- **本周日志已补规划留痕**：今天的规划决策已沉淀到周志，避免后续协作再次依赖口头上下文判断“现在到底该做什么”。

### Phase 4 本轮正式收口

- **官方顺序手工联调全部通过**：用户已确认 `radish-client`、`radish-console`、`Radish.Api.AuthFlow.http` 与 `radish-scalar` 均无异常，当前可将身份语义 `Phase 4` 从“稳定维护准备”进一步收束为“本轮正式收口、转入维护态”。
- **下一步明确切向 `M14`**：在 Phase 4 本轮正式收口后，后续不再继续扩张协议输出实施，而是转入 `M14` 入口评审与任务拆分。

### WebOS 入口补齐

- **新增 Scalar 桌面入口**：`radish.client` 当前新增 `Scalar` 外部应用图标，跳转到 Gateway `/scalar` 或本机直连 `http://localhost:5100/scalar`，并对齐到仅 `system / admin` 可见。
- **Scalar 已补自动授权跳转**：桌面入口当前会直接附带自动授权参数，打开 `Scalar` 后会按当前既有 OIDC 流程自动进入一次授权，不再要求用户进入页面后再手工点一次登录。

### M14 第一轮入口启动

- **`M14` 当前已从候选入口切换为正式主线**：本轮已把“宿主运行与最小可观测性基线”从重定义文档进一步推进到执行入口收口，不再停留在“下一里程碑入口评审中”的表述。
- **首轮执行清单已新增**：新增 `guide/m14-host-runtime-checklist.md`，把 `validate:baseline:host`、`DbMigrate doctor / verify`、宿主排障顺序，以及测试 / 生产部署后的最小复核动作收束到同一入口。
- **文档口径已同步回链**：`planning/current.md`、`development-plan.md`、`guide/validation-baseline.md`、`deployment/guide.md` 与 `guide/m14-host-runtime-observability-baseline.md` 当前已统一改成“`M14` 已启动，且默认执行入口已经明确”的口径。
- **运行态检查已补失败分类**：`check:host-runtime` 当前不再只输出失败结果，还会直接归类为端口未监听、TLS/证书、请求超时、宿主自报 `Unhealthy` 或一般 HTTP 状态异常，方便按 `M14` 顺序继续排障。
- **Gateway 健康端点语义已拆开**：`/health` 当前只代表最小后端宿主链，`/healthz` 继续保留更完整的下游观测；本地未启动 `console` 时不再干扰 `M14` 默认运行态检查。
- **`/healthz` 明细响应已补齐**：Gateway 当前已把 `/healthz` 从纯文本状态升级为结构化 JSON，默认输出整体状态、生成时间、总耗时，以及每个下游项的名称、状态、标签、耗时和异常摘要，便于直接人工分诊。
- **运行态脚本已补失败联动明细**：`check:host-runtime` 当前新增 `--details`，在默认检查失败时会自动补拉 `Gateway /healthz` 并输出压缩后的关键摘要，减少手工二次排查。
