# SQLite 探针模块识别与失败诊断的本地修正

> 2026-11-04（Asia/Shanghai）。所有者批准先提交上轮结果文档，再按[成组方案第 9 节](../features/sqlite-hosted-validation-repair-plan.md#windows-module-probe-followup)完成本地修正与验证；新完整 SHA 和远端范围另行确认。未 push、派发、发布或部署。

## 1. 提交与实现范围

上轮七份结果文档已提交到 `dev`，完整 SHA 为 `1e1d0ff1022b0f59f849b30352e69243fbb40a42`，使用当前用户 Git 身份，提交 hooks 通过。本批代码验证针对该提交之后的工作区，不把该文档提交当作包含探针修正的新候选。

`Scripts/sqlite/SqliteProbeModuleInspector.cs` 承接现有模块枚举与识别职责；Windows / Linux 继续使用 `Process.Modules`，macOS 使用 dyld 已加载映像及加载地址。名称子串仅收集相关诊断候选，原生身份须满足平台精确名称 / 格式；托管 provider 须通过 PE / CLR ILOnly、程序集名、模块名与文件名联合核对。未知身份仍失败，不主动加载候选库，也不以直接排除某个文件名作为身份依据。

零个、两个原生候选都拒绝；同路径 / 同加载地址的重复记录单独报错且保留原条目，同文件不同加载地址也不放行。候选文件的 SHA256 在解析前写入，文件读取 / PE 解析失败仍保留已取得的清单与错误。数量判断前已落盘 `module-inventory.json`；主探针失败结果追加阶段与清单位置，编排器白名单同步保留该文件。

四宿主编译输入绑定新增公共识别源码的 hash；Tests 项目直接链接同一文件，验证实现行为。没有新增依赖、修改 SQLite contract / hash / 版本、改变生产连接池 / 业务代码、CI job 或 mode 矩阵；原始依赖图、真实加载路径 / hash、SQLite `3.53.3` / source ID、编译输入绑定仍受检查，另复核检查期间 native hash 未变化。

## 2. 本地构建与行为测试

环境沿用 macOS `27.0.1` / arm64、.NET SDK `10.0.300`、runtime `10.0.8`、Node `24.16.0`。业务项目构建使用已有 assets 与 native 产物，`--no-restore --warnaserror`；临时探针仍通过既有无 PackageReference 工程还原 / 编译，没有安装或更新依赖 / 工具。

| 验证 | 结果 |
| --- | --- |
| Tests 项目及宿主引用 Debug / Release 严格构建 | 均为 0 warning / 0 error |
| 五组定向 filter，排除 PostgreSQL | 两配置各 **82 passed / 0 failed / 0 skipped** |
| 其中新增模块行为用例 | 两配置各 22 项；原 SQLite 三个迁移用例也均通过 |
| Node 探针契约 / 编排器 / 原生来源契约 | **32 passed / 0 failed** |
| 四宿主、两配置的独立探针编译 | **8 份**，全部 SDK `10.0.300`，无 PackageReference |
| 四宿主、两配置的实际 macOS arm64 冷进程 | **24 个 mode 全部通过** |

定向 filter 保持：

```text
(FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~DbMigrateBootstrapTests|FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~AttachmentServiceUploadContractTest)&Database!=PostgreSQL
```

构建使用 `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c <Debug|Release> --no-restore --warnaserror`；测试使用 `dotnet test` 的同项目、配置和上述 filter，追加 `--no-build --no-restore --logger 'trx;LogFileName=<配置>.trx' --results-directory artifacts/sqlite-probe-module-repair/2026-11-04`。初轮 Debug 79 项通过后补入三个格式 / PE 边界用例，最终两配置均为 82 项；初轮 TRX 另存，不计入最终 164 次结果。

Node 命令为 `node --test Scripts/sqlite/probe-contract.test.mjs Scripts/ci/validate-sqlite-platform.test.mjs Scripts/check-sqlite-native.test.mjs`。新 C# 用例使用受控 PE / ELF / Mach-O 文件头和实际托管 provider 文件，覆盖三平台名称比较、provider 元数据、同 hash 的两个原生文件、重复条目 / 不同加载地址、零命中、未知 / 伪装 / 错误格式、损坏 PE、缺文件及清单保存；不把文件头夹具描述为 Windows 进程运行证据。

实际探针使用本机四宿主既有 `bin/<配置>/net10.0`，逐份执行现有 `--compile-from / --output / --host / --scratch` CLI。每配置 API 4 mode、Auth / DbMigrate 各 3、Gateway 2，共 12 个，双配置 24 个。所有编译清单的源码 hash 与本批工作区一致；各 mode 的实际 native 路径位于对应宿主输出，hash 为 `09fe20a6c050912a7944b2f2f2981debe31679f7919f523c87251ae8cf0f2daa`，版本及 source ID 匹配原 contract，每份清单恰好一个原生候选。这是本机已有输出的验证，没有新增 publish、容器或托管平台验收。

## 3. 证据与清理

本机忽略目录 `artifacts/sqlite-probe-module-repair/2026-11-04/` 保留完整 TRX、命令记录、源码指纹与 `validation-summary.json`。最终 TRX SHA256：

| 文件 | SHA256 |
| --- | --- |
| Debug | `832aa130c39e1c3b8d7b433c29d887fff9c7e32bc2937de79b006588c2c29442` |
| Release | `631eca699930793ac3b7caeda8f3d7f1636f9f32ef79677dace83053ed52f18d` |

使用实际 `retainProbeEvidence / assertEvidence` 白名单保留 **124 份诊断文件**，其中包括 24 份真实模块清单；文件集合及 hash 清单保存为 `evidence-manifest.json`。仅删除本批探针 scratch；八个宿主输出中的临时 `SqliteNativeRuntimeProbe.dll` 均已删除，测试自有临时目录按用例清理。没有启动项目服务 / 容器或触达生产，没有清理他人缓存。

`npm run check:repo-quality:changed`、`npm run lint:changed` 与 `git diff --check` 均通过；Lint 明确无前端脚本适用。仅保留验证基线与记录索引的既有篇幅提醒，不扩展文档拆分。

## 4. 完成边界

本地实现、识别行为、失败留痕和 macOS arm64 实际加载已完成验证。旧 Windows 两个实际命中项仍未知；本地分类用例不能回溯确认旧根因，也不能替代新的 Windows `Process.Modules`、双配置、publish 与冷进程验证。远端新候选仍需新完整 SHA 和单独批准的执行范围，旧 run 不重试；本批未改宿主依赖或放宽任何来源 / 身份门禁。
