# L2 评论神评 / 沙发实时重算与标识填充日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 CommentService 的实时重算、神评 / 沙发检查与标识填充生成点，以及相关直接消费者的原行为回归。

## 实现与保留边界

- 移除逐次开始、扫描、空结果、成功更新及递归填充的 Info / Debug。神评、沙发、填充消费点分别生成 `comment.god_recheck_failed / comment.sofa_recheck_failed / comment.highlight_fill_failed` Error；外层重算 catch 使用 `comment.highlight_recheck_failed`，仅记录受控 failureKind，不传递异常对象或业务明细。
- 实时开关、数量门槛、前五名与并列最高赞、稳定窗口、替换阈值、零赞清理、排名和快照不变。新高亮保持退役旧行 → 插入 → 基础奖励 Outbox → 缓存失效；既有高亮仍先入队增量奖励，再更新快照及缓存。业务键、载荷、时间与审计不变。
- 计算 helper 原先消费的异常继续消费，正常返回给外层；外层不追加同一失败事件。根页标识填充失败仍返回评论并继续资料填充；单条详情的高亮读取、外层分页与资料失败继续向外传播。
- **现有部分写入边界**：旧高亮退役、新高亮插入、奖励入队或快照更新后，后续步骤失败可能仍返回 NoChange。真实 TranAop 对此正常返回走 commit 分支，不因 Error 日志自动回滚。本批不增加补偿、不改变事务属性，也不把无变更返回解释为数据库没有变化。
- 当前根页会清空子评论；保留的递归 helper 使用定向调用验证。未把该验证写成页面子树加载或实时广播验收。

## 验证

- 定向 .NET **92 / 92** 通过，无跳过：CommentHighlightLoggingTests、新旧评论实时推送 / 高亮 / 编辑回归及 RuntimeLogPolicyTests / RuntimeLoggingAdapterTests。
- 旧 / 候选 × Development / Production，候选显式 mode，Development 开启 diagnostics。候选 Error 逐字段核对事件码、模式、等级与仅有的 failureKind；旧输出捕获 Verbose 以上全部日志。
- 真实 CommentService、ReliableOutboxService、TranAop 与 Controller；mock 仓储对列更新和插入做内存状态模拟，验证退役 / 插入 / 入队 / 缓存顺序、并列排名、奖励载荷、无变更重复计算、八类失败阶段、增量奖励失败与部分进度。
- 列表降级、单条详情与外层失败传播、成功根标识 / 递归沙发标识、创建 / 点赞 / 删除结果，以及 VoChanged 为 false 时不广播高亮均有回归。上一批 Controller 回归继续覆盖编辑 / 版本恢复的调用边界。
- 既有测试覆盖稳定窗口与点赞领先阈值。外层重算 catch 保留原代码边界，本批未人为制造日志器故障来触发该 catch；注入的计算失败由内层消费并只记录一次。
- 首次编译发现新测试将 IReadOnlyCollection 参数误写为 IEnumerable，修正后全组通过。Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；8 个变更文件卫生、全量文档及 `git diff --check` 通过。记录索引 315 行仅有非阻塞篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CommentHighlightLoggingTests|FullyQualifiedName~CommentHighlightRealtimeServiceTest|FullyQualifiedName~CommentEditHistoryServiceTest|FullyQualifiedName~CommentRealtimeLoggingTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库 / 容器、访问生产或切换日志开关。内存状态与 mock 事务调用不代表 SQLite / PostgreSQL 真实提交、回滚、并发或跨事务奖励幂等证据；未执行 Redis、WebSocket、浏览器或 HTTP 中间件验收。

## 下一批与剩余事项

- 下一批为 PostQuickReplyService 轻回应通知入队 helper 及 PostQuickReplyController：原 helper 仍记录原始异常和业务身份后重抛，需核对创建事务、Outbox 与最终异常所有权。
- CommentController 静默 400 消费边界、其余业务和 SignalR 框架来源继续治理。评论高亮部分写入 / NoChange 的可靠性调整需独立确认；生产开关保持关闭，L2 尚未整体完成。
