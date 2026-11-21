# 统一日志 Console 与退出联验（2026-11-21）

本批从 `cad92b69` 开始。组装环境为 OrbStack / Linux arm64、Fluent Bit 5.1.2 固定候选、PostgreSQL 16.15 和 Redis 7.4.12。旧版以 `v26.8.1-release` 发布记录的五个镜像索引解析 arm64 manifest，新版用当前源码和缓存基础镜像打包。Compose 预检只允许 daemon Forward 回环 `127.0.0.1:32424` 和 Gateway HTTPS `https://localhost:35443`。首次启动前核对固定镜像、挂载范围和端口。实验账户来自隔离库的开发夹具。[发布记录](./m15-release-record-v26.8.1-2026-08-15.md#已发布制品)

## 实施和离线回归

运行日志页支持来源、级别、时间、事件码及关联字段筛选，提供受保护的续页游标、安全详情、CSV 审计和历史只读摘要。采集状态及持久告警支持公共租户的 Console 资源门禁、同故障去重、版本并发保护和处置轨迹。启用参数见专题。慢 SQL 同语句聚合等待稳定的安全指纹。[查询和告警契约](../features/unified-logging-governance-design.md#首轮查询和告警入口)

35 项 .NET 定向测试覆盖 JwtBearer 默认令牌处理器、运行日志快照、终态事件重放与操作键去重、采集告警确认前后的恢复条件及既有 L3 接收。15 项 Node 定向测试覆盖满卷和 chunk 恢复断言。前端缓存构建、受影响 Console 脚本 lint 和四层权限扫描通过。

候选 API 曾因加载 `JsonWebTokens 8.0.1` 与 `Tokens 8.14.0` 组合，在读取令牌时抛出 `MissingMethodException`。`Radish.Api` 直接引用精确版本 `Microsoft.IdentityModel.JsonWebTokens [8.14.0]`。只用仓库已有 NuGet 缓存解析并构建，核对 API 的 assets 和运行依赖清单中两项版本均为 `8.14.0`。PostgreSQL 联验另复现分页快照 SQL 的 `xmin` 引用报错，改为显式使用 `runtime_log_event.xmin`，保留原快照边界。

```bash
dotnet restore Radish.slnx --source "$PWD/.nuget/packages" --packages "$PWD/.nuget/packages" -p:NuGetAudit=false
dotnet build Radish.slnx --no-restore -c Debug
```

## 隔离联验结果

最终运行 `radish-logging-exit-51c03ba6` 返回 `passed`。

旧版通过真实 OIDC 登录。在切换前捕获该次登录的原始 bearer 并关闭浏览器页面，升级后用同一 bearer 请求候选 API，返回 HTTP 200 且账号标识一致。停写后的升级前锚点包含六库备份、应用卷、Redis AOF 和旧日志目录。两项 Log migration 通过 `apply`、`verify` 和二次 `apply`，旧表及样本仍可查。

候选 PC（1920 × 1080 CSS 像素）和 mobile（390 × 844 CSS 像素）均使用全新浏览器会话登录。查询首屏、详情、游标续页、越界拒绝、CSV 导出、旧日志和审计摘要通过。委派角色可查日志，访问告警和撤销查询权限后均返回 HTTP 403。未登录访问告警返回 HTTP 401，Log 库断连时查询返回 HTTP 503。

满卷阶段复用 24 MiB 共享 tmpfs，两路各限 4 MiB。在同一 Forward 连接向已有 chunk 发两波共 640 条合成事件。原生存储返回 ENOSPC。两路共有 167 个已交付键，输入写失败增量为 1，创建失败增量为 0。默认级别捕获独立错误，未观测尾段数量为未知。终态重复投递和同操作只生成一个告警，满卷窗口也只生成一个告警。mobile 点击确认返回 HTTP 200，PC 用确认前的版本重试返回 HTTP 409。PC 和 mobile 均显示待确认、已确认和已解决。释放空间后新发两条各自出现在 JSONL 和 Log 库中，缓冲归零后自动恢复，同一告警保留人工确认与系统恢复两条有序轨迹。

候选在停写后将快照恢复到新 Log 逻辑库和独立文件卷，校验 219 条事件的键与内容哈希、持久告警、两条处理轨迹、JSONL、缓冲和采集配置。旧版运行镜像在前向 schema 上回滚并完成 OIDC 登录，新表、事件、历史样本与告警保留。

首轮的候选新令牌与跨升级旧令牌均返回 HTTP 401。该轮的失败报告 SHA-256 为 `588bfa55f5d0623ed7f2aed6b44384cea9c063453d4fbf14c32983ef32099ff4`。依赖对齐后的诊断轮确认跨升级旧令牌返回 HTTP 200 且账号标识一致，PC / mobile 候选登录也已通过。该轮查询随后返回 HTTP 503，隔离 PostgreSQL 将原因定位到上述系统列引用。

## 启动与清理

```bash
node Scripts/logging/collector-compose-exit-probe.mjs --plan
node Scripts/logging/collector-compose-exit-probe.mjs --run "$cached_playwright_module"
```

运行入口读取已核验的缓存镜像和已构建的工作区产物。通过本机缓存的 Playwright 与 Chromium 启动隔离配置。需要指定已有 Chromium 可执行文件时通过 `RADISH_EXIT_BROWSER_EXECUTABLE` 传入。每轮按 Compose 项目标签清理容器、网络、卷、候选镜像标签、临时证书和备份。保留输出于本机 `output/playwright/` 下的对应运行目录。

最终报告和八张关键截图保存在 `output/playwright/radish-logging-exit-51c03ba6/`。其中 `report.json` 的 SHA-256 为 `477d6b39e8f5e364dc3697263a0e3acf408ec81e480e057cdb41c9ef059f229e`，`SHA256SUMS` 的 SHA-256 为 `ad09f4cf0591e7787bab70ae4abdbebc19f97caa5576c05e9e80cf930ed8ca39`。逐项校验通过，独立复核确认本轮项目标签下的资源、临时目录与两个回环监听均已清理。

## 剩余门禁

- 实际部署按目标架构复核七类来源的镜像 digest、挂载文件系统和容量、Docker daemon 到采集端的路径、停写排空与备份顺序。正式产物和目标部署仍需各自的验收证据。[部署预检](../features/unified-logging-l3-ingest-plan.md#l1-l3-compose-preflight)
- 普通运行日志首次同步前的丢失窗口沿用已确认的裁决。故障尾段保持未知，宿主掉电与实际文件系统的行为单独留证。[首次同步与块恢复](./unified-logging-l1-l3-storage-recovery-2026-11-19.md)
- 慢 SQL 同语句聚合等待安全指纹。日志退出按现有范围做阶段评审，并在确认后接续 Native。[收口范围](../features/unified-logging-governance-design.md#closeout-batches)
