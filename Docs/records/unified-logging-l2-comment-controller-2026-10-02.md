# L2 评论创建 / 点赞 / 编辑最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 CommentController 创建 / 点赞 / 编辑的最终异常消费，以及 CommentService、ForumContentWriteService 和 CommentRepository 对应的正常拒绝来源。

## 已确认方案与实现

- 审计发现正常拒绝与依赖故障共用 ArgumentException / InvalidOperationException，仅在 Controller 按原异常类型记录会制造告警噪声。项目所有者本轮明确批准增加评论专用异常子类，并保持 HTTP 状态、文案、事务和幂等行为。
- 新增 `CommentContentValidationException : ArgumentException`，仅用于明确的最小 / 最大内容长度规则；新增 `CommentOperationRejectedException : InvalidOperationException`，用于点赞评论不存在，以及编辑服务已经返回的失败结果。没有匹配异常原文或读取异常 Data 来推断业务分类。
- Controller 消费的其余参数 / 操作异常分别生成 `comment.create_failed / comment.like_failed / comment.edit_failed` Error，仅带受控 failureKind。正常拒绝保持安静；未被消费的异常继续由 API 最终边界记录一次。
- 编辑校验阶段保持原失败元组返回。正常长度校验不记录；设置依赖的 ArgumentException 在 Service 消费点记录 `comment.edit_failed` 后仍返回 `(false, message)`，上层转换为明确拒绝，不重复记录。版本恢复消费者接收的结果与文案不变。
- 内容、身份、提交键、摘要、通知载荷、URL / 查询参数与原始异常文本不进入运行日志；既有错误响应文案契约未改。

## 行为与证据边界

- 评论创建 / 编辑继续由 ForumContentWriteService 的事务 AOP 包围提交台账与写入，版本追加后完成成功台账；失败仍调用 rollback。重放、重复内容及无变化仍按原语义返回，不重复写入 / 推送。
- 创建 / 编辑提交后的详情读取失败仍返回 500，不能据响应失败宣称写入已回滚；点赞详情读取仍在 Controller 原 catch 内，InvalidOperationException 继续返回 400。未调整推送顺序、软删除、编辑时间 / 次数限制或版本冲突。
- 新日志测试使用真实 CommentService、ForumContentWriteService、ReliableOutboxService、TranAop、Controller、结果过滤器及内存 API 异常管道；台账、版本、数据库与 SignalR 依赖为 mock。它们验证调用顺序和事务分支，不替代该整条链的真实数据库事务。
- LikeRelationConsistencyTest 使用临时 SQLite 验证评论缺失的明确拒绝与无写入，以及通知 Outbox 失败时点赞关系 / 计数真实回滚，并保留原异常实例。既有写入事务测试也验证台账 / 业务事实 / Outbox 事实共同回滚；该既有测试走发帖写入入口，不冒充评论全链运行证据。

## 验证

- 主定向组 **126 / 126** 通过，无跳过，首轮编译 / 测试通过。覆盖新增四模式用例、既有评论推送、编辑规则、提交写入、事务、点赞一致性、API 错误契约及日志策略 / 适配器。
- 旧 / 候选 × Development / Production；候选显式 mode，Development 开启 diagnostics。核对单条 Error、稳定事件码、mode、固定 failureKind / HTTP statusCode，以及敏感哨兵不进入日志。
- 覆盖正常长度 / 缺失 / 权限 / 编辑次数与时间窗口、提交键 / 冲突 / 限频、快照 / Begin / 设置 / 插入 / 计数 / 入队 / 版本 / 完成台账故障、详情读取边界、成功通知载荷、审计、重放与无变化。
- Node 日志契约 **27 / 27** 通过。
- 版本记录 / 恢复补验 **4 / 4** 通过；API `--warnaserror` 构建通过，0 警告、0 错误。14 个变更文件卫生、全量文档及 `git diff --check` 通过；记录索引 317 行仅有非阻塞篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CommentControllerLoggingTests|FullyQualifiedName~CommentRealtimeLoggingTests|FullyQualifiedName~CommentEditHistoryServiceTest|FullyQualifiedName~ForumContentWriteServiceTest|FullyQualifiedName~ForumContentWriteTransactionIntegrationTest|FullyQualifiedName~LikeRelationConsistencyTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~ApiErrorContractTest'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ForumContentRevisionServiceTest'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关；未取得 PostgreSQL、真实宿主、WebSocket 或浏览器证据。

## 下一批

- ContentSubmissionService 唯一约束冲突消费点仍记录原始异常及提交身份信息。继续核对保存点恢复、既有记录读取成功 / 失败与最终异常归属；本批没有改动该共享服务的并发、幂等或限频行为。
- 其余业务和框架来源继续治理，生产开关保持关闭，L2 尚未整体完成。
