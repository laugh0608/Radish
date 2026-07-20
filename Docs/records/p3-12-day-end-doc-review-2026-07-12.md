# P3-12 2026-07-12 日终提交回顾与文档审阅

> 日期：2026-07-12（Asia/Shanghai）
>
> 范围：`df5c53c4..db8d1821` 之间 36 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- Q1-C、Q2-A、Q2-B、Q2-C、Q3 与 Release Go 运行态验收全部完成，`P3-12-F` 已从候选进入正式生产并关闭。
- `v26.7.1-release` 至 `v26.7.1.1203-release` 暴露的镜像、PostgreSQL 标识符、OpenIddict provider / model 与候选 DateTime 参数阻断均以不可变 tag 逐次记录；最终 `v26.7.1.1204-release` 五镜像和固定 tag 生产前滚成功。
- 生产保留 PostgreSQL / Redis 数据与 migration ledger，DbMigrate、API、Auth、Gateway、Frontend 正常运行，首个管理员已创建。
- 首次管理员页面只在部分私域入口触发，公开入口与 Workbench 未统一经过 `BootstrapGate`；后端管理员存在性和创建事务正确，该问题进入发布后非阻断维护首项。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `da2f1456` | `docs(planning): 关闭 Q1-C 发布门禁` | 文件访问令牌治理完成，工程顺位进入时间语义。 |
| `37f48377` | `feat(time): 统一发布高风险时间语义` | UTC `TimeProvider` 与系统业务日覆盖发布高风险链路。 |
| `0f2ac3c5` | `feat(time): 收口经验业务日契约` | 经验自然日输入输出与审计口径统一为 `DateOnly` / 业务日。 |
| `7ac68c75` | `feat(database): 建立 schema 版本与 OpenIddict 迁移` | 业务库 ledger 与 provider-specific OpenIddict EF migrations 建立。 |
| `7f4cee49` | `feat(database): 迁移经验自然日为 date` | 首个正式业务 migration 完成 SQLite / PostgreSQL `date` 前滚。 |
| `08f8e43b` | `feat(database): 收口迁移并发与恢复门禁` | provider 锁、重入、备份恢复和严格 verify 进入候选门禁。 |
| `9733e73c` | `Merge branch 'master' into dev` | 稳定主线 ancestry 回灌完成，不改变专题边界。 |
| `bf6de436` | `feat(release): 建立产品版本单一真值` | .NET、npm、Rust、Tauri、Flutter 与镜像版本统一由 `version.json` 驱动。 |
| `5e89b76e` | `docs(git): 建立 master 回灌 dev 闭环` | PR 合入后必须先回灌 `dev` 的协作规则固化。 |
| `0d11210a` | `docs(frontend): 收敛多端产品主线` | 纯 Web 唯一正式主线、Flutter 条件维护、Tauri 冻结口径完成同步。 |
| `4efab166` | `fix(frontend): 收口候选 lint 与 Hook 契约` | 四个前端 workspace 的 lint / Hook warning 收敛。 |
| `f42b3453` | `ci(quality): 建立候选级质量验证入口` | Candidate Quality、warning-as-error 与完整前后端候选验证建立。 |
| `2c1c794e` | `ci(images): 增加镜像供应链门禁` | 镜像扫描、SBOM、provenance 与依赖安全进入发布链。 |
| `5d6461c2` | `test(quality): 补齐身份与数据边界回归` | JWT、idle session、Hub `sub` 与仓储租户边界补精确回归。 |
| `e0e4de6a` | `docs(planning): 完成 Q3 候选质量门禁` | Q3 关闭，顺位进入真实候选运行态验收。 |
| `f630ddbb` | `fix(database): 允许分表库接管 schema baseline` | 真实启动暴露的 split table baseline 接管阻断修复。 |
| `f0df59bb` | `fix(api): 修复异常边界宿主启动` | API 全局异常边界的宿主启动阻断修复。 |
| `efebb769` | `docs(release): 记录候选运行态验收` | SQLite 升级、Gateway PC/mobile 与宿主探针证据归档。 |
| `72074cdb` | `docs(release): 准备 26.7.1 正式发布` | 首个 26.7.1 发布记录和 tag 前置材料建立。 |
| `3f518101` | `release: 准备 Radish v26.7.1 正式发布 (#59)` | 正式发布 PR 合并；后续镜像漏洞使该 tag 成为失败尝试。 |
| `9ac3c861` | `fix(images): 收敛前端运行镜像暴露面` | 前端运行层切换并减少不必要资产，修复漏洞阻断。 |
| `bd83dd03` | `docs(release): 记录镜像阻断与正式补发` | 原 tag 不可变，补发轨道切到 1201。 |
| `6db3668b` | `fix(images): 收敛前端运行镜像并准备 26.7.1 补发 (#60)` | 1201 PR 合并，五镜像随后成功。 |
| `c3a1709e` | `docs(release): 记录补发镜像完成状态` | 1201 镜像供应链结果回写。 |
| `414f968f` | `fix(database): 兼容 PostgreSQL 物理标识符迁移` | Main migration 改为解析真实小写物理标识符。 |
| `336f847b` | `fix(deploy): 统一 DbMigrate OpenIddict 数据源` | DbMigrate 部署态显式使用 PostgreSQL OpenIddict 配置。 |
| `791419f9` | `docs(release): 准备 26.7.1.1202 部署补发` | 1201 生产阻断、数据保留和 1202 前滚边界归档。 |
| `2717a8a2` | `fix(release): 修复 DbMigrate PostgreSQL 部署阻断 (#61)` | 1202 PR 合并并产出成功镜像。 |
| `f6daa770` | `fix(database): 修复 OpenIddict PostgreSQL 初始迁移` | 固定 PostgreSQL 时间列模型并把 snapshot 差异纳入门禁。 |
| `d4e252e5` | `docs(release): 准备 v26.7.1.1203 修复补发` | 1202 OpenIddict 阻断与 1203 修复边界归档。 |
| `4a2fadbf` | `docs(release): 回写 1203 候选验证结果` | 空库、重入、schema adoption 与容器路径验证记录完成。 |
| `ae0cd43a` | `fix(database): 修复 OpenIddict PostgreSQL 初始迁移阻断 (#62)` | 1203 PR 合并；远程候选随后暴露 DateTime 测试装配缺口。 |
| `8251526f` | `fix(database): 稳定 PostgreSQL DateTime 参数契约` | UTC 参数规范化从 SQL 日志职责拆出，生产与集成测试统一复用。 |
| `af2d0856` | `docs(release): 准备 v26.7.1.1204 修复补发` | 1203 失败保持不可变，1204 补发记录建立。 |
| `3f449e5b` | `docs(release): 回写 1204 候选验证结果` | PostgreSQL `10/10`、后端 `635/635` 与 Candidate Quality 同款验证归档。 |
| `53539556` | `fix(database): 稳定 PostgreSQL DateTime 参数契约 (#63)` | 1204 PR 合并、tag 与五镜像成功，最终完成生产前滚。 |
| `db8d1821` | `docs(release): 关闭正式部署阶段` | 发布阶段关闭，生产 bootstrap UX 缺口转入维护线。 |

