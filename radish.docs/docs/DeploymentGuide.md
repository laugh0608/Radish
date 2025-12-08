# 部署与容器指南

## 目标
本指南面向需要在本地或服务器上快速部署 Radish 的维护者，说明如何使用 `Radish.Api/Dockerfile` 构建镜像、配置环境变量，并提供一个 PostgreSQL + API 的 Compose 示例，确保与 `global.json` 指定的 .NET 10 SDK 及现有目录结构保持一致。

## 先决条件
- Docker Engine ≥ 24，能够拉取 `mcr.microsoft.com/dotnet/*` 官方镜像。
- .NET SDK 10.0.0（满足 `global.json`），用于调试或本地 `dotnet publish`。
- Node.js 20+：可选，用于本地构建前端；镜像会在 `with-node` 阶段安装 Node 响应 webpack/Vite 资源打包需求。
- PostgreSQL 15+：本地或托管实例，需提供 `ConnectionStrings__Default`。

## 构建服务镜像
`Radish.Api/Dockerfile` 采用多阶段构建：`with-node` 准备 Node 环境，`build/publish` 负责编译，`final` 提供轻量运行时。常用构建命令如下：

```bash
docker build \
  -f Radish.Api/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Release \
  -t radish/server:local .
```

如需在构建阶段打包前端，可在 `Radish.Api` 项目文件中引用打包产物，或在 Dockerfile 的 `build` 阶段追加 `npm install && npm run build --prefix radish.client`。

## 运行容器
镜像默认监听 8080/8081（HTTP/HTTPS）。启动容器示例：

```bash
docker run -d --name radish-api \
  -p 8080:8080 -p 8081:8081 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="Host=db;Port=5432;Database=radish;Username=radish;Password=radish" \
  -e ASPNETCORE_URLS="http://+:8080" \
  radish/server:local
```

将实际数据库凭据、安全密钥等以环境变量或 `appsettings.Production.json` 挂载方式注入。日志可通过 `docker logs -f radish-api` 追踪；若需热重载，请改用 `dotnet watch` 在宿主机运行。

## Docker Compose 示例
建议在 `deploy/docker-compose.yml` 中集中定义依赖服务，可参考：

```yaml
services:
  api:
    build:
      context: ..
      dockerfile: Radish.Api/Dockerfile
      args:
        BUILD_CONFIGURATION: Release
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Default: Host=db;Port=5432;Database=radish;Username=radish;Password=radish
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy
  db:
    image: postgres:16
    environment:
      POSTGRES_USER: radish
      POSTGRES_PASSWORD: radish
      POSTGRES_DB: radish
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U radish"]
      interval: 10s
      timeout: 5s
      retries: 5
    volumes:
      - db-data:/var/lib/postgresql/data
volumes:
  db-data:
```

使用 `docker compose -f deploy/docker-compose.yml up --build` 可一键启动依赖。若后续引入前端容器，可在同一文件内新增 `client` 服务并通过 Nginx/Caddy 统一暴露。

## 数据库初始化与迁移（Radish.DbMigrate）

> 适用于：新环境第一次部署数据库，或在已有数据库上按版本执行结构迁移。

### 快速开始：一键初始化（推荐）

对于全新环境或本地开发，推荐直接使用 `seed` 命令，它会自动完成表结构初始化和数据填充：

```bash
# 通过启动脚本（推荐）
pwsh ./start.ps1  # 或 ./start.sh
# 选择 7 (DbMigrate)，直接按回车选择默认的 seed

# 或直接运行
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed
```

**seed 命令会自动：**
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
   dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
   ```

   - 该命令会：
     - 根据当前配置创建数据库（若不存在）；
     - 扫描 `Radish.Model` 中的实体类型并执行 SqlSugar Code First（`InitTables`），自动创建/补全表结构。
3. 使用数据库客户端确认结构是否符合预期（表/字段/索引）。

### 生成迁移 SQL 并应用到生产

1. 在一套用于对比的测试库上执行 `DbMigrate init`，让其结构同步到最新代码。
2. 使用数据库工具导出“从当前生产版本 → 新版本”的结构差异 SQL，并保存到仓库（建议放在 `deploy/sql` 目录按日期/版本命名）。
3. 在发布新版本前，由 DBA 或 CI/CD 流水线在生产数据库上执行本次版本对应的迁移 SQL：
   - 顺序执行所有未执行过的脚本；
   - 执行完成后再滚动升级/重启 API 与 Gateway。
4. 对于需要回填的数据（例如为历史记录设置新字段的默认值），可以：
   - 在迁移 SQL 中添加 `UPDATE` 语句，或
   - 后续在 `Radish.DbMigrate` 项目中实现 `seed` 子命令统一处理。

> 建议：生产环境中**不要**在 Api/Gateway 启动时自动调用 `InitTables`，避免意外结构变更；所有结构更新应通过经审核的迁移 SQL 或专门的迁移任务执行。

### 初始化基础数据（seed 子命令）

`Radish.DbMigrate` 中的 `seed` 子命令用于初始化一批基础数据，当前默认会：

- 创建固定 Id 的角色：10000 System、10001 Admin、20000 system、20001 admin；
- 创建固定 Id 的租户：30000 Radish、30001 Test；
- 创建固定 Id 的部门：40000 Development、40001 Test；
- 创建固定 Id 的测试用户：20002 test（绑定租户 30000 与部门 40000）。

在仓库根目录执行：

```bash
# 通过启动脚本（推荐）
pwsh ./start.ps1  # 或 ./start.sh
# 选择 7 (DbMigrate)，直接按回车选择默认的 seed

