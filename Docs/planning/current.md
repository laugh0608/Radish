# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、当前判断、执行顺位、发布门禁与维护线**。
>
> 历史批次、命令级验证和页面级实现记录统一查看 [已完成摘要](/planning/archive)、[记录索引](/records/) 与 [开发日志](/changelog/)。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前子阶段**：`P3-12-F 正式版发布候选`
- **工程第一顺位**：`P3-12-F / Release Go 候选级运行态验收与受控试用准备`
- **产品下一顺位**：`完成 Release Go 门禁后开展小规模受控试用`
- **复核日期**：`2026-07-12`
- **当前判断**：
  - 纯 Web 已成为唯一正式产品主线并覆盖 PC / mobile 浏览器；`/desktop` 仅保留 WebOS 历史兼容入口，Flutter 转为条件式维护，Tauri 冻结为实验资产。
  - `P3-12-A-D` 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - `P3-12-E1-E7` 已完成首批产品成熟度硬化；E8 首日已回拉主导航、用户语言、页面滚动、聊天工作区和公开文档可见口径。
  - 2026-07-10 全仓审计确认发布工程成熟度落后于产品与功能进度；Q0 属于进入发布候选前必须完成的安全阻断。
  - 2026-07-11 Q0-A 已完成：npm / NuGet High / Critical 清零，NuGet 审计恢复，`Dependency Security` 已纳入 Repo Quality workflow，并由用户确认配置到远程 `master` ruleset。
  - 2026-07-11 Q0-B 已完整关闭：生产性能基准、Weather、敏感配置、事务演示、测试租户写入与手动未读推送入口已删除，正常租户查询与正式通知能力保留，精确防回归契约已建立。
  - 2026-07-11 Q0-C 已完成：API JWT 启用 `radish-api` audience，完整 Claims 与成功鉴权高频日志已删除，Auth transport security 受 Development 显式配置和 Gateway 单跳 Forwarded Proto 契约约束。
  - 2026-07-11 Q0-D 已完成：Markdown 阅读态、富文本导入 / 建链 / 导出共用链接协议白名单，危险或未登记 scheme 统一退化为不可点击文本。
  - 2026-07-11 Q0 成组运行态补验已完成：Scalar 只暴露实际 v1 文档，Gateway 授权码、受保护 API、UserInfo、refresh、audience 拒绝、宿主健康与真实 Markdown / Hub 旅程均通过。
  - 2026-07-11 E8-B 已完成：关系复访、公开 Docs 服务端契约、Console 移动低风险边界、Pencil `P02` 与 Discover 内容优先结构均已收口；七项有限矩阵已通过 PC / mobile CSS 视口成组运行态验收，旅程中发现的公开帖子举报入口和举报队列帖子导航查询阻断已修复并补定向测试。当前无接受后置项和已知 `P0/P1` 阻断。
  - PR `#58` 已于 2026-07-11 合并到 `master`，合并提交为 `c5906604`；2026-07-12 已通过 `9733e73c` 把该 `master` 结果回灌 `dev`。当前固定采用“开发内容经 `dev -> master` PR 集成、每次合入 `master` 后立即 `master -> dev` 回灌”的闭环，不再维持单向 ancestry 分叉。
  - 集成安全复核发现并清除了附件测试脚本中的完整过期 JWT、默认账号密码、client secret 与 password grant；跨平台脚本统一改用进程级 `RADISH_ACCESS_TOKEN`，完整 JWT / Bearer 字面量 / 私钥头扫描已接入 `Baseline Quick / Repo Quality`。
  - `P3-12-F` 进入条件已经满足，阶段正式切换到正式版发布候选；当前仍不把技术 smoke 或页面可达误写成真实使用增长，真正的增长验证从受控试用开始。
  - `P3-12-F` 不再被定义为“所有候选工作全部完成后的奖励阶段”：Q0 与有限产品矩阵负责进入 F，Q1 / Q2 / Q3 的发布必要子集在 F 内完成并作为 Release Go 门禁，Q4 转为持续维护。
  - 2026-07-11 Q1-A 已完成实现收口：14 处裸 `_ = Task.Run` 已按不可丢失业务写、可重算派生数据和 best-effort 实时推送完成迁移；Main / Chat 源库 Outbox、Hangfire 领取与租约恢复、目标写幂等、Message 通知事务、DeadLetter 与受权人工重放 API 已落地。订单权益 / 背包核心写仍保持同步事务，未扩入 Q1-B、Q2、Q3 或页面工作。
  - 2026-07-11 Q1-A 候选级验证已通过：PostgreSQL 源事务回滚、双 Worker 原子领取、租约恢复、通知两表事务与业务键幂等均由环境驱动集成测试覆盖；DbMigrate 首次建库、重入与 verify 通过，真实 API + PostgreSQL Hangfire 已恢复 `Pending` 和过期 `Processing` 重复任务且只生成一份持久通知。验证中发现的 Chat 种子 PostgreSQL 重入阻断与 ReliableOutbox 权限契约缺口已修复。
  - 2026-07-11 Q1-B 已完成：保留 `MessageModel` 并接入全局异常安全边界、稳定错误码、`TraceId / X-Correlation-ID`、模型校验、认证权限与限流统一响应；关键发布 Controller 已同步真实 HTTP 状态，问答、投票、抽奖、轻回应、治理和 Wiki 的异常文案状态分类已清零，HTTP / client / console 与 588 项后端测试通过。
  - Q1 已形成独立提交：Q1-A `33e4690f / 86466308`、Q1-B `873c5ea5`、Q1-C `ef370884`；后续 `9733e73c` 仅完成稳定主线 ancestry 回灌，不改变这些专题提交边界。
  - 2026-07-12 Q1-C 已完整关闭：原始 token 一次返回、原列 SHA-256 Base64Url hash、历史 token 原位迁移、原子消费 / 撤销、列表脱敏、权限、可信代理与日志凭据脱敏均已落地；本地 Main SQLite 已在备份后完成 `DbMigrate apply / verify`，迁移前后完整性检查通过，PostgreSQL 双 Worker 原子额度竞争用例通过 `1/1`。Q1 Release Go 必要子集至此完成，工程第一顺位进入 Q2-A。
  - 2026-07-12 Q2-A Release Go 高风险子集已收口：统一 UTC `TimeProvider` 与系统业务日，迁移 token、幂等、支付、限流、投票 / 抽奖、订单 / 权益、清理、Hangfire 与经验 / 登录自然日；API 自然日改用 `DateOnly`，DbMigrate 能只读报告列类型与异常。SQLite verify、隔离 PostgreSQL 17 集成测试、609 项后端测试与 Baseline Quick 均通过；物理 `date` 改列按职责移交 Q2-B schema ledger。
  - 2026-07-12 Q2-B ledger / OpenIddict 首批已由提交 `7ac68c75` 收口：Main / Log / Message / Chat 引入 `RadishSchemaVersion` baseline 与 checksum drift 门禁，`apply` 接入前置 doctor、OpenIddict 显式迁移、seed 与严格 verify；OpenIddict 持久化边界已从 Auth 宿主拆出，SQLite / PostgreSQL 独立 migration assembly、空库迁移、重入和旧 `EnsureCreated` schema adoption 均已验证，EF Design 传递依赖已安全钉住且 High / Critical 为 `0`。
  - Q2-B 首个业务迁移 `20260712_001_experience_natural_dates` 已完成实现与回归：三处经验自然日改为物理 `date`，SQLite 重建保留索引，PostgreSQL 同时覆盖 `timestamp with/without time zone`，异常历史值拒绝、重入和 ledger 记账通过；全量后端 `615` 通过、`6` 个环境用例跳过，隔离 PostgreSQL 17 定向用例另行实跑通过。
  - 2026-07-12 Q2-B Release Go 必要子集已完整关闭：SQLite non-deferred 写事务、PostgreSQL transaction-scoped advisory lock 与 ledger 二次检查已阻止首次 baseline / 后续 migration 的并发重复执行；baseline 后禁止 Code First / 旧补丁静默修复。SQLite 文件备份恢复自动化测试和 PostgreSQL `pg_dump → 前滚 → pg_restore → 再前滚` 生产相似演练通过；最终全量后端 `618` 通过、`7` 个环境用例跳过，依赖 High / Critical 为 `0`，临时容器已清理。工程第一顺位进入 Q2-C。
  - 2026-07-12 Q2-C Release Go 必要子集已完成：根 `version.json` 统一 .NET、npm workspaces、Rust、Tauri、Flutter 与镜像产品版本，当前候选为 `26.7.1`、Flutter 为 `26.7.1+1`；版本同步、字段漂移、规范 tag、正式发布记录与 Docker 构建前阻断已自动化。Baseline Quick、.NET 构建、Cargo metadata、Flutter analyze 与 `204` 项 Flutter 测试均通过；本批未创建 tag、镜像或部署。工程第一顺位进入 Q3。
  - 2026-07-12 Q3 Release Go 必要子集已完成：根 lint 与 Hook dependency warning 清零，四个前端 workspace 的 lint / type-check / test 进入默认基线，.NET 候选构建按 warning-as-error 执行；全量仓库卫生采用已审计预算阻断新增问题，`Candidate Quality` 提供手动、定期与镜像发布前复用入口，依赖安全、SBOM、High / Critical 镜像扫描和 provenance 已接入。Q3-C 只补 JWT issuer、idle-session handler、Hub 标准 `sub` 与仓储租户软删 / 恢复真实缺口，未重复 Q1 / Q2 已有迁移、并发和错误契约资产；本批未创建 tag、推送镜像或部署。
  - 2026-07-12 Release Go 候选运行态验收已通过：本地 SQLite 在备份后完成 schema / OpenIddict 升级与严格 verify，真实启动暴露的 split table baseline 接管和 API 异常管线宿主阻断已修复；Gateway PC `1920x1080` 与移动 `390x844` CSS 视口覆盖公开、私域、作者态和 Console 代表路径，宿主探针、公开 head 主路径与候选静态门禁通过。公开用户主页初始 HTML 缺少服务端 head 登记为非阻断 P2；当前 Release Go 仅余小规模受控试用，仍未创建 tag、推送镜像或部署。

