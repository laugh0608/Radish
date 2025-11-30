# Gateway 改造方案

> 目标：在现有 Radish 分层架构上增加统一 API Gateway，使前端、后端及其他项目通过单一入口完成认证、路由、聚合、治理等能力。本方案参考 Blog.Core 的 Gateway 实践，但结合项目当前阶段做了简化与优化建议，作为未来迭代的行动指南。

> **当前状态**：根据 2025-11-27 的最新决策，Gateway 项目将**提前启动**，首要任务是将 `Radish.Api` 中的服务欢迎页面（Razor Pages）抽离至独立的 `Radish.Gateway` 项目。这一调整旨在实现关注点分离，让 API 项目专注于提供接口服务，同时为后续的路由转发、统一认证等功能打下基础。完整的 Gateway 功能（P1-P3）仍按原计划在多服务需求明确后实施。

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

### Phase 0：门户页面抽离（优先启动）

**目标**：将 `Radish.Api` 中的服务欢迎页面独立为 `Radish.Gateway` 项目，实现职责分离并为后续 Gateway 功能奠定基础。

**背景**：
- 当前 `Radish.Api/Pages/Index.cshtml` 承载了服务欢迎页、健康检查展示、API 文档入口等功能
- 这些功能属于"服务门户"性质，与 API 接口职责混合在一起
- 抽离后 `Radish.Api` 可以纯粹作为 REST API 服务，Gateway 承担门户展示职责

**关键动作**：
1. **创建 Radish.Gateway 项目**
   - 使用 `dotnet new web -n Radish.Gateway -f net10.0` 创建轻量级 Web 项目
   - 添加到 `Radish.slnx` 解决方案
   - 引用必要的依赖：`Serilog.AspNetCore`、`Microsoft.Extensions.Diagnostics.HealthChecks`

2. **迁移欢迎页面**
   - 将 `Radish.Api/Pages/` 目录（包括 `Index.cshtml`、`_ViewImports.cshtml`）迁移至 `Radish.Gateway/Pages/`
   - 将 `Radish.Api/wwwroot/` 静态资源（css、js）迁移至 `Radish.Gateway/wwwroot/`
   - 保留 `Radish.Api/wwwroot/scalar/` 目录（Scalar API 文档仍由 API 项目提供）

3. **配置 Gateway 项目**
   - 复用 `Radish.Extension.SerilogExtension` 和 `Radish.Common.AppSettingsTool` 实现日志和配置
   - 注册 Razor Pages 和静态文件服务
   - 配置健康检查端点（聚合 API 服务的健康状态）
   - 监听端口：`https://localhost:5000` 和 `http://localhost:5001`

4. **清理 Radish.Api**
   - 移除 `Pages/` 目录和相关 Razor Pages 配置
   - 移除 `MapRazorPages()` 和 `MapFallbackToPage("/Index")` 调用
   - 保留 Scalar API 文档功能（`/api/docs` 路由）
   - API 项目回归纯接口服务定位

5. **更新启动脚本**
   - 修改 `start.ps1` 和 `start.sh`，添加"启动 Gateway"选项
   - 提供"同时启动 Backend + Gateway"的组合选项
   - 更新文档说明新的访问入口

**产出/验收**：
- [x] `Radish.Gateway` 项目成功创建并加入解决方案
- [x] 访问 `https://localhost:5001/` 显示服务欢迎页面（原 API 首页内容）
- [x] 欢迎页面上的健康检查功能正常，能检测到 `Radish.Api` 的健康状态
- [x] 欢迎页面上的"打开项目文档"和"Scalar 可视化"链接正确跳转到 API 服务
- [x] `Radish.Api` 项目移除所有 Razor Pages 相关代码，启动后仅提供 API 服务
- [x] 启用 YARP 路由，在本地开发环境下通过 Gateway 暴露 `/docs` 在线文档入口，并可按需代理到 radish.docs dev 服务
- [x] 启用统一入口路由规划（`/`、`/docs`、`/api`、`/scalar`、`/console` 等），为后续完整 Gateway/BFF 能力预留路径空间
- [x] 启动脚本更新完成（`start.ps1`/`start.sh`），可交互式启动 Gateway、API、前端、文档站与控制台
- [x] 文档更新完成（CLAUDE.md、DevelopmentSpecifications.md、DevelopmentFramework.md）
- [x] **2025-11-27 增强**：门户页面配置化改造，支持生产环境部署
  - [x] 修复 URL 溢出问题，优化卡片布局
  - [x] 实现从配置文件读取服务 URL（新增 `GatewayService.PublicUrl` 配置项）
  - [x] 创建 `appsettings.Production.example.json` 生产环境配置模板
  - [x] JavaScript 健康检查 URL 动态化

