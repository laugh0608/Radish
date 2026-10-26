# 依赖发行边界裁决与后续工作包

> 2026-10-26：项目所有者已批准第 2 节狭义 LGPL 例外及第 3 节首批许可 / 本机 native 验证范围，根 LICENSE 已写入批准文本。实施结果见[许可补充与本机验证](../records/lgpl-exception-validation-2026-10-26.md)；后续源码构建、替换运行试验、NoDrive 询证发送与 SNI 平台方案仍按各自范围确认。

## 1. 当前事项与后续边界

| 对象 | 已确认的技术事实 | 尚需处理的边界 | 下一步建议 |
| --- | --- | --- | --- |
| Hangfire 核心与 PostgreSql 插件 | 四个 DLL 与原 NuGet asset 相同且独立发布；两个精确源码快照已校验；狭义例外与本机验证已完成 | 对应源码完整性、实际可替换性与正式分发材料 | 保留当前调度能力，源码 / PDB 与锁定输入已补证，按 [Core 试验方案](./hangfire-source-replacement-plan.md)确认独立运行验证范围 |
| NoDrive / SystemCommon | 包声明 Apache、候选源码 MIT；无包内 PDB 或嵌入式 portable PDB 可进一步绑定 commit | 上游版本 / 打包来源与许可声明差异 | 保持现有依赖，准备第 4 节询证内容；发送需项目所有者单独授权 |
| Microsoft SNI | Windows 原生 DLL 随当前四宿主通用 publish 交付；包内是 Microsoft 专用条款 | 下游条款、分发条件及各平台实际交付范围 | 先选择正式分发平台；Linux 可独立评估明确 RID 的发布方式，Windows 分发单独核对条款 |

这三项分别处理，不因 notices 已交付就视为发行放行，也不因某项需要外部证据而反复重跑已通过的数据库测试。现阶段继续开发不等于已完成对外发行审核。

## 2. Hangfire 路线：已批准的狭义例外

