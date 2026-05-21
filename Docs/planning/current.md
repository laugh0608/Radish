# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-7-C 近期开发任务重评估与下一批任务选择`
- **复核日期**：`2026-05-21`
- **最近结论**：
  - `P3-1` 至 `P3-5` 已完成公开内容增长、PublicId 试点、留存回流、动态 sitemap 与详情 head snapshot 首批闭环
  - `P3-6` 公开增长部署观察已收口，本地 Gateway 与生产公开域名 `https://radishx.com` 的 public head smoke 均通过，转入维护线
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访小闭环和高信号候选筛查，当前未发现新的 `P0/P1`
  - 原 `P3-7` 收尾口径偏向维护观察，无法给出最近阶段的明确开发入口；当前需要重新评估最近可推进任务，而不是直接切到远期专题
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

1. **找回最近阶段的实际开发入口**
   - 从 `P3-7` 之后的原规划、backlog、未完成功能和近期用户主路径中筛出下一批可开发任务
   - 不把“观察”作为无事可做的结论，也不直接把后续重点提前升级为当前主线
   - 产出最近 `1-2` 个一天级可验证任务，并写清边界、完成条件和验证方式
2. **维护观察降级为并行线**
   - `P3-6 / P3-7` 暴露的新问题仍可回拉小闭环，但不再占用主线
   - 维护线只处理公开访问、head / sitemap、分享预览、运行日志、购买 / 订单 / 背包、权限授权等高信号问题
3. **后续重点保留为路线方向**
   - 公开页面、移动端视图公开页、Flutter 移动客户端、WebOS / PC / Tauri 客户端、Console 等仍有大量功能未适配或未开发完，应纳入后续重点
   - client、console、公开页和各客户端 UI 缺少统一设计与系统化优化，需要后续专题阶段处理
   - UI 专题原则上使用 Pencil 设计稿先行，并在项目中保存和同步 `.pen` 设计源文件；端点拆分后续慎重决定

## 下一顺位

- `P3-7-C 近期开发任务重评估与下一批任务选择`
  - 读取 `current.md`、`development-plan.md`、`phase-two-product-completion.md`、`backlog.md` 和必要的专题文档
  - 输出近期任务候选矩阵，优先看产品主路径、未完成功能、跨端断点、Console 治理效率和验证成本
  - 明确今天或下一批应开发的 `1-2` 个任务，边界必须能在一天级别内验证和提交
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
