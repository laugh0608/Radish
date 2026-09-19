# 2026-09-19 统一日志 L1 采集安全与故障边界

## 本批结论

在[首轮传输实验](./unified-logging-l1-contract-and-transport-2026-09-19.md)基础上，新增可执行的采集规范化层，完成本机隔离环境的输入安全、来源校验、启动解耦及降级验证。仍未修改 production Compose、宿主日志入口或线上服务。

本批关闭的是 **L1 采集安全与故障可见性子项**。没有把 HTTP mock 当作真实入库，没有宣布整个日志专题完成，也没有把本机 arm64 结果扩展成所有生产平台验收。

## 实现

- [`Deploy/logging/normalize.lua`](../../Deploy/logging/normalize.lua)：按 preflight → parser → normalize → UUID → finalize 顺序执行。先拒收 Docker 分片和超限行，再重建规范事件；未知字段与任意消息 / 异常正文不会复制到输出对象。
- [`render-policy.mjs`](../../Deploy/logging/render-policy.mjs)：从唯一 JSON 策略生成 Lua 数据，不手工维护另一份属性或事件说明白名单。
- 合法应用事件保持原 eventId；拒收摘要和原生事件在规范化后统一生成 ID。UUID 插件不会在原始对象里与用户同名字段竞争。
- service / instanceId / containerId 来自已登记的 `radish.<service>.{{.FullID}}` 标签；部署与版本来自采集器显式配置。载荷中的来源声称不能覆盖它们。合法的 Docker 短 ID 可保留为 instanceId。
- PostgreSQL / Redis 首批适配只保留来源和等级，用固定事件说明替代原文。PostgreSQL DETAIL / STATEMENT / CONTEXT / HINT / DEBUG 和 Redis 调试级别省略。这里只验证合成的已登记格式，真实镜像来源设置仍须在接入阶段验证。
- Lua 回调内部捕获输入错误并生成固定拒收摘要；配置关闭插件的异常后原样放行保护模式。部署配置错误应阻止采集器启动，不通过返回原始输入掩盖问题。

接口依据：[Docker 标签模板](https://docs.docker.com/engine/logging/log_tags/)、[Fluent Bit Lua filter](https://docs.fluentbit.io/manual/data-pipeline/filters/lua)。回环 Forward 仍信任受管理宿主，标签不是面向恶意本机进程的认证机制。

## 验证方式

继续使用本任务已授权的镜像、回环随机端口、`radish-logging-l1-*` 容器 / 网络与系统临时目录，没有安装新依赖、启动业务 API 或连接真实数据库。

默认入口现在执行安全边界回归：

```bash
node Scripts/logging/collector-probe.mjs
```

首轮未加规范化的传输实验保留为 `--raw-transport`，只允许合成数据。报告分别写入 `.tmp/logging-l1/boundary-report.json` 和 `collector-report.json`；容器、网络与临时目录在 finally 中清理。

## 实测结果与限制

| 项目 | 已成立的结论 |
| --- | --- |
| API 容器完全不存在 | collector 能启动，文件收到事件；接收端出现后沿用原 ID 投递 |
| 敏感载荷 | 消息、模板、异常、属性及伪造实例字段中的标记秘密不进入文件、HTTP 接收结果或已持久化队列；采集器诊断也检查同一标记 |
| 无效 / 分片输入 | 非 JSON、未知 schema、日期越界、来源不符、未知 tag、Docker partial 转换为有限安全摘要；不保留 raw `log` |
| 原生来源 | PostgreSQL ERROR 与 Redis Warning 保留等级，DETAIL 被省略；原文不进入运行事件 |
| 最大字段组合 | 4000 条包含当前白名单最大值和完整关联字段的事件全部交付；不是仅测试空属性启动日志 |
| 批次 | 本轮大批请求超过原 2 MiB 限制，修订的 16 MiB 模拟入口正常接收，没有 413；不证明任意 Forward 客户端 / 压缩输入的上界 |
| 文件路径不可用 | HTTP 支路仍交付；file output 的 errors / dropped_records 增加；故障事件没有自动补写文件 |
| 文件恢复 | 在容器内恢复路径后，无须重启采集器即可写入新事件；这不补回先前丢弃事件 |
| 队列压力 | HTTP 返回 503 时连续输入 12000 条事件，4 MiB 队列发生旧记录丢弃；dropped_records 明确增加，恢复后新事件能继续交付 |
| 存储预算 | 实测队列文件低于 4 MiB + 单 chunk 余量，日志文件数量保持最多 4 个；轮转仍不是严格逐字节上限 |

完整数字和计数器快照由上述报告记录，不能把特定批次数值当作吞吐 SLA。43 项 .NET / Node 生成契约定向测试另外通过。

文件打开失败属于插件不可重试错误，`Retry_Limit false` 不会把它改成可重试；对应[固定版本源码](https://raw.githubusercontent.com/fluent/fluent-bit/v5.1.2/plugins/out_file/file.c)。因此后续 Console 必须显示介质缺口，不能仅因 HTTP 入库成功就显示“文件正常”。没有模拟 ENOSPC，不能将本次路径错误等同于完整磁盘满验证。

本机文件共享实验曾出现宿主恢复目录而容器仍看到旧文件的情况。最终测试在同一隔离容器内注入 / 恢复故障，并断言容器看到目录后再验证，不把文件共享现象误归因于采集器。

## 下一步

1. 在当前事件契约上推进 L2 生成端适配，先落实引导 / Serilog / Node 入口及有限应急处理，再治理 SQL、AOP 与异常责任；不提前切换 production 默认链路。
2. L1 剩余的正式传输上界、目标部署平台与完整磁盘故障验证仍须关闭；16 MiB 目前是经过代表负载验证的候选值。
3. L3 实现真实内网入口、逐行校验、事务提交后确认、幂等写入与拒收计数，并接入独立管道指标；L4 / L5 再提供 Console 查询与告警。
