# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-10 跨端信息架构与下一批开发任务选择`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束。
  - 第二开发阶段已完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳、多端路线分工和产品治理收口。
  - `2026-05-25` 路线复盘后，多端投入收敛为 `纯 Web + Flutter` 主线：根路径 `/` 与默认浏览器入口转向纯 Web，`/desktop` 仅保留为 WebOS 历史入口，PC/Tauri 放到最后再评估且不再绑定 WebOS。
  - `P3-1` 至 `P3-5` 已完成公开内容 SEO / 分享、`Post.PublicId` 试点、留存回流、动态 sitemap、详情 head snapshot 与部署 smoke 入口。
  - `P3-6` 公开增长部署观察已完成本地 Gateway 与生产域名 `https://radishx.com` smoke 收口，转入维护线。
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访和高信号候选筛查，当前未发现新的 `P0/P1`。
  - `P3-7-C` 已完成 WikiApp、ChatApp、ContentModerationService 与 ExperienceService 首批热区治理，继续拆低风险候选不再作为默认主线。
  - `P3-8-A / B / C` 已完成多端功能缺口与 UI 设计入口审计、Flutter 公开榜单、Console 治理工作台设计端点和 Console 高频页面类型试点。
  - `P3-8-D` 已完成纯 Web、移动 Web、Flutter、Console 围绕公开访问、登录回流、购买 / 订单 / 背包、胡萝卜流水、权限授权和 LongId 字符串契约的多轮治理，可以作为默认主线阶段收口。
  - `P3-9` 首批真实使用主路径已经完成自动化总回归、人工复核、`dev -> master` PR #54 合并和 `Repo Quality` 四项检查。
  - 本轮明确跳过发布，不创建 tag，不进入 M15 测试 / 生产部署流程；项目继续处于第三开发阶段的功能建设期，而不是生产稳定运营期。
  - 当前进入 `P3-10`：围绕 Web、Flutter、PC/Tauri、Console 的功能与 UI 差距，重新梳理信息架构、任务归属和下一批开发优先级。

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-10 跨端信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `40%`：P3-10 跨端任务归属与信息架构评估，重点是 Web、Flutter、PC/Tauri、Console 的入口命名、导航结构和用户任务边界。
- `30%`：下一批开发候选矩阵，重点是功能缺口、UI 差距、真实使用价值、实现成本和验证成本排序。
- `15%`：Flutter 体验结构评估，重点是 `发现 / 消息 / 更多 / 我的` 是否成立，以及商城、通知、资产、个人资料等任务归属。
- `10%`：纯 Web 与 PC/Tauri 边界评估，重点是公开访问、登录后轻量链路、WebOS `/desktop` 保留入口和 Tauri 后置增强边界。
- `5%`：P3-9 / P3-8-D / ID Phase A / Console UI 维护线，只处理新增命中、真实编译错误或明显视觉断裂。

## 已确认的多端方向

1. **Web 浏览器**
   - 使用纯 Web 壳层，覆盖 PC 浏览器与移动浏览器。
   - 根路径 `/` 与默认浏览器入口转向纯 Web。
   - 公开页功能完整度、移动端视图适配、分享 / 回流、轻互动和登录后轻量链路是发布候选验收重点。
2. **Android / iOS**
   - 使用 Flutter 移动原生安装包路线。
   - Android MVP 已完成第一轮；iOS 后续单独评估。
   - Flutter 可在成熟 API 支撑下推进同一工作流的一组受控写入动作，但不扩展成完整移动能力套件。
3. **WebOS `/desktop`**
   - 仅作为历史桌面工作台保留入口。
   - 不再承接新增功能；既有高价值能力按价值逐步迁移到纯 Web 或 Flutter。
   - 只处理阻断级可用性问题和迁移所需缺口。
4. **Windows / macOS / Linux**
   - PC/Tauri 放到最后再评估。
   - 若重启，Tauri 只增强纯 Web 体验，承接系统通知、托盘、文件选择、本地缓存、外链打开、登录回跳等能力。
   - 不再默认绑定 WebOS。
5. **Console**
   - Console 是治理后台，不是公开产品壳层。
   - 后续新增和改动页面必须逐步收敛到共享 UI、统一主题 token 和一致交互反馈。

## 下一顺位

- `P3-10-A 跨端任务归属与信息架构评估`
  - 梳理 Web、Flutter、PC/Tauri、Console 分别承接哪些真实用户任务。
  - 明确入口命名、导航结构、返回语义、登录恢复、错误态和跨端一致性要求。
  - 产出下一批开发候选矩阵，先排序再开工。
- `P3-9 主路径维护回拉`
  - 只在后续复核或真实使用暴露主路径阻断时回拉。
  - 不恢复 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护的无限期深挖。
- `P3-8-D / P3-7-C3 / P3-6` 维护线
  - 只在真实问题、发布候选验收或合并前回归暴露高信号断点时回拉。

## 长期方向与当前衔接

- 标识体系升级：`InternalId / PublicId / FederationId` 分层，`PublicId` 长期优先 `UUIDv7`。
- 社区联邦化：公开社区对象优先按 `ActivityPub + WebFinger` 方向预留。
- 租户语义调整：长期产品语义转向 `instance / node / space / group / category`。
- 多端客户端长期保持分工清晰：纯 Web 负责低门槛访问、PC / 移动浏览器和登录后轻量链路，Flutter 负责移动原生体验，WebOS 只作为 `/desktop` 保留迁移线，PC/Tauri 后置且只增强纯 Web。
- UI 长期治理以设计源文件、视觉 token、共享组件和端点实现同步为准，不让各端继续自然分叉。
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)。

## 并行维护

- 公开 head smoke、动态 sitemap、head snapshot 缓存与生产域名配置。
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线。
- 发布记录、回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`。
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护与迁移评估。
- `Identity Guard`、LongId 字符串契约和 PublicId 试点守护。

## 明确后置

- 把跳过发布误判为进入 Phase 4 稳定运营或大规模发布运维。
- 创建本轮发布 tag、等待镜像或进入 M15 测试 / 生产部署流程。
- 继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 继续把 Console 页面微调作为默认主线。
- Flutter 受控写入之外的完整移动写入能力套件。
- 一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史治理。
- 完整 `PublicId` 全量迁移、数据库主键迁移与 ActivityPub / WebFinger 实现。
- `Gateway & BFF` 深化。
- Redis 与缓存治理专题：多实例 SignalR Backplane、聊天室在线状态、通知未读原子计数、上传限流、商城 / 萝卜币幂等与并发保护、排行榜 / 热点读模型缓存，详见 [Redis 与缓存治理专题](/planning/redis-cache-governance)。
- `Console-ext Phase 2+`。
- 开放平台第三方接入 / SDK。
- 邮件通知系统。
- 完整 `PWA / Service Worker / 离线能力`。
- 完整 Playwright / E2E 平台。
- 完整可观测性平台、Tracing / Metrics 大阶段。

## 阶段文档规则

- `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 等关键入口只描述最近阶段和进度，不承载长背景。
- 功能批次、验证命令、人工验收记录和历史事实默认写入 `Docs/changelog/`、`Docs/planning/archive.md` 或对应专题文档。
- 判断阶段定义时，以本页、[当前进行中](/planning/current)、[第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)、[P3-10 跨端信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture) 与 [已完成摘要](/planning/archive) 为准。
