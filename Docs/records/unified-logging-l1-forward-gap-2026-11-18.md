# 满卷扩容后的 Forward 日志缺口（2026-11-18）

**断点在第 172 条。** 三轮中，1–171 均进入缓冲并交付两路；第 172 条已解码，在扩容写入时失败。173–640 共 468 条均未到达采集器的最后过滤点。复现原来 467 条差额的那一轮，同一输入实例还处理了另一条 API 日志，窗口内聚合计数包含两个不同来源的失败。

## 环境与取证口径

复用[上一批 SQLite 联验](./unified-logging-l1-l3-collector-candidate-2026-11-18.md)的 OrbStack、Linux arm64 镜像 ID、UID、共享卷、端口隔离和合成负载。探针基线为 `726fd6f1`，本次增加可选的 `--trace-growth`。已有镜像直接按 ID 启动。

```bash
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image=sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c --trace-growth
```

同一个合成生产者先写 128 条，等真实共享卷写满后再写 512 条。探针用本轮专用 UUID 前缀及固定长度的序号标识这 640 条，保留原有事件大小、单一 Docker tag 和验收断言。按五个位置核对序号：

1. 从生产者的 Docker 本地日志缓存读回每条事件，独立核对实际输出顺序。
2. 中转只登记各 TCP 连接收到的完整 Forward Message 帧序号、连接结束情况和不完整帧的剩余字节数。
3. 采集器最后一个 Lua 过滤点通过独立 stderr 记录合成序号。到达这里已完成 Forward 解码并取得目标 chunk，后续才调用 Chunk I/O 写入。创建 chunk 失败会在到达此点前返回。
4. 在共享卷占满期间，从缓冲文件字节中提取本轮序号。这是取样时可读的内容，逻辑提交由恢复后的两路终态交叉验证。
5. 释放占位文件并排空队列后，分别从 JSONL 和 SQLite 查出同一生产者的事件 ID，逐条核对。

## 断连时的序号

| 运行 ID | Docker 本地缓存 | 中转完整帧 | 采集器最后过滤点 | 满卷时缓冲文件、最终文件及 SQLite | 写失败 / 创建失败 |
| --- | --- | --- | --- | --- | --- |
| `98b15ad5` | 1–640 | 1–564 | 1–172 | 1–171 | 1 / 0 |
| `5a31deca` | 1–640 | 1–457 | 1–172 | 1–171 | 1 / 0 |
| `f11bdc5b` | 1–640 | 1–640 | 1–172 | 1–171 | 1 / 1 |

三轮均先在缓冲文件看到 1–128。扩容时，第 172 条已通过过滤，写入失败，最终两处均无此 ID。中转都从采集器一侧收到 `ECONNRESET`，生产者侧随后关闭。前两轮的分界随断连时机变化：第一轮有 392 条完整帧（173–564）停在中转至采集器过滤点之间，另有 76 条（565–640）未形成中转可见的完整帧。第二轮对应 285 条（173–457）和 183 条（458–640）。

第三轮的合成生产者只用一条连接，1–640 全部以完整帧到达中转，请求 ACK 的帧数为 0。该连接在第 172 条扩容失败后被采集器重置。173–640 的 468 条已在中转观察到，都未到达采集器写入前的最后过滤点。三轮文件和 HTTP 输出丢弃均为 0。故障解除后各自新发的两条探针均落盘入库，活跃及非空 chunk 均已排空。

### 467 的来源

第三轮复现相同的窗口算式 `640 − 171 − 1 次写失败 − 1 次创建失败 = 467`。释放空间前，探针分别记录到合成连接的 640 帧和真实测试 API 连接的 1 帧。前者被重置，后者收到采集器端结束信号。到排空为止，其余来源共新增这 1 帧，采集器共报告 2 次 Forward 入库前的追加失败。

结合单 worker 遇到追加错误即关闭当前连接的实现，合成连接在第 172 条写失败后无法再在同一连接上触发第二次追加。由此可将同期 `chunk_create +1` 归到另一个 API 来源。这项归因来自连接、序号及指标的交叉核对，指标本身只有输入实例和失败阶段两个标签。本轮按来源核对为 **469 条合成缺口，其中 1 条已计写失败，另有 468 条随断连未到达写入前过滤点**。原先两份报告只保存了聚合值，无法追溯当时每个序号或确认当时创建失败的具体来源。

## 失败如何传导

固定版 Forward 配置使用默认的单 worker。Message 解码后的日志同步进入追加路径，写入失败会返回到 Forward，协议处理退出当前批次并关闭连接。后续完整帧即使已到中转，也可能还在套接字或接收缓冲中。这些帧没有逐条到达存储追加失败点。Docker 使用异步 `fluentd` 驱动，当前配置的 `fluentd-request-ack` 默认为关闭。驱动对本地套接字写成功即可继续发送，收集端追加结果无法逐条传回生产者。[Forward 单 worker 追加](https://github.com/fluent/fluent-bit/blob/66910c10a4d7eafa810b84229cf2dcf7b0f26f97/plugins/in_forward/fw.h#L106-L115) · [Message 追加错误](https://github.com/fluent/fluent-bit/blob/66910c10a4d7eafa810b84229cf2dcf7b0f26f97/plugins/in_forward/fw_prot.c#L1183-L1202) · [协议失败时关闭连接](https://github.com/fluent/fluent-bit/blob/66910c10a4d7eafa810b84229cf2dcf7b0f26f97/plugins/in_forward/fw_conn.c#L112-L121) · [daemon 对应的 ACK 默认值](https://github.com/moby/moby/blob/8af9fe3/daemon/logger/fluentd/fluentd.go#L241-L247) · [驱动的写入与确认](https://github.com/moby/moby/blob/8af9fe3/vendor/github.com/fluent/fluent-logger-golang/fluent/fluent.go#L670-L699)

## 验收与接续

三轮均保留原验收断言，结果为 `failed`。前两轮在中转帧数核对处失败，第三轮中转收到全部 640 帧，在共同缺口与输入失败计数核对处失败。现有候选仍无法给管理员一个由采集器计数独立得出的端到端丢失总数。后续按已确认的计数边界，验证管理员能从独立诊断通道看到断连、采集器可确认的追加失败、数量未知的尾段及恢复。来源级对账需要同一来源的证据。若另行要求端到端逐条确认与重投，再评估协议确认、背压、业务等待及重放去重。

三轮报告均为 `cleanup=true`，按精确标签复核各自的容器、网络、卷和临时目录均已移除。保留原候选镜像及以下本地报告。

| 本地报告（`artifacts/logging-disk-queue-probe/`） | SHA-256 |
| --- | --- |
| `radish-logging-disk-queue-98b15ad5.json` | `9d8f52210a09c5290ca068edb1eb191dddefda8bd58a31b4e81cf2b885f95032` |
| `radish-logging-disk-queue-5a31deca.json` | `84009b6e2a7667901a9afe2756c55480b61524bae8fcac9da425f9be75122f63` |
| `radish-logging-disk-queue-f11bdc5b.json` | `cdf9bb565448cf72f51eac495be3c3602f7acc37a3f2dbd20103f80ca6999b52` |
