# SQLite 托管修正后的第二次独立验收

> 2026-10-31（Asia/Shanghai）。所有者批准将完整候选 `daf5a4d48d86deee2b259b1d8a76e16ec62a778a` push 到 dev，单次执行完整 `sqlite-platforms`，并归档、清理；失败先分析记录，不自动重试，不执行 Razor verify、发布或部署。接续[本地成组修正](./sqlite-hosted-validation-repair-2026-10-31.md)和[首次运行](./sqlite-hosted-native-run-2026-10-31.md)。

## 1. 执行身份

- [Run 41199463470](https://github.com/laugh0608/Radish/actions/runs/41199463470)，`workflow_dispatch`，attempt 1；本轮只派发一次，没有重跑旧 run 或个别 job。
- push 前远端 dev 为 `915f63591bfbfeff0978c8fb2de3b64fd997cce9`，祖先关系通过；push 后 GitHub API 确认完整候选 SHA。传输成功后的本地 origin/dev 锁文件受沙盒限制，核实远端后仅修正本地跟踪引用，未再次 push。
- 工作流 blob 为 `ecf481eb2b171985f0721efaff220beff0ac3a3b`，远端读取内容与本地 hash 一致；输入 `validation_scope=sqlite-platforms`、`expected_head_sha=daf5a4d48d86deee2b259b1d8a76e16ec62a778a`。
- 七个 SQLite job 执行；Razor 与原 Native Images 四类 job 均 skipped。输入由派发命令、job 环境和批准 SHA 门禁交叉确认，不声称取得独立 event payload。

## 2. 逐项结果

本次运行于 `2026-10-31 20:24:33 +08:00` 完成：**6 success / 1 failure / 4 skipped**。七份 artifact 均已校验。Windows 的新测试清理失败使整批验收保持未通过；没有自动重试。

| 目标 | 已确认结果 | 边界 |
| --- | --- | --- |
| Linux x64 | 通过；双配置各 60 项，16 份 publish / 独立探针编译，48 个冷进程、4 次图片自检 | 12 组实际 argv 通过；完整 TRX、清单与清理已校验 |
| Windows x64 | Release 定向测试 57 / 60 通过，3 项失败 | 24 组实际 argv、MSVC x64 样本、Rust 双配置及应用 Release 严格构建通过；未进入 publish / 冷探针 / Debug 应用构建 |
| Intel macOS | 通过；双配置各 60 项，16 份 publish / 独立探针编译，48 个冷进程、4 次图片自检 | 12 组实际 argv、C / C++ x64 链接、完整 TRX 与清理通过 |
| AMD64 API final | 通过；4 mode、图片自检、漏洞策略 | 16 项 recurring registration、成功 / 重试探针通过 |
| AMD64 Auth final | 通过；3 mode、漏洞策略 | 本宿主独立探针编译与实际加载通过 |
| AMD64 Gateway final | 通过；2 mode、漏洞策略 | 本宿主独立探针编译与实际加载通过 |
| AMD64 DbMigrate final | 通过；3 mode、漏洞策略 | 本宿主独立探针编译与实际加载通过 |

macOS 实际使用 Xcode `16.4 / 16F6`、SDK `15.5`、Apple clang `17.0.0`，`clang++` 调用名保留，realpath 指向 clang；两个样本均为 x64 且未执行。Windows 实际 runner 为 `win25-vs2026 / 20261021.278.1`，MSVC tools `14.51.36231`，x64 样本通过。三个原生平台共 48 组实际 MSBuild argv 通过，覆盖本批原 Windows 参数缺口。

本轮实际执行 300 项目标测试（297 passed / 3 failed / 0 skipped），不是原计划全部 360 次：Windows Debug 未执行。Linux / macOS 共 32 份 publish、32 次探针编译、96 个冷进程及 8 次图片自检；四 final 另有 4 次编译、12 个冷 mode 和 API 图片自检。

四个 final 均以 `1001:1001` 运行验证；12 组 0700 case 的容器写入 / 回读 / 文件所有者核对 / 删除，以及四组 0500 目录拒绝写入均通过。原 final Config.User 另行记录，不代表正式镜像用户策略改变。四次 Trivy 评估 HIGH / CRITICAL 零发现，无例外放行。

## 3. Windows 新失败：临时 SQLite 文件生命周期

完整 `Release.trx` 发现 60 项，57 passed、3 failed、0 skipped；三项均为 `AuthOpenIddictMigrationTest`：

- `ApplyMigrationsAsync_ShouldInitializeEmptySqliteAndBeIdempotent`
- `ApplyMigrationsAsync_ShouldAdoptCompleteEnsureCreatedSchema`
- `ApplyMigrationsAsync_ShouldRejectIncompleteEnsureCreatedSchema`

三份堆栈均在 `Radish.Api.Tests/Auth/AuthOpenIddictMigrationTest.cs:420` 的 `DeleteTemporaryDatabase → File.Delete` 抛出 `IOException`，报告临时 `.db` 被占用。原 Windows argv 缺失不再出现；这次停止于迁移测试清理，不能继续沿用首次运行的工具编排失败分类。

只读代码核对：三个用例为唯一临时路径拼接 `Data Source=...`，通过 `UseSqlite` 建立上下文，退出 try 后直接删除文件；未指定 Pooling 或清空对应 SQLite pool。Microsoft.Data.Sqlite 的 [Pooling 默认启用](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings#pooling)。默认连接池持有物理连接与 Windows 删除失败现象相符；相邻 `SqliteNativeContractTests` 已为自己的临时文件明确设置 `Pooling=False`。

这是基于代码、默认契约与失败堆栈的原因推断，尚未捕获实际句柄所有者，也没有修改后重跑。finally 异常可能遮蔽先前断言，不能把这三个迁移用例视为业务断言通过。测试进程退出后，本 job 精确清理成功，`workingTreeRemoved=true`。

建议下一批只确认临时 SQLite 夹具的连接所有权 / pooling 和删除顺序：统一构造临时连接，保留迁移断言与删除失败可见性，不吞异常、不添加删除重试、不改变生产连接池。当前仅分析记录，没有实施修复或新增运行授权。

## 4. 归档与清理

本地归档目录为 `artifacts/sqlite-hosted-native/daf5a4d4/`，保留授权 / 派发事实、工作流快照、服务端元数据、原 ZIP、原始解包证据、解码 job 日志和独立分析。下载使用 GitHub 插件返回的文件引用，临时签名链接不写入项目记录。

七份 ZIP 的服务端 size / SHA256 digest 和内部 manifest 的文件集合、路径、size / hash 全部校验通过：共 **1434 个 payload 文件**，另有七份内部 manifest。七份 candidate 均匹配本轮 SHA / run ID / attempt，七份 cleanup 均 completed / workingTreeRemoved=true、errors=[]；696 条受控命令全部有 completed 终态，无 timeout、取消或 killError。四个 Docker job 共删除八个任务 image tag，未 prune 共享 base image / BuildKit cache。

本地总归档 manifest 覆盖 **1470 个文件**，SHA256 为 `cc333cbddd63731c3d8db0952446e9d43cb4f67e6adfce08778093e07024ae61`。完整逐 job / TRX / artifact 摘要见[结果 JSON](./sqlite-hosted-native-second-run-2026-10-31.json)。原 ZIP、原始解包内容与七份解码 job 日志保持，分析文件单独保存。

本机未安装工具、启动服务 / 容器、改动运行代码或重复派发。托管 VM 的最终物理销毁没有独立观察，仍以任务清理证据与 GitHub 托管生命周期区分记录。

下一步先确认 Windows 临时 SQLite 测试夹具的成组修正与验证边界，再对新的完整 SHA 确认所需远端运行。本次一次性授权已执行完毕；Windows 的 Debug、publish 和冷进程仍未验收，不以其他平台成功替代。
