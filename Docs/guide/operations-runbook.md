# 本地运行与排障手册

本文提供本地开发阶段最常用的运行、验证、观测与排障入口，目标不是覆盖所有历史记录，而是提供一张“今天该从哪里下手”的操作清单。

## 1. 选择启动模式

### 一键组合启动

适合第一次跑通全链路或需要快速确认多宿主联动：

```bash
pwsh ./start.ps1
./start.sh
```

`start.sh` 的组合启动会记录后台服务进程组和子进程树。按下 `Ctrl+C` 或收到 `SIGTERM` 时，脚本会先对后台服务发送 `TERM` 并短暂等待；若仍有子进程残留，再发送 `KILL` 清理，避免 `Radish.Auth`、`Radish.Gateway` 或前端 dev server 留在后台占用端口。

### 单服务启动

适合聚焦单点调试：

```bash
dotnet run --project Radish.Api
dotnet run --project Radish.Gateway
dotnet run --project Radish.Auth

npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

## 2. 最小验证顺序

如果你只是想先确认当前分支是不是健康的，建议从这里开始：

```bash
dotnet build Radish.slnx -c Debug
npm run validate:baseline:quick
```

如果你改动了共享前端包，再补：

```bash
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
```

完整验证口径见 [验证基线](/guide/validation-baseline)。

## 3. 常用观测入口

- Gateway 首页：`https://localhost:5000/`
- Web 功能地图：`https://localhost:5000/workbench`
- WebOS 历史入口：`https://localhost:5000/desktop`
- Gateway 健康检查：
  - `https://localhost:5000/health`
  - `https://localhost:5000/healthz`
- Scalar：`https://localhost:5000/scalar`
- Client：`http://localhost:3000/`
- Console：`http://localhost:3100/`

## 4. 手工接口回归入口

- 目录：[`Radish.Api.Tests/HttpTest`](</D:/Code/Radish/Radish.Api.Tests/HttpTest>)
- 使用说明：[`Radish.Api.Tests/HttpTest/README.md`](</D:/Code/Radish/Radish.Api.Tests/HttpTest/README.md>)
- 模块索引见 [API 说明索引](/guide/api-index)

如果本轮改动触达身份语义、Claim、Auth 协议输出或 Token 解析，优先再补：

- [身份语义防回归回归手册](/guide/identity-claim-regression-playbook)
- `npm run validate:identity`

## 5. 日志与本地文件

- 应用日志：`Logs/{ProjectName}/Log.txt`
- SQL 日志：`Logs/{ProjectName}/AopSql/AopSql.txt`
- 上传目录默认配置：`DataBases/Uploads`
- 分片临时目录默认配置：`DataBases/Temp/Chunks`

如果你怀疑是配置或运行态问题，优先先看：

1. 当前宿主是否读取到了正确的 `appsettings.Local.json`
2. 当前对外入口是否仍与 `RADISH_PUBLIC_URL` / `Issuer` 一致
3. 当前 CORS、端口与前端访问地址是否匹配

## 6. 常见排障路径

### 前端启动后访问不了 API

优先排查：

1. Gateway / Api / Auth 是否都已启动
2. `VITE_API_BASE_URL` 或运行时配置是否仍指向正确入口
3. Gateway 转发和 CORS 是否与当前访问地址一致

### 登录成功后跳转异常

优先排查：

1. `OpenIddict:Server:Issuer` 是否与实际公开入口一致
2. Gateway 是否正确转发 `/Account/*` 与 `/connect/*`
3. 当前客户端回调地址是否仍在官方客户端配置中

### 前端共享包改完没生效

优先排查：

1. 根目录是否执行过 `npm install`
2. 当前是否从正确 workspace 启动了开发服务器
3. 是否需要重启当前 Vite 进程

### 数据库行为与预期不一致

优先排查：

1. 当前是否仍在使用默认 SQLite
2. `appsettings.Local.json` 或环境变量是否覆盖了连接串
3. 当前改动是否应同步数据库结构治理文档

### SQLite 本地并发读写异常

本地 SQLite 场景下，`BaseRepository` 会按“连接配置 + 连接串”串行化执行同一 SQLite 文件上的通用仓储操作，覆盖插入、更新、删除 / 软删除、恢复、列表查询、分页查询、数量查询、存在性检查、联查、分表查询、去重和聚合等基础入口。这个策略用于降低默认 SQLite + Hangfire 后台任务或本地测试并发读写时出现 `database is locked`、`reader is closed / FieldCount when reader is closed` 以及连接生命周期异常的概率；它不改变 PostgreSQL 等外部数据库的异步查询路径，也不应被视为生产高并发数据库方案。

若仍在本地 SQLite 中观察到类似异常，优先确认：

1. 相关读写是否经过 `BaseRepository` 通用入口，或子类仓储自定义 SQLSugar 操作是否复用了 `ExecuteDbOperationAsync`。
2. 是否仍存在 Service 层绕过仓储直接访问 `Db.Queryable` / `ISqlSugarClient`。
3. 是否有 Hangfire 后台任务、前台请求或测试用例同时高频读写同一 SQLite 文件。
4. 是否已执行最新 `DbMigrate apply`，并重启相关宿主确认连接初始化已重新生效。
5. 若异常来自持续写入、多实例竞争或接近生产流量的压测，应优先切换 PostgreSQL，而不是继续扩大 SQLite 兜底。

## 7. 接手某个问题时的阅读顺序

1. [快速开始](/guide/getting-started)
2. [架构总览](/architecture/overview)
3. [开发规范](/architecture/specifications)
4. [API 说明索引](/guide/api-index)
5. [数据库总览](/guide/database-overview)
6. [当前进行中](/planning/current)

## 相关文档

- [快速开始](/guide/getting-started)
- [API 说明索引](/guide/api-index)
- [数据库总览](/guide/database-overview)
- [验证基线](/guide/validation-baseline)
