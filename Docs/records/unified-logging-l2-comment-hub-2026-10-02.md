# L2 CommentHub 自有日志治理

日期：2026-10-02（Asia/Shanghai）。范围为加入 / 离开帖子组的自有日志与输入中广播的相邻行为；不改订阅、认证或权限规则。

## 实现与保留边界

- 移除两处含 PostId / ConnectionId 的 Debug 和不再使用的 ILogger 字段、构造参数。Hub 仍由 DI 正常激活，未新增事件或异常捕获层。
- JoinPost 对非正帖子 Id 保留原 HubException，LeavePost / StartTyping 保留直接返回；加入 / 离开不读取身份，继续等待一次原组操作。组名仍为 `post-comments:{postId}`。
- StartTyping 保留 IsAuthenticated 与正 UserId 双重判断，事件名为 CommentTyping，接收者为 OthersInGroup，载荷和 UTC 时间不变。空白名称回退 Unknown，可空或非正 commentId 仍原样透传，不增加校验。
- 凭据继续优先采用非空白 query access_token，其次按原规则处理 Bearer / 非 Bearer Authorization；不修改裁剪、大小写、null 或空字符串行为。
- 身份读取、加入 / 离开分组、广播接收者解析与发送失败仍传播同一异常实例，不重试、不生成本地重复日志。框架最终处理边界仍待治理。

## 验证

- 新回归覆盖旧 / 候选 × Development / Production；显式核对候选 mode，Development 开启 diagnostics，旧路径 LoggerFactory 显式允许 Trace 以上输出。
- 使用 DI 激活真实 Hub，配合 mock SignalR / normalizer，验证异步组操作等待、无效参数短路、匿名与无效身份输入中短路、凭据顺序、载荷 / 名称回退及 IO / 取消异常传播。
- 复用上一批评论实时推送回归、既有 ClaimsPrincipalNormalizerTests 与运行日志契约 / 适配测试，防止分组引用或日志接线漂移。
- 定向测试最终 **78 / 78** 通过，无跳过。首轮 4 个新用例因 Mock 的递归“无额外调用”检查早于预期发送验证而失败，补齐发送次数断言后全组复验通过；未修改业务行为来适配测试。
- 变更文件卫生、文档检查及 `git diff --check` 通过；记录索引仅触发非阻塞篇幅提醒。
- API `--warnaserror` 构建通过，0 警告、0 错误。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CommentHubLoggingTests|FullyQualifiedName~CommentRealtimeLoggingTests|FullyQualifiedName~ClaimsPrincipalNormalizerTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动真实宿主 / 数据库 / 容器、访问生产或开启候选日志。未执行 WebSocket、框架调度、认证中间件或浏览器验收；Hub 方法回归不代表这些运行边界已通过。

## 下一批

CommentService 神评 / 沙发实时重算与标识填充：核对已消费异常、部分写入、缓存失效、无变更返回及直接 Controller 消费边界，再治理逐次明细和原始异常日志。生产开关继续关闭，L2 尚未整体完成。
