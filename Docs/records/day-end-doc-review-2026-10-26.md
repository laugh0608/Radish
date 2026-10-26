# 2026-10-26 日终代码与文档回顾

> 按 Asia/Shanghai 的提交时间核对当日全部 7 个提交，截止 `ff7a9bef`；本次日终文档提交另计。分支为 `dev`，当日这 7 个提交尚未推送。日终只审阅代码与已有证据、更新文档，不新增依赖、启动服务或展开下一批实现。

## 1. 当日全部提交

| 提交 | 内容 | 审阅结论与材料 |
| --- | --- | --- |
| `571054e8` | NoDrive 正式采用方案及分发预审 | 先冻结部分驱动收缩、配置边界和验证资源；[方案](../features/nodrive-adoption-plan.md)与[采用前核对](./dependency-distribution-preflight-2026-10-26.md)保留原批准范围 |
| `5fe407ce` | 正式采用 NoDrive、限制 provider | Common / Infrastructure 固定 NoDrive，Common 显式提供 PG / SQLite 驱动及 SystemCommon；业务、租户、OpenIddict / Hangfire 校验只允许两种 provider。SpaProxy 经另批固定为既有版本，新增 58 项回归；见[实施记录](./nodrive-adoption-2026-10-26.md) |
| `1ec5d131` | 重点 NuGet notices 分发 | 13 个组件的材料和来源清单、校验脚本 / 自测、四宿主 MSBuild 复制接线；build-only 材料不进入 runtime 输出。NoDrive 来源差异显式保留；见[接线记录](./nuget-notices-distribution-2026-10-26.md) |
| `890ea5d7` | 剩余发行边界与裁决方案 | 冻结 Hangfire 例外 / 源码与替换条件、NoDrive 询证草案及 SNI 平台边界；询证未发送，见[裁决专题](../features/dependency-distribution-decisions.md) |
| `7a449453` | 批准的 LGPL 狭义例外 | 根 LICENSE 加入批准文本，本机 native 离线重建并完成双配置严格构建 / 四宿主核对；其他平台未据此放行，见[验证记录](./lgpl-exception-validation-2026-10-26.md) |
| `98ba293d` | Cronos 正文与 Hangfire 源码补证 | Core 合入的 Cronos MIT 正文随 API 分发；233 个 PDB 源码 checksum 匹配，27 项构建输入整理；发现 SQLite 测试 / publish 原生版本差异，见[补证记录](./hangfire-source-inputs-2026-10-26.md) |
| `ff7a9bef` | Core 源码替换验证 | 临时上游源码基线 / 诊断库严格构建与三组 SQLite 探针通过；实际分析器调整另获批准，正式 Radish DLL / 依赖未被替换，见[试验记录](./hangfire-core-replacement-2026-10-26.md) |

NoDrive 是正式依赖变更；Hangfire 源码重建属于临时隔离试验。二者的代码与授权范围不同，不将试验中的 SDK 分析器调整或重建 DLL 写成产品默认构建。

## 2. 根据代码进行的文档校准