已确认继续以当前开源许可路线评估 Hangfire，保留 Radish 自有代码的 Source-Available 定位。LGPL 第 4 节允许 Combined Work 采用自选条款，同时要求这些条款不妨碍相关库修改及为调试这些修改所作的逆向工程；适用边界以[包内正文](../../ThirdParty/nuget/hangfire.core/1.8.14/COPYING.LESSER)和[发布方文本](https://www.hangfire.io/licensing/lgpl.html)为依据。

根 LICENSE 第 3 节保留一般性修改与逆向限制；第 5 节原先只有第三方自身许可保留。项目所有者已批准并写入以下应用侧狭义例外；实际授权以[根 LICENSE](../../LICENSE)为准，本页记录批准范围，不代表完整发行条件已成立。

```text
For a distribution of Radish that includes a library governed by the GNU
Lesser General Public License, Sections 2, 3 and 9 of this license do not
limit the rights granted by that library's applicable license in the library.

A recipient otherwise lawfully entitled to use that distribution may:
(a) replace the library with a modified, interface-compatible version;
(b) reverse engineer Radish solely to the extent necessary to debug such
    modifications to the library; and
(c) make and use copies of Radish solely as necessary to carry out that
    replacement and execute the resulting combined work.

This exception does not grant a general right to modify or redistribute
Radish's original code. It does not restrict any additional rights that
the applicable library license requires to be permitted for the combined work.
```

实际发行合同与方式仍需单独核对；正式发行前有争议的解释应请熟悉适用地区的专业人士复核。不能只购买 Hangfire 核心商业许可就推定覆盖独立的 `Hangfire.PostgreSql` 插件。

当前已接受该狭义例外；若以后决定退出这类许可链，再转为独立调度替代评估；不直接删 Hangfire，也不把 Quartz 或自有 Outbox 当作免迁移的直接替换。替代方案须覆盖持久化作业、重试、并发、多实例、时区、管理权限与历史作业迁移 / 回滚。

## 3. 已批准的首批实施与后续边界

本次已批准的首个实施批次限于第 2 节文本、既有本机 native 材料重建与相应构建验证；完整源码构建、修改库运行试验、包安装及发行流水另按精确输入确认。

1. **应用条款与 native 输入**：根 LICENSE 仅增加第 2 节批准文本。`Scripts/build-native-images.mjs` 将该文件纳入 fingerprint，并复制为 `native/licenses/LICENSE-Radish.txt`；因此修改后需用已有 Rust `1.96.0` 重建本机 `osx-arm64` Debug / Release 并重新验证，不能只改 Markdown 后沿用旧原生证据。执行 `CARGO_NET_OFFLINE=true node Scripts/build-native-images.mjs --debug` 及无 `--debug` 的 Release 命令，随后执行对应 `--verify`。覆盖范围仅这两个既有 native 目录与 Cargo 构建缓存；脚本自清理 `.building-*` 暂存目录。缺工具链 / 缓存即停止，不下载；其他四个平台留待其正式候选重建。本机通过后执行 .NET Debug / Release 严格构建与四宿主 publish（全部 `--no-restore`），核对根许可副本及现有 notices，不启动任何产品服务。
2. **对应源码交付**：Hangfire / PostgreSql 两份快照及新增 Cronos 精确快照、实际 Core 源码包文件、27 项锁定输入已完成[本地补证](../records/hangfire-source-inputs-2026-10-26.md)；完整发行材料仍需核对。明确 release 下载点旁的源码入口、精确版本、哈希、许可与可获得性；本地临时 tar.gz 不作为对外源码交付已完成的证据。
3. **源码构建输入**：当前 Core 上游使用 `Hangfire.Build 0.4.4`、LibLog / MoreLinq / StackTrace 源码包与 ILRepack；PostgreSql 插件使用 `GitVersion.MsBuild 5.11.1`，不能仅凭 csproj 中旧 VersionPrefix 判断包版本错误。冻结需要获取的版本和构建环境后再执行构建，不先运行上游脚本或自动安装 SDK。
4. **可替换性验证**：下一批 [Core 试验方案](./hangfire-source-replacement-plan.md)已明确 13 个 restore 输入、合并工具与临时 SQLite 资源，尚待批准。先验证库加载、程序集身份与接口兼容，再在隔离测试中验证适当修改的库可用于实际调度调用。仅存在独立 DLL 不等于 LGPL 4d1 的运行条件已证明；不使用关闭真实错误或删除签名检查的方式伪造通过。需要数据库时按精确测试资源另获启动授权。
5. **条件性执行期声明**：核对实际向接收者展示版权的执行期界面是否需要补齐相关库版权与许可入口；未证明必要性前不扩建通用版权页面或改写业务 UI。

完成本工作包也不自动关闭 SNI、NoDrive 或其他生态发行事项。根 LICENSE、发行流程、构建契约变更同步其 Docs 真相源；实际产物与检查证据保存在 records。

## 4. NoDrive 上游询证草案（未发送）

下列文本可供项目所有者审阅；发布 issue、讨论或发送邮件需明确授权，当前只保存在仓库：

```text
We are reviewing the provenance and redistribution notices for
SqlSugarCoreNoDrive 5.1.4.207 and SystemCommon 8.5.0 from NuGet.

Could you confirm the exact source commits and packaging inputs for these
two published packages, and the applicable license for each package?

Both package nuspec files point to Apache-2.0. The candidate SqlSugar
repository snapshots have an MIT root LICENSE, and the observed
SystemCommon pack nuspec still declares version 8.3. We therefore have
not assumed that these snapshots establish the exact published sources.

Please also identify any NOTICE, copyright or other attribution material
that should accompany redistribution of these exact package versions.
```

候选 commit、NuGet archive / DLL 哈希见[来源补证 JSON](../records/nodrive-source-provenance-2026-10-26.json)。询证只需这些公开包信息，不需要发送 Radish 私有配置、业务代码、数据库或凭据。

## 5. SNI 与后续顺位

SNI `5.2.0` 包内第 3 节除了允许有条件分发 object code，还约定应用内分发、下游条款保护及其他责任；复制 LICENSE 不能代替实际合同处理。其余条款以[精确包正文](../../ThirdParty/nuget/microsoft.data.sqlclient.sni.runtime/5.2.0/LICENSE.txt)为准。

Linux 镜像可评估按 `linux-x64 / linux-arm64` 明确 RID、framework-dependent 的正常 SDK 发布，核对 Windows-only SNI 是否自然退出最终产物；该事项尚未试验，不承诺一定移除所有 SqlClient 相关组件。它会改变 restore / publish 输入与镜像候选范围，须独立确认。禁止用手工删除 DLL、`ExcludeAssets` 或隐藏清单代替正确发布。

Windows 后端或通用可移植包若仍交付 SNI，需要继续核对其实际发行条款；不能用 Linux 结果覆盖。Valkey、依赖批次集成、日志 L2 与 P8-C 的相对顺序继续由[当前进行中](../planning/current.md)维护，不因本页草案自动切换服务、开 PR 或发布。
