# 2026-11-05 Controller 最终输出下一批准备

## 基线与发现

基线 `dev / 12f739f9`，工作区起始干净。项目所有者要求继续推进下一步开发计划，本次进行只读源码核对、临时内存缺口探针和方案落盘，未修改产品运行时代码。

- 对 API Controllers 的现有日志生成点与最终 sink 精确来源目录进行对照，明确 14 个未补最终边界的 Controller。有限目录和拟实施方案维护在[衔接方案第 9 节](../features/unified-logging-l3-ingest-plan.md#9-下一批api-controller-最终输出收口)，避免在记录中重复长期契约。
- 大部分来源仍通过没有固定 SourceContext 的 `Log.ForContext("EventCode", ...)` 生成，Attachment 的 ILogger 来源也未被现有精确 sink 接管。共有事件码不能用来证明来源身份；当前候选裁剪不替代默认旧链。
- 已完成的 User / Client / ApiExceptionHandler 与其他第 76 节来源保持原结论，不重复实施。

## 代表性缺口复现

临时 xUnit 夹具通过反射调用真实 `ReactionController.BuildErrorResponse(BusinessException)`，传入 503 业务异常，外围使用现有正式 ConfigureLegacy / RuntimeLoggingConfiguration 接线及带合成 Authorization 的 scope。

两个诊断断言均成立：

1. 默认旧链：响应仍为 503、事件仍为 http.failed，但最终 sink 事件保留 Authorization scope 属性。
2. 候选 Production：响应与事件码保持，序列化结果不包含合成秘密。

执行入口为 `dotnet test ... --no-restore --filter FullyQualifiedName~ControllerOutputReadinessProbe`，并关闭测试配置文件监视；2 项诊断通过。这里“通过”只表示缺口已复现，**不是产品修复通过**。夹具已在运行结束后删除；没有提交把泄漏当作正确行为的测试，实施批次应新增“最终输出不得携带污染”的永久回归。

探针只覆盖这个真实消费方法及两个输出模式，不等于 14 个 Controller 全部运行验证，也不等于完整 action / HTTP 请求、存储或生产证据。其他来源目前依据源码和接线缺口进入已限定的实施队列。

## 命令与裸输出核对

- C# 扫描区分了命令 stdout、公共运行输出 / 独立应急 stderr，以及已注释的 BaseService 调试行。
- DbMigrate doctor / verify / help 保留命令协议；DescribeTarget 对 SQLite 和其他配置目标输出固定占位，CLI 运行诊断定向 stderr。未执行真实 doctor / verify / apply，不沿用本次只读检查授权访问数据库。
- ConsoleHelper 的 WriteColorLine 与四种扩展调用只在定义文件内命中；先前计划中泛称的 ConsoleHelper.WriteLine 已纠正。未检出仓库 C# 直接调用，不代表排除了反射或外部消费者；不因工具存在就声明可达漏洞。

本次没有安装依赖、启动项目服务 / 容器、绑定端口、连接真实数据库、push 或部署。实现批准状态只在第 9 节维护。
