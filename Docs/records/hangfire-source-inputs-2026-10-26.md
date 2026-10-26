# Hangfire 源码补证、构建输入与 Cronos 声明

> 2026-10-26（Asia/Shanghai）。承接 `7a449453` 的许可例外与本机验证，继续源码 / 构建输入核对。本批补齐一个已合入组件的许可文件；没有执行上游构建工具、restore 新依赖或启动产品服务。

## 1. 实际 DLL 的源码证据

用本机 SDK `10.0.300` 编译无新增包的 PE / portable PDB 检查程序，只读取实际 publish DLL 的 metadata。Core、NetCore、AspNetCore 含 embedded portable PDB，PostgreSql 仅有外部 PDB 标识，没有内嵌 PDB。

| DLL | PDB 文档数 | 已验证 SHA256 | 明确未验证的条目 |
| --- | ---: | ---: | --- |
| Hangfire.Core | 233 | 204 个 Hangfire 仓库文件 + 7 个 Cronos 文件 | 22 个 Razor 行映射没有 checksum |
| Hangfire.NetCore | 12 | 11 个仓库文件 + 1 个内嵌生成文件 | 无 |
| Hangfire.AspNetCore | 10 | 9 个仓库文件 + 1 个内嵌生成文件 | 无 |
| Hangfire.PostgreSql | 0 | 无新增 PDB 文件绑定 | 仍按 nuspec commit 与前批源码快照处理 |

总计 **233 个带 checksum 的文档全部匹配**，不将另外 22 个条目计为哈希通过。初次探索比较误将空 checksum 当作不匹配；核对 GUID / blob 为空后单列为未验证，没有忽略真实 hash mismatch。

实际 Core 的 SourceLink 指向 Hangfire commit `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f` 和 Cronos commit `9d6e349e58fda615e47be10c3d9a539e9ce85073`。后者也与 `Cronos 0.8.3` nuspec 一致。新增 Cronos 完整仓库快照的 38 个文件已逐一对照完整 Git tree；仅上游 `.gitattributes` 指定的 sln / csproj CRLF 转换按 Git blob 的 LF 内容核验。

此前列为待补输入的 LibLog、StackTraceFormatter、StackTraceParser、MoreLINQ 已发现于 Hangfire 仓库，四份实际编译源文件也匹配 PDB checksum。它们不是需要从未固定的默认分支重新取得的缺失源码；原 NuGet 模板仍作为构建来源材料保留。

机器摘要见[源码证据](./hangfire-source-evidence-2026-10-26.json)，逐文件详情与原始材料保存在 `/private/tmp/radish-hangfire-source-inputs-bz46loib`。

## 2. 锁定输入与审阅包

从三个已交付 TFM 的上游 lockfile 和 `.nuget/packages.lock.json` 汇总 **27 个包 / 版本条目**；其中 Core 单目标 restore 为 13 个。检查前本机缓存有 9 个 archive，本批只读取得另外 9 个重点源码 / 打包包档案，未写入全局 NuGet 缓存或执行包内脚本。

9 个档案为 Cronos、LibLog、MoreLINQ.Pairwise、StackTraceFormatter、StackTraceParser、Hangfire.Build、ILRepack、psake、RazorGenerator.MsBuild，版本和哈希见[输入清单](./hangfire-build-inputs-2026-10-26.json)。首次网络读取出现一次 TLS 握手错误；保留成功档案并用相同官方 URL 重试，未关闭 TLS 验证。

