# Hangfire Core 源码重建与本机 SQLite 替换验证

> 2026-10-26（Asia/Shanghai）。按已批准的[试验方案](../features/hangfire-source-replacement-plan.md)执行，期间另获批准使用 SDK 自带分析器。只修改临时上游源码和 publish 副本；Radish 正式代码、依赖与构建规则未改。本记录关闭 Core / 本机 SQLite 的这批替换试验，不关闭完整源码发行。

## 1. 输入、失败与批准后的构建

输入为 Hangfire commit `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f`、已有 SDK `10.0.300` / runtime `10.0.8`、Core `netstandard2.0` 及 `ILRepack 2.0.27`。全程在 `/private/tmp/radish-hangfire-replacement-5bb6rlfu`，没有安装新 SDK、PowerShell、Mono 或 Windows 工具链。

13 个批准包全部核对 SDK NuGet API 的 contentHash，首次 `--locked-mode` 恢复通过，feed、包缓存与 HTTP 缓存隔离。上游临时适配只缩小目标并派生对应 lockfile；原始源码完整保留。第一次严格构建真实失败于 NetAnalyzers 8.0.0 与 SDK 10.0.300 的升级提示，没有跳过该 target 或用失败产物继续验证。

项目所有者单独批准后，移除临时上游 `Directory.Build.props` 的 `Microsoft.CodeAnalysis.NetAnalyzers 8.0.0` 引用及派生锁文件对应条目，使用 SDK 自带分析器。实际 restore 图为其余 **12 包**，版本 / contentHash 不变；保留 `EnableNETAnalyzers=true`、`AnalysisLevel=latest`、`AnalysisMode=Recommended`、原有 `NoWarn` 和 `--warnaserror`，没有新增抑制或关闭审计。

| 产物 | 源码变化 | 严格构建 |
| --- | --- | --- |
| baseline | 只有目标 / lockfile / 分析器适配 | 0 警告、0 错误 |
| modified | 在 baseline 上增加两行 `Trace.WriteLine` | 0 警告、0 错误 |

两行标记分别位于 `RecurringJobManager.AddOrUpdate` 与 `BackgroundJobClient.Create` 的实际路径，未修改公共签名。准备过程中曾产生仅有注册标记的中间构建，保留日志但未用于验收；最终修改库包含两个标记。

两组均用已校验 `Cronos 0.8.3` 原 DLL 按 `/target:library /internalize` 合并，ILRepack exit 0。其现有 runtimeconfig 允许 major roll-forward，实际运行于已有 .NET 10。macOS 上出现“source server data is only writeable on Windows”提示，保留日志；没有把调试元数据完整性算作通过。复用上游已生成 Razor 文件，没有复现 Windows Razor 再生成，也没有从源码重建 Cronos。

## 2. 产物身份与接口比较

三组从同一份前批 API publish 的 420 个文件复制，进程通过原 `Radish.Api.deps.json` / `runtimeconfig.json` 正常加载。baseline / modified 仅替换 `Hangfire.Core.dll`，其余 **419 个原文件 hash 全部不变**；original 的 420 个原文件和来源目录全部不变。三组额外探针 DLL 的 hash 相同，没有自定义加载器、签名绕过或回退原库。

| Core | SHA256 |
| --- | --- |
| 原包 | `a65de824dc686217326599c9b0c5e7b0f470bd4425ac0b891d6cdc2351df7522` |
| 源码基线 | `1173000b7e3d49c4a2f9bbc1ac604dbde804a8461c2bb3da854450092f0e419c` |
| 诊断修改库 | `f4c67526faa37682159cc04bc54e24d3d2184d86ee43835845eb27b2a9af3281` |