**设计决策**：
1. **不引入 Ocelot**：Phase 0 阶段不实现路由转发功能，Gateway 仅作为静态门户页面承载
2. **复用基础设施**：共享 `Radish.Extension` 和 `Radish.Common` 中的日志、配置工具
3. **最小依赖**：只引入 Razor Pages、静态文件、健康检查等必要功能
4. **渐进式架构**：为后续 P1-P3 阶段预留扩展空间，但当前不增加复杂度

**预计工作量**：
- 项目创建与配置：0.5 天
- 页面与资源迁移：0.5 天
- 健康检查聚合实现：0.5 天
- 脚本与文档更新：0.5 天
- **总计**：2 天

---

| 阶段 | 目标 | 关键动作 | 产出/验收 |
| --- | --- | --- | --- |
| P1 基线 | 独立 Gateway 项目跑通（路由转发） | - 在 Phase 0 基础上引入 `Ocelot`<br>- 配置 `ocelot.Development.json` 路由规则<br>- 实现 API 请求转发到 `Radish.Api` | 通过 Gateway 访问 `Radish.Api` 既有接口，所有 REST API 请求正常转发 |
| P2 认证 | 网关成为唯一身份验证入口 | - 在 Gateway 暴露 `/auth/login`、`/auth/refresh`，调用现有 Auth 服务或 `Radish.Service`<br>- 配置 `AddAuthentication().AddJwtBearer()` + Ocelot `AuthenticationOptions`<br>- 下游统一信任 Gateway 签发的 Token<br>- **详见** [AuthenticationGuide.md](AuthenticationGuide.md) 了解 OIDC 技术实现 | 前端仅调用 Gateway 完成登录/续期；`Radish.Api` 不再暴露登录接口 |
| P3 路由与聚合 | 按业务域拆分路由并实现典型聚合 | - `ocelot.json` 按模块（身份/内容/积分/商城）组织 Route<br>- 使用 Ocelot Aggregates 或 Gateway Controller 聚合常用数据<br>- 引入 `Polly`、`RateLimitOptions`、`QoSOptions` | 聚合接口返回多下游数据；关键接口具备超时/熔断/限流 |
| P4 服务发现 | 对接 Consul/自建注册中心 | - Gateway 通过 `ocelot.json`/Consul 自动感知下游实例<br>- 下游服务注册心跳<br>- 配置健康探测/权重 | 下游地址不再写死，扩缩容无需改配置 |
| P5 可观测性 | 完善监控与治理 | - 集成 OpenTelemetry、Prometheus 指标、集中日志<br>- TraceId/CorrelationId 透传 Gateway → 下游<br>- 审计 Gateway 的管理接口（如动态路由更新） | 日志/指标/Trace 可关联；攻防/流量事件可定位 |
| P6 渐进迁移 | 内部服务分拆 | - 新建独立服务（积分、商城、通知等），Gateway 维护路由<br>- `Radish.Api` 收敛为内部服务<br>- 编写迁移手册与回滚策略 | 前端永远指向 Gateway；多后端服务并存但统一入口 |

> 阶段 P1-P3 可作为近期目标，P4-P6 列入中长期规划，随实际演进更新。

## 5. 详细任务列表

### Phase 0：门户页面抽离任务清单

**0.1 项目创建**
- [ ] 在解决方案根目录执行 `dotnet new web -n Radish.Gateway -f net10.0`
- [ ] 执行 `dotnet sln Radish.slnx add Radish.Gateway/Radish.Gateway.csproj`
- [ ] 添加项目引用：
  ```bash
  cd Radish.Gateway
  dotnet add package Serilog.AspNetCore --version 8.0.0
  dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks --version 10.0.0
  dotnet add reference ../Radish.Common/Radish.Common.csproj
  dotnet add reference ../Radish.Extension/Radish.Extension.csproj
  ```

