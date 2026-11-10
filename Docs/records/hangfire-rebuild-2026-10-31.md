# Hangfire 本机源码重建与隔离调用验证

> 2026-10-31（Asia/Shanghai）。[实施方案](../features/hangfire-rebuild-delivery-plan.md)与 NetCore 日志补丁均已批准并执行。本机工具链、严格构建、18 组最终 DLL 调用、干净目录重放及清理完成；本记录不关闭 Windows Razor、原 PostgreSql DLL 源码绑定或完整发行审查。精确结果见[JSON](./hangfire-rebuild-2026-10-31.json)。

## 实施范围

准备材料先提交为 `e4ed1dc7`，应用隔离基线固定 `9bc15795`。新增 `Scripts/hangfire/` 专题工具，不接入默认 build / CI；正式 csproj、NuGet.Config、默认 SDK 策略、ThirdParty 与 219 项应用依赖图未改。未 push、触发远端工作流、发布或部署。

任务 `radish-hangfire-source-20261031-c9e4` 只在私有临时目录安装 SDK `10.0.403` / runtime `10.0.14`，官方 archive SHA512 与准备清单匹配。全局 SDK 始终为 `10.0.300`。三个完整 mirror 经 fsck、固定 commit / tag 核对并封存 Git bundles；42 个 nupkg 的 archive hash 与 NuGet contentHash 均核对，五个选择工程的 assets / download-only ref pack 联合为 41 项，另 1 项为 ILRepack `2.0.27`。未增加允许清单之外的包或旧 runtime。

准备期 GitHub tree 响应的 `sha` 曾误记为 tree SHA，实际是查询 commit。执行期以 `git rev-parse <commit>^{tree}` 更正机器契约；三个仓库全部 753 个 blob 的路径、模式与 hash，以及 34 个已审阅文件均与准备输入一致。原准备快照保留，更正值及证明进入本记录 JSON 和归档 `evidence/tree-field-correction.json`，没有更换源 commit。

## 源侧适配与构建

| 输入 / 目标 | 实施与验证 |
| --- | --- |
| 分析器 / SourceLink | 移除显式 8.0.0 包引用，采用固定 SDK 内置任务；保留上游 AnalysisMode、NuGet audit 与 warnaserror，未增加 NoWarn 或禁用审计 |
| Core / 插件 JSON | Newtonsoft.Json 固定 13.0.4；插件 System.Text.Json 固定 6.0.10；其余 42 项精确边界不变 |
| JsonStats | 以 `CamelCaseNamingStrategy` 替代弃用的 `CamelCaseText`，通过普通 / 缩写枚举与 EnumMember 值等价性、真实 Dashboard POST stats 核对 |
| NetCore CA1873 | 单独批准后加 `ILogger.IsEnabled` 守卫；关闭时不求值消息且返回 false。启用、关闭、级别查询、消息回调及 logger 异常传播均验证；原包组保留旧行为对照 |
| GitVersion | 5.11.1 原任务在私有 runtime major roll-forward 下成功；固定本地 master / commit，禁 fetch、normalize、cache，实际产生 1.20.12 / 1.20.12.0，未手工冒充版本 |
| Core 合并 | ILRepack 使用本次源码 Cronos；输出程序集保持 Hangfire.Core 身份，公开接口 / 资源与原包一致 |

五个 Release 目标为 Cronos netstandard2.0、Core netstandard2.0、NetCore netstandard2.1、AspNetCore netcoreapp3.0、PostgreSql netstandard2.0。全部完成 file-feed restore、精确图核对、locked restore 和严格 build，零 warning / error。插件保留 Core 1.8.0 / Npgsql 6.0.11 的编译边界；实际应用调用仍绑定正式 Core 1.8.14 / Npgsql 10.0.0，没有改 deps 或运行库依赖来掩盖替换问题。

隔离应用 19 个工程 / 219 项图匹配，NuGet audit 无直接或传递发现，Debug / Release 严格构建与两份 generic API publish 通过；两配置既有 osx-arm64 native 均核对，通过既有 SQLite / notices 门禁，没有重编 Rust。

## PDB 与重放

逐文件校验发现早期编译 PathMap 与 SourceLink key 使用不同根，虽然当时接口、资源及方法 IL 一致，DLL hash 不同，因此没有放行。现已统一为 `/_/sources/{hangfire|cronos|postgresql}/` 与 `/_/packages/`，区分合并后的不同仓库；路径映射按 SourceRoot.Identity 设置，防止映射互相覆盖。

