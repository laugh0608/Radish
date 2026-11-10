# 能力专题地图

本页是“整体规划 → 大专题 → 子专题”的唯一归属入口。[开发路线图](../development-plan.md)维护产品目标和阶段出口，[当前进行中](current.md)维护即时顺位。Phase、F、P、L 和既有任务编号继续使用；阶段、平台与能力分别管理。本次覆盖现行能力入口、当前依赖治理及后续日志 / Native 工作链，历史细案沿原目录查阅，不因登记而恢复实施。

每个子专题有一个主要归属；跨主线关系列为协同。子专题中的实施方案、执行批次和平台矩阵是交付切片，不另造一层平行任务库。下表列的是设计、任务与验收的正式来源；实际结果沿各文引用的 records 查证，本页不维护状态副本。

<a id="content"></a>

## 社区内容与互动

让用户发现、创作、讨论和整理社区内容。拥有内容生命周期与互动规则；身份 / 处置依赖治理主线，公开页面与 SEO 协同多端体验，不复制业务状态。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [论坛功能](../features/forum-features.md)、[公开论坛](../features/forum-public-app.md)、[Discover](../features/discover-public-app.md) | 既有社区及 Web 完成批次，细项按所属文档 | 多端体验、关系与消息 |
| [内容版本恢复](../features/forum-content-version-recovery-design.md)、[回答生命周期](../features/forum-answer-lifecycle-governance-design.md) | F4-M / O，任务及验收留在原专题 | 身份与治理 |
| [私有收藏](../features/forum-post-bookmark-personal-library-design.md)、[标签发现与 SEO](../features/forum-tag-public-discovery-seo-design.md) | F4-P / Q，既有范围与记录 | 多端体验、知识与创作 |
| [投票](../features/forum-poll-mvp.md)、[抽奖](../features/forum-lottery-mvp.md)、[表情回应](../features/emoji-sticker-reaction-phase2.md) | 原功能批次，沿用原合同与验收 | 激励体系、身份与治理 |

整体完成以指定版本的发布、编辑、互动及失败恢复用户路径成立为准；历史关闭不免除新候选的受影响回归。

<a id="relationships"></a>

## 关系与消息

让关注、圈子、私聊和通知形成复访。拥有关系、消息与通知生命周期；鉴权 / 屏蔽依赖治理，实时传输依赖平台，不把多个 UI 壳层建成独立业务规划。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [聊天总入口](../features/chat-app-index.md)、[聊天路线](../features/chat-app-roadmap.md) | F4 私聊及 B–F 的设计、任务和验收沿聊天细案维护 | 平台与交付、多端体验 |
| [通知中心](../features/notification-center-deepening.md) | F4-B，所属任务及证据 | 身份与治理、内容互动 |
| [个人圈子](../features/circle.md) | 原范围和验收 | 身份与治理 |

指定批次应覆盖发送 / 接收、已读 / 搜索、重连及权限失败；通知或实时链局部通过不能代替整段复访体验。

<a id="identity-governance"></a>

## 身份与治理

维护身份语义、授权、证据及处置一致性。Console 是治理消费端，不是第二份业务权威；公开身份与内部标识按现有契约隔离。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [身份语义](../architecture/user-identity-semantics.md)、[认证](../guide/authentication.md)、[会话过期](../guide/auth-idle-session.md) | 既有身份治理切片，协议与测试入口沿原文 | 平台、多端 |
| [Console 权限](../guide/console-permission-governance.md) | 原权限专题与覆盖矩阵；不新增部署架构 | 多端体验 |
| [案件与证据](../features/content-moderation-case-evidence-action-design.md)、[申诉纠正](../features/content-moderation-appeal-relief-design.md)、[用户屏蔽](../features/user-block-relationship-isolation-design.md) | F4-I / J / K，原任务和验收 | 内容、关系 |

指定作用域的允许 / 拒绝、处置和纠正均须有一致结果及证据；已实现管理页面不等于所有权限边界通过。

<a id="knowledge"></a>

## 知识与创作

让 Docs / Wiki 承接可持续知识贡献、作者协作和附件生命周期；不把开发者内部记录与用户知识内容混为同一发布对象。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [文档系统](../guide/document-system.md)、[作者协作](../features/wiki-author-contribution-collaboration-design.md) | F4-G 及既有文档工作流 | 身份与治理、多端体验 |
| [附件隐私与生命周期](../features/wiki-attachment-privacy-lifecycle-design.md) | F4-L，原合同、失败路径及验证记录 | 平台文件服务、治理 |

