# 部署与容器指南

## 目标
本指南面向需要在本地或服务器上快速部署 Radish 的维护者，说明如何使用 `Radish.Api/Dockerfile`、`Radish.Auth/Dockerfile`、`Radish.Gateway/Dockerfile` 与 `Frontend/Dockerfile` 构建首版最小镜像链，并通过 `Deploy/docker-compose.yml` 及其环境覆盖文件组织 `gateway / api / auth / frontend` 四个容器。当前部署口径已经收束为“开发环境直接 IDE / 宿主机运行，测试与生产环境统一拉取预构建镜像部署”；其中测试环境采用“Gateway 容器内 HTTPS”，生产环境采用“外部反代 HTTPS、容器内 HTTP”。

## 环境口径

当前仓库把部署与运行形态收束为四类：

- **开发运行**
  - 默认直接使用 IDE、`dotnet run` 与前端开发服务器
  - 继续使用仓库内置开发证书与 `localhost` 回调地址
  - 目标是调试效率，不要求与线上部署形态完全一致
- **本地容器验证**
  - 仅用于可选的本地容器构建与启动验证
  - 使用 `Deploy/docker-compose.yml + Deploy/docker-compose.local.yml`
  - 允许在本机执行 `build`，但不作为日常开发启动方式
- **测试部署**
  - 面向企业内部环境或外部客户试用
  - 直接拉取 `GHCR` 中已构建好的镜像，不在部署机现场构建
  - `Gateway` 容器直接对外提供 `HTTPS`
  - `Gateway` TLS 证书与 `Auth` OIDC 签名 / 加密证书均可在首次启动时自动生成并持久化
  - 默认通过 `https://IP:port` 或测试域名访问；若使用自签名证书，浏览器出现证书告警属于预期行为
- **生产部署**
  - 面向正式外部客户
  - 直接拉取 `GHCR` 中已构建好的发布镜像，不在部署机现场构建
  - 外部 `Nginx / Traefik / Caddy` 终止 TLS 并提供 `HTTPS`
  - 容器内部 `Gateway / Api / Auth` 只提供 `HTTP`
  - `Auth` OIDC 证书可在首次启动时自动生成并写入挂载目录；后续必须复用同一组证书

## 仓库发版与合并流程

当前仓库已启用 `master` 分支保护，默认约束如下：

- 禁止直接 push 到 `master`
- 禁止 force push
- 禁止删除 `master`
- `master` 只允许通过 Pull Request 合并
- 合并前需通过仓库检查：
  - `Repo Hygiene`
  - `Frontend Lint`
  - `Baseline Quick`
- 合并前需至少完成 1 次审批，并解决全部 review 对话
- 管理员当前仅允许“通过 Pull Request 绕过”，不开放直接 push

### 推荐分支路径

- 日常开发：在 `dev` 或功能分支完成开发与自检
- 准备发版：从 `dev` 向 `master` 发起 Pull Request
- 合并发布：PR 检查通过后，以 `squash` 或 `rebase` 方式合并到 `master`
- 发布记录：合并完成后创建 Git tag，并在 GitHub Release 中补发布说明

### 最小发版顺序

1. 在 `dev` 完成代码、文档与必要验证
2. 本地至少执行：

   ```bash
   npm run validate:baseline:quick
   ```

3. 发起 `dev -> master` 的 PR
4. 等待 GitHub Actions 中的 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 全部通过
5. 完成审批与会话收束后合并到 `master`
6. 合并后创建版本标签，例如：

   ```bash
   git checkout master
   git pull origin master
   git tag -a v26.3.1-release -m "Release v26.3.1"
   git push origin v26.3.1-release
   ```

7. 等待 `Docker Images` 工作流完成本次镜像产出；若当前标签为 `v*-release`，`GHCR` 会同步产出：
   - `ghcr.io/<owner>/radish-dbmigrate:<tag>` / `latest`
   - `ghcr.io/<owner>/radish-api:<tag>` / `latest`
   - `ghcr.io/<owner>/radish-auth:<tag>` / `latest`
   - `ghcr.io/<owner>/radish-gateway:<tag>` / `latest`
   - `ghcr.io/<owner>/radish-frontend:<tag>` / `latest`

8. 在 GitHub Release 中补齐本次发布说明、已知风险与回滚信息

### 现阶段说明

- 当前团队仅 1 人开发，因此规则允许管理员以 PR 方式完成自审 / 自合并
- 这不改变 `master` 禁止直接 push 的原则
- 后续若团队扩展，可再把审批数从 `1` 提升到 `2`，或补充 `CODEOWNERS`

## 先决条件
- Docker Engine ≥ 24，能够拉取 `mcr.microsoft.com/dotnet/*` 官方镜像。
- 若 `GHCR` 包为私有，还需要准备 `docker login ghcr.io` 所需凭据。
- .NET SDK 10.0.0+，用于调试或本地 `dotnet publish`。
- Node.js 24+：可选，用于宿主机本地构建前端；`Frontend/Dockerfile` 当前直接基于 Node 24 多阶段镜像完成构建与运行时封装。
- PostgreSQL / Redis：当前最小 Compose **不是必需项**。默认链路直接复用仓库共享配置中的 SQLite + 内存缓存，以便先完成首版镜像构建与交付验证；生产环境再按需覆盖。
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
  - `Deploy/docker-compose.yml`
  - `Deploy/docker-compose.local.yml`
  - `Deploy/docker-compose.test.yml`
  - `Deploy/docker-compose.prod.yml`
