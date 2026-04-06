# M15 最小交付与部署基线

> 本页只定义 `M15` 当前主线的默认顺序。
>
> 关联入口：
>
> - [部署与容器指南](/deployment/guide)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
> - [验证基线说明](/guide/validation-baseline)
> - [M15 测试环境最小回滚演练记录（2026-04-06）](/guide/m15-test-rollback-rehearsal-2026-04-06)
> - [M15 生产环境最小回滚预案（2026-04-06）](/guide/m15-prod-rollback-playbook-2026-04-06)
> - [M15 发布记录（v26.3.2-release，2026-04-06）](/guide/m15-release-record-2026-04-06)

## 目标

把仓库里已经真实验证过的发版、部署、发布后最小复核与回滚动作，收成当前唯一默认顺序。

## 当前已确认事实

- `base + test` 与 `base + prod` 已完成真实部署复核
- `M14` 的启动前、启动后与部署后最小复核已完成首轮真实闭环
- 测试环境已完成首轮真实最小回滚演练：`v26.3.2-r1-test -> v26.3.2-test`
- 生产环境当前已形成最小回滚预案，但尚未做真实回滚演练
- `v26.3.2-release` 的首份真实发布记录已落库

## 最小发布顺序

1. 在 `dev` 完成代码、文档与最小验证
2. 执行：

```bash
npm run validate:baseline:quick
```

3. 发起 `dev -> master` 的 PR
4. 等待 `Repo Hygiene / Frontend Lint / Baseline Quick / Identity Guard` 通过
5. 合并到 `master`
6. 创建规范 tag
7. 等待 `Docker Images` 工作流产出对应镜像
8. 在部署环境把镜像 tag 固定到本次发布版本
9. 补一份发布记录

## 最小部署顺序

### 测试部署

1. 复制 `Deploy/.env.test.example` 为 `Deploy/.env.test`
2. 将 `RADISH_*_IMAGE` 固定到明确的 `v*-test` tag
3. 执行：

```bash
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml config
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d
```

4. 按 `M14` 顺序做最小复核

### 生产部署

1. 复制 `Deploy/.env.prod.example` 为 `Deploy/.env.prod`
2. 将 `RADISH_*_IMAGE` 固定到明确的 `v*-release` tag
3. 执行：

```bash
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

4. 按 `M14` 顺序做最小复核

## 发布后最小复核

固定沿用 `M14` 的三层顺序：

1. 启动前前置验证

```bash
npm run validate:baseline:host -- --report-file .tmp/baseline-host-report.md
```

2. 启动后运行态检查

```bash
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
```

3. 汇总维护记录

```bash
npm run collect:m14-host-record
```

4. 如涉及测试部署 / 生产部署，再补部署后复核记录

## 最小留痕

一次发布后最少保留三类事实：

- 一份发布记录
- 一份部署后最小复核记录
- 一份明确的回滚事实或回滚预案说明

## 最小回滚基线

当前最小可行回滚不是自动切换整套环境，而是：

1. 找到上一版已知可用 tag
2. 将 `Deploy/.env.test` 或 `Deploy/.env.prod` 中的 `RADISH_*_IMAGE` 改回该 tag
3. 重新拉取并更新容器
4. 重新执行最小复核

### 当前边界

- 自动回滚脚本：不做
- workflow 改造：不做
- 蓝绿 / 金丝雀 / 多集群发布：不做
- `Gateway & BFF` 深化：不做

## 文档冻结说明

本页后续只在以下情况下更新：

- 新的真实发布 / 部署 / 回滚事实已经改变默认顺序
- `M15` 主线正式结束

普通功能开发、局部 bug 修复与非部署类联调，默认不改本页。
