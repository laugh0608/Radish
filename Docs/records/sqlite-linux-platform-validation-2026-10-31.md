# SQLite Linux 双架构执行与验证

> 2026-10-31（Asia/Shanghai）。**本批已授权矩阵与清理完成：Linux ARM64 原生、AMD64 仿真通过；未 push、发布或部署。** AMD64 仿真不替代原生 AMD64 验收。
>
> 方案见[Linux 执行方案](../features/sqlite-linux-platform-validation-plan.md)，精确镜像与审阅稿见[冻结清单](./sqlite-linux-platform-inputs-2026-10-31.json)。机器证据：[总览与清理](./sqlite-linux-platform-validation-evidence-2026-10-31.json)、[ARM64](./sqlite-linux-platform-validation-arm64-2026-10-31.json)、[AMD64 仿真](./sqlite-linux-platform-validation-amd64-2026-10-31.json)。

## 1. 授权与输入

- 所有者明确确认：执行 Linux ARM64 原生、AMD64 仿真构建 / 测试 / publish、八镜像探针和漏洞扫描，完成后清理本任务资源；不 push、不发布、不部署。
- 开始时 `dev / 9ab4ca47` 工作区干净；实际使用批准的 `6d952fc46d178a92e9adc9268d847a79bb62a317` Git archive，两个准备文档提交 `6d952fc4 / 9ab4ca47` 没有改变产品 / 构建代码。
- Task ID：`radish-sqlite-linux-20261031-a7c2`；所有临时容器 / 自有构建镜像标记 `io.radish.sqlite-linux=<Task ID>`；镜像构建只 `--load`。
- SDK 10.0.403；.NET / ASP.NET 10.0.14；Node stage 24.22.2；Rust 1.96.0；Trivy 0.70.0。SDK / final 为 Ubuntu 24.04，实际 glibc 2.39；Rust stage 为 bookworm / glibc 2.36。
- SQLite native 包仍为 `[3.53.3]`，config / core / provider 为 3.0.3，Microsoft Core 为 10.0.0。未改变项目引用、正式 Dockerfile、Compose、业务配置或数据库 schema。

## 2. 实际矩阵

| 检查 | ARM64 原生 | AMD64 仿真 |
| --- | --- | --- |
| 精确基础镜像下载 | 通过 | 通过 |
| Rust Debug / Release、ABI / manifest / ELF | 通过 | 通过 |
| .NET 严格构建 | Debug / Release 通过 | Debug / Release 通过 |
| 定向测试 | 各 60 项通过、无失败 / 跳过 | 各 60 项通过、无失败 / 跳过 |
| 普通 / RID publish | 两配置 × 四宿主，共 16 份通过 | 同左，合计 32 份 |
| publish 冷加载 | 48 个独立进程通过 | 48 个，合计 96 个 |
| 四份 Release final 镜像 | 全部构建 / 文件 / 材料 / 探针通过 | 同左，合计八镜像 |
| final 冷加载 | 12 个独立进程通过，API 另过图片自检 | 同左，合计 24 个 |
| final 漏洞策略 | 四份 HIGH / CRITICAL 零发现 | 同左，八份均无例外放行 |
| 清理与代理恢复 | 两容器 / 20 个自有镜像 / 临时数据已清理，代理恢复原空值 | 同一批精确清理 |

定向测试过滤 SQLite、EF / DbMigrate bootstrap、图片 ABI 与附件上传契约，排除 PostgreSQL trait；60 为每个配置的执行数，四组共 240 次执行，不宣称再次全量测试。四份 TRX 均为 `Completed`，没有失败、跳过或未执行项。两架构全部普通 / RID 还原图保持批准的 219 项不同 package/version，无增删漂移；四份 NuGet 审计各覆盖 19 项目，未报告漏洞或 feed 错误。

发布 / final 合计 120 个独立冷进程，均只加载一份批准的 e_sqlite3，version 为 3.53.3，source ID 与契约一致；四个主要托管程序集的实际加载路径与 SHA256 同样通过。两架构加载文件分别匹配契约中的 `707fff6b…`（ARM64）、`b0f5c480…`（x64）。通用输出携带的其他 RID 文件只作来源 / 文件校验，不扩大运行平台结论。

10 次真实 Hangfire SQLite storage 探针各验证 16 项注册契约，共 160 项；专用成功 / 失败重试任务均到 `Succeeded`，重试探针共两次尝试，worker 已随进程结束。API 普通 / RID 两配置、两架构及两个 final 共 10 次 Rust 图片自检通过。两个 final API 的 Rust 二进制与 manifest 均与对应独立 Release 产物完全一致；没有改变既定 glibc 2.36 构建基线。

八份 final archive 使用 Trivy 0.70.0 和同一份漏洞库快照，断网扫描后调用现有策略；数据库内容 SHA256 在扫描前后相同。报告覆盖 Ubuntu 包与 .NET 依赖，筛选 HIGH / CRITICAL；结论不等同于所有严重级别或全部静态链接 C / Rust 依赖无漏洞。

| 宿主（每种架构） | notices 组件 | 材料文件 | final 与 build stage |
| --- | ---: | ---: | --- |
| API | 17 | 57 | SQLite / 托管文件 / 材料字节一致 |
| Auth | 12 | 31 | 同左 |
| Gateway | 11 | 29 | 同左，未引入 EF SQLite 组件 |
| DbMigrate | 12 | 31 | 同左 |

