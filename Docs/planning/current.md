# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、当前执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-R C-1B 的 R1-P02 已确认完整能力边界；下一步基于既有家族 UI 基线重整普通帖子 PC 低保真方案`
- **产品下一顺位**：`评审 R1-P02 唯一 PC 方向；确认后再进入 Mobile 画板与代码实现`
- **复核日期**：`2026-08-06`
- **正式主线**：Web 优先；PC / mobile 浏览器共同验收。Flutter 是次级移动原生产品线，WebOS `/desktop` 仅历史兼容，Tauri 暂时弃用并等待未来重新评估。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- `2026-08-06` [正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)的四项推进决策已确认：正式帖子详情承接点赞、reaction 与回帖的回帖；作者删除、投票和抽奖按作者 / 类型状态保留；基础资料、头像和显示时区进入正式 Private Web；转账、资产安全和统计脱离 F4-R，后续单独裁决迁移或退役。R1-P02 能力审计冻结解除，但仍须先通过 PC 代表设计评审。
- `2026-08-06` R1-P02 多轮评审暴露出跨页面族风险：现有代表页审计能够判断布局和视觉复用，但没有证明正式 Web 已承接项目现有能力。R1-P02 Pencil 暂停继续修补，改为先建立正式 Web、WebOS 历史实现、既有专题与 API / Service 的能力覆盖矩阵；不把“当前 Public 未接入”自动固化为长期产品边界。
- `2026-08-06` 已确认长期视觉演进原则：F4-R 当前批次负责建立并落地新的 Radish 家族 UI 基线；本批完成后的视觉重构默认根据最新 family-ui、项目差异附录、已确认代表页和正式代码进行优化与更新，不从零重新设计。只有全新产品形态、现有信息架构无法承载目标任务或功能边界发生结构性变化并获裁决时，才重新建立页面范式。
- `2026-08-05` 已完成 [R1-P02 Public 详情与互动代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)：现有 API、权限与写入边界保持不变；代表设计固定为普通帖子登录读者的 PC 1440 / Mobile 390 完整画板，加问答、身份回流和既有浮层关键状态。当前结构债集中在参与入口重复、解释型侧栏、卡片套卡片、mobile 辅助区长尾，以及 `PublicForumDetail.tsx` 达 `2292` 行；Pencil 方向确认前不进入代码。
- `2026-08-05` [R1-P01 公开发现成组实现与运行态验收](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)已关闭：migration 与 host runtime 通过；Gateway 匿名 / 种子管理员登录回流、PC `1440 × 1000`、mobile `390 × 844`、`zh / en`、`default / guofeng`、真实链接和 Console 治理只读路径通过，页面无横向溢出。
- `2026-08-05` 运行态修正统一 JSON `long` 字符串契约与前端 `number` 假设不一致的根因；公开计数现使用字符串 wire contract、`BigInt` locale 格式化和独立复数判别量，不再回显 i18n 资源键。
- `2026-08-05` 已完成并确认 [R1-P01 mobile 与统一公开读模型设计](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05)：`390px` 代表页将搜索和真实内容前置，把参与动作放入精选讨论，并把社区脉搏、贡献者上下文与知识主题分别嵌入首屏、中段和流后；首轮反馈后，混合流已从异色列表拼接重构为焦点事件、连续编号轨道和嵌入式贡献者节点。画板无裁切、溢出或失效图标，`2x` 视觉自检通过，用户已确认。
- `2026-08-05` 已完成 [R1-P01 公开发现成组实现](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)：Channel 默认 Hidden、migration、版本化 opt-in、append-only 审计、Console `view / manage` 权限和精确租户治理形成闭环；既有 Public 频道不会自动进入匿名流。
- `2026-08-05` “社区正在发生”已落地为统一只读投影：公开 Wiki 首次发布、当前跨帖神评、帖子、问答与显式开放的频道摘要由服务端按 snapshot cutoff 和稳定 keyset 游标合并，响应 `no-store` 且整流失败关闭；`@radish/http` 与 `/discover` 只消费该投影，不再拼接旧列表接口。
- `2026-08-05` `/discover` 已按获批设计实现首条焦点事件、连续编号轨道、嵌入式贡献者、社区脉搏和 PC 非对称洞察区；Client 全量 `489`、Console 全量 `61`、后端专题 `57` 项测试及前后端 production build 通过。PostgreSQL 条件用例仍因本机未配置而显式跳过，不把 SQLite migration 与 Gateway 运行态表述为 PostgreSQL 实跑。
- `2026-08-04` 已完成 [R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)，并确认 `R1-P01 / 社区发现 / PC 1440` 为首个正式 Public 内容流视觉基准：采用现代自然紧凑表面、非对称主次、连续扫描行、Geist 无衬线、`10–16px` 克制圆角、极轻阴影和内嵌数据反馈；灰玉 `#5d6c57`、墨蓝 `#435c74` 与国风暖白 `#f4efe6 / #fbf7f0` 形成 Radish 家族色。
- `2026-08-04` 已按最终裁决清理活动设计源：旧“社区脉搏”PC / mobile、全部参考试验与失败稿均已删除；当前活动源保留已确认的 `1440 × 900` PC、`390px` mobile、`8` 个必要组件母版和主题变量，失败稿继续只由 Git 追溯。
- `2026-08-03` 已创建唯一活动设计源 `radish-web-family-ui-v1.pen`：文件名以主版本管理，普通迭代继续更新 `v1` 并由 Git 留存；原四个大型 `.pen` 不删除、不改名，统一转为只读留档。
- `2026-08-03` `R1-P01` 两轮业务页面评审均未通过。第二轮虽分开了分类、标签和结构化状态，也增加了分类、讨论、社区动态与相关内容区，但绝大多数视觉篇幅仍由帖子占据，全宽分类浏览笨重且偏离参考图的异构工作台节奏；该稿只由 Git 留档，不是实现基线。
- `2026-08-03` 已把 `guofeng` 品牌目标从旧胭脂改为低饱和灰玉 `#5d6c57`，悬停 `#6e736d`，常规操作继续使用墨蓝 `#435c74`；R1-P01 页面使用现有灰玉 / 墨蓝语义 token，全局品牌 token 因影响全部页面族留待共享主题批成组治理。
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

