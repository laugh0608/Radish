# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-8-A 多端功能缺口与 UI 设计入口审计`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - 第二开发阶段已完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳、多端路线分工和产品治理收口
  - `P3-1` 至 `P3-5` 已完成公开内容 SEO / 分享、`Post.PublicId` 试点、留存回流、动态 sitemap、详情 head snapshot 与部署 smoke 入口
  - `P3-6` 公开增长部署观察已完成本地 Gateway 与生产域名 `https://radishx.com` smoke 收口，转入维护线
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访小闭环和高信号候选筛查，当前未发现新的 `P0/P1`
  - 重新评估后确认：原 `P3-7` 收尾口径没有给出最近阶段的明确开发入口，当前应先做近期任务重评估，而不是直接切入后续重点专题
  - `P3-7-C` 已完成近期任务重评估，首批一天级任务按 `WikiApp -> ChatApp` 热区治理推进；`P3-7-C1` WikiApp 与 `P3-7-C2` ChatApp 首批拆分已完成
  - `P3-7-C3` 已完成 `ContentModerationService` 后端热区首批行为等价拆分；`ExperienceService` 每日统计、经验治理观察规则、治理动作留痕、等级配置缓存、交易记录、冻结状态、排行榜与管理员调整辅助逻辑拆分也已完成
  - 当前评估确认：`P3-7-C3` 作为主线阶段收口，`P3-8-A` 从后续重点切为当前主线；首批只做多端功能缺口与 UI 设计入口审计

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `45%`：`P3-8-A` 多端功能缺口与 UI 设计入口审计，形成矩阵、端点分组和首批开发任务建议
- `20%`：WebOS / PC 工作台与既有产品主路径补强，继续处理购买、订单、背包、通知、窗口参数和复访链路中的高价值断点
- `15%`：公开内容与移动回流维护，只处理公开访问、head / sitemap、分享、回流和运行日志暴露的高信号问题
- `10%`：Console 与共享 UI 收敛，新增或改动页面优先复用 `@radish/ui`、主题 token 和统一交互反馈
- `10%`：后端 Service 热区后续评审与既有治理维护，只处理明确收益高、行为等价、可验证的候选

## 已确认的多端方向

1. **Web 浏览器**
   - 使用公开内容壳层，覆盖 PC 浏览器与移动浏览器
   - 公开页功能完整度、移动端视图适配、分享 / 回流和轻互动体验是后续重点
2. **Android / iOS**
   - 使用 Flutter 移动原生安装包路线
   - Android MVP 已完成第一轮；iOS 后续单独评估
   - 移动客户端仍有大量功能与体验需要按主路径补齐，不默认追加低收益微体验
3. **Windows / macOS / Linux**
   - 使用 `Tauri 壳 + WebOS 桌面工作台`
   - Tauri 承接系统窗口、登录回跳、deep link 兼容和安装包能力
   - WebOS 继续承接 Dock、窗口系统、多应用容器和桌面业务体验
4. **Console**
   - Console 是治理后台，不是公开产品壳层
   - 后续新增和改动页面必须逐步收敛到共享 UI、统一主题 token 和一致交互反馈

## 下一顺位

- `P3-8-A 多端功能缺口与 UI 设计入口审计`
  - 建立公开页、移动 Web 公开视图、Flutter、WebOS / PC / Tauri、Console 的功能缺口矩阵
  - 决定 UI 端点分组、`.pen` 设计稿拆分和设计源文件治理建议
  - 选出 `1-2` 个一天级、可验证、能明显推进产品的首批开发任务
  - 首批不直接大面积实现，默认验证 `npm run check:repo-hygiene:changed` 与 `git diff --check`
- `P3-7-C3 后端 Service 热区治理`
  - 作为当前主线已收口，剩余 `ExperienceService` 经验发放主流程进入后续评审池
  - 后续只处理明确收益高、行为等价、可验证的拆分候选
- `P3-6 / P3-7` 继续作为维护线，只在真实问题或发布前回归暴露高信号断点时回拉小闭环

## 长期方向与当前衔接

- 标识体系升级：`InternalId / PublicId / FederationId` 分层，`PublicId` 长期优先 `UUIDv7`
- 社区联邦化：公开社区对象优先按 `ActivityPub + WebFinger` 方向预留
- 租户语义调整：长期产品语义转向 `instance / node / space / group / category`
- 多端客户端长期保持分工清晰：公开 Web 负责低门槛访问，Flutter 负责移动原生体验，Tauri + WebOS 负责桌面工作台
- UI 长期治理以设计源文件、视觉 token、共享组件和端点实现同步为准，不让各端继续自然分叉
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 并行维护

- 公开 head smoke、动态 sitemap、head snapshot 缓存与生产域名配置
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线
- 发布记录、回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`
- 桌面壳层、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护

## 明确后置

- 跳过 `P3-8-A` 审计直接进入整站 UI 重构、多端同时重写或大范围实现
- 完整 `PublicId` 全量迁移、数据库主键迁移与 ActivityPub / WebFinger 实现
- `Gateway & BFF` 深化
- Redis 与缓存治理专题：多实例 SignalR Backplane、聊天室在线状态、通知未读原子计数、上传限流、商城 / 萝卜币幂等与并发保护、排行榜 / 热点读模型缓存，详见 [Redis 与缓存治理专题](/planning/redis-cache-governance)
- `Console-ext Phase 2+`
- 开放平台第三方接入 / SDK
- 邮件通知系统
- 完整 `PWA / Service Worker / 离线能力`
- 完整 Playwright / E2E 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 阶段文档规则

- `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 等关键入口只描述最近阶段和进度，不承载长背景
- 功能批次、验证命令、人工验收记录和历史事实默认写入 `Docs/changelog/`、`Docs/planning/archive.md` 或对应专题文档
- 判断阶段定义时，以本页、[当前进行中](/planning/current)、[第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 与 [已完成摘要](/planning/archive) 为准
