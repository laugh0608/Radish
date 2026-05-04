# 开发路线图（总览）

> 本页只保留 **当前阶段、当前主线、下一顺位、并行维护线与后置池**。
>
> 当前执行细节见 [当前进行中](/planning/current)，已完成阶段见 [已完成摘要](/planning/archive)。

## 当前状态

- **当前里程碑**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`Phase 2-3 Android MVP 第一轮完成后的多端路线收口`
- **当前阶段**：`2026-04-06` 已完成首版真实发布 `v26.3.2-release`，第一开发阶段正式结束；`2026-04-07` 已完成阶段口径重置与多壳层策略冻结；截至 `2026-04-18`，`Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口并转入稳定维护；截至 `2026-05-04`，`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论。随后完成 React 复用路线 spike：Capacitor Android 因登录 / OIDC 与本机调试复杂度终止，不进入移动端产品化主线；Tauri 桌面壳命令级 spike 成立，但其合理定位是 `Tauri 壳 + WebOS 桌面工作台`，不是移动端替代方案，也不是原生 UI 重写路线。后续开发口径收束为三端分工：Web 浏览器使用公开内容壳层，Android / iOS 安装包使用 Flutter 移动原生路线，Windows / macOS / Linux 安装包优先评估 Tauri + WebOS。

## 当前主线入口

- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [前端多壳层策略](/frontend/shell-strategy)
- [当前进行中](/planning/current)
- [论坛轻回应墙设计草案](/features/forum-quick-reaction-wall)
- [论坛应用功能说明](/features/forum-features)

## 当前批次范围

- 截至 `2026-05-04`，当前主线已从“Android MVP RC 验收批次”收口为“Android MVP 第一轮完成后的多端路线定稿”
- `Phase 2-3` 已完成从最小壳层、最小登录 / 会话恢复、forum / docs / profile / discover 真实读取，到复访、轻回应、刷新体验、只读上下文、验证留痕补洞与 RC 验收记录的阶段性收口
- 当前 MVP 结束条件覆盖产品闭环、工程质量、真实交付形态与阻断标准；RC Go 记录见 [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- 后续不再默认开启新的 Flutter 微体验批次；[多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation) 当前已形成三端分工结论：Web 公开内容壳层、Flutter 移动安装包、Tauri + WebOS 桌面安装包
- 若后续暴露 `P0 / P1` 阻断，则只做定点修复，不回头扩完整通知中心、系统推送、发帖、完整评论提交、点赞、投票、编辑治理或 Flutter 专属 BFF
- `Phase 2-2` 公开内容壳层固定转入稳定维护，不再与 Flutter 混成一条建设线

## 后续开发精力规划

近期默认按以下比例安排精力，除非出现 `P0 / P1` 阻断或发布节点要求：

- `35%`：Android MVP 产品化深化，包括测试对象、反馈回收、已知问题列表、版本说明、release 前验收与分发留痕
- `25%`：Tauri + WebOS 桌面安装包第二轮评估，包括默认 WebOS 入口、系统浏览器 + loopback 桌面登录回跳、窗口生命周期、installer、签名、自动更新与托盘 / 菜单取舍
- `20%`：公开内容壳层稳定维护，包括 PC / 移动浏览器响应式问题、公开阅读质量、来源返回与分享链路补洞
- `10%`：社区深化维护，只处理已落地轻回应、通知回流、论坛阅读链路里的真实问题
- `10%`：宿主运行、验证基线、文档同步、发布 / 回滚 / 可观测性维护线

当前不再按“继续扩大 Flutter 到所有平台”或“React WebView 统一所有端”规划精力。

## 已确认的多端方向

1. **Web 浏览器**
   - 使用公开内容壳层
   - 同时适配 PC 浏览器与移动浏览器尺寸
   - 重点是公开阅读、分享、SEO、轻互动和低门槛访问
2. **Android / iOS**
   - 使用 Flutter 单独开发原生安装包
   - Android MVP 已完成第一轮；iOS 后续单独评估
   - 不使用 Capacitor 作为登录态移动端产品化路线
3. **Windows / macOS / Linux**
   - 使用 `Tauri 壳 + WebOS 桌面工作台`
   - Tauri 负责系统级桌面能力与安装包分发
   - WebOS 继续负责 Dock、窗口系统、多应用容器和桌面业务体验
   - 默认入口当前已切到 `/desktop`，而不是 `/docs` 公开阅读页

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
