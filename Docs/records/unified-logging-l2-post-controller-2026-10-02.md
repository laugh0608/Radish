# L2 帖子编辑 / 置顶最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 PostController 编辑 / 置顶、PostService 直接拒绝与 ForumContentWriteService 编辑消费链。内部异常分类方案经项目所有者明确确认后实施。

## 实现与边界

- 新增 PostContentValidationException（ArgumentException 子类），仅用于编辑空标题 / 正文、长度与标签规则；PostOperationRejectedException（InvalidOperationException 子类）仅用于帖子缺失、编辑上限、分类不可用。消息、参数名、父类消费契约保留；系统设置矛盾及仓储 / 依赖故障不改成正常拒绝。
- 编辑原 ArgumentException → 400、InvalidOperationException → 403，置顶原 InvalidOperationException → 404 保持。明确拒绝安静，其余被消费异常生成一次 `post.edit_failed` 或 `post.top_failed` Error，仅带 failureKind；原文、用户 / 租户 / 帖子 ID、内容、提交键、摘要及异常对象不进入日志。
- catch 外的编辑前置查询、置顶 ArgumentException 与两条链的 IO / 超时 / 取消 / 5xx BusinessException 等仍由 API 最终边界记录；正常业务 4xx 保持安静。并未修订旧有错误状态码或响应 MessageInfo。
- 权限、分类 / 标签计数、CAS、审计、提交台账、版本追加、重放 / 重复 / 无变化分支及事务顺序不变。置顶读取详情仍在事务内；更新后详情不可见也按原拒绝回滚。
- PostService 的标签校验辅助方法虽位于 Publish partial 文件，但只由编辑调用；发布仍走独立校验方法，本批不改发布契约。
- **后续边界**：ForumContentRevisionService.RestorePostAsync 仍将参数 / 操作异常转换为 409，并按消息选择部分错误码。此次保留该父类 catch 的响应兼容，未关闭其故障日志归属；随后治理 QuestionController 及框架来源。L2 尚未整体完成。

## 验证

- 新增 6 组 × 4 模式回归：旧 / 候选、Development / Production；候选显式设置 mode，Development 开启 diagnostics。检查单一 Error、事件码、受控属性及敏感哨兵，未消费异常额外覆盖 Message / ToString 不可读取的异常。
- 真实 PostService、ForumContentWriteService、TranAop、Controller、结果过滤器与内存 API 管道；mock 仓储 / 台账 / 版本验证明确拒绝、配置错误、12 个编辑故障阶段、置顶故障、原实例传播、前置查询、200 / 400 / 403 / 404 / 409 / 429 / 500 / 503 与提交 / 回滚调用。
- 成功编辑核对内容修剪、版本 / 编辑次数、操作者 / 时间、分类 / 标签计数、关系迁移、版本 → 台账完成 → 提交顺序；重放 / 重复 / 无变化不重写；重复置顶不重写，取消置顶仍生效。
- SQLite 使用真实 Post 表、BaseRepository、UnitOfWorkManage，验证置顶成功提交，详情读取故障或帖子不可见回滚，编辑台账完成故障回滚帖子内容与版本。分类 / 标签、台账和版本依赖仍为 mock，不宣称多表联合持久化或真实并发验证。
- 初次编译修正测试中的版本摘要类型名；首轮行为测试 130 项通过、4 项因已发布测试帖缺少 PublishTime 而在 SQLite 准备阶段失败。补齐夹具后最终 **134 / 134** 通过、无跳过；Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误。13 个变更文件卫生、全量文档检查与 `git diff --check` 通过；records 索引 319 行触发非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PostControllerLoggingTests|FullyQualifiedName~PostServiceTest|FullyQualifiedName~PostControllerTest|FullyQualifiedName~ForumContentWriteServiceTest|FullyQualifiedName~ForumContentRevisionServiceTest|FullyQualifiedName~ForumContentWriteTransactionIntegrationTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。PostgreSQL、真实宿主、并发请求与浏览器未验收。下一项为帖子版本恢复的最终异常消费。
