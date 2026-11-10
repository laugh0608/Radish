# Windows Razor inventory 本地修复

> 2026-10-31（Asia/Shanghai）。接续[首次 inventory 失败](./hangfire-windows-razor-inventory-2026-10-31.md)，项目所有者批准本地修复、PowerShell 契约接线及文档 / 本地检查。**本地检查通过；修复未 push / dispatch，PowerShell 新契约未运行，verify 未执行。** 结构化状态见[结果 JSON](./hangfire-windows-razor-inventory-repair-2026-10-31.json)，当前执行边界见[方案](../features/hangfire-windows-razor-validation-plan.md)。

## 原因与修复

| 问题 | 本地修改与影响 |
| --- | --- |
| `windows-2025` 已映射 VS 2026，旧清单不能代表当前标签 | 仅 Razor job 改用 `windows-2022`，保留 `[17.0,18.0)` 唯一实例、Full amd64 MSBuild、Framework 至少 4.8。结果只覆盖实际 Server 2022 环境 |
| `$name` 环境变量循环覆盖 `$Name` 参数 | `Invoke-OwnedCommand` 改为 `$CommandName` 与 `$environmentKey`；日志路径、命令记录、失败 / 超时消息统一使用命令标识。进程参数、环境白名单和所有权清理语义不变 |
| VS 早期失败导致没有完整库存，也没有结构化基础身份 | 先保存不完整进度报告，随后执行契约 / 逐步采集；保存 runner、工作流 SHA、vswhere 原始响应及已取得工具身份。仅全部检查通过才写完整库存，verify 拒绝不完整报告 |
| 现有 Node 源码契约无法证明 PowerShell 行为 | 增加无外部依赖的 `Contracts.ps1`，同一入口在实际库存前执行；没有新增矩阵、跳过开关或浮动工具安装 |

官方映射与 Windows 2022 清单读取自 `actions/runner-images` commit `4da2307112a0fad2fd2ecaca8ba7d08683b8c581`：

- [标签映射](https://github.com/actions/runner-images/blob/4da2307112a0fad2fd2ecaca8ba7d08683b8c581/README.md)，blob `8de90fd8b3b5bf9aeec88151e87a3469973bbecd`。
- [Windows 2022 清单](https://github.com/actions/runner-images/blob/4da2307112a0fad2fd2ecaca8ba7d08683b8c581/images/windows/Windows2022-Readme.md)，blob `b27a30ca8423b555ca3b1640c82a1d335183cd12`；Image `20261026.347.1`、VS `17.14.37824.6`、PowerShell `7.6.9`，仅作准备参考，不是实际工具冻结值。
- [官方迁移公告](https://github.com/actions/runner-images/issues/14017)明确需要 VS 2022 时使用 `windows-2022`；VS 2026 的 MSBuild / 旧 Razor task 兼容性不在此次修复范围。

## 证据与行为契约

`inventory-progress.json` 为 schemaVersion 2，始终 `complete=false`，标明当前采集阶段；包含实际 `GITHUB_WORKFLOW_SHA / REF`，失败时保留已知值与缺失项。VS 原始响应先落盘再解析，解析结果先落盘再检查唯一实例及版本。MSBuild 路径 / hash、版本、Framework 程序集和 Release 值逐段保存。只有检查全部通过才创建 `inventory.json` / `complete=true`；四项 verify 精确身份门禁继续有效。

新增 PowerShell 契约共 8 组，**目前只有接线 / 源码静态检查，没有实际执行结果**：

1. 成功子进程的命令名、日志名、退出码与显式环境变量保持一致。
2. 预期 exit 23 子进程的命令名、日志名、错误消息和退出码保持一致。
3. VS 零匹配拒绝且保留原始响应。
4. VS 唯一 17.x 实例可选择且保留进度。
5. VS 多匹配拒绝且保留候选。
6. VS 18.x 实例拒绝。
7. 非法 JSON 拒绝且原始文本仍可审阅。
8. 不完整库存不能用于 verify 身份校验。

两个子进程仅调用当前 `$PSHOME/pwsh.exe`，各限 30 秒，走原 owned process helper；不调用 MSBuild / SDK / Razor。虚构 VS 数据仅写 `work/powershell-contracts`，报告标记 `fixtureOnly=true`，不会生成真实 `inventory.json`。预期 exit 23 原样留在 commands 证据中，由契约报告解释，不改写为成功退出。

## 本地检查与清理边界

```bash
node --test Scripts/hangfire/windows/contracts.test.mjs Scripts/ci/validate-sqlite-platform.test.mjs Scripts/hangfire/toolchain.test.mjs
npm run check:repo-quality:changed
git diff --check
```

- Node 共 28 项通过：Windows Razor 11 项（本轮增加 3 项静态检查）、既有 SQLite 9 项、既有 Hangfire 8 项；覆盖五个原 job 摘要、runner / 手动范围、身份与失败证据顺序、PowerShell 前置接线、fixture / 清理隔离。
- changed-only 仓库卫生、完整文档检查与 `git diff --check` 通过；PS1 按仓库规范保留 UTF-8 无 BOM / CRLF。记录索引 337 行触发超过建议 300 行的非阻断提醒，本轮未扩展索引重构。
- 本机没有 pwsh / Windows Full MSBuild；未安装任何依赖或工具，未执行 PowerShell parser / 行为、Windows inventory 或上游任务。C# 未改动，未重复其构建和原 Core 渲染。
- PowerShell fixture 的 finally 清理已接线；即使契约失败，外层 finally / CI always 仍负责 owned work 清理、失败报告与白名单上传。原始失败 archive 14 个文件 hash 核对通过，未修改历史 artifact。
- 本轮只执行 Node / 文档检查，没有新建服务、端口、数据库、SDK / 包缓存或 runner；无新增运行资源需清理。

下一步先冻结修复提交，再分别确认该完整 SHA 的 push 与一次 inventory（含上述契约及证据上传 / 下载）；通过后才讨论 verify。没有自动重跑旧 run，没有其他矩阵、发布或部署。