**0.2 页面与资源迁移**
- [ ] 创建 `Radish.Gateway/Pages/` 目录
- [ ] 复制 `Radish.Api/Pages/Index.cshtml` 到 `Radish.Gateway/Pages/`
- [ ] 复制 `Radish.Api/Pages/_ViewImports.cshtml` 到 `Radish.Gateway/Pages/`
- [ ] 修改 `_ViewImports.cshtml` 的命名空间为 `Radish.Gateway.Pages`
- [ ] 创建 `Radish.Gateway/wwwroot/` 目录
- [ ] 复制 `Radish.Api/wwwroot/css/` 到 `Radish.Gateway/wwwroot/css/`
- [ ] 复制 `Radish.Api/wwwroot/js/` 到 `Radish.Gateway/wwwroot/js/`
- [ ] 保留 `Radish.Api/wwwroot/scalar/`（不迁移）

**0.3 配置 Gateway Program.cs**
- [ ] 配置加载（对齐 Radish.Api）
- [ ] 注册 `AppSettingsTool` 服务
- [ ] 注册 Serilog 日志
- [ ] 注册 Razor Pages 和静态文件服务
- [ ] 配置健康检查端点
- [ ] 添加 HttpClient 服务（用于检测下游 API 健康状态）
- [ ] 配置监听端口：`https://localhost:5001` 和 `http://localhost:5000`

**0.4 实现健康检查聚合**
- [ ] 修改 `Index.cshtml` 的健康检查 JavaScript，指向 `Radish.Api` 的健康端点
- [ ] 更新页面链接，确保文档和 Scalar 链接指向正确的 API 服务地址
- [ ] （可选）在 Gateway 添加 `/health` 端点，聚合显示自身和下游 API 的健康状态

**0.5 清理 Radish.Api**
- [ ] 删除 `Radish.Api/Pages/` 目录（除了可能存在的其他必要页面）
- [ ] 删除 `Radish.Api/wwwroot/css/` 和 `Radish.Api/wwwroot/js/`（保留 `scalar/`）
- [ ] 在 `Program.cs` 中移除 `builder.Services.AddRazorPages()`
- [ ] 在 `Program.cs` 中移除 `app.MapRazorPages()` 和 `app.MapFallbackToPage("/Index")`
- [ ] 移除 `app.UseDefaultFiles()` 调用（如果仅为首页服务）
- [ ] 保留 `app.UseStaticFiles()`（用于 Scalar 静态资源）

**0.6 更新启动脚本**
- [ ] 更新 `start.ps1`，添加"3. Start Gateway"选项
- [ ] 添加"4. Start Backend + Gateway"组合选项
- [ ] 添加"5. Start all (frontend + backend + Gateway)"全部启动选项
- [ ] 调整原有选项编号（4 → 6: Run tests）
- [ ] 更新 `start.sh`，实现相同的选项调整
- [ ] 在脚本中定义 Gateway 启动函数

**0.7 文档更新**
- [ ] 更新 `CLAUDE.md` - 添加 Gateway 项目说明和启动命令
- [ ] 更新 `DevelopmentSpecifications.md` - 添加 Radish.Gateway 项目结构说明
- [ ] 更新 `DevelopmentFramework.md` - 更新架构图和技术基线
- [ ] 更新 `GatewayPlan.md` - 添加 Phase 0 阶段说明（当前任务）
- [ ] 更新 `README.md` - 修改快速开始部分的访问地址说明

**0.8 测试与验收**
- [ ] 启动 `Radish.Api`，确认仅提供 API 服务，访问根路径返回 404
- [ ] 启动 `Radish.Gateway`，访问 `https://localhost:5001/` 显示欢迎页
- [ ] 测试欢迎页上的健康检查功能
- [ ] 测试欢迎页上的链接跳转（文档、Scalar）
- [ ] 使用 `local-start` 脚本测试各启动选项
- [ ] 确认日志输出正常，Gateway 和 API 日志可区分

---

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

## 8. Blog.Core 实践对比与参考

