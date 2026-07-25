# M15 最小交付与部署基线

> 本页只定义 `M15` 当前主线的默认顺序。
>
> 关联入口：
>
> - [部署与容器指南](/deployment/guide)
> - [生产数据库迁移与发布编排](/guide/production-database-migration-deployment)
> - [M14 宿主运行首轮执行清单](/records/m14-host-runtime-checklist)
> - [验证基线说明](/guide/validation-baseline)
> - [产品版本与发布标识治理](/guide/version-governance)
> - [M15 测试环境最小回滚演练记录（2026-04-06）](/records/m15-test-rollback-rehearsal-2026-04-06)
> - [M15 生产环境最小回滚预案（2026-04-06）](/records/m15-prod-rollback-playbook-2026-04-06)
> - [M15 发布记录（v26.3.2-release，2026-04-06）](/records/m15-release-record-2026-04-06)

## 目标

把仓库里已经真实验证过的发版、部署、发布后最小复核与回滚动作，收成当前唯一默认顺序。

## 当前已确认事实

- 测试 / 生产部署态共用 `Deploy/docker-compose.yaml`；测试环境可以按测试策略选择固定 tag，生产环境必须使用与发布记录一致的不可变 `v*-release` tag
- 生产数据库迁移与应用发布统一由 `Deploy/deploy-production.sh` 编排，固定执行预检、停止写入、六库备份、`apply`、独立 `verify`、应用发布和外部健康检查
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
4. 等待 `Version Contract / Repo Hygiene / Frontend Lint / Baseline Quick / Dependency Security / Backend Guard / Identity Guard` 通过
5. 正式发布时，确保候选提交已包含带机器可读元数据且状态真实的发布记录
6. 合并到 `master`
7. 把最新 `origin/master` 回灌并推送到 `dev`，确认下一轮开发基于当前稳定主线
8. 在 tag 目标提交执行 `node Scripts/version-contract.mjs --tag <tag>`，通过后创建规范 tag
9. 等待 `Docker Images` 工作流再次校验并产出同 tag 镜像
10. 在部署环境用固定 `RADISH_IMAGE_TAG` 指向本次发布镜像
11. 部署后补充发布记录中的真实复核与回滚结论

## 最小部署顺序

### 测试部署

1. 进入 `Deploy/` 目录，复制 `.env.example` 为 `.env`
2. 设置 `RADISH_IMAGE_TRACK=test` 与测试域名 `RADISH_PUBLIC_URL`；如需完全可复现部署，再把 `RADISH_IMAGE_TAG` 固定到明确的 `v*-test` tag
3. 执行：

```bash
cd Deploy
docker compose config
docker compose pull
docker compose up -d
```

4. 按 `M14` 顺序做最小复核

### 生产部署

1. 进入 `Deploy/` 目录，复制 `.env.example` 为 `.env`
2. 设置 `RADISH_IMAGE_TRACK=release`、生产域名 `RADISH_PUBLIC_URL`，并把 `RADISH_IMAGE_TAG` 固定到与发布记录一致的 `v*-release` tag；设置 `.env` 权限为 `600`
3. 先执行只读预检：

```bash
./Deploy/deploy-production.sh --preflight-only
```

4. 进入获批维护窗口后执行：

```bash
./Deploy/deploy-production.sh --confirm-production
```

5. 按 `M14` 顺序做发布后复核，并把备份路径、migration 与健康检查结论写入发布记录

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

生产恢复必须先区分数据库 migration 是否已经开始：

1. migration 尚未开始：可恢复此前运行的应用服务，数据库无需变更。
2. migration 已开始但新 schema 与旧应用明确兼容：可评审固定回退到上一版已知可用 tag。
3. migration 已开始且 schema 不兼容：保持应用停止，保留失败现场，按同一批次的 globals 与六库备份整批恢复，再启动兼容版本。
4. 任一路径完成后重新执行严格 migration 校验和发布后复核。

详细停止线与恢复原则见[生产数据库迁移与发布编排](/guide/production-database-migration-deployment)。

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
