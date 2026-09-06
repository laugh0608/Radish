# 开发路线图（总览）

> 本页维护产品定位、阶段目标、产品线分工和阶段衔接。即时顺位、平台成熟度、候选版本与停止线统一查看[当前进行中](/planning/current)，历史批次查看[记录索引](/records/)。
>
> 整理日期：2026-09-06。本次修正文档归位与过期口径，不改变已确认的多端路线或工程实施顺位。

## 产品定位与优先级

Radish 是面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

| 层级 | 能力 | 规划关注点 |
| --- | --- | --- |
| 社区核心 | 发现、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知和信任治理 | 阅读、参与、创作与回应后的回访闭环 |
| 社区支撑 | Docs、公开主页、Workbench 低频能力地图与 Console | 知识沉淀、身份展示与治理 |
| 辅助激励 | 经验、宠物、资产、背包和商城 | 完善已有能力的正确性与使用闭环，保持与社区主轴的关系 |
| 长期扩展 | 推荐、联邦、PWA、开放平台与多端增强 | 按明确价值重新评审，不能自动成为当前任务 |

低频模块不与社区核心并列争夺默认首页和开发顺位。每个新功能专题需说明其与内容、关系、贡献、治理或复访的关系，沿用[发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)。

## 当前阶段与两条工作线

当前处于 `Phase 4：长期维护与功能完成 / F4 既有功能持续完成`。

- **Web 维护线**：正式 Web 已发布，按反馈与改动风险维护既有功能、身份、权限、写操作、迁移、性能与部署能力。
- **Native 建设线**：Flutter 共享页面与平台基础逐步形成，分别确认构建、设备运行、系统集成和分发结论。
- **执行方式**：个人开发时只保持一个主要功能专题；`P0/P1` 可以中断，P2/P3 按同类问题成组维护。
- **状态解释**：Web 已发布不意味着所有 Native 平台可分发；模拟设备、无签名构建、本地运行与商店分发分别按所属门禁判断。各平台事实见[当前状态表](/planning/current)。

生产部署与长期运维由项目所有者独立负责。主动生产使用数据采集继续冻结到计划内功能完成、没有其他明确任务、产品进入最终完成体复核且项目所有者确认后重启；不作为功能实现、验收或 tag 的前置。

## 已确认的产品线分工

| 产品 / 入口 | 正式职责 | 边界 |
| --- | --- | --- |
| Web | PC / mobile 浏览器默认产品；根路径进入内容优先发现页，Workbench 承接低频能力 | 公开内容、SEO、完整 Author 与 Console 优先在 Web 完成 |
| Flutter Native | 次级原生安装包产品线，长期覆盖 Android / iOS / Windows / macOS / Linux | mobile-first、desktop stage-gated；不机械追平 Web，不提供 Flutter Web；推送、后台、签名和分发分别立项 |
| Console | 桌面优先的社区治理后台 | mobile 承接队列、搜索、证据回看和低风险处理，不要求完整复制桌面能力 |
| WebOS `/desktop` | 历史兼容入口 | 只处理阻断级问题和迁移所需缺口，不承接新增功能 |
| Tauri | 正式弃用的历史资产 | 不恢复开发、UI、CI、构建、签名、分发或验收门禁 |

运行拓扑与具体职责见[架构总览](/architecture/overview)和[多壳层策略](/frontend/shell-strategy)。

## 阶段路线

| 阶段 | 已形成的能力 | 后续归属 |
| --- | --- | --- |
| 第一阶段：首版发布 | 认证、基础社区、商城、治理、部署与回滚基线 | 历史结论归档 |
| 第二阶段：社区深化与多端验证 | 公开内容壳层、Flutter Android MVP、Tauri / WebOS 验证与路线裁决 | 正式产品收束到 Web / Flutter Native |
| 第三阶段：Web-first 与正式产品化 | 正式 Web 能力迁移、页面族、身份与写操作治理、候选验证和发布路径 | 发布后维护与专题持续治理 |
| Phase 4：长期维护与功能完成 | 已发布 Web 持续完善；Native 页面与平台分阶段建设 | 即时阶段进度与下一事项仅在 `current.md` 维护 |