[Blog.Core](https://github.com/anjoy8/Blog.Core) 是成熟的 ASP.NET Core 微服务教程项目，其 Gateway 实践值得参考：

### Blog.Core Gateway 架构特点

| 特性 | Blog.Core 实现 | Radish 规划对比 | 适用性评估 |
|------|---------------|----------------|----------|
| **Gateway 项目** | 独立 `Blog.Core.Gateway` 服务 | `Radish.Gateway` 独立项目 | ✅ 完全适用 |
| **网关框架** | Ocelot + 自定义 Nacos Provider | Ocelot（官方 Provider） | ✅ 先用官方，P4 再定制 |
| **服务发现** | Nacos / Consul | Consul（规划） | ✅ 可参考集成方式 |
| **认证方案** | IdentityServer4 独立服务 | JWT 自建（短期）<br>独立 Identity（长期） | ⚠️ 需评估是否拆分 Identity |
| **配置管理** | Nacos KV 配置中心 | 文件配置（短期）<br>Consul KV（P4） | ✅ 渐进式合理 |
| **部署方式** | Docker + Jenkins CI/CD | Docker Compose + GitHub Actions | ✅ 工具链不同但思路一致 |

### 关键经验借鉴

1. **配置文件拆分策略**：Blog.Core 按环境和模块拆分 `ocelot.{env}.json` + `routes/*.json`，避免单文件过大
2. **认证令牌设计**：使用 IdentityServer4 统一签发，Gateway 和下游服务共享公钥验证
3. **服务注册约定**：每个下游服务启动时向 Nacos/Consul 注册，Gateway 自动发现
4. **监控埋点**：集成 OpenTelemetry + Prometheus，全链路 TraceId 透传

### Radish 的差异化选择

- **不急于引入 IdentityServer**：当前单体架构，自建 JWT 足够；若未来超过 3 个服务，再评估独立 Identity 服务
- **优先本地配置**：开发阶段硬编码下游地址，降低调试复杂度；生产环境再上配置中心
- **轻量级服务发现**：P4 阶段若仅 2-3 个服务，可用健康检查 + 手动配置代替 Consul

## 9. 详细实施指南

### 9.1 P1 阶段：Gateway 基础项目搭建

#### 创建项目

```bash
# 在解决方案根目录执行
dotnet new web -n Radish.Gateway -f net10.0
dotnet sln Radish.slnx add Radish.Gateway/Radish.Gateway.csproj

# 添加必要的包
cd Radish.Gateway
dotnet add package Ocelot --version 23.0.0
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks --version 10.0.0
```

#### Program.cs 最小实现

```csharp
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Radish.Common;
using Radish.Extension.SerilogExtension;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. 配置加载（对齐 Radish.Api）
builder.Host.ConfigureAppConfiguration((context, config) =>
{
    config.Sources.Clear();
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
          .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
          .AddJsonFile($"ocelot.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
          .AddEnvironmentVariables();
});

// 2. 注册共享配置工具
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));

// 3. 日志（复用 Radish.Extension）
builder.Host.AddSerilogSetup();

// 4. 健康检查
builder.Services.AddHealthChecks();

// 5. Ocelot
builder.Services.AddOcelot();

var app = builder.Build();

// 6. 中间件管道
app.MapHealthChecks("/health");
app.UseSerilogRequestLogging();
await app.UseOcelot();

Log.Information("Radish.Gateway 启动成功，监听 {Urls}", builder.Configuration["ASPNETCORE_URLS"]);
app.Run();
```

#### ocelot.Development.json 示例

```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/health",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 7110 }
      ],
      "UpstreamPathTemplate": "/api/health",
      "UpstreamHttpMethod": [ "GET" ]
    },
    {
      "DownstreamPathTemplate": "/api/{everything}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 7110 }
      ],
      "UpstreamPathTemplate": "/api/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

#### 验证步骤

```bash
# 启动后端
dotnet run --project Radish.Api/Radish.Api.csproj

# 新终端启动 Gateway
cd Radish.Gateway
dotnet run --urls="https://localhost:5000;http://localhost:5001"

# 测试健康检查
curl https://localhost:5001/api/health

# 测试转发
curl https://localhost:5001/api/User
```

### 9.2 P2 阶段：认证集成方案

#### 认证架构演进路径

```
┌─────────────────────────────────────────────────────┐
│ P2 短期方案：Gateway 共享 JWT Secret                  │
├─────────────────────────────────────────────────────┤
│  前端 → Gateway（验证 Token）→ Radish.Api（签发+验证） │
│                                                     │
│  优点：改动最小，复用现有 LoginController            │
│  缺点：职责不清，Gateway 未完全接管认证              │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ P3 中期方案：Gateway 接管登录接口                     │
├─────────────────────────────────────────────────────┤
│  前端 → Gateway（签发+验证）→ Radish.Api（仅验证）    │
│                    ↓                                │
│              Radish.Service.Auth（业务逻辑）         │
│                                                     │
│  优点：Gateway 完全接管对外认证，下游仅做校验         │
│  缺点：需要在 Gateway 中引用 Service 层              │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ P4+ 长期方案：独立 Identity 服务                      │
├─────────────────────────────────────────────────────┤
│  前端 → Gateway → Radish.Identity（专职签发）         │
│                → Radish.Api（仅验证）                │
│                → 其他服务（仅验证）                   │
│                                                     │
│  优点：职责清晰，支持多服务共享认证                   │
│  缺点：架构复杂度增加，需要服务发现支持              │
└─────────────────────────────────────────────────────┘
```

#### P2 实施代码（推荐方案）

**Gateway Program.cs 添加 JWT 认证**：

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// JWT 配置（与 Radish.Api 保持一致）
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? "wpH7A1jQRPuDDTyWv5ZDpCuAtwvMwmjzeKOMgBtvBe3ghDlfO3FhKx6vmZPAIazM";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Radish";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "luobo";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
```

**ocelot.json 配置路由认证**：

```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/User",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 7110 }
      ],
      "UpstreamPathTemplate": "/api/User",
      "UpstreamHttpMethod": [ "GET" ],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer",
        "AllowedScopes": []
      }
    },
    {
      "DownstreamPathTemplate": "/api/Login",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 7110 }
      ],
      "UpstreamPathTemplate": "/api/Login",
      "UpstreamHttpMethod": [ "POST" ]
      // 登录接口不需要 AuthenticationOptions
    }
  ]
}
```

### 9.3 P3 阶段：聚合接口实现

#### 场景：前端启动时需要一次性获取多份数据

**创建聚合控制器** `Radish.Gateway/Controllers/AggregateController.cs`：

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace Radish.Gateway.Controllers;

[ApiController]
[Route("gateway")]
[Authorize]
public class AggregateController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AggregateController> _logger;

    public AggregateController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AggregateController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 前端启动时的引导接口，聚合用户信息、菜单、配置等
    /// </summary>
    [HttpGet("bootstrap")]
    public async Task<IActionResult> GetBootstrap()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["Gateway:DownstreamApi"] ?? "https://localhost:7110";

        // 从 Header 中获取 Token 并转发
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            // 并发调用多个下游接口
            var userTask = httpClient.GetAsync($"{apiBaseUrl}/api/User/Current");
            var menuTask = httpClient.GetAsync($"{apiBaseUrl}/api/Menu/UserMenus");
            var configTask = httpClient.GetAsync($"{apiBaseUrl}/api/Config/Frontend");
            var notificationTask = httpClient.GetAsync($"{apiBaseUrl}/api/Notification/Unread");

            await Task.WhenAll(userTask, menuTask, configTask, notificationTask);

            // 组装响应
            var result = new
            {
                user = await userTask.Result.Content.ReadAsStringAsync(),
                menus = await menuTask.Result.Content.ReadAsStringAsync(),
                config = await configTask.Result.Content.ReadAsStringAsync(),
                notifications = await notificationTask.Result.Content.ReadAsStringAsync(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "聚合接口调用失败");
            return StatusCode(500, new { error = "聚合数据失败" });
        }
    }
}
```

**在 Program.cs 注册 HttpClient**：

```csharp
builder.Services.AddHttpClient();
builder.Services.AddControllers(); // 支持 Controller
```

**配置 Polly 策略（可选）**：

```csharp
builder.Services.AddHttpClient()
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

