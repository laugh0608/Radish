# P3-12-E8-Q0 安全与暴露面阻断实施方案

> 状态：`方案已完成，等待确认；尚未修改依赖、生产端点、身份验证或前端运行时行为`
>
> 日期：`2026-07-10`（Asia/Shanghai）
>
> 归属：[P3-12-E8-Q 正式发布工程成熟度与安全收口](/planning/p3-12-e8-release-engineering-maturity-security-closure)

## 摘要

Q0 按 `Q0-A → Q0-B → Q0-C → Q0-D` 分四个独立代码批次推进：

1. `Q0-A`：升级已知高危依赖、恢复 NuGet 审计、增加可阻断的依赖安全门禁。
2. `Q0-B`：从生产 API 删除 Rust 性能测试和 WeatherForecast 演示面，同时清理失效模型、资源、HttpTest 和架构口径。
3. `Q0-C`：启用 JWT audience，清理 Claims / 成功鉴权高频日志，让 OpenIddict transport security 真正受环境配置约束。
4. `Q0-D`：建立 Markdown 链接协议白名单，统一阅读态和富文本编辑态行为。

本方案坚持以下选择：

- 安全升级保持在现有 major 内，不借机进行框架大版本迁移。
- 演示和性能端点直接退出生产，不增加新的环境开关或多层保护继续保留它们。
- Audience 继续使用已经由 Auth 写入 token resource 的 `radish-api`，不新增第二套资源标识。
- Auth 只允许 Development 环境显式关闭 transport security；生产代理链依赖 Gateway 写入并由 Auth 读取 `X-Forwarded-Proto`。
- Markdown 危险协议退化为不可点击文本，不尝试自动修复或猜测用户输入。

## 一、现状与外部安全依据

### 1. 当前依赖命中

| 依赖 | 当前版本 | 当前命中 | 安全目标 |
| --- | --- | --- | --- |
| `Microsoft.AspNetCore.OpenApi` | `10.0.0-rc.2.25502.107` | 仍为 .NET 10 RC 包 | `10.0.9` |
| `Microsoft.OpenApi` | `2.0.0` | High，循环 schema 可导致进程栈溢出终止 | `2.7.5` |
| `react-router-dom` / `react-router` | lock 为 `7.13.0` | npm audit 汇总 High | `7.18.1` |
| `ws` | lock 为 `7.5.10` | High，碎片 / 数据块导致内存耗尽 DoS | `7.5.11` |

选择依据：

