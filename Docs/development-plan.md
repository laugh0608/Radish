# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-8-D 多端主路径缺口补齐与验收矩阵`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - 第二开发阶段已完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳、多端路线分工和产品治理收口
  - `2026-05-25` 路线复盘后，多端投入进一步收敛为 `纯 Web + Flutter` 主线：根路径 `/` 与默认浏览器入口转向纯 Web，`/desktop` 仅保留为 WebOS 历史入口，PC/Tauri 放到最后再评估且不再绑定 WebOS
  - `P3-1` 至 `P3-5` 已完成公开内容 SEO / 分享、`Post.PublicId` 试点、留存回流、动态 sitemap、详情 head snapshot 与部署 smoke 入口
  - `P3-6` 公开增长部署观察已完成本地 Gateway 与生产域名 `https://radishx.com` smoke 收口，转入维护线
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访小闭环和高信号候选筛查，当前未发现新的 `P0/P1`
  - 重新评估后确认：原 `P3-7` 收尾口径没有给出最近阶段的明确开发入口，当前应先做近期任务重评估，而不是直接切入后续重点专题
  - `P3-7-C` 已完成近期任务重评估，首批一天级任务按 `WikiApp -> ChatApp` 热区治理推进；`P3-7-C1` WikiApp 与 `P3-7-C2` ChatApp 首批拆分已完成
  - `P3-7-C3` 已完成 `ContentModerationService` 后端热区首批行为等价拆分；`ExperienceService` 每日统计、经验治理观察规则、治理动作留痕、等级配置缓存、交易记录、冻结状态、排行榜与管理员调整辅助逻辑拆分也已完成
  - `P3-8-A` 多端功能缺口与 UI 设计入口审计已完成，`P3-8-B1` Flutter 公开榜单只读入口已落地，`P3-8-B2` Console 治理工作台设计端点已建立
  - `P3-8-C1` 已完成 Console 内容治理与经验治理首批结构基座：页面 helper、列定义和局部组件已拆分，治理工作台布局已接入，设计稿已扩展到 `P01-P08` 编号页面
  - `P3-8-C2` 已完成 Console 高频历史页面类型试点：设置、表格 CRUD、调度总览、工具型、权限配置、详情和个人资料页面已迁入对应基座；继续筛剩余 CSS 不再作为默认主线
  - 当前切到 `P3-8-D`：移动 Web 公开视图验收矩阵、根路径 `/` 默认入口切换、历史 `?demo` 入口移除、公开个人页分享入口、Flutter 公开商品只读详情、轻量 forum 通知列表、公开商城列表、论坛详情评论发布 / 回复、原生公开详情分享入口、公开文档阅读链路、公开主页链接 / 来源返回复核、登录态商城订单 / 背包只读入口、订单详情与背包来源只读承接、登录态通知只读列表、纯 Web forum 公开详情到工作台参与回流和 shop 登录态购买意图回流已完成；后续继续从纯 Web / Flutter 真实主路径缺口中选择下一批可验证任务

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `45%`：Flutter 真实移动主路径补齐，已补公开商品只读详情、轻量 forum 通知列表、公开商城列表、论坛详情评论发布 / 回复、公开详情分享入口、docs 文档内链阅读、公开主页链接 / 来源返回复核、登录态商城订单 / 背包只读入口、订单详情 / 背包来源承接和登录态通知只读列表，后续继续从移动阅读、复访和跨端回流缺口中选择下一批任务
- `20%`：纯 Web 维护线，已回拉 forum 公开详情到工作台参与回流和 shop 登录态购买意图回流；后续只处理公开访问、分享预览、购买 / 订单 / 背包、权限授权或运行日志暴露的高信号问题
- `20%`：移动 Web 公开视图矩阵维护，当前已阶段收口，后续只做批次级验收或阻断问题复核
- `5%`：WebOS `/desktop` 保留迁移线，只处理阻断级问题和迁移所需缺口，不再作为新增功能承载方向
- `10%`：Console、共享 UI 与后端热区维护，只处理真实使用、权限页面、明显视觉断裂或明确收益高且可验证的问题

