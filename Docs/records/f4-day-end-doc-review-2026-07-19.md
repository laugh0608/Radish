# F4 2026-07-19 日终提交回顾与文档审阅

> 日期：2026-07-19（Asia/Shanghai）
>
> 范围：`2d965d6d^..c15d6320` 的 15 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- F4-C 聊天历史搜索、F4-D 消息 Reaction、F4-E 消息置顶和 F4-F 轻量阅读回执均按 A-D 四批完成服务端权威契约、正式 Web / WebOS 共用页面和成组验收，四个专题全部关闭。
- 今天形成的 Chat 长期基线不依赖页面临时状态：搜索、Reaction、置顶和回执分别拥有清楚的数据、REST、Hub、Store、隐私和并发边界，并统一复用正式 `/messages` 的 `ChatApp`。
- F4-F-D 验收发现并修复频道最后消息投影的共同根因；未读、回执、频道预览和最后消息继续保持各自权威边界，没有用回执逻辑为其他状态兜底。
- 远端规则说明补清了模板与实际 ruleset 的边界：只调整 Actions、required checks 或合并方式时，不应顺带启用模板中的 Conventional Commits 远端门禁。
- 日终交叉审阅发现 Chat 总览、路线图、架构、前端和系统设计仍保留 F4-F-C 或更早状态，表情系统及 Reaction Phase 2 冻结稿也仍把聊天消息写成 Main 库通用 `Reaction` 的后续 TargetType；本次已按真实代码校准。
- 当前没有遗留未提交业务代码。下一顺位不是继续扩展 Chat，而是执行 F4-G-A 候选审计，从已有业务域选择一个长期价值和权威边界清楚的完整专题。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `2d965d6d` | `docs: 说明远端提交规则边界` | 区分仓库模板与远端实际规则，避免精确更新时意外扩大门禁。 |
| `7cd6a706` | `feat(chat): 完成消息检索服务端契约` | `SearchText`、成员 ACL、跨库查询、快照 cursor、消息窗口定位和 HTTP 契约落地。 |
| `71b041bc` | `feat(chat): 完成消息历史搜索工作区` | Pencil 与正式 `/messages` 的 PC / mobile 搜索、筛选、分页、定位和恢复状态落地。 |
| `75178345` | `fix(chat): 完成消息搜索成组验收` | 双账号正式路径和共同根因回归通过，F4-C 关闭。 |
| `289dc585` | `feat(chat): 建立消息回应权威契约` | Chat 库专属 Reaction、幂等 operation、revision、权限和保留策略落地。 |
| `6baa068b` | `feat(chat): 完成消息回应工作区` | 共享 Reaction UI、正式 Web / WebOS 状态合并、并发与重连处理落地。 |
| `e0ff35f7` | `fix(chat): 完成消息回应成组验收` | 双账号、多语言、PC / mobile、权限、并发和离线恢复通过，F4-D 关闭。 |
| `22640e74` | `docs(chat): 建立消息置顶权威设计` | 否定不存在的预留字段，固定独立置顶集合、频道 revision、容量和权限矩阵。 |
| `d032a7e6` | `feat(chat): 建立消息置顶权威契约` | migration、事务锁、目标状态、撤回一致性、REST / Hub 全量快照落地。 |
| `582c2f5e` | `feat(chat): 完成消息置顶工作区` | PC 紧凑条、完整列表、mobile Bottom Sheet、定位和多端 revision Store 落地。 |
| `84031358` | `docs(chat): 完成消息置顶成组验收` | 三普通账号覆盖四类频道、20 条上限、失权与重连，F4-E 关闭。 |
| `b5946782` | `docs(chat): 固定轻量阅读回执权威设计` | 裁决唯一持久游标、隐私矩阵、REST / Hub 边界和活跃阅读面。 |
| `780345b0` | `feat(chat): 建立轻量阅读回执服务端契约` | 单调推进、受限聚合、读者 cursor、显式索引、稳定错误和 HTTP 契约落地。 |
| `d45aace6` | `feat(chat): 完成轻量阅读回执 Web 工作区` | 精确可见游标、会话内存重试、Direct 边界、Private 详情和无障碍落地。 |
| `c15d6320` | `fix(chat): 完成轻量阅读回执成组验收` | 三普通账号全矩阵通过并修复最后消息投影共同根因，F4-F 关闭。 |

## 代码与文档交叉审阅

### F4-C：消息搜索与定位

- `ChannelMessage.SearchText`、共享 normalizer、SQLite / PostgreSQL ledger migration、批量成员 ACL 和专属搜索 Repository 已成为服务端权威实现。
- 正式页面使用独立 `ChatMessageSearchPanel` 与 `useChatMessageSearch` 管理查询、cursor 和恢复，不把跨频道结果写入普通历史消息 Store；定位复用 `GetMessageWindow` 与 `useChatMessageNavigation`。
- 专题设计、实现记录、阶段验收、开发路线和 Chat 入口均已标记 A-D 关闭，没有发现仍声称“只完成 F4-C-A”的当前规划口径。

### F4-D：消息 Reaction

