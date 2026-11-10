# 2026-11-05 L2 自有操作与直接异常消费者本地收口

## 范围与结论

项目所有者批准按[衔接方案第 8 节](../features/unified-logging-l3-ingest-plan.md#8-已批准切片剩余七个自有来源)连续处理七个来源，补本地测试和 Docs；不安装依赖、不启动服务或容器。准备基线为 `dev` 的 `29e8b4f4`，前一批 L3-A 已由 `7002cd75` 保存。

已完成七个来源的本地实现与最终输出回归。直接消费链测试发现 `ApiExceptionHandler` 旧链仍继承任意 scope，因此在已批准的最终出口核验范围内补齐该精确来源。没有更改响应、业务返回、存储事务、限流决定或依赖版本，不据此关闭 L2 整体。

## 来源到输出的核验

| 生成点 | 消费 / 传播边界 | 旧 / 候选出口与证据 |
| --- | --- | --- |
| 当前用户组装 | 权限 / 头像 catch 消费；用户读取外抛 | 正常过程无输出，两个固定降级摘要；真实 Controller + 服务替身，验证空权限集 / 忽略头像与原读取外抛 |
| 客户端列表 / 详情 | 当前 catch 消费并返回既有失败结果 | 固定命名 EventId → 精确 sink，剔除异常和任意业务身份；Controller + manager / query 替身 |
| 客户端四种写入 | 完成后记录结果；失败外抛 BusinessException → ApiExceptionHandler | 验证四种结果、密钥仍在响应且不在输出；直接调用真实 handler 写入内存响应并验证一次 http.failed；OpenIddict manager 自身的已治理来源沿用第 66 节 |
| 首管理员奖励 | 首管理员已创建后 catch 消费奖励异常 | 固定部分完成摘要；真实 BootstrapService + repository / coin 替身，不宣称整个注册回滚或奖励必然未写入 |
| 通知去重 | 三类缓存异常由服务内部消费 | 正常调用安静，检查失败 false，写入 / 删除失败完成；直接服务 + cache 替身。源码仅检出构造注入，无业务方法调用，不认定生产已触达 |
| 聊天搜索 | 保留原搜索校验、游标、结果及外抛 | 删除逐次 Info 与专用 Stopwatch；既有搜索测试使用 Strict ILogger，任何生成日志的尝试都会使测试失败 |
| 限流注册 / 请求观察 | 注册结果与请求诊断分开；原拒绝回调响应 | 真实注册 / 中间件 + 内存 RequestDelegate 验证 429 / Retry-After、白黑名单 GetNoLimiter 与命名策略 403；覆盖 EnableLogging / diagnostic / 模式组合 |
| 经验扩展 | 仅服务注册 | 无日志；确认计算器仍为 Singleton |
| ApiExceptionHandler | 成功写入错误响应后记录；输出失败沿原边界传播 | 固定 EventId → 原 http.failed；statusCode 只允许 int 500–599；不新增消费或重试 |

保留事件全部经过 `ApplicationOperationLogSink` 在上下文合并后重建；对应六个精确来源各有 unknown，来源邻接名称不匹配。共享策略新增固定说明和 `rateLimitState` 封闭枚举。具体事件、门禁与字段以[契约第 76 节](../features/unified-logging-contract.md#76-l2-剩余自有操作及直接异常消费者2026-11-05)为准。

旧链配置 WriteTo / AuditTo 与候选 Development / Production 成组验证；另验证旧链代码 console 接线。覆盖任意 scope、伪造 EventCode / Diagnostic、未知及重复 Id / Name、非法 failureKind、原异常不可读访问器、强类型 trace/span 与候选应急限频。未打开真实 File / Log 数据库 sink；共同包装接线的源码核对不替代真实介质故障验收。

WriteTo 故障被消费，AuditTo 可传播；Client 创建已返回后出现 AuditTo 故障时，原 catch 包装为 BusinessException，已发生的 manager 写入不撤销。候选 writer 故障仍返回原消费结果并使用安全应急输出，不把“日志成功”作为业务成功前提。

## 验证

- `dotnet build Radish.slnx --no-restore -c Debug --warnaserror`：通过，0 warning / 0 error。
- 扩展定向回归：492 项通过，0 失败 / 0 跳过；包含本批操作、限流、Client / User / Bootstrap / 搜索既有测试、策略 / adapter、HTTP 框架与异常链、OpenIddict Core 基础、HostProgram、Hangfire 及 L3 本地测试，明确排除 `Database=PostgreSQL`。
- 补充 OpenIddict Core 全组及 API 错误契约回归：54 项通过，0 失败 / 0 跳过（与前一组有重叠，不相加为独立测试总数）。
- `npm run check:logging-contract`：56 项通过；新增共享固定事件、状态枚举、非法状态剔除与 Production diagnostic 关闭样本。
- 身份 Claim、Console 权限扫描通过；保留既有 `console.hangfire.replay` 未被前端入口引用的提示。
- 文档 / 链接、changed-only 文本卫生和 `git diff --check`：通过；日志指南与 records 索引保留既有篇幅提醒。

定向回归命令：

```bash
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter '(FullyQualifiedName~ApplicationOperation|FullyQualifiedName~RateLimitOperation|FullyQualifiedName~ClientControllerTest|FullyQualifiedName~UserControllerProfileTest|FullyQualifiedName~BootstrapServiceTest|FullyQualifiedName~ChatMessageSearchServiceTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~OuterHttpFrameworkSinkTests|FullyQualifiedName~HttpExceptionPipelineLoggingTests|FullyQualifiedName~OpenIddictCoreLoggingTests|FullyQualifiedName~HostProgramLoggingTests|FullyQualifiedName~HangfireOutputLoggingTests|FullyQualifiedName~RuntimeLogIngest)&Database!=PostgreSQL'
```

补充回归使用相同测试命令及 `FullyQualifiedName~OpenIddictCore|FullyQualifiedName~ApiErrorContractTest` 过滤器，不启动真实 OpenIddict 服务或连接其存储。

## 证据限制与下一步

- 本批只 Build 宿主、调用内存 HTTP 管道和现有本地测试；没有 Start / Run、监听端口、真实登录、Redis / PostgreSQL 或 collector 联验。L3 测试中的临时 SQLite 由原夹具管理与清理。
- 限流黑名单既有分区旁路、代理地址信任与 CIDR 解析保持，不借日志输出改造修复；高频拒绝不产生 Production 逐条告警。
- 其他已登记业务来源仍需逐项核验最终输出，不能把本批精确来源推广到整个宿主。DbMigrate 命令协议、裸输出例外、安全异常栈帧 / 权威上下文与 Flutter 日志边界仍待接续。
- 全仓时间语义检查的既有 Wiki baseline 问题沿用 L3 记录；本批未修改该文件或刷新 baseline，也未宣称全量 baseline 通过。
- 生产候选开关保持关闭；L1 传输 / 磁盘 / 平台、L3 真实接收链及 L3-B 留存恢复、L4 查询、L5 告警、L6 切换未关闭。未 push、PR、发布或部署。
