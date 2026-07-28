# F4 2026-07-28 日终提交回顾与文档审阅

> 日期：2026-07-28（Asia/Shanghai）
>
> 范围：按提交日期统计的 6 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- 今天完成 F4-O 回答生命周期 C / D：正式 Web、PublicId 外部契约、分页与通知定位、PC / mobile Pencil、strict migration、Gateway 代表矩阵、验收期根因修正和环境清理全部闭环，专题关闭。
- `20260728_016_forum_answer_lifecycle_strict` 已以前滚 migration 补齐索引与强一致性校验；既有 `20260727_015` 账本内容和 checksum 保持不可变。
- 正式 Web 通过 `PostAnswerLifecycleSection` 承接回答区；共享 `PostDetail` 只在未注入权威组件时保留旧回答区，避免同页重复渲染，同时维持 WebOS 历史兼容。
- F4-O-D 验收后的临时帖子、回答、Revision、采纳事件、通知、Outbox、浏览历史与经验副作用均已清理，服务和浏览器资源已经释放。
- F4-P-A 已完成候选审计与权威设计，下一专题固定为“论坛帖子收藏与个人内容回访”；今天不进入 B 批代码。
- 日终没有遗留未提交业务代码；下一开发日第一顺位是取得批准后实施 F4-P-B 服务端与 migration。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `06fbb652` | `feat(forum): 完成回答生命周期正式 Web` | 收敛回答创建为 `postIdentifier`，接入回答分页、作者生命周期、采纳变更、举报、通知定位、双语主题和 PC / mobile 正式页面。 |
| `3d406f3e` | `feat(db): 加固回答生命周期迁移校验` | 新增不可变顺序 migration `20260728_016`，补稳定分页 / 作者索引及 Revision、采纳、计数、附件一致性检查。 |
| `470037a4` | `fix(db): 兼容回答生命周期迁移前置校验` | 旧 `PostAnswer` schema 只读取必然存在的 `Id`，避免 `013` 在 `015` 前加载未来字段。 |
| `38067b8b` | `fix(forum): 避免公开问答重复渲染` | 权威回答组件与旧回答区改为互斥渲染，并由静态契约测试锁定边界。 |
| `bb70bc6a` | `docs(planning): 关闭 F4-O 回答生命周期专题` | 补 D 批验收记录，将规划主线从 F4-O 切到 F4-P 候选审计。 |
| `3ca9bd11` | `docs(planning): 确立 F4-P 帖子收藏专题` | 固定私有 Bookmark 真相、显式幂等状态、Post 行锁、计数投影、个人回访路径和 A-D 停止线。 |

## 按代码反查文档

### 已保持一致

- F4-O 权威设计已经记录 `postIdentifier` 新契约、Controller LongId 兼容边界、`P26 / P27`、正式 Web 回答组件、`015 / 016` migration 和 D 批完成状态。
- [F4-O-D 成组验收记录](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)准确记录 migration 顺序兼容、重复回答区根因、最终测试数量、运行态矩阵、数据清理和 PostgreSQL 条件跳过事实。
- `public-web-unified-experience.pen` 与设计源索引已经包含 `P04 / P11 / P26 / P27`，无需再次修改加密设计源。
- 公开论坛说明已经包含 `answer` intent、回答 PublicId 定位、可靠提交和正式 Web 路径；本次代码没有引入新的页面族或壳层归属。

### 本次修正

- 公开 Web 统一体验说明从“Gateway 留待 F4-O-D”更新为 D 批已验收并关闭。
- 早期论坛问答 MVP 页明确 F4-O A-D 已完成，避免新会话继续把 A/B 服务端状态当成现状。
- F4-O migration 说明区分已完成的真实 SQLite 账本 Apply / Verify 与当前机器仍跳过的 PostgreSQL 条件用例。
- F4-N 内容赞赏文档不再把 F4-O-C / D 写成未完成，同时继续固定 `PostAnswer` 不自动进入资产范围；若未来扩展必须另立专题。
- `current.md` 新增 2026-07-29 明天事项，记录索引同步加入本页和 F4-O-D 验收入口。

## 明天事项（2026-07-29）

1. 新会话先读 [当前进行中](/planning/current) 和 [F4-P 权威设计](/features/forum-post-bookmark-personal-library-design)，确认权威对象、Post 行锁顺序、接口与停止线。
2. 汇报 F4-P-B 的实体、Repository / Service、Controller、migration、`@radish/http` 和测试修改范围，获得明确批准后再编码。
3. 服务端先落 `UserPostBookmark` 私有唯一关系和显式 `isBookmarked` 状态；同一帖子写入统一按“Post -> Bookmark”锁顺序执行。
4. migration 使用下一个未占用 Main ID，覆盖 PublicId、唯一关系、稳定分页、租户 / 孤立目标与 `CollectCount` 重建一致性。
5. B 批只做服务端、migration、HTTP 客户端和代码侧回归，不提前修改正式页面，不启动服务或执行 Gateway smoke。

## 验证与留痕边界

- F4-O 最终后端全量为 `1075` 通过、`33` 个 PostgreSQL 条件用例跳过、`0` 失败；`radish.client` 为 `472` 项测试、type-check 与 production build 通过。
- Gateway、API、Auth 和前端运行态入口均返回 `200`；PC / mobile 页面只有一个权威回答区，无水平溢出或浏览器 warning / error。
- `20260727_015` 与 `20260728_016` 已应用，真实 `DbMigrate verify` 无 pending；六个 SQLite 数据库完整性检查均为 `ok`。
- 当前机器未配置 PostgreSQL 集成测试环境，文档继续保留显式条件跳过，不把 SQLite 结果描述为 PostgreSQL 实跑。
- 本次日终只修改文档，执行文档、文本卫生和 Git 差异检查；不重复运行代码测试，不启动服务。

## 明日启动口径

F4-P-B 仍涉及数据库结构、接口和运行时行为。下一开发日先汇报预计范围并取得明确批准；若后续进入 F4-P-D 真实页面验收，优先使用应用内浏览器，只有不可用时再回退 Chrome。
