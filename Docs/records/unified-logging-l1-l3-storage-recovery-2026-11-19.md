# 统一日志双挂载的停写与重放联验（2026-11-19）

OrbStack / Linux arm64、Docker Engine 29.8.2，用固定候选 `sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c` 和临时 SQLite 执行 `storage-recovery`。代码基线为 `4cf4fa21` 加本批探针改动。Forward 只发布到 `127.0.0.1:32792`，collector 和 API 均使用内网监听。缓冲对照为宿主 bind mount（virtiofs）与 Linux VM 内 `local` Docker 卷（btrfs）。取证端只读挂载，每例独立缓冲和 JSONL。两种挂载的六例断言及监听隔离全部通过。[命令和边界](../features/unified-logging-l3-ingest-plan.md#l1-l3-storage-recovery)

## 首次 flush 前的失败原因

两种挂载均在采集器启动约 2.3 秒时发出 `SIGKILL`，早于首次 10 秒定时 flush，此前两路均未收到合成事件。强杀后保全的 chunk 与强杀前 SHA-256 相同，文件头为 `c100`，元数据 79 字节、事件载荷 593 字节，逻辑长度 696 字节，分配大小 4,096 字节。事件 ID 位于声明载荷内。独立重算的 CRC 与文件头内的值不符，头内四字节正好是未最终写定的 CRC 状态按小端存储。重启均报告 `invalid crc32` 和 `format check failed`。

| 缓冲介质 | 存储的 CRC 字节 | 按声明长度重算的最终 CRC32 | 被拒读的合成事件 |
| --- | --- | --- | --- |
| virtiofs bind | `7b91af23` | `dc506e84` | `78546dca-8e32-4ec0-9959-ab0163e81b23` |
| btrfs 卷 | `2da4b366` | `994c5bd2` | `bf9f74b3-387b-4fa0-b063-b326ff8611a9` |

本轮字节证据把原因定位到 ChunkIO 的追加与同步之间。追加会写入中间 CRC，首次 flush 锁块或其他同步路径会写定最终 CRC。完整载荷已经出现在映射文件中，仍可能因缺少同步后的头部状态而无法恢复。两种挂载均复现，说明本轮现象不限于宿主共享挂载。上一批 `c1ddaa0a` 只有通用错误和快照摘要，其原始字节未保留。[固定版 CRC 写入与同步](https://github.com/fluent/fluent-bit/blob/66910c10a4d7eafa810b84229cf2dcf7b0f26f97/lib/chunkio/src/cio_file.c#L97) · [固定版 flush 路径](https://github.com/fluent/fluent-bit/blob/66910c10a4d7eafa810b84229cf2dcf7b0f26f97/src/flb_input_chunk.c#L3786) · [上批记录](./unified-logging-l3-retention-recovery-2026-11-19.md#临时停机重放与缺口)

## 处置与两路对账

| 场景 | 停止点 | 每种挂载的两路结果 |
| --- | --- | --- |
| 首次 flush 前强杀 | 两路均无合成记录，缓冲只余含中间 CRC 的坏块 | 两路各缺 1 个同一 ID，坏块重启后保持原 SHA-256。重启后新发事件各交付 1 条。 |
| 首次 flush 前优雅停写 | 停写前均观测到中间 CRC，停写完成后两路各有 1 条，非空缓冲为 0 | 加上重启后新发事件，JSONL 2 条、Log 2 条，均逐键唯一。 |
| HTTP 503 后优雅停写 | JSONL 1 条、Log 0 条，待投递 chunk 通过头部、长度和 CRC 检查 | 503 请求以原 SHA-256 重放入库。JSONL 共 3 个物理行、2 个唯一键，Log 共 2 个唯一键。只有已证实重放的旧事件在 JSONL 中有 2 份同内容副本。 |

503 两例各对真实接收端额外验证一次同内容重投和一次同键异内容。接收端分别报告重复 1 条和内容冲突 1 条，原有记录完整保留。离线回归会拒绝同键异内容、重放窗口外重复和缺行；实测文件均能解析为完整 JSONL。所有健康终态的非空缓冲为 0，两个强杀终态各只剩已登记的坏块。生产者侧额外损失数量仍为未知。

## 剩余门禁

保持坏块原样取证，按验证过的边界区分已同步待投递与首次 flush 前在途事件。以实际部署的架构、镜像 digest、文件系统、daemon 回环路径和七类服务配置冻结停写顺序、队列与容量门限。目标部署需接受或处理已证实的崩溃窗口，并明确默认诊断如何保全坏块与已知缺口。真实断电、完整站点跨版本恢复和查询／告警仍沿既定门禁推进。[剩余退出条件](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist)

## 原始证据与清理

报告及合成坏块保存在本机 `artifacts/logging-storage-recovery-probe/`。按精确运行标签复核容器、网络和卷均为 0，临时目录已移除。26 项探针定向回归通过。

| 证据 | SHA-256 |
| --- | --- |
| `radish-logging-storage-recovery-72fe8c18.json` | `c8e236766de2db4af67e1cd0cb5c46bc93fba79d404c660f3ceaf2ae034ce9e4` |
| `radish-logging-storage-recovery-72fe8c18-evidence/bind-kill-before-flush.flb` | `aa42ab65d46a3cb1b797cff69db155004cf8646d5c0a26b9a826cfa3dd73d06b` |
| `radish-logging-storage-recovery-72fe8c18-evidence/volume-kill-before-flush.flb` | `1fa8499a9e4e2c3199e4e61ff6ade83121ac80f1fd00486852be9e2bf8079bb7` |
