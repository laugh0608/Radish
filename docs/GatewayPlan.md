# Gateway 改造方案

> 目标：在现有 Radish 分层架构上增加统一 API Gateway，使前端、后端及其他项目通过单一入口完成认证、路由、聚合、治理等能力。本方案参考 Blog.Core 的 Gateway 实践，但结合项目当前阶段做了简化与优化建议，作为未来迭代的行动指南。

## 1. 背景与定位

- 统一入口：前端、其他服务及第三方调用仅访问 Gateway，内部服务保持私有网络暴露。
- 统一认证：在网关层集中处理登录、Token 签发与校验，后端服务只验证来自 Gateway 的 Token/Claims。
- 统一治理：路由、限流、熔断、监控、日志、审计、协议转换（REST ↔ GraphQL/SignalR）均在 Gateway 层串联。
- 渐进改造：短期仍由 `Radish.Api` 承担业务控制器，Gateway 只做转发；中长期可逐步把 API 项目拆成内部服务，由 Gateway 暴露聚合 API。

## 2. 技术选型

| 选型 | 推荐程度 | 原因 | 注意事项 |
| --- | --- | --- | --- |
| Ocelot | ★★★★☆ | 配置驱动、生态成熟、Blog.Core 同款、与 ASP.NET Core Host 融合 | JSON 配置体积较大，需要拆分/热更新策略 |
| YARP | ★★★☆☆ | 更灵活、可自定义中间件、天然支持 .NET 10 | 需要较多编码（Policy/Transform）才能达到 Ocelot 级别的快速配置 |
| APIM/外部网关 | ★★☆☆☆ | 托管能力强，省去自建 | 成本高、无法本地一体化、与现阶段目标不符 |

推荐优先创建 `Radish.Gateway` 项目，引入 `Ocelot`（后续可视需求替换为 YARP），与 `Radish.Api` 并列部署。

## 3. 目标架构

```
Clients ──> Radish.Gateway (Ocelot + Auth + Observability)
                    │
         ┌──────────┴──────────┐
      Radish.Api          其他内部服务
         │                        │
   Radish.Service/...        外部系统/后端
```

Gateway 负责：

1. HTTP/HTTPS 入口、证书、CORS。
2. 登录/刷新接口（可调用 `Radish.Service` 或独立 Identity 服务）。
3. 路由至下游（Radish.Api、radish.identity、未来微服务），并转发 Header/Trace。
4. 聚合接口：如“统一登录态 + 配置 + 通知”一次返回。
5. 熔断/重试/限流、日志、指标、Trace。

## 4. 实施阶段与清单

| 阶段 | 目标 | 关键动作 | 产出/验收 |
| --- | --- | --- | --- |
| P1 基线 | 独立 Gateway 项目跑通 | - `dotnet new web` → `Radish.Gateway`<br>- 引入 `Ocelot`、`Serilog`、`HealthChecks`<br>- 复制基础配置加载/日志模式<br>- `ocelot.Development.json` 配置示例路由（传透到 `Radish.Api`） | `dotnet run --project Radish.Gateway`，调用 `/gateway/health` 成功且可通过 Gateway 访问 `Radish.Api` 既有接口 |
| P2 认证 | 网关成为唯一身份验证入口 | - 在 Gateway 暴露 `/auth/login`、`/auth/refresh`，调用现有 Auth 服务或 `Radish.Service`<br>- 配置 `AddAuthentication().AddJwtBearer()` + Ocelot `AuthenticationOptions`<br>- 下游统一信任 Gateway 签发的 Token | 前端仅调用 Gateway 完成登录/续期；`Radish.Api` 不再暴露登录接口 |
| P3 路由与聚合 | 按业务域拆分路由并实现典型聚合 | - `ocelot.json` 按模块（身份/内容/积分/商城）组织 Route<br>- 使用 Ocelot Aggregates 或 Gateway Controller 聚合常用数据<br>- 引入 `Polly`、`RateLimitOptions`、`QoSOptions` | 聚合接口返回多下游数据；关键接口具备超时/熔断/限流 |
| P4 服务发现 | 对接 Consul/自建注册中心 | - Gateway 通过 `ocelot.json`/Consul 自动感知下游实例<br>- 下游服务注册心跳<br>- 配置健康探测/权重 | 下游地址不再写死，扩缩容无需改配置 |
| P5 可观测性 | 完善监控与治理 | - 集成 OpenTelemetry、Prometheus 指标、集中日志<br>- TraceId/CorrelationId 透传 Gateway → 下游<br>- 审计 Gateway 的管理接口（如动态路由更新） | 日志/指标/Trace 可关联；攻防/流量事件可定位 |
| P6 渐进迁移 | 内部服务分拆 | - 新建独立服务（积分、商城、通知等），Gateway 维护路由<br>- `Radish.Api` 收敛为内部服务<br>- 编写迁移手册与回滚策略 | 前端永远指向 Gateway；多后端服务并存但统一入口 |

> 阶段 P1-P3 可作为近期目标，P4-P6 列入中长期规划，随实际演进更新。

## 5. 详细任务列表

