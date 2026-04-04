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
> - [身份语义 Phase 4 仓库外兼容边界首轮执行记录（仓库资产侧）](/guide/identity-claim-external-compat-first-pass)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [当前进行中](/planning/current)

## 1. 评审目标

这份评审不再重复回答“还缺什么文档”，而是直接回答：

- 前置资产是否已经足够支撑启动判断；
- 仓库内外兼容边界是否已经清楚到可以正式开工；
- 如果还不能开工，下一步应切到哪里，而不是继续停留在模糊状态。

## 2. 当前评审输入

截至 `2026-04-04`，当前已经具备以下三份前置资产：

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

评审结论：**通过**

当前依据已不再只是仓库内推断，而是已补到实际部署范围的事实：

1. 当前只有 **1 套生产环境**，没有独立测试环境。
2. 生产环境当前以 **单个 Docker 实例**运行，版本为 `v26.3.2-release`。
3. 外层反向代理使用 **1Panel 默认反向代理**，仅承担标准 HTTPS 证书与回源转发职责，未配置自定义 JWT 解析或旧 Claim 字段映射。
4. 当前不存在仓库外的换 Token、联调接口、巡检或解析旧 Claim 的独立脚本。
5. 当前 `OpenIddict` 只有默认种子数据，未发现额外第三方客户端。

这意味着：

- 当前部署范围内，已没有未关闭的“仓库外旧 Claim 消费者”事实缺口；
- “开放平台规划允许第三方接入”当前仍只是规划边界，不是现实部署中的已接入对象；
- 外部兼容边界已从“未知风险”收束为“当前部署口径下已确认通过”。

## 4. 最终评审结论

### 4.1 结论

**当前最终启动评审已完成，且后续首轮实施与官方顺序真实回归也已完成。**

更准确的表述是：

> **Phase 4 的仓库内启动输入已经齐备，当前部署范围内的仓库外兼容边界也已被事实关闭；截至 2026-04-04，协议输出收敛首轮实施与官方顺序真实回归均已完成，当前结论已进入“无需回滚，转入稳定维护”。**

### 4.2 当前状态建议

当前事项建议从：

`启动前提资产已完成 3/3，最终启动评审已通过`

更新为：

`最终启动评审已通过，且后续首轮实施与官方回归已完成`

### 4.3 为什么当前允许启动

因为当前缺的已不再是“启动许可所依赖的外部事实”。

这轮补齐后，外部边界已经具备以下最小启动条件：

- 当前真实部署环境数量和范围清楚；
- 当前真实反代口径清楚；
- 当前真实脚本资产清楚；
- 当前真实客户端列表边界清楚。

因此，当前风险已经回到仓库内可控范围：官方客户端字段兼容、Auth 输出收缩顺序，以及回滚窗口执行纪律。

## 5. 后续建议

### 5.1 当前建议

当前最合理的下一步不再是继续补外部边界 checklist，也不再是继续组织首轮官方回归，而是维持既有窗口边界并转入稳定维护。

当前产出：

- 已新增 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist)
- 已新增 [身份语义 Phase 4 仓库外兼容边界首轮执行记录（仓库资产侧）](/guide/identity-claim-external-compat-first-pass)
- 已补齐当前生产环境事实：`1Panel` 默认 HTTPS 反代、`v26.3.2-release`、默认客户端种子、无仓库外脚本

后续执行顺序建议：

1. 继续维持 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window) 的当前边界，不在同一窗口提前删除 `UserClaimReader / CurrentUser` 的输入兼容。
2. 将重点切换到 `Phase 5` 防回归资产接入脚本 / 校验流程准备，避免历史双写或散点 Claim 解析重新回流。
3. 若后续新增新的部署环境、第三方客户端或自定义反代规则，再回到外部兼容边界清单追加评审。

### 5.2 若后续再出现阻塞

`M14` 当前仍保留为后续候选，但已不再是“因为外部边界未知而被迫切换”的默认去向。

只有在 Phase 4 稳定维护过程中发现新的阻塞事实，或主线优先级再次重排时，才再考虑切换到 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)。

## 6. 当前结论摘要

截至 `2026-04-04`，当前可以把最终判断明确写成一句话：

> **身份语义 Phase 4 当前已完成首轮实施与官方顺序真实回归；仓库内输入已齐、仓库外兼容边界已被事实关闭，且当前结论为“无需回滚，转入稳定维护”。**

因此，当前后续主线优先进入：

1. 维持 Phase 4 既有边界与稳定维护口径。
2. 推进 `Phase 5` 防回归治理准备。