## V1 产品与发布范围

Radish V1 的产品定位固定为：

> 面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

范围分层：

- **核心主轴**：发现内容流、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知复访与信任治理。
- **支撑能力**：Docs 知识沉淀、Workbench 低频能力地图、公开主页与 Console 社区治理。
- **辅助激励**：经验、宠物、资产、背包和商城；不抢占首页与当前发布主线。
- **本次正式 Web 发布矩阵**：`Radish.Gateway`、`Radish.Api`、`Radish.Auth`、`Radish.DbMigrate`、`radish.client`、`radish.console`。
- **维护 / 实验矩阵**：Flutter 仅维护既有 MVP，满足原生价值证据后才重新立项；WebOS 为历史兼容；Tauri 冻结，Tauri 与 `Lib/radish.lib` 均不属于当前开发主线或正式 Web 发布阻断面。

## 当前执行入口

- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)
- [P3-12-E8-Q 正式发布工程成熟度与安全收口](/planning/p3-12-e8-release-engineering-maturity-security-closure)
- [P3-12-E8-Q0 安全与暴露面阻断实施方案](/records/p3-12-e8-q0-security-exposure-implementation-plan-2026-07-10)
- [P3-12-E8-B 有限产品矩阵首轮审计记录](/records/p3-12-e8-b-limited-product-matrix-audit-2026-07-11)
- [P3-12-E8 dev -> master 集成审阅记录](/records/p3-12-e8-pre-master-integration-review-2026-07-11)
- [P3-12-F Q1-A 事务后可靠任务审计与实施方案](/records/p3-12-f-q1-a-reliable-post-transaction-task-plan-2026-07-11)
- [P3-12-F Q1-A 候选级可靠性验证记录](/records/p3-12-f-q1-a-candidate-validation-2026-07-11)
- [P3-12-F Q1-B API 错误契约审计与实施方案](/records/p3-12-f-q1-b-api-error-contract-audit-plan-2026-07-11)
- [P3-12-F Q1-C 文件访问令牌审计与实施方案](/records/p3-12-f-q1-c-file-access-token-governance-plan-2026-07-11)
- [P3-12-F Q2-A 时间语义与历史数据迁移方案](/records/p3-12-f-q2-a-time-semantics-migration-plan-2026-07-12)
- [P3-12-F Q2-B 数据库演进与 schema ledger 方案](/records/p3-12-f-q2-b-database-evolution-plan-2026-07-12)
- [P3-12-F Q2-C 版本单一真值治理方案](/records/p3-12-f-q2-c-version-governance-plan-2026-07-12)
- [P3-12-F Release Go 候选运行态验收记录](/records/p3-12-f-release-go-candidate-runtime-validation-2026-07-12)
- [产品版本与发布标识治理](/guide/version-governance)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