- **数据库实现与支持范围**：对照 Common / Infrastructure 包引用、`DatabaseProviderPolicy`、`BaseDbConfig`、`RuntimeDatabaseConfigResolver`、`TenantUtil` 及两份新增测试。配置指南、数据库总览 / 连接管理、框架说明已覆盖 NoDrive 与拒绝非正式 provider；《开发规范》仍写旧明文文件读取和模糊的主从能力，本次修正为真实 SQLite 根路径解析、PostgreSQL 配置方式、整组 provider 校验与 `Slaves` 未接线边界。
- **SQLite 原生身份**：对照 `Directory.Build.props`、Extension bundle 引用与已有 publish / 探针证据，在数据库连接指南补充两条原生资产链和实际文件核对方法。包引用 `3.50.3` 不代表 publish native 已固定为同版本；保持现状可见，不在文档收尾中修改依赖。
- **分发接线与 Cronos**：对照 `Directory.Build.targets`、notices targets / props、校验脚本、manifest 与根 LICENSE。接线方案补上 Core 内 Cronos 正文及 API notices 42 文件的当前口径；不把 Cronos 误计为新增独立 NuGet 包。原接线记录保留 41 文件的历史计数，增加后续补充入口。
- **评估与实施状态**：图片替代后依赖评估中的“二者均未正式替换”已经过期，改为 NoDrive 已采用、Valkey 仍待裁决。NoDrive 采用记录补上同日后续 notices 接线；许可例外记录补上后续源码与 Core 试验入口，保留原时点证据及其他平台未关闭事实。
- **规划与索引**：把下一步建议明确写入[接下来的事项](../planning/current.md#接下来的事项)：先提交 SQLite 原生依赖统一方案，说明精确包、宿主 / 平台、回退和验证矩阵，确认后再实施。更新当前最近结论、Docs README 与 records 索引，历史批次不堆入入口。

没有变更产品方向、API、数据库结构、运行配置、依赖或长期协作规则；`AGENTS.md` / `CLAUDE.md` 不需修改。原始 JSON / CSV 证据、历史包版本与哈希保持原时点含义，未用当前结果覆盖过去失败。

## 3. 已有验证证据与适用边界

下表汇总当日批次已执行的结果，日终没有重跑业务测试、源码构建或资源启动：

| 批次 | 已有结果 | 保留边界 |
| --- | --- | --- |
| NoDrive | Debug / Release 严格构建通过；PG17 全量各 4,135 通过、102 预期跳过；PG16 专项各 43 通过；四宿主产物与真实驱动核对通过 | 102 项 Redis / Valkey 测试未执行；SqlClient / SNI 仍存在；未验证生产或新镜像 |
| notices | 17 项自测含真实 SDK target 反向用例通过；四宿主双配置构建、publish、增量 / clean 边界及材料哈希核对通过 | 只覆盖重点组件，未完成全生态材料或镜像验收 |
| LGPL 例外 | 本机 `osx-arm64` native 两配置离线重建及严格构建 / 四宿主 publish 核对通过 | 其余四平台须按新 LICENSE 重新构建；不沿用历史 fingerprint 宣称有效 |
| 源码 / Cronos | 233 个带 checksum 的源码文档全部匹配，Cronos 来源和正文确认；四宿主 publish 通过 | 22 个 Razor 映射无 checksum；审阅包不是正式完整对应源码交付 |
| Core 替换 | 214 个公开类型 / 2,738 条表面记录一致；三组各完成 16 项注册及一次探针成功，修改组有 16 / 1 次标记 | 仅本机、Core netstandard2.0、当前 publish SQLite 3.50.4；Cronos 未源码重建，其他 TFM / provider / 平台未覆盖 |

当日已按各批归属清理 PG17 / PG16 容器与卷、临时数据库和 worker；保留的 `/private/tmp` 材料为本机审阅证据，不是永久远程归档或公开源码下载点。本次文档收尾没有恢复任何测试资源，也未发送上游询证。

## 4. 日终检查与下次接续

本次 `npm run check:repo-quality:changed`、`npm run check:nuget-notices` 与 `git diff --check` 均通过：11 个改动文件、906 个文档 / 治理文件、851 个 Markdown 与 1,105 个本地相对链接无错误；notices 的 13 组件、23 个派生文件与四宿主 assets 一致，未重新验证外部原始材料。开发规范 845 行、records 索引 326 行保留建议篇幅提醒，均未超过硬上限，本次不扩展为无关文档拆分。仅调整文档，没有为状态校准重复全量构建或业务回归。

下次从[当前进行中](../planning/current.md#接下来的事项)第 1 项开始：先拟定 SQLite 原生依赖统一方案；完整对应源码、NoDrive 询证、SNI 平台分发和 Valkey 各自按范围推进。成组范围确定后，对最新候选做合并前门禁再安排 `dev → master` PR，之后恢复日志 L2 / P8-C 的既定顺位。

今天在日终文档提交后停止，不自动推送、创建 PR、打 tag、发布镜像或部署；后续依赖安装、服务启动与运行时变更仍需对应任务的明确授权。