- 生产交付样例：
  - `Deploy/.env.test.example`
  - `Deploy/nginx.prod.conf`
  - `Deploy/.env.prod.example`
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
- `push -> v*-release`：构建五个镜像入口，并把 `radish-dbmigrate`、`radish-api`、`radish-auth`、`radish-gateway`、`radish-frontend` 以 `<tag>` + `latest` 推送到 `GHCR`
- `workflow_dispatch`：可手动补跑；仅当当前 ref 为 `v*-dev`、`v*-test` 或 `v*-release` tag，且显式启用 `push_backend`、`push_frontend` 对应开关时，才会推送对应镜像

当前镜像命名约定如下：

- `ghcr.io/<owner>/radish-dbmigrate`
- `ghcr.io/<owner>/radish-api`
- `ghcr.io/<owner>/radish-auth`
- `ghcr.io/<owner>/radish-gateway`
- `ghcr.io/<owner>/radish-frontend`

截至 `2026-03-28`，`radish-api / radish-auth / radish-gateway / radish-frontend` 已完成一轮真实 `docker pull` 验证；`radish-dbmigrate` 已接入 workflow 与部署编排，待下一次规范 tag 完成首次真实拉取验证。

当前 tag 规则如下：

- `v*-dev`：`<tag>`、`dev-latest`
- `v*-test`：`<tag>`、`test-latest`
- `v*-release`：`<tag>`、`latest`

其中 `dev-latest`、`test-latest`、`latest` 都只是对应轨道的浮动别名；测试与生产部署都建议固定使用明确的版本 tag，而不是依赖漂移别名。

当前 `frontend` 已接入统一 GHCR 推送规则；之所以可以直接纳入，是因为：

- `Frontend/scripts/serve-static.mjs` 已支持按请求返回 `/runtime-config.js` 运行时配置脚本
- `radish.client` 与 `radish.console` 当前都会优先读取运行时注入的公开地址与功能开关，再回退到构建期 `VITE_*`
- `Deploy/docker-compose.local.yml / docker-compose.test.yml / docker-compose.prod.yml` 也已为 `frontend` 容器补齐运行时环境变量入口

因此当前 `frontend` 已具备“同一个镜像按运行时环境复用”的能力；当前工作流已补齐统一推送规则，且 `frontend` GHCR 首次真实产物已可通过 `docker pull` 获取，当前剩余重点转为上线前外部交付复核。

## 构建服务镜像
以下内容主要用于 `CI`、本地容器验证或手动验证镜像入口。测试部署与生产部署默认应直接拉取 `GHCR` 里的预构建镜像，而不是在部署机执行 `docker build` 或 `docker compose build`。

当前仓库已提供首版最小镜像链，对应四个构建入口：

- `Radish.DbMigrate/Dockerfile`：发布数据库初始化入口，默认用于容器部署时的 `apply` 一次性任务，负责按需 `init + seed` 共享业务库。
- `Radish.Api/Dockerfile`：发布 API，并把仓库 `Docs/` 一并带入镜像，确保固定文档能力在容器内可用。
- `Radish.Auth/Dockerfile`：发布 OIDC 服务，并把仓库 `Certs/` 带入镜像，便于本地 / 内部开发版使用默认开发证书。
- `Radish.Gateway/Dockerfile`：发布网关服务，作为默认对外入口。
- `Frontend/Dockerfile`：当前采用 `Node 24` 多阶段镜像，先构建 `radish.client` 与 `radish.console`，最终镜像只保留静态产物与内置静态服务器，统一托管 `/` 与 `/console/`。

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
- 如需细分，也可通过 `VITE_API_BASE_URL`、`VITE_AUTH_BASE_URL`、`VITE_SIGNALR_HUB_URL`、`VITE_AUTH_SERVER_URL` 单独覆盖

> 说明：当前 `frontend` 镜像已纳入统一 GHCR 推送链路，并且已经完全切到运行时配置注入；同时 `Frontend/Dockerfile` 已收口为轻量多阶段运行时镜像，本地构建验证体积约 `300MB`，继续沿用“预构建镜像 + 部署时注入公开地址”时不再需要额外改 Dockerfile。

## 运行容器
当前最小链默认以 `Gateway` 作为唯一对外入口。Compose 现在拆为“基础文件 + 环境覆盖文件”两层：

- `Deploy/docker-compose.yml`：共享基础编排，只保留所有环境都一致的服务结构、镜像引用、`dbmigrate -> api/auth -> gateway` 启动顺序与下游地址
- `Deploy/docker-compose.local.yml`：本地容器验证覆盖，补充本地 `build` 入口，并让 `Gateway` 在容器内监听 HTTPS
- `Deploy/docker-compose.test.yml`：测试部署覆盖，默认让 `Gateway` 在容器内监听 HTTPS，并自动生成 / 复用测试 TLS 证书与 Auth OIDC 证书
- `Deploy/docker-compose.prod.yml`：生产反代覆盖，默认让 `Gateway` 在容器内监听 HTTP，并关闭 `UseHttpsRedirection()`

开发运行不走 Compose；Compose 只建议按以下三种组合使用：

- `base + local`：`Deploy/docker-compose.yml + Deploy/docker-compose.local.yml`，适用于本地容器构建验证、镜像入口验证、浏览器直连 Gateway 的场景
- `base + test`：`Deploy/docker-compose.yml + Deploy/docker-compose.test.yml`，适用于内部测试部署、客户试用与“容器内直接 HTTPS”场景
- `base + prod`：`Deploy/docker-compose.yml + Deploy/docker-compose.prod.yml`，适用于服务器部署、外层 Nginx 终止 HTTPS、Gateway 容器内仅监听 HTTP 的场景

