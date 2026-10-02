# L2 帖子版本恢复最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 ForumContentRevisionService.RestorePostAsync、PostService 更新、ForumContentWriteService 恢复与 PostController 恢复入口。

## 实现与边界

- 恢复服务原 catch 只包围 PostService.UpdatePostAsync。普通 ArgumentException / InvalidOperationException 成功转换为原 409 BusinessException 后，新增 `post.restore_failed` Error，仅带 failureKind；复用已确认的 PostContentValidationException / PostOperationRejectedException 区分正常拒绝，不新增异常类型。
- 原 Message、ContentRejected / EditLimitReached、消息键及按“次数”匹配错误码的规则保留。日志不包含异常对象 / 原文、正文 / 标题、用户 / 帖子 / 版本 / 附件 ID、提交键与摘要；并未修改对外错误响应文案。
- 先完成原异常转换，再记录已消费故障；若异常 Message getter 自身抛错，保持该失败传播，API 最终记录，恢复边界不提前生成重复 Error。正常业务拒绝和原 4xx BusinessException 安静；5xx BusinessException 及未消费的 IO / 超时 / 取消等仍由 API 记录。
- 授权、版本 / 分类 / 标签 / 附件预检在 catch 前；快照追加与台账完成在 catch 后。上述阶段的普通故障保留原实例传播，不被重归为 409。Controller 无新增日志。
- 恢复正文 / 标题 / 封面、CAS、编辑次数、不可变版本、来源版本关联、标签快照、附件引用、UTC 审计、重放 / 重复及事务边界不变。评论恢复不在本批改动范围。

## 验证

- 新增 5 组 × 4 模式回归：旧 / 候选、Development / Production；候选显式 mode，Development 开启 diagnostics。核对单一 Error、等级 / 事件码 / 属性、敏感哨兵，包含 ToString 不可读取及 Message getter 失败的异常。
- 真实 PostService、版本服务、写入服务、TranAop、Controller、结果过滤器与内存 API 管道，覆盖 17 类恢复 / 更新拒绝、提交台账拒绝、更新阶段异常转 409、配置错误、13 类前后阶段故障、原实例传播及 4xx / 5xx BusinessException 归属。
- 成功恢复核对标题 / 正文 / 封面、版本 / 编辑次数、来源关联、旧快照保留、标签与正文 / 封面附件引用、UTC 时间、操作者与快照 → 引用 → 台账 → 提交顺序；重放 / 重复保持 200，不新增写入。
- SQLite 使用真实 Post、PostContentRevision、PostContentRevisionTag、ForumContentRevisionAttachment 表及 BaseRepository / UnitOfWorkManage；验证成功四类记录提交，更新阶段被转换故障、引用追加故障及台账完成故障回滚。分类 / 标签主数据和提交台账仍为 mock；不宣称全链持久化、真实并发或 PostgreSQL 验收。
- 定向 .NET 回归首轮 **106 / 106** 通过、无跳过；Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误。8 个变更文件卫生、全量文档与 `git diff --check` 通过；records 索引 320 行触发非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PostRestoreLoggingTests|FullyQualifiedName~PostControllerLoggingTests|FullyQualifiedName~ForumContentRevisionServiceTest|FullyQualifiedName~ForumContentWriteServiceTest|FullyQualifiedName~ForumContentWriteTransactionIntegrationTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。PostgreSQL、真实宿主、并发请求及浏览器未验收。下一项为 QuestionController 参数 / 业务 / 聚合异常最终消费，L2 尚未整体完成。
