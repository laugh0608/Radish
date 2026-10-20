# L2 标签创建 / 更新最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 TagController 创建 / 更新及 TagService 两处重名拒绝分类，基于 `dev / a6ca551d` 继续；项目所有者确认方案后实施。

## 实现与边界

- 新增 `TagNameConflictException : InvalidOperationException`，仅用于现有未删除标签重名检查。保留原“标签名称已存在”消息和父类消费关系，不根据消息文本识别业务拒绝。
- TagController 先构造原响应，再为其他 InvalidOperationException 输出一条 `tag.request_failed` Error；仅传 failureKind，不传异常对象 / 原文、标签字段、操作人身份或请求载荷。普通同类异常即使消息也是“标签名称已存在”，仍按故障记录。
- 明确重名、模型 / ID 预检、目标缺失与成功保持安静。HTTP 状态、响应消息和结果过滤器补默认错误码 / 消息键的行为保留；普通 InvalidOperationException 仍返回 400 + 原消息，日志裁剪不改写业务响应。
- 其他异常继续传播至 API，响应 Message getter 抛错时不提前输出标签 Error。Service 不新增 catch / 日志，Controller 不新增聚合异常解包；GetOrCreateTagAsync 和其他标签入口未改。
- 名称去空白、禁用标签参与重名检查、已删除标签排除、更新排除自身、slug 规范化 / 后缀 / 长度规则、字段与审计写入保持。没有新增事务、并发保护、重试或补偿。
- 标签创建 / 更新原本没有事务特性；测试通过真实 TranAop 验证该路径仍直接执行。写入前故障保留原数据；在真实 AddAsync / UpdateAsync 完成后注入故障，已写入行保留，HTTP 失败不代表回滚。这是既有可靠性边界，不是本批新增行为。

## 验证

- 新增六组 × 四模式：旧 / 候选日志、Development / Production；候选显式设置模式，Development 开启 diagnostics。核对事件码、等级、属性集合、日志唯一归属和敏感哨兵，覆盖不可 ToString / 不可读取 Message 的异常。
- 真实 TagService、ForumProfile、BaseRepository、UnitOfWorkManage / TranAop 与内存 SQLite Tag 表；ITagDiscoveryRepository 为未使用的严格 mock。HTTP 通过内存 API 中间件和结果过滤器，不启动宿主。
- 重名覆盖启用 / 禁用标签、名字去空白和更新排除自身；预检覆盖模型无效、非正 ID、更新目标缺失 / 已删除和仓储更新返回 false。
- 创建阶段覆盖重名查询、slug 查询和插入前故障；更新阶段另覆盖目标读取及更新前故障。直接 Service 保持原异常传播且安静，Controller 只消费 InvalidOperationException；IO 故障由 API 记录一次 http.failed。
- 实际 SQLite 插入 / 更新完成后注入 InvalidOperationException 或 IOException，分别核对直接传播、HTTP 400 / 500 与持久化行保留。成功路径验证名称、slug 碰撞、50 字符后缀收敛、已删除记录排除、固定中文标签映射、自身排除、System 操作人回退、描述 / 颜色裁剪及创建 / 修改审计字段。
- 未消费的参数、超时、取消、未知、聚合及 4xx / 5xx BusinessException 单独核对 API 归属；不把依赖抛出的所有异常都标成明确业务拒绝。

### 内存管道的框架边界

额外注入 `BusinessException(404)` 时，TagController 原样向外传播。当前内存 DefaultHttpContext 的 Response.HasStarted 不随 MemoryStream 写入变为 true，异常处理中间件又未设置 AllowStatusCode404Response，因此在写入 404 内容后重抛包装异常。该路径的旧 sink 收到框架原异常，候选 sink 仅输出 runtime.unclassified 安全摘要。测试将它独立列为框架来源未治理证据，没有修改中间件，也没有将其纳入标签安全日志通过范围。

这不影响标签“目标不存在”直接返回 MessageModel 的 404；当前 TagService 创建 / 更新也没有主动抛出 BusinessException(404)。该结果只证明未开始响应的内存管道分支，不代表真实宿主、实际响应已开始状态或生产可达性结论。

首轮测试发现上述重抛与框架日志，随后按实际行为补独立断言；结果过滤器测试辅助代码拆出回调，消除 ASP0016 分析警告。未为通过测试修改运行时框架边界。

### 命令与结果

- 定向 .NET 回归 **72 / 72** 通过，无跳过；其中新增标签日志回归 **24 / 24**。
- Node 日志契约 **27 / 27** 通过。
- API `--no-restore --warnaserror` 构建通过，0 警告、0 错误。
- 10 个变更文件卫生、全量文档及 `git diff --check` 通过；records 索引 324 行有非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~TagServiceTest|FullyQualifiedName~TagControllerTest|FullyQualifiedName~TagSlugHelperTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests' -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

本批不代表 PostgreSQL、并发写入、认证 / MVC 模型绑定、实际 HTTP 宿主或浏览器验收。未安装依赖、启动服务 / 容器或访问生产；日志开关仍为 false，L2 尚未整体完成。自动开奖 SQLite → Outbox 时间转换问题仍按[抽奖记录](./unified-logging-l2-lottery-controller-2026-10-02.md)独立保留，本批未修复。

下一项先核对 CategoryController 创建 / 更新的父分类不存在拒绝与依赖故障最终消费，实施前确认具体方案。本批按授权在 dev 本地提交，不推送。