### 开发运行：IDE / 宿主机直跑

日常开发默认直接使用 IDE、`dotnet run`、`npm run dev` 与启动脚本，不使用 Compose。

### `base + local`：本地容器验证

```bash
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml build
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml up -d
```

本地容器验证口径如下：

- `dbmigrate`：先执行一次 `apply`，自动补齐共享业务库表结构与基础数据
- `gateway`：对外监听 `https://localhost:5000`
- `api`：容器内监听 `5100`
- `auth`：容器内监听 `5200`
- `frontend`：容器内监听 `80`，由 `Gateway` 反向代理 `/` 与 `/console/`

本地容器验证时，`Gateway` 镜像会内置 `Certs/dev-gateway-cert.pfx` 作为开发证书，使 `https://localhost:5000` 可以直接完成 TLS 握手；若浏览器提示证书不受信任，请先在宿主机信任该开发证书。

### `base + test`：测试部署 / 客户试用

先复制 `Deploy/.env.test.example` 为 `Deploy/.env.test`。当前样例默认指向 `ghcr.io/laugh0608/...:test-latest`，用于快速体验测试轨道；若要锁定具体测试版本，仍建议把镜像 tag 改成明确的 `v*-test`。并替换至少以下真实值：

- `RADISH_FRONTEND_IMAGE`
- `RADISH_DBMIGRATE_IMAGE`
- `RADISH_API_IMAGE`
- `RADISH_AUTH_IMAGE`
- `RADISH_GATEWAY_IMAGE`
- `RADISH_PUBLIC_URL`
- `RADISH_GATEWAY_HTTPS_PORT`
- `RADISH_GATEWAY_CERT_PASSWORD`
- `RADISH_AUTH_SIGNING_CERT_PASSWORD`
- `RADISH_AUTH_ENCRYPTION_CERT_PASSWORD`

```bash
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml config
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d
```

测试覆盖默认约定如下：

- `dbmigrate` 会在 `api / auth / gateway` 启动前先执行一次 `apply`，自动初始化 / 补齐共享业务库表结构与基础数据；重复执行保持幂等
- `Gateway` 容器内部监听 `https://+:5000`
- `RADISH_PUBLIC_URL` 需要直接写成测试入口，例如 `https://10.10.10.20:5000`
- `Api / Auth / Gateway` 三个宿主都会优先基于 `RADISH_PUBLIC_URL` 自动收口到同一个 CORS 允许来源
- `Gateway` TLS 证书会在首次启动时按 `RADISH_PUBLIC_URL` 的 host 自动生成，并持久化到测试证书卷
- `Auth` 的 OIDC signing / encryption 证书会在首次启动时自动生成，并持久化到测试证书卷
- `AuthUi__ShowTestAccountHint=true`，登录页会保留测试账号提示，便于测试部署与客户试用时快速联调
- `Api` 会把同一份 Auth signing 证书以只读方式挂载到容器内，并直接做本地 JWT 验签，不依赖再通过 `127.0.0.1` 或外部域名回拉 OIDC metadata / JWKS
- 证书只会在“目标文件缺失”时生成；若证书已存在，则直接复用，不会因为重启漂移
- 若使用 `https://IP:port` + 自签名证书，浏览器出现证书告警属于预期行为，不代表 Gateway / OIDC 链路异常

### `base + prod`：生产 / 外部反向代理

先复制 `Deploy/.env.prod.example` 为 `Deploy/.env.prod`。当前样例默认指向 `ghcr.io/laugh0608/...:latest`，用于和正式发布轨道保持一致；如果要做可追溯发布，仍建议把镜像 tag 固定到明确的 `v*-release`。并替换至少以下真实值：

- `RADISH_FRONTEND_IMAGE`
- `RADISH_DBMIGRATE_IMAGE`
- `RADISH_API_IMAGE`
- `RADISH_AUTH_IMAGE`
- `RADISH_GATEWAY_IMAGE`
- `RADISH_PUBLIC_URL`
- `RADISH_AUTH_CERTS_DIR`
- `RADISH_AUTH_SIGNING_CERT_PATH`
- `RADISH_AUTH_SIGNING_CERT_PASSWORD`
- `RADISH_AUTH_ENCRYPTION_CERT_PATH`
- `RADISH_AUTH_ENCRYPTION_CERT_PASSWORD`

