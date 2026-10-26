# LGPL 狭义例外与本机验证

> 日期：2026-10-26（Asia/Shanghai）。项目所有者已明确批准[裁决方案](../features/dependency-distribution-decisions.md)第 2 节文本与第 3 节首批实施范围。本记录只关闭许可补充及本机构建验证，不代表完整发行放行。

## 1. 实施范围

- 根 [LICENSE](../../LICENSE)第 5 节逐字加入已批准的英文例外，保留 Source-Available 定位和其余条文。该例外针对 LGPL 库权利、接口兼容替换，以及为调试库修改所需的逆向和相关复制 / 使用；具体效力以根文本为准。
- 使用已有 Rust `1.96.0` 与本地 Cargo 缓存，离线重建 `osx-arm64` Debug / Release native 材料并执行既有 `--verify`。
- 完成全解决方案 Debug / Release 严格构建、四宿主 Release publish，逐文件核对许可 / notices 和 native 输出。
- 未改变业务代码、依赖版本、构建脚本、MSBuild / Docker / CI 契约或根 Agent 入口。

本批基于 `890ea5d798210dc404071d14a9c6a596e9f02115` 工作区。输入哈希、native manifest、构建结果、产物差异和日志哈希见[机器证据](./lgpl-exception-evidence-2026-10-26.json)。本地完整日志与发布产物保存在 `/private/tmp/radish-lgpl-exception-says1l57`，临时路径不是正式分发入口。

## 2. 验证命令与结果

```bash
CARGO_NET_OFFLINE=true node Scripts/build-native-images.mjs --debug
CARGO_NET_OFFLINE=true node Scripts/build-native-images.mjs
CARGO_NET_OFFLINE=true node Scripts/build-native-images.mjs --debug --verify
CARGO_NET_OFFLINE=true node Scripts/build-native-images.mjs --verify
dotnet build Radish.slnx -c Debug --no-restore --warnaserror
dotnet build Radish.slnx -c Release --no-restore --warnaserror
npm run check:nuget-notices
```

四宿主分别执行以下命令，`<host>` 为 `Radish.Api`、`Radish.Auth`、`Radish.Gateway`、`Radish.DbMigrate`：

```bash
dotnet publish <host>/<host>.csproj -c Release --no-build --no-restore --warnaserror -o <本批临时目录>/publish/<host>
```

所有命令通过。双配置全解决方案构建均为 **0 警告 / 0 错误**；四个 publish 均退出 0；notices 检查覆盖既有 13 个重点组件与四宿主映射。没有下载工具链 / 包、执行 restore、启动产品服务 / 数据库容器或运行完整数据库回归。本次没有业务行为变化，未新增镜像或浏览器 Smoke。

| 核对对象 | 结果 |
| --- | --- |
| 批准文本 | 根 LICENSE 新增段落与批准方案逐字一致 |
| 本机 native 两配置 | 每份 164 个文件，仅 `licenses/LICENSE-Radish.txt` 与 `manifest.json` 改变；其余 162 个文件 SHA256 不变 |
| native 二进制 / 字体 | 两配置二进制、字体、第三方许可及 `dependencies.json` 均与重建前一致 |
| 许可与 native 复制 | API / Tests 两配置输出，以及 API Release publish 的完整 native 文件树与对应重建目录一致 |
| notices 复制 | 四宿主 Debug / Release 输出与 Release publish 的所有文件路径、数量、SHA256 均与仓库材料一致 |
| 暂存清理 | 没有遗留 `.building-*` 目录；构建缓存及核验产物保留 |

| Release 宿主 | notices 组件 / 文件 | 发布文件总数 |
| --- | --- | --- |
| Radish.Api | 11 / 41 | 419 |
| Radish.Auth | 6 / 16 | 586 |
| Radish.Gateway | 6 / 16 | 107 |
| Radish.DbMigrate | 6 / 16 | 212 |

与前一批 notices publish 相比，文件集合相同；除 Radish 自有程序集 / PDB 和 API 的两份 native 材料外，其余文件哈希全部不变，包括第三方 DLL、配置、deps、字体与 notices。已逐一确认变化程序集（含 `en / zh` 卫星资源）的 informational version 从 `26.8.2+5fe407ce…` 更新为 `26.8.2+890ea5d7…`。首次产物比较因核验脚本漏列自有卫星程序集而停止，检查版本后修正比较范围并全部通过，没有修改产品文件或放宽第三方文件比较。

仓库质量检查通过：7 个改动文件无文本卫生错误；全库 899 个文件、847 个 Markdown / 1,064 个本地链接检查通过；`git diff --check` 通过。仅保留既有 records 索引篇幅建议提醒（322 行，建议 300 行）。

## 3. 未关闭事项

- Hangfire 完整对应源码、构建输入和适当修改库的实际可替换性仍需补证；独立 DLL 与许可例外不代替这些结果。
- 本轮仅重建本机 `osx-arm64`。其余四个平台必须在各自正式候选中按新 LICENSE 重建，历史平台结果不证明当前 fingerprint 有效。
- NoDrive / SystemCommon 精确来源及许可差异、SNI 下游条款与分发平台、全生态材料覆盖仍未关闭；未发送上游询证。
- 本批没有 push、PR、tag、Release、镜像发布或部署。后续按[当前进行中](../planning/current.md)推进。
