# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、当前判断、执行顺位、发布门禁与维护线**。
>
> 历史批次、命令级验证和页面级实现记录统一查看 [已完成摘要](/planning/archive)、[记录索引](/records/) 与 [开发日志](/changelog/)。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前子阶段**：`P3-12-F 正式版发布候选`
- **工程第一顺位**：`P3-12-F / Q1-C 文件访问令牌治理`
- **产品下一顺位**：`完成 Release Go 门禁后开展小规模受控试用`
- **复核日期**：`2026-07-11`
- **当前判断**：
  - 纯 Web 已成为 PC / mobile 浏览器默认产品形态，`/desktop` 仅保留 WebOS 历史兼容入口；Flutter 保持移动原生维护线，PC/Tauri 后置。
  - `P3-12-A-D` 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - `P3-12-E1-E7` 已完成首批产品成熟度硬化；E8 首日已回拉主导航、用户语言、页面滚动、聊天工作区和公开文档可见口径。
  - 2026-07-10 全仓审计确认发布工程成熟度落后于产品与功能进度；Q0 属于进入发布候选前必须完成的安全阻断。
  - 2026-07-11 Q0-A 已完成：npm / NuGet High / Critical 清零，NuGet 审计恢复，`Dependency Security` 已纳入 Repo Quality workflow，并由用户确认配置到远程 `master` ruleset。
  - 2026-07-11 Q0-B 已完整关闭：生产性能基准、Weather、敏感配置、事务演示、测试租户写入与手动未读推送入口已删除，正常租户查询与正式通知能力保留，精确防回归契约已建立。
  - 2026-07-11 Q0-C 已完成：API JWT 启用 `radish-api` audience，完整 Claims 与成功鉴权高频日志已删除，Auth transport security 受 Development 显式配置和 Gateway 单跳 Forwarded Proto 契约约束。
  - 2026-07-11 Q0-D 已完成：Markdown 阅读态、富文本导入 / 建链 / 导出共用链接协议白名单，危险或未登记 scheme 统一退化为不可点击文本。
  - 2026-07-11 Q0 成组运行态补验已完成：Scalar 只暴露实际 v1 文档，Gateway 授权码、受保护 API、UserInfo、refresh、audience 拒绝、宿主健康与真实 Markdown / Hub 旅程均通过。
  - 2026-07-11 E8-B 已完成：关系复访、公开 Docs 服务端契约、Console 移动低风险边界、Pencil `P02` 与 Discover 内容优先结构均已收口；七项有限矩阵已通过 PC / mobile CSS 视口成组运行态验收，旅程中发现的公开帖子举报入口和举报队列帖子导航查询阻断已修复并补定向测试。当前无接受后置项和已知 `P0/P1` 阻断。
  - PR `#58` 已于 2026-07-11 合并到 `master`，合并提交为 `c5906604`；本地 `dev` 保持在 `738b3f67`，继续遵循 `dev -> master` 单向 PR 流程，不把 `master` merge 或 rebase 回 `dev`。
  - 集成安全复核发现并清除了附件测试脚本中的完整过期 JWT、默认账号密码、client secret 与 password grant；跨平台脚本统一改用进程级 `RADISH_ACCESS_TOKEN`，完整 JWT / Bearer 字面量 / 私钥头扫描已接入 `Baseline Quick / Repo Quality`。
  - `P3-12-F` 进入条件已经满足，阶段正式切换到正式版发布候选；当前仍不把技术 smoke 或页面可达误写成真实使用增长，真正的增长验证从受控试用开始。
  - `P3-12-F` 不再被定义为“所有候选工作全部完成后的奖励阶段”：Q0 与有限产品矩阵负责进入 F，Q1 / Q2 / Q3 的发布必要子集在 F 内完成并作为 Release Go 门禁，Q4 转为持续维护。
  - 2026-07-11 Q1-A 已完成实现收口：14 处裸 `_ = Task.Run` 已按不可丢失业务写、可重算派生数据和 best-effort 实时推送完成迁移；Main / Chat 源库 Outbox、Hangfire 领取与租约恢复、目标写幂等、Message 通知事务、DeadLetter 与受权人工重放 API 已落地。订单权益 / 背包核心写仍保持同步事务，未扩入 Q1-B、Q2、Q3 或页面工作。
  - 2026-07-11 Q1-A 候选级验证已通过：PostgreSQL 源事务回滚、双 Worker 原子领取、租约恢复、通知两表事务与业务键幂等均由环境驱动集成测试覆盖；DbMigrate 首次建库、重入与 verify 通过，真实 API + PostgreSQL Hangfire 已恢复 `Pending` 和过期 `Processing` 重复任务且只生成一份持久通知。验证中发现的 Chat 种子 PostgreSQL 重入阻断与 ReliableOutbox 权限契约缺口已修复。
  - 2026-07-11 Q1-B 已完成：保留 `MessageModel` 并接入全局异常安全边界、稳定错误码、`TraceId / X-Correlation-ID`、模型校验、认证权限与限流统一响应；关键发布 Controller 已同步真实 HTTP 状态，问答、投票、抽奖、轻回应、治理和 Wiki 的异常文案状态分类已清零，HTTP / client / console 与 588 项后端测试通过。
  - Q1-B 已提交为 `873c5ea5`；工作区随后保持干净，`dev` 未合并或 rebase `master`。
  - 2026-07-11 Q1-C 运行时实施已完成：原始 token 一次返回、原列 SHA-256 Base64Url hash、历史 token 原位迁移、原子消费 / 撤销、列表脱敏、创建者 / 上传者 / System / Admin 权限、可信代理与日志凭据脱敏均已落地。解决方案构建 `0` warning / `0` error，后端测试通过 `597`、跳过 `2`；当前仍需备份后执行目标库 `DbMigrate apply / verify`，并在可用 PostgreSQL 环境补跑双 Worker 并发用例。

