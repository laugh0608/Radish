# 重点 NuGet notices 分发接线方案

> 状态：2026-10-26 项目所有者批准后已实施，四宿主构建 / publish 接线与定向验证完成，见[实施记录](../records/nuget-notices-distribution-2026-10-26.md)。上游补证见[来源核对记录](../records/nodrive-source-review-2026-10-26.md)；本方案承接[NoDrive 正式采用](./nodrive-adoption-plan.md)，不更改业务或依赖版本。

## 1. 目标与边界

让已整理的重点 NuGet 许可正文、署名与来源说明随 API / Auth / Gateway / DbMigrate 的 build / publish 产物交付，避免仅存在开发机 NuGet 缓存或临时审查包中。项目根 LICENSE 保持原定位，第三方材料分别保留各自条款。

本批覆盖 13 个已核对组件，其中 11 个属于当前 runtime 图，2 个仅构建使用；不宣称覆盖全部 226 条 NuGet 依赖或整个 .NET / Web / Flutter / Rust / 镜像生态。完整分发状态、源码提供等独立义务仍在[许可治理专题](./dependency-license-governance.md)维护。

2026-10-31 按已批准的[SQLite 统一方案](./sqlite-native-unification-plan.md)追加六个 runtime 组件及其原文 / attribution，当前 catalog 共 19 个组件（17 runtime / 2 build-only）；具体输入以仓库 manifest 为准。原批次计数保留其历史含义，不扩大为全生态许可放行。

## 2. 原文与目录

已按以下边界新增文件，实施未安装包、下载或执行第三方代码：

- `ThirdParty/nuget/`：按小写 package ID / 精确 version 保存已审查正文、署名 / 来源说明；runtime 与 build-only 的用途由清单字段区分。
- `ThirdParty/nuget/manifest.json`：记录 package ID、版本、作者、copyright、原始许可声明、来源 URL / commit（未知则留空）、分发宿主、原始正文 SHA256、仓库副本 SHA256 与未关闭证据项。
- `ThirdParty/nuget/NOTICE-INDEX.txt`：解释覆盖范围及逐组件索引，不声称已取得商业授权或完成全生态审核。
- `Scripts/build/nuget-notices.props`：显式列出受控包版本、材料路径、目标相对路径及 SHA256。由清单生成或核对，不根据文件名递归猜测许可。
- `Scripts/build/nuget-notices.targets`：校验并复制材料；仅从 `Directory.Build.targets` 导入。
- `Scripts/check-nuget-notices.mjs` 及其定向测试：开发时校验 JSON / MSBuild 映射一致、来源哈希、宿主覆盖及缺失失败行为；仅使用已有 Node 标准库，不加入 Docker 构建依赖。

日常校验入口为 `npm run check:nuget-notices` 与 `npm run check:nuget-notices:self-test`。经过审查的清单 / 原文变更用 `node Scripts/check-nuget-notices.mjs --write --assets` 重建派生声明与 props；原始材料目录可用 `--source-root=<目录>` 单独复核，不能把分发副本当作上游原文。目录布局见[验证基线](../guide/validation-baseline.md)。

为遵循仓库文本卫生，分发副本仅规范化为 UTF-8 无 BOM、LF、无行尾空白及末尾换行，保留正文措辞、署名与段落；原始字节哈希和规范化副本哈希分别记录。不得将重新排版后的哈希冒充上游原文哈希。现有原始审查 zip 及其清单保持不变，不重新打包覆盖历史证据。

NoDrive / SystemCommon 同时保留包声明的 Apache 正文、metadata 署名与来源差异说明；候选源码 MIT 正文作为另标来源的证据材料保留，不替换包声明、不写成已确认的双许可。证据缺口进入 manifest 与索引，不用空 NOTICE 或虚构源码 commit 填补。

## 3. 宿主与材料映射

| 组件 | 版本 | 本批材料 | 分发范围 |
| --- | --- | --- | --- |
| Hangfire.AspNetCore / Core / NetCore | `1.8.14` | 各自 LICENSE.md、COPYING、COPYING.LESSER、NOTICES、LICENSE_STANDARD、LICENSE_ROYALTYFREE 及署名；Core 另含合入的 Cronos 0.8.3 MIT 正文 | 仅 API |
| Hangfire.PostgreSql | `1.20.12` | 自身 LICENSE.md 与署名 | 仅 API |
| Hangfire.Storage.SQLite | `0.4.2` | 自身 LICENSE 与署名 | 仅 API |
| Isopoh.Cryptography.Argon2 / Blake2b / SecureArray | `2.0.0` | 逐包 LICENSE、作者、来源及原包代码未经本批修改的说明 | 四宿主 |
| Microsoft.Data.SqlClient.SNI.runtime | `5.2.0` | LICENSE.txt、Microsoft 署名与来源 | 四宿主；当前 publish 均携带 Windows runtime 文件 |
| SqlSugarCoreNoDrive / SystemCommon | `5.1.4.207 / 8.5.0` | Apache 正文、各包 metadata、候选源码许可证据与未关闭项 | 四宿主 |
| Riok.Mapperly | `4.3.1` | nuspec 精确 commit 的 LICENSE、作者及来源 | 仓库构建材料；不复制到四宿主 runtime 目录 |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | `1.23.0` | EULA.md、ThirdPartyNotices.txt 及来源 | 仓库构建材料；不复制到四宿主 runtime 目录 |

