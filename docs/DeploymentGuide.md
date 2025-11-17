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

## 排查与清理
- 若构建时提示 SDK 版本不符，执行 `dotnet --list-sdks` 确认版本或在容器内设置 `DOTNET_NOLOGO=1` 以减少输出。
- 端口占用可通过 `docker compose ps` 或 `lsof -i :8080` 定位，调整 `ports` 映射即可。
- 清理旧镜像：`docker image prune -f`；清理多余卷：`docker volume prune`（慎用）。
