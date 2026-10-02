# L2 问答 Controller 最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 QuestionController 九个入口与 ForumQuestionService 的明确内容拒绝分类。

## 实现与边界

- 项目所有者单独确认新增 ForumAnswerContentValidationException，继承 ArgumentException；只替换创建 / 编辑回答的空内容与超过 20000 字符检查。原 Message、ParamName、父类消费和 HTTP 状态保持；没有修改恢复正文或其他业务规则。
- BuildErrorResponse 成功构造原响应后，普通 ArgumentException 记录 `question.request_failed` Error，仅带 failureKind；5xx BusinessException 记录 `http.failed` Error，仅带 statusCode / failureKind。明确内容拒绝和 4xx BusinessException 保持安静。
- 原三个 catch 组及 AggregateException 解包 / 过滤器行为保留。单一已知异常走原消费，多异常、未知或空聚合继续传播；Message getter 若使响应构造失败，Controller 不先记录，交 API 最终处理。错误码、消息键及消息参数规范化不变。
- 运行日志不包含异常对象 / 原文、回答 / 帖子 / 用户身份、正文、提交键或摘要。现有 API 对外错误文案不属于本次日志载荷变更。
- 保留回答与采纳 CAS、软删除、不可变版本、附件绑定、UTC 审计、提交台账、通知载荷与事务边界。原无内容变化分支、采纳状态无变化分支及各自台账语义未改，不扩为可靠性修复。

## 验证

- 新增 6 组 × 4 模式回归：旧 / 候选、Development / Production；候选显式 mode，Development 开启 diagnostics。验证单一事件、等级 / 属性与敏感哨兵，包含 ToString 不可调用和 Message getter 抛错的异常。
- 九个入口均覆盖直接和嵌套单一聚合的普通参数异常、明确内容拒绝、4xx / 5xx BusinessException；未知、IO、超时、取消、InvalidOperation、多个叶异常及空聚合只由 API 记录。兼容响应原文、消息参数规范化及结果过滤器的真实 HTTP 状态。
- 六个写入操作使用真实 ForumQuestionService、ForumQuestionRepository、ContentSubmissionService、ReliableOutboxService、TranAop、Controller 与内存 API 管道。SQLite 持久化 Post、PostQuestion、PostAnswer、PostAnswerContentRevision、PostAnswerAcceptanceEvent、Attachment、ForumContentRevisionAttachment、ContentSubmissionRecord、ReliableOutboxMessage。
- 按操作覆盖问题读取、提交快照、台账开始 / 完成、写入、附件绑定、通知与作者资料填充故障；核对回答正文 / 版本 / 软删除、回答计数 / 采纳状态、版本、附件引用、台账和 Outbox 回滚。通知写入后抛出的普通错误、4xx / 5xx 和单一聚合也验证整笔回滚；直接服务调用保留原异常实例，不提前记录。
- 成功与再次调用验证创建 / 编辑 / 恢复内容、来源版本、软删除审计、采纳 / 撤销事件、附件引用、台账完成、通知接收者 / 业务键 / 目标 / UTC 时间，重放不新增记录。另验正常内容、缺失、权限、已采纳、CAS、提交键、附件、版本拒绝及分页 / 版本读取。
- 交互策略和作者头像依赖为 mock；SQLite、内存 API 管道不代表 PostgreSQL、真实并发、认证 / MVC 模型验证、HTTP 宿主、附件存储或通知投递验收。
- 定向 .NET 回归最终 **106 / 106** 通过、无跳过；Node 日志契约 **27 / 27** 通过。开发过程中修正了测试类型引用、预期错误码及 SQLite 准备数据的更新条件；未因此调整生产规则。
- API `--warnaserror` 构建通过，0 警告、0 错误。10 个变更文件卫生、全量文档及 `git diff --check` 通过；records 索引 321 行触发非阻断篇幅提示。
- 一次测试重跑遇到 MSBuild 本机管道的沙盒权限限制，提权重跑通过；该失败命令残留进程已精确结束。未启动应用服务。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~QuestionControllerLoggingTests|FullyQualifiedName~QuestionControllerTest|FullyQualifiedName~ForumQuestionContractTest|FullyQualifiedName~ForumQuestionRepositoryTest|FullyQualifiedName~ForumContentWriteServiceTest|FullyQualifiedName~ContentSubmissionServiceTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。下一项为 PollController / PostPollService 投票消费边界；L2 尚未整体完成。