## 10. 核心技术难点深度分析

### 10.1 多租户路由传递

**问题**：Radish 已有多租户隔离机制（字段/表/库），Gateway 如何正确传递租户上下文？

**解决方案**：

```csharp
// Gateway 中间件：提取并注入租户 Header
app.Use(async (context, next) =>
{
    // 方案1：从 JWT Claims 中提取
    var tenantId = context.User.FindFirst("TenantId")?.Value;

    // 方案2：从自定义 Header 中提取（适用于 API Key 认证）
    if (string.IsNullOrEmpty(tenantId))
    {
        tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    }

    // 注入到转发 Header
    if (!string.IsNullOrEmpty(tenantId))
    {
        context.Request.Headers["X-Tenant-Id"] = tenantId;

        // 同时注入日志上下文
        using (Serilog.Context.LogContext.PushProperty("TenantId", tenantId))
        {
            await next();
        }
    }
    else
    {
        await next();
    }
});
```

**Ocelot 配置传递自定义 Header**：

```json
{
  "Routes": [
    {
      "UpstreamHeaderTransform": {
        "X-Tenant-Id": "{header:X-Tenant-Id}",
        "X-User-Id": "{claim:sub}",
        "X-Trace-Id": "{header:X-Trace-Id}"
      }
    }
  ]
}
```

**下游服务（Radish.Api）接收**：