### 1. 准备 `dev -> master` 候选集成

- Q1 / Q2 / Q3 与候选运行态验收均已形成独立提交和验证记录。
- 本轮留痕提交后 `dev` 相对 `c5906604` 为 `23` 个提交、`308` 个文件；最终 PR 前重新 fetch 并刷新 `master...dev` 范围、merge-tree 与 required checks。
- 是否创建 PR、合并、开始受控试用、创建 tag 和生产发布继续分别决策。

### 2. 执行剩余 Release Go 门禁

- 候选运行态验收已经通过；移动端工具未提供 DPR 设置，记录只声明 `390x844` CSS 视口覆盖。
- 下一步建立并执行小规模受控试用，记录激活、首次参与、收到回应后的回流、核心任务失败和用户反馈。
- 公开用户主页服务端 head 缺口按非阻断 P2 进入并行维护线，不冒充受控试用或阻断当前集成准备。
- 合并到 `master`、创建 tag 和生产发布继续是三个独立决策；当前不创建 tag、不部署。

## 下一顺位

1. 最终刷新 `master...dev` 集成范围并准备 `dev -> master` PR。
2. 建立并执行小规模受控试用记录。
3. 受控试用无未处置 `P0/P1` 后，再分别决策合并、tag 与生产发布。

## 并行维护线

