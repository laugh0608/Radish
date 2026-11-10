# Windows Razor 第二次 inventory 与身份差异

> 2026-10-31（Asia/Shanghai）。按本轮授权 push 修复提交，并执行一次新的 inventory。**8 组 PowerShell 契约、库存采集、清理与上传成功；image 身份存在未解释差异，未冻结 verify 期望值。** [结构化结果](./hangfire-windows-razor-inventory-second-2026-10-31.json)；接续[本地修复](./hangfire-windows-razor-inventory-repair-2026-10-31.md)与[执行方案](../features/hangfire-windows-razor-validation-plan.md)。

## 执行身份

- 批准、push、run HEAD 与实际 `GITHUB_WORKFLOW_SHA` 均为 `464910ddfa5f1df076128e86fdc618742ebeee0e`；工作流 blob 为 `c4e490ccada977243ae4702ab9e4617312b5dea7`。
- `dev` 从 `26ba4390752b85e06ce10043b9bfe4496c83033a` 快进一个提交；`master` 保持 `4a8589b6a1c5264506c51429cf7fe470e783f9fc`。push 后本地跟踪引用遭遇同一沙盒写锁限制，按已批准方式最小提权修复，未重复 push。
- [Run 41191661741](https://github.com/laugh0608/Radish/actions/runs/41191661741)，attempt `1`，创建于 `2026-10-31 18:10:27 +08:00`，最终 `success`。Windows job `124509519975` 成功，其余五个 job 全部 skipped。
- 输入为 `validation_scope=hangfire-razor`、`razor_mode=inventory`、`expected_head_sha=464910ddfa5f1df076128e86fdc618742ebeee0e`；四项 verify 期望值未传。输入证据为 dispatch 命令与 job 日志，没有独立事件 payload。

## 实测结果与未关闭差异

| 项目 | 实际证据 |
| --- | --- |
| runner / OS | `windows-2022` / Windows Server 2022，`10.0.20348`；runner、OS、进程均 X64 |
| PowerShell | `7.6.9` |
| VS 2022 | 唯一 Enterprise 实例，`17.14.37824.6` |
| Full amd64 MSBuild | `/version` 与 FileVersion 均为 `17.14.60.43110` |
| MSBuild SHA256 | `7da5f86d7dfa627c4071edc9563fda7ace7ec0aaadd8b2aaccb92d2a823535c0` |
| Framework | registry Release `533509`；四个 CLR4 兼容程序集身份 / hash 已保存 |
| 库存 | schemaVersion 2，`complete=true`、`stage=complete`、`upstreamTasksExecuted=false` |

8 组契约实际通过，fixture 已删除。四条 owned command 依次为 `contract-success`、`contract-exit-23`、`vswhere`、`msbuild-version`；退出码分别为 `0 / 23 / 0 / 0`。exit 23 是契约要求的预期失败，原样保留；命令名与日志名未被环境变量循环覆盖。虚构 VS fixture 仍标为 `fixtureOnly=true`，实际 VS 原始响应独立保留。

**image 身份尚不能冻结**：job 初始化日志的 Runner Image 为 `20261025.346.1`，对应 Included Software / Image Release 链接也指向 `20261025.346`；完整库存与早期进度报告中的环境变量 `ImageVersion` 则为 `20261026.347.1`。两份原始证据均保留，尚无证据解释原因；没有把其中任意一个改写成另一值。工作流及库存代码未硬编码这两个版本值，库存字段直接采集 `$env:ImageVersion`。本轮不重跑、不放宽匹配、不进入 verify；后续先只读核对差异来源，再提出精确执行方案。

## 归档与清理

- [Artifact](https://github.com/laugh0608/Radish/actions/runs/41191661741/artifacts/13038559508)：`hangfire-razor-41191661741-1`，ID `13038559508`，8603 bytes，7 天保留，到期 `2026-11-07 18:10:49 +08:00`。
- ZIP SHA256 `5c8e8a3ffea3bf3a3aa4b91d75a58d48b27964681ef9b495dfb78a1b6de39359` 与服务端 digest 一致；manifest SHA256 `dbe83da4f3f746bde6e1797860285460faa93dce95f7d91218eddb0ea6124f74`。10 个 payload 文件 size / hash 全部通过，加 manifest 共 11 个文件，无额外文件。
- 本地保留 `artifacts/hangfire-windows-inventory/41191661741-1`：ZIP、原 artifact、job 日志、运行 / 任务 / artifact 元数据、工作流快照、远端引用与汇总。archive manifest 覆盖 19 个文件，自身 SHA256 `cacf9b183ba762d20081c20d61f577dfc63fea2f62d8c66ffbf2fc0fc682c610`。
- `gh api` 刷新元数据遭遇 TLS 超时，随后通过 GitHub connector 取得成功终态并归档；`jobs.json` 为 connector 规范化的 job / step 响应，不冒充原始 REST 全字段响应。没有因网络错误重试 dispatch。
- `cleanup.json` 为 `complete=true`、`workingTreeRemoved=true`、`errors=[]`、`globalToolsChanged=false`，端口 / 数据库为空；清理、上传、post-job 均成功。VM 销毁没有独立观察，剩余证据和 checkout 依赖托管 runner 生命周期清理。本机只新增归档，没有新建服务或工具链。

没有下载 SDK、11 项包或上游源码；没有运行 Razor task、23 对再生成、Core 构建、16 组探针或 verify。`result.json` 明确 `windowsValidationComplete=false`。SQLite 其他平台、发布、部署和 master 合并未执行。
