# 2026-10-02 日终代码与文档回顾

> 时间口径：Asia/Shanghai。审阅范围为当日 `dev` 上全部 19 个开发提交（`c6d29f5c` 至 `a60ee583`，对照基线 `f5262a09`）；本次另作纯文档收尾提交。项目所有者要求今天到此结束，下一步只写入明天事项。

## 今日提交与批次索引

按提交时间从早到晚。表中测试数量引用对应批次当时的 .NET 定向回归，包含重叠用例，不相加为独立测试总数，也不表示日终重新执行过这些测试。

| 提交 | 代码变化与审阅要点 | 批次证据 / 当时通过数 |
| --- | --- | --- |
| `c6d29f5c` | 关注通知准备 / 入队按直接消费者区分故障归属；关系独立提交和失败后不补投的行为保留 | [关注入队](./unified-logging-l2-user-follow-2026-10-02.md) / 63 |
| `5cd804f4` | 偏好抑制全部接收者时移除 Info；SignalR 推送失败改安全 Warning，保留入箱与 Outbox 状态 | [通知创建 / 推送](./unified-logging-l2-notification-creation-2026-10-02.md) / 68 |
| `d852b449` | 通知 Hub 异常断开在清理后生成安全事件；补正本日前三批测试的候选 Mode 配置 | [通知 Hub](./unified-logging-l2-notification-hub-2026-10-02.md) / 124（扩大回归） |
| `ff510159` | 关系失效在 Chat / Notification 双 Hub 分别输出安全降级事件，继续后续接收者 | [用户关系失效](./unified-logging-l2-user-interaction-2026-10-02.md) / 51 |
| `b2e8f271` | ChatHub 正常连接 / 频道操作安静，异常断开保留安全 Warning；presence 与分组顺序不变 | [ChatHub](./unified-logging-l2-chat-hub-2026-10-02.md) / 79 |
| `954491df` | 评论实时发送失败使用固定 Warning；五类载荷、提交后推送与后续高亮发送决策保留 | [评论实时推送](./unified-logging-l2-comment-realtime-2026-10-02.md) / 64 |
| `fb5e55d2` | CommentHub 移除逐次 Debug 和无用 logger 注入；订阅、输入中与异常传播保留 | [CommentHub](./unified-logging-l2-comment-hub-2026-10-02.md) / 78 |
| `4f3d0aa5` | 神评 / 沙发重算与标识填充改为安全消费事件；保留部分进度、NoChange 和事务现状 | [评论高亮](./unified-logging-l2-comment-highlight-2026-10-02.md) / 92 |
| `9213b9c8` | 轻回应通知入队不再重复记录所有异常；Controller 的原 400 / 404 / 5xx 消费分别归属 | [轻回应](./unified-logging-l2-post-quick-reply-2026-10-02.md) / 61，API 契约另 8 |
| `c0ee8a05` | 评论长度 / 操作拒绝使用专用子类，依赖异常仍有安全 Error；原状态与写入后详情读取边界保留 | [评论最终消费](./unified-logging-l2-comment-controller-2026-10-02.md) / 126，版本补验另 4 |
| `ad9cc201` | 提交台账冲突仅在既有记录成功返回后记录恢复事件；保持保存点与后续失败传播 | [内容提交冲突](./unified-logging-l2-content-submission-2026-10-02.md) / 120 |
| `7b7d2bee` | 帖子内容 / 操作拒绝与依赖失败分类；编辑、置顶保留原状态和事务 | [帖子编辑 / 置顶](./unified-logging-l2-post-controller-2026-10-02.md) / 134 |
| `d5af7919` | 帖子恢复在既有 409 转换成功后记录被消费依赖故障；不改变恢复、CAS 与不可变快照 | [帖子版本恢复](./unified-logging-l2-post-restore-2026-10-02.md) / 106 |
| `9bb01805` | 问答九入口在响应成功构造后记录依赖失败；保留单一聚合解包及错误字段 | [问答最终消费](./unified-logging-l2-question-controller-2026-10-02.md) / 106 |
| `46875227` | 投票 ID 拒绝使用专用子类；Controller 最终消费与真实投票 / 计数回滚回归 | [投票最终消费](./unified-logging-l2-poll-controller-2026-10-02.md) / 88 |
| `a6ca551d` | 抽奖查询 / 手动开奖最终消费治理；自动任务继续原批次口径，发现 SQLite → Outbox 时间偏差 | [抽奖最终消费](./unified-logging-l2-lottery-controller-2026-10-02.md) / 103 |
| `e9ae513c` | 标签重名专用分类；其他 InvalidOperationException 保留 400 且记录故障，写后失败不新增回滚 | [标签最终消费](./unified-logging-l2-tag-controller-2026-10-02.md) / 72 |
| `a9e5c898` | 分类父级缺失用私有可空结果表达；依赖异常按原 catch 归属，0 行更新仍成功 | [分类最终消费](./unified-logging-l2-category-controller-2026-10-02.md) / 90 |
| `a60ee583` | 回应三入口消费 5xx 与一次冲突重试使用安全事件；发现真实仓储恢复已删除行的偏差 | [表情回应](./unified-logging-l2-reaction-controller-2026-10-02.md) / 73 |

