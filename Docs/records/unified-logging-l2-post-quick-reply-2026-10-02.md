# L2 轻回应通知入队与 Controller 日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 PostQuickReplyService 通知入队 helper、PostQuickReplyController 最终消费边界及直接调用链回归。

## 实现与保留边界

- 通知 helper 移除全异常原始 Warning，只有被 Create 转为 400 的 ArgumentException 在通知阶段生成 `quick_reply.notification_enqueue_failed` Error，包含固定 failureKind 后原样重抛。IOException、超时、取消、InvalidOperationException 等继续到 API 最终边界生成一次 `http.failed`。
- Controller Create / Delete 消费的 5xx BusinessException 补齐 `http.failed` Error，仅带 statusCode / failureKind；4xx 业务拒绝保持安静。GetRecentByPostId 消费的 InvalidOperationException 生成 `quick_reply.query_rejected` Warning，继续返回既有 404。普通 ArgumentException 参数校验保持安静。
- 响应状态、业务错误码、消息键和原文案未改；日志不包含帖子 / 轻回应 / 作者 / 收件人 / 租户身份、通知键、内容、标题、头像、查询参数或原始异常文本。原响应文案边界不等同于运行日志脱敏。
- 创建保持内容 trim / 空白折叠、长度限制、权限和锁定检查、冷却 / 重复内容限制、插入 → 两个缓存标记 → 资料填充 → 通知入队顺序。通知收件人筛选、任务键、业务键、序列化载荷、UTC 时间及 Outbox 默认重试配置不变。
- **原事务 / 缓存边界**：通知入队失败继续上抛，真实 TranAop 调用 rollback；缓存标记此前已经写入，不会由数据库回滚撤销。失败后重试可能被冷却 / 去重拒绝，本批不引入补投、缓存补偿或创建成功重放。事务实际数据库效果仍需独立运行态验证。
- 删除仍按作者 / 管理员权限软删除，并保留 DeletedAt / DeletedBy / ModifyTime / ModifyBy / ModifyId 审计列。

## 验证

- 定向 .NET **61 / 61** 通过，无跳过：新增 PostQuickReplyLoggingTests 24 个四模式用例、既有 PostQuickReplyServiceTest 7 个用例及 RuntimeLogPolicyTests / RuntimeLoggingAdapterTests 30 个用例。
- 旧 / 候选 × Development / Production；候选显式配置 mode，Development 开启 diagnostics，核对事件码、等级、mode 及属性仅包含 failureKind 或 statusCode / failureKind。旧输出捕获 Verbose 以上日志并包含属性 / 异常槽，验证敏感哨兵不泄漏。
- 使用真实 Service、ReliableOutboxService、TranAop、Controller、ApiErrorContract 结果过滤器与内存 API 异常管道，mock 仓储 / 缓存。验证通知完整载荷、UTC 与顺序、正常提交、失败回滚调用、原异常实例传播、单一日志归属、冷却 / 去重、作者自回 / 无收件人跳过及删除审计列。
- 注入入队阶段的参数、操作、IO、超时、取消、禁止读取文本的异常、503 / 409 BusinessException；覆盖插入 / 缓存 / 资料 / 发布权限故障、缺少 Outbox、功能禁用、无效查询配置、正常校验及业务拒绝。
- 首轮因断言库读取刻意构造的异常 Message 导致 4 个用例失败，改为直接捕获并核对实例后全组通过；同时修正测试分析器提示和 diagnostics 配置键。未因此调整业务代码。
- Node 日志契约 **27 / 27** 通过。
- API 错误契约补验 **8 / 8** 通过，无跳过；API `--warnaserror` 构建通过，0 警告、0 错误。9 个变更文件卫生、全量文档与 `git diff --check` 通过；记录索引 316 行仅有非阻塞篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PostQuickReply|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~ApiExceptionHandlerTests'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ApiErrorContractTest'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

主测试筛选中的 ApiExceptionHandlerTests 无匹配项，API 错误契约实际类名为 ApiErrorContractTest，按上方第二条命令单独补验。未安装依赖、启动宿主 / 数据库 / 容器、访问生产或切换日志开关。mock 事务与内存缓存不代表真实 SQLite / PostgreSQL 回滚、Redis TTL 或浏览器 / HTTP 宿主验收。

## 下一批与剩余事项

- 下一批核对 CommentController 创建 / 点赞 / 编辑静默消费的 ArgumentException / InvalidOperationException，结合 ForumContentWriteService 与 CommentService 区分正常校验和阶段性故障，保留返回、幂等、写入与推送行为。
- SignalR 框架来源、其余业务链与异常安全栈帧继续治理。轻回应缓存补偿及评论高亮部分写入可靠性改造不在本批范围；生产开关保持关闭，L2 尚未整体完成。
