# L2 评论实时推送日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 CommentRealtimePushService 的失败消费点及唯一直接生产消费者 CommentController；不扩张为完整评论业务治理。

## 实现与保留边界

- 失败只生成 `comment.push_failed` Warning 和固定 failureKind，移除原始异常、事件名与帖子 Id。Clients 访问、Group 访问和 Send 失败继续由原 catch 消费，无新增重试；正常推送安静。
- 五类事件的名称、组名、对象载荷、评论父 / 根关系、点赞计数与 UTC 事件时间不变。创建 / 更新与高亮仍保留各自无效输入短路；删除 / 点赞不新增原本不存在的参数校验。
- Controller 创建、点赞、软删除、编辑、版本恢复仍先完成原业务步骤，再发送评论变化与高亮。首个发送失败不阻止第二个发送或成功响应；推送失败不能被解释为业务写入失败。
- 重放、重复内容、无变更保持安静；详情缺失时创建 / 点赞仍可推送高亮，编辑 / 恢复不重算或推送。业务写入、详情读取和重算本身的失败仍按既有边界传播或映射为 400，不纳入推送 catch。
- Controller 原有 ArgumentException / InvalidOperationException 静默响应映射未新增日志。CommentService 神评 / 沙发计算和填充的日志、CommentHub 自有 Debug 及 SignalR 框架输出仍未治理，不宣称完整评论链路已经安全或每次失败都有唯一最终事件。

## 验证

- 四种组合：旧 / 候选 × Development / Production；候选显式设置 mode，Development 开启 diagnostics，核对最终 JSON 的事件码、等级、模式与仅有的 failureKind 属性。
- 使用真实推送服务和真实 Controller，mock SignalR 与业务依赖；验证五类载荷和组名、三层推送失败、无重试、后续高亮继续发送、返回结果、软删除审计字段、调用顺序与原异常实例传播。
- 异常样本覆盖 IO、超时、取消、InvalidOperationException 与禁止访问 Message / ToString 的自定义异常；日志不含内容、身份、组名、事件名或异常正文。
- 定向 .NET 测试最终 **64 / 64** 通过，无跳过：新推送 / Controller 回归、既有 CommentHighlightRealtimeServiceTest / CommentEditHistoryServiceTest、RuntimeLogPolicyTests 和 RuntimeLoggingAdapterTests。首次编译发现新测试遗漏 Serilog 扩展方法命名空间，已修复；收紧属性断言和补充重算失败传播用例后已复验。
- Node 日志契约 27 / 27 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；变更文件卫生、文档检查与 `git diff --check` 通过。记录索引仅有超过建议篇幅的非阻塞提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CommentRealtimeLoggingTests|FullyQualifiedName~CommentHighlightRealtimeServiceTest|FullyQualifiedName~CommentEditHistoryServiceTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动项目 / 数据库 / 容器、访问生产或开启候选日志。未执行真实数据库事务 / 幂等并发、WebSocket、HTTP 管道、认证中间件或浏览器验收；L2 尚未整体完成。

## 下一批

CommentHub 加入 / 离开帖子组日志仍含帖子与连接身份；下一批保留分组、匿名 / 已认证输入中广播和失败传播，治理该 Hub 自有日志。CommentService 及框架来源继续按实际调用链分批推进。
