# 2026-09-19 统一日志 L1 契约与采集首轮实验

## 结论

L1 已开始实施，生成策略与跨语言契约测试已落地；采集实验复现了必须在切换前修正的边界。**L1 尚未关闭，不进入生产采集切换。** 现有业务日志路径未改动，没有部署到 `hub.radishx.com`。

设计入口：[统一日志专题](../features/unified-logging-governance-design.md)；实现真相源：[事件契约与进度](../features/unified-logging-contract.md)。

## 授权与环境

- 项目所有者本轮明确允许拉取 Fluent Bit `5.1.2`，运行及清理 `radish-logging-l1-*` 隔离容器 / 网络。
- 使用本机既有 `node:24.16.0-alpine3.23` 作为模拟 HTTP 接收端与合成 stdout 生产者；未安装 npm / NuGet 包。
- 宿主 macOS + OrbStack，镜像实际架构 Linux arm64；未验证原生 Linux amd64、Docker Desktop 或 rootless。
- Forward 仅映射 `127.0.0.1` 随机端口；HTTP 模拟端不映射宿主；无 Docker socket 挂载，无业务库、项目数据或线上服务读写。
- 临时目录用于配置、文件、磁盘队列和模拟接收结果。测试结束清理容器、网络和临时目录，只保留镜像缓存与脱敏报告。

## 候选身份与插件

- 镜像：`cr.fluentbit.io/fluent/fluent-bit:5.1.2`
- 本机实际 digest：`sha256:d792375ca8e53be72fc25716c28f291f32c6fc6f4f31d12d0d14bc78cefe9226`
- 二进制：`Fluent Bit v5.1.2`，commit `66910c10a4d7eafa810b84229cf2dcf7b0f26f97`。
- 实际成功配置 / 运行：Forward input、JSON parser filter、file output、HTTP output、filesystem buffering。
- 项目许可证：[Apache-2.0](https://raw.githubusercontent.com/fluent/fluent-bit/v5.1.2/LICENSE)；[版本公告](https://fluentbit.io/announcements/v5.1.2/)。本轮没有重新分发定制镜像；分发前仍需核对镜像内第三方 notice 和目标架构清单。

上述 digest 仅表示本机取得的候选身份，不能当作已经审计过所有平台的 manifest。

## 实验结果

入口：[`collector-probe.mjs`](../../Scripts/logging/collector-probe.mjs)，仅可输入合成载荷；HTTP mock 不代表真实数据库事务。

| 场景 | 结果与证据边界 |
| --- | --- |
| 模拟接收端返回 503 | 文件继续收到 10 条事件；随后 HTTP 恢复收到相同 ID 集合 |
| collector 被 SIGKILL 后重启 | 另一组 10 条已持久化待投递事件恢复；不代表尚在内存中的事件不会丢 |
| 200 条 / 2 MiB HTTP 限制 | 494 条与 506 条的 chunk 均收到 413；后续没有重试这两个 chunk |
| 坏批后健康批 | 独立健康批仍可被接收；不代表被拒批次已隔离留存或补录 |
| 64 KiB 轮转、最多 3 片历史 | 文件数量有界，但历史片实测约 2,002,182 与 2,052,358 字节；阈值不是硬字节上限 |
| 修订信封预算 16 MiB / 32768 条 | 1000 条普通载荷、1000 条含 JSON 转义膨胀的载荷全部收到；最大单请求 6,072,890 字节 / 1000 条 |
| 20,000 字符 stdout 长行 | Docker 产生 `partial_message` 分片，parser-only 路径不能保持完整事件；此配置不能生产使用 |
| 资源清理 | 最终报告 `cleanup=true`；测试容器、网络及临时数据清理完成 |

413 语义与固定版本[HTTP 插件源码](https://raw.githubusercontent.com/fluent/fluent-bit/v5.1.2/plugins/out_http/http.c)一致：400–499 除 408 / 429 返回不可重试失败。不能通过改返回 200 假装入库成功，也不能简单改为 503 让超限 chunk 永久重试。

`body_key` 虽支持逐记录请求，本轮未采用；首期继续校准批量 JSONL 路径，避免未实测的替代模式成为另一条生产写入路径。

## 代码与定向验证

- .NET / Node 从一个版本化 JSON 策略读取三级映射、服务 / 分类、静态事件说明和受限属性。
- 共同样本覆盖 Production 诊断丢弃、Development 显式启用、Fatal、最小级别、嵌套 / 编码秘密、来源伪造、关联 ID、数值 / 整数约束。
- 最终 JSON 输出使用 8 KiB UTF-8 上限，防止已观察到的长行分片；对超限输出明确失败，尚未接入宿主 sink。
- Node 定向测试 22 项通过；.NET 定向测试 21 项通过，均无跳过。测试项目构建通过；文档链接、变更文本卫生、时间语义和敏感字面量扫描通过。既有 records 索引篇幅提醒仍在，不属于日志运行失败。
- 未启动真实 API / PostgreSQL、未验证日志表事务幂等、Console UI、告警、原生 PostgreSQL / Redis 脱敏、磁盘满及生产负载。

## 后续关闭条件

1. 采集规范化必须拒绝未知 schema / 原始分片，有限摘要不能带原文；原生来源有明确安全解析规则。
2. HTTP 上限需从事件 / Forward / chunk 最坏情况推导并压测，服务端内部处理批次与传输批次分离；拒收摘要、重试幂等和隔离留存有真实实现。
3. 文件总容量必须包含按 chunk 轮转超出量；磁盘 / 队列耗尽的计数与应急通道有验证。
4. API 完全未启动、文件不可写、进程重启、长行、来源伪造的组合场景通过后，才冻结生产配置并进入宿主切换。
