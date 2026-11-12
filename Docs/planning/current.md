# 当前进行中

> 本页维护即时状态、执行顺位与必要停止线。产品方向见[开发路线图](/development-plan)，批次事实见[记录索引](/records/)；旧入口流水已迁入[历史记录](/records/f4-planning-entry-history-2026-09-06)。

## 规划阅读链

[整体路线图](../development-plan.md) → [能力专题地图](topics.md) → [统一日志](../features/unified-logging-governance-design.md)、[Flutter Native](../features/flutter-native-product-ui-design.md)与[客户端连接](../frontend/client-server-connection-plan.md)。依赖来源与发行材料由[原专题切片](../features/dependency-license-governance.md#delivery-slices)保留，按维护 / 发行节点复核。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`已规划功能完成主线`；2026-11-04 项目所有者明确要求优先补齐功能缺口，再转入以长期运营 / 维护为主的节奏，依赖来源补证等后置。
- **下一完整交付**：在指定隔离部署环境完成统一日志查询、管道故障识别、站内告警和升级 / 恢复 / 回滚；先固定剩余 Service / Job / Hub 最终出口及诊断必需字段清单，再按下述四批完成。L3-A、浏览器、七个自有来源与 14 个 Controller 的本地成果不重复实施。
- **规划复核日期**：`2026-11-10`；所有者确认收口顺序，本轮只改文档，无新增产品验收。已有成果已由 `8d6cc63a` 集成，集成不关闭日志 L1–L6、Native 或连接能力的剩余门禁。[本次记录](../records/planning-closeout-2026-11-10.md)区分方案、事实和验证边界；11 月 4–5 日运行与实现证据仍沿原记录。
- **日志专题复核日期**：`2026-11-11`；共享后台 Job、抽奖 Job、文件和记录清理、上传会话及文件访问令牌清理以及 Outbox 的最终输出已完成本地修复与回归。[Outbox 本批记录](../records/unified-logging-l2-outbox-output-2026-11-11.md)保留真实链路边界；奖励、币与经验六个来源已按[第 10.8 节](../features/unified-logging-l3-ingest-plan.md#l2-reward-output)完成本地最终输出修复与回归，见[实施记录](../records/unified-logging-l2-reward-output-audit-2026-11-11.md#implementation)。商城、库存与支付口令四个来源已按[第 10.9 节](../features/unified-logging-l3-ingest-plan.md#l2-shop-output)完成本地修复与回归，见[实施记录](../records/unified-logging-l2-shop-output-audit-2026-11-11.md#implementation)。已实现范围见[事件契约第 7–84 节](../features/unified-logging-contract.md)。L2 整体和真实链路仍按各自退出条件推进。
- **当前日志切片**：2026-11-12 已完成公开查询与报表、通知与实时推送、Hub 自有断开事件、内容及表情的本地最终输出修复。[第 10.14 节](../features/unified-logging-l3-ingest-plan.md#l2-diagnostic-and-output-boundaries)已完成 API 5xx 反馈 ID、关联字段、Flutter 应用本地报告及 CLI / 应急边界的本地回归。真实采集与介质走 L1 / L3，反馈 ID 的索引与受权查询走 L3 / L4，原生输出走目标设备验收。见[本批实施记录](../records/unified-logging-l2-diagnostic-output-boundary-audit-2026-11-12.md#implementation)。
- **源码候选版本**：`26.8.2`；尚未创建该候选的 test tag、GitHub Release、发布镜像或部署。
- **最近正式发布**：`v26.8.1-release`（2026-08-15，正式 tag 与五镜像已发布）。生产部署与长期运维由项目所有者独立负责，不作为当前开发顺位或功能验收前置。

## 已确认待实施的客户端方向

2026-10-12 已确认[客户端连接规划](../frontend/client-server-connection-plan.md)：同一个官方安装包运行时选择官方端点或用户自部署后端，无需重新编译。服务器选择、认证 / 缓存隔离及官方端点交付尚未验收；该能力明确保留在功能完成队列，不以已有单实例平台验收代替。

## 功能优先与依赖维护边界

2026-11-04 项目所有者调整此前依赖专题优先的顺位：先完成已经确认的产品 / 平台功能，再以长期运营和维护为主逐项完善来源、构建复现及发行材料。当前仍属于功能完成阶段，不提前宣称进入稳定运营，也不因该调整重启主动生产使用数据采集。

依赖治理已完成的图片 / 映射替代、NoDrive 采用、重点 notices、SQLite 统一及本次平台验收保持原结论，详见[依赖切片](../features/dependency-license-governance.md#delivery-slices)。Hangfire 原包 PDB / 来源补证、Windows Razor 重现、源码材料生成及正式入口、NoDrive / SystemCommon 询证、SNI 发行核对和 Valkey 是否迁移，均保留到所属专题的后续维护 / 发行队列；不继续主动询证、轮询上游或推进专用验证，不自动恢复。

实际可达的安全问题、运行故障、数据一致性问题及 `P0/P1` 仍按风险及时处理。正式发行前核对与该产物实际分发相关的条件；后置补证不构成发行放行，也不降低现有运行、测试和安全门禁。

## 接下来的事项

1. **按四批完成日志交付**：最终出口与诊断范围 → 真实采集 / 持久化 → Console 查询 / 站内告警 → 迁移 / 候选验收。四批沿原 L1–L6，不新建另一套阶段；任务与结束条件见[日志收口范围](../features/unified-logging-governance-design.md#closeout-batches)。第一批只读列明精确来源、共享 helper、消费链、旧 / 候选出口和已有证据，按缺口成组实施；诊断字段先区分首版必需和后续增强，不默许全部删除或无限补齐。
2. **日志退出后接续 Native**：按 P8-C Windows、P8-D Linux、P8-E 成组验收完成已确认平台范围。必要阻塞为工具链、真实认证 / 安全存储、窗口 / 输入和持续状态的目标平台证据；签名、真机与分发继续按平台单列。平台资源不可达时形成具名等待项，再评审是否在已有可用平台提前推进连接能力，不自动扩建环境或取消平台目标。
3. **完成客户端连接能力**：先冻结实例身份、兼容发现、地址 / TLS、OIDC 和旧单实例状态迁移，再实现同包选择、会话 / 缓存 / 实时连接隔离与切换恢复。两个测试实例只证明机制，官方与自部署的正式交付各需对应证据，见[连接规划](../frontend/client-server-connection-plan.md)。
4. **按范围验收后转入维护为主**：日志完整候选、已确认 Native 平台范围和客户端连接分别验收后，再作阶段评审；不等待全部 backlog、商店分发或所有外部设备条件清零。批次集成与正式发行 / 生产部署分开，后置来源材料不等于发行放行。

必要阻塞与停止扩张：日志的来源 / 诊断范围、目标部署架构、真实 L1/L3 故障证据和查询 / 告警 / 迁移验收尚未关闭。新增任务必须直接影响上述交付的正确性、安全、数据保全或可执行性；已完成切片不因统一规划重新进入。业务事务 / 响应 / 缓存缺陷仅在直接阻断本次验收或属 P0/P1 时进入当前批次，其余回到原专题。指定环境不可用时报告缺口，不用更多离线来源扫描代替真实链路验收。

个人开发继续只保持一个主要功能专题；上述是已有缺口与推进队列，不授权同时实施多个专题。依赖安装、服务 / 容器启动、外部通信、平台操作和发布部署仍按当前任务具体范围确认。

## 产品线成熟度

下表区分已发布产品的维护状态、原生平台建设与分发状态；一个平台的 `Go` 不替代其他平台或分发门禁。

| 产品 / 平台 | 已成立的结论 | 仍未关闭的边界 |
| --- | --- | --- |
| Web（PC / mobile） | 正式 Web 已发布；F4-B 至 F4-Q、F4-S 与 F4-R Web 页面族 / 主题门禁已关闭 | 后续维护、每批候选回归和独立发布 / 部署复核 |
| Flutter 共享层 | P1–P5 首轮主题、页面族与成组静态门禁已关闭 | 新改动仍按影响面复核；共享测试不替代平台验收 |
| Android | P6 双 AVD 验收，`Android UI AVD RC Go` | P6-C 真机、正式签名与外部分发 |
| iOS | P7-C `Simulator Go`、P7-D1 minimal readiness 已关闭 | 无付费会员与测试真机，P7-D2 / D3 暂缓；External TestFlight / App Store 后置 |
| macOS | P8-B1 工程与 P8-B2 本地 development 运行态 `Go` | 正式签名、公证、安装包与分发 |
| Windows / Linux | 共享 expanded UI 可继承；runtime 仍为显式 `unsupported` 内存 shell | Windows P8-C、Linux P8-D 平台建设及 P8-E 成组门禁 |

正式产品线只保留 Web 与 Flutter Native；Web 优先，Flutter mobile-first、desktop stage-gated。WebOS `/desktop` 仅历史兼容，Tauri 正式弃用。

## 最近结论

最新日终回顾见[2026-11-05 日终记录](../records/day-end-doc-review-2026-11-05.md)，汇总六个开发 / 方案提交、代码与文档对照、验证边界及下次恢复建议。此前平台与依赖结果见[11 月 4 日回顾](../records/day-end-doc-review-2026-11-04.md)；历史事实不作为当前待办。

## 当前平台边界

- macOS 已使用真实安全存储与 native auth owner。无付费会员的 ad-hoc 候选使用加密本机登录 Keychain，固定 `first_unlock_this_device`、`synchronizable: false` 与 `usesDataProtectionKeychain: false`；未来正式签名时重新评估。
- macOS 本地运行证据来自完成深度签名校验的 standalone Debug app。会改写 framework 并影响嵌套签名的 `flutter run / attach` 不作为候选证据。
- iOS 近期 Internal TestFlight 临时复用生产 Gateway 的裁决继续有效，但 D2 / D3 尚未执行；该裁决不授权访问生产数据或执行 Apple 外部操作。
- 以上具体契约以[Flutter Native 专题](/features/flutter-native-product-ui-design)和对应记录为准。

## 当前主线：日志专题恢复顺位

2026-11-04 恢复[统一日志专题](../features/unified-logging-governance-design.md)，保留 2026-10-15 已完成结论。当前范围见[事件契约第 7–88 节](../features/unified-logging-contract.md)，此前批次与证据限制见[11 月 5 日日终回顾](../records/day-end-doc-review-2026-11-05.md)。下面按已有生产接线和明确缺口收口，不向未使用框架、外部任意订阅器或全进程证明扩张。

1. **浏览器输出本批已完成**：共享 HTTP 五处错误出口和两套应用 logger 已接入公共实现，固定文本 / 字段、三级 / diagnostic、兼容调用与最终 console 有成组自动化回归，见[本批记录](../records/unified-logging-l2-browser-output-2026-11-05.md)。不自动增加遥测回传，也不将模块替身证据视为真实浏览器 Smoke；后续不重复该切片，按“接下来的事项”推进剩余来源。
2. **保留自有来源的准确边界**：Common 两处 catch、API 周期注册 / 固定文档同步、Gateway 停止及两个 Hangfire 来源保持已完成结论。User / Client / Bootstrap / 通知去重 / 搜索 / 限流 / 经验七个来源与直接异常消费者、14 个 Controller、共享后台 Job、抽奖 Job、文件和记录清理、服务内清理、Outbox、奖励 / 币 / 经验、商城 / 库存 / 支付口令、公开查询与报表、通知与实时推送、两条 Hub 自有断开事件及内容与表情已完成对应的本地输出切片。见[自有来源记录](../records/unified-logging-l2-owned-operations-2026-11-05.md)、[Controller 记录](../records/unified-logging-l2-controller-output-2026-11-05.md)及[调用链清单](../features/unified-logging-l3-ingest-plan.md#l2-final-output-first-slice)。
3. **保留未使用 / 外部通道边界**：DataProtection 列明来源已治理，OpenIddict Client / Validation / DataProtection 集成未注册；外部 EventPipe / DiagnosticListener / Activity / metrics 不自动受控，详见 [DataProtection 记录](../records/unified-logging-l2-dataprotection-2026-10-15.md)。Socket `.Client` 与 HttpSys delegation 后置，不将这些已知限制重新放到功能收口之前进行无限调查。
4. **独立问题保留**：Reaction 已删除行恢复返回 409、自动抽奖 SQLite → Outbox 时间偏移、关注入队与关系提交不同事务、评论高亮部分写入、轻回应回滚后的缓存保留，以及标签 / 分类非事务写入与 0 行成功语义，统一见[日终回顾的未关闭边界](../records/day-end-doc-review-2026-10-02.md#独立问题与未关闭边界)。表情管理的既有错误映射、文件副作用和存储 false / null 故障折叠见[本批记录](../records/unified-logging-l2-sticker-controller-2026-10-07.md)；可靠性与响应契约改造均需独立确认。Rust `.tmp` 水印回退已由图片替代移除，改用内容识别与显式输出格式，交付验收及 PR 集成已完成。

后续新实现范围仍先确认方案。依赖安装、项目服务 / 容器启动、访问生产和系统证书库不因本批完成而取得授权。

生产候选开关继续关闭；L3-A 仅完成本地接收 / 入库切片，L1 传输上界 / 磁盘故障、L2 整体治理、L3 成组持久化验收、L4 查询、L5 Console 告警及 L6 切换尚未完成。Native P8-C readiness 保留后续顺位。

## 后续平台事项：P8-C Windows readiness

1. 只读确认日常开发 VM 与 CleanBase VM 的系统版本、架构、磁盘 / 快照状态与角色，不修改干净基线。
2. 审计 Flutter `3.44.x`、Visual Studio C++ Desktop workload / ATL、CMake / Ninja、Git 与 Windows SDK 的现状和缺口。
3. 核对 Windows runner 输入、产品身份、OIDC callback、Credential Manager / preferences、窗口与退出边界。
4. 冻结日常 VM 实施、CleanBase 候选验收、服务连接、数据影响、证据与精确清理方案。
5. readiness 不提前安装工具或生成平台工程；后续安装、依赖变更、服务启动与 VM 基线修改须按明确范围单独授权。

## 审阅改进候选

- 候选排序、影响面与完成标准统一维护在[工程改进候选清单](/planning/engineering-improvement-candidates)。
- 上述工程改进候选仍仅完成审阅；类型检查、Outbox、管理端安全边界、Web 加载、Flutter CI 与结构治理均未因本次文档更新取得实施授权。
- 候选不自动替代当前日志专题或后续 P8-C。选定批次后再确认方案与顺位；不以本次审阅启动全仓重构或主动生产数据采集。
- 类型检查的已知覆盖限制和补充命令见[验证基线说明](/guide/validation-baseline)。

## 并行维护线

- `2026-09-19` 根据项目所有者反馈整理[统一日志与跨容器汇聚专题方案](/features/unified-logging-governance-design)：已确认设计并开始 L1，统一生成契约与隔离采集首轮实验已落地；[实测](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)发现 HTTP 413 丢弃、文件按 chunk 轮转和 Docker 长行分片。[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)已通过本机验证；L2 生成入口与已完成子项统一见[事件契约](../features/unified-logging-contract.md)，继续剩余业务调用链与框架来源治理。正式传输上界及发布平台门禁未关闭，生产默认链路未切换。
- 接收明确的 `P0/P1` 生产故障、用户反馈、安全、依赖、迁移和部署问题；P2/P3 按同类问题成组处理。
- 公开 head、动态 sitemap、生产域名、镜像漏洞门禁和多实例附件基础设施按真实触达范围维护。
- WebOS 只处理阻断级兼容；Flutter 承接高价值原生路径，不机械追平 Web。
- 时间语义继续由 baseline 约束；`SystemConfigStorageCoordinator.cs` 的既有 `DateTime.Now` 已清零，不再列为待修复项。
- 主动生产使用数据采集继续冻结到计划内功能完成、没有明确维护任务且项目所有者确认的最终收尾阶段。

## 当前不做

- 不继续把依赖原包 PDB、上游构建完全复现、公开询证或源码材料生成列作功能开发前置；不自动复核或重试后置的 Hangfire 专项。来源 / 发行未关闭项仍保留，正式发行按实际范围单独核对。
- 不因 F4-N 关闭而扩入 `PostAnswer`、自定义理由、自定义金额、重复赞赏或独立赞赏中心。
- 不把 F4-O 扩成回答投票、复杂排序、悬赏、萝卜币、独立问答 App 或全量 PublicId 迁移。
- F4-Q 已关闭，不回拉标签关注、个性化推荐、标签首页、SSR / SSG 或公开个人页 sitemap；商品评价不扩入媒体、回复、评价有用或独立评价中心，公开等级不扩为公开经验详情。
- R3-F02 已关闭，不借主题收口新增认证、自服务或权限能力。
- 不恢复 Tauri、不扩展 WebOS、不引入 Flutter Web，不重启主动生产证据采集。
- 不继续修改历史 `.pen` 留档，不为路由、主题、文案或等价状态复制完整画板；后续 `.pen` 修改仍需当前任务明确授权。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后统一集成。

## 验证与执行入口

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`；合并前执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 Smoke 只在专题或成组功能验收时执行；项目启动需当前任务授权。历史测试数量不替代当前候选验证。
- [Flutter Native 产品化与 UI 重构](/features/flutter-native-product-ui-design)
- [P8-A desktop readiness](/records/f4-flutter-native-p8a-desktop-platform-readiness-2026-09-05) · [P8-B readiness](/records/f4-flutter-native-p8b-macos-platform-foundation-readiness-2026-09-05) · [P8-B1 实施](/records/f4-flutter-native-p8b1-macos-platform-foundation-implementation-2026-09-05) · [P8-B2 运行验收](/records/f4-flutter-native-p8b2-macos-local-runtime-acceptance-closure-2026-09-05)
- [工程改进候选清单](/planning/engineering-improvement-candidates) · [验证基线说明](/guide/validation-baseline)
- [开发路线图](/development-plan) · [记录索引](/records/) · [旧入口历史流水](/records/f4-planning-entry-history-2026-09-06)
