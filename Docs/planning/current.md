# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、明日执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-P-A 权威设计已完成；等待批准进入 B 批服务端与 migration`
- **产品下一顺位**：`以私有帖子收藏补齐“阅读 -> 保存 -> 回访”，先落权威关系与幂等写入`
- **复核日期**：`2026-07-28`
- **正式主线**：纯 Web；PC / mobile 浏览器共同验收。WebOS `/desktop` 仅历史兼容，Flutter 条件维护，Tauri 冻结。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- PR `#65` 已集成七月下旬 Wiki 作者协作、宠物公开名片、内容治理与申诉、用户屏蔽、Wiki 附件及生产迁移编排等成组成果；CI 门禁同时修复真实数据库配置探测、SQLite 连接串解析和依赖安全问题。
- [F4-L Wiki 附件隐私与生命周期权威闭环](/features/wiki-attachment-privacy-lifecycle-design)已完成 A-D 批并关闭：Main 权威引用、Wiki 私有默认、动态 ACL、受保护资源、六身份 Gateway 矩阵及 SQLite / PostgreSQL 验证形成闭环。
- [F4-M 论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)已完成 A-D 批并关闭：Post / Comment Revision、CAS、完整快照、旧历史受权兼容、安全恢复、正式 Web 与多身份 PC / mobile 矩阵均通过。
- F4-M-D 验收修正了版本摘要时间双真相和正式 `/me` 缺少退出入口的共同根因；临时数据残留为 `0`，六库完整性与严格 migration verify 通过。
- [F4-N-D 成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)已通过并关闭专题：Post / Comment 登录回流、资产守恒、Outbox、Log 双分录、通知定位及 PC / mobile 代表矩阵形成闭环。
- D 批修正了 `reward` 登录返回意图缺失和 Reliable Task camelCase payload 反序列化契约根因；测试数据、运行设置、浏览器、服务和备份均已清理，六库严格 migration verify 通过。
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)已完成 A-D 批并关闭：PublicId、服务端分页、独立 Revision、附件、CAS、采纳事件、治理申诉、可靠通知、正式 Web 与 strict migration 形成闭环。
- [F4-O-D 成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)已通过：匿名、问题作者兼管理员、回答作者的 PC / mobile 代表矩阵覆盖创建、编辑、恢复、采纳与撤销；其余权限、失败和治理边界由自动化回归覆盖。
- D 批修正 migration 前置校验加载未来字段、正式 Web 新旧回答区重复渲染两项共同根因；临时帖子、回答、Revision、事件、通知、Outbox、浏览历史和经验副作用均已清理，六库完整性与严格 verify 通过。
- 当前机器未配置 PostgreSQL 集成测试环境，相关条件用例保持显式跳过，不把 SQLite 结果表述为 PostgreSQL 实跑。
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)已完成 A 批候选审计与权威设计：新增私有 Bookmark 关系作为 Main 唯一真相，`CollectCount` 只作可重建投影，写入使用显式目标状态。
- F4-P 固定帖子详情收藏与 `/me/content?tab=bookmarks` 两条正式 Web 路径；收藏不通知作者、不发奖励、不公开收藏者，也不扩收藏夹、推荐或跨对象收藏。

## 下一事项（F4-P-B，等待批准）

1. 新增 `UserPostBookmark`、专属 Repository / Service、显式状态写入、个人分页与不可用目标移除。
2. 增加 Main migration 和 strict verify，覆盖 PublicId、唯一关系、租户、孤立目标与 `CollectCount` 投影一致性。
3. 接入 `@radish/http` 并完成服务端、HTTP、SQLite 与 PostgreSQL 条件回归；B 批不提前修改正式页面。

## 当前执行入口

- [开发路线图](/development-plan)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [F4-N 论坛内容赞赏](/features/forum-content-reward)
- [F4-N-D 论坛内容赞赏成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)
- [F4-O-D 论坛回答生命周期成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)
- [2026-07-27 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-27)
- [F4-M-D 论坛内容版本成组验收](/records/f4-m-d-forum-content-revision-stage-acceptance-2026-07-26)
- [F4-L-D Wiki 附件成组验收](/records/f4-l-d-wiki-attachment-stage-acceptance-2026-07-26)
- [2026-07-26 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-26)
- [验证基线说明](/guide/validation-baseline)

## 并行维护线

- 接收明确的 `P0/P1` 生产故障、用户反馈、安全、依赖、迁移和部署问题；P2/P3 按同类问题成组处理。
- 公开 head、动态 sitemap、生产域名、镜像漏洞门禁和多实例附件基础设施按真实触达范围维护，不与 F4-P 主专题并行扩张。
- WebOS 只处理阻断级兼容；Flutter 只维护既有 MVP 的阻断、安全和认证兼容。
- 主动生产使用数据采集继续冻结到计划内功能完成、没有明确维护任务且用户确认的最终收尾阶段。

## 当前不做

- 不因 F4-N 关闭而扩入 `PostAnswer`、自定义理由、排行榜、自定义金额、重复赞赏或独立赞赏中心。
- 不把 F4-O 扩成回答投票、复杂排序、悬赏、萝卜币、独立问答 App 或全量 PublicId 迁移。
- 未取得 B 批明确批准前不进入代码；不把 F4-P 扩成收藏夹、推荐、公开收藏主页、跨对象收藏或通知奖励。
- 不解冻 Tauri，不扩展 Flutter / WebOS 新功能，不重启主动生产证据采集。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后再统一集成。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 smoke 只在专题或成组功能准备验收时执行；启动服务前必须取得当前任务授权。
- 本页不保存历史流水；完成事实进入 records、changelog 或 archive。
