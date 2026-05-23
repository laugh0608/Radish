# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-7-C3 后端 Service 热区评估与首批治理候选`
- **复核日期**：`2026-05-23`
- **最近结论**：
  - `P3-1` 至 `P3-5` 已完成公开内容增长、PublicId 试点、留存回流、动态 sitemap 与详情 head snapshot 首批闭环
  - `P3-6` 公开增长部署观察已收口，本地 Gateway 与生产公开域名 `https://radishx.com` 的 public head smoke 均通过，转入维护线
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访小闭环和高信号候选筛查，当前未发现新的 `P0/P1`
  - 原 `P3-7` 收尾口径偏向维护观察，无法给出最近阶段的明确开发入口；当前需要重新评估最近可推进任务，而不是直接切到远期专题
  - `P3-7-C` 已从原规划、backlog、未完成功能和近期主路径中选出下一批一天级任务：先做 WikiApp 热区治理，再做 ChatApp 热区治理
  - `P3-7-C1` 已完成 WikiApp 文档工作台首批热区拆分：侧边栏抽出为独立组件，纯 helper 迁出主文件，`WikiApp.tsx` 已降至硬上限以内
  - `P3-7-C2` 已完成 ChatApp 聊天工作台首批热区拆分：消息列表、频道侧栏、成员面板、输入区状态和纯 helper 已从主文件抽出，`ChatApp.tsx` 已降至硬上限以内
  - `P3-7-C3` 已完成后端 Service 首批候选治理：`ContentModerationService` 采用行为等价 partial 分文件拆出举报目标快照解析与队列导航装配，主文件从 `1735` 行降至 `806` 行
  - `ExperienceService` 已完成每日统计、经验治理观察规则与治理动作留痕辅助逻辑拆分，`ExperienceService.cs` 从 `2807` 行降至 `1807` 行
  - 项目仍处于单人开发期和功能建设期，没有稳定用户反馈和专职测试，不能把“等待真实使用观察”作为默认主线

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **收口工作台代码热区首批治理**
   - `WikiApp -> ChatApp` 两个一天级前端热区任务已完成首批行为等价拆分
   - 后端 Service 首批已完成 `ContentModerationService` 与 `ExperienceService` 每日统计 / 观察规则 / 治理动作留痕行为等价拆分，下一步继续复核 `ExperienceService.cs` 剩余热区是否存在安全拆分点
   - 不把“观察”作为无事可做的结论，也不直接把后续重点提前升级为当前主线
2. **维护观察降级为并行线**
   - `P3-6 / P3-7` 暴露的新问题仍可回拉小闭环，但不再占用主线
   - 维护线只处理公开访问、head / sitemap、分享预览、运行日志、购买 / 订单 / 背包、权限授权等高信号问题
3. **后续重点保留为路线方向**
   - 公开页面、移动端视图公开页、Flutter 移动客户端、WebOS / PC / Tauri 客户端、Console 等仍有大量功能未适配或未开发完，应纳入后续重点
   - client、console、公开页和各客户端 UI 缺少统一设计与系统化优化，需要后续专题阶段处理
   - UI 专题原则上使用 Pencil 设计稿先行，并在项目中保存和同步 `.pen` 设计源文件；端点拆分后续慎重决定

## 下一顺位

- `P3-7-C3 后端 Service 热区评估与首批治理候选`
  - 已完成 `ContentModerationService.cs` 首批行为等价拆分，以及 `ExperienceService.cs` 每日统计 / 观察规则 / 治理动作留痕辅助逻辑拆分
  - 下一步继续复核 `ExperienceService.cs` 剩余缓存、等级配置与发放辅助逻辑是否存在一天级、安全可验证的拆分候选
  - 不改 API 契约、权限语义、数据库结构或业务规则；若评估发现需要运行时行为变更，应先单独评审
  - 验证优先覆盖对应后端定向测试与 `dotnet build Radish.slnx -c Debug`
- `P3-8 多端功能补全与 UI 设计治理` 保留为后续重点方向，不作为当前最近任务的默认入口

## 并行维护项

- 公开 head smoke、动态 sitemap、head snapshot 缓存与 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生产域名配置维护
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`
- 桌面壳层、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护
- Console 新增或改动页面继续优先复用 `@radish/ui` 组件、交互反馈和主题 token

## 当前不做

- 把维护观察继续当作唯一主线
- 等真实用户反馈出现后才继续开发未完成功能
- 直接把未来多端功能补全和 UI 设计治理切成当前主线
- 未经近期任务重评估直接启动整站 UI 重构或多端同时重写
- 直接拍死所有 `.pen` 文件拆分；端点粒度需在后续专题中结合实际页面和客户端边界决定
- 绕过 Pencil / 设计稿直接大面积改 UI
- 把 Tauri 当作原生 UI 重写路线
- “移动版 WebOS”
- 完整 `PublicId` 全量迁移或数据库主键迁移
- 完整 Playwright / E2E 平台
- 完整可观测性平台或大而全运维平台

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取
- 没有主线切换、优先级变化或新的关键事实，不改本页
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页
- 后续多端功能与 UI 治理细节统一写入 [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
