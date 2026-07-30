# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、明日执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-R 等待 Pencil 空闲；F4-S 已完成 A-D 并关闭`
- **产品下一顺位**：`等待用户明确确认 Pencil 空闲后进入 C-1B；此前不读取或修改 .pen`
- **复核日期**：`2026-07-30`
- **正式主线**：Web 优先；PC / mobile 浏览器共同验收。Flutter 是次级移动原生产品线，WebOS `/desktop` 仅历史兼容，Tauri 暂时弃用并等待未来重新评估。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- `2026-07-30` 已裁决 F4-R 为家族 UI 统一接入与产品视觉重构专题：通用视觉遵循 RadishX `family-ui v26.7.2`，Radish 通过本地差异附录保留内容优先 Public、四主题权益、Console Workbench 与多端产品契约。
- `2026-07-30` 已完成 C-0 参考素材审计：完整阅读 family-ui `references.md` 并逐张查看其索引的 27 张参考图，建立共享基础、Public、Private / Author、Console、暗色与移动端的吸收 / 排除映射；参考图不作为产品素材，mobile 不由桌面图缩放。
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

## 今日进展（2026-07-30，F4-R 等待 C-1B；F4-S 已关闭）

1. A 批已完成：建立 [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)和 [Radish UI 差异附录](/frontend/ui-addendum)，固定规范优先级、Profile、四主题、多端顺位与停止线。
2. A 批已同步当前规划、开发路线、壳层策略、前端设计、项目入口和 Agent 协作文件，没有改写历史验收事实。
3. B 批已完成：复制 family-ui token，建立 Client / Console L2 兼容映射，统一共享 Ant Design 状态语义，并补主题定义与映射测试。
4. C-0 已完成：审计 `references.md` 与全部 27 张参考图，形成 Radish 页面族参考映射并固化为 Pencil / 代码前置门禁。
5. C-1A 已完成：形成[代表页代码事实审计](/frontend/f4-r-representative-page-audit)，确认 `7` 个 R1、`4` 个 R2 与 R3 路由族继承关系，并拆分两类 Console 代表工作台。
6. Pencil 当前由其他项目使用；只有用户明确确认空闲后，C-1B 才先复核 `R1-F01`，再按 Public、Author、Private、Console 顺序更新命中的 R1 / R2 代表设计。
7. 等待期间已完成[镜像漏洞门禁分层](/guide/image-vulnerability-gate)：五个正式镜像统一生成 Trivy JSON，由仓库内策略评估器完成分层裁决、报告工件和例外到期治理。
8. 等待 Pencil 期间穿插完成 F4-S A-D：服务端公开白名单、账号 / 内容 / 商品资格、确定性排名、稳定错误契约和 Web 路由均已收口；Gateway PC / mobile、匿名 / TestUser 和 API 边界矩阵通过，详细证据见[成组验收记录](/records/f4-s-d-leaderboard-public-governance-stage-acceptance-2026-07-30)。
9. 本轮已按当前任务授权启动并停止全部本地服务，浏览器、测试身份与头像访问计数副作用均已清理；未读取或修改 `.pen`，F4-R C / D 仍不扩 WebOS，不恢复 Tauri。

## 明日事项（2026-07-31）

1. 新会话先读取本页、[F4-R 专题](/features/family-ui-convergence-design)、[Pencil 代表页协作流程](/frontend/pencil-representative-page-workflow)、[C-1A 代码事实审计](/frontend/f4-r-representative-page-audit)和 [UI 差异附录](/frontend/ui-addendum)，不默认展开历史记录。
2. 首先向用户确认 Pencil 是否空闲；只有得到当前任务的明确确认后，才读取或修改 `.pen`。
3. Pencil 空闲时进入 C-1B：先复核 `R1-F01` 共享组件、状态、主题与壳层矩阵，并再次核对 family-ui `references.md` 及 27 张参考图映射；再按 Public、Author、Private、Console 顺序处理必要 R1 / R2，不为 R3 派生页复制画板。
4. 代表结构与契约确认前不进入页面族实现；功能、文案、按钮、权限和状态机继续服从专题文档与当前代码。
5. Pencil 仍被占用时，不读取或修改 `.pen`；可以只读审计下一项不依赖 Pencil 的完整 F4 功能或维护候选，形成 `2~4` 个有代码事实、用户价值、权威边界、维护成本和停止线的候选，获得批准后再建专题或编码。
6. 默认不启动服务或执行真实 smoke；只有专题准备验收且获得当前任务授权后再启动。正式顺位继续为 Web 优先、Flutter 条件式承接移动原生价值、WebOS 只兼容、Tauri 冻结；不重启主动生产证据采集。

## 当前执行入口

- [开发路线图](/development-plan)
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
- F4-Q 已关闭，不回拉标签关注、个性化推荐、标签首页、SSR / SSG 或公开个人页 sitemap；F4-R C-1B 代表设计复核前不提前实施页面视觉。
- 不恢复 Tauri，不扩展 WebOS 新功能，不把 Flutter 做成 Web 的机械复制，也不重启主动生产证据采集。
- 用户明确确认 Pencil 空闲前，不读取或修改任何 `.pen` 文件，也不提前实现尚未复核的 F4-R 页面视觉。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后再统一集成。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 smoke 只在专题或成组功能准备验收时执行；启动服务前必须取得当前任务授权。
- 本页不保存历史流水；完成事实进入 records、changelog 或 archive。