```bash
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

生产覆盖默认约定如下：

- `dbmigrate` 会在 `api / auth / gateway` 启动前先执行一次 `apply`，自动初始化 / 补齐共享业务库表结构与基础数据；若使用 SQLite，宿主机挂载目录中会生成或复用 `Radish.db` 等数据库文件
- `RADISH_*_IMAGE` 应指向 `GHCR` 中已经构建完成的发布镜像，生产建议固定到明确版本 tag，而不是 `latest`
- `GatewayService__PublicUrl`、前端运行时公开地址回退值，以及 Auth 的 `Issuer / CORS` 都通过 `RADISH_PUBLIC_URL` 对齐真实外部域名
- `Api / Auth / Gateway` 三个宿主的部署态 CORS 允许来源都会优先从 `RADISH_PUBLIC_URL` 推导，启动日志应保持一致
- `Auth` 中官方 OIDC 客户端（`radish-client / radish-console / radish-scalar`）的 Gateway 回调地址也会跟随 `OpenIddict__Server__Issuer` 对齐，因此 `RADISH_PUBLIC_URL` 必须与真实外部 HTTPS 域名保持一致
- `prod` 口径下不要直接用 `http://localhost:5000` 做登录验证；若访问协议、域名或端口与 `RADISH_PUBLIC_URL` 不一致，OpenIddict 会因 `redirect_uri` 不匹配而拒绝请求
- `Gateway` 容器内部监听 `http://+:5000`
- `GatewayRuntime__EnableHttpsRedirection=false`
- `AuthUi__ShowTestAccountHint=false`，正式登录页默认不再暴露测试账号提示
- `Auth` 通过 `RADISH_AUTH_CERTS_DIR` 把宿主机证书目录挂载到容器 `/app/certs`，并通过 `RADISH_AUTH_*` 变量覆盖生产证书路径与密码
- `Api` 会从同一个挂载目录只读复用 Auth signing 证书，并直接做本地 JWT 验签，避免再依赖外部反代域名或容器内 loopback 去获取 OIDC 元数据
- 若 `RADISH_AUTH_CERT_AUTO_GENERATE=true` 且目标证书文件不存在，`Auth` 会在首次启动时自动生成 OIDC 证书并写入挂载目录；后续启动直接复用同一组证书
- TLS 由外部 Nginx / Traefik / Caddy 终止，再转发到容器内 HTTP 端口；仓库已提供可直接落地的 `Deploy/nginx.prod.conf`
- 不要再额外覆盖 `Cors__AllowedOrigins__0` 之类的单索引数组项；部署态统一以 `RADISH_PUBLIC_URL` 为准，避免旧 `localhost` 端口残留

**文件上传目录挂载（生产环境建议）**：
- 本地存储模式下，上传文件存放在 `DataBases/Uploads/`
- 建议挂载到宿主机持久化目录，避免容器重启丢失文件

如需分别单独运行镜像，可参考：

```bash
docker run -d --name radish-api \
  -p 5100:5100 \
  -v /data/radish/db:/app/DataBases \
  -v /data/radish/logs:/app/Logs \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS="http://+:5100" \
  ghcr.io/laugh0608/radish-api:latest
```

将实际数据库凭据、安全密钥与证书路径等以环境变量或挂载文件方式注入。上面的镜像地址仅用于对齐当前 `.env.prod.example` 默认值；正式发布仍建议固定到明确的 `v*-release` tag。日志可通过 `docker logs -f <container>` 追踪；若需热重载，请继续使用宿主机 `dotnet watch` / `npm run dev`。

## Docker Compose 示例
仓库根目录已提供真实可用的基础编排与环境覆盖文件，推荐按环境组合使用：

```bash
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml config
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml build
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml up -d

docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml config
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml up -d

docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

当前 Compose 口径如下：

- 默认使用仓库共享配置中的 SQLite 与内存缓存，不强依赖 PostgreSQL / Redis。
- 当前三套容器编排都会先执行 `dbmigrate apply`，再启动 `api / auth / gateway`，避免首次部署时因为共享业务库缺表而直接在登录链路报错；首次远程镜像 + 空库部署的真实 Smoke 待下一次规范 tag 补齐。
- `DataBases/` 与 `Logs/` 会挂载到宿主机，便于保留 SQLite、上传文件与运行日志。
- `Frontend` 运行的是预构建镜像；该镜像在 `CI` 中统一构建，并同时托管 `radish.client` 与 `radish.console`。
- `Gateway` 会通过环境变量把 `/` 与 `/console/` 反代到前端容器，并把 `/api`、`/connect`、`/Account` 等路径转发给对应后端服务。
- `GatewayRuntime__EnableHttpsRedirection` 当前已作为运行时开关显式暴露，可与 `ASPNETCORE_URLS` 一起切换容器内部的 HTTP / HTTPS 监听模式。
- 开发运行默认使用 IDE / 宿主机直跑，不走 Compose。
- 本地容器验证默认保持项目既有口径：`Gateway` 作为唯一对外 HTTPS 入口，访问地址为 `https://localhost:5000`。
- 测试部署默认保持“Gateway 容器内 HTTPS + 自动生成测试 TLS 证书 + 自动生成 Auth OIDC 证书”的单机交付口径。
- 测试部署与生产部署默认直接拉取 `GHCR` 预构建镜像，不在部署机执行 `build`。
- 生产覆盖默认收口为“外层 HTTPS、内层 HTTP”，避免与外部反向代理重复做 TLS 终止。

如果后续需要切换到 PostgreSQL / Redis，只需在 Compose 中继续补相应服务，并通过环境变量覆盖共享配置。

## 数据库初始化与迁移（Radish.DbMigrate）

> 适用于：新环境第一次部署数据库，或在已有数据库上按版本执行结构迁移。

### 快速开始：一键初始化（推荐）

对于全新环境或本地开发，推荐直接使用默认命令（或显式传入 `apply`），它会自动完成表结构初始化和数据填充：

在执行前，建议先用 `doctor` 做一次只读自检：

```bash
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
```

`doctor` / `verify` 只会检查当前环境名、`MainDb`、已启用连接、关键 `ConnId` 与主库业务表状态，不会执行 `init` 或 `seed`。

```bash
# 通过启动脚本（推荐）
pwsh ./start.ps1  # 或 ./start.sh
# 选择 7 (DbMigrate)，直接按回车选择默认的 apply

# 或直接运行
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj
# 或
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply
```