第三阶段的进入条件与 Release Go 历史口径见[P3-12-E8 发布工程专题](/planning/p3-12-e8-release-engineering-maturity-security-closure)；后续候选按[验证基线](/guide/validation-baseline)、版本及部署专题执行，不用历史“已通过”替代新候选验证。

## 功能完成线的已关闭范围

以下为能力分组与设计入口，不是待执行清单。批次级测试数字、历史“下一步”和授权记录不再复制到总览。

- **关系与复访**：一对一私聊、F4-B 通知中心、F4-C 聊天搜索、F4-D Reaction、F4-E 置顶、F4-F 已读回执、F4-K 用户屏蔽。
- **知识与信任治理**：F4-G Docs 作者协作、F4-I 治理案件与证据、F4-J 申诉纠正、F4-L Wiki 附件隐私。
- **论坛内容与发现**：F4-M 版本恢复、F4-N 赞赏、F4-O 回答生命周期、F4-P 私有收藏、F4-Q 标签公开发现与 SEO。
- **激励与产品呈现**：F1 商城履约、F2 主题、F3 i18n、F4-H 宠物公开名片、F4-S 公开排行榜、F4-R Web 页面族与主题门禁。

完整功能入口与退出边界见[发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)、[家族 UI 专题](/features/family-ui-convergence-design)和[Flutter Native 专题](/features/flutter-native-product-ui-design)。

## 阶段衔接与改进候选

1. Native 继续按已经确认的平台阶段推进；即时顺位、外部条件和运行范围见[当前进行中](/planning/current)。
2. 2026-09-06 的[全面审阅](/records/project-review-2026-09-06)已形成[工程改进候选清单](/planning/engineering-improvement-candidates)，按验证可信度、后台可靠性、管理端边界、Web 加载、Flutter CI 与维护热点排序。
3. 候选清单只承载建议范围和完成标准，不自动替代当前专题；进入实施时按既有协作规则确认方案，再更新 `current.md`。
4. 完整功能批次形成后按现行 Git 与验证规则准备集成，不要求等待全部原生平台完成；合并、创建 tag、镜像与部署保持独立边界。

## 持续维护与明确后置

持续维护既有身份与权限、资产与订单、通知与可靠任务、数据库迁移、公开 head / sitemap、依赖与镜像安全、WebOS 阻断级兼容，以及已落地 Flutter 路径。大型文件、共享前端边界和历史卫生问题按触达范围治理，不启动全仓机械拆分。

以下继续后置，具体停止线见[当前进行中](/planning/current)与[Backlog](/planning/backlog)：

- WebOS 新功能、Tauri 恢复、Flutter Web、Native Console 和 Flutter 机械追平 Web。
- 推荐算法、ActivityPub / WebFinger、完整 PublicId / 主键迁移。
- 宠物经济扩展、完整移动商城、完整钱包 / 售后与资产风控平台。
- 完整 PWA、完整 E2E / 可观测性平台、Redis 平台化、开放平台和 BFF 深化。
- Q4 全量大文件拆分、历史样式与仓库卫生清零。
- iOS External TestFlight / App Store 及对应账号删除、完整 Flutter UGC、StoreKit / IAP 与商店材料，按项目所有者已确认的 D4 范围后置。

管理端安全边界的审阅候选仅用于明确现状和评估选项，不表示 BFF 或新的部署架构已获批准。任何候选也不重启主动生产使用数据采集。

## 文档入口

- [当前进行中](/planning/current)：今天做什么、平台成熟度、版本、停止线。
- [工程改进候选清单](/planning/engineering-improvement-candidates)：待确认改进的范围、顺序与完成标准。
- [全面审阅记录](/records/project-review-2026-09-06)：证据、判断及未验证范围。
- [Flutter Native 产品化与 UI 重构](/features/flutter-native-product-ui-design)：平台与页面的具体契约。
- [验证基线说明](/guide/validation-baseline)：当前验证入口、覆盖限制和使用方式。
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)：维护节奏与各功能专题。
- [未来规划](/planning/backlog)：明确后置项；[记录索引](/records/)与[历史入口流水](/records/f4-planning-entry-history-2026-09-06)：批次事实。
