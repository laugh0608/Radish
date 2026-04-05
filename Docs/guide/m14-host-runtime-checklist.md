# M14 宿主运行首轮执行清单

> 本页是 `M14` 的首轮执行入口，负责把宿主运行、自检、排障和最小部署复核收束成同一份可执行清单。
>
> 阶段定义见：[M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)

## 适用范围

以下场景优先从本页开始：

- 刚进入 `M14`，需要明确第一轮到底做什么
- `DbMigrate`、宿主配置、连接定义或部署变量刚发生变化
- `npm run validate:baseline:host` 失败，需要按统一顺序分诊
- 测试部署或生产部署完成后，需要做最小运行态复核

## 第一轮默认执行顺序

### 1. 先跑统一宿主验证入口

默认先执行：

```bash
npm run validate:baseline:host
```

它当前承担的是：

- 默认验证基线
- `DbMigrate doctor`
- `DbMigrate verify`

第一轮目标不是一次性覆盖所有联调，而是先回答三个问题：

- 当前仓库与宿主配置是否至少还能通过最小 build/test
- 当前环境是否具备最小启动前提
- 当前失败更像是代码回归、配置问题，还是宿主运行问题

当前默认主路径已经固定为两段：

1. 启动前先执行 `npm run validate:baseline:host`
2. 宿主启动后再执行 `npm run check:host-runtime`

这样可以先把“代码/配置/数据库前置”与“运行态/网关链路”明确分层，不再混在同一轮里排障。

如果需要把启动前这一轮验证结果直接回写到维护记录或本地留痕，可直接运行：

```bash
npm run validate:baseline:host -- --report
```

如果需要直接落到文件，可直接运行：

```bash
npm run validate:baseline:host -- --report-file .tmp/baseline-host-report.md
```

### 2. 若失败，优先按“失败归类”拆开

不要一上来就直接重配环境或重启所有宿主，先确认失败属于哪一类：

- `type-check / build / test` 失败：先按默认代码回归处理
- `doctor` 失败：优先看配置、连接定义、关键 `ConnId`
- `verify` 失败：优先看数据库前置、种子状态、表结构或缺列
- 宿主可启动但页面异常：优先看健康检查、日志与网关链路
- 部署后外部访问异常：优先看 `RADISH_PUBLIC_URL`、反代头和 OIDC 回调链

## 宿主排障顺序

### 第 1 步：确认配置前提

优先确认：

- `MainDb` / `Databases` 是否存在且至少包含 `Main`、`Log`
- 当前环境是否加载了预期的 `appsettings.*`
- `Snowflake.WorkId`、`Redis`、证书路径等关键配置是否为当前环境真实值
- 测试 / 生产部署时，`RADISH_PUBLIC_URL` 是否与真实访问地址一致

如果这一层不成立，先修配置，不要直接把问题归因到宿主代码。

### 第 2 步：确认 `doctor` 结论

优先关注：

- 连接定义是否完整
- 关键 `ConnId` 是否缺失
- 配置是否可被当前宿主正确读取

结论判断：

- `doctor` 失败，通常先停在配置层处理
- `doctor` 通过，说明最小配置骨架基本成立，可以继续往下看

### 第 3 步：确认 `verify` 结论

优先关注：

- 数据库文件 / 连接是否真实可访问
- 关键表结构、缺列或种子前置是否满足
- 文档 / Wiki 这类依赖历史迁移补齐的链路是否仍缺前置

结论判断：

- `verify` 失败，优先按数据库与迁移前置处理
- `verify` 通过，说明当前环境至少具备最小运行前提

### 第 4 步：确认宿主健康检查与启动日志

当 `doctor` / `verify` 已通过，但宿主仍异常时，优先看：

- `npm run check:host-runtime`
- `/health`
- `/healthz`
- `Logs/{ProjectName}/Log.txt`
- `Logs/{ProjectName}/AopSql/AopSql.txt`
- 宿主启动期异常、证书加载错误、Issuer 不匹配、端口占用或反代头异常

这一层的目标是先判断：

- 宿主是否真的启动成功
- 异常发生在启动期、请求期还是反代转发期
- 是单宿主问题，还是 Gateway 串联问题
- 运行态检查是否已经明确落到 `Gateway / Api / Auth` 其中某一层

当前 `check:host-runtime` 失败时会直接归类为“端口未监听 / TLS / 超时 / 宿主已启动但健康未通过 / HTTP 状态异常”等类型，先按脚本给出的分类处理，不要一上来就把所有情况都混成“Gateway 有问题”。

