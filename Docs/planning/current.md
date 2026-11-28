# 当前进行中

> 本页维护即时状态、执行顺位与必要停止线。产品方向见[开发路线图](/development-plan)，批次事实见[记录索引](/records/)；旧入口流水已迁入[历史记录](/records/f4-planning-entry-history-2026-09-06)。

## 规划阅读链

[整体路线图](../development-plan.md) → [能力专题地图](topics.md) → [统一日志](../features/unified-logging-governance-design.md)、[Flutter Native](../features/flutter-native-product-ui-design.md)与[客户端连接](../frontend/client-server-connection-plan.md)。依赖来源与发行材料由[原专题切片](../features/dependency-license-governance.md#delivery-slices)保留，按维护 / 发行节点复核。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`已规划功能完成主线`；2026-11-04 项目所有者明确要求优先补齐功能缺口，再转入以长期运营 / 维护为主的节奏，依赖来源补证等后置。
- **客户端开发暂停（2026-11-21）**。Flutter Native 平台建设与客户端连接保留原方案和顺位。非客户端六项提案中已选定第 2 项。[社区功能与平台治理六项提案](./community-platform-next-proposals.md)
- **规划复核日期**为 2026-11-10，所有者确认日志、Native、客户端连接的收口顺序。该次规划以 `8d6cc63a` 集成的成果为基线。[规划复核记录](../records/planning-closeout-2026-11-10.md)
- **日志出口评审（2026-11-21）**。OrbStack 隔离候选通过登记来源、诊断及数据保全评审。继续接受普通运行日志首次同步前的崩溃窗口，满卷未观测尾段数量保持未知。实际部署预检与慢 SQL 安全指纹单列。[出口评审](../records/unified-logging-exit-review-2026-11-21.md) · [最终联验](../records/unified-logging-exit-console-2026-11-21.md)
- **源码候选版本**：`26.8.2`；尚未创建该候选的 test tag、GitHub Release、发布镜像或部署。
- **最近正式发布**：`v26.8.1-release`（2026-08-15，正式 tag 与五镜像已发布）。生产部署与长期运维由项目所有者独立负责，不作为当前开发顺位或功能验收前置。

## 已确认且暂停的客户端方向

2026-10-12 已确认[客户端连接规划](../frontend/client-server-connection-plan.md)：同一个官方安装包运行时选择官方端点或用户自部署后端，无需重新编译。服务器选择、认证 / 缓存隔离及官方端点交付尚未验收；该能力明确保留在功能完成队列，不以已有单实例平台验收代替。

## 功能优先与依赖维护边界

2026-11-04 项目所有者调整此前依赖专题优先的顺位：先完成已经确认的产品 / 平台功能，再以长期运营和维护为主逐项完善来源、构建复现及发行材料。当前仍属于功能完成阶段，不提前宣称进入稳定运营，也不因该调整重启主动生产使用数据采集。

依赖治理已完成的图片 / 映射替代、NoDrive 采用、重点 notices、SQLite 统一及本次平台验收保持原结论，详见[依赖切片](../features/dependency-license-governance.md#delivery-slices)。Hangfire 原包 PDB / 来源补证、Windows Razor 重现、源码材料生成及正式入口、NoDrive / SystemCommon 询证、SNI 发行核对和 Valkey 是否迁移，均保留到所属专题的后续维护 / 发行队列；不继续主动询证、轮询上游或推进专用验证，不自动恢复。

实际可达的安全问题、运行故障、数据一致性问题及 `P0/P1` 仍按风险及时处理。正式发行前核对与该产物实际分发相关的条件；后置补证不构成发行放行，也不降低现有运行、测试和安全门禁。

## 接下来的事项

1. **验收普通 Web 会话**。隔离升级与回退、旧页面回流、隔夜恢复、两层期限与三个 Hub 断开、双 Auth 接管、多标签回流、频道消息重试、专用密钥轮换及认证库备份恢复均已实测。Console 专用停用命令已接入主库和认证库，先记录账号状态及撤销任务，再主动撤销该账号的普通 Web 会话与授权。Auth 登录重定向日志已改为安全摘要，Scalar 已使用 Auth 登记的公开 Gateway 基址。下一步在同一非默认地址候选核对 Auth 两个日志出口，并从 Scalar 页面授权至受保护读取。正式开关保持关闭，实际掉线实例与入口仍待核实。[凭据隔离验收](../records/auth-session-client-isolation-2026-11-28.md) · [停用入口与撤销契约](../features/auth-session-overnight-recovery-plan.md#web-account-disable-proposal) · [账号停用验收](../records/auth-session-account-disabled-2026-11-28.md#console-停用入口与主动撤销) · [验收矩阵](../features/auth-session-overnight-recovery-plan.md#修复验收)
2. **客户端恢复后按原顺位推进**。P8-C Windows、P8-D Linux、P8-E 成组验收完成后接续客户端连接。各平台须取得目标工具链、认证 / 安全存储、窗口 / 输入与重启证据，签名、真机与分发按平台单列。客户端连接按[已确认规划](../frontend/client-server-connection-plan.md)完成同包实例选择、隔离与恢复。
3. **按范围验收后转入维护为主**。日志完整候选、已确认 Native 平台范围和客户端连接分别验收后，再作阶段评审。批次集成与正式发行 / 生产部署分开，商店分发和外部设备条件继续按各平台门禁。

日志生产候选开关维持关闭。实际部署另核目标架构、七类来源、镜像、daemon 路径、容量及停写备份顺序。慢 SQL 同语句聚合等待安全指纹。[部署预检](../features/unified-logging-l3-ingest-plan.md#l1-l3-compose-preflight) · [告警契约](../features/unified-logging-governance-design.md#首轮查询和告警入口)

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

最新日终回顾覆盖 11 月 21 日的日志出口、Windows 只读盘点与暂停、默认令牌期限复现及普通 Web 会话三段实现。[2026-11-21 日终记录](../records/day-end-doc-review-2026-11-21.md)

## 当前平台边界

- macOS 已使用真实安全存储与 native auth owner。无付费会员的 ad-hoc 候选使用加密本机登录 Keychain，固定 `first_unlock_this_device`、`synchronizable: false` 与 `usesDataProtectionKeychain: false`；未来正式签名时重新评估。
- macOS 本地运行证据来自完成深度签名校验的 standalone Debug app。会改写 framework 并影响嵌套签名的 `flutter run / attach` 不作为候选证据。
- iOS 近期 Internal TestFlight 临时复用生产 Gateway 的裁决继续有效，但 D2 / D3 尚未执行；该裁决不授权访问生产数据或执行 Apple 外部操作。
- 以上具体契约以[Flutter Native 专题](/features/flutter-native-product-ui-design)和对应记录为准。

## 客户端暂停点：P8-C Windows readiness

日常 VM 实测 Windows 11 25H2 ARM64 build `26200.9711`，C: 可用 13.29 GiB。现有 Git、ARM64 MSVC 与 Windows SDK 可复用，实际缺项为 Flutter / Dart、VS 的 x86/x64 C++、ARM64 ATL、CMake / Ninja。先核算安装空间，并从目标用户会话确认 Flutter 引导使用原生 ARM64 PowerShell。Windows CleanBase 的来宾核对安排在获准的候选副本。[只读盘点](../records/f4-flutter-native-p8c-windows-readiness-2026-11-21.md)

候选按原生 ARM64 Flutter、Win32 runner、系统浏览器 OIDC、用户级 DPAPI、非敏感 preferences、窗口输入与关闭重启验收。工具安装、runner 生成、隔离服务启动和候选验收按说明范围分别确认。[命令、影响、证据与清理](../features/flutter-native-p8c-windows-plan.md)

## 审阅改进候选

- 候选排序、影响面与完成标准统一维护在[工程改进候选清单](/planning/engineering-improvement-candidates)。
- 上述工程改进候选仍仅完成审阅；类型检查、Outbox、管理端安全边界、Web 加载、Flutter CI 与结构治理均未因本次文档更新取得实施授权。
- 候选仍列于工程改进清单。选定批次后再确认方案与顺位。
- 类型检查的已知覆盖限制和补充命令见[验证基线说明](/guide/validation-baseline)。

## 并行维护线

- 日志已登记来源见[事件契约](../features/unified-logging-contract.md)，诊断和后续项见[出口评审](../records/unified-logging-exit-review-2026-11-21.md)。既有事务、响应、缓存与表情存储问题按各自专题处理。[未关闭边界](../records/day-end-doc-review-2026-10-02.md#独立问题与未关闭边界) · [表情管理记录](../records/unified-logging-l2-sticker-controller-2026-10-07.md)
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
- 停用 Pencil，既有 `.pen`、预览和评审记录归档。后续按[代表页设计流程](/frontend/pencil-representative-page-workflow)更新受影响的结构和状态。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后统一集成。

## 验证与执行入口

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`；合并前执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 Smoke 只在专题或成组功能验收时执行；项目启动需当前任务授权。历史测试数量不替代当前候选验证。
- [Flutter Native 产品化与 UI 重构](/features/flutter-native-product-ui-design)
- [P8-A desktop readiness](/records/f4-flutter-native-p8a-desktop-platform-readiness-2026-09-05) · [P8-B readiness](/records/f4-flutter-native-p8b-macos-platform-foundation-readiness-2026-09-05) · [P8-B1 实施](/records/f4-flutter-native-p8b1-macos-platform-foundation-implementation-2026-09-05) · [P8-B2 运行验收](/records/f4-flutter-native-p8b2-macos-local-runtime-acceptance-closure-2026-09-05)
- [工程改进候选清单](/planning/engineering-improvement-candidates) · [验证基线说明](/guide/validation-baseline)
- [开发路线图](/development-plan) · [记录索引](/records/) · [旧入口历史流水](/records/f4-planning-entry-history-2026-09-06)