## 今日进展（2026-08-05，R1-P01 闭环与 R1-P02 审计）

1. 在已确认 PC 基准上完成并确认 `R1-P01 / 社区发现 / Mobile 390`，首轮反馈后把异色列表拼接收敛为焦点事件、连续编号轨道和嵌入式贡献者节点。
2. 新增 Channel 匿名摘要字段、默认 Hidden migration、版本化写入、append-only 审计、Console 页面与独立 `view / manage` 权限；不把 Public 频道自动开放。
3. 完成 `ChannelSummary / MemberActivity / HighlightedComment / Post / Question` 统一公开读模型、数据库侧资格与 keyset 查询、snapshot 游标、纯文本投影、`no-store` 和整流失败关闭。
4. 完成 `@radish/http` 契约与 `/discover` PC / mobile 页面，首项焦点不再假设具体来源，跨入口保留真实 `href`，数量文案按 locale 与英文单复数输出。
5. 后端构建 `0` warning；专题测试 `57` 通过、`6` 个 PostgreSQL 条件用例显式跳过；Client `489`、Console `61` 项全量测试及两端 production build 通过。
6. 应用本批 migration 并启动完整宿主，通过 host runtime、Gateway 匿名 / 登录态、PC / mobile、双语、代表主题、真实链接和 Console 治理只读路径复核；没有提交频道公开状态变更。
7. 运行态发现并修正 `long` 字符串 wire contract 与前端计数类型不一致的根因，补齐定向测试、全量 Client 测试和 production build 后关闭 `R1-P01`。
8. 完成 `R1-P02` 路由、权限、状态、专题和 PC / mobile 结构审计；固定连续阅读—讨论主轴、唯一帖子操作带、条件回答区和非重复线程索引，并明确视觉实现前拆分超限详情容器。

## 当前下一步

1. 依据已确认裁决修订 R1-P02 产品边界与代表状态，普通帖子 PC 方案必须体现帖子 / 回帖点赞、reaction、回帖的回帖，以及克制的轻回应和赞赏层级。
2. 基于现有活动设计与新家族 UI 基线重新收敛唯一 PC 主方案，提供截图、差异和推荐结论；未经确认不进入 Mobile 或代码实现。
3. PC 方向确认后再完成 Mobile 和代码拆分实现；局部差异不再复制多套残缺整页。
4. F4-R 当前批次完成后，后续视觉工作默认基于已落地家族 UI 规范优化更新，不从零重设计；全局灰玉品牌 token 仍进入共享主题批单独治理。

## 当前执行入口

- [开发路线图](/development-plan)
- [正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)
- [R1-P02 Public 详情与互动代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)
- [公开 forum 应用结构](/features/forum-public-app)
- [R1-P01 公开发现成组实现](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)
- [R1-P01 mobile 与统一公开读模型设计](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05)
- [社区发现 Public App](/features/discover-public-app)
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
