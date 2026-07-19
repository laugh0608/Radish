# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、当前判断、执行顺位、发布门禁与维护线**。
>
> 历史批次、命令级验证和页面级实现记录统一查看 [已完成摘要](/planning/archive)、[记录索引](/records/) 与 [开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`发布后长期维护与功能完成`
- **工程第一顺位**：`F4-D-C 聊天消息 Reaction：Pencil 与正式 Web`
- **产品下一顺位**：`先更新 PC / mobile Pencil，再实现消息气泡回应、picker、revision Store 与重连追平；成组运行态验收留给 F4-D-D`
- **复核日期**：`2026-07-19`
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
  - 2026-07-12 Release Go 候选运行态验收已通过：本地 SQLite 在备份后完成 schema / OpenIddict 升级与严格 verify，真实启动暴露的 split table baseline 接管和 API 异常管线宿主阻断已修复；Gateway PC `1920x1080` 与移动 `390x844` CSS 视口覆盖公开、私域、作者态和 Console 代表路径，宿主探针、公开 head 主路径与候选静态门禁通过。公开用户主页初始 HTML 缺少服务端 head 登记为非阻断 P2；本批未创建 tag、推送镜像或部署。
  - 2026-07-12 发布节奏已按个人开发者模式调整：工程 Release Go 证据已经满足正式发布要求；小规模受控试用改为发布后的早期真实使用观察，不再阻挡 tag。正式部署和部署后复核完成后，项目进入长期维护线与功能完成线，优先完成商城商品效力与权益履约，再推进主题和 i18n 完成度。
  - 2026-07-12 `v26.7.1-release` 已指向 `3f518101` 并推送；Docker Images `#16` 的 Candidate Quality 通过，但前端最终镜像基于 Debian 的运行层命中 High / Critical 漏洞，前端镜像未推送且正式部署未执行。旧 tag 保持不可变并记录为失败发布尝试；补发改用规范扩展标识 `v26.7.1.1201-release`，五个镜像必须全部重新构建并通过门禁。
  - 2026-07-12 PR `#60` 已合并到 `master`，`master / dev / origin` 已统一到 `6db3668b`；`v26.7.1.1201-release` 已推送，Docker Images `#17` 的 Candidate Quality 与五个正式镜像 job 全部成功，High / Critical 扫描、多架构推送、SBOM 和 provenance 已完成。当前只剩固定 tag 生产部署与部署后复核。
  - 2026-07-12 首次生产部署固定使用 `v26.7.1.1201-release`，PostgreSQL / Redis 健康且 Frontend 已启动，但 DbMigrate 在 baseline 后因 PostgreSQL 小写物理表与硬编码 PascalCase migration SQL 不一致触发 `42P01`；API、Auth、Gateway 未启动。同时确认 DbMigrate 缺少 OpenIddict PostgreSQL 配置并回退 SQLite。服务器数据与 ledger 保留，工程第一顺位切到 `v26.7.1.1202-release` 前滚修复。
  - 2026-07-12 PR `#61` 已合并到 `master`，`master / dev / origin` 已统一到 `2717a8a2`；`v26.7.1.1202-release` 与 Docker Images `#18` 已成功。生产保留 volume 前滚后，自然日 migration 与 OpenIddict PostgreSQL provider 修复均生效，但空 OpenIddict PostgreSQL 首次 `MigrateAsync` 因运行态模型受 Npgsql legacy timestamp 全局开关污染，与 snapshot 的四个 `timestamptz` 字段不一致而阻断。API、Auth、Gateway 仍未启动，工程第一顺位切到 `v26.7.1.1203-release`。
  - 2026-07-12 PR `#62` 已合并到 `master`，`master / dev / origin` 已统一到 `ae0cd43a`；`v26.7.1.1203-release` 已创建并推送。Docker Images `#19` 的 Candidate Quality 在 `635` 项后端测试中出现 `3` 项 PostgreSQL 集成测试失败，错误均为 `DateTimeKind.Unspecified` 无法写入 `timestamp with time zone`；五个镜像 job 未执行，生产未前滚。1203 保持为不可变失败尝试，工程第一顺位切到 1204。
  - 2026-07-12 PR `#63` 已合并到 `master`，`master / dev / origin` 已统一到 `53539556`；`v26.7.1.1204-release` 已推送，五镜像构建成功并以固定 tag 完成生产前滚。DbMigrate、API、Auth、Gateway、Frontend 均正常运行，首个管理员已成功创建，`P3-12-F` 正式发布执行至此关闭。
  - 部署后真实使用发现首次管理员门禁入口不一致：公开入口与 Workbench 未统一经过 `BootstrapGate`，点击“聊天”等私域入口后才显示初始化页。后端管理员存在性、事务与并发保护正确，且当前管理员已创建，因此该问题不阻断现有生产运行；工程第一顺位据此切换到顶层门禁统一编排、入口级重复包裹清理与精确路由契约测试。
  - 进入长期维护线后，`dev` 上的日常文档、小修复与连续开发提交默认按完整功能、成组维护或主动发版批次积累；不再为单独文档提交频繁创建 `dev -> master` PR。批次达到可交付边界后再统一集成，合入 `master` 后仍立即回灌 `dev`。
  - 2026-07-13 首次管理员统一入口门禁已在 `dev` 收口：`BrowserAppRouter` 统一承接公开、Workbench、私域、历史 Root 与 OIDC 回调入口，8 处入口级重复包裹已删除；OIDC 只在初始化状态 `ready` 后挂载。`radish.client` 333 项测试、type-check、lint 与生产构建通过，本批未启动服务、未执行 Gateway smoke、未创建 PR 或 tag。
  - 2026-07-13 Client / Console 跨应用导航首批契约已在 `dev` 收口：两端保持独立 SPA 与 OIDC client，新增受控 `backTo`、当前标签页认证往返保持、全局来源返回、Workbench / WebOS 安全入口和正式 Web 对象回看；client / Console 静态验证与生产构建通过，本批未启动服务、未执行 Gateway smoke、未创建 PR 或 tag。
  - 2026-07-13 F1-A 订单履约安全已在 `dev` 完成本地实现：支付失败与履约失败分流，重试前校验成功扣款流水，发放完全读取订单快照，持续权益 / 背包资源 ID 分离，固定日期快照和 schema ledger 历史回填已落地；后端全量 `631` 项通过、`10` 项环境用例按配置跳过，Client / Console production build 通过。本批继续留在 `dev`，未启动服务、未执行 Gateway smoke、未创建 PR 或 tag。
  - 2026-07-13 F1-B 消耗品幂等与使用流水已在 `dev` 完成本地实现：改名卡、经验卡和萝卜币红包统一为原子扣减与效果写入，成功结果可按同键回放且同键异参冲突；`ShopEntitlementOperation`、ledger 迁移、正式 Web 稳定幂等提交与 Console 用户流水回看已落地。后端全量 `636` 项通过、`10` 项环境用例按配置跳过，Client `336` 项、Console `34` 项通过，两端 lint 与 production build 通过。本批继续留在 `dev`，未启动服务、未执行 Gateway smoke、未创建 PR 或 tag。
  - 2026-07-13 F1-C 权益唯一选择与失效已在 `dev` 完成本地实现：`UserActiveBenefit` 以租户、用户和权益类型建立唯一当前指针，启用 / 停用 / 过期 / 撤销统一进入事务仓储和业务流水；服务端按 UTC 实时派生 `Available / Active / Expired / Revoked`，定时任务只物化过期事实并通过可靠 Outbox 请求通知。`20260713_003_user_active_benefit` ledger 迁移、doctor 冲突诊断、历史多激活回填、Client 状态展示、Console 受权撤销与回归测试已落地；所有权益类型仍保持不可售 / 不可启用。后端全量 `643` 项通过、`10` 项环境用例按配置跳过，前端 `379` 项通过，两端 lint 与 production build 通过。本批继续留在 `dev`，未启动服务、未执行 Gateway smoke、未创建 PR 或 tag。
  - 2026-07-14 F1-D 徽章与称号真实生效已在 `dev` 完成本地实现：共享 `UserAdornmentVo`、租户隔离批量装配、UTC 实时有效性与公开附件降级已接入公开主页、帖子、问答回答、评论及回复；Client 使用同一身份装饰组件，商品可售 / 可启用 / 配置约束统一由服务端能力元数据提供，Badge / Title 能力开放但历史商品保持下架，其余权益继续关闭。后端全量 `648` 项通过、`10` 项环境用例按配置跳过，Client `339` 项、Console `35` 项通过，两端 type-check、lint、production build 与 `validate:baseline:quick` 通过。本批未启动服务、未执行 Gateway smoke、未创建 PR 或 tag；工程第一顺位进入 F1-E。
  - 2026-07-14 F1-E 商城专题验收已完成：修复通用权益流水中消耗品专属字段的历史 `NOT NULL` 约束并增加前滚迁移，真实完成徽章 / 称号激活、同类切换、停用与公开身份同步；订单、背包、公开主页、帖子评论和 Console 排障页均通过 PC / mobile 成组复核。后端全量 `652` 项通过、`11` 项环境用例按配置跳过，解决方案构建为 0 warning / 0 error；临时权益、选择指针和操作流水已清理，数据库完整性正常。详细证据见 [F1-E 商城权益专题验收记录](/records/f1-e-shop-entitlement-stage-acceptance-2026-07-14)，工程第一顺位进入 F2。
  - 2026-07-14 F2 主题系统专题验收已完成：四套正式主题统一根节点、共享 UI 与 Ant Design 运行时，Header 在 PC / mobile 均提供正式入口，Theme 商品资源白名单和权益激活 / 停用接入服务端权威选择。匿名持久化、登录激活、同类切换、跨标签同步、暗夜页面族、`/desktop` 与 Console 边界均已复核；临时权益数据已清理。详细证据见 [F2 主题系统专题验收记录](/records/f2-theme-system-stage-acceptance-2026-07-14)，工程第一顺位进入 F3 i18n 完成度治理。
  - 2026-07-15 F3-B2 已完成本地实现与静态回归：Console 用户 / 内容治理 / 订单业务文案进入独立中英文域资源，client Messages / Me 的语言、日期、数字和复数残余已收口；订单与治理高频失败具备稳定 HTTP status、`Code / MessageKey` 和双语服务端兜底，业务控制流不再依赖展示文案。四个前端 workspace 定向验证、两端生产构建、后端全量 `660` 项测试和解决方案构建均通过；未启动服务、未执行 Gateway smoke、未创建 PR、tag 或发布。
  - 2026-07-15 F3-C1 Console 设置域已完成本地实现与静态回归：个人设置、系统设置列表 / 编辑 / 筛选 / 历史进入独立双语资源；代码注册设置按稳定 `voKey` 映射本地名称、说明和影响范围，未知定义保留服务端元数据兜底。在线词元编辑明确后置为独立“本地化资源管理”专题，不混入 `SystemConfig`；Console 41 项测试、type-check、lint 与 production build 通过。
  - 2026-07-15 F3-C2 Console 商品配置域已完成本地实现与静态回归：商品列表、详情、表单和能力说明进入独立双语资源；可售控制只认服务端能力矩阵，能力说明新增稳定 key，商品有效期改用结构化字段，商品高频写入失败补齐稳定状态与双语错误契约。Console 44 项测试、strict type-check、lint 与 production build，后端 661 项测试及解决方案构建通过；未启动服务或执行 Gateway smoke。
  - 2026-07-15 F3-C3 Docs 业务域已完成本地实现与静态回归：client Docs 作者态与 Console 文档治理进入宿主双语资源，日期、数字、复数和 Wiki 结构化错误形成闭环；Docs 标题、摘要、正文、slug、来源路径及访问名单仍保留原文。client 351 项、Console 47 项与 Wiki / 资源定向后端测试通过；未启动服务或执行 Gateway smoke。
  - 2026-07-15 F3-C4 client 圈子域已完成本地实现与静态回归：系统词元、locale 日期 / 数字和英文数量规则形成闭环，帖子与用户内容继续保留原文；共享 UserFollow API 改用宿主本地化 fallback 与结构化错误，后端补齐稳定状态、`Code / MessageKey` 和双语资源。client 354 项、后端 662 项测试及 client production build 通过，11 项环境用例按配置跳过；未启动服务或执行 Gateway smoke。
  - 2026-07-15 F3-C5 client 宠物域已完成本地实现与静态回归：`/pet` 与 `/me` 摘要按稳定阶段、心情和动作字段解析系统词元，宠物名称继续保留原文；日期、数字、次数、冷却和流水反馈按 locale 与英文单复数展示。Pet API 与后端高频失败补齐结构化错误和双语资源。client 357 项、后端 664 项测试与 client production build 通过，11 项环境用例按配置跳过，解决方案构建 0 warning / 0 error；未扩展宠物经济、任务、Console 配置或公开名片，未启动服务或执行 Gateway smoke。
  - 2026-07-15 F3-C6 client 经验域已完成本地实现与静态回归：经验详情、`/me` 摘要、桌面状态与共享 `ExperienceBar` 由宿主双语词元和 locale formatter 驱动，分页、图表与英文数量规则形成闭环；系统类型只按稳定 `voExpType` 解析，未知类型保留原值，等级名、备注和冻结原因继续保留配置或人工原文。当前经验 API 改用结构化 `ApiResponseError`，未登录、越权和经验数据不存在具备稳定 HTTP status、`Code / MessageKey` 与 API 双语资源。client 363 项、共享 UI 8 项、后端 668 项测试通过，11 项环境用例按配置跳过，client production build 与解决方案构建通过；未改经验规则、等级公式、上限、排行、冻结语义、数据库或 Console，未启动服务或执行 Gateway smoke。
  - 2026-07-15 F3-C7 client 萝卜资产域已完成本地实现与静态回归：萝卜坑五个标签、`/me` 资产摘要、Profile 钱包 / 流水、资料余额与桌面余额统一使用宿主双语词元、locale formatter 和 long 字符串安全金额；交易类型、状态、统计分类和安全日志只按稳定字段解析，未知词元保留原值，人工及审计内容保持原文。Coin / PaymentPassword API 改用结构化 `ApiResponseError`，高频失败具备稳定 HTTP status、`Code / MessageKey` 与 API 双语资源，模拟通知入口已删除并继续由 `/notifications` 承接。client 370 项、共享 UI 8 项、后端 674 项测试通过，11 项环境用例按配置跳过，client production build、解决方案构建与 Baseline Quick 通过；未改资产、转移、奖励、统计、口令锁定等业务规则，未改数据库、迁移或 Console，未启动服务或执行 Gateway smoke。
  - 2026-07-16 F3-C8 已完成本地实现与静态收口：client 低频页面、公开承诺、统一公开 head、共享反馈 / 上传 labels 与结构化错误已覆盖真实消费者；上传边界同步固定业务类型、权限、路径、服务端 MIME / 文件签名、禁用 SVG、单次提交和失败清理，头像裁切具备处理期关闭锁与失效任务隔离。实际链路复核中暴露的论坛发布原子性、PostgreSQL 唯一冲突恢复和动态错误参数也已按事务保存点与安全标量参数契约治理。两个功能提交为 `d5341095 / 762e32ac`；最终静态基线为 client 415 项、共享 UI 21 项、Console 52 项、`@radish/http` 13 项、后端 814 项通过，12 项 PostgreSQL 环境用例跳过，解决方案构建 0 warning / 0 error，两端 production build 与 Baseline Quick 通过。未启动服务或执行 Gateway smoke，详见 [F3-C8 静态收口记录](/records/f3-c8-shared-feedback-upload-shell-static-closure-2026-07-16)。
  - 2026-07-17 F3-C9 已完成本地实现与静态收口：Console 角色授权、分类 / 标签、表情、经验和萝卜管理的正式路由与真实消费者进入宿主双语资源，日期、数字、英文数量和 LongId / long 金额使用统一 locale formatter 与字符串安全口径；动态详情面包屑、稳定枚举、权限动作、附件上传和结构化错误形成闭环。表情失败响应迁入真实 HTTP 契约并保留批量冲突数据，经验 / 萝卜高频管理失败补齐稳定 `Code / MessageKey`，`withAuth` 刷新只允许复用原请求配置单次重放。Console 56 项、`@radish/http` 16 项、后端 817 项测试通过，12 项 PostgreSQL 环境用例跳过；Console lint / production build、解决方案构建、Baseline Quick 与仓库卫生检查通过。未启动服务或执行 Gateway smoke，下一顺位进入 F3-D 专题验收。
  - 2026-07-17 F3-D 已完整关闭：Public / Private / Auth、Console 认证 / 无权限与管理员受权内页均通过 `zh / en × PC / mobile` 代表运行态矩阵；Dashboard、用户、治理、订单、应用、个人资料、Hangfire、not-found 和移动功能抽屉均正常，根级宽度稳定。验收修复了 Auth 官方客户端元数据、嵌套 OIDC 语言往返、Console 低频正式路由词元、LongId 字符串门禁及个人资料 Form 挂载时序警告；未修改管理员账号、角色、权限或 Main 业务数据。详见 [F3-D i18n 专题验收记录](/records/f3-d-i18n-stage-acceptance-2026-07-17)，工程第一顺位进入 F4-A。
  - 2026-07-18 一对一私聊与会话管理已确认为下一完整功能专题并完成设计：复用 `Channel / ChannelMember / ChannelMessage / ChatHub / ChatApp`，新增 `DirectConversation` 会话元数据；批次 A 先治理私有频道成员 ACL、持久化发送幂等和 Chat 附件访问，不提前开放页面入口。
  - 2026-07-18 一对一私聊批次 A 已完成本地实现：两项 schema ledger migration、`DirectConversation` / 归档 / 消息幂等约束、REST / Hub / 附件共用 ACL、Chat 私有附件与正式 Web 认证图片加载均已落地；后续生命周期和页面入口分别由批次 B / C 承接。
  - 2026-07-18 一对一私聊批次 B 已完成本地实现：幂等建会话、接受 / 拒绝 / 阻断 / 解除 / 归档生命周期、陌生请求首条约束、无正文预览通知、权威会话摘要、新消息恢复归档与可靠附件绑定已落地。解决方案构建 `0 warning / 0 error`，后端 `845` 项通过、`15` 项 PostgreSQL 环境用例跳过，`Baseline Quick` 通过；公开个人页和 `/messages` 新交互继续留在批次 C。
  - 2026-07-18 一对一私聊批次 C 已完成本地实现：Pencil 已覆盖 PC 请求 / 归档 / 阻断与移动列表 / 请求详情；公开个人页发消息、匿名登录回流、`/messages` 权威分区与动作、归档视图、深链恢复、移动列表 / 详情切换、会话状态实时刷新及中英文结构化错误均已落地。client 422 项测试、type-check、lint、production build 与 `Baseline Quick` 通过；未启动服务或执行 Gateway smoke，下一顺位进入批次 D 成组验收。
  - 2026-07-18 一对一私聊批次 D 已完成并关闭专题：SQLite / PostgreSQL migration 与 Chat Service / Controller / Hub / 附件边界定向回归通过，Gateway 下真实 Chromium `1920x1080` 与 `390x844 @ DPR 3` 使用三个普通账号完成互关私聊、陌生请求、接受 / 拒绝、阻断 / 解除、归档 / 自动恢复、未读、深链及中英文状态。验收中按共同根因修复 ChatHub 路由生命周期竞态、活跃会话已读推进和 SignalR / YARP 查询凭据日志；临时账号、会话、通知、授权、`81` 个 token、浏览器凭据与备份均已清理，四库完整性正常且精确残留为 `0`。下一顺位进入通知中心深化专题的现状审计与设计。
  - 2026-07-18 F4-A 首轮证据归因已完成：仓库内唯一明确的发布后生产 UX 证据是首次管理员入口门禁不一致，该 `P2` 已由 `54852c73` 在 `dev` 修复并等待正常批次发布复核；其余六链路数据均来自本地种子、受控 smoke、自动化或发布前设计反馈，不能证明真实使用价值。F4-A 的采集说明与模板已经形成，但按个人开发者节奏冻结为最终收尾资产，不再作为当前顺位或功能选题前置。详见 [F4-A 首批真实使用证据整理与反馈归因记录](/records/f4-a-first-real-usage-evidence-attribution-2026-07-18)。
  - 2026-07-18 F4-B-A 通知中心现状审计与设计已完成：现有生产者、月表 / 用户关系 / 偏好、Service / Repository、Controller / Hub、正式 Web / WebOS / Flutter 和测试已交叉复核。专题固定采用稳定通知定义、结构化目标、用户通知分组和权威摘要 revision；未读缓存、缓存去重、虚假 delivery 状态和客户端关键字分类退出正确性链路。下一批进入 F4-B-B 数据、查询与服务端契约，不提前改正式 Web 页面。
  - 2026-07-18 F4-B-B 通知数据、查询与服务端契约已完成：现有生产者统一使用注册类型、模板参数、结构化目标和源事件 UTC 时间；Message 库专属仓储事务已覆盖幂等重放、部分接收者、偏好抑制、固定窗口聚合、权威分组 / 摘要 revision、并发已读与容量清理。SQLite / PostgreSQL ledger migration、新 HTTP API、SignalR revision 提示及 `@radish/http` 契约已落地；解决方案构建、后端 `869` 项测试、Baseline Quick、正式 Web 生产构建和仓库卫生检查通过，`16` 项环境用例按配置跳过，本专题两项 PostgreSQL 定向集成测试已在隔离数据库通过。按停止线未改通知页面结构、未启动服务或执行浏览器 smoke；下一顺位进入 F4-B-C。
  - 2026-07-18 F4-B-C 正式 Web 通知工作区已完成：Pencil `P12 / P12B / P26 / P26B / P26C` 覆盖 PC / mobile 权威列表、偏好与恢复状态；`/notifications`、共享 Store、导航角标、Workbench 和 WebOS 复用面已迁移到分组 / 摘要 / 偏好 / 结构化 target / revision 契约。跨标签只传账号隔离 revision，未读不再本地增减，关键字分类与泛化目标回退已删除；client `425` 项、后端通知定向 `25` 项、type-check、lint、production build、Baseline Quick 和仓库卫生通过，`1` 项 PostgreSQL 环境用例按配置跳过。未启动服务或执行浏览器 smoke；F4-B 仍待 F4-B-D 真实成组验收。
  - 2026-07-18 F4-B-D 已完成并关闭通知中心深化专题：三个普通账号在 Gateway 正式路径完成关注、评论 / 回复、点赞聚合、私信请求、商城购买、偏好抑制、已读竞态、多标签 / 离线 / cursor 恢复和目标失效，`zh / en × PC / mobile` 与 WebOS 复用面均通过。验收按共同根因补齐历史 delivery schema 前滚、仓储写入行数门禁、关注软删恢复、触发者名称、结构化目标可用性和 NotificationHub 生命周期；临时业务数据、凭据、容器与备份均已清理，六库完整性与精确残留正常。F4-B 正式关闭，工程第一顺位进入 F4-C 聊天历史搜索与消息定位的现状审计和专题设计。
  - 2026-07-18 F4-C-A 已完成现状审计和专题设计：搜索范围固定为当前会话 / 全部当前可见会话，使用派生 `SearchText`、服务端成员 ACL、SQLite / PostgreSQL 字面量包含、快照 cursor 和现有 `GetMessageWindow` 定位；PC / mobile 页面、隐私、恢复、停止线和 A-D 批次已明确。下一顺位进入 F4-C-B 服务端权威检索契约，不提前改页面。
  - 2026-07-19 F4-C-B 已完成：`SearchText` 共用规范化器、`20260718_003_chat_message_search` migration、历史分批回填与备份恢复、批量成员 ACL、专属 Repository / Service、POST API、查询指纹 / 消息快照 / 可见频道集合 cursor、稳定双语错误和 `@radish/http` 契约已落地。后端全量 `896` 项通过、`18` 项环境用例按配置跳过；本专题 PostgreSQL 17 migration 与 `strpos` 字面查询 `2` 项另行实跑通过，解决方案构建、Baseline Quick 与相关 workspace 检查通过。按停止线未改 Pencil 或正式 Web 页面，下一顺位进入 F4-C-C。
  - 2026-07-19 F4-C-C 已完成：Pencil `P13C / P13D / P27C / P27D`、正式 `/messages` 搜索工作区、独立搜索与消息导航 Hook、PC 侧栏、mobile 单列状态、cursor 继续加载、`GetMessageWindow` 权威定位、Back / Forward 恢复、中英文和完整页面状态已落地。Client `430` 项、type-check、lint、production build、Baseline Quick 与仓库卫生通过；未启动服务或执行浏览器 smoke，下一顺位进入 F4-C-D。
  - 2026-07-19 F4-C-D 已完成并关闭专题：Gateway 正式路径与 WebOS 复用面完成双账号 `zh / en × PC / mobile` 的当前 / 全部会话、分页、定位、历史、撤回和权限矩阵；验收修复 Enter 提交、PC Forward 侧栏状态和撤回摘要残留。临时账号及关联数据精确残留为 `0`，六库完整性和迁移 verify 通过。详见 [F4-C-D 成组验收记录](/records/f4-c-d-chat-message-search-stage-acceptance-2026-07-19)，工程第一顺位进入 F4-D-A 聊天消息 Reaction 现状审计与专题设计。
  - 2026-07-19 F4-D-A / B 已完成：专题设计固定 Chat 专属持久化、`CanReact`、目标状态幂等、消息 revision、完整 Hub 快照与首批不通知边界；服务端已落地 migration、原子仓储、30 天 operation 清理、撤回同步软删除、Service / API / `@radish/http` 与稳定双语错误。下一顺位进入 F4-D-C Pencil 与正式 Web。

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
- [v26.7.1-release 正式发布记录](/records/m15-release-record-2026-07-12)
- [v26.7.1.1201-release 补发记录](/records/m15-release-record-v26.7.1.1201-2026-07-12)
- [v26.7.1.1202-release 部署修复补发记录](/records/m15-release-record-v26.7.1.1202-2026-07-12)
- [v26.7.1.1203-release OpenIddict 修复补发记录](/records/m15-release-record-v26.7.1.1203-2026-07-12)
- [v26.7.1.1204-release PostgreSQL DateTime 修复补发记录](/records/m15-release-record-v26.7.1.1204-2026-07-12)
- [2026-07-12 日终提交回顾与文档审阅](/records/p3-12-day-end-doc-review-2026-07-12)
- [2026-07-15 F3 i18n 日终提交回顾与文档审阅](/records/f3-i18n-day-end-doc-review-2026-07-15)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [商城商品效力与权益履约专题](/features/shop-product-effect-entitlement-fulfillment)
- [前端主题与 i18n 实施说明](/frontend/theme-i18n-implementation)
- [F3 i18n 完成度治理实施说明](/frontend/i18n-completion-governance)
- [F3-D i18n 专题验收记录](/records/f3-d-i18n-stage-acceptance-2026-07-17)
- [F4-A 首批真实使用证据整理与反馈归因记录](/records/f4-a-first-real-usage-evidence-attribution-2026-07-18)
- [F4-C-B 聊天历史搜索服务端权威检索完成记录](/records/f4-c-b-chat-message-search-server-contract-2026-07-19)
- [F4-C-C 聊天历史搜索正式 Web 工作区完成记录](/records/f4-c-c-chat-message-search-web-workspace-2026-07-19)
- [F4-C-D 聊天历史搜索成组验收记录](/records/f4-c-d-chat-message-search-stage-acceptance-2026-07-19)
- [F4-D 聊天消息 Reaction 设计](/features/chat-message-reaction-design)
- [F4-D-B 聊天消息 Reaction 服务端权威契约完成记录](/records/f4-d-b-chat-message-reaction-server-contract-2026-07-19)
- [F2 主题系统专题验收记录](/records/f2-theme-system-stage-acceptance-2026-07-14)
- [产品版本与发布标识治理](/guide/version-governance)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [Client 与 Console 跨应用导航契约](/frontend/client-console-navigation-contract)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

### 1. 完成 F1 商城与 F2 主题专题

- 已完成商城路线、支付与幂等说明、商城实体 / Service、正式 Web 商城和 Console 现有实现的首轮交叉复核。
- 已建立[商城商品效力与权益履约专题](/features/shop-product-effect-entitlement-fulfillment)，明确订单履约、消耗品使用、权益选择、过期、撤销、审计和商品开放矩阵。
- `F1-A / F1-B / F1-C / F1-D / F1-E` 已完成本地实现、静态回归与批次级运行态验收。Badge / Title 仅开放服务端能力，不自动恢复任何历史商品销售；其余权益类型继续关闭。
- `F2` 已完成四主题统一运行时、正式 Web 入口、Theme 权益资源契约和 PC / mobile 页面族验收；暗夜 / 樱花商品仍保持下架，是否销售继续由管理员显式决定。
- 首次管理员门禁与既有文档提交继续保留在 `dev`，待形成完整功能或成组维护批次后统一集成。

### 2. 进入发布后常态开发

- 长期维护线接收用户反馈、bug、安全、依赖、迁移和部署问题。
- F4-A 已完成仓库证据盘点、观测盲区归因、生产五库与时间分表校准，并形成只读脱敏采集说明和批次模板；该专题现已冻结为项目最终收尾资产。
- 当前功能开发不等待生产六链路聚合，也不把缺少真实使用证据作为拒绝功能选题的理由。
- 继续从现有 Docs、聊天、通知、治理、宠物及其他既有业务边界中，只选择一个长期价值和系统边界明确的完整专题推进。

## 下一顺位

1. 一对一私聊专题已经完成批次 A-D 并关闭；详细矩阵、修复、临时数据清理与未覆盖风险见 [一对一私聊与会话管理设计](/features/chat-direct-conversation-design)。
2. [F4-B 通知中心深化与通知治理](/features/notification-center-deepening) 的 A-D 批已经完成，真实生产链路、偏好、聚合竞态、写操作、恢复、目标失效和清理矩阵全部通过，专题已关闭。
3. [F4-C 聊天历史搜索与消息定位](/features/chat-message-search-design) 的 A-D 批已经完成；权威检索、跨库迁移、正式 Web / WebOS、双账号 PC / mobile 矩阵、共同根因修复和临时数据清理均通过，专题关闭。
4. [F4-D 聊天消息 Reaction](/features/chat-message-reaction-design) 的 A / B 批已经完成设计与服务端权威契约；下一顺位进入 F4-D-C，先更新 PC / mobile Pencil，再实现正式 Web / WebOS 共用的消息回应 UI、picker、revision Store 与重连追平。消息置顶、逐条已读和移动系统通知继续分别后置，主动生产证据采集保持最终收尾冻结。

## 昨日事项（2026-07-18）

1. 已按六条链路盘点生产发布记录、明确用户反馈、本地日志、SQLite 数据、审计配置和相关数据模型；没有启动服务、安装依赖或读取无关历史归档。
2. 已确认本地样本只包含 `System / Admin / TestUser / F3DTester717`、受控帖子 / 评论 / 聊天 / 通知 / 举报和 Development 日志，不将其写成真实使用价值证据。
3. 已将首次管理员入口问题归为高可信 `P2` 维护项；2026-07-12 的发布与迁移 `P0/P1` 均已闭环，当前没有新的未处置阻断。
4. 已建立 F4-A 专题记录，明确生产证据获取入口、脱敏字段和判断门槛；本次生产证据盘点没有形成产品推荐，但不限制当前从既有功能边界选择专题。
5. 已校准生产五库、宿主日志和时间分表边界，建立长期采集说明与批次模板；本轮没有生产数据访问或脱敏导出，未新增真实使用结论。
6. 已按个人开发者节奏将主动生产证据采集冻结为项目最终收尾事项；当前顺位回到下一项完整功能专题的选择与设计。
7. 已确认一对一私聊与会话管理为下一专题并完成设计说明；下一开发批次固定为数据、成员 ACL、消息幂等和 Chat 附件访问，不并行展开搜索、Reaction、置顶或逐条阅读回执。
8. 已完成一对一私聊批次 A 的数据与访问边界实现；该批按停止线没有提前开发公开主页和 `/messages` 新交互。
9. 已完成一对一私聊批次 B 的生命周期、权威摘要、陌生请求事务与可靠附件绑定；下一批固定先更新 Pencil 设计源，再进入正式 Web 页面开发。
10. 已完成一对一私聊批次 C 的设计源和正式 Web 页面；下一批固定为定向回归与双账号 PC / mobile 成组验收，不并行展开新的聊天能力。
11. 已完成一对一私聊批次 D 的定向回归、真实双账号 PC / mobile 成组验收、成因修复与临时数据清理；专题完成标准全部满足并关闭，下一顺位进入通知中心深化专题设计。
12. 已完成 F4-B-A 通知中心生产者、数据、服务、实时、客户端和文档审计，建立稳定分类、结构化目标、偏好、聚合、权威摘要、容量、迁移与验收设计；下一顺位进入 F4-B-B 服务端实现。
13. 已完成 F4-B-B 定义注册、结构化生产者、Message 库 migration、专属仓储事务、偏好 / 分组 / 摘要 / 容量清理、新 HTTP / SignalR 及前端契约；下一顺位进入 F4-B-C 正式 Web 通知工作区。
14. 已完成 F4-B-C Pencil PC / mobile 权威通知工作区、正式 `/notifications`、共享 Store / 导航角标、Workbench / WebOS 复用、结构化 target、revision / cursor / 跨标签恢复和中英文静态回归；下一顺位进入 F4-B-D，F4-B 尚未关闭。
15. 已完成 F4-B-D 三普通账号真实生产链路、偏好、聚合竞态、写操作、目标失效、多标签 / 断线 / cursor、`zh / en × PC / mobile` 与 WebOS 成组验收；共同根因修复、定向回归、数据清理和数据库完整性检查均通过，F4-B 正式关闭。下一顺位进入 F4-C 聊天历史搜索与消息定位的现状审计和专题设计。
16. 已完成 F4-C-A Chat 数据、索引、ACL、搜索文本、跨库查询、cursor、消息定位、PC / mobile 页面和恢复边界审计与专题设计；下一批固定为 F4-C-B 服务端权威检索契约，不提前修改 Pencil 或正式 Web 页面。

## 今日事项（2026-07-19）

1. 已完成 `SearchText`、顺序索引、`20260718_003_chat_message_search` ledger migration、历史分批回填、doctor / apply / verify / 重入与 SQLite 备份恢复，不修改原消息正文；撤回同步清除派生搜索文本。
2. 已建立写入、migration 和测试共用的搜索文本规范化器，固定 mention 可见名、空白 / 控制字符、内部资源协议、Form C Unicode、invariant 大小写与 4000 字符边界。
3. 已扩展批量可见频道快照并实现专属搜索 Repository；当前 / 全部会话共用既有成员 ACL，管理员不穿透私聊，SQLite `instr` 与 PostgreSQL `strpos` 对 `% / _ / \\ / 引号` 执行参数化字面匹配。
4. 已实现 `IChatMessageSearchService`、POST `ChannelMessage/Search`、DTO / Vo、稳定双语错误和 `@radish/http` 契约；cursor 绑定账号与查询指纹、消息快照、可见频道集合和 `(CreateTime, Id)`，条件或权限变化显式返回 `Chat.SearchCursorInvalid`。
5. 后端全量 `896` 项通过、`18` 项环境用例按配置跳过；本专题 PostgreSQL 17 migration / Repository `2` 项另行实跑通过，解决方案构建为 `0 warning / 0 error`，`@radish/http` type-check / lint 与 Baseline Quick 通过。
6. 已更新 Pencil `P13C / P13D / P27C / P27D`，四个 PC / mobile 搜索与状态画板均通过布局检查。
7. 已完成正式 `/messages` 搜索工作区、内存搜索状态、cursor 继续加载、消息导航 Hook、`GetMessageWindow` 权威定位、Back / Forward 恢复和 WebOS 同组件兼容；关键词不进入 URL、持久化或日志。
8. Client `430` 项、type-check、lint、production build、`@radish/http` 检查、Baseline Quick、仓库卫生与 `git diff --check` 均通过；未启动服务或执行浏览器 smoke，下一顺位进入 F4-C-D。
9. 已完成 F4-C-D 双账号运行态矩阵，覆盖中英文、PC / mobile、当前 / 全部会话、日期、分页、消息定位、Back / Forward、撤回、互关私聊、陌生请求、拒绝 / 阻断、无权频道与 WebOS；三个共同根因已修复并补测试。
10. 两批临时账号、会话、消息、关注、通知、授权、余额、Outbox、日志和备份均已清理；精确残留为 `0`，六库完整性与 DbMigrate verify 通过，F4-C 正式关闭。
11. 已完成 F4-D-A 现状审计与专题设计，固定 Chat 专属 Reaction 表与 operation ledger、独立 `CanReact`、目标状态幂等、消息 revision、完整 Hub 快照、撤回一致性和首批不产生通知中心事件的边界。
12. 已完成 F4-D-B 服务端权威契约：实体与 `20260719_004_chat_message_reaction` migration、原子设置 / 回放 / 10 种上限、30 天小批次清理、消息撤回同步软删除、Service / API / Hub / `@radish/http`、LongId 和双语稳定错误均已落地；下一顺位进入 F4-D-C。

## 明日事项（2026-07-20）

1. 进入 F4-D-C，先更新 PC / mobile Pencil 的消息回应展示、已有回应操作、picker、加载 / 错误 / 长聚合与键盘状态。
2. 实现正式 `/messages` 与 WebOS 共用的 Reaction Store，以 `messageId + revision` 合并 HTTP / Hub 完整快照，并在历史、定位窗口、新消息、重连和账号 reset 时正确追平或清空。
3. 接入 Unicode / sticker picker、目标状态 operation ID、公开 / 公告 / 私有 / Direct 状态禁用、撤回移除、中英文与四主题语义 token；本批优先静态和组件回归，真实双账号矩阵留给 F4-D-D。
4. 消息置顶、逐条已读、移动系统通知和主动生产证据采集继续后置，不与 F4-D 并行展开。

## 并行维护线

- 公开 head、动态 sitemap、head snapshot 与生产公开域名配置。
- 附件持久化与访问边界：继续补 `Document / Wiki` 业务域 ACL 与历史迁移、分片 attachment correlation、durable quota settlement，以及多实例共享临时存储 / 分布式锁；Chat 附件可靠绑定已在私聊批次 B 完成。
- HTTP 认证恢复边界：关键非幂等写入需要幂等 / 去重保护，client 附件 XHR 的 URL 配置来源需收敛到 `getApiClientConfig()`。
- 镜像漏洞门禁分层：Critical 与可修复 High / Critical 保持阻断，无修复 High 转为可追溯维护项与定期复核。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容维护。
- Flutter 只做既有 MVP 的阻断、安全与认证兼容维护；不默认新增功能、扩 iOS 或追平 Web。
- 历史大文件、全量仓库卫生、TypeScript strict 和共享前端边界按 touched-file / 专题治理持续下降，不作为进入 F 的全量前置。

## 当前不做

- 不在产品仍有明确功能、维护或设计任务时重启 F4-A 主动生产证据采集，不把它作为功能选题、开发或发布前置。
- 不在 `dev` 或 PR head 上提前创建正式 tag，不绕过 `master` required checks、镜像扫描或部署前备份。
- 发布执行期间不扩展全仓 strict、完整 E2E、页面改版或无关重构。
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