- [Microsoft.OpenAPI GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc) 明确 2.x 修复版本为 `2.7.5`；Q0 不升级到 3.x，避免不必要的 OpenAPI 类型破坏。
- [Microsoft.AspNetCore.OpenApi NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) 在 2026-07-10 的 .NET 10 稳定线为 `10.0.9`，并兼容 Microsoft.OpenApi 2.x。
- [React Router GHSA-49rj-9fvp-4h2h](https://github.com/advisories/GHSA-49rj-9fvp-4h2h) 等审计项覆盖当前 `7.13.0`；npm 官方注册表在 2026-07-10 返回稳定版 `7.18.1`，高于本轮全部受影响区间。
- [ws GHSA-96hv-2xvq-fx4p](https://github.com/advisories/GHSA-96hv-2xvq-fx4p) 明确 `7.5.11` 为 7.x 修复版本；`@microsoft/signalr@10.0.0` 使用 `^7.5.10`，无需 override 即可解析到 `7.5.11`。

Console 当前使用 `createBrowserRouter` Data Mode，并非 React Router Framework Mode，因此部分服务端 advisory 的直接可利用面较低；但发布门禁不能以“当前调用模式可能不受影响”替代升级，仍按 High 清零处理。

### 2. 审计命令行为

- `npm audit --omit=dev --json` 当前返回 3 个 High，并以非零状态退出。
- `dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore` 当前列出多个项目中的 `Microsoft.OpenApi 2.0.0` High，但命令状态仍为 `0`。
- 因此 CI 不能只依赖 `dotnet package list` 的退出码，必须解析 JSON 中的 `vulnerabilities[].severity`。
- `Scripts/dotnet-command.mjs` 当前追加 `NuGetAudit=false` 和 `NoWarn=NU1903`；前者隐藏审计，后者覆盖项目原有 `NoWarn` 并制造约 795 条警告噪音。

## 二、Q0-A 依赖安全与审计恢复

### 1. 依赖调整

修改：

- `Radish.Api/Radish.Api.csproj`
  - `Microsoft.AspNetCore.OpenApi` → `10.0.9`。
- `Radish.Extension/Radish.Extension.csproj`
  - `Microsoft.AspNetCore.OpenApi` → `10.0.9`。
  - `Microsoft.OpenApi` → `2.7.5`。
- `Frontend/radish.console/package.json`
  - `react-router-dom` → `^7.18.1`。
- `package-lock.json`
  - `react-router` / `react-router-dom` 固定解析到 `7.18.1`。
  - `ws` 固定解析到 `7.5.11`。

不调整：

- 不升级 `Scalar.AspNetCore 2.10.3`，除非 OpenAPI 编译或 Scalar 定向验证证明现有版本不兼容。
- 不把 `ws` 提升为直接依赖，也不增加 `overrides`；SignalR 的现有 semver 范围已经允许安全补丁。
- 不在 Q0 引入 `Directory.Packages.props`；中央包版本治理后置到 Q2 / Q4。

### 2. 包安装边界

AI 协作者不执行 `npm install`。进入 Q0-A 实施时：

1. AI 先修改 `Frontend/radish.console/package.json`。
2. 用户执行：

   ```bash
   npm install react-router-dom@^7.18.1 --workspace=radish.console
   npm update ws
   ```

3. AI 复核 `package-lock.json` 只包含预期依赖变更，再继续代码与验证。

.NET 依赖直接编辑 csproj，不执行 `dotnet add package`；后续通过允许的 build / restore 流程验证。

### 3. dotnet 命令修正

修改 `Scripts/dotnet-command.mjs`：

- 删除 `NuGetAudit=false` 自动追加逻辑。
- 删除 `NoWarn=NU1903` 自动追加逻辑。
- 保留 `-m:1` 和 restore `--disable-parallel` 的本地资源控制。
- 不新增新的 warning suppression。

补充 contract：

- `check:repo-quality-contract` 必须拒绝 `dotnet-command.mjs` 再出现 `NuGetAudit=false` 或全局 `NoWarn=NU1903`。
- 构建警告恢复为项目自身 `NoWarn` 口径；剩余真实警告进入 Q3 预算，不在 Q0 扩大清理。

### 4. 依赖安全门禁

新增 `Scripts/check-dependency-security.mjs` 和 `npm run check:dependency-security`：

- 执行 `npm audit --omit=dev --json`，解析 High / Critical 数量。
- 执行 `dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore`，遍历所有项目、框架、顶级和传递包的漏洞项。
- 任一生态存在 High / Critical 时退出失败。
- 命令本身失败、JSON 无法解析或审计源不可用时同样失败，不把“审计未完成”视为安全通过。
- Q0 不引入 advisory allowlist；确有不可修复项时必须另行说明风险、范围和到期日。

更新 Repo Quality：

- 新增必需 job：`Dependency Security`。
- job 安装 Node 24 / .NET 10，执行 `npm ci`、`dotnet restore Radish.slnx`、`npm run check:dependency-security`。
- 同步 `Scripts/repo-quality-contract.mjs`、`Scripts/check-repo-quality-contract.mjs` 和 `.github/rulesets/master-protection.json`。
- `validate:ci` 是否纳入联网审计后置到 Q3；Q0 先保证 PR 必需检查真实阻断。

### 5. Q0-A 验证

- `npm audit --omit=dev`
- `dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore`
- `npm run check:dependency-security`
- `npm run check:repo-quality-contract`
- `npm run type-check --workspace=radish.console`
- `npm run test --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run test --workspace=radish.client`
- `dotnet build Radish.slnx -c Debug`
- `dotnet test Radish.Api.Tests`

若需要验证 Scalar 页面和 SignalR 真实连接，必须等待用户当轮确认前后端已经启动后再执行 Gateway smoke。

## 三、Q0-B 生产调试与高消耗端点退出

### 1. 删除生产运行面

删除：

- `Radish.Api/Controllers/v2/RustTestController.cs`
- `Radish.Api/Controllers/v2/WeatherForecastController.cs`
- `Radish.Model/ViewModels/WeatherForecastVo.cs`

保留：

- `Lib/radish.lib/src/benchmark/` 中的本地基准函数和 Rust 测试资产。
- C# / Rust 正式图片处理、哈希等真实运行时能力。

Q0 不新建 BenchmarkDotNet 项目。若以后仍需 C# / Rust 对比基准，必须放在测试 / benchmark 工程，不得重新暴露 HTTP Controller。

### 2. 清理失效资产

- 从 `Radish.Api/Resources/Errors.en.resx`、`Errors.zh.resx` 删除仅供 Weather 演示使用的资源键。
- 从 `Radish.Api.Tests/HttpTest/Radish.Api.Smoke.http` 删除 Weather 请求，以真实用户查询或健康入口作为 smoke 首项。
- 从 `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http` 删除旧 Weather 注释。
- 更新 `Docs/architecture/framework.md`：v2 不再以 RustTest 作为实验接口示例。
- 更新 `Docs/architecture/specifications.md`：Rust 性能验证改为本地 cargo / benchmark 入口；路径参数示例改用真实业务 Controller；删除 `/api/v2/RustTest` 示例。
- 更新 `Docs/architecture/i18n.md`：Weather 本地化示例改用现有 User 查询本地化契约。
- 保留 changelog 历史事实，不改写过去记录。

### 3. 防回归契约

在 `Radish.Api.Tests/Security` 增加生产 API 表面测试：

- 反射 `Radish.Api` Controller 类型。
- 明确拒绝 `RustTestController`、`WeatherForecastController` 和后续同名回归。
- 不采用“所有含 Test/Demo 字样都禁止”的宽泛规则，避免误伤测试辅助类型；当前只守护已裁决退出的生产面。

### 4. Q0-B 验证

- 目标源码 / OpenAPI 文案扫描不再命中两个 Controller。
- `dotnet build Radish.slnx -c Debug`
- `dotnet test Radish.Api.Tests`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

运行态补验时检查 `/openapi/v2.json` 与 `/scalar` 不再出现性能测试和 WeatherForecast 标签。

## 四、Q0-C 身份验证与敏感日志

### 1. JWT Audience

当前 Auth 已在 `AuthorizationController` 中对 access token 调用 `principal.SetResources("radish-api")`，因此 API 直接以 `UserScopes.RadishApi` 作为唯一合法 audience。

实现：

- 新增可测试的 `ApiJwtValidationPolicy`，集中创建 `TokenValidationParameters`。
- 设置 `ValidateAudience = true`。
- 设置 `ValidAudience = UserScopes.RadishApi`。
- 保持 issuer、lifetime、signing key、`ClockSkew = Zero`、`sub` 和 `role` 语义不变。
- `AuthorizationController` 中的 resource 字面量改为 `UserScopes.RadishApi`，避免资源标识双写。
- Authority 模式和部署态本地证书模式共同使用同一 audience policy。

测试：

- 正确 issuer / signing key / `aud=radish-api` 通过。
- audience 缺失或错误时拒绝。
- audience 正确但 scope 缺失时仍被 Client Policy 拒绝。
- `radish-api` scope 正常化与已有 Client Policy 行为保持不变。

### 2. Claims 与高频成功日志

修改：

- `Radish.Api/Program.cs`
  - 删除 Client Policy 的完整 Claims、全部 scopes 和“授权成功”Information 日志。
  - Client Policy 保留 `UserClaimReader.HasScope` 判断，不改变授权语义。
  - JWT query token 提取成功、token validated 等高频成功日志降到 Debug 或删除。
  - 认证失败 / challenge 只记录 path、错误类型和 correlation 信息，不记录 token、Claims 或 scope 全量。
- `Radish.Api/Hubs/NotificationHub.cs`
  - 删除完整 Claims 和成功提取 userId 的 Information 日志。
  - 认证失败只保留连接级安全诊断，不输出 Claims 内容。

扩展 `check-identity-claims.mjs`：

- 明确禁止 `Program.cs` 和 Hub 中重新出现“所有 Claims”或 Claims 拼接日志。
- 要求 JWT policy 继续启用 audience 并指向 `UserScopes.RadishApi`。
- 现有标准 Claim 输出 / 兼容读取守卫保持不变。

### 3. OpenIddict Transport Security

当前 `AllowInsecureHttp=false` 没有生效，因为 `Program.cs` 无条件调用 `DisableTransportSecurityRequirement()`。

实现：

- 新增 `OpenIddictTransportSecurityPolicy`，只在以下条件同时成立时返回允许关闭：
  - `OpenIddict:Server:AllowInsecureHttp=true`；
  - `IHostEnvironment.IsDevelopment()`。
- Production / Staging 如果配置为 `true`，启动时直接失败并指出错误配置，不静默忽略。
- `Program.cs` 仅在 policy 允许时调用 `DisableTransportSecurityRequirement()`。
- `Radish.Auth/Properties/launchSettings.json` 的 Development `http` profile 显式设置 `OpenIddict__Server__AllowInsecureHttp=true`，承接本地直连开发。
- `deploy/docker-compose.local.yaml` 当前使用 Production + `AllowInsecureHttp=true`，改为 `false`；本地 Docker 对外仍通过 Gateway HTTPS，Auth 通过 ForwardedHeaders 识别原始 scheme。
- `deploy/docker-compose.yaml` 保持 `false`。

代理信任边界：

- Q0 不同时改造 Docker 固定网段或完整可信代理配置，避免把身份修复扩成部署网络重构。
- 生产 compose 中 Auth 继续只使用 `expose: 5200`，不得添加宿主 `ports`。
- Auth 保持 `UseForwardedHeaders()` 位于认证和 OpenIddict 之前，显式设置 `ForwardLimit=1`。
- `KnownIPNetworks / KnownProxies` 的精确生产 CIDR 治理作为后续部署安全项记录；在完成前，唯一可接受前提是 Auth 不直接暴露到 Docker 网络外。

测试：

- Development + true：允许关闭 transport security。
- Development + false：保持 transport security。
- Production / Staging + true：启动策略拒绝。
- Production + false：通过 Gateway `X-Forwarded-Proto=https` 正常执行 OIDC。
- compose contract 断言 Auth 没有宿主端口暴露。

### 4. Q0-C 验证

- `npm run check:identity-claims`
- `npm run validate:identity`
- JWT / transport policy 定向测试
- `dotnet build Radish.slnx -c Debug`
- `dotnet test Radish.Api.Tests`
- `rg` 确认不再存在完整 Claims 日志和无条件 `DisableTransportSecurityRequirement()`。

运行态补验必须覆盖：登录授权、回调、refresh、API 受保护请求、通知 / 聊天 / 评论 Hub，以及错误 audience token 拒绝。

## 五、Q0-D Markdown 危险链接协议

### 1. 安全契约

新增共享函数 `sanitizeMarkdownLinkHref(value): string | null`，只允许：

- `http:`、`https:`；
- 站内绝对 / 相对路径；
- `#anchor` 与安全 query；
- 有效的 `attachment://<positive-id>` 项目附件协议。

默认拒绝：

- `javascript:`；
- `data:`，包括 `data:text/html`；
- `vbscript:`；
- 未明确允许的任意自定义 scheme；
- 伪造或无效的 `attachment://`。

`mailto:`、`tel:` 当前没有产品需求与既有内容命中，本批不开放；后续如需支持，必须单独增加产品边界和测试。

### 2. 共享实现边界

- 在 `@radish/ui` URL / Markdown 工具中实现并导出 sanitizer，不修改通用 `resolveConfiguredMediaUrl` 的职责。
- `MarkdownRenderer`：
  - `href` 经过 sanitizer；
  - 图片继续使用现有 `defaultUrlTransform`、`attachment://` 与 `sticker://` 资源规则；
  - `resolveLinkHref` 自定义重写结果也必须再次经过 sanitizer。
- `RichTextMarkdownEditor`：
  - `buildRichLinkHtml` 只为安全 URL 生成 `<a>`；
  - 被拒绝的 URL 只渲染转义文本，不生成可点击元素；
  - 图片与附件上传协议保持原行为。
- 将富文本 Markdown ↔ HTML 的纯转换函数移入独立工具文件，以便无 DOM 测试，避免继续扩大组件体积。

### 3. 测试入口

- 为 `@radish/ui` 增加最小 `test` script 与 `tests/markdownUrl.test.ts`。
- 为 client 增加富文本转换安全测试，覆盖：
  - `javascript:`、大小写 / 前后空白变体；
  - `data:text/html`、`vbscript:`；
  - `https`、站内相对链接、anchor；
  - 有效 / 无效 attachment；
  - Public Docs 自定义链接重写后再次校验。
- 不在 Q0 引入 jsdom 或完整组件测试框架；组件级可访问性 / DOM 测试后置到 Q3。

### 4. Q0-D 验证

- `npm run test --workspace=@radish/ui`
- `npm run type-check --workspace=@radish/ui`
- `npm run lint --workspace=@radish/ui`
- `npm run test --workspace=radish.client`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

## 六、实施顺序与提交边界

建议形成四个独立提交：

1. `fix(deps): 清理发布阻断依赖漏洞`
2. `fix(api): 移除生产性能与演示端点`
3. `fix(auth): 收紧 audience 与传输安全边界`
4. `fix(ui): 统一 Markdown 链接协议防护`

顺序原因：

- A 先恢复可信的依赖基线，后续构建不再携带已知 High。
- B 删除明确的生产攻击面，且不依赖身份改造。
- C 涉及身份和代理链，必须在独立批次验证登录、refresh 与 Hub。
- D 是前端安全边界，可独立回归论坛、Docs 和 Wiki。

每个批次验证通过后再进入下一批；不把四批压成一个难以回滚的大提交。

## 七、预计变更范围

### Q0-A

- 2 个 csproj、Console package、package-lock。
- `Scripts/dotnet-command.mjs`。
- 新增依赖安全脚本与 package script。
- Repo Quality workflow、contract 和 master ruleset 文件。

### Q0-B

- 删除 2 个 Controller、1 个 Vo。
- 2 个 API 资源文件、2 个 HttpTest 文件。
- framework、specifications、i18n 和 Rust 指南相关口径。
- 新增生产 API 表面测试。

### Q0-C

- API JWT policy、Program、NotificationHub。
- Auth transport policy、Program、launch profile。
- local / production compose contract 与身份扫描。
- JWT、transport、compose 定向测试与身份文档。

### Q0-D

- `@radish/ui` Markdown URL 工具、renderer、exports、package test 入口。
- client RichTextMarkdownEditor 与独立转换工具。
- UI / client 定向安全测试。

## 八、风险、回滚与停止条件

### 1. 主要风险

- OpenAPI 2.0.0 → 2.7.5 可能产生接口细节变化；先以编译和 Scalar 文档生成验证，不直接跨到 3.x。
- React Router 7.13.0 → 7.18.1 可能改变导航细节；重点回归 `/console/` base、登录回流、权限守卫和查询参数。
- Audience 开启后，任何历史 token 如果没有 `aud=radish-api` 会立即失效；部署前必须抽样现有 access / refresh 后新 token。
- Transport security 收紧后，绕过 Gateway 直接调用 Auth HTTP 的开发流程会被拒绝；仅 Development launch profile 显式保留。
- Markdown sanitizer 可能让历史危险或未知协议链接变为纯文本，这是预期安全变化，不提供兼容执行。

### 2. 回滚边界

- 依赖批次只允许回滚到仍无 High / Critical 的版本，不得回滚到当前漏洞版本。
- 生产端点删除不做运行时回滚；如需基准能力，只能恢复到非 HTTP 测试工程。
- Audience / transport 如暴露代理配置问题，应修正 issuer、resource 或 forwarded headers，不重新关闭验证。
- Markdown 如误伤合法链接，应扩充明确白名单和测试，不恢复“允许所有 scheme”。

### 3. 立即停止并重新确认的情况

- OpenAPI 2.7.5 与当前 Scalar 无法兼容，且修复需要 Scalar major 升级。
- 现网 token 实际没有 `aud=radish-api`，与源码 `SetResources` 结论不一致。
- 生产 Auth 存在 Gateway 之外的合法 HTTP 入口。
- 历史业务内容真实依赖 `mailto:`、`tel:` 或其他未记录的自定义链接协议。
- 依赖安全 job 需要修改实际 GitHub ruleset，但当前连接权限不足以同步远端配置。

出现上述情况时，保留已经验证通过的独立批次，不用临时 fallback 绕过问题。

## 九、确认后第一步

方案确认后先实施 `Q0-A`。由于 npm 依赖安装必须由用户执行，第一步流程为：

1. AI 修改 csproj、Console package、dotnet command 和依赖安全门禁代码。
2. AI 告知用户执行两条 npm 命令更新 lock。
3. 用户执行完成后，AI 复核 lock、构建、测试与两个生态的漏洞审计。
4. Q0-A 独立提交后，再进入 Q0-B。
