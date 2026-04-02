# 身份语义 Phase 4 最终启动评审

> 本文用于回答最后一个问题：**在三份前置资产都已齐备后，身份语义 Phase 4 现在是否应正式启动？**
>
> 关联文档：
>
> - [身份语义 Phase 4 启动前提确认](/guide/identity-claim-phase4-readiness)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
> - [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)
> - [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [当前进行中](/planning/current)

## 1. 评审目标

这份评审不再重复回答“还缺什么文档”，而是直接回答：

- 前置资产是否已经足够支撑启动判断；
- 仓库内外兼容边界是否已经清楚到可以正式开工；
- 如果还不能开工，下一步应切到哪里，而不是继续停留在模糊状态。

## 2. 当前评审输入

截至 `2026-04-02`，当前已经具备以下三份前置资产：

1. [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
2. [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
3. [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)

这意味着：

- 仓库内关键协议消费者已经被事实化；
- 长期保留字段、历史双写字段与输入兼容边界已经有单一口径；
- 首轮实施顺序、官方回归顺序与默认回滚优先级已经可执行。

因此，当前已经不再缺“启动判断所需的仓库内文档输入”。

## 3. 评审项

### 3.1 仓库内运行时前提

评审结论：**通过**

依据：

- 运行时代码主路径已经完成 `CurrentUser / ICurrentUserAccessor` 收口；
- 兼容层已冻结；
- 身份语义扫描已接入当前验证基线。

### 3.2 仓库内协议消费者事实化程度

评审结论：**通过**

依据：

- `radish-client`
- `radish-console`
- `radish-scalar`
- `Radish.Api.AuthFlow.http`
- `Radish.Api` 资源服务器边界

以上对象当前都已经被收束到明确文档，不再只是“可能存在依赖”。

### 3.3 字段保留与回滚口径

评审结论：**通过**

依据：

- 标准长期保留字段已经明确为 `sub / name / preferred_username / role / scope / tenant_id`
- `ClaimTypes.NameIdentifier / ClaimTypes.Role / ClaimTypes.Name` 已明确为“允许停止双写”
- `TenantId / jti` 已明确为“只保留输入兼容”
- 首轮窗口默认回滚顺序也已定义

### 3.4 仓库外兼容边界

评审结论：**不通过**

当前问题不是“发现了明确的仓库外阻塞项”，而是：

- **当前没有足够事实证明仓库外不存在阻塞项。**

具体来说，仓库当前仍无法直接证明以下问题已经被关闭：

1. 已部署环境里是否还有脚本、联调工具、外部网关映射或历史资产在直接读取 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId`。
2. 是否存在仓库外调用方只消费 Access Token、不走 `userinfo`、也不依赖当前前端标准映射。
3. 开放平台“允许第三方客户端接入”的规划，在现实环境里是否已经出现真实接入方。

只要这三类问题没有被事实关闭，当前就不能把“可以正式停双写”写成已通过结论。

## 4. 最终评审结论

### 4.1 结论

**当前最终启动评审不通过，不建议正式启动身份语义 Phase 4 实施。**

更准确的表述是：

> **Phase 4 的仓库内启动输入已经齐备，但仓库外兼容边界尚未被事实关闭，因此当前不进入“协议输出收敛实施”。**

### 4.2 当前状态建议

当前事项建议从：

`启动前提资产已完成 3/3，等待最终启动评审`

更新为：

`最终启动评审未通过，继续暂停 Phase 4 实施`

### 4.3 为什么不是“有条件立即启动”

因为当前缺的不是“更细一点的实现方案”，而是 **启动许可所依赖的外部事实**。

如果在这个状态下直接开工，风险会从仓库内转移到：

- 已部署环境中的未知脚本与联调资产；
- 外部网关映射；
- 尚未被盘清的第三方 / 开放平台接入边界。

这不符合当前文档已经定义好的启动标准。

## 5. 下一步建议

### 5.1 近端建议

当前最合理的下一步不是立刻改 Auth 输出，而是先补一份 **仓库外兼容边界确认清单**，至少逐项回答：

1. 当前有哪些真实部署环境仍在使用旧 Token / 历史脚本。
2. 是否存在读取 `ClaimTypes.NameIdentifier / ClaimTypes.Role / TenantId` 的外部脚本或网关映射。
3. 是否存在仓库外只消费 Access Token 的调用方。
4. 是否已有真实第三方 / 开放平台客户端接入，而不是仅停留在规划层。

只有把这几项写成事实，当前评审才可能从“不通过”变成“通过”。

当前产出：

- 已新增 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist)
- 后续若要重新评估 Phase 4，当前应先按该清单逐项关闭外部事实缺口

### 5.2 若短期无法确认

如果短期内无法拿到这些外部事实，当前主线建议按既有规划切换为：

[M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)

原因：

- 这比把 Phase 4 长时间挂在“随时要启动”的模糊状态更稳；
- 也符合当前规划页已经约定的主线切换顺序。

## 6. 当前结论摘要

截至 `2026-04-02`，当前可以把最终判断明确写成一句话：

> **身份语义 Phase 4 当前不启动；仓库内输入已齐，但仓库外兼容边界未被事实关闭。**

因此，后续主线只剩两个选择：

1. 补仓库外兼容边界确认清单，然后重新做一次启动评审。
2. 若短期内无法确认，则转入 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)，不继续悬挂 Phase 4。
