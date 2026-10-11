# L2 早期消费点错误结果与日志所有权（2026-10-11）

## 授权与范围

项目所有者先确认只读复核，再批准先提交工作区既有更改，实施早期轻回应 / 评论 / 帖子消费边界、轻回应通知内部异常分类，以及四模式回归与文档。既有证书监视 / 配置加载批次已提交为 `5919fc49`，与本批改动分开。

本批不安装或还原依赖、不启动项目服务、不访问生产；保留响应、事务和缓存边界。当前契约见[事件契约第 57 节](../features/unified-logging-contract.md#57-l2-早期消费点的错误结果构造与日志所有权)。

## 根因与实施

早期日志 catch 在读取 `ex.Message` 之前记录“已消费”事件；若该虚属性求值抛出新异常，原错误结果尚未完成，上层又可能输出最终失败。原恶意异常测试继承普通 Exception，绕过了实际读取 Message 的 ArgumentException / InvalidOperationException / BusinessException 消费分支，不能证明这些分支没有提前日志。

| 消费位置 | 本批调整 | 保留契约 |
| --- | --- | --- |
| 轻回应查询 | InvalidOperationException 先构造结果再 Warning | 原 404 与 quick_reply.query_rejected |
| 轻回应创建 / 删除 | BusinessException 先构造结果，5xx 才 Error | 原状态 / 文案 / Code / MessageKey，4xx 静默 |
| 评论创建 / 点赞 / 编辑 | 四个 catch 先构造结果再 Error | 原 400，专用拒绝静默 |
| 帖子编辑 / 置顶 | 三个 catch 先构造结果再 Error | 编辑 400 / 403、置顶 404，专用拒绝静默 |
| 评论编辑内容校验 | 先构造 `(false, message)` 再 Error | 失败元组、写入拒绝与版本恢复响应映射 |
| 轻回应通知 helper → Create | 专用异常传递阶段，Create 构造 400 后记录 | 原通知事件、父类消费与事务回滚 |

共调整 10 个原有 Controller 日志 catch、1 个服务返回元组 catch，并将通知 ArgumentException 的日志从 helper 移至 Create 的既有参数异常 catch。不新增事件编码或改变日志级别 / 属性白名单。

`PostQuickReplyNotificationEnqueueException : ArgumentException` 只标识通知阶段，原异常保留为 InnerException，Message 委托给原异常。包装时不求值消息，也不记录；事务代理先按原规则回滚，Controller 才读取原消息并构造 400。该分支的传播实例改变；其他异常仍保持原实例，直接调用服务不再提前生成通知日志。保留服务构造函数签名，移除不再使用的 logger 字段。

通知消息读取失败或回滚本身失败时，helper 没有提前事件；实际到达的异常按既有 Controller / API 规则消费。评论服务求值失败的新 ArgumentException / InvalidOperationException 仍可能由外层编辑 catch 消费，不能一律称为 API 500。

## 四模式与行为证据

测试覆盖旧 / 候选 × Development / Production，复用真实服务、事务代理、结果过滤器及内存 API 管道：

- 被消费的参数 / 操作 / 业务异常在 Message 求值时抛出 IOException，本层没有原消费事件，API 完成 500 响应后输出单个安全 http.failed。
- 评论失败元组只求值一次；若求值抛出普通参数 / 操作异常，由外层编辑 catch 保留 400 并记录一次；普通设置依赖失败仍由服务记录一次。
- 真实评论版本恢复消费者保留普通依赖拒绝的 409 与正常长度拒绝静默；不可读消息传播到 API，且不写评论列、不追加版本，事务代理调用回滚。
- 轻回应直接服务调用不记录通知事件，不提前读取 Message，包装保留原异常；Controller 在回滚后、日志前读取一次消息。回滚失败也不触发原消息求值或通知日志。
- 原正常成功、业务拒绝、Code / MessageKey、软删除审计、台账 / Outbox / 版本顺序与 SQLite 事务用例继续回归；轻回应已写入的两个缓存标记仍不会随数据库回滚撤销。
- 安全断言检查事件数、级别、固定 failureKind、允许的属性，以及业务原文 / 身份 / 缓存键不进入运行日志。

本批新增 7 个四模式理论测试，共 28 个测试实例；每个实例内覆盖多条失败路径。测试中直接捕获不可读参数异常，避免断言库自身求值 Message 混入生产路径观测。

## 验证结果

- 定向 `PostQuickReplyLoggingTests / CommentControllerLoggingTests / PostControllerLoggingTests`：**100 / 100 通过**，含新增 28 个四模式实例。
- 相关服务、Controller、内容写入 / 版本恢复、评论实时推送、提交台账及 HTTP 错误边界回归：**329 / 329 通过**，包含上述 100 项。既有 SQLite 事务与 API 错误 JSON 写入失败用例通过。
- `dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`：**0 warning / 0 error**。
- `npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。仅保留 logging.md 与 records/index.md 的既有软篇幅提醒。

测试命令使用 `--no-restore`；完成构建后的扩展回归加 `--no-build`。初次共享编译构建缓慢，精确中断后禁用共享编译；沙盒构建成功，但 VSTest 本机通信端口遭 Permission denied，随后按最小范围提权执行测试。该权限只用于测试进程通信，业务测试使用内存管道，没有启动项目服务。

可复现的扩展回归筛选：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --no-build --filter 'FullyQualifiedName~PostQuickReply|FullyQualifiedName~CommentControllerLoggingTests|FullyQualifiedName~PostController|FullyQualifiedName~PostServiceTest|FullyQualifiedName~PostRestoreLoggingTests|FullyQualifiedName~ForumContentWrite|FullyQualifiedName~ForumContentRevisionServiceTest|FullyQualifiedName~ContentSubmissionLoggingTests|FullyQualifiedName~CommentRealtimeLoggingTests|FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest' -m:1 -nr:false
```

## 边界与后续

日志只证明本层错误结果构造完成，不证明 HTTP 响应写入完成。服务已完成失败元组或 Controller 已返回结果后，过滤器、序列化或传输的独立故障仍按既有 API / 框架边界记录，不新增全请求去重。API 错误处理中间件本身仍在错误 JSON 写入成功后记录。

保留轻回应回滚后的缓存标记、评论高亮部分写入等既有独立问题；本批不新增重试、补偿、可靠性保证或业务规则调整。内存管道、mock 事务轨迹与既有 SQLite 用例不代表 PostgreSQL、Redis、项目宿主、网络传输或生产验收。

生产候选开关继续关闭，L2 未整体关闭。Socket / QUIC transport、Http.Connections、协议 resolver 等框架来源后续先只读核对并确认范围。
