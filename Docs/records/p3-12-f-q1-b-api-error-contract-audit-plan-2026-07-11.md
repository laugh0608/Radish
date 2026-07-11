# P3-12-F Q1-B API 错误契约审计与实施方案

> 状态：`审计与三批运行时实施完成；静态与自动化验证通过`
>
> 日期：`2026-07-11`（Asia/Shanghai）
>
> 范围：只覆盖正式 Web 发布矩阵中的 API 错误响应、异常映射和客户端兼容；不包含 Q1-C、Q2、Q3、页面调整、服务启动、部署或无关重构。

## 摘要

当前主要风险不是缺少某一个异常处理中间件，而是五个边界同时漂移：Controller 直接暴露未知 `ex.Message`，`MessageModel.StatusCode` 与真实 HTTP 状态不一致，业务错误仍按异常文案分类，认证 / 权限 / 限流 / 模型校验存在多种响应形状，前端虽能解析 `MessageModel`，却没有关联诊断标识，也无法同时保留真实 HTTP 状态与响应体状态。

全仓静态盘点确认，`Radish.Api/Controllers` 有 130 处 `ex.Message / exception.Message`，分布在 25 个文件；其中 45 个宽泛 `catch (Exception ex)` 分布在 9 个文件。Controller 内有 11 处按异常文案相等或包含关系判断 HTTP 状态。Controller 约有 592 处 `MessageModel` 返回或构造，而显式 `IActionResult / ActionResult` 出口只是少数；大量失败响应只在 JSON 体内写 400 / 404，真实 HTTP 仍为 200。

实施继续保留 `MessageModel` 作为 `/api/v1/**` JSON 契约，没有整体切换到 Problem Details；统一错误结果工厂、API 全局异常处理、稳定错误码、`TraceId` 和关键 Controller 迁移均已完成。OIDC 标准协议响应、文件 / 流式响应、SignalR、健康检查和 Gateway 公共 HTML 未被统一 JSON 包裹。

## 一、审计范围与判定口径

本轮检查了：

- `Radish.Api` Controller、Filter、JWT 事件、限流中间件、模型校验和请求管线。
- `Radish.Service / Core / Infrastructure` 中的通用异常、业务异常、消息文案传播及持久失败记录。
- `Radish.Gateway` 自身异常页与代理边界，`Radish.Auth` 的 Razor 注册错误和 OpenIddict 协议出口。
- `MessageModel`、`BusinessException`、`HttpStatusCodeEnum` 与既有错误码 / i18n 文档约定。
- `@radish/http` 及 client / console 的错误解析、401 刷新和直接 `fetch / XMLHttpRequest` 兼容点。
- 既有 Controller、Service、HTTP 脚本和前端测试覆盖。

审计区分三类信息：

1. **可对外业务信息**：经过明确分类、可直接展示的领域文案与稳定错误码。
2. **只供服务端诊断的信息**：异常类型、堆栈、SQL、文件路径、依赖地址和内部状态，只进入结构化日志并关联 `TraceId`。
3. **运维持久记录**：Outbox、上传、订单等失败摘要可以保留受控信息，但不得未经分类直接回传客户端。

## 二、现状证据与风险

### 1. Controller 与异常分类

- 130 处异常消息直接参与响应，数量最高的文件为 `ShopController` 13 处、`ContentModerationController` 11 处、`WikiController` 10 处、`SystemConfigController` 9 处、`PollController` 9 处。
- 45 个宽泛 `catch (Exception ex)` 集中在 `WikiController` 10 处、`SystemConfigController` 9 处、`ClientController` 6 处、`ChunkedUploadController` 5 处、`StatisticsController` / `RoleController` / `AttachmentController` 各 4 处等。
- Lottery、Poll、QuickReply、ContentModeration、Comment、Question 等 Controller 通过 `ex.Message == ...` 或 `.Contains(...)` 决定 400 / 403 / 404。文案变化、本地化或底层异常替换都会改变协议行为。
- 后端存在约 27 处 `throw new Exception`、140 处 `ArgumentException`、204 处 `InvalidOperationException`。不能把全部同类型异常的 `Message` 直接视为安全业务文案。

### 2. `MessageModel` 与真实 HTTP 状态

- 泛型 `MessageModel<T>.Failed()` 复用 `Message()`，未设置失败状态，默认 `StatusCode = 200`；非泛型 `MessageModel.Failed()` 默认 400。相同调用语义产生不同结果。
- 多数 Action 直接返回 `Task<MessageModel...>`。即使对象内手动填写 400 / 404，MVC 仍通常发送 HTTP 200。
- `HttpStatusCodeEnum` 只有 200、400、401、403、404、500，无法规范表达 409、429、502、503。
- `MessageInfoDev` 泛型字段默认是 `"Nothing happened here."`，现有运行时没有实际使用。它不应被改造成生产环境返回原始异常的通道。
- `[ApiController]` 的默认模型校验响应仍可能输出 Problem Details；未发现 `InvalidModelStateResponseFactory`，因此同一 API 同时存在两种 JSON 错误形状。