完成需覆盖贡献、审查、发布和附件权限 / 撤回；开发文档导航完善不算用户知识产品验收。

<a id="incentives"></a>

## 激励体系

用已有经验、宠物、资产与商城支持社区参与；不扩张为新的经济、支付或风控平台。内容与身份仍为上游事实源。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [商城履约](../features/shop-product-effect-entitlement-fulfillment.md) | F1，原权益与履约验收 | 身份与治理、平台 |
| [宠物路线](../features/radish-pet-roadmap.md)、[宠物说明](../guide/radish-pet-system.md) | F4-H 及原已纳入范围 | 关系、公开主页 |
| [内容赞赏](../features/forum-content-reward.md)、[排行榜](../features/leaderboard.md) | F4-N / S，原范围与验收 | 内容互动、身份治理 |

已有资产变更、权益生效和公开展示在同一批次有一致验证；后置经济扩展不因归组成为当前待办。

<a id="experience"></a>

## 多端体验

Web 优先覆盖 PC / mobile，Flutter Native 按平台阶段交付；拥有壳层、页面族、主题与跨端衔接，不复制各业务专题的任务。WebOS 只保留历史兼容，Tauri 继续弃用。

| 子专题 / 整合入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [家族 UI 与页面族](../features/family-ui-convergence-design.md) | F4-R；页面族及主题任务在原专题 | 全部业务能力、平台 |
| [Flutter Native](../features/flutter-native-product-ui-design.md) | P1–P8；任务、平台验收及缺口在原专题，下一工作由 current 排序 | 身份、关系、内容 |
| [客户端连接](../frontend/client-server-connection-plan.md) | 已登记连接方向，细化与验收按原文，不能推导已实现 | 身份、平台与交付 |
| [Web 完全化整合](p3-12-web-completion-webos-retirement.md) | P3-12 历史阶段包，引用业务专题，不另维护当前业务状态 | 全部业务能力 |

一个平台的构建、模拟器、设备运行、签名和分发分别判断。P8-C 等未通过项不能由 Web 或其他 Native 平台的 Go 代签。

<a id="platform-delivery"></a>

## 平台与交付

提供可维护、可验证的运行与交付基础。拥有依赖、日志、存储、可靠任务、验证和发布契约；不借治理改动扩张产品范围，不把本地验证当生产部署。

| 子专题入口 | 阶段与任务 / 验收来源 | 协同 |
| --- | --- | --- |
| [依赖许可治理](../features/dependency-license-governance.md#delivery-slices) | 许可范围、各切片状态和验收由原方案承接，records 保存结果；即时顺位沿 current | 文件 / 图片、数据库、运行宿主 |
| [统一日志](../features/unified-logging-governance-design.md) | L1–L6 阶段及退出条件在总纲；L2 细项在[事件契约](../features/unified-logging-contract.md)，不重复任务 | 所有宿主、Console |
| [上传与文件](../features/file-upload-design.md)、[配额修正切片](../features/upload-quota-boundary-fix-plan.md) | 已有文件契约及指定修复范围 | 内容、知识、依赖治理 |
| [运行配置](../guide/runtime-configuration-boundaries.md)、[系统设置](../guide/system-settings-governance.md) | 原配置 / 设置契约及实施范围 | 身份与治理 |
| [验证基线](../guide/validation-baseline.md)、[发布工程](p3-12-e8-release-engineering-maturity-security-closure.md)、[部署](../deployment/guide.md) | 验证 / 交付规则与历史阶段证据；当前候选独立验收 | 全部能力主线 |

依赖替换需覆盖精确产物、行为与对应分发材料；日志需按 L 批次验收，发布另按授权执行。工程候选仅从[候选清单](engineering-improvement-candidates.md)选取，不自动中断 current 顺位。

## 任务、状态与证据

设计状态、执行状态和验证事实分别维护；细项状态归所属子专题或明确的实施切片，current 只摘录当前重点和停止线。完成应注明版本、范围、环境、证据与未验项，长期能力只汇总已交付基线和剩余范围，不按文档数计算完成率。完整维护规则见[文档治理](../guide/document-governance.md#planning-hierarchy)。