```csharp
// Radish.Api/Program.cs 中间件
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    if (!string.IsNullOrEmpty(tenantId))
    {
        // 注入到 HttpContext.Items 供后续使用
        context.Items["TenantId"] = tenantId;

        // 或者直接设置到 TenantUtil
        // TenantUtil.SetCurrentTenant(tenantId);
    }
    await next();
});
```

### 10.2 配置膨胀管理

**问题**：随着路由增多，`ocelot.json` 可能膨胀到数千行。

**解决方案：按模块拆分配置文件**

```
Radish.Gateway/
├── ocelot.json                  # 全局配置
├── ocelot.Development.json      # 开发环境覆盖
├── ocelot.Production.json       # 生产环境覆盖
└── routes/
    ├── auth.json                # 认证模块路由
    ├── content.json             # 内容模块路由
    ├── points.json              # 积分模块路由
    ├── commerce.json            # 商城模块路由
    └── admin.json               # 管理后台路由
```

**Program.cs 加载策略**：

```csharp
builder.Host.ConfigureAppConfiguration((context, config) =>
{
    var env = context.HostingEnvironment.EnvironmentName;

    config.Sources.Clear();

    // 基础配置
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
          .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

    // Ocelot 全局配置
    config.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
          .AddJsonFile($"ocelot.{env}.json", optional: true, reloadOnChange: true);

    // 模块路由配置
    var routesPath = Path.Combine(context.HostingEnvironment.ContentRootPath, "routes");
    if (Directory.Exists(routesPath))
    {
        foreach (var routeFile in Directory.GetFiles(routesPath, "*.json"))
        {
            config.AddJsonFile(routeFile, optional: true, reloadOnChange: true);
        }
    }

    config.AddEnvironmentVariables();
});
```

**合并配置的辅助类**（可选）：

```csharp
public static class OcelotConfigMerger
{
    public static FileConfiguration Merge(params FileConfiguration[] configs)
    {
        var merged = new FileConfiguration();

        foreach (var config in configs)
        {
            merged.Routes.AddRange(config.Routes);
            // 合并其他配置...
        }

        return merged;
    }
}
```

### 10.3 TraceId 全链路透传

**目标**：从前端请求到所有下游服务，使用统一的 TraceId 关联日志。

**实施方案**：

```csharp
// Gateway 中间件
app.Use(async (context, next) =>
{
    // 优先使用客户端传来的 TraceId
    var traceId = context.Request.Headers["X-Trace-Id"].FirstOrDefault();

    // 如果没有则生成新的
    if (string.IsNullOrEmpty(traceId))
    {
        traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Trace-Id"] = traceId;
    }

    // 写入响应 Header，便于前端关联
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Trace-Id"] = traceId;
        return Task.CompletedTask;
    });

    // 注入 Serilog 上下文
    using (Serilog.Context.LogContext.PushProperty("TraceId", traceId))
    {
        await next();
    }
});
```

**Ocelot 配置自动传递**：

```json
{
  "GlobalConfiguration": {
    "RequestIdKey": "X-Trace-Id"
  }
}
```

**下游服务同样配置中间件**：

```csharp
// Radish.Api/Program.cs
app.Use(async (context, next) =>
{
    var traceId = context.Request.Headers["X-Trace-Id"].FirstOrDefault()
                  ?? Guid.NewGuid().ToString("N");

    using (LogContext.PushProperty("TraceId", traceId))
    {
        await next();
    }
});
```

**前端使用**：

```typescript
// radish.client/src/shared/api/client.ts
axios.interceptors.request.use(config => {
  // 生成或复用 TraceId
  const traceId = sessionStorage.getItem('currentTraceId') || uuidv4();
  config.headers['X-Trace-Id'] = traceId;
  return config;
});

axios.interceptors.response.use(response => {
  const traceId = response.headers['x-trace-id'];
  if (traceId) {
    console.log('[API Response] TraceId:', traceId);
  }
  return response;
});
```

## 11. 与 Radish 现有架构的整合方案

### 11.1 Autofac 容器兼容

**问题**：Radish.Api 使用 Autofac，而 Ocelot 默认依赖 Microsoft.Extensions.DependencyInjection。

**解决方案**：

```csharp
// Radish.Gateway/Program.cs
builder.Host
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // 注册 Gateway 特定的依赖
        containerBuilder.RegisterType<GatewayService>().As<IGatewayService>();

        // 如果需要复用 Radish.Extension 的模块
        // containerBuilder.RegisterModule(new AutofacModuleRegister());
    });

// 注意：AddOcelot() 必须在 ConfigureContainer 之前调用
builder.Services.AddOcelot();
```

