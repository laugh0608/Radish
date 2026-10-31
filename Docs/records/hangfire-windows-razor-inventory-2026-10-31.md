# Windows Razor 首次 inventory 与失败清理

> 2026-10-31（Asia/Shanghai）。已按本轮授权 push 指定提交并执行一次 inventory；**VS 2022 查询无匹配，库存未完成，已停止且未重跑**。失败证据上传、下载校验与任务清理完成。[结构化结果](./hangfire-windows-razor-inventory-2026-10-31.json)；接续[执行方案](../features/hangfire-windows-razor-validation-plan.md)。

## 提交与执行身份

- 批准 / 实际 run HEAD：`26ba4390752b85e06ce10043b9bfe4496c83033a`。
- 远端 `dev` 从 `29e7030122bc2462262ebd714964106a00242c65` 快进 10 个提交；默认分支 `master` 保持 `4a8589b6a1c5264506c51429cf7fe470e783f9fc`，没有开放的 `dev` PR。
- 首次 push 成功后，本地 `origin/dev` 写锁被沙盒拒绝。按停止线暂停；获单独授权后，以带旧值校验的 `git update-ref` 最小提权修复跟踪引用，没有重复 push。
- 远端工作流 ID `390778640`、状态 active；`dev` 文件 blob 为 `134ff1e8fed12548537e54ae91de164bc055c6b0`，与批准提交一致。
- [Run 41190198055](https://github.com/laugh0608/Radish/actions/runs/41190198055)，attempt `1`，事件 `workflow_dispatch`，创建时间 `2026-10-31 17:48:44 +08:00`。
- 输入：`validation_scope=hangfire-razor`、`razor_mode=inventory`、`expected_head_sha=26ba4390752b85e06ce10043b9bfe4496c83033a`；四项 verify 工具期望值为空。
- 仅 Windows Razor job 执行，其余五个 job 全部 skipped。批准 SHA 检查及固定 SHA checkout 成功；末次查询仅有这一次运行。

工作流来源按运行 head SHA 与文件 blob 核对；当前脚本未独立输出 `GITHUB_WORKFLOW_SHA`，不把推导的来源记成实际采集字段。输入证据为执行命令及 runner 日志，未保存独立事件 payload。

## 失败事实与边界

runner 初始化日志记录实际 image 为 `windows-2025-vs2026`、版本 `20261030.294.1`，OS 为 Windows Server 2025 / `10.0.26100` / Datacenter，已不同于准备清单中的 VS 2022 image。

库存只执行了已有 vswhere：`-products * -version [17.0,18.0) -requires Microsoft.Component.MSBuild -format json -utf8`。进程退出码为 0，原始输出为 `[]`，随后触发 `Require exactly one eligible VS 2022 instance`。这证明限定查询没有匹配，不能据此推定机器没有其他 VS 版本。

`inventory.json` 未生成，Full MSBuild `/version` 未执行；精确 VS / MSBuild 版本、MSBuild hash、Framework 兼容程序集及 PowerShell 版本未完成采集。未下载 SDK、11 项包或上游源码，未执行 Razor task、生成、构建、渲染或 verify。Windows Razor 验证仍未完成。

另发现命令元数据缺陷：`commands.json[0].name` 实际为 `MSBUILDDISABLENODEREUSE`。`Common.ps1` 的 `Invoke-OwnedCommand` 使用 `$name` 遍历环境变量，PowerShell 大小写不敏感，覆盖了参数 `$Name`。原日志路径仍为 `commands/0001-vswhere.log`，executable / arguments / exitCode 明确指向 vswhere；保留原证据，不改写该字段。本轮未修复代码。

## 证据、清理与下一步

- [失败 artifact](https://github.com/laugh0608/Radish/actions/runs/41190198055/artifacts/13038741482)：`hangfire-razor-41190198055-1`，ID `13038741482`，1888 bytes；保留 7 天，到期 `2026-11-07 17:49:06 +08:00`。
- 下载 ZIP SHA256：`c08aadc66d54409849b0e94b95ac22f170ea5324b1caea8ed16366bfdf4fdd5e`，与服务端 digest 及上传日志一致。
- artifact manifest SHA256：`205e5024c52364b0bc69d69832f704efb723b3e42cbc8668ca38e4915a3810c9`。5 个 payload 文件逐项核对 size / SHA256；加 manifest 共 6 个文件，无额外文件。
- 本地归档：`artifacts/hangfire-windows-inventory/41190198055-1`，保留 ZIP、原 artifact、解码 job 日志、run / jobs / artifacts 元数据、工作流快照、远端引用和汇总。
- 本地 archive manifest 覆盖 14 个文件，自身 SHA256 为 `fda171c520f80ffcb946edd1f04860ebb96d8d08c8ec80b3cf1344f925192baf`。
- `cleanup.json` 确认 `complete=true`、`workingTreeRemoved=true`、`errors=[]`、`globalToolsChanged=false`，端口为空、数据库为空。清理与上传步骤成功；`result.json` 明确为 failed / `windowsValidationComplete=false`。
- runner post-job 已完成。剩余证据与 checkout 依赖 GitHub 托管 VM 销毁清理，未独立观察 VM 销毁事件；本地没有新建服务、工具链或临时执行目录，只保留归档。

后续需先确认 runner / VS 选择策略及命令元数据修复范围，再针对新提交单独授权 inventory；不自动放宽到 VS 2026、不安装 / repair VS、不修改 verify 门禁。本轮没有重跑、其他平台矩阵、master 合并、tag、发布或部署。