**apply 命令会自动：**
1. 检查数据库表结构是否存在
2. 如果表不存在，自动执行 `init` 创建表结构
3. 填充初始数据（角色、租户、部门、用户等）

这样您无需手动先执行 `init` 再执行 `seed`，一条命令即可完成所有初始化。

### 本地/测试环境：仅初始化数据库结构

如果您只需要创建表结构而不填充数据，可以单独使用 `init` 命令：

1. 确认 `Radish.Api/appsettings.Local.json`（或对应环境的 `appsettings.{Environment}.json`）已配置好数据库连接：
   - SQLite 场景：只需指定数据库文件名（如 `Radish.db`）。
   - PostgreSQL 场景：配置好 `Host/Port/Database/Username/Password`。
2. 在仓库根目录执行：

   ```bash
   dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
   dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
   ```

   - 该命令会：
     - 先只读检查当前配置与连接状态；
     - 根据当前配置创建数据库（若不存在）；
     - 扫描 `Radish.Model` 中的实体类型并执行 SqlSugar Code First（`InitTables`），自动创建/补全表结构。
3. 使用数据库客户端确认结构是否符合预期（表/字段/索引）。

### 生成迁移 SQL 并应用到生产

1. 在一套用于对比的测试库上执行 `DbMigrate init`，让其结构同步到最新代码。
2. 使用数据库工具导出“从当前生产版本 → 新版本”的结构差异 SQL，并保存到仓库（建议放在 `Deploy/sql` 目录按日期/版本命名）。
3. 在发布新版本前，由 DBA 或 CI/CD 流水线在生产数据库上执行本次版本对应的迁移 SQL：
   - 顺序执行所有未执行过的脚本；
   - 执行完成后再滚动升级/重启 API 与 Gateway。
4. 对于需要回填的数据（例如为历史记录设置新字段的默认值），可以：
   - 在迁移 SQL 中添加 `UPDATE` 语句，或
   - 后续在 `Radish.DbMigrate` 项目中实现 `seed` 子命令统一处理。

> 建议：生产环境中**不要**在 Api/Gateway 启动时自动调用 `InitTables`，避免意外结构变更；所有结构更新应通过经审核的迁移 SQL 或专门的迁移任务执行。

### 初始化基础数据（seed 子命令）

`Radish.DbMigrate` 中的 `seed` 子命令用于初始化一批基础数据，当前默认会按步骤执行：

- 角色
- 租户
- 部门
- 用户
- 用户时区偏好
- 用户角色
- 角色 API 权限
- 论坛分类 / 标签
- Wiki 文档（当前仅输出步骤日志，未预置默认文档）
- 聊天室默认频道
- 等级配置
- 商城分类 / 商品
- 表情包默认数据（当前仅输出步骤日志，未预置默认数据）

在仓库根目录执行：

```bash
# 通过启动脚本（推荐）
pwsh ./start.ps1  # 或 ./start.sh
# 选择 7 (DbMigrate)，直接按回车选择默认的 apply

# 或直接运行
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply
```

**智能初始化**：`apply` 命令会自动检测数据库表结构，如果表不存在会先执行 `init` 创建表结构，然后再填充数据。因此对于全新环境，您只需运行 `apply` 即可完成所有初始化工作。

**推荐顺序**：`doctor` → `apply`（全新环境）或 `doctor` → `init` → `seed`（需要先单独校验结构时）。

实现位于 `Radish.DbMigrate/InitialDataSeeder.cs`，并按模块拆分为 `SeedRolesAsync`、`SeedTenantsAsync`、`SeedDepartmentsAsync`、`SeedUsersAsync` 等方法，支持后续按业务扩展更多种子数据。

当前 `seed` 执行器具备以下特性：

- **自动步骤日志**：每个种子步骤都会自动输出“开始 / 完成 / 耗时 / 失败”日志，新增步骤时无需再手写汇总日志。
- **自动最终汇总**：命令结束后会按实际执行的步骤自动打印汇总，避免新增种子后遗漏日志说明。
- **旧库幂等纠正**：针对默认用户、用户时区偏好、聊天室默认频道、商城商品等固定种子数据，重复执行 `seed` 时会优先纠正旧记录，而不是直接因唯一键冲突失败。
- **占位步骤显式可见**：如 Wiki 文档、表情包默认数据等当前尚未预置的模块，也会在日志中明确打印“跳过”，方便确认种子覆盖范围。

---

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
- `Deploy/.env.prod.example`：当前 `base + prod` 组合的最小变量样例，至少覆盖 `RADISH_PUBLIC_URL`、Auth 证书挂载目录，以及签名 / 加密证书路径与密码。
- `Radish.Gateway/Program.cs`：当前已经显式启用 `X-Forwarded-For / X-Forwarded-Proto / X-Forwarded-Host` 识别，能够正确处理反代后的 Scheme、Host 与重定向。

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

- **`base + local`**：`Deploy/docker-compose.yml + Deploy/docker-compose.local.yml` 当前直接让 Gateway 在容器内终止 TLS，并只对宿主机暴露 `https://localhost:5000`
- **`base + prod`**：`Deploy/docker-compose.yml + Deploy/docker-compose.prod.yml` 当前默认要求通过 `--env-file Deploy/.env.prod` 注入真实域名与 Auth 证书变量，再由外层 Nginx 终止 HTTPS
- **生产环境**：Gateway / Api / Auth 当前仍默认走 HTTP 容器内通信，TLS 只在反向代理层终止
- **内网可信场景**：反代到 HTTP 端口是安全的
- **零信任架构**：如需端到端加密，可配置反代到 HTTPS，但需要额外证书管理

