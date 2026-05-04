# 开发路线图（总览）

> 本页只保留 **当前阶段、当前主线、下一顺位、并行维护线与后置池**。
>
> 当前执行细节见 [当前进行中](/planning/current)，已完成阶段见 [已完成摘要](/planning/archive)。

## 当前状态

- **当前里程碑**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`Phase 2-3 Flutter 客户端 MVP`
- **当前阶段**：`2026-04-06` 已完成首版真实发布 `v26.3.2-release`，第一开发阶段正式结束；`2026-04-07` 已完成阶段口径重置与多壳层策略冻结；截至 `2026-04-18`，`Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口并转入稳定维护，当前产品主线正式切到 `Phase 2-3 Flutter 客户端 MVP`。当前第一批范围定义、真相源文档与工程骨架已完成；第二批已完成 Android MVP 可测链路、最小 forum notification 回流、Android 本地 release APK 发布候选首轮收口、Flutter `--dart-define` Gateway 环境切换能力，以及 Android RC 签名配置诊断与分发前置清单；第三批已完成中文文案基线、个人复访入口产品化与 forum detail 轻回应最小读写闭环，并已通过一轮 Android 真机人工复核；第四批“复访深化 + 已登录轻互动回看”与第五批至第二十三批窄范围小闭环、刷新体验收口、RC 补验评估、验证索引整理和命令级回归记录补洞均已完成代码或文档口径收口。`截至 2026-05-04，Android MVP RC 验收已给出 Go 结论`：本轮接受 `https://radishx.com` 作为 RC 验收 Gateway，release APK 命令级验证、签名检查、构建预检与小米 15S Pro / Android 16 真机人工复核均已通过，当前可将 `Phase 2-3 Android MVP` 标记为“第一轮完成”。`

## 当前主线入口

- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [前端多壳层策略](/frontend/shell-strategy)
- [当前进行中](/planning/current)
- [论坛轻回应墙设计草案](/features/forum-quick-reaction-wall)
- [论坛应用功能说明](/features/forum-features)

## 当前批次范围

- 截至 `2026-05-04`，当前主线已从“Android MVP RC 验收批次”收口为“Android MVP 第一轮完成”
- `Phase 2-3` 已完成从最小壳层、最小登录 / 会话恢复、forum / docs / profile / discover 真实读取，到复访、轻回应、刷新体验、只读上下文、验证留痕补洞与 RC 验收记录的阶段性收口
- 当前 MVP 结束条件覆盖产品闭环、工程质量、真实交付形态与阻断标准；RC Go 记录见 [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- 后续不再默认开启新的 Flutter 微体验批次；下一步先执行 [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)，用 `2-3` 天验证 `Capacitor + Tauri` 的 React 复用路线是否明显优于继续扩大 Flutter
- 若后续暴露 `P0 / P1` 阻断，则只做定点修复，不回头扩完整通知中心、系统推送、发帖、完整评论提交、点赞、投票、编辑治理或 Flutter 专属 BFF
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