### 11.2 配置工具复用

**目标**：Gateway 使用与 Api 相同的配置读取方式。

**实施**：

```csharp
// 1. Gateway 项目添加引用
// <ProjectReference Include="..\Radish.Common\Radish.Common.csproj" />

// 2. Program.cs 注册
builder.Services.AddSingleton(new AppSettingsTool(builder.Configuration));

// 3. 使用方式与 Api 一致
var gatewayPort = AppSettings.RadishApp("Gateway", "Port").ObjToInt();
var enableRateLimit = AppSettings.RadishApp("Gateway", "EnableRateLimit").ObjToBool();
```

### 11.3 Serilog 配置复用

**方案1：直接复用 Extension**

```csharp
// Gateway 项目引用 Radish.Extension
// <ProjectReference Include="..\Radish.Extension\Radish.Extension.csproj" />

builder.Host.AddSerilogSetup(); // 与 Api 完全一致
```

**方案2：独立配置（推荐用于区分日志）**

```csharp
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("ApplicationName", "Radish.Gateway")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Async(a => a.Console())
        .WriteTo.Async(a => a.File(
            path: "Log/Gateway/Gateway.txt",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));
});
```

### 11.4 启动脚本整合

**更新 start.ps1**：

```powershell
Write-Host "Select an action:"
Write-Host "1. Start frontend (radish.client)"
Write-Host "2. Start backend (Radish.Api)"
Write-Host "3. Start Gateway (Radish.Gateway)"
Write-Host "4. Start backend + Gateway"
Write-Host "5. Start all (frontend + backend + Gateway)"
Write-Host "6. Run unit tests (Radish.Api.Tests)"
$choice = Read-Host "Enter choice (1-6)"

function Start-Gateway {
    Push-Location $repoRoot
    try {
        Write-Host "Starting Gateway (Radish.Gateway)."
        $env:ASPNETCORE_URLS = "https://localhost:5001;http://localhost:5000"
        dotnet run --project Radish.Gateway/Radish.Gateway.csproj -c $Configuration
    }
    finally {
        Pop-Location
    }
}

switch ($choice) {
    "3" { Start-Gateway }
    "4" {
        Start-Backend &
        Start-Sleep -Seconds 3
        Start-Gateway
    }
    "5" {
        Start-Frontend -Detached
        Start-Backend &
        Start-Sleep -Seconds 3
        Start-Gateway
    }
}
```

## 12. 实施时间线与优先级建议

### 当前项目阶段评估（基于 M1-M4）

| 维度 | 现状 | Gateway 的必要性 | 建议 |
|------|------|----------------|------|
| **服务数量** | 单体 Radish.Api | 低（单体不需要网关） | ⏸️ 暂缓 |
| **并发压力** | 预计 <1000 QPS | 低（单机可承载） | ⏸️ 暂缓 |
| **认证复杂度** | JWT 自建，单一入口 | 低（无跨服务认证需求） | ⏸️ 暂缓 |
| **前端调用** | 直连 Radish.Api | 低（无聚合需求） | ⏸️ 暂缓 |
| **架构演进计划** | 预留微服务拆分 | **中**（需提前预留） | ✅ **预留扩展点** |

### 分阶段实施建议

#### **阶段 0（M1-M6）：预留模式**
**时间**：当前 - M6 结束
**目标**：不实施 Gateway，但在代码中预留扩展点

**任务清单**：
- [x] 在 `Radish.Api` 中添加 TraceId 透传中间件
- [x] JWT Claims 预留 `gateway` 标识字段
- [x] 日志格式规范化（便于后续集中采集）
- [ ] 在 CLAUDE.md 和文档中标注 Gateway 规划
- [ ] local-start 脚本预留 Gateway 启动选项（注释状态）

#### **阶段 1（M7-M8）：评估触发**
**时间**：M7 开始
**触发条件**（满足任一即启动 P1）：
1. 开始拆分第一个独立服务（如 Radish.Notification）
2. API 接口数量超过 50 个
3. 需要统一的限流/熔断策略
4. 前端需要聚合接口减少请求次数

**决策会议**：在 M7 计划会上评估是否启动 Gateway 项目

#### **阶段 2（M9-M10）：P1-P2 实施**
**前提**：已决策启动 Gateway
**目标**：Gateway 基础能力 + 认证集成

**交付物**：
- Radish.Gateway 项目跑通
- 至少 10 个核心 API 通过 Gateway 转发
- JWT 认证在 Gateway 层验证
- 健康检查、日志、TraceId 透传
- 更新 local-start 脚本和 CI 流水线

