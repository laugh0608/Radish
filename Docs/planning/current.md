# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-10 Web-first 首页信息流与关键契约开发准备`
- **复核日期**：`2026-06-09`
- **最近结论**：
  - `P3-1` 至 `P3-5` 已完成公开内容增长、PublicId 试点、留存回流、动态 sitemap 与详情 head snapshot 首批建设。
  - `P3-6` 公开增长部署观察已收口，本地 Gateway 与生产公开域名 `https://radishx.com` 的 public head smoke 均通过，转入维护线。
  - `P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访和高信号候选筛查，当前未发现新的 `P0/P1`。
  - `P3-7-C` 已完成 WikiApp、ChatApp、ContentModerationService 与 ExperienceService 首批热区治理；剩余经验发放主流程进入后续评审池，不再作为当前默认主线。
  - `P3-8-A / B / C` 已完成多端功能缺口与 UI 设计入口审计、Flutter 公开榜单入口、Console 治理工作台设计端点和 Console 高频页面类型试点；继续筛剩余 CSS / 历史页面已转为低收益维护项。
  - `P3-8-D` 已完成移动 Web 公开视图矩阵、根路径 `/` 切向纯 Web、`/desktop` 保留入口、公开分享 / 来源返回、纯 Web forum / shop 登录回流、Flutter 通知 / 论坛 / 商城 / 资产 / 经验 / 最近访问 / 个人资料、单商品购买、订单 / 背包 / 胡萝卜流水排障、Console returnTo / 权限授权 / LongId 字符串守护等多轮治理。
  - `P3-8-D` 作为默认主线可以阶段收口；继续围绕同一批购买、订单、背包、权限或 ID 守护反复深挖，会进入收益递减，后续只在新缺口、新扫描命中、真实编译错误或发布候选验收暴露问题时回拉。
  - 项目仍处于单人开发期和功能建设期，尚未进入稳定运营期；本轮跳过发布后不直接切 Phase 4，而是在 Phase 3 内进入 `P3-10`，继续梳理跨端信息架构、功能缺口和 UI 开发优先级。
  - `P3-9-A / B / C / D` 已完成首批主路径验收入口、Flutter 登录移动用户、访客公开访问与分享、Console 管理员排障入口整备。
  - `P3-9-E` 已完成发布候选路径自动化总回归；`validate:baseline`、`validate:identity`、client / console / Flutter 定向验证均通过。
  - Flutter 登录态商城 / 钱包路径已完成修复复测；商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口未暴露新增 `P0/P1`。
  - 访客公开访问与 Console 管理员排障路径抽查未暴露新增阻断；`dev -> master` PR #54 已合并，`Repo Quality` 四项检查均通过。
  - P3-9 合并后本地验证已完成：`validate:ci` 通过；因规划入口命中默认执行面 / 门禁资产，已补 `validate:identity` 并通过。
  - 本轮明确跳过发布：不创建 tag，不等待镜像，不进入 M15 测试 / 生产部署流程；P3-9 转入维护与回拉线。
  - 当前继续第三开发阶段，不进入生产稳定运营；下一主线为 `P3-10`，围绕 Web 默认入口、信息流 / 个人圈子边界、PublicId 分批、评论互动治理、UI 改造和历史功能规划回拉选择下一批开发范围。
  - Flutter 先从当前第一顺位后移，后续在 Web 信息架构与 API 契约稳定后承接移动原生复访、通知、消息和轻互动。
  - `P3-10-A` 已完成初版任务归属、历史功能规划回拉和源码复核。
  - `P3-10-B1 / B3` 首批代码已完成：`/discover` 已调整为 Web-first 公开信息流，User PublicId 公开主页 / 榜单契约已落地并通过 PC 视角浏览器复核。
  - `P3-10-B4 / B5` 首批代码已完成：新增评论实时 Hub、Web / 公开详情订阅、typing 提示、评论树事件合并、神评 / 沙发稳定窗口、并列展示与奖励幂等；`dotnet build`、`dotnet test Radish.Api.Tests`、评论定向测试和 `radish.client` 构建均已通过。
  - `P3-10-B2` 首批代码已完成：`/circle` 作为登录态个人圈子入口落地，发现 / 论坛 / 圈子职责分工已写入 P3-10 专题，关系链用户项补 `VoPublicId` 并支持 `/u/usr_...` 公开主页入口。

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)
- [Radish 电子宠物开发计划](/features/radish-pet-roadmap)
- [个人圈子](/features/circle)
- [P3-9 真实使用主路径产品化与发布候选整备](/planning/p3-9-real-usage-release-candidate)
- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **收口 P3-10-B / C Web-first 首批代码**
   - 第一组开发入口为 Web 首页信息流、用户 PublicId / 公开主页契约、评论实时 / 神评稳定性。
   - 评论实时与神评稳定性已进入本地验证通过状态，后续优先做双用户人工联调和发布候选前批次级回归。
   - Token 不活跃过期、Web UI 改造和 Radish 电子宠物继续作为同阶段候选，不抢占已完成的 Web 信息架构首批入口。
   - 只把能改善真实使用路径、跨端一致性、登录恢复、对象标识契约、评论互动或治理效率的问题纳入当前主线。
2. **把 P3-8-D 降级为维护与回拉线**
   - 移动 Web 公开页逐页打磨、Console 剩余页面迁移、购买 / 订单 / 背包重复复核、ID Phase A 广泛扫描不再作为默认日常主线。
   - 新增外部 ID 边界、扫描命中、真实编译错误或发布候选验收暴露问题时，再做定向治理。
3. **保留必要门禁，放宽过期约束**
   - Flutter 不再作为本批第一顺位；成熟 API 支撑的一组同一工作流动作可以在后续跨端承接批次进入受控写入。
   - 大页面重设计、端点级视觉治理和跨页面视觉体系变更仍先更新 Pencil；小范围功能、状态、表单、文案或行为等价修正不被 Pencil 阻塞。
   - 本地验证继续按风险分层；完整 baseline 默认放在准备合并到 `master` 前或发布部署节点。

## 下一顺位

- `P3-10-B4 / B5 评论实时与神评稳定性回归`
  - 使用 Gateway + Web 详情页做双用户联调，覆盖评论创建 / 更新 / 删除 / 点赞 / typing / 神评变化。
  - 关注 SignalR 断线恢复、匿名详情页订阅、登录态 token 续接和奖励幂等旧数据兼容。
- `P3-10-B6 Token 不活跃过期`
  - 在评论互动治理方案稳定后推进，避免会话治理打断当前 Web 产品形态建设。
- `P3-10-B2 个人圈子维护与回归`
  - 继续按 [个人圈子](/features/circle) 中的 `/circle` 登录态私域、`/forum` 讨论对象权威归属、`/discover` 公开分发面分工复核。
  - 如后续扩展关系链，只补真实复访需要的关注动态和用户跳转，不直接实现 ActivityPub / WebFinger、推荐算法、短动态或转发 / 引用。
- `P3-10-B1 / B3 维护与回归`
  - 将 `/discover` 从公开导航聚合页调整为可持续浏览的内容流方案。
  - 继续保留公开 head、分享、移动 / PC 布局、LongId 兼容读取和 PublicId 分享路由回归。

## 明日事项

- 第一顺位：围绕 `P3-10-B4 / B5` 做双用户主路径联调，优先覆盖 Gateway 下的 Web 详情页、公开详情页匿名订阅、登录用户评论创建 / 更新 / 删除 / 点赞 / typing / 神评变化。
- 第二顺位：联调暴露问题时，按事件契约、Hub 入组 / 重连、登录态 token 续接、前端评论树合并和奖励幂等边界成组修复，并执行对应精准验证。
- 第三顺位：进入 `P3-10-B6` Token 不活跃过期方案前，整理前端活跃记录、refresh 校验、退出登录 UX 与 Hub 连接恢复的交互边界。
- 第四顺位：保留 `P3-10-B2` 圈子回归入口，复核 `/circle` 登录回流、关注动态、关注 / 粉丝列表、`/u/usr_...` 跳转和论坛详情跳转。
- 第五顺位：保留 `P3-10-B1 / B3` 回归入口，必要时复核 `/discover`、`/leaderboard`、`/u/usr_...`、公开 head 与 `DbMigrate apply` 后的旧库补列结果；不要回到 P3-8-D 购买 / 订单 / 背包或 Console 低频页面筛查作为默认主线。

## 并行维护项

- 公开 head smoke、动态 sitemap、head snapshot 缓存与 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生产域名配置维护。
- `M14` 宿主运行与最小可观测性基线、`M15` 最小交付与部署基线。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 保留入口、窗口几何记忆、主题切换、聊天室、通知中心、商城等既有能力稳定维护。
- Console 新增或改动页面继续优先复用 `@radish/ui` 组件、交互反馈和主题 token。

## 当前不做

- 不把维护观察继续当作唯一主线。
- 不等待真实用户反馈出现后才继续开发未完成功能。
- 不创建本轮发布 tag，不进入 M15 测试 / 生产部署流程。
- 不把跳过发布误判为进入生产稳定运营。
- 不继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 不继续把移动 Web 公开页逐页打磨、Console 剩余 CSS / 历史页面筛查作为默认主线。
- 不绕过 Pencil / 设计稿直接大面积改 UI；但小范围功能、状态、表单、文案或行为等价修正不被 Pencil 前置阻塞。
- 不把 Tauri 当作原生 UI 重写路线或 WebOS 默认分发路线。
- 不做“移动版 WebOS”，新功能默认不进入 WebOS。
- 不让 PC 客户端抢占纯 Web / Flutter 主线。
- 不让 Flutter 抢占当前 Web 默认入口、信息流、ID 契约和评论互动治理主线。
- 不把首页瀑布流、个人圈子、推荐算法和联邦社交一次性混成同一个开发任务。
- 不一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史治理。
- 不启动完整 `PublicId` 全量迁移或数据库主键迁移。
- 不启动完整 Playwright / E2E 平台。
- 不启动完整可观测性平台或大而全运维平台。

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取。
- 没有主线切换、优先级变化或新的关键事实，不改本页。
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页。
- `P3-10` 细节统一写入 [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)。
