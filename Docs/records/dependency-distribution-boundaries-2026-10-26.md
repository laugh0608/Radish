# 剩余依赖发行边界核对（2026-10-26）

> 承接 `1ec5d131` 的[重点 notices 接线](./nuget-notices-distribution-2026-10-26.md)，本批完成只读源码 / 产物核对与[后续裁决草案](../features/dependency-distribution-decisions.md)。根 LICENSE、业务代码、依赖、MSBuild、Docker、CI 与服务状态均未修改。

## 1. Hangfire 源码与包对应

从 NuGet nuspec 声明的 commit 获取两份官方仓库源码快照，仅下载和逐文件校验，没有运行其中的脚本或安装构建依赖。GitHub 连接器用于 tree / 文本读取；二进制 tar.gz 由公开 codeload 地址读取到本批临时目录。

| 仓库快照 | commit | 文件数 | 校验结果 |
| --- | --- | ---: | --- |
| Hangfire 核心 / 两个 ASP.NET 集成包 | `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f` | 607 | 596 份原始 Git blob 一致，11 份 sln / csproj 仅有上游 `.gitattributes` 指定的 CRLF 转换 |
| Hangfire.PostgreSql | `a5a728233ac8feb447695acb01f607bd848b00f7` | 108 | 全部原始 Git blob 一致 |

两份 Git tree 均完整、未截断，没有 submodule；archive 文件集合与 tree 的 blob 集合一致。第一次按原始字节核对在 `Hangfire.sln` 停止，检查上游 `.gitattributes` 后确认 11 项均仅换行差异；保留原 archive，不改写源码以制造相同哈希。逐文件明细保存在临时证据，仓库[证据摘要](./dependency-distribution-boundaries-evidence-2026-10-26.json)保存 archive 哈希、差异清单及 6 份构建文件原文 / Git blob 验证。

核对当前 API publish 中的四个 DLL：`Hangfire.Core / AspNetCore / NetCore / PostgreSql` 均与各自 NuGet 选中 runtime asset 逐字节一致，独立存在；仓库未声明 `PublishSingleFile`、`PublishTrimmed`、`PublishAot` 或自有 ILMerge / ILRepack 操作。这个结果证明 Radish 本批未修改这些库，不代表库本身没有上游合并步骤。

关键新增发现：上游 [psake-project.ps1](https://github.com/HangfireIO/Hangfire/blob/51647c20e36ab010f7dfc4fa8b1ed666070d7f4f/psake-project.ps1)会把 Cronos 合入 Core；Core csproj 还引用 LibLog、MoreLinq.Source、StackTraceFormatter.Source、StackTraceParser.Source 等源码包，构建依赖 `Hangfire.Build 0.4.4`。因此**两个仓库 tar.gz 不能直接称为完整 Corresponding Source**，还需核对合并 / 嵌入来源与构建输入。

PostgreSql nuspec 实际写 `1.20.12+2`，NuGet 解析版本为 `1.20.12`；源码使用 `GitVersion.MsBuild 5.11.1` 和 Git 历史计算版本，不能仅因 csproj 保留 `1.9.4` 常量而认定 commit 错误。本轮未做源码重建或修改库替换试验。

## 2. 发行条款对照

这是工程发行准备核对，不作已经满足所有法律义务的结论。依据为精确包内原文和发布方说明：

- [Hangfire 许可页](https://www.hangfire.io/licenses.html)列明其覆盖包与可选路线，并提醒第三方集成可有不同条款；PostgreSql 插件须独立处理。
- LGPL 第 4 节同时涉及应用侧条款、声明 / 正文、共享库替换或重链接路径，以及条件适用的安装信息。当前独立 DLL 和 notices 已成立，实际接口兼容修改后的可用性尚未证明。
- GPL 第 6 节的对应源码交付不能由本机缓存代替；网络分发路线需要与二进制相应的获取方式和清楚入口，并承担持续可获得性责任。此处以[包内 COPYING](../../ThirdParty/nuget/hangfire.core/1.8.14/COPYING)为准。
- Radish 根 LICENSE 第 3 节一般性禁止修改 / 逆向，第 5 节只明确第三方自身条款。因此应用侧为库修改调试所需的例外仍需权利人裁决；狭义草案已写入[方案第 2 节](../features/dependency-distribution-decisions.md#_2-hangfire-路线狭义例外草案)，未改根 LICENSE。
- SNI `5.2.0` 第 3 节还有应用内分发及下游条款等条件；原文随包交付只是其中一项材料事实。通用 publish 当前仍带 Windows runtime，不能用 Linux 部署意图假设已经不分发它。

GNU 官网 HTML / text 在本轮 web fetch 超时；LGPL 同时核对到 Hangfire 发布方[全文页](https://www.hangfire.io/licensing/lgpl.html)，GPL 使用已归档的精确包原文。不以未成功读取的网页作为额外验证证据。

## 3. NoDrive 可继续取得的本地证据

两个 SqlSugar DLL 的 PE debug directory 均只有 CodeView / checksum / reproducible 项，没有类型 17 的 embedded portable PDB；精确包文件集也没有独立 PDB。因此没有从本轮本地材料取得新的 SourceLink commit 绑定。DLL 哈希与此前记录一致，现有 Apache / MIT 及 SystemCommon 版本差异继续保留。

继续重复下载相同包或搜索同名 commit 不会自动消除该缺口。已准备基于公开包 ID / version 的上游询证草案；尚未发送 issue、讨论或邮件，也未将候选 commit 改写为确定来源。

## 4. 实施影响与后续

修改根 LICENSE 会影响 Rust native 输入：`Scripts/build-native-images.mjs` 对该文件做 fingerprint，并复制到 `native/licenses/LICENSE-Radish.txt`。任何批准的许可文本调整均需纳入后续 native 重建与候选复核，不允许绕过现有 `--verify` 门禁。

本批原始源码包、完整 tree、逐文件证明与读取文本保存在 `/private/tmp/radish-distribution-review.zzetph`。该目录不代表已上线的源码获取服务；未安装依赖、编译上游源码、启动项目 / 容器或创建发布资产。

后续先确认 Hangfire 保留路线是否允许所列狭义授权例外；然后冻结源代码交付、构建输入与修改库替换验证的具体工作包。NoDrive 外部询证和 SNI 平台 / 发行条款保持独立边界，具体裁决见[方案](../features/dependency-distribution-decisions.md)。

本批 `npm run check:repo-quality:changed` 与 `git diff --check` 通过：6 个变更文件、897 个文档 / 治理文件、846 个 Markdown 与 1,057 个本地链接均通过。仅有记录索引原有的建议篇幅提醒；未修改执行代码，因此没有重复构建或数据库回归。