### OIDC 证书滚动更新流程

1. **准备新证书**：参考《[鉴权与授权指南](/guide/authentication)》的“证书生成示例”生成新 `.pfx`（签名/加密可拆分）。
2. **上传/挂载**：将新证书放到宿主机（如 `/etc/radish/certs/auth-signing-2025Q1.pfx`），并映射到容器的 `/app/certs`。
3. **更新部署变量**：修改 `Deploy/.env.prod` 中的 `RADISH_AUTH_SIGNING_CERT_PATH / RADISH_AUTH_SIGNING_CERT_PASSWORD / RADISH_AUTH_ENCRYPTION_CERT_PATH / RADISH_AUTH_ENCRYPTION_CERT_PASSWORD`；若证书目录也变化，同时更新 `RADISH_AUTH_CERTS_DIR`。
   ```bash
   docker compose --env-file Deploy/.env.prod \
     -f Deploy/docker-compose.yml \
     -f Deploy/docker-compose.prod.yml \
     up -d auth
   ```
   Kubernetes 集群可在 Helm values 中覆盖对应 `env`，并执行 `helm upgrade`。
4. **分批重启**：
   - Compose：`docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d auth`
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

- 最新一次 `master` PR 已完成 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`
- `npm run validate:baseline` 已通过
- 如本轮触达宿主 / 配置 / `DbMigrate` / 部署链，`npm run validate:baseline:host` 已通过
- 当前没有阻塞主线的已知 `P0 / P1` 问题
- 已具备真实外部 HTTPS 域名，可为 `Deploy/.env.prod` 提供真实 `RADISH_PUBLIC_URL`
- 已具备可挂载到 Auth 容器的正式证书，或至少已具备可持久化写入的正式证书目录 / 卷
- 已具备 Docker 镜像可构建、可推送、可拉取、可部署的最小条件

### 建议执行顺序

1. **先确认生产变量与证书路径**
   - `RADISH_FRONTEND_IMAGE`
   - `RADISH_API_IMAGE`
   - `RADISH_AUTH_IMAGE`
   - `RADISH_GATEWAY_IMAGE`
   - `Deploy/.env.prod` 中的 `RADISH_PUBLIC_URL` 必须与真实外部 HTTPS 域名完全一致
   - `RADISH_AUTH_CERTS_DIR`
   - `RADISH_AUTH_SIGNING_CERT_PATH`
   - `RADISH_AUTH_SIGNING_CERT_PASSWORD`
   - `RADISH_AUTH_ENCRYPTION_CERT_PATH`
   - `RADISH_AUTH_ENCRYPTION_CERT_PASSWORD`
   - 确认证书文件在宿主机真实存在，且容器内挂载后路径与变量值一致

2. **先做静态展开，不直接启动**
   - 在仓库根目录执行：
     ```bash
     docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
     ```
   - 确认 `gateway / api / auth / frontend` 四个服务都已展开
   - 确认 `RADISH_PUBLIC_URL`、证书挂载目录、`GatewayRuntime__EnableHttpsRedirection=false` 等关键变量都已落到最终配置

3. **再校验外层反代**
   - Nginx / Traefik / Caddy 的公开域名需与 `RADISH_PUBLIC_URL` 保持一致
   - 外层反代必须保留：
     - `Host`
     - `X-Forwarded-For`
     - `X-Forwarded-Proto`
     - `X-Forwarded-Host`
   - 若使用 Nginx，可基于 `Deploy/nginx.prod.conf` 修改 `server_name` 与证书路径后再上线

4. **启动 `base + prod` 组合并做最小运行态检查**
   - 推荐命令：
     ```bash
     docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
     docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
     ```
   - 启动后至少确认：
     - `/health` 可访问
     - `/` 可打开 WebOS
     - `/console/` 可打开 Console
     - Auth / Gateway / Api 容器日志中没有证书加载失败、Issuer 不匹配或重定向异常

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

### 最小记录模板

- 记录日期：YYYY-MM-DD
- 记录范围：上线前交付复核
- 外部域名：<domain>
- Compose 组合：`base + prod`
- 证书来源：<path or secret name>
- 静态展开：通过 / 阻塞
- 最小运行态：通过 / 阻塞
- OIDC 回调链路：通过 / 阻塞
- 结论：可进入更大范围部署 / 仍需修复

### 当前说明

- 本清单属于“上线前交付复核”，不阻塞当前“可发内部开发版”的判断
- 当前阶段若尚不具备真实 `RADISH_PUBLIC_URL`、Auth 证书或 Docker 镜像推送 / 部署条件，可先暂缓本清单；这表示“当前不阻塞”，不表示“真实外部联调已完成”
- 当前下一阶段主线应先补齐 `CI/CD` 与 Docker 镜像推送链路；待条件具备后，再按本清单正式执行并补记录
- 若后续只是常规功能迭代，仍优先使用 `validate:baseline` 与 `master` PR 质量门禁，不需要每次都执行本清单
- 若未来把 `Auth` 扩为多实例部署，OIDC 证书必须来自共享挂载目录、共享卷或外部密钥服务，不能让每个实例各自自动生成一套

## 文档系统部署

固定文档已统一收口到仓库 `Docs/`，不再维护独立的 `radish.docs` 文档站，也不再建议通过 Gateway 额外挂载一个 `/docs` 站点。

### 当前推荐方案

1. 固定文档直接存放在 `Docs/` 目录，按 `architecture/`、`guide/`、`frontend/`、`features/`、`deployment/`、`changelog/` 等分类维护。
2. API 启动时自动扫描 `Docs/**/*.md`，同步为只读固定文档，并重写站内链接与资源路径。
3. WebOS 中统一使用“文档”应用展示固定文档与在线文档；在线文档继续存数据库。
4. 通过 `Document.ShowBuiltInDocs` 控制是否展示固定文档，便于其他站点复用时关闭项目内置文档。

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

- `/` → 前端 WebOS（radish.client）
- `/api` → 后端 API（Radish.Api）
- `/scalar` → Scalar API 文档 UI（转发到 Radish.Api 的 `/scalar`，`/api/docs` 为旧兼容路径）
- `/console` → 控制台前端（radish.console）

生产环境中，这些路径通常通过反向代理映射到 `{GatewayService.PublicUrl}`，例如：

- `https://radish.com/`、`https://radish.com/console` 等。

