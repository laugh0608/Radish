# 开发路线图（总览）

> 本页只保留 **当前阶段、当前主线、下一顺位、并行维护线与后置池**。
>
> 当前执行细节见 [当前进行中](/planning/current)，已完成阶段见 [已完成摘要](/planning/archive)。

## 当前状态

- **当前里程碑**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`Phase 2-3 Flutter 客户端 MVP`
- **当前阶段**：`2026-04-06` 已完成首版真实发布 `v26.3.2-release`，第一开发阶段正式结束；`2026-04-07` 已完成阶段口径重置与多壳层策略冻结；截至 `2026-04-18`，`Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口并转入稳定维护，当前产品主线正式切到 `Phase 2-3 Flutter 客户端 MVP`。当前第一批范围定义、真相源文档与工程骨架已完成；第二批已完成 Android MVP 可测链路、最小 forum notification 回流、Android 本地 release APK 发布候选首轮收口、Flutter `--dart-define` Gateway 环境切换能力，以及 Android RC 签名配置诊断与分发前置清单；第三批已完成中文文案基线、个人复访入口产品化与 forum detail 轻回应最小读写闭环，并已通过一轮 Android 真机人工复核。`

## 当前主线入口

- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [前端多壳层策略](/frontend/shell-strategy)
- [当前进行中](/planning/current)
- [论坛轻回应墙设计草案](/features/forum-quick-reaction-wall)
- [论坛应用功能说明](/features/forum-features)

## 当前批次范围

- 当前主线当前已从“Flutter 客户端 Android MVP 的范围定义与工程骨架、第二批真实业务接线”进入“第三批产品层回补”
- `Phase 2-3` 第二批当前已落地：最小会话恢复链路、壳层登录态分发，以及 forum 从公开 feed 到帖子详情、评论分页、子评论分页、作者跳转、评论精确定位、public profile 详情回跳与首批壳层 / 宿主 handoff 的收口
- forum detail 内最小原地登录入口、登录目标持久化续接，以及围绕该入口的一轮 Android 真机人工联调当前也已完成；detail 来源登录不再只能依赖壳层状态条或 profile 入口间接触发
- `discover / docs / profile` 当前也都已完成首批真实只读页面接线，不再停留在统一占位页
- `Phase 2-3` 第三批当前已完成产品层回补：中文主文案基线已经覆盖主 tab、壳层状态、登录提示和首批真实页面状态；`profile` 已产品化最近 forum 阅读与公开主页复访入口；forum detail 已接入轻回应墙最小读写闭环
- Android MVP 当前可测链路已完成一轮人工验收；登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读与 detail 原地登录续接当前都可作为第二批已验收事实
- Android 宿主通知 handoff、壳层最近阅读续接与 public profile 评论入口当前都已统一接到 Flutter forum 的原生 handoff 目标；Flutter 已登录壳层当前也已补一个最小可测 forum notification 来源，可从当前用户最新通知读取字符串化 `postId / commentId` 并回到原生 forum detail
- Android MVP 本地 release APK 发布候选当前已完成首轮收口：包身份为 `com.radish.client` / `Radish`，release signing 读取逻辑与密钥忽略边界已落地，release 包联网权限已补齐，真机安装后登录、基础读取与样式显示均已确认正常
- Android RC 签名配置诊断与分发前置清单当前已完成首轮收口：Gradle 提供 `:app:checkReleaseSigningConfig` 检查正式签名材料，清单见 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)
- 第三批 Android 真机复核当前已通过；复核中发现的同一帖子详情返回列表后无法再次打开的问题已完成 handoff 状态修复与定向测试覆盖；Android RC 外部分发继续等待真实签名材料、测试 Gateway 与外部分发对象，不把发布线冻结误判为业务功能阻塞
- `Phase 2-2` 公开内容壳层固定转入稳定维护，不再与 Flutter 混成一条建设线

## 已确认的长期方向（暂不进入当前主线）

- **标识体系升级**
  - 长期方向已从“继续把 `Snowflake long` 作为统一对象标识”切换为“`InternalId / PublicId / FederationId` 分层”
  - `InternalId` 面向数据库内部主键；`PublicId` 面向 API、前端路由、公开链接与跨端契约；`FederationId` 面向未来联邦 canonical URI
  - `PublicId` 的长期底层标准优先采用 `UUIDv7`，而不是继续扩大 Snowflake 的外部暴露面
- **社区联邦化**
  - 这是确定会进入未来阶段的方向，但当前不纳入第二阶段前半程主线
  - 联邦节点默认按“一个公开域名 / 一个部署实例”定义，不按 `Tenant` 定义
  - 公开社区对象联邦优先按 `ActivityPub + WebFinger` 方向预留，聊天室跨节点互通作为独立后续议题
- **租户语义调整**
  - 当前多租户实现继续保留，用于数据库隔离、权限与历史兼容
  - 长期社区产品语义不再以“租户”为中心，未来公开社区与联邦口径转向 `instance / node / space / group / category`
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 并行维护

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- 发布记录、最小回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`
- 已收口的一期能力稳定维护，例如桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等

## 明确后置

- `Gateway & BFF` 深化
- `Console-ext Phase 2+`
- 开放平台第三方接入 / SDK
- 邮件通知系统
- 完整 `PWA / Service Worker / 离线能力`
- 完整 Playwright / E2E 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 阶段文档规则

- 阶段定义只以：
  - [开发路线图](/development-plan)
  - [当前进行中](/planning/current)
  - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
  - [前端多壳层策略](/frontend/shell-strategy)
  - [已完成摘要](/planning/archive)
  为准
- `M14 / M15 / post-m15` 相关文档继续有效，但只承担维护线说明，不再承担“当前产品主线”定义
- 第一开发阶段的范围与结果，继续以：
  - [首版 dev 边界](/planning/dev-first-scope)
  - [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix)
  - [已完成摘要](/planning/archive)
  作为归档依据
