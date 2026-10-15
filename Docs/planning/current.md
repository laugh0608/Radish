# 当前进行中

> 本页维护即时状态、执行顺位与必要停止线。产品方向见[开发路线图](/development-plan)，批次事实见[记录索引](/records/)；旧入口流水已迁入[历史记录](/records/f4-planning-entry-history-2026-09-06)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`统一日志专题 L2：生成端治理`（项目所有者确认的维护线）
- **下一项工作**：确认 ASP.NET DataProtection 的精确来源、事件和验证方案，再独立推进 Auth 启动摘要与程序集裸输出；本批 SelfLog 已收口，后续实现另行确认。
- **状态复核日期**：`2026-10-15`；已完成表情管理、API 已处理 404、SignalR Core、外层 HTTP、Kestrel 协议 / HTTPS、证书监视 / 配置加载、早期消费点二次失败、服务端 transport / Http.Connections / 协议解析及 YARP / 出站 HTTP / 配置管理 / 条件性亲和与健康检查、认证／授权、路由、宿主健康服务、OpenIddict Server / Core、自有 Auth 登录／注册／会话与 SelfLog 日志治理，见[事件契约第 49–68 节](../features/unified-logging-contract.md)。L2 尚未关闭，未新增平台运行、镜像发布或部署验收。
- **源码候选版本**：`26.8.2`；尚未创建该候选的 test tag、GitHub Release、镜像或部署。
- **最近正式发布**：`v26.8.1-release`（2026-08-15，正式 tag 与五镜像已发布）。生产部署与长期运维由项目所有者独立负责，不作为当前开发顺位或功能验收前置。

## 已确认待实施的客户端方向

2026-10-12 已确认[客户端连接规划](../frontend/client-server-connection-plan.md)：同一个官方安装包运行时选择官方端点或用户自部署后端，无需重新编译。本轮仅更新产品与客户端文档；服务器选择、认证 / 缓存隔离及官方端点交付尚未验收，日志治理与后续 P8 顺位继续按本页推进。

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

- `2026-10-15` 完成[SelfLog 治理](../records/unified-logging-l2-selflog-2026-10-15.md)：两条链统一为独立、限频的安全 stderr 自诊断，覆盖会话配置读取、logger 构造及释放边界；原文文件停止新增，历史文件保留。DataProtection、Auth 启动日志与其他裸输出未实施。

- `2026-10-15` 完成[自有 Auth 日志治理](../records/unified-logging-l2-auth-account-idle-session-2026-10-15.md)：账户与会话共 24 处旧调用点收敛为 10 个事件及 2 个未知摘要，覆盖安全输出、结果准备顺序和四模式内存回归；解析 catch 仅有生成方法证据，未执行真实登录或生产切换。

- `2026-10-11` 完成 11 个日志 L2 开发提交，涵盖早期消费点结果构造、证书 / 传输、YARP、认证授权 / 路由、宿主健康服务及 OpenIddict Server / Core；公共策略净新增 522 个编码。[日终回顾](../records/day-end-doc-review-2026-10-11.md)汇总全部批次、文档校准和验证限制，L2 尚未关闭，生产链路未切换。

- `2026-10-07` 完成日志 L2 的 7 个开发提交，覆盖表情管理、API 404 / 错误响应完成边界，以及 SignalR Core、外层 HTTP 和 Kestrel 协议 / HTTPS 来源。[日终回顾](../records/day-end-doc-review-2026-10-07.md)汇总提交、文档校准、验证证据与未关闭边界；L2 尚未关闭，生产链路未切换。

- `2026-10-02` 完成日志 L2 十九批治理，覆盖通知入队 / 创建、实时推送与 Hub 自有日志，以及评论、内容提交、帖子、问答、投票、抽奖、标签 / 分类和表情回应消费边界。[日终回顾](../records/day-end-doc-review-2026-10-02.md)汇总全部提交、证据与已知限制；SignalR 框架等剩余来源继续治理，生产链路未切换。

- `2026-09-28` 完成日志 L2 十五批生成端治理，覆盖账户 / 经验 / 奖励、商城、文件 / 附件、支付口令、公开内容与统计报表。完整提交清单、代码与文档对照及验证边界见[日终回顾](../records/day-end-doc-review-2026-09-28.md)。保留业务规则与权威审计，生产链路未切换。

- `2026-09-23` 完成日志 L2 的 9 个开发 / 验证提交；代码与文档对照、各批次证据及未覆盖边界见[日终回顾](../records/day-end-doc-review-2026-09-23.md)。SQL / seed / Outbox / Rust、Hangfire / 后台任务、奖励、服务内清理及币扣除 / 转账直接消费边界已推进；L2 尚未关闭，生产链路未切换。

