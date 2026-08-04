# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、当前执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-R C-1B 的 R1-P01“社区发现 / PC 1440”已确认为 Public 内容流视觉基准；下一轮按该基准独立设计 mobile`
- **产品下一顺位**：`完成 R1-P01 mobile 任务重排，并为公共频道、成员公开动态和跨帖神评设计统一公开读模型、公开资格与隐私边界`
- **复核日期**：`2026-08-04`
- **正式主线**：Web 优先；PC / mobile 浏览器共同验收。Flutter 是次级移动原生产品线，WebOS `/desktop` 仅历史兼容，Tauri 暂时弃用并等待未来重新评估。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- `2026-08-04` 已完成 [R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)，并确认 `R1-P01 / 社区发现 / PC 1440` 为首个正式 Public 内容流视觉基准：采用现代自然紧凑表面、非对称主次、连续扫描行、Geist 无衬线、`10–16px` 克制圆角、极轻阴影和内嵌数据反馈；灰玉 `#5d6c57`、墨蓝 `#435c74` 与国风暖白 `#f4efe6 / #fbf7f0` 形成 Radish 家族色。
- `2026-08-04` “社区正在发生”在统一扫描轴中并置公共频道话题、成员公开动态、跨帖神评、帖子和问答；帖子 / 问答可复用既有能力，前三类进入代码前必须新增统一公开读模型、公开资格、隐私、稳定排序和分页契约，设计稿不构成接口已存在的事实。
- `2026-08-04` 已按最终裁决清理活动设计源：旧“社区脉搏”PC / mobile、全部参考试验与失败稿均已删除，当前只保留唯一 `1440 × 900` 正式 PC 代表页、`8` 个必要组件母版和主题变量；最终画板无裁切、溢出或失效引用。mobile 尚未形成正式稿，下一轮必须按移动主任务独立重排，不能缩放 PC 或机械堆叠右栏。
- `2026-08-03` 已创建唯一活动设计源 `radish-web-family-ui-v1.pen`：文件名以主版本管理，普通迭代继续更新 `v1` 并由 Git 留存；原四个大型 `.pen` 不删除、不改名，统一转为只读留档。
- `2026-08-03` `R1-P01` 两轮业务页面评审均未通过。第二轮虽分开了分类、标签和结构化状态，也增加了分类、讨论、社区动态与相关内容区，但绝大多数视觉篇幅仍由帖子占据，全宽分类浏览笨重且偏离参考图的异构工作台节奏；该稿只由 Git 留档，不是实现基线。
- `2026-08-03` 下一轮不继续在现有构图上叠加模块：先重新拆解参考 `13 / 16 / 18 / 27` 的非对称结构、模块尺度、密度和多实体并置方式，再按当前代码审计可公开读取的帖子、问答 / 投票、Docs、商品、榜单、作者、圈子等能力，形成低保真结构备选。
- `2026-08-03` 已把 `guofeng` 品牌目标从旧胭脂改为低饱和灰玉 `#5d6c57`，悬停 `#6e736d`，常规操作继续使用墨蓝 `#435c74`；设计源已更新，当前运行时旧值待下一实现批替换。
- `2026-08-03` 已完成 `R1-F01`：统一四主题变量矩阵、Brand / Workbench Action 前景语义、按钮 / 输入 / 状态 chip、加载 / 空态 / 错误 / 权限状态槽，以及 PC / 390px mobile 壳层契约。
- `2026-08-03` 已将 F4-R 上游基线升级为 RadishX `family-ui v26.7.3`：通用副本使用灰玉参考默认并新增 `text-on-brand`；基线补充批当时显式保留 `guofeng` 胭脂品牌，后续 R1-P01 灰玉裁决不改写该历史事实。
- `2026-08-03` 已复核新版全部规范正文、token 和参考索引；27 张参考图仅迁入 family-ui `reference-ui/`，Git blob 全部 `R100` 相同，既有页面族映射继续有效，并强化“观察、拆解、提炼、转译”与禁止照抄边界。
- `2026-07-30` 已将 Pencil 协作调整为代表页驱动：功能、文案、权限和状态机服从专题与代码；R1 / R2 维护必要代表设计，R3 继承实现并通过真实页面截图复核，不再维持逐路由设计镜像。
- `2026-07-30` 已完成 C-1A 代码事实审计：裁决 `7` 个 R1、`4` 个 R2 与 R3 继承表；Console 普通表格 / 明细和案件治理 / 审计因布局、动作与 mobile 模型不同，拆为两个完整代表类型。
- `2026-07-30` 已完成并关闭 [F4-S 公开排行榜参与资格、隐私边界与可信度治理](/features/leaderboard)：公开类型固定为经验、发帖、评论、人气、热门商品；余额、累计消费与购买数量退出匿名排名；列表、总数和个人排名统一资格与稳定排序，读取不再补写公开身份。D 批通过 Gateway PC / mobile、匿名 / TestUser、旧路由和失败契约矩阵，并修正未知整数类型被框架枚举绑定提前截断的问题。
- `2026-07-30` 已完成镜像漏洞门禁分层：`CRITICAL` 与存在修复版本的 `HIGH` 默认阻断，无修复 `HIGH` 留痕但不阻断；原始报告、策略裁决和限期精确例外均可追溯。
- 多端顺位调整为 Web 优先、Flutter 次级；Tauri 暂时弃用，保留历史验证资产但不进入开发、UI、CI、发布或验收门禁，未来只在桌面原生价值、目标用户和维护预算明确时重新评估。
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
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)已完成 A-D 批并关闭：Main 私有关系、显式幂等状态、Post 锁序、稳定分页、不可用目标、正式 Web 与代表运行态矩阵形成闭环。
- [F4-P-D 成组验收](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29)覆盖匿名、普通收藏者、作者和第三方读者，以及 PC / mobile、`zh / en`、代表主题、同状态重试、多标签并发和无跨域副作用边界。
- D 批修正个人内容来源被压缩到 `/me` 的统一导航契约根因；公开详情现携带完整 `MeRoute`，可精确返回原收藏标签与页码。临时数据和运行状态已清理，六库完整性与 strict migration verify 通过。
- [F4-Q 论坛标签公开发现、可见性与 SEO 闭环](/features/forum-tag-public-discovery-seo-design)已完成 A-D 批并关闭：公开帖子判定、数据库侧公开计数 / 热门 / 相关聚合、标签首包 head、tags sitemap、正式 Web 相关主题和运行态矩阵形成闭环。
- [F4-Q-D 成组验收](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29)覆盖匿名、普通登录用户和 Console 管理员，以及 PC / mobile、`zh / en`、`default / guofeng` 代表路径；热门进入、相关切换、禁用 / 删除 / 恢复、`GET / HEAD`、canonical、JSON-LD 和 sitemap 均已通过。
- D 批按共同根因修正 tags sitemap 分片路由、首包 / runtime JSON-LD 单一脚本、英文数量复数、不可用标签 `noindex`、Console 软删除列表和恢复预检契约；临时标签、PostTag、审计与访问计数已清理，六库完整性及 strict migration verify 通过。

