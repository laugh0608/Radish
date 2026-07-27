# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、明日执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-N-A 论坛内容赞赏权威设计文档修订`
- **产品下一顺位**：`按已批准裁决校准首版对象、资产事务、审计投影、通知、治理与停止线`
- **复核日期**：`2026-07-26`
- **正式主线**：纯 Web；PC / mobile 浏览器共同验收。WebOS `/desktop` 仅历史兼容，Flutter 条件维护，Tauri 冻结。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- PR `#65` 已集成七月下旬 Wiki 作者协作、宠物公开名片、内容治理与申诉、用户屏蔽、Wiki 附件及生产迁移编排等成组成果；CI 门禁同时修复真实数据库配置探测、SQLite 连接串解析和依赖安全问题。
- [F4-L Wiki 附件隐私与生命周期权威闭环](/features/wiki-attachment-privacy-lifecycle-design)已完成 A-D 批并关闭：Main 权威引用、Wiki 私有默认、动态 ACL、受保护资源、六身份 Gateway 矩阵及 SQLite / PostgreSQL 验证形成闭环。
- [F4-M 论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)已完成 A-D 批并关闭：Post / Comment Revision、CAS、完整快照、旧历史受权兼容、安全恢复、正式 Web 与多身份 PC / mobile 矩阵均通过。
- F4-M-D 验收修正了版本摘要时间双真相和正式 `/me` 缺少退出入口的共同根因；临时数据残留为 `0`，六库完整性与严格 migration verify 通过。
- F4-N-A 现状审计已完成，设计方向已于 2026-07-26 获得批准；今晚只完成日终文档收口，不进入模型、接口、页面或 migration。

## 明日事项（2026-07-27，F4-N-A）

1. 修订[论坛内容赞赏](/features/forum-content-reward)权威专题，首批只支持 `Post / Comment`；`PostAnswer` 在回答通知定位与内容治理形成完整边界前后置。
2. 首批只保留预设理由，不开放自定义理由文本；后者必须作为具备举报、隐藏和证据链的独立治理增强再评估。
3. 固定 Main 资产真相：`ContentReward`、双方余额、`TIP` 流水、幂等终态和 Reliable Outbox 在专属 Repository 事务中原子提交；不复用需要支付密码的通用转账接口。
4. 将 Log DB 的 `BalanceChangeLog` 明确定义为可靠、幂等的审计投影，不再宣称与 Main 主账跨库同事务；现有通用转账的跨库日志假设保留为独立维护债。
5. 固定发送者 / 接收者账号状态、治理状态和双向屏蔽资格矩阵；通知使用 `ContentRewardReceived + Reaction + ForumPost`，逐笔投递、不聚合并受屏蔽策略抑制。
6. 补齐 API、失败恢复、SQLite / PostgreSQL 并发、正式 Web PC / mobile、A-D 批次、验证矩阵和停止线；完成文档后汇报差异，进入 F4-N-B 代码前再次等待明确批准。

## 当前执行入口

- [开发路线图](/development-plan)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [F4-N 论坛内容赞赏](/features/forum-content-reward)
- [F4-M-D 论坛内容版本成组验收](/records/f4-m-d-forum-content-revision-stage-acceptance-2026-07-26)
- [F4-L-D Wiki 附件成组验收](/records/f4-l-d-wiki-attachment-stage-acceptance-2026-07-26)
- [2026-07-26 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-26)
- [验证基线说明](/guide/validation-baseline)

## 并行维护线

- 接收明确的 `P0/P1` 生产故障、用户反馈、安全、依赖、迁移和部署问题；P2/P3 按同类问题成组处理。
- 公开 head、动态 sitemap、生产域名、镜像漏洞门禁和多实例附件基础设施按真实触达范围维护，不与 F4-N 主专题并行扩张。
- WebOS 只处理阻断级兼容；Flutter 只维护既有 MVP 的阻断、安全和认证兼容。
- 主动生产使用数据采集继续冻结到计划内功能完成、没有明确维护任务且用户确认的最终收尾阶段。

## 当前不做

- 不提前实现 F4-N 模型、API、migration、Pencil 或页面。
- 不把 `BalanceChangeLog` 跨库写入包装成分布式事务，也不顺手重构现有通用转账。
- 不在首批扩入 `PostAnswer`、自定义理由、排行榜、自定义金额、重复赞赏或独立赞赏中心。
- 不解冻 Tauri，不扩展 Flutter / WebOS 新功能，不重启主动生产证据采集。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后再统一集成。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 smoke 只在专题或成组功能准备验收时执行；启动服务前必须取得当前任务授权。
- 本页不保存历史流水；完成事实进入 records、changelog 或 archive。