### 3. 管线中的独立错误出口

- `Radish.Api` 没有全局 `IExceptionHandler` 或等价异常处理中间件，未知异常无法形成稳定安全响应。
- JWT `OnChallenge` 只记录日志；`RequireConsolePermissionAttribute` 返回空的 401 / 403，均不提供错误码或诊断标识。
- IP 黑名单和限流返回匿名 `{ status, message, success, retryAfter? }`，字段名和 `MessageModel` 不一致。
- Gateway 开发环境使用 Developer Exception Page，生产使用 `/Error` HTML；代理下游 API 响应应原样透传，不能二次包装。
- Auth 的 OpenIddict authorize / token 等标准协议响应必须保持 OAuth / OIDC 结构；Razor 注册页当前把未知 `ex.Message` 放入 `TempData`，需要改为安全文案并在服务端日志保留完整异常。

### 4. Service 与基础设施传播

- `CSharpImageProcessor`、`LocalFileStorage`、`CommentService` 等把底层 `ex.Message` 填入返回结果，Controller 即使不直接 catch，也可能继续泄露内部信息。
- `BusinessException` 已支持 `StatusCode + ErrorCode`，但只在宠物、回应、背包等少量链路使用，错误码同时存在 `Pet.InvalidAction` 和 `InvalidArgument / ConcurrentConflict` 等不统一形式。
- 数据库唯一冲突识别中存在对 SQLite / PostgreSQL 异常文案的底层适配。这类识别可以暂留在基础设施边界，但必须转换为明确领域结果，不能把原始数据库消息上送。
- 日志、审计和可靠任务失败记录中的异常详情属于内部观测面，不因 Q1-B 一律删除；本批只治理其对外传播与重复记录。

### 5. 前端兼容边界

- `@radish/http` 已能在非 2xx 响应中解析 `MessageModel`，并支持 `Code / MessageKey`；因此切换为真实 HTTP 状态不需要另造客户端协议。
- 当前 `ParsedApiResponse.statusCode` 优先采用响应体状态，不能区分真实 HTTP 状态和兼容体状态；类型中也没有 `TraceId`。
- token refresh 依赖真实 401。将 body-only 401 改为真实 401 能修正恢复语义，但必须防止业务 401 与 OIDC token 端点标准错误互相混淆。
- client / console 仍有少量直接 `fetch / XMLHttpRequest`，集中在认证 bootstrap、token refresh、站点品牌和附件上传；实施时必须纳入契约测试，而不是假定全部走统一客户端。

## 三、目标错误响应契约

### 1. API JSON 响应

继续使用现有字段，并新增可选 `TraceId`：

```json
{
  "statusCode": 409,
  "isSuccess": false,
  "messageInfo": "资源状态已发生变化，请刷新后重试",
  "code": "Common.Conflict",
  "messageKey": "error.common.conflict",
  "traceId": "0HN...",
  "responseData": null
}
```

规则如下：

- `StatusCode` 必须与真实 HTTP 状态一致；兼容期不允许继续制造新的 body-only 错误。
- `MessageInfo` 只能是经过分类的安全文案，客户端可展示但不得依赖文案做分支。
- `Code` 是稳定机器契约，沿用既有 Pascal dotted 约定；`MessageKey` 沿用 `error.* / info.*`。
- `TraceId` 是客户端报障与服务端日志的关联标识。响应体 `TraceId` 与 `X-Correlation-ID` 响应头使用同一值，不再维护第二套独立 ID。
- `ResponseData` 只放结构化、安全且客户端确实需要的详情，例如模型字段错误；未知异常保持 `null`。
- `MessageInfoDev` 保留兼容但停止使用，默认不输出；不得承载未知异常、SQL、路径或堆栈。后续大版本再评估删除。

### 2. 错误码首批注册表

| HTTP | `Code` | 用途 |
| --- | --- | --- |
| 400 | `Common.ValidationFailed` | 参数、模型校验和明确业务输入错误 |
| 401 | `Auth.Unauthorized` | API 缺少或失效身份 |
| 403 | `Auth.Forbidden` | 已认证但无权限 |
| 404 | `Common.NotFound` 或领域 `.NotFound` | 资源不存在 |
| 409 | `Common.Conflict` 或领域 `.Conflict` | 并发、唯一性或状态冲突 |
| 429 | `RateLimit.Exceeded` / `RateLimit.IpBlocked` | 速率限制和 IP 阻断 |
| 500 | `System.UnexpectedError` | 未分类服务端异常 |
| 502 / 503 | `Dependency.BadGateway` / `Dependency.Unavailable` | 仅明确识别的外部依赖故障 |

