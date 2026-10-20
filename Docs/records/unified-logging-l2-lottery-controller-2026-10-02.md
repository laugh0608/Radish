# L2 抽奖 Controller 最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 LotteryController 查询 / 手动开奖及 PostLotteryService 对应 ID 校验分类，同时验证自动开奖的既有故障归属。

## 实现与边界

- 项目所有者单独确认新增 LotteryInputValidationException，继承 ArgumentException；仅替换查询 / 手动开奖的两处非正帖子 ID 检查，保留 Message、ParamName 和 400 响应。自动开奖仍抛原 ArgumentException，不把无效任务数据改为安静输入拒绝。
- 两个 HTTP 入口在原响应成功构造后记录普通参数故障 `lottery.request_failed` Error，或 5xx BusinessException 的 `http.failed` Error；属性仅 failureKind，或 statusCode / failureKind。不输出异常对象 / 原文、用户 / 帖子 / 抽奖身份、评论快照、奖品或通知载荷。
- 明确参数拒绝、登录 / 模型预检、4xx 业务拒绝及正常成功安静。状态、原消息、错误码 / 消息键与不复制 MessageArguments 的行为保留。Message getter 使构造失败时不提前记录；未消费异常交给 API。
- Controller 不新增聚合异常解包，TranAop 原有单一聚合解包保持。服务不新增 catch / 日志，自动任务继续由 PostLotteryJob 输出一条既有批次摘要；任务中的 4xx 业务异常仍计入失败，扫描异常仍原样传播。
- 参与池、作者去重与最早评论选择、中奖人数、快照、手动一小时门槛和截止规则、重复开奖拒绝、UTC 审计、通知任务与返回结果不变。自动按配置截止时间筛选与留痕，手动按当前时间；手动空池 409，自动空池完成结算且不要求通知 Outbox。
- 中奖记录、开奖状态、通知 Outbox 和结果刷新继续位于原事务内。依赖故障或详情不可见仍共同回滚；不新增并发保护、补偿、幂等成功或可靠性规则。

## 新发现的既有时间边界

SQLite 读取自动开奖 DrawTime 后为 `DateTimeKind.Unspecified`。PostLotteryService 将该值用于中奖记录、通知载荷和 Outbox 入队；ReliableOutboxRepository 对非 Utc 值调用 ToUniversalTime。本机 Asia/Shanghai 测试中，Outbox 信封 OccurredAtUtc 比中奖 / 通知载荷时间提前 8 小时。应用 SqlSugarSetup 未配置 SQLite 读回 Kind 修复，PostgreSQL 参数规范化也不作用于此处。

这与[时间语义契约](../guide/time-semantics.md)存在缺口。本批只锁定当前调用行为和失败所有权，不修改时间值、已有数据或仓储规范化。后续需独立确认修复范围，并分别验证 SQLite / PostgreSQL 与不同时区；本次未访问生产，不能推断生产数据已受影响。

## 验证

- 新增 6 组 × 4 模式：旧 / 候选、Development / Production；候选显式配置 mode，Development 开启 diagnostics。核对事件、等级、属性集合及敏感哨兵，包含不可 ToString / 不可读取 Message 的异常。
- 真实 PostLotteryService、PostService、ForumProfile、BaseRepository、ReliableOutboxService / Repository、UnitOfWorkManage / TranAop、PostLotteryJob；SQLite 持久化 Post、PostLottery、PostLotteryWinner、Comment、ReliableOutboxMessage。HTTP 使用内存 API 中间件和结果过滤器，不启动宿主。
- 两个 HTTP 入口覆盖 400 / 403 / 404 / 409 / 500 / 503 业务响应、参数故障、IO / 超时 / 取消 / 未知故障、聚合异常及响应构造失败。明确 ID 类型仅用于 HTTP 两处，自动无效 ID 仍为普通参数故障。
- 业务拒绝覆盖模型 / 登录预检、帖子缺失 / 删除 / 未发布 / 禁用、抽奖缺失 / 删除、作者权限、已开奖、缺少截止时间、手动过早 / 已到截止、手动空池与自动过早。禁用帖子在写入后刷新不可见时仍 404，并回滚已写入 Outbox。
- 阶段故障覆盖帖子 / 抽奖 / 评论 / 中奖读取、时钟、中奖插入、开奖更新、通知入队和详情刷新。专门在真实 Outbox AddAsync 完成后再抛错，核对中奖、开奖状态和 Outbox 回滚；直接服务保持传播，HTTP 和 Job 各自承担最终日志。
- 成功路径核对父评论筛选、作者排除、截止时刻包含 / 截止后排除、同作者按时间及 ID 取最早、99 人上限收敛到实际 2 人、0 人配置下限为 1、500 字快照、姓名 / 标题 / 奖品空白回退、审计、匿名查看及重复开奖 409。通知接收者、业务键 / 任务键、优先级、模板、目标、身份和发生时间保留。
- 自动任务覆盖正常 / 空池批次、无到期任务安静、无效任务 ID、业务 4xx 失败、扫描异常，以及第一项晚期故障回滚后第二项继续提交。批次 processedCount 保持原成功数语义。
- 辅助标签 / 投票 / 问答及未使用的业务依赖为 mock；不代表 PostgreSQL、并发开奖、认证 / MVC 模型绑定、真实 HTTP 宿主、通知投递或浏览器验收。
- 测试准备中修正了 SqlSugar 时间表达式、SQLite 日期写入格式及非空 PublishTime 夹具，按现有 TranAop 单一聚合解包和异常 Message 不可读边界调整断言；自动 Outbox 的既有时区差异按上节单独记录。另有一次 MSBuild 命名管道被沙盒拒绝，已退出并以获准的定向命令重跑。
- 定向 .NET 回归 **103 / 103** 通过，无跳过；Node 日志契约 **27 / 27** 通过。API `--warnaserror` 构建通过，0 警告、0 错误。
- 10 个变更文件卫生、全量文档及 `git diff --check` 通过；records 索引 323 行有非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~LotteryControllerLoggingTests|FullyQualifiedName~PostLotteryServiceTest|FullyQualifiedName~PostServiceTest|FullyQualifiedName~BusinessJobLoggingTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests' -m:1 -p:UseSharedCompilation=false
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动宿主 / 数据库服务 / 容器、访问生产或切换日志开关。下一项为 TagController / TagService 标签创建与更新的最终消费；L2 尚未整体完成。