# 或直接运行
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed
```

**智能初始化**：`seed` 命令会自动检测数据库表结构，如果表不存在会先执行 `init` 创建表结构，然后再填充数据。因此对于全新环境，您只需运行 `seed` 即可完成所有初始化工作。

实现位于 `Radish.DbMigrate/InitialDataSeeder.cs`，并按模块拆分为 `SeedRolesAsync`、`SeedTenantsAsync`、`SeedDepartmentsAsync`、`SeedUsersAsync` 等方法，支持后续按业务扩展更多种子数据。所有插入都会先通过 `AnyAsync` 判断是否已存在，保证可以安全重复执行。

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

#### Nginx 配置示例

```nginx
# Gateway 服务
server {
    listen 443 ssl http2;
    server_name radish.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5000;  # 反代到 HTTP 端口
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;  # 告诉后端原始请求是 HTTPS
    }
}

# API 服务
server {
    listen 443 ssl http2;
    server_name api.radish.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5100;  # 反代到 HTTP 端口
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }
}
```

#### ASP.NET Core 配置

在 `Program.cs` 中添加转发头中间件，让应用知道它在反向代理后面：

```csharp
using Microsoft.AspNetCore.HttpOverrides;

// 配置转发头中间件
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

#### 生产环境配置示例

Gateway 和 API 项目的 `appsettings.Production.json`：

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"  // 只监听 HTTP
      }
    }
  }
}
```

### Docker Compose 完整示例（含反向代理）

```yaml
services:
  nginx:
    image: nginx:alpine
    ports:
      - "443:443"
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certs:/etc/nginx/certs:ro
    depends_on:
      - gateway
      - api

  gateway:
    build:
      context: ..
      dockerfile: Radish.Gateway/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5000
      GatewayService__PublicUrl: https://radish.com
      DownstreamServices__ApiService__BaseUrl: http://api:5100
    expose:
      - "5000"
    networks:
      - radish-network

  api:
    build:
      context: ..
      dockerfile: Radish.Api/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5100
      ConnectionStrings__Default: Host=db;Port=5432;Database=radish;Username=radish;Password=${DB_PASSWORD}
    expose:
      - "5100"
    depends_on:
      db:
        condition: service_healthy
    networks:
      - radish-network

  db:
    image: postgres:16
    environment:
      POSTGRES_USER: radish
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: radish
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U radish"]
      interval: 10s
      timeout: 5s
      retries: 5
    volumes:
      - db-data:/var/lib/postgresql/data
    networks:
      - radish-network

volumes:
  db-data:

networks:
  radish-network:
    driver: bridge
```

### 注意事项

- **开发环境**：Gateway 应用对外使用 HTTPS 端口（`https://localhost:5000`，`http://localhost:5001` 仅用于重定向）
- **生产环境**：应用使用 HTTP 端口，TLS 由反向代理处理
- **内网可信场景**：反代到 HTTP 端口是安全的
- **零信任架构**：如需端到端加密，可配置反代到 HTTPS，但需要额外证书管理

## 可选：部署 Gateway 在线文档

本节为可选配置，仅当你希望在 Gateway 下托管在线文档站点时才需要执行。

### 步骤 1：安装文档工程依赖

在仓库根目录执行：

```bash
npm install --prefix radish.docs
```

该命令会在 `radish.docs` 目录下安装 VitePress 等文档工程依赖。

### 步骤 2：构建文档静态站点

```bash
npm run docs:build --prefix radish.docs
```

构建完成后，文档站点的静态文件会输出到：

- `radish.docs/dist`

该目录通常由独立 docs 服务或静态服务器托管；如需让 Gateway 托管静态文档，可将该目录挂载或复制到 `Radish.Gateway/DocsSite`，并在 Gateway 配置中设置对应的 `StaticFolder`。
### 步骤 3：在 Gateway 中启用 Docs

建议在 `Radish.Gateway/appsettings.Local.json` 中配置（`Local` 文件已被 Git 忽略，适合放环境差异与敏感信息）：

```json
"Docs": {
  "Enabled": true,
  "RequestPath": "/docs",
  "StaticFolder": "DocsSite"
}
```

说明：

- 基础配置 `Radish.Gateway/appsettings.json` 中 `Docs.Enabled` 默认为 `false`，开源用户默认不会启用在线文档；
- 当本地或生产环境需要文档站点时，通过 `appsettings.Local.json` 或环境变量覆盖即可开启；
- `RequestPath` 与 `StaticFolder` 应与 `Program.cs` 中的中间件配置保持一致。