领域码继续使用 `Auth.InvalidCredentials`、`User.NotFound`、`Pet.InvalidAction` 等形式。现有扁平码在触达对应链路时迁移，不为本批一次性机械改名全部历史码。

### 3. 异常映射

| 来源 | 对外映射 | 日志 |
| --- | --- | --- |
| 已验证的领域异常 | 异常声明的状态、Code、MessageKey 和安全文案 | Information / Warning，按预期程度区分 |
| 模型校验 | 400 + 字段错误结构 | 默认不记录 Error |
| 身份 / 权限 | 401 / 403 + 稳定码 | 仅记录必要上下文，不记录 token |
| 明确依赖异常 | 502 / 503 + 安全文案 | Error，记录依赖类别和耗时 |
| 未知异常 | 500 + `System.UnexpectedError` + `TraceId` | Error，记录完整异常一次 |

`ArgumentException / InvalidOperationException` 不能按类型全局暴露原消息。只有调用点已转换为明确领域异常或结果时，消息才可对外；否则进入未知异常安全映射。

## 四、运行时边界与停止线

### 1. 统一处理的范围

- `Radish.Api` 的 `/api/**` JSON Controller。
- API 模型校验、JWT challenge / forbidden、Console 权限 Filter、限流和 IP 黑名单。
- Controller 及其下游 Service 抛出的领域异常和未知异常。

### 2. 不统一包裹的范围

- Auth 的 OAuth / OIDC authorize、token、userinfo、logout 等标准协议响应。
- 文件下载、图片、导出、流式上传 / 下载，以及响应已经开始的请求。
- SignalR negotiate、WebSocket 和 Hub 调用。
- health checks、Hangfire Dashboard、Scalar / OpenAPI。
- Gateway 代理得到的下游响应、公开 head snapshot、sitemap 和浏览器 HTML 错误页。

这些边界仍要避免泄露未知异常，但保持各自标准媒体类型与协议结构。

## 五、实施设计

### 批次一：契约基础与全局安全边界

1. 扩展 `MessageModel<T> / MessageModel`：统一成功 / 失败默认状态，新增可选 `TraceId`，停止输出 `MessageInfoDev` 默认占位。
2. 扩展 HTTP 状态枚举与集中错误码常量；为 `BusinessException` 补 `MessageKey`，并把它明确为“已验证可对外”的领域异常。
3. 建立统一 `ApiErrorResultFactory`，负责创建 `ObjectResult`、同步真实 HTTP 状态、错误码、消息键和 `TraceId`。
4. 在 API 注册全局异常处理：领域异常按声明映射，未知异常统一 500，完整异常只记录一次。
5. 配置模型校验、JWT 401 / 403、Console 权限、429 / IP 阻断复用同一结果写入器；保留 `Retry-After`。

批次一解决未知异常泄露和管线多形状问题，但不依靠全局 Result Filter 把全部历史 `MessageModel` 自动改成非 2xx。实施前泛型 `Failed()` 默认 200，直接全局翻译会在未分类的数百个调用点产生过宽行为变化。

### 批次二：关键 Controller 领域化与真实状态

1. 优先迁移正式发布核心链路：帖子 / 评论 / 问答 / 回应、聊天 / 通知、商城 / 萝卜币 / 订单、Console 治理和配置。
2. 将 Action 的失败出口改为统一结果工厂或 `ActionResult<MessageModel<T>>`，使真实 HTTP 状态与响应体一致。
3. 删除 Controller 的未知 `catch (Exception)`；需要恢复业务分支的调用点改抛领域异常或返回明确结果。
4. 消除基于异常中文文案的状态判断；数据库唯一冲突、乐观锁和依赖错误在仓储 / Service 边界转换为稳定领域码。
5. 处理从 Service 返回的 `ex.Message`，区分安全业务失败和内部异常，不只机械替换 Controller 字符串。

### 批次三：客户端兼容与剩余发布矩阵收口

1. `@radish/http` 同时保留真实 `httpStatus`、契约 `statusCode`、`Code`、`MessageKey` 和 `TraceId`；错误恢复只依赖真实 HTTP 状态和稳定码。
2. 为 non-2xx `MessageModel`、空体 401 / 403 兼容、非 JSON 响应和未知 500 增加解析测试。
3. 复核 client / console 直接 `fetch / XMLHttpRequest` 调用，统一采用现有客户端或明确适配新响应，不调整页面布局与产品交互。
4. 收口本次正式发布矩阵内剩余生产 Controller 的未知 `ex.Message`；不要求本批完成所有领域码的长期细粒度迁移。