所有 9 个包经 SDK 自带 `NuGet.Packaging.PackageArchiveReader.GetContentHash` 计算后与上游 lockfile 一致。签名 archive 的 SHA512 与内容 hash 分开保留；算法口径可见 [NuGet 源码](https://source.dot.net/NuGet.Packaging/PackageArchiveReader.cs.html)，没有用 archive hash 不同来跳过锁文件验证。

审阅 ZIP `hangfire-source-input-review-2026-10-26.zip` 位于本批临时目录，35 个条目、9,047,571 字节，SHA256 为 `55e4bba119a3c1a975935425ff5a9c7438c55388e0fc50dab95d88c963b87fe6`，压缩后已重新逐文件核验。它汇集三个原始仓库快照、九个包档案、逐文件 / PDB 证据与本批检查程序源码。它是本地审阅材料，**不是完整 Corresponding Source 发行包**；不存在正式下载入口或可获得性承诺，也未把其中的构建辅助二进制作为 Radish runtime 分发。

## 3. Cronos 许可补充与产物复核

既有 Hangfire 上游 `NOTICES` 没有列出实际合入的 Cronos。已从精确 `Cronos 0.8.3` 包内复制完整 MIT 正文到 [LICENSE-Cronos-0.8.3.txt](../../ThirdParty/nuget/hangfire.core/1.8.14/LICENSE-Cronos-0.8.3.txt)，原始 / 规范化 SHA256 均记录在既有 manifest；随后按既有命令生成派生 attribution、宿主 manifest 和 MSBuild 哈希输入，没有修改复制规则或 DLL。

`node Scripts/check-nuget-notices.mjs --write --assets` 与只读检查通过；四宿主均执行 `dotnet publish <host>/<host>.csproj -c Release --no-restore --warnaserror -o <本批临时目录>/publish/<host>` 并通过，API 额外诊断 publish 为 0 警告 / 0 错误。

| 宿主 | notices 组件 / 文件 | 发布文件数 |
| --- | --- | --- |
| Radish.Api | 11 / 42 | 420 |
| Radish.Auth | 6 / 16 | 586 |
| Radish.Gateway | 6 / 16 | 107 |
| Radish.DbMigrate | 6 / 16 | 212 |

所有 notices 路径、数量和 hash 均与仓库材料相同；仅 API 新增 Cronos 正文。四个 Hangfire DLL、其他第三方 DLL / 原生文件与上一批产物逐字节相同，Rust native 全树不变。Radish 自有 DLL / PDB 随当前 revision 重编译；另有下面单列的 deps 索引差异，不能概括为整个非 notices 产物均未变。

## 4. 新发现：SQLite 测试与发布基线不同

本次普通 publish 与前批 `--no-build` 的 deps 对比触发进一步检查，发现既有两条原生资产链同时存在：

- `Directory.Build.props` 各层引用 `SQLitePCLRaw.lib.e_sqlite3 3.50.3`。
- `Radish.Extension` 引用 `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`，传递引入 `SourceGear.sqlite3 3.50.4.5`；Hangfire SQLite 的旧 bundle_green 链又需要前一种包。
- SDK 日志记录同名 native 文件版本相等时的任意冲突选择。API / Auth / DbMigrate 的新 deps 移除了 SourceGear 的 29 个重叠 runtime 索引，但实际复制的 29 个文件匹配 SourceGear；Auth 还移除了 SystemCommon 的重复 SqlSugar compile 索引。依赖包版本未变，SqlSugar 仍为既定 NoDrive DLL。
- 对本机 `.dylib` 直接调用只读 `sqlite3_libversion()`：Tests Release 输出为 **3.50.3**，API publish 为 **3.50.4**；对应 SHA256 分别精确匹配上述两个包。本探针未创建数据库或启动服务。

因此，历史“保留 native 3.50.3”应理解为引用 / 测试输出观察，不能扩展为发布文件身份已固定。前批测试通过不等于已覆盖当前 publish 的 SQLite native 二进制；历史文件哈希不变也不能消除这一差异。本批仅纠正证据边界，没有手工删除资产、使用 `ExcludeAssets` 或调整依赖。

## 5. 下一批与停止线

[Core 替换试验方案](../features/hangfire-source-replacement-plan.md)已冻结 13 个 restore 输入、现有 ILRepack、实际 publish 副本和临时 SQLite worker 的验证范围，等待明确批准；没有先执行源码重建或 worker。该试验以实际发布包的 native hash / version 为基线。

统一 SQLite 原生依赖与 build / publish 一致性需要单独方案；完整上游 Windows / Razor 流程、PostgreSql 独立重建、正式源码获取入口、NoDrive 询证与 SNI 条款继续保留未关闭。本批未推送、发 PR、打 tag 或发布。

最终文档与卫生检查通过：15 个改动文件、903 个文档 / 治理文件、849 个 Markdown 与 1,077 个本地链接；仅保留 records 索引既有建议篇幅提醒。`git diff --check` 通过，notices 只读校验通过。