## 排查与清理
- 若构建时提示 SDK 版本不符，执行 `dotnet --list-sdks` 确认版本或在容器内设置 `DOTNET_NOLOGO=1` 以减少输出。
- 端口占用可通过 `docker compose ps` 或 `lsof -i :8080` 定位，调整 `ports` 映射即可。
- 清理旧镜像：`docker image prune -f`；清理多余卷：`docker volume prune`（慎用）。

## 未来两镜像多容器部署设计（规划阶段）

> 本节用于记录未来在服务器上采用“两个镜像、多容器”的部署思路，目前 **仅作为设计文档，不在开发阶段使用 Docker 进行启动与测试**。
>
> 当前仓库已经有一套真实可构建的最小镜像链，本节保留的是“进一步压缩镜像数量”的后续设计，不代表当前首版 `dev` 已经切换到该方案。

### 设计目标与前提

- 目标机器内存约 **4–8GB**，希望在资源有限的前提下尽量简化镜像数量；
- 采用 **两个基础镜像**：
  - `radish-backend`：承载所有 .NET 服务（Api / Gateway / Auth / DbMigrate 等）；
  - `radish-frontend`：承载所有前端项目的构建与静态资源（radish.client / radish.console）。
- 在 Compose 层面仍然按照服务拆分多个容器：
  - 后端：`gateway`、`api`、`auth`（以及可选的 `db-migrate` 等 Job 容器）；
  - 前端：`client`（未来可扩展为 `console`、`docs` 独立容器）；
  - 数据：`postgres`、`redis`。
- 区分概念：**镜像数量少** 不代表只跑少量容器，每个服务仍然应有独立容器，便于监控、扩容和故障隔离。

### 后端镜像设计示例（Dockerfile.backend 草案）

> 下述 Dockerfile 仅为设计示例，用于说明统一后端镜像的构建思路，**仓库中默认不会自动创建该文件**。当后续确实需要上线容器部署时，可在确认路径与项目之后再落地。

```dockerfile
# 统一构建所有 .NET 服务的后端镜像
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制解决方案与各项目文件（根据实际项目补充）
COPY Radish.slnx ./
COPY Radish.Api/Radish.Api.csproj Radish.Api/
COPY Radish.Auth/Radish.Auth.csproj Radish.Auth/
COPY Radish.Gateway/Radish.Gateway.csproj Radish.Gateway/
# 如有额外 Job/Web 项目，可在此继续追加 COPY + restore

RUN dotnet restore Radish.Api/Radish.Api.csproj
RUN dotnet restore Radish.Auth/Radish.Auth.csproj
RUN dotnet restore Radish.Gateway/Radish.Gateway.csproj

# 复制全部源代码
COPY . .

# 分别发布到不同目录，供运行时容器按需选择入口
RUN dotnet publish Radish.Api/Radish.Api.csproj -c Release -o /app/api /p:UseAppHost=false
RUN dotnet publish Radish.Auth/Radish.Auth.csproj -c Release -o /app/auth /p:UseAppHost=false
RUN dotnet publish Radish.Gateway/Radish.Gateway.csproj -c Release -o /app/gateway /p:UseAppHost=false
# 例如：RUN dotnet publish Radish.DbMigrate/Radish.DbMigrate.csproj -c Release -o /app/dbmigrate /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/api /app/api
COPY --from=build /app/auth /app/auth
COPY --from=build /app/gateway /app/gateway
# COPY --from=build /app/dbmigrate /app/dbmigrate

# 运行时不在镜像中写死 ENTRYPOINT，由 docker-compose 的 command 决定具体运行哪一个服务
```

### 前端镜像设计示例（Dockerfile.frontend 草案，兼顾 WebOS SEO）

> 同样仅作为设计示例，说明如何用单一前端镜像承载三个前端项目。
> 其中 `radish.client` 未来会承载 WebOS（帖子列表/详情等公开页面），需要对搜索引擎友好，建议采用 SSR/SSG + hydrate 的方式，而不是纯 SPA。出于简化，本节只记录“镜像运行形态”的规划，不约束具体框架实现（可以是 Vite SSR、Next.js、Astro 等）。

