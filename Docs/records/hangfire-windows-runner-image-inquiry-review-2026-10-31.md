# Windows runner image 上游询证与关闭记录

> 2026-11-07（Asia/Shanghai）。[actions/runner-images#15201](https://github.com/actions/runner-images/issues/15201) 已以 `completed` 关闭。Radish 的 Windows Razor 验证继续保留在后续维护 / 发行队列，执行边界见[专题方案](../features/hangfire-windows-razor-validation-plan.md)。

## 上游关闭结果

- 上游确认 Radish 报告的运行实际使用 `20261026.347.1`。该镜像由 `20261025.346.1` 重新特化，已安装软件相同，适用 [347 发行说明](https://github.com/actions/runner-images/releases/tag/win22%2F20261026.347)。重新特化正确更新了 Machine / Process `ImageVersion`，但未重写 `C:\imagedata.json`，从该文件取值的启动日志和 `.setup_info` 因此继续显示 `20261025.346.1`。这更正了此前「环境变量保留基础镜像版本」的初步解释。[上游更正](https://github.com/actions/runner-images/issues/15201#issuecomment-6689880438)
- 上游确认常规完整构建 [`20261101.353.1`](https://github.com/actions/runner-images/releases/tag/win22%2F20261101.353) 已部署到 `windows-2022`。独立复测截图显示启动日志、`.setup_info`、`C:\imagedata.json` 和 Machine / Process `ImageVersion` 均为 `20261101.353.1`，一致性检查通过。User 层未设置是上游说明的预期行为。重新特化时同步重写 `C:\imagedata.json` 的防复发改动仍由上游跟进。[部署说明](https://github.com/actions/runner-images/issues/15201#issuecomment-6689880438) · [独立复测截图](https://github.com/actions/runner-images/issues/15201#issuecomment-6700569091)
- `laugh0608` 回复确认当前镜像的差异已消除，并于 `2026-11-07 11:30:20 +08:00` 关闭 issue。GitHub API 核验 `state=closed`、`state_reason=completed`。[关闭回复](https://github.com/actions/runner-images/issues/15201#issuecomment-6701669445) · [Issue](https://github.com/actions/runner-images/issues/15201)
- 独立复测覆盖新镜像的版本一致性。Radish 的诊断报告继续保留原始冲突值与门禁失败结果。新 `inventory`、工具身份审阅、Razor 再生成与 `verify` 留待维护批次，恢复时另行确认范围及运行授权。本次文档更新未触发 CI。

## 2026-10-31 提交结果

- 诊断记录已提交为 `39066415bcdc3aa61675f15889f917cc2bbb4f2e`。项目所有者审阅并批准了询证稿与证据子集。
- Issue：[actions/runner-images#15201](https://github.com/actions/runner-images/issues/15201)，由 `laugh0608` 于 `2026-10-31T10:52:16Z`（北京时间 18:52:16）创建；提交后核验为 open。
- 附件：[windows-2022-image-identity-evidence-41193259788.zip](https://github.com/user-attachments/files/34215319/windows-2022-image-identity-evidence-41193259788.zip)。重新下载得到 6020 bytes，SHA256 与审阅包完全一致。
- 远端标题和正文逐字核对通过：保留审阅稿，仅在正文末尾追加附件链接。附件 ZIP 内部的 “NOT SENT” 是准备时快照，按授权原样上传，未为状态更新改包。
- GitHub 连接器创建操作返回 403 / Resource not accessible by integration，未创建 Issue；随后使用本机已登录 GitHub CLI 创建一次成功。附件通过 GitHub 网页上传，空白表单已关闭，未重复创建或留言。
- 本地留痕：`artifacts/hangfire-windows-inquiry/41193259788/submitted-body.md`、`submission.json` 和 `uploaded-evidence-verified.zip`。原始诊断归档与审阅 ZIP 保持不变；仅更新包外提交状态。
- 本轮未 push、未重跑 inventory、未执行 verify 或其他矩阵。身份冲突仍为 unresolved，等待上游解释；新运行仍须另行明确授权。

## 已批准的提交方式与核心诉求

已向 [actions/runner-images](https://github.com/actions/runner-images/issues) 提交一份 Windows Server 2022 Bug Report，使用[已审阅的标题 / 英文正文](./hangfire-windows-runner-image-inquiry-draft-2026-10-31.md)。本次已只读核对其[表单](https://github.com/actions/runner-images/blob/main/.github/ISSUE_TEMPLATE/bug-report.yml)，模板 blob 为 `a0d4a8fc7b680cf228966c2e9688a95910388287`。勾选 GitHub Actions - Standard Runners / Windows Server 2022；回归状态填 Unknown。

请求上游回答四件事：

1. 根据 run / job / Worker ID，确认实际投放的 image revision 和对应软件清单。
2. 解释为什么两份描述文件与 Machine 环境值不同步，明确哪项元数据错误或陈旧；不只给出“忽略某个字段”的建议。
3. 给出已知问题 / 修复跟踪及含修复的 image 或部署范围，说明之后如何用诊断验证。
4. 如果这些字段设计上代表不同含义，提供受支持的映射文档；若归属托管 runner 配置团队，请协助转交。

选择该入口的理由是冲突同时存在于 `C:\imagedata.json` 与机器级环境，而 runner 日志忠实反映 `.setup_info`；目前没有证据证明 runner 的日志输出代码有缺陷。[Ubuntu issue 15197](https://github.com/actions/runner-images/issues/15197)仅作背景线索，所核对评论仍无官方根因说明，不直接追加为同一故障，也不同时向多个仓库重复报送。

## 证据口径与附件

草稿以 [run 41193259788](https://github.com/laugh0608/Radish/actions/runs/41193259788) / attempt 1 / commit `75a9e33f8a3d1856d1fa0ffaa7f96c091824d456` 为主证据。采样时 `.setup_info`、`imagedata.json` 和初始化日志都为 `20261025.346.1`；Machine / Process 为 `20261026.347.1`；User 为 null。20 组契约通过，失败来自本项目的一致性断言。不得改写成编译失败、确认上游污染或已经查明制作环节。

保留如下限制：

- 两源文件只保存现场 hash 与白名单字段，未导出完整文件；不能在本地独立重算它们的 hash，时间戳也不能单独证明镜像制作顺序。
- `startupLogCompared=false` 是原报告的真实值；归档后逐项日志对照通过属于后续审阅事实，不回写原文件。
- 第二次 inventory 虽为 success，但已有日志 / 环境差异且缺少完整来源快照，不能列为最后正常版本。
- VS / MSBuild 等工具相同不能证明整套 image 相同；本次证据不倒推旧 VM 的具体原因。

准备的审阅附件位于 `artifacts/hangfire-windows-inquiry/41193259788/`：

| 文件 | 来源与用途 |
| --- | --- |
| `image-identity.json` | 从原 artifact 原样复制，完整保留白名单身份报告 |
| `powershell-contracts.json`、`result.json`、`cleanup.json` | 原样复制，解释 20 项契约通过、身份拒绝与清理结果 |
| `startup-image.log` | 从已归档 job.log 原字节截取起始至 Runner Image 组结束，包含 runner / provisioner / OS / image 身份；不包括 token 权限组之后的内容 |
| `context.json` | 从已归档 run / jobs 元数据提取的 run ID、SHA、事件、时间、job、artifact 来源和后续日志对照结论，明确属于派生摘要 |
| `README.txt`、`review-manifest.json` | 说明子集边界；逐文件 hash / size、原 archive / artifact manifest 身份、原始文件与摘录的来源关系 |
| `windows-2022-image-identity-evidence-41193259788.zip` | 上述材料的独立附件包，明确不是原 GitHub artifact |

附件 ZIP 共 8 个文件（7 个 payload + manifest），6020 bytes，SHA256 `f5c584fd474a6142a4083d922e097be80988229dddfaac479dc9a5a0340e6b27`；校验清单自身 SHA256 为 `90cd726a3eb2fab17bdbd5deb3d668be6cf3ad32c61983a7b386c8842b00a9a8`。ZIP 解包检查、逐文件 size / hash 与四份原报告逐字节核对均通过。

英文草稿已内嵌关键版本、路径、hash、UTC 时间、region / Worker ID 与公开 run 链接，可在原 artifact 到期后继续阅读。对外发送范围限英文标题 / 正文及这份证据子集；不附完整本地归档、VS 安装原始响应、内部 setup 组或全部环境变量。Worker ID 来自公开日志，保留以便上游定位。

原 artifact 到期时间为 `2026-11-07 18:34:11 +08:00`；已下载的本地副本继续保留，不需要为了保留证据再次运行或延长远端保留期。本轮已按明确授权公开上传上述证据子集，完整原始归档仍留在本地。

## 提交时的后续判定方案

| 阶段 | 具体行动 | 完成条件 / 停止线 |
| --- | --- | --- |
| 材料审阅（已完成） | 项目所有者批准英文措辞、4 个问题和证据子集 | 批准范围为向 actions/runner-images 提交询证和指定附件；不包含 push 或新运行 |
| 询证提交（已完成） | 已创建 #15201 并附上原样 ZIP，远端正文与附件下载校验通过 | 等待上游回复；补充材料或转交按实际范围处理，不顺带触发 CI |
| 上游确认已修复 | 先只读核对修复说明、适用 image 与元数据契约，再冻结当时精确代码 SHA 和一次 inventory 方案 | 重新批准后才运行；20 组契约、必需来源一致、工具身份、日志对照、上传 / 下载及清理都须通过；失败仍不自动重试 |
| 上游说明字段含义不同 | 先审阅有版本依据的正式映射，必要时单独提出身份建模 / 门禁变更方案 | 不因一句“以某值为准”就忽略其他来源或删断言；变更须批准并验证 |
| 无结论或无法复现 | 继续保留 unresolved 与旧证据；若请求新采样，先明确它要排除的假设和新增采集项 | 不反复调度碰运气，不选一个版本作为默认，不扩大到其他平台或自托管环境 |
| 身份审阅关闭后 | 依据当时完整库存，另行提出 verify 的精确 SHA、工具期望值、输入 / 清理方案 | inventory 成功或上游回复都不自动授权 verify |

上游询证结果见本文首节。维护批次恢复后，先按专题方案确认新 `inventory` 的完整提交 SHA、输入和执行范围，再依据该批实际库存审阅 `verify` 的期望值。