最终校验同时要求实际 PDB checksum、SourceLink 路径覆盖及对应仓库 / commit / 相对文件一致。所有修改 C# 嵌入 PDB 并逐字节校验；PostgreSql 相邻 portable PDB 还须匹配 PE CodeView GUID / stamp 和 PDB checksum。checksum 校验遵循 [dotnet/runtime PE-COFF 规范](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PE-COFF.md)的 PDB ID 归零算法。

源码基线 Core / NetCore / AspNetCore / PostgreSql 分别核对 211 / 12 / 10 / 46 个带 checksum 的文档，诊断 Core 为 212 个。Core 另有 22 个既有 Razor `.cshtml` 文档无 checksum，明确列为 Windows 再生成边界，不伪造通过；对应输入与已生成 C# 均在 bundle 中保存。

在新的源码目录和新空包缓存从 bundles / feed 再构建，**五个基线 DLL 的 SHA256、MVID、公共接口、资源与方法 IL 全部一致**，包括合并后的 Core。该结论只比较本次适配的两轮构建，不声称与上游原包字节一致。完整 hash 见结果 JSON。

## 最终运行矩阵

| Storage | Debug 原包 / 源码 / 诊断 | Release 原包 / 源码 / 诊断 |
| --- | --- | --- |
| SQLite 3.53.3 | 3 / 3 通过 | 3 / 3 通过 |
| PostgreSQL 17.11 ARM64 | 3 / 3 通过 | 3 / 3 通过 |
| PostgreSQL 16.15 ARM64 | 3 / 3 通过 | 3 / 3 通过 |

18 组均校验实际 DLL 路径 / hash、16 项真实 API 周期注册后移除、单 Hangfire HostedService 启停、一个成功任务与一个失败后重试成功任务，以及内存 Dashboard GET / POST stats 200。合计 288 项注册核对、36 个最终 Succeeded 任务、54 次任务尝试；诊断组均产生预期注册 / 创建标记。未启动 Kestrel 或业务宿主。DataProtection 使用内存临时 provider，其 HostedService 未启动。

最终源码 DLL 修正映射后重新执行 12 组源码 / 诊断组，保留 hash 未变的 6 组原包结果；逐项确认 18 份记录与最终元数据报告的 DLL hash 一致。早期 12 组与旧重放保留在 `superseded-pre-canonical-sourcelink/`，不计入最终数量。第一轮 HostedService 选择错误的失败也保留，后续只筛选确切 Hangfire service，并断言数量为 1。

8 项 Node 契约测试通过，覆盖输入 / 包漂移、精确下载版本、公共契约、源码映射 / checksum / 越界读取、所有权与 CLI 参数。另用真实 .NET 进程验证损坏 PDB、非法 provider 参数与非测试数据库端点均被拒绝；这些预期异常的 SIGABRT 和错误信息单独记录，不当作业务用例成功。归档副本的 8 项测试也通过。

## 归档与清理

本地审阅目录为 `artifacts/source-bundles/radish-hangfire-source-20261031-c9e4`，共 869 文件 / 160,350,382 字节；索引 SHA256：

```text
f788ba5a979b3fc94fc3eab813003231482db19161d4f190746342f7812c2df8
```

包含三个 Git bundles / refs、42 项 feed、工具源码、原始 / 派生锁、实际 diff / 修改源码、许可证 / Dashboard 资源索引、DLL / PDB 本地审阅副本与完整执行证据。SDK archive / 安装和业务应用源码 / API publish 未纳入归档。它是本地审阅材料，不是正式发行入口；归档重建说明与工具 README 均注明授权及所需基线。

逐文件重新核对归档索引后清理：PG17 / PG16 均按 ID + 标签删除本任务容器 / 卷，schema 检查删除完成，15432 无监听，私有 SDK / 源码 / 缓存 / bin / obj / 测试数据所在任务根已移除。进程表核对本任务私有 dotnet / 编译器 / 探针残留为 0；共享 PG 镜像与准备证据保留，没有改代理或全局 SDK。

## 接续边界

本机重建与调用证明完成。下一批先核对 Windows Full MSBuild / RazorGenerator 2.5.0 的托管 runner 库存、精确命令与输入 / 输出关系；实现 CI、push 或远端执行分别按范围确认。原 PostgreSql 包 DLL 没有取得新的源码绑定证明，正式源码获取入口与完整发行审查保持独立事项；SQLite 剩余原生托管矩阵未远端执行。NoDrive 询证、SNI、Valkey 与日志 L2 不因本批自动取得新授权。
