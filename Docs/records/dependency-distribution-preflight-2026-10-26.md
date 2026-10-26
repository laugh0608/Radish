# 依赖分发与 NoDrive 正式采用前核对（2026-10-26）

> 基于 `dev` 的 `d64bd005`，本批只读核对实现并准备方案。未安装包、修改产品代码、启动容器、访问生产或重新生成 publish。正式采用入口见[实施方案](../features/nodrive-adoption-plan.md)，材料哈希见[证据 JSON](./dependency-distribution-preflight-2026-10-26.json)。

## 1. 状态与输入

- 开始时工作区干净，本地 `dev` 与已存储的 `origin/dev` 引用一致；未通过 fetch 核对实时远程。
- 19 份现有 `obj/project.assets.json` 合计 237 个 package ID + version，仍包含 `SqlSugarCore`，不包含 NoDrive / SystemCommon；Mapperly 已存在。
- 此数字为本机已有解析文件快照，不是 clean restore 后的新候选清单，也不与历史全生态 947 条直接比较。原 CSV 保持不变。
- SDK 为 `10.0.300`；Docker context 为本机 OrbStack Unix socket，服务端为 `linux/aarch64`，当时无运行容器，15432 无监听，可用磁盘约 380 GiB。
- 本机 `osx-arm64` 的 Debug / Release native 输入与产物校验均通过。没有重新构建 native 或安装工具链。

## 2. 配置入口的实际缺口

| 源码 | 当前事实 | 正式采用方案 |
| --- | --- | --- |
| `BaseDbConfig.MutiInitConn` | 筛选启用连接后逐项处理连接串，没有 PG / SQLite 支持白名单 | 先完整校验启用连接，再做路径处理；不通过删包制造缺驱动异常 |
| `BaseDbConfig.SpecialDbString` | 仍有 SQL Server / MySQL / Oracle 本地文件读取分支 | 支持边界批准后删除不可达分支 |
| `TenantUtil.GetConnectionConfig` | 只检查租户 DbType 非 null，直接创建 SqlSugar 连接配置 | 同时校验分库租户 provider，避免绕过应用配置入口 |
| `RuntimeDatabaseConfigResolver` | 显式类型接受枚举解析，未知数字也可能经 Enum.TryParse 进入结果 | 在 OpenIddict / Hangfire 连接串处理前校验支持类型，保留合法 legacy 行为 |
| 文档枚举表 | 列出七种历史值，未在表旁区分正式支持范围 | 保留数值兼容并明确仅 PG / SQLite 为正式支持 |

未读取任何真实租户数据，不推定实际部署存在或不存在非正式 provider。

## 3. 分发材料定位

已从当前解析目录读取 12 个重点包的 nuspec 与许可文件名 / SHA256，并记录原始作者、copyright、来源和作用项目。下表描述材料是否存在与接线事实，不作各分发主体的许可放行结论。

| 对象 | 本地材料事实 | 剩余动作 |
| --- | --- | --- |
| Hangfire AspNetCore / Core / NetCore `1.8.14` | 三包各有 `LICENSE.md`、`COPYING`、`COPYING.LESSER`、`NOTICES` 和两份商业许可文本 | 按实际采用路径整理原文与对应来源；商业文本存在不代表购买或授权 |
| Hangfire.PostgreSql `1.20.12` | 独立 `LICENSE.md`；作者与核心包不同 | 不能由核心包材料代替插件，单独保留 |
| Hangfire.Storage.SQLite `0.4.2` | 存在独立 `LICENSE` | 纳入实际 SQLite 调度器闭包，不能遗漏本地 provider |
| Argon2 / Blake2b / SecureArray `2.0.0` | 三包有相同哈希的 `LICENSE`，metadata 记录 `Michael Heyman 2022` | 保留逐包归属与来源，核对最终产物声明；不更换密码算法 |
| SNI runtime `5.2.0` | 包内 `LICENSE.txt` | 与 SqlClient 托管包分开整理，核对不同目标 runtime 的分发 |
| VS Container Tools `1.23.0` | `EULA.md` 与 `ThirdPartyNotices.txt` | 按构建用途记录；不能据 assets 命中推定工具本体进入最终应用 |
| Mapperly `4.3.1` | metadata 是 `Apache-2.0` expression，本地包内未发现独立许可正文 | 从精确版本来源补材料；生成器在 Service 中为 `PrivateAssets=all`，构建与运行分发分别核对 |
| SqlSugarCore `5.1.4.207` | 当前包无独立许可正文；NoDrive / SystemCommon 尚不在当前解析图 | 正式安装后核对新两包及上游版本材料，不能预写“已完成替换” |

`Directory.Build.targets` 当前复制的是 native 产物树，四宿主 Dockerfile 复制各自 publish；没有在这条公共接线上看到 NuGet 重点包许可正文的通用收集步骤。应在新候选实际产物中确认缺口后再设计 notices 接线，不能把包缓存当作发行材料。

## 4. 字体不再从零处理

- `Scripts/build-native-images.mjs` 已复制 DejaVu 与 Noto Sans 许可，生成依赖清单；`Directory.Build.targets` 将整个 native 树复制到 API / 测试输出与 publish。
- 本机 Debug / Release 的 `LICENSE-DejaVu.txt`、`OFL-NotoSansCJK.txt` 均存在，四份副本 SHA256 与源文件一致。
- Flutter `pubspec.yaml` 已声明 `assets/fonts/noto/licenses/`；目录中有 Sans / Serif 两份 OFL。Noto Serif 不在 Rust 图片字体输入中，不要求它出现在 native 输出。
- 本次没有构建 Flutter 安装包或 Linux 镜像，以上接线与本机哈希不代替各平台最终产物验收。

## 5. 后续执行边界

项目所有者已选择继续部分收缩方向；具体包命令、配置消费点行为与 PostgreSQL 测试资源已整理到[正式方案第 6 节](../features/nodrive-adoption-plan.md#6-已确认命令与运行资源)。本批材料准备不复用 10 月 21 日的安装 / 启动授权。此后另获批准的执行结果见[正式采用记录](./nodrive-adoption-2026-10-26.md)，不覆盖本文准备阶段快照。

此处不执行 Valkey 迁移、不探测源实例或生产 key，不创建 PR / tag、不发布。进入正式实现后，应记录当前候选的新测试与依赖图，不覆盖本次只读快照或既往实验结论。

## 6. 准备阶段验证

- `npm run check:docs` 通过：文本卫生、根入口同步和 Markdown 本地链接均无错误。
- `git diff --check` 通过；本批仅新增 / 更新 Docs 文档与 JSON 证据。
- 证据中的源码 / 项目声明 / assets 哈希、字体原文与副本哈希、12 个重点包的唯一性及 19 份 assets 数量已复核。
- 未执行候选 restore、后端构建 / 测试或新 publish；这些结果必须在具体范围批准后另行记录。