前三批候选 Development 的配置问题已在 `d852b449` 中修正，并以 124 项扩大回归复验；前两行的原批次数量不能替代这项补验。当前技术契约以[统一日志事件契约第 7–48 节](../features/unified-logging-contract.md)为准，历史记录的“下一批”只表达当时执行顺序。

## 文档与代码对照结论

本次对照当天全部生产代码差异、共享事件策略、新增回归的覆盖点及批次证据，再核对相关通知、评论高亮、轻回应、版本恢复、问答、投票、抽奖、分类 / 标签和回应专题。属于文档一致性复核，不宣称完成全仓代码审计或新一轮业务运行验收。

1. **修正过期进度**：日志契约第 31 节仍把 NotificationHub 自有生命周期列为待治理，现改为指向第 32 节；SignalR 框架来源继续保留待办。治理设计补上本日日终入口，日志使用指南改为指向第 7–48 节的准确覆盖范围。
2. **补齐功能文档中的可靠性限制**：通知专题补充关注关系与入队不共用事务，以及自动抽奖的 Outbox 时间偏差；抽奖专题补充当前已知限制、开奖事务包含通知入队和详情刷新，并把 4 月“当前下一步”明确为历史快照。轻回应专题补充数据库回滚后缓存仍可能拒绝重试；高亮专题补充部分写入 / NoChange 边界，移除已不存在的空扫描日志说明。
3. **区分自有日志与框架输出**：通知实时同步说明补充安全事件、清理 / 传播顺序和框架来源未收口的限制。双 Hub、多接收者或后续独立失败允许有多条不同事件；不把“最终所有权”笼统写成整条调用链只能输出一条日志。
4. **补记静态发现的边界**：早期轻回应、评论和帖子消费点仍先记录，再读取异常 Message 构造响应或失败结果；getter 再次抛错时可能已有日志并继续进入 API。现将其列为待核对边界，不套用后续批次“先构造成功再记录”的回归结论，也未在文档收尾中改代码或新增测试。
5. **收拢入口**：当前规划明确 10 月 3 日事项；19 条批次细目移至本页统一索引，records 入口由 326 行降至 308 行。当前状态、完整证据和具体技术契约分别放在 planning、records 与 features，不复制到根入口。
6. **保留无需变化的契约**：帖子 / 回答 / 投票的业务规则、HTTP 错误映射和数据模型没有因异常子类分类而改变，不在功能页重复日志实现细节。Reaction 专题已随末批补齐恢复偏差，无需再重复修改；开发路线、AGENTS / CLAUDE、依赖、运行配置与生产开关不变。

## 独立问题与未关闭边界