## 文档审阅与修正

- `Docs/planning/current.md` 已新增 2026-07-13 明日事项，并把工程顺位固定为首次管理员统一入口门禁。
- `Docs/development-plan.md` 与第三阶段总页已从 1202 / Q2-B / 发布候选旧状态切换到 Phase 4 发布后维护与功能完成。
- `Docs/guide/authentication.md` 已移除 SQLite-only、Auth 启动建库、直接 `dotnet ef database update` 和业务库无需 migration 的过期口径；数据库章节拆入 [OpenIddict 数据库与迁移](/guide/authentication-openiddict-database)，改为 provider-specific migrations + `Radish.DbMigrate apply` 单一写入口。
- `Docs/architecture/framework.md` 与 `Docs/guide/configuration.md` 已明确 PostgreSQL DateTime 参数规范化是独立持久化契约，不依赖 SQL 日志或 Npgsql legacy timestamp 初始化顺序。
- `Docs/deployment/guide.md` 已回写五镜像及 DbMigrate 真实生产验证，并如实说明 1204 首次管理员门禁尚未覆盖公开根入口与 Workbench。
- SQLite / PostgreSQL migration 项目作为 startup project 的 `has-pending-model-changes` 命令已分别实跑，均报告运行态模型与 snapshot 无变化。
- Q1-C、Q2-A/B/C、Q3、候选运行态与 1201-1204 发布细节已有专题 records，不在入口文档重复命令流水。
- 历史 changelog 与专题方案中的旧实现描述属于当时事实，不做追溯改写。
- `authentication.md` 已通过拆分降至专题深度文档 `1200` 行硬上限以内；deployment / configuration 的既有篇幅提醒本批未扩为无关拆分，后续按 touched-file 文档治理继续下降。

## 明日事项

1. 推送日终文档批次，经 PR 合入 `master` 后回灌 `dev`；文档同步不创建新发布 tag。
2. 先说明并确认首次管理员统一顶层门禁方案，再修改 `BrowserAppRouter / BootstrapGate / *Entry`，保持后端和生产数据不变。
3. 补公开、Workbench、私域与 OIDC 回调的路由契约测试；普通代码验证优先，不默认启动服务或执行浏览器 smoke。
4. 继续收集生产登录、内容参与、聊天、通知、Console、证书与迁移信号；没有新 P0/P1 后进入 F1 商城权益专题文档复核。

## 当前不做

- 不移动或复用 `v26.7.1.1204-release`，不因非阻断 UX 缺口立即补发。
- 不修改生产管理员、角色、`SystemBootstrapState`、schema ledger 或 `__EFMigrationsHistory`。
- 不在日终文档批次扩展代码、依赖、服务启动、数据库操作或页面 smoke。