```dockerfile
FROM node:20 AS base
WORKDIR /app

# 安装三个前端项目的依赖
COPY Frontend/radish.client/package*.json Frontend/radish.client/
COPY Frontend/radish.console/package*.json Frontend/radish.console/

RUN cd Frontend/radish.client && npm install
RUN cd Frontend/radish.console && npm install

# 复制源代码
COPY radish.client radish.client
COPY radish.console radish.console

# 统一构建所有前端项目
# - radish.client: 未来可以是 SSR/SSG 构建（生成 server bundle + HTML 模板）
# - radish.console: 通常构建为静态站点
RUN cd Frontend/radish.client && npm run build
RUN cd Frontend/radish.console && npm run build

# 运行时使用 Node 作为前端服务容器入口
# - WebOS (radish.client) 通过 Node 服务进行 SSR/SSG 渲染，返回带完整 HTML 的帖子列表/详情页
# - console 可按需独立托管；固定文档无需单独静态站点
FROM node:20 AS final
WORKDIR /app

COPY --from=base /app .

# 约定：
# - `radish.client` 提供一个 SSR 入口（例如 scripts 中的 "start:ssr"），监听 3000 端口；
# - 具体的 SSR 实现细节由前端工程内部决定，Docker 只关心如何启动该服务。
CMD ["npm", "run", "start:ssr", "--prefix", "radish.client"]
```

> 注意：上面的 `start:ssr` 仅为占位命令，用于表达“这个容器将以一个 Node Web 服务的形式运行 WebOS SSR”，真正实现时需要在 `Frontend/radish.client/package.json` 中定义对应脚本。

### 两镜像多容器的 Compose 结构示例（草案）

> 本小节给出一个基于 `radish-backend` 与 `radish-frontend` 的 Compose 结构示例，并结合 4–8GB 内存的目标机器给出初始内存限制配置。数值仅供参考，实际部署应结合 `docker stats` 和监控数据调整。

推荐在仓库根目录使用 `docker-compose.yml` 统一编排（路径可根据团队习惯调整）：

```yaml
version: "3.9"

services:
  gateway:
    image: radish-backend
    container_name: radish-gateway
    command: ["dotnet", "Radish.Gateway.dll"]
    working_dir: /app/gateway
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
    ports:
      - "5000:5000"
      - "5001:5001"
    depends_on:
      - api
      - auth
      - client
    mem_reservation: 192m
    mem_limit: 384m
    networks:
      - radish-net

  api:
    image: radish-backend
    container_name: radish-api
    command: ["dotnet", "Radish.Api.dll"]
    working_dir: /app/api
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      # ConnectionStrings__Main: "Host=postgres;Port=5432;Database=radish;Username=radish;Password=change_me"
    depends_on:
      - postgres
      - redis
    mem_reservation: 256m
    mem_limit: 768m
    networks:
      - radish-net

  auth:
    image: radish-backend
    container_name: radish-auth
    command: ["dotnet", "Radish.Auth.dll"]
    working_dir: /app/auth
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
    depends_on:
      - postgres
      - redis
    mem_reservation: 256m
    mem_limit: 512m
    networks:
      - radish-net

  client:
    image: radish-frontend
    container_name: radish-client
    ports:
      - "3000:80"  # 生产环境可由 Gateway 或外部反向代理统一暴露
    mem_reservation: 64m
    mem_limit: 256m
    networks:
      - radish-net

  postgres:
    image: postgres:16
    container_name: radish-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: "radish"
      POSTGRES_USER: "radish"
      POSTGRES_PASSWORD: "change_me"
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    mem_reservation: 256m
    mem_limit: 768m
    networks:
      - radish-net

  redis:
    image: redis:7
    container_name: radish-redis
    restart: unless-stopped
    command: ["redis-server", "--save", "60", "1", "--loglevel", "warning"]
    volumes:
      - redisdata:/data
    ports:
      - "6379:6379"
    mem_reservation: 64m
    mem_limit: 256m
    networks:
      - radish-net

networks:
  radish-net:
    driver: bridge

volumes:
  pgdata:
  redisdata:
```

该结构下，在 4–8GB 宿主机上：

- 所有服务的 `mem_limit` 上限之和约为 3GB 左右，保留了充足的系统与缓冲空间；
- 后续可根据实际监控情况对 `mem_reservation` 与 `mem_limit` 做细调：
  - 如果某服务常年远低于软限制，可适当下调节省资源；
  - 如果某服务经常接近硬限制，则需要考虑优化缓存/查询或上调限制。

### 当前开发阶段的约定与落地建议

- **当前阶段默认仍优先宿主机运行与调试**：
  - 后端推荐继续使用 `dotnet run` / `dotnet watch` 在宿主机运行 Api / Gateway / Auth；
  - 前端推荐继续使用 `npm run dev --prefix Frontend/radish.client` 等命令运行 Vite 开发服务；
  - Docker 相关内容当前已不再只是草案，而是首版 `dev` 的最小构建 / 交付验证资产；但运行态联调与日常开发默认仍以宿主机方式为主。
- **在真正落地容器部署前，建议遵循以下步骤**：
  1. 与运维/基础设施同学确认最终的目录结构与文件命名（例如是否采用 `Dockerfile.backend`、`Dockerfile.frontend` 与仓库根目录 `docker-compose.yml`）；
  2. 根据实际数据库/Redis/域名/TLS 方案，补全 Compose 中的环境变量与端口映射；
  3. 在测试环境中逐步启用各服务容器，并使用 `docker stats`/监控系统验证内存与 CPU 占用情况；
  4. 确认没有影响现有非容器化部署流程后，再考虑将该方案纳入正式的部署流水线。