八个 final 均确认不含 Node。Auth / Gateway 沿用现有 apt 步骤，实际包版本已由 `dpkg-query` 留痕；仅冻结 FROM 不宣称 apt 输入逐字节可复建，也不改变 Hangfire / NoDrive 等尚未关闭的发行材料边界。

## 3. 网络与执行环境处置

首次 ARM64 SDK `docker pull` 约 100.6 秒后由 daemon 返回 MCR registry HEAD 的 `TLS handshake timeout`，与客户端只读直连可达形成区别。先按方案暂停 MCR 相关步骤，完成独立 Node / Rust / scanner 下载；没有自动改代理或镜像源。

所有者随后单独批准临时排除 MCR，并在本任务结束恢复原值。原自定义 `network.proxy.exclude` 为空；根域排除后 registry 可达，但官方 `southeastasia.data.mcr.microsoft.com` 数据重定向仍超时；后缀写法重试同样超时。最后显式列出根域和实际 MCR 数据子域后，两架构 SDK / ASP.NET 下载成功：

```text
mcr.microsoft.com,southeastasia.data.mcr.microsoft.com
```

没有关闭 TLS 校验、修改系统代理、换 registry 或重启引擎。这里记录具体配置与观察结果，不据此断言 OrbStack 的后缀匹配实现有缺陷。结束时已核对当前值仍由本任务设置，恢复原空值并重新读取确认。

Buildx 两次初始构建均在开始前因沙盒禁止写入 `~/.docker/buildx/activity` 而失败，随后按同一冻结模板 / 标签最小范围提权重试成功，没有改变构建参数或产品代码。

## 4. 证据有效性与临时调度修正

- 本机 Python 3.9 不支持 `hashlib.file_digest`，首次 API archive 已导出，但扫描调度尚未开始；改为标准流式 SHA256 后从该步骤继续，没有重跑已通过的构建 / 探针。
- 临时源码路径 `/var/...` 与 Node ESM canonical `/private/var/...` 不同，使首次策略 CLI 的 main guard 未进入，进程返回 0 但没有 JSON。该次标为无有效裁决；改用同一冻结文件的真实路径后生成并验证 evaluation JSON。没有把退出码 0 单独记作通过，也没有重复执行已完成的 Trivy 扫描。
- 清理脚本首次遗漏 Docker 过滤器的 `label=` 前缀，未删除资源即失败；此时代理已恢复。修正选择器后继续按已记录 ID / label 清理，没有重复改代理或触及其他项目。三项临时调度修正均未修改仓库脚本或产品实现。
- 测试以完整 TRX 的实际发现 / passed / failed / skipped 为准；运行探针以 JSON 中实际模块路径、SHA256、SQLite version / source ID 与托管程序集哈希为准。
- 每个 final 都单独核对 build stage 的原始 assets / 源包 / publish，再与 final 的 SQLite 文件和 `third-party-notices` 字节比对；未在 runtime 镜像补装 Node。

## 5. 数据与未覆盖边界

没有映射宿主端口，没有启动 API / Auth / Gateway 业务入口或 DbMigrate 默认 `apply`；没有访问生产 / 既有数据库、启动 PG / Redis / Valkey 或改写证书信任。探针只使用内存库、任务专属目录及专用 Hangfire 队列，注册断言后移除业务定时任务，执行的仅为测试任务。

AMD64 本机执行属于仿真；本批通过仍不关闭原生 AMD64 runner 验收。Windows x64、Intel macOS、PG 43 项环境用例、Hangfire 完整对应源码材料、NoDrive 询证和 SNI 分发边界仍独立保留。本批不包含页面联调、业务宿主 Smoke、远端 workflow、tag、Release 或部署。

## 6. 清理与证据留存

- 已删除两个任务 SDK 容器和 20 个自有标签（两 native、两 validation、八 final、八 build stage）；末次核对本任务标签的容器与镜像均为零。
- 已删除 marker 校验通过的临时源码快照、source.tar、publish / native 二进制副本、测试数据库、扫描库缓存和镜像 archive；未创建 named volume / 自定义网络，未映射宿主端口。
- 原始日志、396 条命令记录、四份完整 TRX、原始 assets、deps / 输出文件哈希清单、120 份实际加载结果、八份扫描原报告 / 策略、notices / native SHA256、十份固定 Dockerfile 及临时控制脚本文本另存为本机证据。原始大文件不提交 Git；本页关联的三个机器摘要已进入工作区。
- 归档有 2,087 项带 SHA256 的文件，外加归档索引本身，约 38.5 MiB；逐项复核均匹配。准备阶段旧 metadata 证据继续保留。
- 按方案保留九份共享基础 / scanner 镜像与共享 BuildKit cache，不执行任何 prune。结束时 Docker 报告 Images 18.09 GB、Build Cache 55.98 GB；宿主剩余约 310.2 GiB。没有记录起始 cache 精确值，不把整个共享缓存或宿主磁盘差额归因本任务。

本机原始证据目录（不含产品二进制或测试数据库）：

```text
/var/folders/dp/rzjx58m54kng6tn5k5gjw71h0000gn/T/radish-sqlite-linux-20261031-a7c2-evidence
```

本批通过的是批准的后端 Linux ARM64 原生与 AMD64 仿真验证范围，不改变 Flutter Linux 产品阶段，也不自动关闭其他服务平台、发行审核、集成或发布条件。