**风险**：
- 开发环境需要同时启动两个服务（Api + Gateway）
- 调试复杂度增加（建议保留 Api 直连模式）

#### **阶段 3（M11-M12）：P3 路由与聚合**
**目标**：完善路由配置，实现至少 1 个聚合接口

**交付物**：
- 按模块拆分路由配置文件
- 实现 `/gateway/bootstrap` 聚合接口
- 配置限流、熔断策略
- 前端切换到 Gateway 地址

#### **阶段 4（M13+）：P4-P6 渐进推进**
**目标**：根据实际需求决定是否引入服务发现、配置中心、服务拆分

**决策依据**：
- 服务数量 ≥3：启动 P4 服务发现
- 配置变更频繁：启动 P4 配置中心
- 单体性能瓶颈：启动 P6 服务拆分

### 关键决策点

#### **何时不需要 Gateway？**
- 始终是单体架构
- 团队规模 <5 人
- 日活用户 <10000
- 无第三方对接需求

#### **何时必须上 Gateway？**
- 服务数量 ≥3
- 需要统一的安全策略（WAF、限流、鉴权）
- 前端需要聚合接口
- 准备对外开放 API

### 成本与收益评估

| 项目 | 成本 | 收益 |
|------|------|------|
| **开发成本** | 2-3 人周（P1-P2）<br>1-2 人周（P3）<br>3-4 人周（P4-P6） | 长期降低多服务集成复杂度 |
| **运维成本** | 增加 1 个服务实例<br>配置管理复杂度提升 | 统一监控、统一治理 |
| **学习成本** | 团队需学习 Ocelot/YARP<br>网关调试技巧 | 掌握微服务网关技术栈 |
| **风险** | 单点故障风险<br>性能瓶颈可能 | 通过多副本和负载均衡缓解 |

### 建议行动

**立即执行**：
1. 在下次迭代计划会议上讨论 Gateway 启动时机
2. 在 `DevelopmentLog.md` 中记录本次技术评估结论
3. 在 `Radish.Api` 中预留 TraceId 和 Header 透传机制

**M7 评估时考虑**：
1. 当前是否有服务拆分计划？
2. 前端是否有聚合接口需求？
3. 是否需要统一限流策略？

**决策后更新**：
- `DevelopmentPlan.md` 添加 Gateway 相关里程碑
- `DevelopmentFramework.md` 更新架构图
- `README.md` 更新技术栈说明

---

## 附录：常见问题

### Q1: Gateway 会成为性能瓶颈吗？
**A**: 理论上会增加一跳延迟（通常 <5ms），但通过以下优化可以忽略：
- 使用 Kestrel 高性能服务器
- 启用 HTTP/2 或 HTTP/3
- 静态资源直接走 CDN，不经过 Gateway
- 热点接口可以缓存在 Gateway 层

### Q2: 如何避免 Gateway 单点故障？
**A**:
- 部署至少 2 个 Gateway 副本
- 前置 Nginx/HAProxy 做负载均衡和健康检查
- 使用容器编排（Docker Swarm / Kubernetes）自动重启

### Q3: Ocelot 和 YARP 如何选择？
**A**:
- **优先 Ocelot**：配置驱动，快速上手，生态成熟
- **考虑 YARP**：需要深度定制（如自定义负载均衡算法、Transform 逻辑）

### Q4: 如何在本地调试 Gateway？
**A**:
```bash
# 方案1：同时启动两个终端
terminal1> dotnet run --project Radish.Api
terminal2> dotnet run --project Radish.Gateway

# 方案2：使用 Tye（微软微服务本地编排工具）
tye run  # 自动启动所有服务

# 方案3：保留 Api 直连模式
前端环境变量：
VITE_API_BASE_URL=https://localhost:7110  # 开发时直连 Api
VITE_API_BASE_URL=https://localhost:5001  # 测试 Gateway
```

### Q5: Gateway 是否需要独立数据库？
**A**:
- **P1-P3 阶段**：不需要，配置文件即可
- **P4+ 阶段**：如果启用动态路由配置功能，可以考虑存储到 Redis 或数据库

---

> **最后更新**：2025-11-27（添加 Phase 0 门户页面抽离阶段）
> **下次评审**：Phase 0 完成后，评估是否启动 P1 路由转发阶段
> **文档维护者**：请在每次 Gateway 相关决策后更新本文档，并同步到 `DevelopmentLog.md`