1. **项目初始化（P1）**
   - [ ] `Radish.Gateway/Radish.Gateway.csproj`：启用 `net10.0`、Nullable、ImplicitUsings；引用 `Ocelot`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `HealthChecks`.
   - [ ] `Program.cs`：对齐 `Radish.Api` 的配置顺序（UserSecrets、Environment、CommandLine），注册 `AddOcelot()`, `AddEndpointsApiExplorer()`, `AddSwaggerGen()`（用于自查），并注入 `UseSerilog`.
   - [ ] 配置文件：`appsettings.{Env}.json` 继承日志、CORS、JWT 配置段；新增 `Gateway:BaseAddress`, `Gateway:Upstreams`.
   - [ ] `ocelot.Development.json`：至少包含 `Radish.Api` 的健康检查、登录、内容 API 路由示例。
   - [ ] 文档/脚本：更新 `local-start.(ps1|sh)` 以便一键启动 Gateway。

2. **认证链路（P2）**
   - [ ] 引入共享认证逻辑（可复用 `Radish.Service.Auth`）到 Gateway 控制器或中间层。
   - [ ] 统一密钥：Gateway 负责签发 Access/Refresh Token，`Radish.Api` 验证签名即可。
   - [ ] Ocelot 配置 `AuthenticationOptions` + `RouteClaimsRequirement`，确保所有受保护 API 走网关验证。
   - [ ] 登录限流、验证码、IP 黑名单等安全策略集中于 Gateway。

3. **路由/聚合（P3）**
   - [ ] `Routes` 以业务域命名（`/api/content/*`, `/api/identity/*`, `/api/commerce/*`）。
   - [ ] 通过 `DownstreamHostAndPorts` 或 `ServiceName`（Consul）指向多个实例。
   - [ ] 使用 `AggregateRoutes` 或自定义 Controller（`/gateway/bootstrap`）一次返回：用户信息、菜单、通知、前端配置。
   - [ ] 基于 `QoSOptions` 配置熔断窗口、重试次数、超时时间；`RateLimitOptions` 配 IP/ClientId 限流。
   - [ ] 统一将 `TraceId`, `UserId`, `ClientId`, `Forwarded-*` Header 注入下游。

4. **服务发现与配置治理（P4）**
   - [ ] 集成 Consul（或其他注册中心）客户端，Gateway 读取 `ServiceName` + `DownstreamScheme`.
   - [ ] 通过 Consul KV/配置中心托管 `ocelot.json`，实现热更新；提供内置管理接口更新路由。
   - [ ] 健康检查：Gateway 定期探测下游 `/health`，自动摘除失败实例。

5. **可观测性（P5）**
   - [ ] Serilog 输出 Gateway 访问日志（含上游 IP、下游地址、耗时、状态码）。
   - [ ] OpenTelemetry：采集 HTTP Server + HTTP Client + 自定义事件，并将 TraceId 写入响应 Header。
   - [ ] Prometheus 指标：QPS、成功率、延迟、限流/熔断触发次数。
   - [ ] 审计网关配置变更、登录事件，将审计日志写入 `Radish.Repository`.

6. **渐进迁移（P6）**
   - [ ] 拆分 `Radish.Api` 中的业务模块为独立服务（积分、商城、通知等），提供 `docker-compose` 与文档指引。
   - [ ] Gateway 文档中维护“服务清单 + 协议 + 版本”表，供前端/第三方对接。
   - [ ] 编写回滚策略：当 Gateway 故障或配置异常时，可临时回退到 `Radish.Api` 直连模式。

## 6. 技术难点与对策

| 难点 | 说明 | 对策 |
| --- | --- | --- |
| 统一认证 | 登录 Token 需统一签发、续期、吊销 | 抽象 `IIdentityProvider`，Gateway 持有私钥签发，配置 `TokenValidationParameters` 统一下发；刷新逻辑由 Gateway 控制，后端仅校验。 |
| 配置膨胀 | `ocelot.json` 容易冗长 | 拆分为 per-environment + per-domain 文件，并在 Program 中 `AddOcelot(builder.Configuration)`；或迁移到 Consul KV。 |
| 聚合性能 | 同时调用多个下游导致延迟 | 并发调用 + 超时/熔断策略，必要时缓存；对耗时长接口可通过后台任务预聚合。 |
| 观测与定位 | 请求经过多跳，排障困难 | 强制使用 CorrelationId（如 `X-Request-Id`）并透传；日志包含上下游地址与耗时。 |
| 安全 | Gateway 暴露公网，需要抗压 | 配置 WAF/反向代理白名单、速率限制、IP 黑名单；敏感接口二次校验（验证码/人机）。 |
| 部署 | Gateway 成为单点 | 至少部署两个副本，并在负载均衡器中做健康检查；预留滚动更新策略。 |

## 7. 与现有计划的衔接

- 短期（M1-M4）继续专注现有框架与业务实现，但在设计 DTO、认证与日志时预留 Gateway 需求（如 Header 透传、Token 结构）。
- 自 M5 起可安排专项迭代，将 P1-P3 纳入 DevelopmentPlan 的后续周次（例如 M9+），并在 DevelopmentLog 中记录每次推进。
- 文档更新：本文件与 `DevelopmentFramework.md`、`DevelopmentPlan.md` 同步，确保所有贡献者知道 Gateway 是后续重点。
- 脚本/配置：在 Gateway 工作启动前，先评估 `local-start.*`、CI Pipeline 需要的变更，避免临时阻断日常开发。

> 本方案将随着实现进度持续补充。每次关键决策（选型切换、阶段完成）需在 `DevelopmentLog.md` 记录，并在 `DevelopmentFramework.md` 标注最新状态。
