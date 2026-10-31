# Windows Razor image 身份诊断增强

> 2026-10-31（Asia/Shanghai）。接续[第二次 inventory](./hangfire-windows-razor-inventory-second-2026-10-31.md)。项目所有者先批准只读核对，随后批准本地诊断增强。**本地实现与静态检查完成，新增 PowerShell 行为契约尚未执行；新候选尚未 push / dispatch，verify 未执行。** 当前契约以[专题方案](../features/hangfire-windows-razor-validation-plan.md)为准。

## 只读核对结论

第二次运行 `41191661741` / attempt 1 / commit `464910ddfa5f1df076128e86fdc618742ebeee0e` 的初始化日志为 `20261025.346.1`，库存环境值为 `20261026.347.1`。重新获取的远端 job 日志仍为前者；本地 19 个归档文件和 archive manifest 的 hash 未变。没有覆盖原始证据，也没有重跑任务。

已定位两个来源，但**尚不能确定上游哪个环节造成不一致**：

- 与实际 runner `2.338.2` 对应的 [JobExtension.cs](https://github.com/actions/runner/blob/v2.338.2/src/Runner.Worker/JobExtension.cs#L108)从 setup 文件打印组内容；[HostContext.cs](https://github.com/actions/runner/blob/v2.338.2/src/Runner.Common/HostContext.cs#L510)将其定位为 runner 根目录的 `.setup_info`。
- Radish 直接采集进程环境变量 `$env:ImageVersion`，在 PowerShell 契约执行前已经保存；工作流与相关 PowerShell 中未发现改写该变量的路径，代码与运行提交一致。
- 官方 image commit `36de032a97840ee900185b6168c9d63983e8db5a` 的[构建模板](https://github.com/actions/runner-images/blob/36de032a97840ee900185b6168c9d63983e8db5a/images/windows/templates/build.windows-2022.pkr.hcl#L63)使用同一 `image_version` 调用 [Configure-ImageDataFile.ps1](https://github.com/actions/runner-images/blob/36de032a97840ee900185b6168c9d63983e8db5a/images/windows/scripts/build/Configure-ImageDataFile.ps1)与 [Configure-SystemEnvironment.ps1](https://github.com/actions/runner-images/blob/36de032a97840ee900185b6168c9d63983e8db5a/images/windows/scripts/build/Configure-SystemEnvironment.ps1)，生成 image 描述和机器级环境变量。两处版本并非设计上允许分别代表不同版本；公开源码也不能证明该 VM 的实际制作 / 投放过程。
- [官方 Release](https://github.com/actions/runner-images/releases/tag/win22/20261026.347)存在，VS `17.14.37824.6`、PowerShell `7.6.9` 与实测相符；日志所指 `win22/20261025.346` 的 Git tag 和 Release 在核对时均返回 404。前者只能支持初始化描述可能陈旧或错误的推断；后者不证明该标签从未存在。
- 同期 [issue 15197](https://github.com/actions/runner-images/issues/15197)也有初始化日志指向不存在标签的用户报告，但涉及 Ubuntu，且没有足以解释本次 Windows 运行的官方根因结论，不拿它关闭本问题。

旧证据未保存当时的 `.setup_info`、`C:\imagedata.json` 及三级环境变量，无法区分镜像制作、runner 配置、用户级覆盖或进程继承。已有 VS / MSBuild hash、契约与清理结果保留为实测，image 身份仍为 unresolved；不自动选择任一版本或放宽 verify。

## 已批准的本地实现

新增 `ImageIdentity.ps1` 和 `ImageIdentity.Contracts.ps1`，沿现有入口接线，不改工作流输入、runner、其他五个 job 或正式依赖。

| 范围 | 实现 |
| --- | --- |
| 来源定位 | 最多 16 层父进程，只按 PID 查询，验证创建时间；唯一实际 worker 的 bin 父目录提供 `.setup_info` 路径；找不到显式失败，不递归扫描 |
| 文件证据 | `.setup_info` / `C:\imagedata.json` 的路径、采样时间、字节大小、最后写入时间及原字节 SHA256；只读句柄限制并发写入 / 删除，文件读取上限 1 MiB |
| 导出边界 | 仅保留唯一 Runner Image 组的四个已校验身份字段及其原始 detail；固定 windows-2022 / 官方 URL 格式，拒绝重复字段与冲突链接；其他组、完整配置与解析异常消息不导出 |
| 环境证据 | 只读 Process / Machine / User 的 ImageOS / ImageVersion；用户级 null 合法，存在时须一致；不枚举全部环境，不修改 registry / profile |
| 一致性 | 所有必需来源均可读取、解析并相互一致才为 consistent；缺失或冲突保留具体来源 issues，无 fallback，无规范化覆盖 |
| 失败顺序 | 早期进度 → 身份诊断 → 20 组契约 → VS / MSBuild 库存 → 一致性断言；身份 unresolved 时先保存工具库存，再失败并走原清理 / 上传流程 |
| Verify | 旧的 complete 库存不足以放行，须新增一致性检查及原四项精确身份检查；不新增 bypass 参数 |

库存 schemaVersion 升为 3，保存 `image-identity.json` 的相对路径和 hash。`complete=true` 表示库存字段采集完毕，不代表身份已通过；身份失败时 result 明确 failed。诊断报告 `startupLogCompared=false`，采样时的 `.setup_info` 不自动等于 job 初始化时内容；归档后仍需对照原始日志，不用新 VM 结果回填旧运行。

原 8 组 PowerShell 契约在第二次运行已通过。本次新增 12 组覆盖：文件 hash / 白名单及已知属性大小写；非法 JSON / 重复组；额外字段拒绝；链接版本冲突；缺失 / 过大 / 编码错误；一致来源与空用户覆盖；两份文件各自冲突；Machine / User 覆盖冲突；必需来源与 OS 拒绝；旧库存 / unresolved / 采样值漂移拒绝；worker 父进程定位；PID 复用 / worker 缺失。全部 fixture 只写既有 owned work，沿原 finally 删除。

## 验证与执行边界

```bash
node --test Scripts/hangfire/windows/contracts.test.mjs Scripts/ci/validate-sqlite-platform.test.mjs Scripts/hangfire/toolchain.test.mjs
npm run check:repo-quality:changed
git diff --check
```

- Node 共 31 项通过：Windows Razor 14 项静态契约、SQLite 9 项、既有 Hangfire 8 项。原五个 job 摘要保持一致。
- 本机无 pwsh，未执行 PowerShell parser / 新增 12 组行为契约或实际 CIM / 文件来源读取；没有安装工具。不得把 Node 源码检查记成 Windows 行为验证。
- 文档链接 / 文本卫生与 diff 检查通过；记录索引仍有超过建议 300 行的非阻断提醒，本次未扩展索引重构。
- 未新增 SDK、包缓存、服务、端口或数据库。旧归档保持原 hash；本次没有远端运行资源需要清理。

下一步先按新候选完整 SHA 单独确认 push 与一次 inventory（含 20 组契约、白名单上传、下载校验及 owned cleanup）。任一必需身份来源缺失 / 冲突会失败并保留诊断，不自动重试；成功后还须人工对照初始化日志。该授权不包含 verify、SQLite 其他平台、master 合并、tag、发布或部署。
