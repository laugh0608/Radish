# Hangfire 来源与源码交付材料复核

> 2026-11-04（Asia/Shanghai）。所有者要求提交 SQLite 本轮结果并继续下一步；结果文档已提交为 `864f9f99`，本批随后只做本地来源 / 留存核查和材料方案准备。正式执行边界归于[重建与交付方案第 8 节](../features/hangfire-rebuild-delivery-plan.md#8-接续原包来源与正式源码交付准备)，机器摘要见[JSON](./hangfire-source-delivery-review-2026-11-04.json)。

## 1. 输入与留存

本地原归档 `artifacts/source-bundles/radish-hangfire-source-20261031-c9e4` 的 `evidence-index.json` SHA256 仍为 `f788ba5a979b3fc94fc3eab813003231482db19161d4f190746342f7812c2df8`；覆盖 **869 文件 / 160,350,382 字节**，完整集合、size / SHA256 逐项通过。索引自身及后续 `cleanup.json` 不在该索引内，清理报告另保留，不将文件数量混算。

在本批临时目录从三个本地 bundle 建只读核验副本，执行 `git fsck --full --strict`、`git bundle verify`、冻结 commit / tree 与完整树核对；没有重新取得远端源码或执行上游脚本。

| 输入 | 冻结 commit | tree / blobs |
| --- | --- | --- |
| Hangfire | `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f` | `b5ee5d721d64442d4bff4d9592bd05e9f5fda421` / 607 |
| Cronos | `9d6e349e58fda615e47be10c3d9a539e9ce85073` | `d6fe40daf2b07b26f837fa36fb47002ca38ca9c7` / 38 |
| PostgreSql | `a5a728233ac8feb447695acb01f607bd848b00f7` | `f6f0556f83951060c076a08500e8a42435c8262a` / 108 |

三个 bundle 均无前置对象要求，753 个 blob 的路径 / 模式 / Git 对象与 tree 绑定。42 个 feed archive 的 hash 受原索引覆盖，本批另从各 nuspec 核对精确 id / normalized version，与 `Scripts/hangfire/inputs.json` 的 42 项集合完全一致；未重新执行 contentHash 算法或 restore，不将本批包身份核对写成新审计 / 构建。

## 2. 原 PostgreSql DLL 的新增来源声明补证

读取仓库本地 `.nuget/packages` 的原 `Hangfire.PostgreSql 1.20.12` archive，SHA256 为 `8112b10e784e5bbf6dac3955835bd56511c868aced725fad784199bb655e7e04`。包内 nuspec 版本为 `1.20.12+2`，repository commit 为冻结的 `a5a728…`；文件集中没有 PDB。

原 DLL SHA256 为 `659c72e0e1f458154d6244b32697181e2dc538c2f88eae50a4249cadd1faff72`，与旧报告及本机 API Release 输出逐字节一致。本批使用现有 SDK `10.0.300` / runtime `10.0.8`、SDK 自带 Roslyn / reference assemblies 严格编译无 PackageReference 的只读元数据程序，未反射加载或执行原 DLL。得到：

| 字段 | 实际值 |
| --- | --- |
| AssemblyVersion / FileVersion | `1.20.12.0` |
| AssemblyInformationalVersion | `1.20.12+2.Branch.master.Sha.a5a728233ac8feb447695acb01f607bd848b00f7` |
| AssemblyMetadata RepositoryUrl | `https://github.com/frankhommers/Hangfire.PostgreSql` |
| MVID | `bfc53de0-5896-4f22-b0da-3e61a6c82530` |
| CodeView GUID / age | `b4c7b3c6-a4e2-4935-9bf0-21ce1826eb98` / `1` |
| portable stamp | `c60b058f` |
| PdbChecksum SHA256 | `c6b3c7b4e2a435a99bf021ce1826eb988f050bc602d768edaedd734e25808e5a` |

只读复核[官方 tag](https://api.github.com/repos/hangfire-postgres/Hangfire.PostgreSql/git/ref/tags/1.20.12)仍直接指向该 commit；[Release 1.20.12](https://github.com/hangfire-postgres/Hangfire.PostgreSql/releases/tag/1.20.12)没有附加 asset，当前按该 head SHA 查询 Actions 返回 0 个保留运行。这个结果只说明本次接口未返回运行，不能推断历史上没有构建。

此前原 DLL 无 SourceLink / 文档 checksum 的结论保持。新增字段证明原 DLL **声明**来自相同 commit，而非只有 nuspec 与 tag；它不证明全部编译源码逐字节对应，也不使适配重建 DLL 等同于原包。该层级已明确写入接续方案，不回改旧记录。

## 3. 官方符号获取与未关闭边界

依据 [NuGet 官方符号服务说明](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg)及 [Microsoft portable key 实现](https://github.com/dotnet/symstore/blob/main/src/Microsoft.SymbolStore/KeyGenerators/PortablePDBFileKeyGenerator.cs)，从原 CodeView GUID 构造 portable key，并附原 `PdbChecksum` 请求头。文件名按 [BuildKey](https://github.com/dotnet/symstore/blob/main/src/Microsoft.SymbolStore/KeyGenerators/KeyGenerator.cs)使用小写后的规范 URL 为：

```text
https://symbols.nuget.org/download/symbols/hangfire.postgresql.pdb/b4c7b3c6a4e249359bf021ce1826eb98FFFFFFFF/hangfire.postgresql.pdb
SymbolChecksum: SHA256:c6b3c7b4e2a435a99bf021ce1826eb988f050bc602d768edaedd734e25808e5a
```

该请求返回 **404**。首次保留原大小写的请求也返回 404，随后按官方 key 实现纠正请求路径；未以错误形式的请求单独推断不存在。symbol package 地址读取出现 TLS 握手超时，没有取得 HTTP 可用性结论。均保留原请求结果，没有关闭证书校验或安装工具，未取得任何新 PDB / 符号包。

因此只记录“本批未取得匹配 PDB”，不宣称符号永久缺失。后续若取得 PDB，应先匹配 GUID / stamp / PE checksum，再校验 SourceLink 的 repo / commit / 路径与逐文件 checksum；不接受只有可打开的同名 PDB。

## 4. 交付材料分层与 Windows 状态

原归档保存了三个完整历史 bundle、42 项 feed、工具源码、适配 / tracked diff / 新增 targets / 修改 C#、DLL / PDB 审阅副本、调用与重放证据。它足以继续本地审阅，本批没有将其改名为正式对应源码发行包。

本批从冻结树核对 Dashboard 31 个资源，以及总 NOTICES 的 15 个组件标题；资源包括五类第三方 JavaScript、Bootstrap CSS 内嵌 normalize.css 与五个字体文件。标题数量不是实际交付组件数量，尚需按具体文件对应来源、许可和材料；不在本批增加、升级或删除这些资源。[第 8 节](../features/hangfire-rebuild-delivery-plan.md#8-接续原包来源与正式源码交付准备)已给出候选纳入表、默认留在审阅层的文件及实际下载入口方案，等待确认后实施生成 / 检查接线。

只读复核 [runner-images#15201](https://github.com/actions/runner-images/issues/15201)仍为五条评论，[最新维护者回复](https://github.com/actions/runner-images/issues/15201#issuecomment-6580306089)仍待部署后跟进，没有修复完成或精确版本确认；未发送新消息、派发 inventory / verify 或放宽身份门禁。

## 5. PostgreSql 上游询证草稿（未发送）

拟向 `hangfire-postgres/Hangfire.PostgreSql` 公开 issue 询证；本节只是待审阅文本，尚无发送授权。

**标题：** Source / matching Portable PDB provenance for NuGet Hangfire.PostgreSql 1.20.12

```text
We are reviewing the source provenance of the published NuGet package
Hangfire.PostgreSql 1.20.12 (nuspec version 1.20.12+2).

The package repository commit, the current 1.20.12 tag, and the DLL's
AssemblyInformationalVersion all identify:
a5a728233ac8feb447695acb01f607bd848b00f7

The netstandard2.0 DLL has SHA256:
659c72e0e1f458154d6244b32697181e2dc538c2f88eae50a4249cadd1faff72

Its Portable PDB identity is:
CodeView GUID b4c7b3c6-a4e2-4935-9bf0-21ce1826eb98, age 1,
stamp 0xc60b058f, SHA256 checksum
c6b3c7b4e2a435a99bf021ce1826eb988f050bc602d768edaedd734e25808e5a.

The package contains no PDB, and our lookup of this identity at the NuGet
symbol server returned 404. We have not inferred that symbols never existed.

Could you provide the matching Portable PDB / SourceLink information, or
point us to retained build provenance for this exact published DLL?
Please also confirm whether any source changes or generated build inputs
outside the tagged repository were used, and identify the SDK / GitVersion
and packaging inputs needed to reproduce it.

We can rebuild an adapted version from the tagged repository, but are
keeping that separate from verification of the original published binary.
```

草稿只包含公开包、DLL 身份和源码 commit，不发送 Radish 业务代码、配置、凭据或本地完整归档；也不要求上游证明 Radish 的全部发行义务。

## 6. 证据与执行范围

本批证据位于 `artifacts/source-readiness/hangfire-delivery-20261104/`，包括只读检查源码、原元数据 / nuspec、归档核验、完整 Git 树、42 项包 hash、上游响应和符号请求结果。检查副本与临时程序在保存结果后清理；原 2026-10-31 归档保持不变。

本批总索引覆盖 12 个文件（不含自身），SHA256 为 `5a2eb8bac2c249eeda890a7d1ac13d6ac8df9ea1c404af89b9f9d118598b8b2b`，逐项回读核对通过。`npm run check:repo-quality:changed` 与 `git diff --check` 通过；仅保留既有 records 索引篇幅提醒，本批不扩展文档拆分。

本批未安装依赖 / SDK、未运行旧重建阶段或项目服务 / 数据库、未重新执行 18 组调用、未改应用图 / 正式 DLL / ThirdParty、未 push / 远端执行 / 对外发消息 / 发布 / 部署。后续需要的外部询证、材料生成接线与新运行按方案分别确认。
