# L2 投票 Controller 最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 PollController 查询 / 投票 / 关闭和 PostPollService 的明确 ID 校验分类。

## 实现与边界

- 项目所有者单独确认新增 PollInputValidationException，继承 ArgumentException；只替换现有四处帖子 / 选项 ID 校验。Message、ParamName、父类消费与 HTTP 状态不变。
- 三个入口原重复的响应构造分别归入两个类型明确的私有方法，在构造成功后记录：普通 ArgumentException 为 `poll.request_failed` Error，5xx BusinessException 为 `http.failed` Error。只包含 failureKind 或 statusCode / failureKind，不包含异常对象 / 原文、用户 / 帖子 / 选项身份、问题和选项文本。
- 明确参数拒绝、登录 / 模型预检与 4xx 业务拒绝保持安静。原错误码、消息键、响应文案和不复制 MessageArguments 的行为保留；Controller 不新增 AggregateException 消费。原事务 AOP 单一聚合解包保持。
- Message getter 抛错时不提前生成 Error，由 API 处理响应构造失败；IO、超时、取消、InvalidOperationException 等未消费异常继续由 API 记录。
- 投票 / 关闭后调用 GetByPostIdAsync 刷新仍在原事务内。初查允许但详情不可见的帖子，仍会在写入后返回原 404 并回滚；普通刷新故障同样回滚。未改写入顺序、截止规则、权限、计数、审计或重复操作决策。

## 验证

- 新增 6 组 × 4 模式回归：旧 / 候选、Development / Production；候选显式 mode，Development 开启 diagnostics。验证安全等级 / 事件 / 属性及敏感哨兵，包含不可 ToString 和不可读取 Message 的异常。
- 三个 Controller 入口验证正常拒绝、400 / 403 / 404 / 409 / 500 / 503 BusinessException、参数故障、未消费异常与聚合异常。保留对外原文、错误码 / 消息键与结果过滤器 HTTP 状态。
- 真实 PostPollService、PostService、ForumProfile、BaseRepository、UnitOfWorkManage / TranAop，SQLite 持久化 Post、PostPoll、PostPollOption、PostPollVote；API 使用内存中间件及结果过滤器，不启动宿主。
- 阶段故障覆盖帖子 / 投票 / 选项读取、重复检查、时间读取、记录插入、选项 / 总数更新和详情刷新依赖。核对失败后的投票记录、计数、关闭标记和审计字段回滚，直接服务调用保留原异常实例且不提前记录。
- 验证非正 ID、模型 / 登录预检、缺失 / 删除 / 不可见帖子、缺失投票 / 选项、其他投票选项、已截止 / 截止时刻、重复投票与非作者关闭。不可见帖子在写入后的刷新阶段仍按原 404 回滚。
- 成功路径核对投票 / 关闭返回、选项顺序与比例、总数回退、用户已投状态、匿名状态、UTC 审计和空名称回退。再次投票 / 关闭保持 409，不新增写入。隐藏前置查重结果后触发真实 SQLite 唯一约束，确认原数据库故障由 API 记录且未改变计数；该情形不等于并发请求验收。
- 辅助标签、问答与未使用的业务依赖为 mock。未验收 PostgreSQL、真实并发、认证 / MVC 模型绑定或浏览器。
- 定向 .NET 回归最终 **88 / 88** 通过、无跳过；Node 日志契约 **27 / 27** 通过。测试准备阶段修正标签替身空集合与 SqlSugar 时间表达式取值；固定本轮 UTC 快照，避免预设日期过期造成假失败，生产规则未改。
- API `--warnaserror` 构建通过，0 警告、0 错误。10 个变更文件卫生、全量文档及 `git diff --check` 通过；records 索引 322 行触发非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PollControllerLoggingTests|FullyQualifiedName~PollControllerTest|FullyQualifiedName~PostPollServiceTest|FullyQualifiedName~PostServiceTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。下一项为 LotteryController / PostLotteryService 抽奖消费边界；L2 尚未整体完成。
