# L2 分类创建 / 更新最终消费日志治理

日期：2026-10-02（Asia/Shanghai）。基于 `dev / e9ae513c` 继续，范围为 CategoryController 创建 / 更新的父分类拒绝、InvalidOperationException 消费和原有写入语义；项目所有者已确认具体方案。

## 实现与边界

- ResolveCategoryLevelAsync 是 Controller 私有方法；父分类缺失改为返回空层级，两个入口直接返回原 400 +“父分类不存在”。正常拒绝保持安静，不新增共享异常类型，不通过消息匹配区分拒绝与故障。
- 原 catch 内其他 InvalidOperationException 在原响应构造成功后输出一条 `category.request_failed` Error，只带 failureKind，不传异常对象 / 消息、分类名称 / slug / 描述、附件或操作人身份。即使依赖故障消息恰好为“父分类不存在”，也仍记录 Error。
- 状态、响应消息和结果过滤器补默认错误码 / 消息键的行为保留；依赖 InvalidOperationException 仍返回原 400。Message getter 抛错时不提前输出，交给 API 最终处理。
- 更新目标查询与 AutoMapper 映射位于 try 外，故障仍上抛；父分类查询 / 映射及写入位于原 try 内。其他异常、聚合和 BusinessException 不新增消费或解包，通用 Service / Repository 和 API 中间件未调整。
- 保留模型、非正 ID、自身父级与目标不存在预检；禁用父分类可用、已删除父分类不可用，顶级 0 及其余层级的 Math.Max 规则保持。名称去空白、slug 规则、描述保留空白、附件标识、排序 / 启用和审计字段保持。
- 更新依然忽略影响行数：预读之后目标被删除或软删除、实际更新 0 行，仍返回 200 / true /“更新成功”。创建 / 更新原本没有事务特性，真实仓储写入后再抛错时，数据仍保留；本批不引入事务、CAS、重试或补偿。

## 定向回归

- 新增七组 × 四模式：旧 / 候选日志、Development / Production；候选显式指定模式，Development 开启 diagnostics。核对事件码、等级、有限属性和敏感哨兵；覆盖不可 ToString、Message 不可读取、异常内层和 Data 载荷。
- 真实 BaseService<Category, CategoryVo>、BaseRepository<Category>、ForumProfile、UnitOfWorkManage / TranAop 和内存 SQLite Category 表。仓储与 mapper 代理仅注入阶段故障，其余调用透传；IAttachmentUrlResolver 为未使用的严格 mock。
- 父分类缺失 / 已删除 / 非正标识、模型无效、更新 ID 无效 / 自身父级 / 目标缺失或已删除均验证安静拒绝、原响应和无写入。
- 创建覆盖父分类读取、映射、插入前故障；更新另覆盖目标读取 / 映射，以及更新前故障。普通 InvalidOperationException 在 try 外由 API 记录 http.failed，在 try 内由 Controller 记录 category.request_failed；IO 等未消费故障均由 API 处理。
- 不可读取 Message 导致响应构造失败时只记录 API 最终错误；参数、超时、取消、未知及聚合异常继续传播。4xx / 5xx BusinessException 的直接传播与 API 响应消息、错误码、消息键 / 参数保持；注入 404 仅验证 Controller 原样传播，其内存框架分支沿用[标签记录](./unified-logging-l2-tag-controller-2026-10-02.md)的独立证据。
- 真实插入 / 更新完成后注入 InvalidOperationException 或 IOException，核对 400 / 500、单一日志归属和持久化数据保留。成功覆盖顶级 / 子级、禁用父级、负层级钳制、slug 小写 / 连续空格 / 名称回退、可空字段、描述原样和审计身份 / 字段保留。
- 预读后由夹具实际删除或软删除目标，再执行真实 UpdateColumnsAsync，验证影响行数为 0、返回仍为成功且安静；这不是并发安全验收或可靠性修复。
- 更新继续使用原表达式 DateTime.Now；没有把日志治理扩展为时间规范化。自动开奖 SQLite → Outbox 时间转换问题仍按[抽奖记录](./unified-logging-l2-lottery-controller-2026-10-02.md)独立保留。
- 首轮发现 xUnit Throws 辅助会读取不可访问的异常 Message，改为测试内直接捕获原异常；未因此调整业务代码。

## 验证命令与结果

- Node 日志契约 **27 / 27** 通过。
- 定向 .NET 回归 **90 / 90** 通过，无跳过；其中新增分类日志回归 **28 / 28**。
- API `--no-restore --warnaserror` 构建通过，0 警告、0 错误。
- 8 个变更文件卫生、全量文档及 `git diff --check` 通过；records 索引 325 行有非阻断篇幅提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CategoryControllerLoggingTests|FullyQualifiedName~TagControllerLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests' -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

HTTP 仅内存 API / 结果过滤器，不启动宿主。本批不代表 PostgreSQL、真实并发、MVC 认证 / 模型绑定或浏览器验收。未安装依赖、启动服务 / 容器或访问生产；RadishLogging.Enabled 保持 false，L2 尚未整体完成。按授权在 dev 本地提交，不推送。

下一项为 ReactionController 单目标 / 批量汇总及切换回应的 BusinessException 最终消费，先核对 Service 拒绝和依赖故障，再确认方案。