- 公开 head、动态 sitemap、head snapshot 与生产公开域名配置。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容维护。
- Flutter 只做既有 MVP 的阻断、安全与认证兼容维护；不默认新增功能、扩 iOS 或追平 Web。
- 历史大文件、全量仓库卫生、TypeScript strict 和共享前端边界按 touched-file / 专题治理持续下降，不作为进入 F 的全量前置。

## 当前不做

- 不创建发布 tag，不进入生产部署或 Phase 4 稳定运营。
- 不创建发布 tag、不推送镜像、不部署；不扩展全仓 strict、完整 E2E 或无关重构。
- 不新增 E9 式全站逐页 UI / 文案扫尾；新缺口必须命中 E8-B 有限矩阵、Q0 或真实阻断。
- 不把 Console 移动端做成桌面完整能力复制。
- 不解冻 Tauri，不恢复完整 Flutter 套件；二者均不得与纯 Web 并行争夺当前主线资源。
- 不启动推荐算法、ActivityPub / WebFinger、宠物经济扩展、完整移动商城、完整 PWA、完整 E2E 或大而全可观测性平台。
- 不把 Q4 全量大文件、历史颜色、格式和文档债务设为进入 F 的阻断。
- 不绕过 Pencil 直接实施新页面族、全局导航、跨页面视觉体系或重大交互；既有页面的小范围密度、文案、状态和布局修正可代码先行并用截图复核。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 后续如需再次执行发布候选运行态检查，仍需用户当轮明确说明前后端已经启动。
- 本页不再记录 P3-1 至 P3-12-D 的命令级流水；历史事实进入 records、changelog 或 archive。