| 边界 | 已确认事实与后续范围 |
| --- | --- |
| Reaction 取消后再添加 | 通用仓储默认排除已删除行，恢复查询不能命中，两次插入冲突后返回 409。真实 SQLite 已复现；受控提供已删除行的分支测试不证明实际恢复已修复。见[回应记录](./unified-logging-l2-reaction-controller-2026-10-02.md)。 |
| 自动开奖时间 | SQLite 截止时间读回为 Unspecified，Outbox 再按本地时区转 UTC；Asia/Shanghai 下信封提前 8 小时。通知处理器采用信封发生时间，需独立确认规范化方案。见[抽奖记录](./unified-logging-l2-lottery-controller-2026-10-02.md)。 |
| 关注关系与通知 | 关系先独立提交，入队失败不回滚关系；再次关注未变更时不补投，需另评估事务 / 补偿。见[关注记录](./unified-logging-l2-user-follow-2026-10-02.md)。 |
| 评论高亮与轻回应 | 高亮消费失败后可能保留部分写入并正常返回；轻回应数据库回滚不撤销此前缓存。两者日志治理均不自动修复可靠性。见[高亮](./unified-logging-l2-comment-highlight-2026-10-02.md)与[轻回应](./unified-logging-l2-post-quick-reply-2026-10-02.md)。 |
| 响应与持久化 | 评论创建 / 编辑后的详情读取发生在写入事务提交后；标签 / 分类创建与更新没有事务特性，写后抛错仍保留数据；分类更新 0 行仍成功。HTTP 失败不总等于数据库回滚。见[评论](./unified-logging-l2-comment-controller-2026-10-02.md)、[标签](./unified-logging-l2-tag-controller-2026-10-02.md)与[分类](./unified-logging-l2-category-controller-2026-10-02.md)。 |
| 错误处理再次失败 | 早期部分 catch 的日志先于 Message 读取，日终仅静态确认顺序，尚未新增该故障路径实测。后续按具体入口确认日志所有权和响应构造顺序。 |
| 框架输出 | SignalR 连接 / 消息处理仍可能输出独立日志；标签合成 404 在 DefaultHttpContext / MemoryStream 下触发框架重抛，旧 sink 收到原异常。该内存现象不能推广为所有真实宿主的 404 行为。见[通知 Hub](./unified-logging-l2-notification-hub-2026-10-02.md)与[标签记录](./unified-logging-l2-tag-controller-2026-10-02.md)。 |

上述问题不在今日文档提交中修复；可靠性和运行时改动仍需独立方案确认。Rust `.tmp` 水印回退按[既有记录](./unified-logging-l2-outbox-native-2026-09-23.md)后置，生产候选开关继续关闭，L2 尚未整体完成。

## 验证范围

各批回归结果和命令见上表链接；当天新增测试包含旧 / 候选 × Development / Production、真实 Service / Controller / TranAop、受控依赖和分批 SQLite 证据。表情回应末批 .NET **73 / 73**、Node **27 / 27** 通过，API 构建 0 警告、0 错误。内容提交批次最终显式排除了未配置环境的 PostgreSQL 用例，不能写成 PostgreSQL 已验收。

没有新增真实 SignalR / WebSocket、数据库并发、HTTP 宿主、MVC 认证 / 模型绑定、浏览器或通知实际投递验收结论。日志安全回归不会抹去各记录单独列明的框架边界与失败行为。

文档收尾检查：`npm run check:repo-quality:changed` 与 `git diff --check` 通过；11 个变更文件、829 个文档 / 治理文件的文本检查，以及 789 个 Markdown 文件的 716 个本地相对链接检查通过。`Docs/guide/logging.md` 保持 768 行、records 索引降至 308 行，两项超过建议篇幅的提醒均非阻断。本次只修改 Docs，不重跑应用测试或构建。

## 明天事项与停止线

明天（2026-10-03）首项为 StickerController 的 CreateGroup / AddSticker / BatchAddStickers / BatchUpdateSort：对照 StickerService 核对输入拒绝、标识冲突、目标缺失、排序快照失效、缩略图 / 依赖故障和批量部分写入，再说明方案并等待确认。具体执行与验证边界见[当前规划](../planning/current.md)。

其余业务与框架来源逐组推进；以上独立问题不自动混入表情管理日志批次。L1 剩余传输 / 磁盘门禁、L2 整体治理及 L3–L6 均未因今天提交关闭，Native P8-C readiness 保持后续顺位。

本次日终收尾不安装依赖、不启动服务 / 容器、不设置定时任务、不访问生产、不 push 或发布部署。明天事项是书面计划，本日以文档提交结束。