## 六、实施结果

1. `MessageModel<T> / MessageModel` 已统一失败默认状态，增加可选 `TraceId`；`MessageInfoDev` 改为可空且不再输出默认占位。
2. API 已接入 `IExceptionHandler`、统一结果工厂、模型校验、JWT challenge / forbidden、Console 权限和限流响应；未知异常只返回安全文案、`System.UnexpectedError` 和诊断标识。
3. 已迁移论坛帖子 / 评论 / 问答 / 投票 / 抽奖 / 轻回应、聊天 / 通知、商城 / 萝卜币、Wiki、Console 治理 / 配置 / 客户端 / 角色、附件 / 分片上传和经验治理入口的真实 HTTP 状态同步。
4. 问答、投票、抽奖、轻回应、内容治理和 Wiki 的文案状态判断已替换为显式 `BusinessException` 状态、Code 与 MessageKey；Controller 中异常文案状态分类静态复扫为 0。
5. Auth 注册页未知异常不再显示原始消息，改为安全提示和诊断标识；OAuth / OIDC 标准协议出口未调整。
6. `@radish/http` 新增 `httpStatus` 与 `traceId`，同时保留兼容体 `statusCode`；旧 HTTP 200 失败体仍可解析，真实 401 继续驱动既有 token refresh。

未迁移的历史 Controller 仍可能直接返回已经显式捕获的 `ArgumentException / BusinessException` 文案；它们不属于未知异常泄露，后续领域细粒度错误码只在触达相应业务时继续治理，不扩大本次 Release Go 范围。

## 七、验证结果

### 1. 后端定向测试

- 未知异常包含模拟 SQL、路径或密钥片段时，响应只含安全文案、`System.UnexpectedError` 和 `TraceId`，日志可按同一标识定位完整异常。
- 领域异常分别验证 400、404、409、503，真实 HTTP 状态、响应体状态、Code 和 MessageKey 一致。
- `[ApiController]` 模型校验返回统一 400 与结构化字段错误，不回落 Problem Details。
- 无身份、权限不足、限流、IP 阻断分别返回 401、403、429，且 429 保留 `Retry-After`。
- 已开始的响应、文件流、SignalR、health、Hangfire 和 Scalar 不被 JSON 异常处理器二次写入。
- Auth 的 OIDC 标准错误响应保持协议字段；Razor 未知异常不向页面展示原始消息。

### 2. 前端与兼容测试

- `@radish/http` 对真实 400 / 401 / 403 / 404 / 409 / 429 / 500 均能读取 MessageModel 文案、Code、MessageKey 和 TraceId。
- 真实 401 触发既有 token refresh；刷新失败按既有注销流程处理，不因响应体 200 假成功。
- 兼容期仍能解析历史 HTTP 200 + `IsSuccess=false`，但记录契约状态，不把它作为新接口范式。
- 非 JSON 文件 / HTML / OIDC 响应不被误判为 MessageModel。

### 3. 静态退出检查

- `Radish.Api/Controllers` 不再直接向生产响应拼接未知 `ex.Message`。
- 关键 API 不再用异常文案判断 HTTP 状态。
- 未知异常没有重复 Controller 日志与全局 Error 日志；完整异常只在服务端记录一次。
- 本次正式发布关键 API 的真实 HTTP 状态与契约体一致，前端错误恢复测试通过。

自动化验证结果：

- `dotnet build Radish.Api/Radish.Api.csproj -c Debug --no-restore`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore`：588 passed、1 个 PostgreSQL 环境测试按条件跳过、0 failed。
- `npm run type-check --workspace=@radish/http` 与 `npm run test --workspace=@radish/http`：通过，6 tests。
- `radish.client`、`radish.console` type-check：通过。
- Controller 异常文案状态分类复扫：0；未知异常、业务异常、TraceId 与真实状态过滤器新增 4 项定向后端测试。

## 八、范围控制与后续停止点

- Q1-B 的 Release Go 停止点是：未知异常安全、统一诊断关联、关键 API 真实状态和前端恢复成立；不是一次性完成全仓所有领域错误码重写。
- 不整体切换到 Problem Details，不引入第二套通用响应模型，不通过全局 Filter 猜测所有历史失败语义。
- 不把 Q1-C 文件令牌、Q2 时间 / 数据库 / 版本、Q3 发布基线、页面提示样式或国际化资源全面翻修混入本批。
- 不删除内部日志和受控失败记录中的所有异常信息；只阻止未分类信息越过对外边界。
- 三个批次已按本记录完成；成组运行态 smoke 仍留到候选验收且需用户当轮确认服务已启动。