## 今日进展（2026-08-04，R1-P01 PC 视觉基准收口）

1. 按 family-ui 参考 `13 / 16 / 18 / 27` 完成结构转译，固定吸收非对称主次、异构尺度、连续工作面和紧凑工具区，并排除后台侧栏、外部业务、presence 与同尺寸卡片墙。
2. 只读审计现有公开实体、API、路由、权限和失败边界，确认问答 / 投票、少量讨论、标签、Docs、商品及公开榜单可直接组合；独立作者推荐、公开圈子、跨实体时间流和个性化必须先有新读模型。
3. 在 `radish-web-family-ui-v1.pen` 完成三个 PC 低保真候选；用户已选择“社区脉搏”，并按三轮反馈将其重校准为 `1440px` 高密度桌面布局。主区使用双栏问题脉搏和 `5` 条多列讨论，右侧保留 `5` 位贡献者、Docs / 商品和 `5` 个主题。
4. 获选结构经过现代自然视觉重校准、家族色校准和混合信息流复核后，最终定名为 `R1-P01 / 社区发现 / PC 1440` 并获用户确认；活动设计源已删除旧 PC / mobile 与全部失败稿，只保留唯一正式 PC、`8` 个必要组件母版和主题变量。
5. UI 长期规范已新增“现代自然紧凑基准”：后续页面继承表面、密度、排版、用色、动作和数据表达语法，但不照搬社区页的信息架构；标题允许一行不可被标题替代的真实任务 / 内容范围说明，禁止纯解释或氛围文案。
6. Pencil 节点布局检查无裁切、溢出或失效引用，最终文件已保存并完成 `2x` PNG 导出；本轮不改运行时代码、不启动服务、不执行真实 smoke，也不提前进入 `R1-P02`。

