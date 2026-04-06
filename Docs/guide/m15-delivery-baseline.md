# M15 最小交付与部署基线

> 本页用于把当前仓库已经具备的发布、部署、自检与回滚资产收束成单一入口。
>
> 关联入口：
>
> - [部署与容器指南](/deployment/guide)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
> - [验证基线说明](/guide/validation-baseline)
> - [当前进行中](/planning/current)
> - [M15 测试环境最小回滚演练记录（2026-04-06）](/guide/m15-test-rollback-rehearsal-2026-04-06)

## 目标

`M15` 当前不追求完整平台化交付，也不扩成更重的流水线或自动回滚系统。  
本阶段只做一件事：把仓库里已经真实验证过的发布、部署、自检与回滚动作收成可执行的最小基线。

当前要回答的问题是：

- 一次正式发布的最小顺序是什么
- 一次测试部署 / 生产部署的最小步骤是什么
- 发布后如何做最小复核
- 如果本次发布出现问题，最小可行回滚怎么做

## 当前仓库现实

截至当前，以下事实已经成立：

- `Repo Quality` 与 `Docker Images` 工作流已落地
- `v*-dev`、`v*-test`、`v*-release` 三条 tag 轨道已存在
- `radish-dbmigrate / radish-api / radish-auth / radish-gateway / radish-frontend` 已纳入统一 GHCR 口径
- `base + test` 与 `base + prod` 已完成真实部署复核
- `M14` 的启动前、启动后与部署后最小复核已完成首轮真实闭环
- 测试环境已完成一轮真实最小回滚演练：`v26.3.2-r1-test -> v26.3.2-test`

因此，`M15` 当前不再需要证明“能不能部署”，而是要把“如何稳定交付与如何回滚”写成默认动作。

## 适用范围

以下场景优先从本页开始：

- 准备从 `dev` 发起正式发布
- 需要把某个 `v*-test` 或 `v*-release` 部署到环境
- 发布后发现异常，需要退回上一版已知可用镜像
- 需要补发布 / 部署 / 回滚的最小运维留痕

## 最小发布顺序

1. 在 `dev` 完成代码、文档与最小验证
2. 至少执行：

```bash
npm run validate:baseline:quick
```

3. 发起 `dev -> master` 的 PR
4. 等待 `Repo Hygiene / Frontend Lint / Baseline Quick / Identity Guard` 通过
5. 合并到 `master`
6. 创建规范 tag：

```bash
git checkout master
git pull origin master
git tag -a v26.3.2-release -m "Release v26.3.2"
git push origin v26.3.2-release
```

7. 等待 `Docker Images` 工作流产出对应镜像
8. 在部署环境把镜像 tag 固定到本次发布版本，而不是依赖 `test-latest` / `latest`

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

推荐固定沿用 `M14` 的三层顺序：

1. 启动前前置验证：

```bash
npm run validate:baseline:host -- --report-file .tmp/baseline-host-report.md
```

2. 启动后运行态检查：

```bash
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
```

3. 汇总维护记录：

```bash
npm run collect:m14-host-record
```

4. 如涉及测试部署 / 生产部署，再补部署后复核记录：

- [M14 部署后最小复核记录模板](/guide/m14-deployment-review-record-template)

## 最小回滚基线

当前仓库的最小可行回滚，不是自动切换整套环境，而是：

1. 找到上一版已知可用 tag
2. 将 `Deploy/.env.test` 或 `Deploy/.env.prod` 中的 `RADISH_*_IMAGE` 改回该 tag
3. 重新拉取并更新容器
4. 重新执行最小复核

如果当前只有 1 个已知可用版本，不要强行回滚到已知坏版本。更稳妥的默认做法是：

1. 基于当前稳定代码补一个新的测试锚点 tag，例如 `v26.3.2-r1-test`
2. 先验证该锚点版本可正常部署
3. 再从该锚点回滚到原稳定版本，优先验证“回滚流程本身”是否闭环

首轮真实样例见：

- [M15 测试环境最小回滚演练记录（2026-04-06）](/guide/m15-test-rollback-rehearsal-2026-04-06)

### 测试部署回滚

1. 找到上一版已知可用 `v*-test`
2. 在 `Deploy/.env.test` 中把：
   - `RADISH_DBMIGRATE_IMAGE`
   - `RADISH_FRONTEND_IMAGE`
   - `RADISH_API_IMAGE`
   - `RADISH_AUTH_IMAGE`
   - `RADISH_GATEWAY_IMAGE`
   都切回上一版 tag
3. 执行：

```bash
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d
```

4. 重新执行：

```bash
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
npm run collect:m14-host-record
```

### 生产部署回滚

1. 找到上一版已知可用 `v*-release`
2. 在 `Deploy/.env.prod` 中把：
   - `RADISH_DBMIGRATE_IMAGE`
   - `RADISH_FRONTEND_IMAGE`
   - `RADISH_API_IMAGE`
   - `RADISH_AUTH_IMAGE`
   - `RADISH_GATEWAY_IMAGE`
   都切回上一版 tag
3. 执行：

```bash
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

4. 重新执行最小复核，并补一份部署后记录

## 回滚触发条件

当前建议至少在以下情况触发回滚判断：

- 登录 / 回调 / 登出主链路阻塞
- `Gateway / Api / Auth` 运行态健康检查失败且无法在短窗口内修复
- `RADISH_PUBLIC_URL`、Issuer、证书或反代头错误导致外部入口不可用
- 生产环境出现阻塞主线的 `P0 / P1` 问题

如果问题仅限于可快速修正的配置错误，且当前环境仍可控，可优先修配置后复核；不要机械地把所有问题都直接升级为回滚。

## 当前边界

`M15` 当前明确不做：

- 自动回滚脚本
- 蓝绿 / 金丝雀 / 多集群发布策略
- 独立部署平台或更重的发布编排系统
- 把 `M15` 扩成 `Gateway & BFF` 深化专题

## 当前建议

后续所有部署相关留痕，建议至少同时保留三类事实：

- 本次使用的明确镜像 tag
- 本次执行的最小复核记录
- 若触发回滚，本次回滚前后的版本差异与回滚后复核结果
