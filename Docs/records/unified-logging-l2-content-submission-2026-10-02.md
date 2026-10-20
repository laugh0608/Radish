# L2 内容提交并发冲突恢复日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 ContentSubmissionService 唯一约束冲突后的读取 / 重置恢复及直接消费者审计。

## 实现与边界

- 移除恢复尝试前包含原始异常、用户、操作类型与提交键的 Warning。成功从既有记录取得结果后生成 `content_submission.conflict_resolved` Warning，仅带 failureKind；不记录记录 ID、键、正文摘要、指纹、SQL、保存点名称或异常原文。
- 事件只表示恢复分支取得结果。Pending 返回 Processing、成功记录返回 Succeeded / DuplicateContent、摘要不符返回 Conflict、失败 / 过期记录重置后返回 Started，均保持原样；它不代表业务成功或外层事务提交。
- 既有记录缺失继续抛出原冲突异常；读取 / 重置失败继续传播该异常，不生成恢复成功摘要。正常创建、直接重放、非法键、限频与完成台账保持安静。
- 保存点、事务、键与指纹、频率限制、24 小时保留及审计未改。既有唯一约束识别仍读取 Message / InnerException，本批不改变数据库错误识别规则，也不宣称业务代码完全不读取异常文本。
- 评论创建链已验证：恢复结果对应重放 / 409 / 正常写入；读取 ArgumentException 仍由 CommentController 返回 400 并生成安全 Error，IO 等由 API 最终边界记录。恢复后另一步业务写入失败时，恢复 Warning 与最终 Error 是不同事件。
- **尚未关闭的消费者**：PostController 编辑的 ArgumentException / InvalidOperationException，以及 QuestionController 部分参数 / 业务 / 聚合异常仍有静默消费；本批未扩改其分类与响应，不能据此宣称共享内容提交全链已完成异常治理。

## 验证

- 新增旧 / 候选 × Development / Production 回归，候选显式 mode，Development 开启 diagnostics；核对事件码、等级、mode、受控属性、敏感哨兵与原始异常 ToString 不进入日志。
- mock 仓储验证保存点回滚调用先于恢复查询、各恢复结果、重置清理字段 / 审计、缺失 / 读取 / 重置 / 非唯一失败的原实例传播与单一最终 Error。
- 真实 ContentSubmissionService、ForumContentWriteService、TranAop、CommentController 和内存 API 异常管道验证提交 / 回滚调用及后续失败。Controller 已消费结果直接断言 MessageModel 状态，本测试辅助管道不冒充 MVC 完整 HTTP 结果执行。
- SQLite 使用真实唯一约束、BaseRepository 与 UnitOfWorkManage；通过受控首次查询不可见制造冲突，验证 SAVEPOINT → ROLLBACK TO → RELEASE 顺序、外层此前写入保留、后续仍可写入，以及外层提交 / 回滚的实际结果。不是并发竞态验证。
- 首轮 120 项通过，1 个 PostgreSQL 用例因未配置连接跳过；修正新增测试的取消令牌分析器提示后，最终筛选显式排除该 PostgreSQL 用例，**120 / 120** 通过、无跳过。Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误。变更文件卫生、文档检查与 `git diff --check` 通过；records 索引 318 行触发非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter '(FullyQualifiedName~ContentSubmissionLoggingTests|FullyQualifiedName~ContentSubmissionServiceTest|FullyQualifiedName~ForumContentWriteServiceTest|FullyQualifiedName~ForumContentWriteTransactionIntegrationTest|FullyQualifiedName~UnitOfWorkManageTest|FullyQualifiedName~CommentControllerLoggingTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests)&FullyQualifiedName!~ExecuteInSavepointAsync_Should_Recover_PostgresTransaction'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。PostgreSQL、真实宿主、并发请求及浏览器未验收。下一批优先 PostController 编辑 / 置顶最终消费，随后核对 QuestionController；L2 尚未整体完成。
