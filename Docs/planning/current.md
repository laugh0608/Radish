# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-9 真实使用主路径产品化与发布候选整备`
- **复核日期**：`2026-06-08`
- **最近结论**：
  - `P3-1` 至 `P3-5` 已完成公开内容增长、PublicId 试点、留存回流、动态 sitemap 与详情 head snapshot 首批建设。
  - `P3-6` 公开增长部署观察已收口，本地 Gateway 与生产公开域名 `https://radishx.com` 的 public head smoke 均通过，转入维护线。
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访和高信号候选筛查，当前未发现新的 `P0/P1`。
  - `P3-7-C` 已完成 WikiApp、ChatApp、ContentModerationService 与 ExperienceService 首批热区治理；剩余经验发放主流程进入后续评审池，不再作为当前默认主线。
  - `P3-8-A / B / C` 已完成多端功能缺口与 UI 设计入口审计、Flutter 公开榜单入口、Console 治理工作台设计端点和 Console 高频页面类型试点；继续筛剩余 CSS / 历史页面已转为低收益维护项。
  - `P3-8-D` 已完成移动 Web 公开视图矩阵、根路径 `/` 切向纯 Web、`/desktop` 保留入口、公开分享 / 来源返回、纯 Web forum / shop 登录回流、Flutter 通知 / 论坛 / 商城 / 资产 / 经验 / 最近访问 / 个人资料、单商品购买、订单 / 背包 / 胡萝卜流水排障、Console returnTo / 权限授权 / LongId 字符串守护等多轮治理。
  - `P3-8-D` 作为默认主线可以阶段收口；继续围绕同一批购买、订单、背包、权限或 ID 守护反复深挖，会进入收益递减，后续只在新缺口、新扫描命中、真实编译错误或发布候选验收暴露问题时回拉。
  - 项目仍处于单人开发期和功能建设期，尚未进入稳定运营期；下一步不直接切 Phase 4，而是在 Phase 3 内进入 `P3-9`，把已打通能力组织成可体验、可演示、可验收的真实使用主路径。
  - `P3-9-A / B / C / D` 已完成首批主路径验收入口、Flutter 登录移动用户、访客公开访问与分享、Console 管理员排障入口整备。
  - `P3-9-E` 已完成发布候选路径自动化总回归；`validate:baseline`、`validate:identity`、client / console / Flutter 定向验证均通过。
  - Flutter 登录态商城 / 钱包路径已完成修复复测；商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口未暴露新增 `P0/P1`。
  - 访客公开访问与 Console 管理员排障路径抽查未暴露新增阻断；当前进入 `PR -> master` 前扩大验证与合并材料准备。

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **推进 P3-9 真实使用主路径产品化**
   - 从“继续找局部契约缺口”转向“按真实用户路径组织产品体验和验收结论”。
   - 首批围绕访客、登录移动用户和管理员三类路径做批次级验收、成组修复和收口结论。
   - 只修阻断主路径、造成跨端回流丢失、破坏 LongId / PublicId 契约、影响登录恢复或影响 Console 排障效率的问题。
2. **把 P3-8-D 降级为维护与回拉线**
   - 移动 Web 公开页逐页打磨、Console 剩余页面迁移、购买 / 订单 / 背包重复复核、ID Phase A 广泛扫描不再作为默认日常主线。
   - 新增外部 ID 边界、扫描命中、真实编译错误或发布候选验收暴露问题时，再做定向治理。
3. **保留必要门禁，放宽过期约束**
   - Flutter 不再把“只能只读”作为长期门禁；成熟 API 支撑的一组同一工作流动作可以进入受控写入批次。
   - 大页面重设计、端点级视觉治理和跨页面视觉体系变更仍先更新 Pencil；小范围功能、状态、表单、文案或行为等价修正不被 Pencil 阻塞。
   - 本地验证继续按风险分层；完整 baseline 默认放在 PR 合并到 `master` 前或发布部署节点。

## 下一顺位

- `P3-9-E PR 前扩大验证与合并准备`
  - 按 [验证基线说明](/guide/validation-baseline) 执行 `PR -> master` 前扩大验证。
  - 整理发布候选主路径自动化证据、人工复核结论、已知风险和合并建议。
  - 若扩大验证暴露 `P0/P1`，只回拉主路径阻断、状态恢复、身份契约、公开链接、跨端回流或 Console 排障效率问题。

## 今日事项

- 第一顺位：执行 `PR -> master` 前扩大验证，优先跑 `validate:baseline`、`validate:identity` 与必要的 client / console / Flutter 定向验证。
- 第二顺位：整理发布候选合并材料，收口自动化证据、人工复核结论、已知风险和合并建议。
- 第三顺位：若扩大验证命中 Flutter 商城 / 钱包、登录恢复、公开链接、LongId / PublicId 或 Console 排障阻断，按同类问题成组修复并补定向测试；不要恢复 P3-8-D 旧路径反复深挖。
- 第四顺位：将 Flutter `发现 / 消息 / 更多 / 我的` 信息架构作为后续产品设计评估项，只先评估跨端任务归属和入口命名，不抢占发布候选合并准备。

## 并行维护项

- 公开 head smoke、动态 sitemap、head snapshot 缓存与 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生产域名配置维护。
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护。
- Console 新增或改动页面继续优先复用 `@radish/ui` 组件、交互反馈和主题 token。

## 当前不做

- 不把维护观察继续当作唯一主线。
- 不等待真实用户反馈出现后才继续开发未完成功能。
- 不继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 不继续把移动 Web 公开页逐页打磨、Console 剩余 CSS / 历史页面筛查作为默认主线。
- 不绕过 Pencil / 设计稿直接大面积改 UI；但小范围功能、状态、表单、文案或行为等价修正不被 Pencil 前置阻塞。
- 不把 Tauri 当作原生 UI 重写路线或 WebOS 默认分发路线。
- 不做“移动版 WebOS”，新功能默认不进入 WebOS。
- 不让 PC 客户端抢占纯 Web / Flutter 主线。
- 不一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史治理。
- 不启动完整 `PublicId` 全量迁移或数据库主键迁移。
- 不启动完整 Playwright / E2E 平台。
- 不启动完整可观测性平台或大而全运维平台。

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取。
- 没有主线切换、优先级变化或新的关键事实，不改本页。
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页。
- `P3-9` 细节统一写入 [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)。
