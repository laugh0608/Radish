# 2026-10-20 dev → master 合并前检查（许可证待办）

- 日期：2026-10-20（Asia/Shanghai）
- 批次基线：`master f5262a09` → `dev 9b17484d`，49 个提交、301 个文件；检查开始时工作区干净，`dev` 与 `origin/dev` 一致，已包含 `origin/master`。
- 当时结论：**暂缓 PR**。合并前检查时项目所有者选择申请 Six Labors Community 许可；随后明确建立[第三方依赖许可治理与替代专题](../features/dependency-license-governance.md)，当前先评估完整替代与适用许可路径，不以取得 key 作为唯一恢复方式。检查阶段未创建 PR、提交或推送；同日后续获准将这批开发候选与方案提交保存，严格构建阻断仍保留。

> 后续状态（2026-10-21）：Six Labors 四包已由 Rust 单实现替代，修正后的完整预检通过；PR #75 已合并为 `4a8589b6` 并回灌 dev。详见[图片替代记录](./rust-image-replacement-2026-10-20.md)。下文保留原检查时的失败和停止线，不作为当前阻塞结论。

## 范围与远端门禁

本批集成日志 L2 的通知 / 实时推送、评论 / 内容 / 帖子、问答 / 投票 / 抽奖、标签 / 分类 / 表情，以及 HTTP / Kestrel / SignalR / YARP / 路由 / 认证授权 / OpenIddict / DataProtection 框架来源、Auth 自有日志、SelfLog、三宿主启动与停止摘要及 Common 程序集诊断。另含客户端官方 / 自部署实例连接规划和联系邮箱更新。

L2 尚未整体关闭，生产日志候选开关保持关闭；客户端连接变更只属于规划文档，不代表运行时功能已实现。未修改 Flutter 产品代码、发布配置或数据库迁移逻辑。

- 完整差异命中后端影响面 218 个文件（后端宿主 / 服务 / 数据模型），身份影响面 1 个文件（`AccountController`，Auth 协议输出）；没有用干净工作区的 changed-only 结果代替 PR 影响面。
- 使用宿主机 `/opt/homebrew/bin/gh`，确认当前账号可访问仓库，无已有的 `dev → master` 开放 PR。
- 远端 `master` ruleset 实际要求 `Candidate Quality`、严格保持分支最新、通过 PR 合并；允许 merge commit / rebase merge，禁用 squash merge。

## 提前发现的依赖阻断与已授权修复

`npm run check:dependency-security` 初次执行：npm High / Critical 为 0，NuGet 有 53 条 High 命中。跨项目去重后涉及三个包，不是 53 个独立漏洞：

| 包 | 检查时版本 | 本轮候选版本 | 处理位置 |
| --- | --- | --- | --- |
| `SixLabors.ImageSharp` | `3.1.12` | `4.1.3` | `Radish.Infrastructure` |
| `SixLabors.ImageSharp.Drawing` | `2.1.7` | `3.1.3` | `Radish.Infrastructure`，配套升级 |
| `System.Security.Cryptography.Xml` | `10.0.10` | `10.0.13` | 两个 Auth migrations 项目 |
| `System.Security.Cryptography.Pkcs` | 传递 `8.0.1 / 10.0.10` | `10.0.13` | `Radish.Common` 显式引用，传递到相关宿主 |

项目所有者已单独授权本轮依赖更新、`dotnet restore Radish.slnx --disable-parallel`、必要图片 / 水印兼容调整及重新验证。`Drawing 3.1.3` 的包元数据要求 `ImageSharp >=4.1.3`，因此将原拟 `4.1.2` 对齐到 `4.1.3`。npm 声明及 lockfile 未改变。

图片处理兼容调整仅涉及三处：文字水印使用 `Paint / DrawingCanvas.DrawText` 与 `SolidBrush`；文字测量使用 `MeasureBounds`；非 nullable `PixelTypeInfo` 直接读取 `BitsPerPixel`。修改过的 Infrastructure csproj 同时去除既有 UTF-8 BOM。

