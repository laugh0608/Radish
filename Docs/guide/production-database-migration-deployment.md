# 生产数据库迁移与发布编排

> 本页定义 Radish 生产环境数据库结构变更的固定发布入口、执行顺序、失败停止线与恢复边界。

## 当前结论

- 生产发布统一使用仓库根目录下的 `Deploy/deploy-production.sh`；直接执行 `docker compose up -d` 不再是生产发布默认入口。
- 生产镜像必须固定为与仓库发布记录一致的不可变 `v*-release` tag，禁止使用 `release-latest` 或 `latest`。
- 表、字段、索引、约束和必要的数据回填必须先进入有序 Radish migration；生产环境由一次性 `Radish.DbMigrate apply` 任务执行，不允许 Api、Auth 或 Gateway 启动时自行迁移。
- 每个发布批次即使没有 pending migration，也会显式执行一次 `apply` 和独立 `verify`，以确认数据库 ledger、结构和数据语义与发布镜像一致。
- 迁移开始前必须停止应用写入并备份六个 PostgreSQL 数据库；迁移或严格校验失败后保持应用停止，由维护者明确决定前滚修复或整批恢复。

## 使用入口

首次准备部署目录：

```bash
cp Deploy/.env.example Deploy/.env
chmod 600 Deploy/.env
mkdir -p DeployData/Postgres DeployData/Redis DeployData/AuthCerts
mkdir -p DeployBackups DataBases Logs
```

至少确认以下生产值：

- `RADISH_IMAGE_TRACK=release`
- `RADISH_IMAGE_TAG=<明确的 v*-release tag>`
- `RADISH_DEPLOYMENT_STAGE=production`
- `RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED=false`
- `RADISH_PUBLIC_URL=<真实外部 HTTPS origin>`
- PostgreSQL、Redis 与 Auth 证书凭据
- 六个 PostgreSQL 数据库名
- `RADISH_BACKUP_PATH=<专用备份目录>`

先执行只读预检：

```bash
./Deploy/deploy-production.sh --preflight-only
```

预检通过并进入获批的维护窗口后，执行完整发布：

```bash
./Deploy/deploy-production.sh --confirm-production
```

完整发布会改变生产容器与数据库状态；执行前仍须按照发布流程确认版本、备份空间、维护窗口和恢复负责人。

## 固定执行顺序

脚本按以下顺序执行：

1. 校验 `.env` 权限、生产阶段、开发种子开关、外部 HTTPS 地址、数据库标识符和备份目录。
2. 校验 `RADISH_IMAGE_TAG` 为不可变 release tag，并确认 `Docs/records/` 中存在同 tag 的发布记录。
3. 解析 Compose 配置，确认 DbMigrate、Frontend、Api、Auth、Gateway 五个镜像全部精确落到该 tag。
4. 在维护窗口前拉取五个固定镜像，避免应用停止后才等待大体积下载。
5. 确认 PostgreSQL 与 Redis 已启动并通过 Compose 健康检查。
6. 记录此前正在运行的应用服务，然后停止 Gateway、Api、Auth、Frontend，阻断跨库业务写入。
7. 备份 PostgreSQL globals，并依次备份 Main、Log、Message、Chat、OpenIddict、Hangfire 六库。
8. 检查每份备份非空，以 `pg_restore --list` 验证 custom dump 可解析，并写入 `SHA256SUMS` 和批次元数据。
9. 以一次性容器显式执行 `Radish.DbMigrate apply`。
10. 以另一份一次性容器独立执行 `Radish.DbMigrate verify`。
11. 重建并启动 Frontend、Api、Auth，确认运行后再重建 Gateway。
12. 访问 `${RADISH_PUBLIC_URL}/health`，成功后写入发布成功标记。

备份目录格式为：

```text
<RADISH_BACKUP_PATH>/<UTC timestamp>-<release tag>/
```

其中包含 globals、六库 dump、校验和、元数据以及成功或失败标记。脚本不会自动清理历史备份。

## Schema 变更如何进入发布

增加表、字段、索引或约束时，开发阶段必须完成：

1. 修改实体或 OpenIddict EF 模型。
2. 新增稳定且顺序递增的 migration ID，定义前置条件、Apply、严格 Verify 和 checksum source。
3. 对 SQLite 与 PostgreSQL 同时验证旧基线升级、重复 apply、严格 verify 和异常历史数据拒绝。
4. 对新增非空字段采用可迁移顺序：先增加可空或安全默认结构，完成数据回填与验证，再在后续 migration 收紧约束。
5. 对删除、改名、类型收窄等破坏性变更采用 expand / migrate / contract 分批演进，先保证新旧应用版本的兼容窗口，再移除旧结构。
6. 在发布候选记录中写明 migration 范围、数据库兼容性与恢复策略。

生产机器不生成临时差异 SQL，也不根据实体直接 Code First 修补既有库。发布脚本只执行随固定 DbMigrate 镜像交付并已进入 ledger 的 migration。

## 失败行为

| 失败阶段 | 数据库状态 | 应用处理 |
| --- | --- | --- |
| 预检、拉取镜像或基础设施健康检查失败 | 未进入备份和迁移 | 不停止现有应用 |
| 停止应用后、备份完成前失败 | migration 尚未开始 | 重新启动发布前正在运行的应用服务 |
| `apply` 或独立 `verify` 失败 | migration 已开始或状态需人工确认 | 保持应用停止，保留备份与失败标记 |
| 新应用启动或外部健康检查失败 | 数据库已通过本次 migration verify | 不自动切回旧镜像，由维护者判断兼容性与故障原因 |

脚本不会吞掉迁移错误，也不会在迁移开始后自动恢复数据库。自动恢复属于破坏性动作，而且六库、应用版本与失败现场必须作为同一批次判断，因此保留明确的人工授权边界。

## 恢复原则

- 禁止手工修改 `RadishSchemaVersion` ledger 或已发布 migration checksum。
- migration 尚未开始时，可恢复此前运行的应用版本，不需要改动数据库。
- migration 已开始后，优先评估补充更高 migration ID 的前滚修复；不得原地改写已经发布的 migration。
- 只有确认旧应用与新 schema 兼容时，才允许仅回退应用镜像。
- 若必须恢复数据库，应先保留失败现场，再使用同一发布批次的 globals 与六库备份整批恢复；不能只恢复其中一个业务库。
- 恢复动作应先在隔离环境校验 `SHA256SUMS`、dump 可解析性、恢复顺序和目标版本 `verify`，再在获批维护窗口执行。
- PostgreSQL 六库 dump 不是 Redis、附件目录、OIDC 证书和外部对象存储的完整灾备方案；这些资产继续按各自持久化与灾备策略管理。

当前脚本有意不提供自动删除数据库、自动覆盖恢复或自动回退镜像的参数。需要正式恢复时，应根据具体版本和失败阶段制定批次级执行记录并获得明确授权。

## 验证与维护

代码侧合约入口：

```bash
npm run check:production-deploy
```

该测试使用隔离的假 Docker / curl 驱动，验证固定 tag、备份顺序、重复发布仍执行 migration、迁移失败阻断发布和备份失败恢复旧应用等契约，不连接真实 Docker 或数据库。

生产发布前还应按照风险执行仓库门禁和发布记录复核；真实执行后的镜像、迁移、健康检查、备份路径与恢复判断写入对应发布记录。

## 相关入口

- [数据库结构变更协作口径](/guide/database-schema-change-governance)
- [M15 交付与部署基线](/guide/m15-delivery-baseline)
- [部署与容器指南](/deployment/guide)
- [产品版本与发布标识治理](/guide/version-governance)