Hangfire 商业条款保留为包附带备选条款，不表示选择该路线或已购买授权。PostgreSql 插件单独保留，不能套用核心的商业条款。

同日[源码补证](../records/hangfire-source-inputs-2026-10-26.md)后，Cronos 正文以 `LICENSE-Cronos-0.8.3.txt` 归入 Core 材料目录，API notices 从 41 个文件增至 42 个；它是合入 Core 的组件材料，不增加独立 NuGet 包条目。当前四宿主共享 6 个 runtime 组件，API 再增加 5 个。保持按组件独立目录，允许正文重复，避免相同文件名互相覆盖。字体继续由既有 native / Flutter assets 接线，不重复生成或复制为 NuGet 包。

## 4. MSBuild 行为与失败策略

只启用 `Radish.Api`、`Radish.Auth`、`Radish.Gateway`、`Radish.DbMigrate` 四个宿主；项目引用不能把同一材料再次传入消费者。默认 Debug / Release 的普通 build 与 publish 均携带本宿主材料，目标路径为 `third-party-notices/nuget/`，其中包含本宿主索引与组件正文。

1. 四宿主解析现有 `project.assets.json`，使用 SDK 自带 `ResolvePackageDependencies` 输出到本 target 的私有 item，不打开全局 legacy items，不引入自定义编译任务或新工具链。
2. 显式核对受控 runtime 包的 ID / version 与宿主映射。已知包消失、版本变化或出现于清单之外的宿主时，要求更新材料及映射后重试；不能沿用旧版许可副本静默通过。未纳入本批的普通包不在此处自动取得许可结论。
3. 每份声明文件必须存在且 SHA256 与清单一致；由 MSBuild `GetFileHash` 校验。缺文件、哈希不符、索引漏项或路径冲突直接失败，不下载补齐、不警告后继续、不设置跳过开关。
4. 复制使用显式 item 和受控相对路径，包含普通 build、`publish --no-build --no-restore` 以及项目引用路径下的重复项检查。清理仅限自有 `third-party-notices/nuget/` 输出子树，避免删除 native 字体或用户材料；增量构建也不得留下已移除的组件副本。
5. 已知来源 / 许可差异保持显式 `unresolved`；它们属于发行未关闭事项，不伪装成材料损坏，也不让日常开发构建永久失败。校验通过只证明本批材料完整交付。

四份 Dockerfile 已完整复制 publish，故本批不修改 Dockerfile、Compose、运行镜像或端口；该静态接线路径仍需未来镜像候选复核，不以本机 publish 代替镜像验收。没有新增运行时 API、页面、服务或认证行为。

## 5. 验证与完成标准

按本批构建 / 分发风险验证，不重复 NoDrive 已通过的数据库全量测试：

- 校验 13 组件元数据、来源正文与规范化副本；与既有 226 条清单及四宿主 assets 对照。
- 定向反向验证：缺文件、篡改正文、错误版本、宿主映射漂移和目标路径冲突均能失败；用临时副本构造，不篡改 NuGet 缓存或正式材料。
- 四宿主 Debug / Release 严格构建及全新 Release publish；核对 API 11 组件、其余各 6 组件，并确认 build-only 两包不会进入 runtime notices。
- 覆盖 `--no-build --no-restore` publish、增量重建、清理后的重建及本宿主索引与文件哈希一致性；确认 SqlSugar DLL 和既有 native 字体声明未漂移。
- 执行 `npm run check:docs`、变更范围卫生检查与 `git diff --check`。保留材料清单、失败路径验证和四宿主产物记录。

命令沿用现有 SDK / Node / NuGet assets 与已有 native 产物；build / publish 均 `--no-restore`，不触发新包安装、产品服务、数据库容器、浏览器 Smoke 或 Docker 镜像构建。缺少现有前置产物时报告，不自行安装或启动。

若 SDK 任务无法满足版本校验，或需要更改 CI、Docker、包版本、运行时 / 原生构建边界，先说明差异再确认。回退仅撤销本批构建 import、targets、脚本与材料，不改依赖、不触碰数据。完成本批后再确定剩余发行证据及独立 Valkey 顺位，不自动 PR、push、tag 或发布。
