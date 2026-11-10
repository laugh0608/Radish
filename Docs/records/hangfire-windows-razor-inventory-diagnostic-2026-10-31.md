# Windows Razor 诊断 inventory 与身份门禁

> 2026-10-31（Asia/Shanghai）。指定提交已 push，并按授权执行一次诊断 inventory。**20 组 PowerShell 契约通过，工具库存采集完成；文件元数据与 Machine / Process 环境版本不一致，身份门禁按设计拒绝，run 为 failure。** 证据上传、下载校验与清理完成，未重试、未执行 verify。[结构化结果](./hangfire-windows-razor-inventory-diagnostic-2026-10-31.json)；接续[诊断增强](./hangfire-windows-razor-image-identity-diagnostics-2026-10-31.md)与[专题方案](../features/hangfire-windows-razor-validation-plan.md)。

## 提交与执行身份

- 批准、push、run HEAD、库存与诊断报告中的 workflow SHA 均为 `75a9e33f8a3d1856d1fa0ffaa7f96c091824d456`；工作流 blob 为 `c4e490ccada977243ae4702ab9e4617312b5dea7`。
- `dev` 从 `464910ddfa5f1df076128e86fdc618742ebeee0e` 快进一个提交；默认分支 `master` 保持 `4a8589b6a1c5264506c51429cf7fe470e783f9fc`，执行前没有开放的 dev PR。
- push 已成功，本地跟踪引用写锁受沙盒限制；核对远端后，带旧值校验最小提权更新 `origin/dev`，没有重复 push。
- [Run 41193259788](https://github.com/laugh0608/Radish/actions/runs/41193259788)，attempt `1`，创建时间 `2026-10-31 18:33:49 +08:00`；Windows job `124514190507`，其余五个 job 全部 skipped。
- 输入：`validation_scope=hangfire-razor`、`razor_mode=inventory`、`expected_head_sha=75a9e33f8a3d1856d1fa0ffaa7f96c091824d456`。四项 verify 期望值未传；输入证据为 dispatch 命令和 job 日志，未保存独立事件 payload。

## 身份冲突已定位到本次 VM 的文件与机器环境

| 来源 | 实测版本 | 附加事实 |
| --- | --- | --- |
| job 初始化 Runner Image 日志 | `20261025.346.1` | Image 为 windows-2022，两个链接均指向 `20261025.346` |
| `C:\actions-runner\cached\2.338.2\.setup_info` | `20261025.346.1` | 来源由实际 worker 父进程定位，最后写入时间 `2026-10-31T10:33:51.9126576Z` |
| `C:\imagedata.json` | `20261025.346.1` | 最后写入时间 `2026-10-25T21:42:20.3679383Z` |
| Machine 环境 | `20261026.347.1` | ImageOS 为 win22 |
| Process 环境 | `20261026.347.1` | ImageOS 为 win22，与 Machine 一致 |
| User 环境 | null | ImageVersion / ImageOS 均未设置 |

归档复核逐项比较初始化日志的四条 image 信息，与两文件的 `selectedDetail` 完全相同。因此本次冲突不只存在于日志显示，也未见用户级覆盖；证据直接表明 image 描述文件与机器级环境元数据不同步。**仍不能确定哪个版本代表实际完整镜像，也不能仅凭最后写入时间认定具体制作 / 投放环节出错。** 旧运行 `41191661741` 缺少这些来源快照，本次观察不能作为它的追溯证明。

诊断 `assessment.status=unresolved`，仅两项问题：`files.setupInfo.versionMismatch` 和 `files.imageData.versionMismatch`。所有必需来源均已取得，没有路径猜测或缺失项兜底。两份文件分别记录原字节 SHA256：

- `.setup_info`：`e4eb6bfd3794c182cd29565152665e956e90551a2f3070b6e1963d7589cbeb36`，776 bytes。
- `imagedata.json`：`c2ec31491057e5d8b58218369093a3240d73ad1d15269ae74e943d00fad1eda0`，426 bytes。

完整源文件未导出，只保留其现场 hash 与白名单字段；本地不能独立重算这两个源文件 hash。两文件整体结构不同，不要求它们的 hash 相同。原 `image-identity.json` 的 `startupLogCompared=false` 未改写；独立归档汇总将后续日志对照记为 true。

## 契约、工具与拒绝结果

20 组 PowerShell 行为契约全部 passed（原 8 + 新增 12），fixture 已删除。真实路径定位、文件读取和环境采集均已执行；虚构测试仍独立标为 `fixtureOnly=true`。四条 owned command 为契约成功、契约预期 exit 23、vswhere、MSBuild `/version`，退出码 `0 / 23 / 0 / 0`，均已结束。

VS `17.14.37824.6`、Full amd64 MSBuild `/version` / FileVersion `17.14.60.43110`，MSBuild SHA256 `7da5f86d7dfa627c4071edc9563fda7ace7ec0aaadd8b2aaccb92d2a823535c0`；VS / MSBuild 记录与第二次库存相同。Framework Release `533509`，PowerShell `7.6.9`，兼容程序集身份 / hash 已保留。

`inventory.json` 为 schemaVersion 3 / `complete=true`，诊断报告 hash 与文件相符；随后一致性断言抛出 `Consistent image identity required; inspect image-identity.json`。`result.json` 明确 failed / inventory / `windowsValidationComplete=false`。这表示库存采集完成但不可放行，不是 20 组契约失败，也不是 Razor 构建失败。

## 归档、清理与停止线

- [Artifact](https://github.com/laugh0608/Radish/actions/runs/41193259788/artifacts/13039112970)：`hangfire-razor-41193259788-1`，ID `13039112970`，10392 bytes，保留 7 天，到期 `2026-11-07 18:34:11 +08:00`。
- ZIP SHA256 `1fea6d5f479e9b1b7f8d7c283a7870df7b5a9b5cfc10bd59df73bb8b4dd224b6` 与服务端 digest 相符；manifest SHA256 `5b8cd810946d98e47f5d835d8885004266458da5065130d71d45d4b04a78f515`。12 个 payload 文件的 size / hash 均通过，加 manifest 共 13 个文件，无额外文件。
- 本地归档 `artifacts/hangfire-windows-inventory/41193259788-1` 保存 ZIP、artifact、job 日志、run / jobs / artifacts 元数据、工作流快照、远端引用与汇总。archive manifest 覆盖 21 个文件，自身 SHA256 `f096346d7c1a1ff2527c2da0aa85ae516c44a9e51b86577c20c23a7bc1aa4a18`。`jobs.json` 为 GitHub connector 的规范化响应，不称原始 REST 全字段响应。
- 清理报告 `complete=true`、`workingTreeRemoved=true`、`errors=[]`、`globalToolsChanged=false`；端口和数据库为空。清理、上传和 post-job 均成功；未独立观察 VM 销毁，剩余 checkout / 证据依赖托管 runner 生命周期清除。
- 没有下载 SDK、11 项包或上游源码，没有执行 Razor task、生成、构建、渲染或 verify；没有其他平台矩阵、tag、发布、部署或 master 合并。

本次一次性授权已执行完毕。下一步若要进一步解释上游元数据不同步，可基于本次 run / worker / 两侧身份制作询证材料，再由项目所有者决定是否对外提交；本轮未发送。不得通过选定一个版本、改写证据或放宽一致性门禁推进 verify；后续代码、push、任何新 inventory 或 verify 均按具体范围另行确认。
