# 2026-11-01 规划结构统一记录

日期使用 Asia/Shanghai。本批按所有者确认，统一整体规划、大专题、子专题及实施切片的职责，保留现有目录、编号和产品 / 执行边界。

## 结果与边界

新增七条能力主线地图；补齐依赖治理的原方案 / 证据路由、日志 L2 与 Native P 切片归属。明确细项唯一状态来源，current 只汇总活跃范围、阻塞和下一步。没有改动技术合同、既有验收结果、代码、依赖、运行或部署，也不推进 Windows SQLite 夹具、Hangfire verify、日志或 P8-C 实施。

## 验证

- `npm run check:docs` 通过：952 个文件文本卫生、877 份 Markdown / 1380 个本地相对链接通过；根协作入口未改。
- `npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。记录索引原有 349 行，本次增加到 351 行，仍有超过 300 行建议值的非阻断提醒；未将无关历史拆分混入本批。
- 本批 11 份文档另核验 363 个本地链接与 9 个锚点；七条主线表登记的 42 个子专题来源无重复主要归属。
- 对照原文确认：三个功能合同只新增归属 / 路由，current 的执行顺位、下一动作及平台边界逐字保留；移出的历史摘要仅调整相对链接。改动限定文档，暂存区为空，未提交或推送。

<a id="retained-history"></a>

## 从 current 移入的历史批次摘要

下文按 2026-11-01 整理前原貌保留，只代表各日期的历史事实，不提供当前执行指令。


- `2026-10-31` [日终回顾](day-end-doc-review-2026-10-31.md)覆盖当日 18 个提交及文档校准：SQLite 统一、Linux / PG / 托管验证、Hangfire 本机重建和 Windows inventory 已分批留证；Windows SQLite 夹具与 Razor 身份门禁仍未关闭。当天最后两次文档提交仅本地保存，未新增运行、发布或部署。

- `2026-10-31` [Hangfire 本机源码重建](hangfire-rebuild-2026-10-31.md)完成：42 项封存，五目标严格构建与干净目录 DLL 字节一致重放，18 组 SQLite / PG17 / PG16 最终产物调用、四类日志契约和反向拒绝验证通过；已归档并清理私有 SDK、数据库和工作副本。Windows Razor、正式源码入口及完整发行审查保持未关闭。

- `2026-10-31` [PG 接库回归与托管 CI 本地接线](postgres-regression-2026-10-31.md)完成：PG17 双配置各 4,137 项通过 / 102 项 Redis、Valkey 预期跳过，PG16 双配置各 43 项通过，四组真实 Hangfire worker 通过，资源和临时构建目录已清理。托管 CI 本地 28 项契约及 YAML / 旧 job 对照通过；未 push、远端运行、发布或部署。
- `2026-10-31` 完成 SQLite 统一、本机全量验证与后续[Linux 平台批次](sqlite-linux-platform-validation-2026-10-31.md)：ARM64 原生 / AMD64 仿真双配置定向测试各 60 项通过，32 份 publish、120 个冷进程及八 final 镜像通过，HIGH / CRITICAL 零发现，临时资源和代理设置已清理恢复。本机全量口径见[原实施记录](sqlite-native-unification-2026-10-31.md)；其余原生平台、PG 与完整发行材料不自动放行，本批未发布。
- `2026-10-26` 完成 NoDrive 正式部分收缩、业务 / 租户 / OpenIddict / Hangfire provider 边界和 58 项新回归。Debug / Release 严格构建、PG17 全量各 4,135 项通过（102 项 Redis / Valkey 预期跳过）、PG16 专项各 43 项及四宿主产物检查通过；资源已清理。随后完成重点 notices / Cronos 正文分发、LGPL 狭义例外、Hangfire 源码补证与 Core 本机替换验证，见[日终回顾](day-end-doc-review-2026-10-26.md)。SQLite 原生依赖统一、完整来源 / 发行证据与 Valkey 迁移仍未关闭。本批未发布，采用结果见[实施记录](nodrive-adoption-2026-10-26.md)。
- `2026-10-21` 完成 PR #75 合并回灌、Mapperly 替代、NoDrive / Valkey 隔离验证，以及上传配额过期和 Int64 边界修复；[日终回顾](day-end-doc-review-2026-10-21.md)汇总全部提交、验证范围和后续事项。NoDrive / Valkey 未正式采用，本批未发布。
- `2026-10-20` 建立[第三方依赖许可治理专题](../features/dependency-license-governance.md)与[Rust 完整替代方案](../features/rust-image-processing-implementation-plan.md)；947 条依赖清单保留为迁移前快照。图片替代的交付、平台与成组验收已关闭，并于次日完成 PR 集成，见[实施记录](rust-image-replacement-2026-10-20.md)。

- `2026-10-15` 完成六个日志 L2 开发提交，覆盖自有 Auth 账户 / 会话、SelfLog、ASP.NET Core DataProtection、三宿主启动摘要、Common 程序集诊断及 API 启动业务 / Gateway 停止通知；[日终回顾](day-end-doc-review-2026-10-15.md)汇总全部批次、代码与文档对照、验证限制及后续事项。L2 尚未关闭，生产链路未切换。

- `2026-10-11` 完成 11 个日志 L2 开发提交，涵盖早期消费点结果构造、证书 / 传输、YARP、认证授权 / 路由、宿主健康服务及 OpenIddict Server / Core；公共策略净新增 522 个编码。[日终回顾](day-end-doc-review-2026-10-11.md)汇总全部批次、文档校准和验证限制，L2 尚未关闭，生产链路未切换。

- `2026-10-07` 完成日志 L2 的 7 个开发提交，覆盖表情管理、API 404 / 错误响应完成边界，以及 SignalR Core、外层 HTTP 和 Kestrel 协议 / HTTPS 来源。[日终回顾](day-end-doc-review-2026-10-07.md)汇总提交、文档校准、验证证据与未关闭边界；L2 尚未关闭，生产链路未切换。

- `2026-10-02` 完成日志 L2 十九批治理，覆盖通知入队 / 创建、实时推送与 Hub 自有日志，以及评论、内容提交、帖子、问答、投票、抽奖、标签 / 分类和表情回应消费边界。[日终回顾](day-end-doc-review-2026-10-02.md)汇总全部提交、证据与已知限制；SignalR 框架等剩余来源继续治理，生产链路未切换。

- `2026-09-28` 完成日志 L2 十五批生成端治理，覆盖账户 / 经验 / 奖励、商城、文件 / 附件、支付口令、公开内容与统计报表。完整提交清单、代码与文档对照及验证边界见[日终回顾](day-end-doc-review-2026-09-28.md)。保留业务规则与权威审计，生产链路未切换。

- `2026-09-23` 完成日志 L2 的 9 个开发 / 验证提交；代码与文档对照、各批次证据及未覆盖边界见[日终回顾](day-end-doc-review-2026-09-23.md)。SQL / seed / Outbox / Rust、Hangfire / 后台任务、奖励、服务内清理及币扣除 / 转账直接消费边界已推进；L2 尚未关闭，生产链路未切换。

- `2026-09-19` 完成部署反馈修复及统一日志 L1 / L2 入口子项；[日终回顾](day-end-doc-review-2026-09-19.md)汇总 7 个提交及文档校准。L2 仍未完成，`RadishLogging.Enabled` 默认 false。
- `2026-09-06` 完成[项目全面审阅](/records/project-review-2026-09-06)：确认类型检查入口漏检、Outbox 租约归属保护缺口、Web 构建预加载负担及 Flutter 通用 CI 覆盖缺口。条件性安全风险、维护热点与证据范围均已记录；本批仅整理文档，尚未修复代码或调整工程顺位。
- `2026-09-05` [P8-B2 macOS 本地运行验收](/records/f4-flutter-native-p8b2-macos-local-runtime-acceptance-closure-2026-09-05)关闭：standalone signed Debug 候选在本地 Gateway 完成 OIDC、Keychain / preferences、重启恢复、三档窗口、四主题和桌面输入验收；临时服务、注册与数据已精确清理。
- `2026-09-01` 项目所有者确认[当前无付费 Apple 会员或测试真机](/records/f4-flutter-native-p7d2-d3-external-prerequisite-deferral-2026-09-01)，P7-D2 / D3 暂缓；恢复需满足当时最新候选、会员 / Team / App ID、设备与分阶段授权条件。
