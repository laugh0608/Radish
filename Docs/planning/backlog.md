# 未来规划 / Backlog

> 本页只保留 **当前未启动、且已明确后置** 的事项。
>
> 当前正式主线请看：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)

## 当前后置原则

- backlog 中的事项，默认都不是“现在就该做”
- 第二开发阶段当前正式主线已经固定为 `Phase 2-1 社区深化第一批`
- 若某项要从 backlog 回拉为主线，必须先更新 `planning/current.md` 与阶段路线图
- backlog 只保留后置项，不再承担阶段定义职责

## 第二开发阶段后续池

### 社区深化第二批

- `Notification Realtime P2` 更深能力：通知聚合、偏好设置、容量治理
- `Chat App P2`：私聊、消息搜索、Reaction、消息置顶、阅读回执、权限细化
- `P3-ext / P4-ext / P5-ext`：论坛投票 / 问答 / 抽奖更深增强
- 经验 / 等级 / 排行榜的防刷与后台治理完善

### 多端深化后置项

- 移动 Web 的 `SEO / 分享卡片 / 外链打开策略` 深化
- 完整 `PWA / Service Worker / 离线缓存 / Web Push`
- 平板与大屏移动混合形态专门适配
- Flutter Desktop 深化与更多平台扩展

### 标识体系与社区联邦预研

- `ID Architecture`：为核心聚合引入 `InternalId / PublicId / FederationId` 分层，冻结新接口继续暴露 `long` 主键的扩散
- `PublicId Rollout`：优先为 `User / Post / Comment / Attachment / Channel / Notification / WikiDocument` 设计并落地 `PublicId`
- `Snowflake Exit Strategy`：待外部契约完成 `PublicId` 化后，再评估内部 Snowflake 主键向数据库 `sequence / identity` 的迁移窗口
- `Federation Readiness`：为未来联邦预留本地对象 / 远端对象、canonical URI、收发队列、签名与重试边界
- `ActivityPub / WebFinger`：作为未来公开社区联邦首选方向预研，不纳入第二阶段前半程主线
- `Tenant Semantics Convergence`：保持当前多租户实现可用，但逐步把公开社区语义从 `tenant` 收敛到 `instance / node / space`
- 详细口径见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 平台与治理后置项

### `Console-ext Phase 2+`

- 更完整权限中心、审计与共享接口治理
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