当前 `validate:baseline:host` 也已补启动前分流提示：

- 若失败落在默认基线：先修代码回归，再回到 `validate:baseline:host`
- 若失败落在 `doctor`：先修配置、连接定义与关键 `ConnId`
- 若失败落在 `verify`：先修数据库前置、缺列/表结构或种子状态

只有这三层通过后，才建议进入 `check:host-runtime` 做启动后分诊。

当前其报告输出也已与运行态脚本对齐，统一使用 `Summary / Actions` 两段，便于把启动前与启动后记录放进同一份维护材料。

如果希望在失败时顺手把失败宿主的 `/healthz` 关键条目摘要一起打出来，可直接运行：

```bash
npm run check:host-runtime -- --details
```

如果需要把当前检查结果直接回写到维护记录、PR 或回归记录，可直接运行：

```bash
npm run check:host-runtime -- --report
```

如果需要直接把报告落到文件，避免再从终端复制，可直接运行：

```bash
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
```

当前 `--report` 输出已拆为：

- `Summary`：保留本轮检查事实与 `Gateway /healthz` 摘要
- `Actions`：按 `Gateway / Api / Auth` 或具体降级条目直接给出下一步建议

如果既要看终端分诊摘要，又要输出固定格式报告，可组合执行：

```bash
npm run check:host-runtime -- --details --report
```

如果既要看终端分诊摘要，又要把固定格式报告直接写入文件，可组合执行：

```bash
npm run check:host-runtime -- --details --report-file .tmp/host-runtime-report.md
```

当前 `Gateway` 端点语义也已拆开：

- `/health`：最小后端宿主链，优先用于 `M14` 默认检查
- `/healthz`：更完整的下游观测，适合继续判断 `console` 等扩展下游状态；当前已返回结构化 JSON，可直接看到整体状态、时间戳、耗时和每个下游条目的状态、标签与异常摘要
- `Api / Auth` 当前也保持同样的分层语义：`/health` 只保留最小存活探活，`/healthz` 继续补 JWT / OIDC Issuer 与证书前提明细

当前 `Api / Auth / Gateway` 的启动日志也会额外打印 JWT / OIDC / 下游探活目标运行摘要，便于在看健康检查之前先核对当前宿主到底按哪套运行模式启动。

### 第 5 步：确认网关与外部访问链路

如果宿主本身正常，但外部访问仍异常，优先确认：

- Gateway 到 Api/Auth 的转发是否正常
- 外层反代是否保留 `Host`、`X-Forwarded-Proto`、`X-Forwarded-Host`
- `OpenIddict__Server__Issuer` 是否与 `RADISH_PUBLIC_URL` 同步
- `radish-client / radish-console / radish-scalar` 的回调入口是否仍落在当前公开地址下

补充说明：

- `Gateway` 当前仍会观测 `console-service`，但在本地仅验证后端宿主最小链路时，`console` 未启动默认只记为 `Degraded`，不再把整条宿主健康检查直接打成 `Unhealthy`

## 部署后最小复核动作

### 测试部署

至少确认：

- `docker compose ... config` 可静态展开
- `docker compose ... pull` 与 `up -d` 无阻塞错误
- `dbmigrate -> api/auth -> gateway` 顺序正常
- `/health` 可访问
- `/`、`/console/`、`/scalar` 可打开
- 登录、回调、登出至少有一轮真实验证

### 生产部署

除了测试部署的最小动作，还应额外确认：

- `RADISH_PUBLIC_URL` 与真实外部 HTTPS 域名完全一致
- 外层 Nginx / Traefik / Caddy 反代头未丢失
- Auth 证书路径、密码与挂载目录真实可用
- OIDC 回调、`userinfo` 与受保护接口至少完成一轮真实访问

## 首轮完成标准

当以下条件同时成立时，可认为 `M14` 第一轮执行入口已经走通：

- 已有统一入口文档，不再靠口头说明决定先跑什么
- `validate:baseline:host` 已被明确为默认宿主验证入口
- `doctor` / `verify` / 健康检查 / 日志 / 部署复核之间的顺序已经固定
- 测试部署与生产部署都能回到同一套最小排障骨架
- 后续若继续拆任务，能够明确知道是在补哪一层，而不是重新发明阶段定义

## 关联入口

- [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)
- [验证基线说明](/guide/validation-baseline)
- [部署与容器指南](/deployment/guide)