三者身份均为 `Hangfire.Core, Version=1.8.14.0, Culture=neutral, PublicKeyToken=null`。反射比较的 **214 个公开类型、2,738 条表面记录完全一致**，覆盖基类 / 接口、可见成员、签名、flags、泛型约束与参数默认值；不声称已经证明所有自定义 attribute、IL 或内部行为等价。三组引用的程序集身份也一致，重建文件不要求与原包逐字节相同。

探针使用现有 SDK reference packs 和 publish DLL 编译，无新包恢复。编译保留一条 CS1701：SQLitePCLRaw 的 System.Runtime 8 引用与 net10.0 reference pack 的身份假定；没有抑制，随后三组 .NET 10 实际执行均成功。该提示与上游 Core 的零警告严格构建分开记录。

## 3. 真实 SQLite 注册与执行

三组均调用正式 `ApiRecurringJobRegistration.Register`，只用内存配置启用可选每日统计，逐项读取真实 SQLite 中的注册并核对 ID、cron、时区、方法和参数。实际 publish native 的 `sqlite3_libversion()` 均返回 **3.50.4**；所有原生文件的 SHA256 冻结在机器证据中，未沿用测试目录 3.50.3 的假定。

| 观察 | original | baseline | modified |
| --- | ---: | ---: | ---: |
| 实际持久化并核对的周期任务 | 16 | 16 | 16 |
| worker 启动前剩余业务周期任务 | 0 | 0 | 0 |
| 专用队列探针执行 / Succeeded | 1 / 1 | 1 / 1 | 1 / 1 |
| 注册诊断标记 | 0 | 0 | 16 |
| 创建诊断标记 | 0 | 0 | 1 |
| 进程退出码 | 0 | 0 | 0 |

三组稳定注册观测逐项相同，原始数据库 hash 记录另行保留，生成 ID / 时间不用于跨组相等断言。真实 Radish 周期任务全部删除并再次确认空集后，才启动单 worker、仅监听 `radish-replacement-probe` 队列；探针唯一行为是向本批文件写入一行文本，三组均恰好一行。未执行任何实际清理、商城或 Outbox 业务方法。

worker 等待上限 20 秒、停止上限 5 秒，外层每进程 45 秒超时；本批未触发超时。`using` 释放 worker / storage，各子进程正常退出后，对三个 SQLite 文件和执行文件留 hash 再精确删除，确认没有同名数据库 / WAL / SHM 残留。没有启动 API / Gateway / Auth 宿主、监听端口、使用容器或连接外部数据库。

## 4. 材料、验证结论与下一步

[机器摘要](./hangfire-core-replacement-evidence-2026-10-26.json)保存完整 DLL / native hash、12 包图、13 包 feed 校验、注册观测、诊断计数和清理条目。原始日志、接口清单、适配 patch、探针源码及逐次执行 JSON 保存在本批临时目录。

本地审阅包 `hangfire-core-replacement-review-2026-10-26.zip` 共 67 项、18,175,377 字节，SHA256 为 `8a996f0e0723a10bfdd852a646e766688fea77a3258ea6549e70c064e29d0413`，压缩后逐项重新核验。内含原始 Git 源码 archive、全部批准 feed archive、原包 / 两个重建 Core、工具、patch 和证据；不包含完整 Radish 运行包，也不是对外 Corresponding Source 下载入口。

本批证明：在本机 `osx-arm64` / .NET 10 / Core `netstandard2.0` / 当前 SQLite publish 下，源码重建基线和保持接口的诊断修改库可以替换原 Core，进入真实注册、入队及执行路径。未覆盖其他 TFM、平台、provider，未独立重建 NetCore / AspNetCore / PostgreSql，也未完成完整官方发行流程或所有 LGPL 发行条件。

下一步优先形成 SQLite 两条原生资产链统一方案，再确认依赖与构建修改范围；同时继续对应源码材料、Cronos 源码重建、PostgreSql 构建闭包及正式源码获取入口。NoDrive 询证、SNI 分发、全生态材料与 Valkey 保持独立范围。本批未推送、创建 PR、发布或部署。
