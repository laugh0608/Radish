# L2 Hangfire 最终输出边界修正与本地验证

> 2026-11-04（Asia/Shanghai）。所有者批准先提交[方案](../features/unified-logging-l2-hangfire-output-plan.md)，再完成修正、双配置本地验证及文档同步；方案已提交为 `d95799039a09c9ecc1bbc0d505b1f13cd2f4faab`。本批实现尚在工作区，未 push 或触发远端任务。精确命令、TRX / 源码 / DLL hash 见[结果 JSON](./unified-logging-l2-hangfire-output-2026-11-04.json)。

## 实施结果

- `HangfireRuntimeLogProvider / HangfireRuntimeStateFilter` 显式绑定各自完整 SourceContext；第三方 logger 名称仍不进入输出。provider 无异常时显式设置 failureKind 缺省，防止 scope / AddPropertyIfAbsent enricher 补入合法但非生产者生成的故障类型。
- 新 `HangfireLogSink` 只重建两个精确来源。provider 仅保留合法 failureKind；状态事件额外保留与编码一致的 outcome、规范小写 D 格式 operationId。原 Job ID / 参数、文本、异常、嵌套对象、无关 jobKind / count / 字符串关联字段不进入受控最终事件。
- 未知 / 缺失 / 错误来源事件编码分别转为 `hangfire.runtime_unknown / job.state_unknown`，只保留固定元数据。相邻或格式不匹配的 SourceContext 原样交给原链，不扩大接管第三方范围。
- 旧链代码 / 配置 WriteTo、配置 AuditTo，以及候选 RuntimeSerilogSink 前均接入同一重建。保留原 Serilog 等级 / Timestamp，trace / span 仅采用强类型或原 Activity 回退；候选时间和三级 / diagnostic / Fatal 映射继续沿用原公共策略。
- 不改变 AutomaticRetry 的次数 / 延迟 / 重复日志抑制、状态选举与存储提交、任务注册、异常传播、logger 所有权、依赖、生产开关或前端日志。状态事件仍描述提交前的候选处理决定，不是已提交状态审计。

## 最终本地验证

| 验证 | 结果 |
| --- | --- |
| Tests 与其引用工程 Debug / Release `--no-restore --warnaserror` 构建 | 两配置均 0 warning / 0 error |
| 方案 filter 的 Debug 定向回归 | 222 passed / 0 failed / 0 skipped |
| 同一 filter 的 Release 定向回归 | 222 passed / 0 failed / 0 skipped |
| 新增 Hangfire 专项 | 每配置 83 项，全部执行通过 |
| .NET / Node 共享 fixture | 新增 4 组 unknown / 模式样本，两端通过 |
| `npm run check:logging-contract` | 52 passed / 0 failed |
| 仓库卫生、文档 / 本地链接、`git diff --check` | 通过 |

filter 的 `CleanupLoggingTests` 同时匹配 `ServiceCleanupLoggingTests`；222 项按实际 TRX 统计，包含两个清理类、HostProgram、HostStartup、任务注册、RuntimeLoggingAdapter、RuntimeLogPolicy 与本批 Hangfire 专项。测试使用现有 SDK `10.0.300` / runtime `10.0.8`、Node `24.16.0`，未 restore 产品依赖或安装工具。

83 项新增用例覆盖真实 ConfigureLegacy WriteTo / AuditTo 与候选配置、Development / Production、诊断开关 / 阈值、scope / root enrichment 污染、无异常缺省值、原异常不可读 getter、未知 / 跨来源编码、错型 / 非法枚举 / operationId、强类型 trace/span、相邻来源、实际六组重试选举、正常成功 / 停机重入队、sink 输出故障、候选应急输出及释放所有权。

初次开发构建因新增测试缺少本地 JobTarget 辅助类失败，补齐后严格构建通过。首轮 Debug 的 220 项中 218 通过、2 项失败，均来自新测试把故障后的 Dispose 当作可重复调用；改为一次释放并对照原 Serilog 的异常 / 次数，另加正常所有权用例。未为通过测试吞异常、增加幂等释放或放宽产品门禁；初始日志 / TRX 与最终结果分别保留。

## 证据、清理与结束边界

本地证据目录为 `artifacts/logging-l2-hangfire-output/2026-11-04/`，保留初始及最终构建 / 测试日志、双配置最终 TRX、初始失败 TRX、Node 结果、七份实现 / 测试 / fixture 原字节快照和输入 / DLL hash。

总索引覆盖 **21 文件（不含自身）**，SHA256 为 `ff941902e76496cb5ead1dc89ba3d1829ff957e8316e81ad4e51344c29390700`，已按文件集合 / size / hash 回读核验；仓库 JSON 与归档 summary 一致。常规 bin / obj、构建缓存与本批证据保留，测试临时数据由夹具释放；本批未安装私有 SDK 或启动任务专属常驻服务。

本批没有启动 API / Auth / Gateway、Hangfire worker 或外部数据库，不绑定端口，不修改 Compose / 生产数据，不发外部询证、不 push / 执行远端工作流、不发布或部署。内存 sink 和存储替身证明不扩大为真实文件 / 数据库落盘、真实任务执行或系统部署验收。

两个自有 Hangfire 来源的本次最终输出缺口已关闭，**L2 整体仍未关闭**。下一步按当前功能顺位确认浏览器共享 HTTP / sanitizer / Client / Console logger 的有限实现批次，并保留其他已登记自有来源的准确边界；不恢复依赖来源调查或外部任意诊断通道扫描。