## 已确认的多端方向

1. **Web 浏览器**
   - 使用纯 Web 壳层，覆盖 PC 浏览器与移动浏览器
   - 根路径 `/` 与默认浏览器入口转向纯 Web
   - 公开页功能完整度、移动端视图适配、分享 / 回流、轻互动和登录后轻量链路是后续重点
2. **Android / iOS**
   - 使用 Flutter 移动原生安装包路线
   - Android MVP 已完成第一轮；iOS 后续单独评估
   - 移动客户端仍有大量功能与体验需要按主路径补齐，不默认追加低收益微体验
3. **WebOS `/desktop`**
   - 仅作为历史桌面工作台保留入口
   - 不再承接新增功能；既有高价值能力按价值逐步迁移到纯 Web 或 Flutter
   - 只处理阻断级可用性问题和迁移所需缺口
4. **Windows / macOS / Linux**
   - PC/Tauri 放到最后再评估
   - 若重启，Tauri 只增强纯 Web 体验，承接系统通知、托盘、文件选择、本地缓存、外链打开、登录回跳等能力
   - 不再默认绑定 WebOS
5. **Console**
   - Console 是治理后台，不是公开产品壳层
   - 后续新增和改动页面必须逐步收敛到共享 UI、统一主题 token 和一致交互反馈

## 下一顺位

- `P3-8-D 多端主路径缺口补齐与验收矩阵`
  - 第一批建立移动 Web 公开视图验收矩阵，覆盖 `/discover / forum / docs / u/:id / leaderboard / shop`
  - 验收重点是窄屏信息密度、来源返回、分享入口、公开只读边界、公开链接和跨端回流，而不是继续做整站视觉重构
  - 根路径 `/` 默认入口切换、`?demo` 清理、`/u/:id` 显式分享入口、移动 Web 公开视图矩阵阶段收口、Flutter 公开商品只读详情、轻量 forum 通知列表、公开商城列表、论坛详情评论发布 / 回复、公开详情分享入口、docs 文档内链阅读、原生公开主页链接 / 来源返回复核、登录态商城订单 / 背包只读入口、订单详情与背包来源只读承接、登录态通知只读列表、纯 Web forum 公开详情到工作台参与回流和 shop 登录态购买意图回流已完成；下一批继续从 Flutter 真实移动主路径或跨端回流高信号缺口中选择，不直接启动完整移动商城、完整通知中心或 WebOS 新功能
- `P3-8-C2 Console 页面类型对齐`
  - 首批高频页面已收口，后续作为维护项
  - 只在真实使用、权限页面或明显视觉断裂暴露问题时回拉，不继续把剩余 CSS 筛查作为默认主线
- `P3-7-C3 后端 Service 热区治理`
  - 作为当前主线已收口，剩余 `ExperienceService` 经验发放主流程进入后续评审池
  - 后续只处理明确收益高、行为等价、可验证的拆分候选
- `P3-6 / P3-7` 继续作为维护线，只在真实问题或发布前回归暴露高信号断点时回拉小闭环

## 长期方向与当前衔接

- 标识体系升级：`InternalId / PublicId / FederationId` 分层，`PublicId` 长期优先 `UUIDv7`
- 社区联邦化：公开社区对象优先按 `ActivityPub + WebFinger` 方向预留
- 租户语义调整：长期产品语义转向 `instance / node / space / group / category`
- 多端客户端长期保持分工清晰：纯 Web 负责低门槛访问、PC / 移动浏览器和登录后轻量链路，Flutter 负责移动原生体验，WebOS 只作为 `/desktop` 保留迁移线，PC/Tauri 后置且只增强纯 Web
- UI 长期治理以设计源文件、视觉 token、共享组件和端点实现同步为准，不让各端继续自然分叉
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 并行维护

- 公开 head smoke、动态 sitemap、head snapshot 缓存与生产域名配置
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线
- 发布记录、回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护与迁移评估

## 明确后置

- 跳过 `P3-8-A` 审计直接进入整站 UI 重构、多端同时重写或大范围实现
- 继续把 Console 页面微调作为默认主线
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
