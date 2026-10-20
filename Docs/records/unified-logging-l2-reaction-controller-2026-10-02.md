# L2 表情回应最终消费与冲突重试日志治理

日期：2026-10-02（Asia/Shanghai）。基于 `dev / a9e5c898` 继续，项目所有者已确认 ReactionController 三入口最终消费及 ReactionService 原重试 Warning 的具体方案。

## 实现与边界

- GetSummary / BatchGetSummary / Toggle 消费的 5xx BusinessException 在原响应成功构造后生成一次 `http.failed` Error，只含 statusCode / failureKind。4xx 业务拒绝、模型预检和正常成功安静；原消息、错误码保留，MessageKey / MessageArguments 继续不复制。
- 未消费异常由 API 最终处理；Controller 不新增 catch、聚合解包或响应转换。读取当前用户时的 BusinessException 仍处于原 catch 内；异常 Message getter 导致响应构造失败时，Controller 不提前输出 Error。
- Service 既有冲突 Warning 改用登记事件 `reaction.retrying`，仅保留受控 failureKind。旧 / 候选日志都不接收异常对象、原文、目标 / 用户标识、表情值或贴纸路径。Warning 表示已选择重试，不能当作已成功或已提交的证据。
- 保留 IsUniqueConstraintConflict 的异常 ToString 与三个原有匹配标记；日志分类器不据原文作判断，也不额外渲染异常。重试次数仍为一次；第二次唯一冲突返回原 409 ConcurrentConflict，第二次普通故障继续传播，不新加本地 Error。
- 不改变单目标 / 批量查询规范化、有效 ID 去重、100 目标上限、空目标键、回应分组 / 排序 / 当前用户标识和缩略图解析；汇总仍不验证目标存在性或可见性。
- Toggle 保留 Post / Comment 可用性与贴纸校验、10 种回应上限、字段规范化、审计身份和 UTC 时间。添加、取消、原恢复分支及最终汇总仍处于同一 Required 事务；实际写入后失败或刷新失败继续回滚。

## 独立发现：已删除回应恢复偏差

- 真实 ReactionService → BaseRepository → SQLite 中，添加成功、取消成功后，再次添加返回 409。
- 原因：Reaction 实现 IDeleteFilter，通用 QueryFirstAsync 固定通过 CreateTenantQueryable 排除已删除记录；Service 再附加 IsDeleted 条件也无法取回已删除行。
- 随后的 Add 与旧行唯一键 `(UserId, TargetType, TargetId, EmojiValue)` 冲突，重试一次再次冲突；事务回滚，旧行保持已删除状态。Post / Comment、unicode / sticker 的四种组合均验证此现状。
- 此行为与 Phase 2 文档中的恢复目标存在偏差，已在专题中补充已知实现限制。本批不改变仓储公共接口、软删除查询或恢复逻辑，不把安全重试日志视为功能修复；后续需独立说明方案并确认。
- 为验证原 Service 恢复代码没有漂移，测试另以显式代理提供已删除行，继续使用真实 UpdateColumnsAsync / SQLite 事务验证字段恢复及晚期故障回滚。该受控分支不能作为通用仓储支持恢复的证据。

## 定向回归

- 新增七组 × 四模式（28 项）：旧 / 候选日志 × Development / Production；内存宿主与候选日志模式显式一致，Development 开启 diagnostics，旧路径捕获 Verbose 以上输出。
- Controller 消费矩阵包含三个入口、服务 / 当前用户访问故障、400 / 401 / 403 / 404 / 409 / 429 / 500 / 502 / 503，及参数、IO、超时、取消、不可渲染异常、聚合异常和不可读 Message。核对原响应与单一最终消费；敏感哨兵、内层异常、Data、消息键和参数均不进入日志。
- 真实 ReactionService、BaseRepository、TranAop、UnitOfWorkManage、SQLite 五表：Reaction、Post、Comment、StickerGroup、Sticker；附件 URL resolver 使用 mock，仓储代理仅用于阶段故障或明确标注的受控查询。
- 覆盖参数 / 目标 / 贴纸 / 容量拒绝，单目标与批量分组、有效 ID 去重、空键、匿名标记、缩略图、正常添加 / 取消的真实字段和审计时间。
- 故障注入覆盖目标 / 分组 / 贴纸读取、回应预读 / 计数、插入前后、取消写入前后、汇总查询与缩略图；IO 与已消费的 503 分别验证最终日志、原状态及数据库回滚。刷新返回 409 时同样回滚且安静。
- 通过隐藏预读触发真实唯一约束：第一次冲突后看见已有回应时，重试沿原语义取消旧回应；持续隐藏时两次插入冲突转 409，数据库回滚。此为受控冲突，不代表真实并发验收。
- 合成冲突覆盖一次重试成功、二次冲突、二次 IO / 503 及写入成功后刷新失败。原分类器读取冲突 ToString 一次，日志不再次渲染；重试后依赖故障为一条 Warning 与一条最终 Error，且重试事件上下文不会泄漏到最终 HTTP 日志。
- 初轮修正了测试种子的必填 PublishTime 和夹具更新的 Where 条件；未为测试通过调整业务行为。一次沙盒重跑因 VSTest 本机通信端口绑定被拒绝而中止，随后按既有授权提权执行定向测试。

## 验证命令与结果

- 定向 .NET 回归 **73 / 73** 通过，无跳过；其中新增 Reaction 四模式回归 **28 / 28**。
- Node 日志契约 **27 / 27** 通过。
- API `--no-restore --warnaserror` 构建通过，0 警告、0 错误。
- 10 个变更文件卫生、全量文档与本地链接及 `git diff --check` 通过；records 索引 326 行有非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReactionControllerLoggingTests|FullyQualifiedName~ReactionControllerTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~ApiErrorContractTest' -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

本批仅使用内存 API / 结果过滤器，不启动宿主。不代表 PostgreSQL、真实并发、MVC 认证 / 模型绑定、附件存储或浏览器验收。未安装依赖、启动业务服务 / 容器或访问生产；RadishLogging.Enabled 保持 false，L2 尚未整体完成。按授权在 dev 本地提交，不推送；此前自动开奖 SQLite → Outbox 时间转换问题仍独立保留。

下一项为 StickerController 创建分组 / 单表情、批量保存 / 排序的异常消费，先核对明确拒绝、依赖故障和批量写入语义，再确认方案。