- 真实数据位于 Chat 库专属 `ChatMessageReaction / ChatMessageReactionOperation`，通用表情系统只提供表情目录和共享 `ReactionBar`；它不是 Main 库 `Reaction(TargetType=ChatMessage)` 的扩展。
- desired-state、operation fingerprint、revision、10 种上限、30 天 operation 保留、撤回一致性和权限裁剪均有服务端契约与测试，前端只合并权威状态。
- 日终发现 `chat-system.md`、`emoji-sticker-system.md` 与 Reaction Phase 2 冻结稿仍保留旧通用 Reaction 表口径，本次已修正，并为架构、路线图和前端说明补齐 F4-D 权威专题链接；共享 `ReactionBar` 继续允许 `ChatMessage` 作为 UI 目标类型，但不代表复用 Main 库 API 或持久表。

### F4-E：消息置顶

- `ChatMessagePin` 是唯一活跃集合，`Channel.PinRevision` 只承担集合版本；不存在旧草案所称 `PinnedMessageId / IsPinned` 预留字段。
- 20 条容量、频道级事务锁、撤回原子移除、完整快照、消息定位、PC / mobile 入口和账号 reset 已成为维护基线。
- 权威专题和验收记录已正确关闭；日终只把总览、路线图与架构中停在 F4-E-C 的状态更新为 A-D 完成。

### F4-F：轻量阅读回执

- `ChannelMember.LastReadMessageId` 是唯一持久游标；未读计算继续读取同一字段，但回执、未读、频道预览和最后消息不互相替代或派生额外真相源。
- 客户端仅在页面可见、聚焦、会话位于尾部且 WebOS 窗口位于前台时，通过 REST 提交实际可见的最高持久消息；Hub 只广播无个人数据的 `ReadReceiptsChanged` 失效提示。
- Public / Announcement 不展示，普通 Private 仅发送者查看人数和读者分页，Accepted Direct 只展示对端单一边界；在线 Presence 不参与回执语义。
- 日终发现多个 Chat 入口仍写成 F4-F-A / B / C，已统一校准为 A-D 关闭，并补齐实际组件、Hook、Store 与文档导航。

### 文档分层与未改范围

- `Docs/planning/current.md` 只承载当前状态与明日执行事项；详细提交事实进入本记录，Chat 长期契约留在各权威专题和系统文档。
- `Docs/development-plan.md` 与发布后功能完成线已正确指向 F4-G-A，本次不重复堆叠明日命令级步骤。
- 历史专题中的审计事实、旧方案摘录和迁移过程继续保留；它们用于解释裁决依据，不因当前实现完成而追溯改写。
- 移动系统通知、Flutter、Tauri 和主动生产证据采集的既定边界没有变化。

## 今日验证回顾

以下结果来自各提交与专题验收的实际记录；本次日终文档批次不重复运行代码测试或运行态验收：

- F4-C-B 后端 `896` 项、PostgreSQL 17 专项 `2/2` 与 Baseline Quick 通过；F4-C-C client `430` 项和构建通过；F4-C-D 正式 Gateway 双账号矩阵通过。
- F4-D-B 后端 `906` 项、PostgreSQL 专项 `2/2` 与解决方案构建通过；F4-D-C client `436` 项通过；F4-D-D 搜索 migration 前向兼容根因修复、真实离线恢复和双账号矩阵通过。
- F4-E-B 后端 `922` 项、PostgreSQL 专项 `2/2` 通过；F4-E-C client `442` 项通过；F4-E-D 三普通账号、四类频道、20 条上限、失权和重连矩阵通过。
- F4-F-B 后端 `938` 项、PostgreSQL 专项 `2/2` 通过；F4-F-C client `447` 项与共享 UI `24` 项通过；F4-F-D client `449` 项、后端 `938` 项、解决方案构建和三普通账号运行态矩阵通过。
- 各成组验收结束后服务均已停止，临时账号、频道、会话、消息、通知、凭据、备份和浏览器资产按记录清理；六库完整性与严格 migration verify 通过。
- 本次日终文档 staged 仓库卫生与 `git diff --cached --check` 通过；`emoji-sticker-system.md` 原有篇幅仍超过 800 行建议线但低于 1200 行硬上限，本批只修正与 F4-D 冲突的当前口径，不在日终扩成文档拆分重构。

## 明日事项

1. 启动 `F4-G-A 功能完成线候选审计与专题裁决`，先按规划读取顺序确认阶段和候选边界，不从历史 backlog 直接挑功能实施。
2. 对 Docs / Wiki 作者协作与权限、圈子关系与复访、治理案件 / 证据 / 动作、宠物既有成长与复访进行同口径只读审计；Chat 与通知只作为真实缺口复核项。
3. 逐项交叉核对数据、migration、Repository / Service、API / Hub、HTTP 契约、Store、正式 PC / mobile 页面、WebOS 复用、测试和权威文档，识别重复真相源、权限 / 隐私 / 数据 / 性能风险和维护成本。
4. 用统一矩阵选择一个推荐专题，说明其他候选后置原因，给出用户价值、长期边界、预计修改范围、主要取舍、A-D 批次、验证矩阵、停止线和完成标准。
5. 先向用户汇报并等待明确批准；获批前不修改业务架构、接口、migration、Pencil 或专题设计，也不并行展开多个候选。

## 当前不做

- 不继续从历史 Chat Phase 2 清单追加能力，不修改已经关闭的 F4-C 至 F4-F 权威契约。
- 不启动移动系统通知，不解冻 Flutter 或 Tauri，不重启主动生产证据采集。
- 不在日终文档批次修改代码、依赖或数据库，不启动服务或执行浏览器 smoke。