升级后联网安全审计通过：npm / NuGet High、Critical 均为 0。漏洞依据：[ImageSharp](https://github.com/advisories/GHSA-j3p4-wp97-rph4)、[XML](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6)、[PKCS](https://github.com/advisories/GHSA-846f-f764-76mx)。这只代表本次审计时间点，不替代恢复集成时重新审计。

## 当前验证证据

| 验证 | 结果与范围 |
| --- | --- |
| `npm run check:repo-quality:candidate` | 原批次全仓历史预算、文档卫生与本地链接通过 |
| `npm run lint` | 四个 Web workspace 全量零 warning |
| Client / Console 显式 app / node 类型检查及 Console strict 检查 | 5 条命令通过，补充聚合 type-check 的已知覆盖限制 |
| `npm run validate:baseline:host -- --warnings-as-errors --report-file .tmp/master-pr-20261020-baseline.md` | 升级前原候选完整基线通过；0 warning / error，后端 3951 通过、43 跳过；doctor / verify 只读自检通过。该结果不代表升级后的严格构建通过 |
| `npm run check:logging-contract` | 48 项通过 |
| `dotnet restore Radish.slnx --disable-parallel` | 版本对齐后还原成功 |
| `npm run check:dependency-security`（升级后） | npm / NuGet High、Critical 均为 0 |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1`（升级后） | 被缺少 Six Labors 许可证阻断；其余三处 API 错误已随后适配 |
| `dotnet build Radish.slnx -c Debug --no-restore -m:1`（API 适配后） | 编译通过，保留库自身允许 Debug 继续的许可证 warning，不计作严格构建通过 |
| `npm run validate:identity`（升级后） | 扫描和 35 项定向测试通过、0 跳过；构建仍有许可证 warning |
| 升级后全量测试（带 PostgreSQL 17） | `dotnet test Radish.Api.Tests --no-build --no-restore -m:1`，3993 通过、0 失败、1 跳过，共 3994 项，耗时 7 分 26 秒；PostgreSQL 集成测试全部执行，唯一跳过项是未配置真实 Rust 动态库的 ABI 测试 |
| `npm run check:repo-quality:changed`（本轮修改后） | 修改文件卫生、根入口同步、全量文档与链接检查通过 |
| `git diff --check` | 原批次差异与本轮修改均通过 |

沙盒首次禁止前端服务器合约测试监听临时端口（`EPERM`），联网审计亦受限；切换宿主机后取得真实结果。没有把环境失败改写为测试成功。

未启动业务宿主、未执行浏览器 / AuthFlow 真实 Smoke、未访问生产、未创建 tag / Release / 镜像或部署。Rust 源码本批未变；真实 Rust 动态库 ABI 测试未配置 `RADISH_TEST_NATIVE_LIBRARY`，不能将其跳过写成通过。

## 许可证阻断与恢复顺序

新版 Drawing 的 `SixLabors_ValidateLicense` 在编译前执行。缺少有效 key 时，普通 Debug 给出 warning，Release 不允许继续，warning-as-error 同样失败。本轮没有添加 warning 例外或排除许可证 target。

合并前检查时项目所有者确认无已有许可证，并选择“暂缓 PR，申请 Community 许可后继续升级”。随后同日改为先做全项目许可盘点和替代方案，详见[审计记录](./dependency-license-audit-2026-10-20.md)。四个候选包内 Split License 也存在 Source Available 等条件分支，不能把构建 key 阻断等同于必须购买商业许可。以下 1–3 项仅为后续若选择保留 Six Labors 的备选接线；若选择完整替代，按专题门禁退出四包后再集成。

[官方许可说明](https://sixlabors.com/pricing/)与[Community 申请入口](https://licensing.sixlabors.com/)供备选路线核对；若使用程序集范围的 Community key，应覆盖实际直接引用项目 `Radish.Infrastructure`。

1. 项目所有者取得许可后，只提供本机文件路径；不要将许可正文放进聊天、提交或测试记录。
2. 通过包自身的 `SixLaborsLicenseFile / SixLaborsLicenseKey` 入口进行本机验证，完成 Debug warning-as-error 与 Release 构建及相关回归。
3. 在创建 PR 前确认 GitHub Actions 的安全注入方式；正式 Docker 构建也需覆盖许可输入。当前未设置 GitHub Secret 或改写 CI / Docker 构建链路，接线范围需先确认。
4. 恢复时重新 fetch 分支、复核工作区与 PR 差异，按最新候选补齐卫生、严格构建、测试和联网审计，再提交后续修复与证据、推送 `dev`、创建 `dev → master` PR 并等待 `Candidate Quality`。
5. 后续若合并 PR，先将最新 `origin/master` 快进或普通 merge 回灌并推送 `dev`，再推进下一轮开发；不自动触发发布或部署。

## 临时环境与本地证据

- 项目所有者单独授权启动 `radish-master-pr-20261020-pg`（`postgres:17`），仅绑定 `127.0.0.1:55432`，不挂载业务目录；测试结束后已执行 `docker stop` 与 `docker rm`，停止并删除容器。
- 升级后全量测试使用 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 指向该隔离容器，不访问本机既有业务 PostgreSQL。
- 日志位于 `/private/tmp/radish-master-pr-20261020-*.log`，基线报告位于 `.tmp/master-pr-20261020-baseline.md`，升级后 TRX 位于 `.tmp/master-pr-20261020-results/`。临时日志及报告不是版本库长期资产，以上正文保留必要结论。
- 五个代码 / 项目文件及本记录在检查结束时保持未提交；同日后续按项目所有者要求提交保存。恢复集成前应先复核，不得将开发检查点误认为可发布候选。
