# 部署与容器指南

## 目标
本指南面向需要在本地或服务器上快速部署 Radish 的维护者，说明如何使用 DbMigrate、API、Auth、Gateway 与 Frontend 五个镜像入口，并通过 `Deploy/docker-compose.local.yaml` 或 `Deploy/docker-compose.yaml` 组织应用服务及 PostgreSQL / Redis。当前部署口径已经收束为“开发环境直接 IDE / 宿主机运行，本地容器验证保留容器内 HTTPS，测试与生产环境统一采用外部反代 HTTPS、容器内 HTTP，并默认启用 PostgreSQL / Redis”。

当前若要按仓库现实执行发版、部署、发布后最小复核与回滚，请优先参考：

- [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
- [生产数据库迁移与发布编排](/guide/production-database-migration-deployment)
- [M15 v26.7.1.1204-release 生产部署记录](/records/m15-release-record-v26.7.1.1204-2026-07-12)
- [M15 发布记录（v26.3.2-release，2026-04-06）](/records/m15-release-record-2026-04-06)
- [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)
- [M14 宿主运行首轮执行清单](/records/m14-host-runtime-checklist)

其中本页继续保留环境口径、Compose 细节与部署注意事项；最小发布顺序、最小复核顺序与最小回滚基线统一收口到 `M15` 单一入口。

## 环境口径

当前仓库把部署与运行形态收束为四类：

- **开发运行**
  - 默认直接使用 IDE、`dotnet run` 与前端开发服务器
  - 继续使用仓库内置开发证书与 `localhost` 回调地址
  - 目标是调试效率，不要求与线上部署形态完全一致
- **本地容器验证**
  - 仅用于可选的本地容器构建与启动验证
  - 使用 `Deploy/docker-compose.local.yaml`
  - 允许在本机执行 `build`，但不作为日常开发启动方式
- **测试部署**
  - 面向企业内部环境或外部客户试用
  - 直接拉取 `GHCR` 中已构建好的镜像，不在部署机现场构建
  - 与生产部署保持同构：外部 `Nginx / Traefik / Caddy` 终止 TLS，容器内部 `Gateway / Api / Auth` 只提供 `HTTP`
  - 默认启用 PostgreSQL / Redis，测试与生产的主要差异应收敛为镜像 tag、域名、证书路径、密钥和数据
  - `Auth` OIDC 签名 / 加密证书可在首次启动时自动生成并持久化；后续必须复用同一组证书
- **生产部署**
  - 面向正式外部客户
  - 直接拉取 `GHCR` 中已构建好的发布镜像，不在部署机现场构建
  - 外部 `Nginx / Traefik / Caddy` 终止 TLS 并提供 `HTTPS`
  - 容器内部 `Gateway / Api / Auth` 只提供 `HTTP`
  - 默认启用 PostgreSQL / Redis
  - `Auth` OIDC 证书可在首次启动时自动生成并写入挂载目录；后续必须复用同一组证书

## 更换域名时的处理边界

### 不再需要手工修改的数据库数据

采用 `AttachmentId / attachment://` 的新写入与已迁移数据已经完成“去 URL 真值化”收口。更换域名时，不再需要手工修改以下字段：

- `Attachment` 记录
- `Sticker.AttachmentId`、`StickerGroup.CoverAttachmentId`
- `Reaction.StickerAttachmentId`
- `ChannelMessage.AttachmentId`
- `Product.IconAttachmentId`、`Product.CoverAttachmentId`
- `ProductCategory.IconAttachmentId`
- `Order.ProductIconAttachmentId`
- 正文中的 `attachment://{id}` 引用

例外：历史 Markdown / 富文本、Wiki revision、`Site.Branding.FaviconUrl` 或外部缓存若仍保存 `/uploads/**` 或旧域名绝对 URL，必须在关闭兼容静态入口或切换域名前完成盘点、迁移与抽样验证。

原因很简单：

- 附件实体当前只存 `StoragePath` / `ThumbnailPath` 等存储信息；
- 业务实体统一存 `AttachmentId`；
- `voUrl` / `voThumbnailUrl` 等展示字段全部在运行时派生。

### 更换域名时真正需要调整的内容

- `RADISH_PUBLIC_URL`
- 外部反向代理的域名、证书与回源配置
- OIDC `Issuer`、客户端回调地址和登录登出入口
- 前端运行时公开入口配置
- 如果外部系统缓存了旧域名下的临时访问链接 `accessUrl`，需要重新分发新链接

### 资源访问路径要求

- 业务侧统一推荐通过 `/_assets/attachments/{id}` 与 `/_assets/attachments/{id}/thumbnail` 暴露媒体资源。
- `Radish.Gateway` 当前已内建 `/_assets/attachments/** -> Radish.Api` 转发。
- 如果系统通过外层反代统一对外，仍需确保该路径被放行到 Gateway，而不是被静态站点或默认首页兜底吞掉。
- 用户上传根目录不得通过 `/uploads/**` 直接暴露；当前只保留 `/uploads/DefaultIco/**` 版本内置可信图标。业务附件必须经过 `/_assets/attachments/**` 的状态与访问权限判定。

## 仓库发版与合并流程

`master` 只通过 Pull Request 合并，禁止直接 push、force push 与删除；日常开发和必要验证在 `dev` 或功能分支完成。PR 合并后必须先把最新 `origin/master` 回灌到 `dev`，再开始下一轮开发。

正式发布还必须满足：

- 按 [M15 交付与部署基线](/guide/m15-delivery-baseline)完成本地与 CI 门禁。
- 按[产品版本与发布标识治理](/guide/version-governance)校验机器可读发布记录、候选提交和 tag。
- 等待 `Docker Images` 为 DbMigrate、Api、Auth、Gateway、Frontend 产出同一固定 tag 的镜像。
- 生产部署只消费该固定 tag，不以 `release-latest` 或 `latest` 作为可追溯版本。

发布记录入口见[记录与验收索引](/records/)。

## 先决条件
- Docker Engine ≥ 24，能够拉取 `mcr.microsoft.com/dotnet/*` 官方镜像。
- 若 `GHCR` 包为私有，还需要准备 `docker login ghcr.io` 所需凭据。
- .NET SDK 10.0.0+，用于调试或本地 `dotnet publish`。
- Node.js 24+：可选，用于宿主机本地构建前端；`Frontend/Dockerfile` 当前直接基于 Node 24 多阶段镜像完成构建与运行时封装。
- PostgreSQL / Redis：测试与生产 Compose 默认会启动 PostgreSQL 16 与 Redis 7，并通过环境变量覆盖 `Databases` 与 `Redis` 共享配置；本地容器验证仍可继续使用 SQLite + 内存缓存。
- **Auth 证书**：准备好 OIDC 签名/加密证书（`.pfx` 文件），或至少预留一个可持久化写入的挂载目录供容器在首次启动时自动生成证书；默认的 `Certs/dev-auth-cert.pfx` 仅用于本地联调，生产必须替换。

## 当前仓库资产

- 后端镜像：
  - `Radish.DbMigrate/Dockerfile`
  - `Radish.Api/Dockerfile`
  - `Radish.Auth/Dockerfile`
  - `Radish.Gateway/Dockerfile`
- 前端镜像：
  - `Frontend/Dockerfile`
  - `Frontend/scripts/serve-static.mjs`
- 最小编排：
  - `Deploy/docker-compose.yaml`
  - `Deploy/docker-compose.local.yaml`
- 生产交付样例：
  - `Deploy/.env.example`
  - `Deploy/nginx.prod.conf`
- CI / 镜像工作流：
  - `.github/workflows/repo-quality.yml`
  - `.github/workflows/docker-images.yml`
- 容器证书入口脚本：
  - `Scripts/docker/auth-entrypoint.sh`
  - `Scripts/docker/gateway-entrypoint.sh`
  - `Scripts/docker/cert-utils.sh`

## GHCR 镜像工作流

当前仓库已补 `Docker Images` 工作流，默认采用 `GHCR` 作为统一镜像仓库，口径如下：

- `Repo Quality`：仅在 `pull_request -> master / dev` 与 `workflow_dispatch` 触发，不再因普通 `dev` push 消耗资源
- `push -> v*-dev`：构建五个镜像入口，并把 `radish-dbmigrate`、`radish-api`、`radish-auth`、`radish-gateway`、`radish-frontend` 以 `<tag>` + `dev-latest` 推送到 `GHCR`
- `push -> v*-test`：构建五个镜像入口，并把 `radish-dbmigrate`、`radish-api`、`radish-auth`、`radish-gateway`、`radish-frontend` 以 `<tag>` + `test-latest` 推送到 `GHCR`
- `push -> v*-release`：构建五个镜像入口，并把 `radish-dbmigrate`、`radish-api`、`radish-auth`、`radish-gateway`、`radish-frontend` 以 `<tag>` + `release-latest` + `latest` 推送到 `GHCR`
- `workflow_dispatch`：可手动补跑；仅当当前 ref 为 `v*-dev`、`v*-test` 或 `v*-release` tag，且显式启用 `push_backend`、`push_frontend` 对应开关时，才会推送对应镜像

当前镜像命名约定如下：

- `ghcr.io/<owner>/radish-dbmigrate`
- `ghcr.io/<owner>/radish-api`
- `ghcr.io/<owner>/radish-auth`
- `ghcr.io/<owner>/radish-gateway`
- `ghcr.io/<owner>/radish-frontend`

截至 `2026-07-12`，`v26.7.1.1204-release` 已完成五个正式镜像的构建、扫描、推送、固定 tag 拉取和生产部署；`radish-dbmigrate` 已在生产完成 PostgreSQL / OpenIddict 前滚，应用服务随后正常启动。

当前 tag 规则如下：

- `v*-dev`：`<tag>`、`dev-latest`
- `v*-test`：`<tag>`、`test-latest`
- `v*-release`：`<tag>`、`release-latest`、`latest`

其中 `dev-latest`、`test-latest`、`release-latest`、`latest` 都只是对应轨道的浮动别名；需要完全可复现的部署时，建议用固定版本 tag 覆盖浮动别名。

当前 `frontend` 已接入统一 GHCR 推送规则；之所以可以直接纳入，是因为：

- `Frontend/scripts/serve-static.mjs` 已支持按请求返回 `/runtime-config.js` 运行时配置脚本
- `radish.client` 与 `radish.console` 当前都会优先读取运行时注入的公开地址与功能开关，再回退到构建期 `VITE_*`
- `Deploy/docker-compose.local.yaml` 与部署态 `Deploy/docker-compose.yaml` 也已为 `frontend` 容器补齐运行时环境变量入口

因此当前 `frontend` 已具备“同一个镜像按运行时环境复用”的能力；当前工作流已补齐统一推送规则，且 `frontend` GHCR 首次真实产物已可通过 `docker pull` 获取，当前剩余重点转为上线前外部交付复核。

## 构建服务镜像
以下内容主要用于 `CI`、本地容器验证或手动验证镜像入口。测试部署与生产部署默认应直接拉取 `GHCR` 里的预构建镜像，而不是在部署机执行 `docker build` 或 `docker compose build`。

当前仓库已提供首版最小镜像链，对应五个构建入口：

- `Radish.DbMigrate/Dockerfile`：发布数据库初始化入口，默认用于容器部署时的 `apply` 一次性任务，负责按需 `init + seed` 共享业务库。
- `Radish.Api/Dockerfile`：发布 API，并把仓库 `Docs/` 一并带入镜像，确保固定文档能力在容器内可用。
- `Radish.Auth/Dockerfile`：发布 OIDC 服务，并把仓库 `Certs/` 带入镜像，便于本地 / 内部开发版使用默认开发证书。
- `Radish.Gateway/Dockerfile`：发布网关服务，作为默认对外入口。
- `Frontend/Dockerfile`：使用 `node:24-bookworm-slim` 构建 client / console，以 `node:24-alpine` 作为最终运行层，只保留静态产物与内置静态服务器，并移除运行时不需要的 npm / npx / Corepack / Yarn 入口，统一托管 `/` 与 `/console/`。

常用单镜像构建命令如下：

```bash
docker build \
  -f Radish.DbMigrate/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Release \
  -t radish/dbmigrate:local .

docker build \
  -f Radish.Api/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Release \
  -t radish/api:local .

docker build \
  -f Radish.Auth/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Release \
  -t radish/auth:local .

docker build \
  -f Radish.Gateway/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Release \
  -t radish/gateway:local .

docker build \
  -f Frontend/Dockerfile \
  -t radish/frontend:local .
```

当前前端镜像不再依赖构建期 `VITE_*` 参数；运行时会通过容器环境变量按请求返回配置脚本：

- 静态服务会在请求 `/runtime-config.js` 时动态返回运行时配置
- 运行时默认优先读取 `RADISH_PUBLIC_URL`
- 部署态默认只注入 `RADISH_PUBLIC_URL`，前端运行时配置统一回退到同一个 Gateway 公开入口
- 静态服务会拒绝 `//`、非法路径编码和 absolute-form 等异常请求目标并返回 `400`；拒绝日志只记录截断、转义且移除查询参数后的路径，单个异常请求不得结束 Node 进程

> 说明：当前 `frontend` 镜像已纳入统一 GHCR 推送链路，并完全切到运行时配置注入。最终层不承担依赖安装或现场构建职责；基础镜像、系统包或 Node 运行层发生变化时，必须重新执行最终镜像 High / Critical 扫描，不能只依据构建层审计结果。

## 运行容器
当前最小链默认以 `Gateway` 作为唯一对外入口。Compose 现在收束为两类：

- `Deploy/docker-compose.yaml`：测试 / 生产共用的部署态编排，直接拉取远程镜像，默认启用 PostgreSQL / Redis，`Gateway` 容器内监听 HTTP，外部反代终止 HTTPS。
- `Deploy/docker-compose.local.yaml`：本地容器验证编排，保留本地 `build` 入口，默认使用 SQLite + 内存缓存，并让 `Gateway` 在容器内监听 HTTPS。

开发运行不走 Compose；Compose 只建议按以下两种组合使用：

- 本地容器验证：`Deploy/docker-compose.local.yaml`
- 测试 / 生产部署：进入 `Deploy/` 目录后使用默认 `docker-compose.yaml + .env`

### 开发运行：IDE / 宿主机直跑

日常开发默认直接使用 IDE、`dotnet run`、`npm run dev` 与启动脚本，不使用 Compose。

### 本地容器验证

```bash
docker compose -f Deploy/docker-compose.local.yaml build
docker compose -f Deploy/docker-compose.local.yaml up -d
```

本地容器验证口径如下：

- `dbmigrate`：先执行一次 `apply`，自动补齐共享业务库表结构与基础数据
- `RadishDeployment__Stage=local` 与 `Seed__DeveloperDefaultsEnabled=true`：允许创建 `system / admin / test` 开发演示账号、默认密码、默认头像与用户角色绑定，仅用于本地容器验证
- `gateway`：对外监听 `https://localhost:5000`
- `api`：容器内监听 `5100`
- `auth`：容器内监听 `5200`
- `frontend`：容器内监听 `80`，由 `Gateway` 反向代理 `/` 与 `/console/`

本地容器验证时，`Gateway` 镜像会内置 `Certs/dev-gateway-cert.pfx` 作为开发证书，使 `https://localhost:5000` 可以直接完成 TLS 握手；若浏览器提示证书不受信任，请先在宿主机信任该开发证书。

### 测试 / 生产部署

复制 `.env.example` 为 `.env`，设置 `600` 权限，并按真实环境替换镜像、外部 HTTPS 地址、PostgreSQL、Redis、持久化目录与 Auth 证书配置：

```bash
git clone https://github.com/laugh0608/Radish.git
cd Radish
mkdir -p DeployData/Postgres DeployData/Redis DeployData/AuthCerts
mkdir -p DeployBackups DataBases Logs
cp Deploy/.env.example Deploy/.env
chmod 600 Deploy/.env
```

测试环境使用 `RADISH_IMAGE_TRACK=test`，可按测试策略选择固定 `v*-test` tag；生产环境必须使用 `release` track、与发布记录一致的不可变 `v*-release` tag、`production` stage、关闭开发种子，并设置专用 `RADISH_BACKUP_PATH`。

```bash
# 测试环境
cd Deploy
docker compose config
docker compose pull
docker compose up -d

# 生产环境（回到仓库根目录）
cd ..
./Deploy/deploy-production.sh --preflight-only
./Deploy/deploy-production.sh --confirm-production
```

部署态由环境变量把 Main、Log、Message、Chat、OpenIddict 与 Hangfire 指向 PostgreSQL，并启用 Redis；公开地址、Gateway、Issuer、CORS 与 OIDC 回调统一服从 `RADISH_PUBLIC_URL`。TLS 在外部反向代理终止，容器内保持 HTTP。持久化目录、证书生成和生产 migration 的完整契约分别见[配置管理](/guide/configuration)、[OpenIddict 数据库与迁移](/guide/authentication-openiddict-database)和[生产数据库迁移与发布编排](/guide/production-database-migration-deployment)。

### 最小回滚入口

测试环境可按 [M15 最小交付与部署基线](/guide/m15-delivery-baseline) 切回已知可用 tag。生产环境必须先判断 migration 是否开始以及新旧 schema 是否兼容，不能默认只回退镜像；停止线、整批六库恢复和人工授权边界见[生产数据库迁移与发布编排](/guide/production-database-migration-deployment)。

**附件与分片部署边界**：

- `${FileStorage:Local:BasePath}`（默认 `DataBases/Uploads`）与 `ChunkedUpload:TempChunkPath`（默认 `DataBases/Temp/Chunks`）都必须位于持久化挂载内；Gateway 和外层反向代理只放行 `/_assets/attachments/**` 与可信 `DefaultIco` 路由，不映射整个 `/uploads/**`。
- 当前会话互斥使用进程内 `AsyncKeyedLock`，启用分片上传的 `Radish.Api` 必须保持单实例；扩容前需同时具备共享临时存储、按会话分布式锁和故障切换验收，粘性会话不能作为一致性保证。细节见[文件上传与附件管理](/features/file-upload-design)。
- 合并到 `master` 前至少盘点历史 `/uploads/**` 直链；阶段验收取得启动授权后，再通过真实 Gateway 验证用户上传静态路径不可达、`DefaultIco` 可达及受控附件状态 / 私有访问规则。

## Docker Compose 示例
仓库根目录已提供真实可用的基础编排与环境覆盖文件，推荐按环境组合使用：

```bash
docker compose -f Deploy/docker-compose.local.yaml config
docker compose -f Deploy/docker-compose.local.yaml build
docker compose -f Deploy/docker-compose.local.yaml up -d

cd Deploy
docker compose config
docker compose pull
docker compose up -d
```

当前 Compose 口径如下：

- 本地容器验证默认使用仓库共享配置中的 SQLite 与内存缓存；测试与生产默认启用 PostgreSQL / Redis。
- 本地容器验证和测试 Compose 会在依赖健康后执行 `dbmigrate apply`，再启动 `api / auth / gateway`；生产环境改由固定脚本显式执行备份、`apply`、独立 `verify` 和应用发布。
- `DataBases/` 与 `Logs/` 会挂载到宿主机，便于保留本地 SQLite、上传文件与运行日志。
- `Frontend` 运行的是预构建镜像；该镜像在 `CI` 中统一构建，并同时托管 `radish.client` 与 `radish.console`。
- 部署态 `frontend / api / auth / gateway` 统一使用 `restart: unless-stopped`，用于进程异常退出后的自动恢复；一次性 `dbmigrate` 不配置重启策略，人工 `docker compose stop` 仍保持停止语义。
- `Gateway` 会通过环境变量把 `/` 与 `/console/` 反代到前端容器，并把 `/api`、`/connect`、`/Account` 等路径转发给对应后端服务。
- `GatewayRuntime__EnableHttpsRedirection` 当前已作为运行时开关显式暴露，可与 `ASPNETCORE_URLS` 一起切换容器内部的 HTTP / HTTPS 监听模式。
- 开发运行默认使用 IDE / 宿主机直跑，不走 Compose。
- 本地容器验证默认保持项目既有口径：`Gateway` 作为唯一对外 HTTPS 入口，访问地址为 `https://localhost:5000`。
- 测试部署与生产部署默认保持同构：外层 HTTPS、内层 HTTP、PostgreSQL、Redis、持久化 Auth OIDC 证书。
- 测试部署与生产部署默认直接拉取 `GHCR` 预构建镜像，不在部署机执行 `build`。
- 部署态 PostgreSQL 覆盖范围包含 SqlSugar 管辖的 `Main / Log / Message / Chat`、OpenIddict EF Core 存储与 Hangfire 存储；本地容器验证继续默认使用 SQLite provider。

## 数据库初始化与迁移（Radish.DbMigrate）

`Radish.DbMigrate` 是业务库与 OpenIddict schema 的唯一写入入口。`doctor` 和 `verify` 只读；`apply` 对空库执行初始化，对已有库按 ledger 顺序前滚 migration，并执行安全的幂等 seed 和严格校验。

本地或隔离测试环境：

```bash
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- verify
```

本地需要单独检查结构初始化时可以显式使用 `init`；生产环境不单独调用 `init / seed`，只通过发布脚本执行 `apply` 与独立 `verify`：

```bash
./Deploy/deploy-production.sh --preflight-only
./Deploy/deploy-production.sh --confirm-production
```

新增表、字段、索引、约束与数据回填必须进入有序 migration；开发默认账号只允许在 `local/test` 且显式开启开发种子时创建。完整模型、ledger、双 provider 验证与恢复规则见[数据库结构变更协作口径](/guide/database-schema-change-governance)和[生产数据库迁移与发布编排](/guide/production-database-migration-deployment)。

## 反向代理配置

### 最佳实践：TLS 终止在反向代理层

生产环境推荐使用反向代理（Nginx/Traefik/Caddy）处理 HTTPS，后端服务监听 HTTP 端口。

#### 架构流程

```
用户浏览器 → HTTPS (443)
    ↓
Nginx/Traefik (TLS 终止)
    ↓
HTTP (5000/5100) → ASP.NET Core 应用
```

#### 优势

1. **TLS 终止发生在反向代理层** - 证书只需在 Nginx/Traefik 配置一次，简化证书管理和续期
2. **性能更好** - 避免双重 TLS 握手和加密/解密开销，内网通信不需要加密（可信网络）
3. **配置更简单** - 后端应用不需要配置证书，Kestrel 只监听 HTTP

#### 当前仓库交付文件

- `Deploy/nginx.prod.conf`：当前仓库随代码交付的生产反向代理样例，默认采用“宿主机 Nginx 终止 HTTPS，再回源 `127.0.0.1:5000`”的口径。
- `Deploy/.env.example`：当前部署态组合的最小变量样例，至少覆盖 `RADISH_IMAGE_TRACK`、`RADISH_PUBLIC_URL`、PostgreSQL / Redis 密码，以及 Auth 证书密码；如需固定版本再启用 `RADISH_IMAGE_TAG`。
- `Radish.Gateway/Program.cs`：当前已经显式启用 `X-Forwarded-For / X-Forwarded-Proto / X-Forwarded-Host` 识别，能够正确处理反代后的 Scheme、Host 与重定向。
- `Radish.Auth`：OpenIddict transport security 在非 Development 环境不允许关闭；Gateway 的 `/Account` 与 `/connect` 路由会显式传递 `X-Forwarded-Proto`，Auth 仅处理最近一跳。部署编排必须继续只对 Auth 使用 `expose: 5200`，不得添加宿主 `ports`。

#### Nginx 落地步骤

1. 复制 `Deploy/nginx.prod.conf` 到服务器的 Nginx 主配置或站点配置位置。
2. 把 `server_name` 改成真实域名，把 `ssl_certificate` / `ssl_certificate_key` 改成正式证书路径。
3. 保持 `proxy_pass http://127.0.0.1:5000;`，即可覆盖 `/`、`/console/`、`/api`、`/connect`、`/Account`、`/health` 等当前 Gateway 入口。
4. 若 Nginx 不是宿主机部署，而是作为同一 Docker 网络内的单独容器运行，请把 upstream 从 `127.0.0.1:5000` 改为 `gateway:5000`。
5. 校验并重载 Nginx：

   ```bash
   nginx -t
   systemctl reload nginx
   ```

### 注意事项

- **本地容器验证**：`Deploy/docker-compose.local.yaml` 当前直接让 Gateway 在容器内终止 TLS，并只对宿主机暴露 `https://localhost:5000`
- **测试 / 生产部署**：进入 `Deploy/` 目录后，`docker compose` 会自动读取 `docker-compose.yaml` 与 `.env`，由 `.env` 注入真实域名、镜像 tag 与密钥，再由外层 Nginx 终止 HTTPS
- **生产环境**：Gateway / Api / Auth 当前仍默认走 HTTP 容器内通信，TLS 只在反向代理层终止
- **内网可信场景**：反代到 HTTP 端口是安全的
- **零信任架构**：如需端到端加密，可配置反代到 HTTPS，但需要额外证书管理

### OIDC 证书滚动更新流程

1. **准备新证书**：参考《[鉴权与授权指南](/guide/authentication)》的“证书生成示例”生成新 `.pfx`（签名/加密可拆分）。
2. **上传/挂载**：将新证书放到宿主机（如 `/etc/radish/certs/auth-signing-2025Q1.pfx`），并映射到容器的 `/app/certs`。
3. **更新部署变量**：修改 `Deploy/.env` 中的 `RADISH_AUTH_SIGNING_CERT_PASSWORD / RADISH_AUTH_ENCRYPTION_CERT_PASSWORD`；如需替换已有证书文件，应先在 `RADISH_AUTH_CERTS_PATH` 指向的宿主目录内完成证书轮换。
   ```bash
   cd Deploy
   docker compose up -d auth
   ```
   Kubernetes 集群可在 Helm values 中覆盖对应 `env`，并执行 `helm upgrade`。
4. **分批重启**：
   - Compose：进入 `Deploy/` 后执行 `docker compose up -d auth`
   - Kubernetes：`kubectl rollout restart deploy/radish-auth`
5. **验证**：
   ```bash
   curl https://radish.com/.well-known/jwks | jq '.keys[].kid'
   ```
   确认 `kid` 已切换为新证书，再用新 Token 访问 `https://radish.com/api/...` 验证签名。
6. **清理旧证书**：确保所有客户端已换取新 Token 后，删除旧 `.pfx` 并吊销旧密码。

> 建议记录证书轮换日期，并在运维手册中说明下一次到期时间，以避免证书过期导致 Auth 下线。

## 上线前交付复核清单

> 适用于：首版 `dev` 已达到“可发内部开发版”，且已具备真实外部 HTTPS 域名、Auth 证书，以及 Docker 镜像可构建 / 可推送 / 可部署条件，准备进一步做真实外部域名部署、对外可访问环境联调或上线前最后一轮交付复核。

### 目标

- 把“容器能启动”进一步提升为“真实外部访问口径可用”
- 确认公开域名、外层反代、Auth 证书、OIDC 回调地址与容器内配置完全一致
- 为后续 Git tag / Release / 对外环境部署留一份可复用的事实记录

### 进入条件

- 当前 `master` 最小门禁已收敛为 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`、`Dependency Security`、`Backend Guard`、`Identity Guard` 六项
- `npm run validate:baseline` 已通过
- 如本轮触达宿主 / 配置 / `DbMigrate` / 部署链，`npm run validate:baseline:host` 已通过
  - 当前它也是 `M14` 的默认宿主验证入口，失败时优先回到 [M14 宿主运行首轮执行清单](/records/m14-host-runtime-checklist) 按顺序分诊
- 当前没有阻塞主线的已知 `P0 / P1` 问题
- 已具备真实外部 HTTPS 域名，可为 `Deploy/.env` 提供真实 `RADISH_PUBLIC_URL`
- 已具备可持久化 Auth OIDC 证书的宿主目录；首次部署可由容器自动生成，后续必须复用同一组证书
- 已具备 Docker 镜像可构建、可推送、可拉取、可部署的最小条件

### 建议执行顺序

1. **先确认生产变量与证书路径**
   - `RADISH_IMAGE_TRACK` 或固定版本 `RADISH_IMAGE_TAG`
   - `Deploy/.env` 中的 `RADISH_PUBLIC_URL` 必须与真实外部 HTTPS 域名完全一致
   - `RADISH_POSTGRES_USER / RADISH_POSTGRES_PASSWORD / RADISH_REDIS_PASSWORD`
   - `RADISH_AUTH_SIGNING_CERT_PASSWORD / RADISH_AUTH_ENCRYPTION_CERT_PASSWORD` 应分别用 `openssl rand -hex 32` 生成，并在 `RADISH_AUTH_CERTS_PATH` 目录生命周期内保持不变
   - 确认 `RADISH_POSTGRES_DATA_PATH / RADISH_REDIS_DATA_PATH / RADISH_AUTH_CERTS_PATH` 指向的宿主目录会被保留和备份

2. **先做静态展开，不直接启动**
   - 从仓库根目录进入 `Deploy/` 后执行：
     ```bash
     cd Deploy
     docker compose config
     ```
   - 确认 `gateway / api / auth / frontend` 四个服务都已展开
   - 确认 `RADISH_PUBLIC_URL`、镜像 tag、`GatewayRuntime__EnableHttpsRedirection=false` 等关键变量都已落到最终配置

3. **再校验外层反代**
   - Nginx / Traefik / Caddy 的公开域名需与 `RADISH_PUBLIC_URL` 保持一致
   - 外层反代必须保留：
     - `Host`
     - `X-Forwarded-For`
     - `X-Forwarded-Proto`
     - `X-Forwarded-Host`
   - 若使用 Nginx，可基于 `Deploy/nginx.prod.conf` 修改 `server_name` 与证书路径后再上线

4. **执行生产发布编排并做运行态检查**
   - 推荐命令：
     ```bash
     ./Deploy/deploy-production.sh --preflight-only
     ./Deploy/deploy-production.sh --confirm-production
     ```
   - 启动后至少确认：
     - `/health` 可访问
     - `/` 可进入纯 Web 默认入口（当前为 `/discover` 公开分发页）
     - `/desktop` 可打开 WebOS 保留入口
     - `/console/` 可打开 Console
     - Auth / Gateway / Api 容器日志中没有证书加载失败、Issuer 不匹配或重定向异常
   - 若这里失败，不要直接扩大排查范围，优先回到 [M14 宿主运行首轮执行清单](/records/m14-host-runtime-checklist) 确认是 `doctor/verify`、宿主日志还是网关 / 反代链路的问题

5. **做真实外部域名链路验证**
   - 使用与 `RADISH_PUBLIC_URL` 完全一致的域名访问，不要混用 `localhost`
   - 验证：
     - `radish-client` 登录、回调、登出
     - `radish-console` 登录、回调、登出
     - `radish-scalar` 登录、回调、登出
     - 业务接口经 Gateway 转发后仍可正常返回
   - 若出现 `redirect_uri` 不匹配，优先检查：
     - 外部访问域名是否与 `RADISH_PUBLIC_URL` 一致
     - `OpenIddict__Server__Issuer` 是否已跟随 `RADISH_PUBLIC_URL`
     - 外层反代是否正确传递 `X-Forwarded-Proto` 与 `Host`

### 记录模板

上线前交付复核不再建议临时手写字段，统一复用：

- [M14 部署后最小复核记录模板](/records/m14-deployment-review-record-template)

最少应明确记录：

- 部署态组合、外部域名、镜像版本与证书来源
- `validate:baseline:host`、`check:host-runtime` 与 `collect:m14-host-record` 的执行结果
- `RADISH_PUBLIC_URL`、Issuer、反代头、`/health`、`/`、`/desktop`、`/console/`、`/scalar`
- `radish-client / radish-console / radish-scalar` 登录、回调、登出，以及 `userinfo` / 受保护接口结论

### 当前说明

- 本清单属于“上线前交付复核”，不阻塞当前“可发内部开发版”的判断
- 当前阶段若尚不具备真实 `RADISH_PUBLIC_URL`、Auth 证书或 Docker 镜像推送 / 部署条件，可先暂缓本清单；这表示“当前不阻塞”，不表示“真实外部联调已完成”
- 截至 `2026-04-06`，仓库已进入 `M15` 第一批；发布、部署、发布后最小复核与回滚的默认顺序统一以 [M15 最小交付与部署基线](/guide/m15-delivery-baseline) 为准
- 若后续只是常规功能迭代，仍优先使用 `validate:baseline` 与 `master` PR 质量门禁，不需要每次都执行本清单
- 若本轮问题已经落到宿主运行、自检或最小排障层，统一按 [M14 宿主运行首轮执行清单](/records/m14-host-runtime-checklist) 的顺序处理，不要把部署问题与代码回归问题混在一起排
- 若未来把 `Auth` 扩为多实例部署，OIDC 证书必须来自共享挂载目录、共享卷或外部密钥服务，不能让每个实例各自自动生成一套

## 文档系统部署

固定文档已统一收口到仓库 `Docs/`，不再维护独立的 `radish.docs` 文档站，也不再建议通过 Gateway 额外挂载一个 `/docs` 站点。

### 当前推荐方案

1. 固定文档直接存放在 `Docs/` 目录，按 `architecture/`、`guide/`、`frontend/`、`features/`、`deployment/`、`changelog/` 等分类维护。
2. API 启动时自动扫描 `Docs/**/*.md`，同步为只读固定文档，并重写站内链接与资源路径。
3. WebOS `/desktop` 保留入口中继续可通过“文档”应用展示固定文档与在线文档；在线文档继续存数据库。
4. 通过 `Document.ShowBuiltInDocs` 控制是否展示固定文档，便于其他站点复用时关闭项目内置文档。

补充约束：

- 固定文档启动同步当前依赖 `WikiDocument / WikiDocumentRevision` 表已存在。
- 若部署机上的主库仍未完成 `DbMigrate apply`，API 启动时会把固定文档同步记为“已跳过，请先执行 DbMigrate apply”，而不是继续抛缺表错误。
- 这类跳过日志不代表文档系统坏了，而是说明当前部署顺序不满足“先迁移、后启动宿主”的前置要求。

### 相关配置

```json
{
  "Document": {
    "ShowBuiltInDocs": true,
    "BuiltInDocsPath": "Docs",
    "StaticAssetsRequestPath": "/docs-assets"
  }
}
```

### Gateway 常见入口（开发环境）

- `/` → 纯 Web 默认入口（radish.client，普通浏览器当前进入 `/discover`）
- `/desktop` → WebOS 保留入口（radish.client）
- `/api` → 后端 API（Radish.Api）
- `/scalar` → Scalar API 文档 UI（转发到 Radish.Api 的 `/scalar`，`/api/docs` 为旧兼容路径）
- `/console` → 控制台前端（radish.console）

生产环境中，这些路径通常通过反向代理映射到 `{GatewayService.PublicUrl}`，例如：

- `https://radish.com/`、`https://radish.com/console` 等。

## 排查与清理
- 若构建时提示 SDK 版本不符，执行 `dotnet --list-sdks` 确认版本或在容器内设置 `DOTNET_NOLOGO=1` 以减少输出。
- 端口占用可通过 `docker compose ps` 或 `lsof -i :8080` 定位，调整 `ports` 映射即可。
- 清理旧镜像：`docker image prune -f`；清理多余卷：`docker volume prune`（慎用）。

## 后续镜像治理边界

当前真实交付仍以五个独立镜像和 `Deploy/docker-compose*.yaml` 为准，不维护未落地的“两镜像多容器”草案。若未来因镜像复用、制品体积或资源治理重新立项，需先确认服务隔离、独立扩缩容、健康检查、资源限制与回滚边界，再新增专题设计；不得用草案替代本页记录的真实部署入口。