### 步骤 4：通过 Gateway 访问在线文档

启动 Gateway 后，可通过以下地址访问文档站点：

- 本地默认：`https://localhost:5000/docs`
- 生产环境：`{GatewayService:PublicUrl}/docs`

> 提示：当 `Docs.Enabled = true` 但 `DocsSite` 目录不存在时，Gateway 会在日志中输出告警并回退到门户页，不会影响其他服务与路由。

### 常见 Gateway 路由一览（开发环境）

在默认本地开发配置下，Gateway 提供以下典型入口：

- `/` → 前端 webOS（radish.client）
- `/docs` → 在线文档站（radish.docs 构建产物或 dev 代理）
- `/api` → 后端 API（Radish.Api）
- `/scalar` → Scalar API 文档 UI（转发到 Radish.Api 的 `/api/docs`）
- `/console` → 控制台前端（radish.console）

生产环境中，这些路径通常通过反向代理映射到 `{GatewayService.PublicUrl}`，例如：

- `https://radish.com/`、`https://radish.com/docs`、`https://radish.com/console` 等。

## 排查与清理
- 若构建时提示 SDK 版本不符，执行 `dotnet --list-sdks` 确认版本或在容器内设置 `DOTNET_NOLOGO=1` 以减少输出。
- 端口占用可通过 `docker compose ps` 或 `lsof -i :8080` 定位，调整 `ports` 映射即可。
- 清理旧镜像：`docker image prune -f`；清理多余卷：`docker volume prune`（慎用）。

## 未来两镜像多容器部署设计（规划阶段）

> 本节用于记录未来在服务器上采用“两个镜像、多容器”的部署思路，目前 **仅作为设计文档，不在开发阶段使用 Docker 进行启动与测试**。
>
> 当前开发阶段建议仍使用 `dotnet run` / `dotnet watch` / `npm run dev` 等方式在宿主机直接运行各项目。

### 设计目标与前提

- 目标机器内存约 **4–8GB**，希望在资源有限的前提下尽量简化镜像数量；
- 采用 **两个基础镜像**：
  - `radish-backend`：承载所有 .NET 服务（Api / Gateway / Auth / DbMigrate 等）；
  - `radish-frontend`：承载所有前端项目的构建与静态资源（radish.client / radish.console / radish.docs）。
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
COPY radish.client/package*.json radish.client/
COPY radish.console/package*.json radish.console/
COPY radish.docs/package*.json radish.docs/

RUN cd radish.client && npm install
RUN cd radish.console && npm install
RUN cd radish.docs && npm install

# 复制源代码
COPY radish.client radish.client
COPY radish.console radish.console
COPY radish.docs radish.docs

# 统一构建所有前端项目
# - radish.client: 未来可以是 SSR/SSG 构建（生成 server bundle + HTML 模板）
# - radish.console / radish.docs: 通常构建为静态站点
RUN cd radish.client && npm run build
RUN cd radish.console && npm run build
RUN cd radish.docs && npm run build

# 运行时使用 Node 作为前端服务容器入口
# - WebOS (radish.client) 通过 Node 服务进行 SSR/SSG 渲染，返回带完整 HTML 的帖子列表/详情页
# - console/docs 可通过同一个 Node 服务以静态方式挂载，或继续采用其他静态托管方案
FROM node:20 AS final
WORKDIR /app

COPY --from=base /app .

# 约定：
# - `radish.client` 提供一个 SSR 入口（例如 scripts 中的 "start:ssr"），监听 3000 端口；
# - 具体的 SSR 实现细节由前端工程内部决定，Docker 只关心如何启动该服务。
CMD ["npm", "run", "start:ssr", "--prefix", "radish.client"]
```

> 注意：上面的 `start:ssr` 仅为占位命令，用于表达“这个容器将以一个 Node Web 服务的形式运行 WebOS SSR”，真正实现时需要在 `radish.client/package.json` 中定义对应脚本。

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

- **当前阶段不要求在 Docker 中启动与测试**：
  - 后端推荐使用 `dotnet run` / `dotnet watch` 在宿主机运行 Api / Gateway / Auth；
  - 前端推荐使用 `npm run dev --prefix radish.client` 等命令运行 Vite/VitePress 开发服务；
  - Docker 相关内容仅作为将来部署到服务器（测试/预生产/生产环境）时的设计参考。
- **在真正落地容器部署前，建议遵循以下步骤**：
  1. 与运维/基础设施同学确认最终的目录结构与文件命名（例如是否采用 `Dockerfile.backend`、`Dockerfile.frontend` 与仓库根目录 `docker-compose.yml`）；
  2. 根据实际数据库/Redis/域名/TLS 方案，补全 Compose 中的环境变量与端口映射；
  3. 在测试环境中逐步启用各服务容器，并使用 `docker stats`/监控系统验证内存与 CPU 占用情况；
  4. 确认没有影响现有非容器化部署流程后，再考虑将该方案纳入正式的部署流水线。

