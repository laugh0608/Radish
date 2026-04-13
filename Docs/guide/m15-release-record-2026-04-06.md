# M15 发布记录（v26.3.2-release，2026-04-06）

> 本页用于沉淀 `M15` 当前首份真实发布记录。
>
> 关联入口：
>
> - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
> - [M15 发布记录模板](/guide/m15-release-record-template)
> - [M14 部署后最小复核记录（2026-04-06）](/guide/m14-deployment-review-record-2026-04-06)
> - [M15 测试环境最小回滚演练记录（2026-04-06）](/guide/m15-test-rollback-rehearsal-2026-04-06)
> - [M15 生产环境最小回滚预案（2026-04-06）](/guide/m15-prod-rollback-playbook-2026-04-06)

## 记录信息

- 记录日期：2026-04-06
- 记录人：项目协作记录
- 发布类型：正式发布

## 发布标识

- Git tag：`v26.3.2-release`
- 对应提交：`e6de7f825956481b830a5846405594e91415ce88`
- 来源分支：`master`
- 镜像 tag：
  - `radish-dbmigrate:v26.3.2-release`
  - `radish-frontend:v26.3.2-release`
  - `radish-api:v26.3.2-release`
  - `radish-auth:v26.3.2-release`
  - `radish-gateway:v26.3.2-release`

## 测试部署结论

- 部署情况：已部署
- 最小复核：通过
- 关联记录：[M14 部署后最小复核记录（2026-04-06）](/guide/m14-deployment-review-record-2026-04-06)
- 说明：
  - 当前发布批次对应的测试轨道稳定版本为 `v26.3.2-test`
  - `base + test` 本轮复核已通过，当前未见新的部署阻塞事实

## 生产部署结论

- 部署情况：已部署
- 最小复核：通过
- 关联记录：[M14 部署后最小复核记录（2026-04-06）](/guide/m14-deployment-review-record-2026-04-06)
- 说明：
  - 正式发布镜像已统一固定到 `v26.3.2-release`
  - `base + prod` 本轮复核已通过，当前未见新的部署阻塞事实

## 回滚目标

- 测试环境优先回滚目标：`v26.3.2-test`
- 生产环境优先回滚目标：无
- 说明：
  - 测试环境当前已有已知可用稳定锚点 `v26.3.2-test`，并已完成 `v26.3.2-r1-test -> v26.3.2-test` 的最小回滚演练
  - 生产环境当前仍只有 1 个已知可用 `v*-release` 锚点，即 `v26.3.2-release`；因此本轮不伪造“上一版可回滚目标”，继续以生产回滚预案作为默认边界

## 已知风险 / 后置项

- 生产环境当前尚未形成第二个已知可用 `v*-release` 锚点，因此仍未具备可直接指向的真实生产回滚目标
- 本记录只负责沉淀本次发布的最小事实，不替代 `M14` 部署后最小复核记录，也不替代后续真实回滚记录
