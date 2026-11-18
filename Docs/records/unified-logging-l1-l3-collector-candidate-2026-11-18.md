# 统一日志 L1 / L3 采集器候选 SQLite 联验（2026-11-18）

**候选未通过。** 文件回滚重投、权限拒写、满卷创建 chunk、HTTP 队列溢出和文件轮转均能对账。已有 chunk 扩容失败的两次复验中，640 条事件都只交付 171 条到文件和 SQLite，469 条共同缺口只对应 2 条输入失败计数。剩余 467 条的丢失位置尚未确认。

## 候选构建

在 OrbStack 的 Linux arm64 环境执行，工作树起点为 `f4838410`。Fluent Bit 5.1.2 上游提交为 `66910c10a4d7eafa810b84229cf2dcf7b0f26f97`，归档 SHA-256 为 `5062da78b0ced47ec79afaab865a45c02f0d85979ab1e7f729c8823e4344ebb8`。归档与补丁适用性检查、六项离线文件故障回归以及 Radish 的 Linux arm64 Debug 原生产物校验均通过。[构建与联验入口](../features/unified-logging-l3-ingest-plan.md#l1-l3-disk-queue-followup)

原构建先在 APT 获取索引时遇到 TLS 验证失败。显式指定上游镜像已有的 CA 文件后，APT 可以读取签名索引。固定 Debian 基础镜像自带 `libssl3t64 3.5.7-1~deb13u3`，同日主仓库只提供配套 `libssl-dev` 的 `deb13u2` 版本。构建层补上基础镜像声明的 `20261005T000000Z` Debian 安全快照，取得匹配的 `deb13u3`。APT 解析结果为新增 94 个编译层包，已有包升级、降级和移除均为 0。[候选 Dockerfile](../../Deploy/logging/fluent-bit/Dockerfile)

本次从两个固定 HTTPS 快照预取签名索引及所需的 94 个包（98,539,192 字节），逐包核对索引中的 SHA-256。Buildx 以只读本地快照替换这两个下载地址，断网执行安装与编译，APT 使用基础镜像的 Debian 归档密钥核验签名。基础镜像、编译参数、源码和运行层均按候选 Dockerfile 取值。

本机候选 ID 为 `sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c`，arm64 镜像 manifest 为 `sha256:bed1926e36287d3d8573bdf3463958579079ba7d517bd6f9b3aae21d8eea8c61`。运行层的版本检查输出 `Fluent Bit v5.1.2`。各轮使用相同镜像 ID、UID 10001、24 MiB 共享 tmpfs、两条 4M 输出队列及空白 SQLite。Forward 使用回环动态端口，API、入库、指标和中转端口仅在本轮网络内监听。

```bash
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image=sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c
```

## 文件与 SQLite 的对账

以下按合成事件 ID 统计。文件和 SQLite 同时缺失同一条时，共同缺口只计一次。文件支路仍在重试的记录，在释放空间并排空队列后核对终态。对照基线见[原版磁盘联验](./unified-logging-l1-l3-disk-queue-runtime-2026-11-17.md)。

| 故障场景 | 文件交付 | SQLite 交付 | 计数与恢复 |
| --- | --- | --- | --- |
| 已缓冲 128 条后写满卷 | 故障时 0，恢复后 128 | 满卷时已入库 128 | 文件支路回滚并重试，终态两路丢弃均为 0。旧批无重复、坏行或截断尾，两条恢复探针均完整交付。 |
| 拒绝文件写入 | 0 / 2 | 2 / 2 | 文件丢弃增量 2，HTTP 丢弃 0。恢复权限后两条新记录均落盘入库。 |
| 满卷后创建新 chunk | 0 / 1 | 0 / 1 | 共同缺 1，输入 `chunk_create +1`、`chunk_write +0`，两路输出丢弃均为 0。释放空间后两条新记录均交付。 |
| SQLite 返回 503，HTTP 队列溢出 | 12,000 / 12,000 | 4,800 / 12,000 | 五波各 2,400 条，文件跨轮转交付。HTTP 缺 7,200，丢弃增量 7,200，文件丢弃 0。撤销故障后存活积压排空，两条新记录均交付。 |

## 已有 chunk 扩容

首轮 `709bd1bf` 在卷满、已有 chunk 扩容时，等待 Forward 帧齐全与写失败指标同时成立而超时。该报告保留了前三种磁盘用例。复验将独立的 HTTP 队列用例先行，扩容用例在采到真实 `chunk_write` 失败后释放空间、等待排空，保存两路终态与中转观察值，再执行原有的帧数和计数断言。

两次复验均分两波从同一生产者送出 128 + 512 条。排空后，两路交付的 171 个 ID 完全相同，文件中没有重复、坏行或不完整尾。两次故障窗口内均为输入 `chunk_write +1`、`chunk_create +1`，文件及 HTTP 输出丢弃增量均为 0。

| 复验 | 发送 | 中转完整帧 | 发送数减完整帧数 | 完整帧数减去交付和已计输入失败 | 两路共同缺口减去已计输入失败 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `e25c196f` | 640 | 516 | 124 | 343 | 467 |
| `3e83d6d2` | 640 | 475 | 165 | 302 | 467 |

最后一轮在扩容窗口观察到 1 个连接关闭时留有不完整帧，非预期帧数为 0。释放空间后，另发的两条新记录均完整落盘并入库，活跃 chunk 和非空 chunk 文件均为 0，卷上余 2 个空文件。中转按生产者统计完整帧，输入指标按实例和阶段聚合。两者都缺少与每次失败逐条对应的 ID，因此表中差额只能限定调查区间。

**剩余问题。** 下一步在同一故障窗口用合成序号核对生产者写出、中转收到完整帧、采集器解码及实际追加的记录，区分断连前后的已确认丢失与未知区间。管理员需要能看出输入指标只覆盖其中一部分。完成该项后再复核候选传输与内存，随后接续留存和备份恢复。

## 报告与清理

三轮报告均记录 `result=failed`、`cleanup=true`。按各轮精确标签复查，容器、网络、共享卷均为 0。三个运行目录和构建目录均已移除，含临时 SQLite、凭据、源码和快照副本。本机保留候选镜像、构建缓存和以下 JSON，供同一候选复核。

| 本地报告（`artifacts/logging-disk-queue-probe/`） | SHA-256 |
| --- | --- |
| `radish-logging-disk-queue-709bd1bf.json` | `507fdc2e7d5055f65d67486db06ec086e0b129c4637db67396015bdb2a3b0adc` |
| `radish-logging-disk-queue-e25c196f.json` | `92791295bd3ee66f5e70b2f742fdfb6b47a1cbee41cc5dcb221c17bac2766166` |
| `radish-logging-disk-queue-3e83d6d2.json` | `4157ad8d3bcafa903de567a9bd9bb1f8ec6beb18d00bebe630b6aeee047ae9af` |
