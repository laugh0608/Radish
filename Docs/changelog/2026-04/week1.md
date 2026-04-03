# 2026-04 第一周 (04-02)

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