## V1 产品与发布范围

Radish V1 的产品定位固定为：

> 面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

范围分层：

- **核心主轴**：发现内容流、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知复访与信任治理。
- **支撑能力**：Docs 知识沉淀、Workbench 低频能力地图、公开主页与 Console 社区治理。
- **辅助激励**：经验、宠物、资产、背包和商城；不抢占首页与当前发布主线。
- **本次正式 Web 发布矩阵**：`Radish.Gateway`、`Radish.Api`、`Radish.Auth`、`Radish.DbMigrate`、`radish.client`、`radish.console`。
- **维护 / 实验矩阵**：Flutter 为维护与后续受控移动增强；WebOS 为历史兼容；Tauri 与 `Lib/radish.lib` 不属于本次正式 Web 发布阻断面。

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
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

### 1. 完成 Q1-C 目标库迁移与 PostgreSQL 验收

- 备份 Main 数据库后执行 `DbMigrate apply`，紧接着执行严格 `verify`，确认不存在旧明文、异常格式或重复 hash。
- 迁移后不回滚到只理解明文 token 的旧二进制；历史 32 字符 GUID token 仍由新运行时 hash 后命中。
- 配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 后执行 Q1-C PostgreSQL 双 Worker 用例，以真实数据库竞争结果关闭并发门禁。

### 2. 保持 P3-12-F Release Go 边界

- Q1-C 只治理文件访问 token 的存储、消费、撤销、权限、迁移和验证；不在本批混入 Q2、Q3、页面调整或无关附件重构。
- 合并到 `master`、创建 tag 和生产发布继续是三个独立决策；当前不创建 tag、不部署。
- 候选级运行态 smoke 仍只在用户当轮确认服务已启动后执行。

## 下一顺位

1. 备份当前 Main 数据库，执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply`。
2. 执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- verify`，并在 PostgreSQL 环境补跑 Q1-C 双 Worker 并发用例。
3. Q1-C 达到 Release Go 退出条件后，进入 Q2 的生产相似 PostgreSQL / OpenIddict 升级与版本治理必要子集。

## 并行维护线

- 公开 head、动态 sitemap、head snapshot 与生产公开域名配置。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容维护。
- Flutter analyze / test 与已落地主路径维护，不扩完整能力套件。
- 历史大文件、全量仓库卫生、TypeScript strict 和共享前端边界按 touched-file / 专题治理持续下降，不作为进入 F 的全量前置。

## 当前不做

- 不创建发布 tag，不进入生产部署或 Phase 4 稳定运营。
- Q1-C 目标库迁移和 PostgreSQL 门禁关闭前不进入 Q2；不混入 Q3、页面调整或无关重构。
- 不新增 E9 式全站逐页 UI / 文案扫尾；新缺口必须命中 E8-B 有限矩阵、Q0 或真实阻断。
- 不把 Console 移动端做成桌面完整能力复制。
- 不恢复 WebOS、Tauri 或完整 Flutter 套件为当前主线。
- 不启动推荐算法、ActivityPub / WebFinger、宠物经济扩展、完整移动商城、完整 PWA、完整 E2E 或大而全可观测性平台。
- 不把 Q4 全量大文件、历史颜色、格式和文档债务设为进入 F 的阻断。
- 不绕过 Pencil 直接实施新页面族、全局导航、跨页面视觉体系或重大交互；既有页面的小范围密度、文案、状态和布局修正可代码先行并用截图复核。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 发布候选运行态检查仍需用户当轮明确说明前后端已经启动。
- 本页不再记录 P3-1 至 P3-12-D 的命令级流水；历史事实进入 records、changelog 或 archive。
