# M15 测试环境最小回滚演练记录（2026-04-06）

> 本记录用于沉淀 `M15` 第一批的首轮真实回滚演练结果。
>
> 关联入口：
>
> - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
> - [部署与容器指南](/deployment/guide)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)

## 演练背景

截至 `2026-04-06`，测试环境已知可用版本只有 `v26.3.2-test`。

当前已明确：

- `v26.3.1-test` 属于已知问题版本，存在登录阻塞，不应作为回滚目标
- 因此本轮不能伪装成“回滚到旧稳定版本”的演练
- 为了先验证回滚流程本身，当前采用“同一稳定代码重新打一个测试锚点 tag，再从该锚点回滚到现有稳定版本”的方式完成首轮闭环

本轮演练的性质是：

- 流程回滚演练
- 非跨功能版本回滚演练
- 目标是确认“切 tag -> 拉镜像 -> 更新容器 -> 复核链路 -> 再切回”的最小动作真实可执行

## 演练版本

- 演练前稳定版本：`v26.3.2-test`
- 本轮新增测试锚点：`v26.3.2-r1-test`
- 回滚目标版本：`v26.3.2-test`

## 演练顺序

### 1. 建立第二个已知可用锚点

基于当前稳定代码创建新的测试 tag：

```bash
git checkout master
git pull origin master
git tag -a v26.3.2-r1-test -m "Test rollback anchor v26.3.2-r1"
git push origin v26.3.2-r1-test
```

等待 `Docker Images` 工作流产出以下镜像：

- `ghcr.io/laugh0608/radish-dbmigrate:v26.3.2-r1-test`
- `ghcr.io/laugh0608/radish-frontend:v26.3.2-r1-test`
- `ghcr.io/laugh0608/radish-api:v26.3.2-r1-test`
- `ghcr.io/laugh0608/radish-auth:v26.3.2-r1-test`
- `ghcr.io/laugh0608/radish-gateway:v26.3.2-r1-test`

### 2. 切换到测试锚点版本并复核

将 `Deploy/.env.test` 中的 `RADISH_*_IMAGE` 统一切到 `v26.3.2-r1-test`，然后执行：

```bash
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml config
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
npm run collect:m14-host-record
```

### 3. 从测试锚点回滚到稳定版本并复核

将 `Deploy/.env.test` 中的 `RADISH_*_IMAGE` 统一改回 `v26.3.2-test`，然后执行：

```bash
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
npm run collect:m14-host-record
```

## 演练结果

本轮用户已确认：

- `v26.3.2-r1-test` 部署正常
- 从 `v26.3.2-r1-test` 回滚到 `v26.3.2-test` 正常
- 登录链路、基础访问与最小运行态检查当前无阻塞问题

当前据此可以确认：

- `M15` 第一批已经不只停留在文档收口，已完成首轮真实回滚流程验证
- 当前测试环境至少已经具备两个已知可用锚点：`v26.3.2-test` 与 `v26.3.2-r1-test`
- 以后如需继续做回滚演练，不必再依赖已知坏版本 `v26.3.1-test`

## 当前结论

截至 `2026-04-06`，`M15` 第一批可更新为：

- 最小发布顺序已明确
- 测试部署与生产部署顺序已明确
- 发布后最小复核顺序已明确
- 测试环境最小回滚流程已完成首轮真实演练

当前剩余边界仍然不变：

- 尚未完成“跨功能版本差异”的真实回滚演练
- 尚未做自动回滚
- 尚未扩展到蓝绿 / 金丝雀 / 多集群发布
- 尚未把 `M15` 扩写为独立部署平台专题