## 当前下一步（下一开发日）

1. 以已确认的 `R1-P01 / 社区发现 / PC 1440` 为继承基准，独立设计 `390px` mobile：先固定搜索、真实内容、参与动作与洞察模块的任务顺序，不缩放 PC，也不把右侧洞察区机械堆到末尾。
2. 为公共频道摘要、成员公开动态和跨帖神评设计统一公开读模型，明确公开资格、隐私、治理、稳定排序、游标分页与缓存边界；帖子和问答继续复用现有公开契约。
3. PC / mobile 代表画板共同通过后，再规划 `/discover`、`publicSeoStatic.test.ts`、locale 文案和灰玉运行时 token 的成组实现与定向验证。
4. mobile 与读模型边界未收口前不启动服务、不执行真实 smoke、不改运行时代码，也不提前扩张 `R1-P02`。

## 当前执行入口

- [开发路线图](/development-plan)
- [R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)
- [2026-08-04 日终设计收口与文档审阅](/records/f4-day-end-doc-review-2026-08-04)
- [2026-08-03 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-03)
- [2026-07-30 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-30)
- [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)
- [F4-R C-1 代表页代码事实审计](/frontend/f4-r-representative-page-audit)
- [Radish UI 差异附录](/frontend/ui-addendum)
- [F4-S 公开排行榜参与资格、隐私边界与可信度治理](/features/leaderboard)
- [F4-S-D 公开排行榜治理成组验收](/records/f4-s-d-leaderboard-public-governance-stage-acceptance-2026-07-30)
- [F4-S 公开排行榜治理代码侧验收](/records/f4-s-leaderboard-public-governance-code-acceptance-2026-07-30)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [F4-N 论坛内容赞赏](/features/forum-content-reward)
- [F4-N-D 论坛内容赞赏成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)
- [F4-O-D 论坛回答生命周期成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)
- [F4-P-D 论坛帖子收藏成组验收](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29)
- [F4-Q 论坛标签公开发现、可见性与 SEO 闭环](/features/forum-tag-public-discovery-seo-design)
- [F4-Q-D 论坛标签公开发现成组验收](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29)
- [2026-07-29 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-29)
- [2026-07-28 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-28)
- [2026-07-27 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-27)
- [F4-M-D 论坛内容版本成组验收](/records/f4-m-d-forum-content-revision-stage-acceptance-2026-07-26)
- [F4-L-D Wiki 附件成组验收](/records/f4-l-d-wiki-attachment-stage-acceptance-2026-07-26)
- [2026-07-26 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-26)
- [验证基线说明](/guide/validation-baseline)
- [镜像漏洞门禁分层](/guide/image-vulnerability-gate)

## 并行维护线

- 接收明确的 `P0/P1` 生产故障、用户反馈、安全、依赖、迁移和部署问题；P2/P3 按同类问题成组处理。
- 公开 head、动态 sitemap、生产域名、镜像漏洞门禁和多实例附件基础设施按真实触达范围维护，不与 F4-R 候选审计并行扩张。
- WebOS 只处理阻断级兼容；Flutter 按 Web 优先顺位承接明确高价值移动原生路径，不机械追平 Web。
- 主动生产使用数据采集继续冻结到计划内功能完成、没有明确维护任务且用户确认的最终收尾阶段。

## 当前不做

- 不因 F4-N 关闭而扩入 `PostAnswer`、自定义理由、自定义金额、重复赞赏或独立赞赏中心。
- 不把 F4-O 扩成回答投票、复杂排序、悬赏、萝卜币、独立问答 App 或全量 PublicId 迁移。
- F4-Q 已关闭，不回拉标签关注、个性化推荐、标签首页、SSR / SSG 或公开个人页 sitemap；F4-R 各页面族在对应 R1 / R2 复核前不提前实施其视觉改造。
- 不恢复 Tauri，不扩展 WebOS 新功能，不把 Flutter 做成 Web 的机械复制，也不重启主动生产证据采集。
- 不继续修改历史 `.pen` 留档，不为路由、主题、文案或等价状态复制完整画板；任何后续 `.pen` 修改仍需当前任务的明确授权。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后再统一集成。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 smoke 只在专题或成组功能准备验收时执行；启动服务前必须取得当前任务授权。
- 本页不保存历史流水；完成事实进入 records、changelog 或 archive。
