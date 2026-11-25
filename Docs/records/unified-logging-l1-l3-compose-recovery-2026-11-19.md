# 统一日志独立 Compose 停写与恢复联验（2026-11-19）

代码以 `e0a31f1e` 加本批维护入口和探针为基线。隔离目标为 OrbStack / Linux arm64、Docker Engine 29.8.2、Compose 5.5.0。五个 Radish 镜像用工作区产物和缓存基础镜像打包。collector 使用固定 5.1.2 候选，数据库使用 PostgreSQL 16.15 和 Redis 7.4.12 的冻结镜像。[配置与命令](../features/unified-logging-l3-ingest-plan.md#l1-l3-compose-preflight)

## 镜像、文件系统和 daemon 日志路径

每个实际运行容器的镜像 ID 与启动前冻结值比对。五个本地镜像使用本地镜像 ID，报告另存有仓库 digest 的固定输入。管理探针只发布 `127.0.0.1:32424`。API 公共端口、日志接收端口、HTTP 中转、采集指标和数据库保持项目内网。实际 `HostConfig.LogConfig` 为 fluentd 异步、`non-blocking` 和 `4m`，地址为 daemon 回环。七类来源各有真实 `radish.<service>.<FullID>` 帧，报告保留其事件键和两路确认。

- collector 缓冲和 JSONL、PostgreSQL 数据、Redis AOF、API / Auth / DbMigrate 的共享应用数据都在独立项目的 `local` 卷，实测 btrfs。collector 配置的挂载表标记为 virtiofs。临时证书及 PostgreSQL 初始化脚本的文件系统由各自容器内的 `stat -f` 报为 `fuseblk` 和 `fuse`。容器镜像根层的 daemon 存储驱动为 overlayfs。Gateway 与 Frontend 没有额外持久化挂载。
- PostgreSQL 实际参数 `log_statement=none`、`log_error_verbosity=terse`、`log_min_error_statement=panic`。合成 PostgreSQL 警告的原始内容在两路均未出现。Redis 验证启用 AOF，未认证请求被拒绝，其原生日志也通过两路归一化交付。
- collector 自诊断走独立 Docker `local` 日志，最多两个 1 MiB 文件。探针只保留指定错误行和合成块的校验信息。

## 停写、快照和坏块

1. 先停 Gateway，再停正常 API、Auth、Frontend、Redis，确认 DbMigrate 已完成。保留 PostgreSQL，为同一 API 镜像开启 `--runtime-log-ingest-only`。维护口收到无凭据提交返回 401，业务路径返回 404，原公共 API 监听不可达。用受控事件验证两路交付后切断 Forward。健康终态的活跃 chunk 和非空缓冲均为 0。
2. 对单个合成键注入 PostgreSQL 入库失败。实际 HTTP 503 接受 0 条，JSONL 已有 1 份，Log 库为 0 条。关闭 Forward、停止 collector 并确认其正常退出，再停止维护接收器。校验待投递 chunk 的头、声明长度及 CRC。确认 Log 写入连接归零后保存 `pg_dump -Fc`、JSONL、缓冲和 collector 配置。恢复到新的 Log 逻辑库和独立卷，对比归档中的原 Log 记录、文件清单和 SHA-256。重启后以同一 HTTP 请求摘要确认 503 批次提交。该键在 JSONL 中保留 2 份相同内容，在 Log 库中只有 1 条。离线对账只允许该重放键出现同内容副本。
3. 在恢复缓冲中放入上轮已保全的合成坏块，再启动同一候选。默认 `error` 级别可见 `[storage] format check failed` 和块路径，精确的 `invalid-crc32` 来自离线复核。坏块 SHA-256 保持一致，本轮两路均未出现块内旧 ID。这个 ID 的两路已知缺口在原匹配部署的[双挂载联验](./unified-logging-l1-l3-storage-recovery-2026-11-19.md)已有记录。此后本轮的新事件正常两路交付。首次同步前普通运行日志的崩溃窗口按已确认的边界接受。未观察到的生产者缓冲和网络在途尾段损失数量为未知。

## 实际部署待核

- 对实际宿主复核架构和 daemon 回环可达性，冻结七类运行镜像与 collector digest，逐容器核对实际 logging driver、选项、标签和采集入口。复核 PostgreSQL 和 Redis 的原生日志配置。
- 核定应用、数据库、JSONL 和缓冲的实际设备及文件系统、剩余容量、队列／留存上限、单片超出量与告警阈值。确认实际入口、作业和所有写入者的停写顺序，确认维护接收与快照点的权限和一致性复核方式。
- 在实际部署复核诊断可见性、坏块取证、恢复演练和可识别损失窗口。生产者在途尾段及真实断电边界仍待验证。查询、站内告警和整站升级／恢复／回滚继续沿既定门禁推进。[退出条件](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist)

## 取证和清理

最终项目 `radish-logging-compose-e8e180b6` 通过。健康排空后两路各 195 个唯一键。Frontend 在隔离 Compose 中由 Docker init 转发 SIGTERM，退出码为 143，其余已停生产者及 collector 均正常退出。恢复后两路各 196 个唯一键，JSONL 比 Log 多 1 个已知重放副本。坏块轮新增事件在两路各有 1 份。

初次取证读取 Docker 标准输出时漏掉了 collector 写到 stderr 的拒读摘要。改用 Compose 合并输出，并在写入坏块前后对照证据。日志配置保持 `error`。构建时从已固定的本地缓存导出 OCI 基础镜像，构建上下文直接引用其本地 manifest。

退出时按项目标签复核容器、网络和卷均为 0，移除本轮五个镜像标签及临时数据、归档和凭据。保留脱敏断言和合成坏块于本机 `artifacts/logging-compose-probe/`。26 项 .NET 定向测试和 15 项 Node 探针定向测试通过。

| 文件 | SHA-256 |
| --- | --- |
| `radish-logging-compose-e8e180b6.json` | `3509f1f75c68a60a2e5b4addb67bc0831d47c6930cf1ab300c6e4ecad6a2081d` |
| `radish-logging-compose-e8e180b6-crc32.flb` | `aa42ab65d46a3cb1b797cff69db155004cf8646d5c0a26b9a826cfa3dd73d06b` |
