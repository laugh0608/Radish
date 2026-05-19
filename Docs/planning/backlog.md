# 未来规划 / Backlog

> 本页只保留 **当前未启动、且已明确后置** 的事项。
>
> 当前正式主线请看：
>
> - [当前进行中](/planning/current)
> - [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)

## 当前后置原则

- backlog 中的事项，默认都不是“现在就该做”
- `P3-2 PublicId` 最小试点首批已完成，下一正式主线以 [当前进行中](/planning/current) 为准
- 若某项要从 backlog 回拉为主线，必须先更新 `planning/current.md` 与阶段路线图
- backlog 只保留后置项，不再承担阶段定义职责

## 已回拉到当前规划的事项

以下事项不再作为普通 backlog 处理，已迁入 [第二阶段产品功能补全规划](/planning/phase-two-product-completion) 统一排序：

- 萝卜坑 Phase 5 中影响工作流闭环的支付密码真实 API、快捷跳转、复制反馈、安全日志判断
- Console 与后台治理中的模拟 API、用户详情、萝卜币流水、订单列表、商城管理前端和统计 TODO
- 经验 / 等级治理中的管理员调整、冻结 / 解冻、每日统计、缓存和配置化上限
- 商城权益效果中的双倍经验、帖子置顶、帖子高亮等可购买权益效果复核

以下事项已回拉到 [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)：

- 公开内容增长基础：`P3-1` 首批已完成 `SEO / canonical / robots / sitemap seed / 详情复制链接` 基线；动态 sitemap、结构化数据和 SSR / SSG 后置专题评估
- `PublicId` 最小试点方案：`Post` 首批已完成，不做全量迁移；历史补齐或扩面仍需单独评估
- 代码热区拆分候选：超大公开页面、超大 Service、Flutter 大页面
- 用户留存轻闭环：通知、复访、轻互动与公开分享的回流链路

## 第二开发阶段后续池

### 社区深化第二批

- `Notification Realtime P2` 更深能力：通知聚合、偏好设置、容量治理
- `Chat App P2`：私聊、消息搜索、Reaction、消息置顶、阅读回执、权限细化
- `P3-ext / P4-ext / P5-ext`：论坛投票 / 问答 / 抽奖更深增强
- 经验 / 等级 / 排行榜的更深防刷策略和后台治理增强（基础治理已回拉到当前规划）

### 多端深化后置项

- 完整 `PWA / Service Worker / 离线缓存 / Web Push`
- 平板与大屏移动混合形态专门适配
- Flutter Desktop 深化与更多平台扩展

### 标识体系与社区联邦预研

- `ID Architecture`：为核心聚合引入 `InternalId / PublicId / FederationId` 分层，冻结新接口继续暴露 `long` 主键的扩散
- `PublicId Rollout`：优先为 `User / Post / Comment / Attachment / Channel / Notification / WikiDocument` 设计并落地 `PublicId`；当前只回拉最小试点方案，全量 rollout 仍后置
- `Snowflake Exit Strategy`：待外部契约完成 `PublicId` 化后，再评估内部 Snowflake 主键向数据库 `sequence / identity` 的迁移窗口
- `Federation Readiness`：为未来联邦预留本地对象 / 远端对象、canonical URI、收发队列、签名与重试边界
- `ActivityPub / WebFinger`：作为未来公开社区联邦首选方向预研，不纳入第二阶段前半程主线
- `Tenant Semantics Convergence`：保持当前多租户实现可用，但逐步把公开社区语义从 `tenant` 收敛到 `instance / node / space`
- 详细口径见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 平台与治理后置项

### Redis 与缓存治理专题

- 后置专题入口：[Redis 与缓存治理专题](/planning/redis-cache-governance)
- 当前结论：Redis 已作为部署态默认缓存后端生效，但业务仍应优先依赖 `ICaching` / `IDistributedCache`，不直接散落 Redis 专用调用
- 后续只在多实例、SignalR 跨实例推送、聊天室在线状态、通知未读数原子化、上传限流、商城 / 萝卜币幂等与并发保护、排行榜或热点读模型出现明确需求时回拉
- 不把 `P3-6` 扩成 Redis 平台化专项；每次回拉都必须对应真实问题或明确业务批次

### `Console-ext Phase 2+`

- 更完整权限中心、审计与共享接口治理
- Console 前端后续治理应优先复用 `@radish/ui` 的组件、交互反馈与主题 token，逐步收敛历史页面的自定义样式和重复组件，保持 Console 与 Radish 其他前端入口的视觉一致性
- 不在第二阶段前半程启动

### `Gateway & BFF` 深化

- 暂缓，不作为第二阶段前半程目标
- 只有在多入口边界稳定且出现明确聚合需求时再重新启动

### 开放平台

- 第三方接入、SDK、应用生态开放
- 不在第二阶段前半程启动

### Later

- 邮件通知系统
- 分发增强
- 完整可观测性平台
- 完整 Playwright / E2E 平台

## 维护规则

- 已启动事项迁移到 [当前进行中](/planning/current)
- 已完成事项迁移到 [已完成摘要](/planning/archive) 或 [开发日志](/changelog/)
- 本页后续只做后置池增删，不再扩写阶段说明