- `2026-09-19` 完成部署反馈修复及统一日志 L1 / L2 入口子项；[日终回顾](../records/day-end-doc-review-2026-09-19.md)汇总 7 个提交及文档校准。L2 仍未完成，`RadishLogging.Enabled` 默认 false。
- `2026-09-06` 完成[项目全面审阅](/records/project-review-2026-09-06)：确认类型检查入口漏检、Outbox 租约归属保护缺口、Web 构建预加载负担及 Flutter 通用 CI 覆盖缺口。条件性安全风险、维护热点与证据范围均已记录；本批仅整理文档，尚未修复代码或调整工程顺位。
- `2026-09-05` [P8-B2 macOS 本地运行验收](/records/f4-flutter-native-p8b2-macos-local-runtime-acceptance-closure-2026-09-05)关闭：standalone signed Debug 候选在本地 Gateway 完成 OIDC、Keychain / preferences、重启恢复、三档窗口、四主题和桌面输入验收；临时服务、注册与数据已精确清理。
- `2026-09-01` 项目所有者确认[当前无付费 Apple 会员或测试真机](/records/f4-flutter-native-p7d2-d3-external-prerequisite-deferral-2026-09-01)，P7-D2 / D3 暂缓；恢复需满足当时最新候选、会员 / Team / App ID、设备与分阶段授权条件。

## 当前平台边界

- macOS 已使用真实安全存储与 native auth owner。无付费会员的 ad-hoc 候选使用加密本机登录 Keychain，固定 `first_unlock_this_device`、`synchronizable: false` 与 `usesDataProtectionKeychain: false`；未来正式签名时重新评估。
- macOS 本地运行证据来自完成深度签名校验的 standalone Debug app。会改写 framework 并影响嵌套签名的 `flutter run / attach` 不作为候选证据。
- iOS 近期 Internal TestFlight 临时复用生产 Gateway 的裁决继续有效，但 D2 / D3 尚未执行；该裁决不授权访问生产数据或执行 Apple 外部操作。
- 以上具体契约以[Flutter Native 专题](/features/flutter-native-product-ui-design)和对应记录为准。

## 下一步事项（2026-10-15 更新）

继续[统一日志专题](../features/unified-logging-governance-design.md)。已完成范围见[事件契约第 7–68 节](../features/unified-logging-contract.md)，最新批次与证据限制见[SelfLog 记录](../records/unified-logging-l2-selflog-2026-10-15.md)，不再逐批列作待办。

1. **先形成 ASP.NET DataProtection 治理方案**：只读核对已区分“OpenIddict Client / Validation / DataProtection 依赖存在但未注册”和“ASP.NET DataProtection 实际使用”；后者的 XML / 路径 / 异常日志仍未治理。IdentityModel 默认空 logger、仓库无诊断订阅器不等于外部 EventPipe / DiagnosticListener / Activity / metrics 已受控；详细边界见最新记录。Socket `.Client` 与 HttpSys delegation 继续后置。
2. **登记自有 Auth 启动日志**：`Program.cs` 的 ApplicationStarted 回调仍有 16 处 banner / 配置摘要日志，后续按来源与配置读取风险确认，不从此前 24 处账户／会话调用点治理推定整个 Auth 已完成。既有 `auth.seed.completed` 不重复治理；`RuntimeExtension` 程序集加载失败的裸输出单独列项。
3. **形成 L2 剩余覆盖清单**：对照生产接线与事件契约，区分已治理精确来源、待治理自有业务日志、相邻透传来源、桥接前读取风险及非日志输出，确定下一批完整范围；现有生成器 / 内存证据不替代真实登录、网络探测、存储或部署验收。
4. **独立问题保留**：Reaction 已删除行恢复返回 409、自动抽奖 SQLite → Outbox 时间偏移、关注入队与关系提交不同事务、评论高亮部分写入、轻回应回滚后的缓存保留，以及标签 / 分类非事务写入与 0 行成功语义，统一见[日终回顾的未关闭边界](../records/day-end-doc-review-2026-10-02.md#独立问题与未关闭边界)。表情管理的既有错误映射、文件副作用和存储 false / null 故障折叠见[本批记录](../records/unified-logging-l2-sticker-controller-2026-10-07.md)；可靠性与响应契约改造均需独立确认，Rust `.tmp` 水印回退仍后置。

以上为后续恢复工作的顺位；本批不授权安装依赖、启动项目服务 / 容器、访问生产或系统证书库。真实运行与后续实现仍按具体范围确认。

生产候选开关继续关闭；L1 传输上界 / 磁盘故障、L2 整体治理、L3 入库、L4 查询、L5 Console 告警及 L6 切换尚未完成。Native P8-C readiness 保留后续顺位。

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
