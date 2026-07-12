# API 说明索引

本文是 Radish 固定文档中的 API 手册入口，负责回答三个问题：

- 现在去哪看接口定义
- 当前接口遵循哪些通用约定
- 某个领域改动后，应该补哪份手工回归资产

本文不替代 Scalar / OpenAPI，也不替代专题 `.http` 脚本。

## 1. 当前权威来源

### 在线接口定义

- Scalar UI：`https://localhost:5000/scalar`
- OpenAPI JSON：
  - `https://localhost:5000/openapi/v1.json`
  - `https://localhost:5000/openapi/v2.json`

### 仓库内手工回归资产

- 目录：[`Radish.Api.Tests/HttpTest`](</D:/Code/Radish/Radish.Api.Tests/HttpTest>)
- 使用说明：[`Radish.Api.Tests/HttpTest/README.md`](</D:/Code/Radish/Radish.Api.Tests/HttpTest/README.md>)

如果你要确认“接口今天到底长什么样”，优先顺序建议是：

1. 先看 Scalar / OpenAPI
2. 再看对应 `.http` 脚本是否已覆盖回归入口
3. 需要更细专题说明时，再看对应 `guide/` 或 `features/` 文档

## 2. 通用接口约定

### 路径与版本

- 当前采用 **URL 路径版本控制**
- 常见格式：`/api/v{version}/{Controller}/{Action}`
- 当前仓库已存在 `v1` 与 `v2` 文档输出

### 认证方式

- 前端与交互式文档默认走 OIDC / Bearer Token
- 本地调试时可通过 `Radish.Api.AuthFlow.http` 或 Scalar OAuth 完成授权

### 通用响应壳

后端统一响应结构以 `MessageModel<T>` 为准，前端通常映射为：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "操作成功",
  "responseData": {}
}
```

### 错误与异常契约

- `MessageModel<T>` 的 `statusCode` 必须与真实 HTTP 状态保持一致；前端不得只根据响应体中的成功字段判断网络结果。
- `BusinessException` 由全局 `ApiExceptionHandler` 转换为稳定的 HTTP 状态、错误码、`messageKey` 和用户可读消息。
- 未知异常统一返回 `500 + UnexpectedError`，响应中不暴露原始 `ex.Message`、堆栈、SQL 或内部路径。
- 每个错误响应携带或复用 `TraceId / X-Correlation-ID`，用于关联日志和用户反馈。
- 全局异常中间件只处理尚未开始写入、且属于 `MessageModel` API 的响应；非 API 响应或已经开始传输的异常重新抛给 ASP.NET Core，不能被吞掉或伪装为成功。
- Controller 可返回业务失败，但必须同步真实 HTTP 状态；不要依赖统一异常处理器修正错误的 Controller 状态码。

前端 `@radish/http` 会保留真实 HTTP 状态、解析历史 HTTP 200 失败体，并对错误对象中的 token、密码、cookie、authorization 等字段脱敏。页面应使用统一客户端和错误解析，不自行拼接 `fetch` 异常协议。

### 分页壳

分页接口通常返回：

```json
{
  "page": 1,
  "pageSize": 20,
  "dataCount": 100,
  "pageCount": 5,
  "data": []
}
```

### 媒体资源口径

- 附件公开资源统一走 `/_assets/attachments/{id}`
- 缩略图统一走 `/_assets/attachments/{id}/thumbnail`
- 正文中的资源引用协议统一为 `attachment://{id}`

### 外部 ID 字符串安全

- 服务端内部仍可使用 Snowflake `long` 主键。
- API 响应、通知 `extData`、公开路由、深链参数、Console 查询参数和 Flutter handoff 中的外部对象 ID，前端都应按字符串消费。
- JavaScript / TypeScript 侧禁止把用户 ID、帖子 ID、评论 ID、订单 ID、商品 ID、通知 ID、角色 ID、资源 ID、API 模块 ID 等外部 long 标识提前转成 `number`。
- Dart / Flutter 侧也应优先把外部对象 ID 建模为 `String`，只有分页、数量、金额、排序权重和枚举状态等真实数值进入 `int`。
- 需要正式切换 `PublicId` 的对象按 [ID 与联邦路线图](/architecture/id-and-federation-roadmap) 推进；当前要求先保证仍暴露的 LongId 字符串安全。
- 当前已进入 PublicId 首批的对象：
  - `Post.PublicId`：公开 forum 详情、分享、canonical、通知和回流优先使用。
  - `User.PublicId`：公开主页和公开榜单用户跳转优先使用；公开帖子 / 评论 / 统计等内部接口仍使用 LongId 字符串。
- 用户公开展示不使用登录名或邮箱。公开页面、公开榜单、提及搜索和关系链结果优先展示 `DisplayName#PublicIndex` 派生出的 `DisplayHandle`；`PublicIndex` 只用于展示、搜索和人工辨识，不替代 `PublicId` 作为公开路由主键。

## 3. 当前模块索引

下面的分组优先按“手工回归入口”组织，而不是按 Controller 逐个展开。

### 认证与基础

- `Radish.Api.AuthFlow.http`
- `Radish.Api.Smoke.http`
- `Radish.Api.Tenant.http`

用途：

- 获取访问令牌
- 做最小链路验证
- 验证租户相关接口与上下文
- 读取和更新当前登录用户基础个人资料：`User/GetMyProfile`、`User/UpdateMyProfile`

### 社区与论坛

- `Radish.Api.Community.http`
- `Radish.Api.Forum.Core.http`
- `Radish.Api.Forum.Comment.http`
- `Radish.Api.Forum.Poll.http`
- `Radish.Api.Forum.Question.http`
- `Radish.Api.Forum.Lottery.http`

对应范围：

- 社区分发
- 帖子 / 评论
- 投票 / 问答 / 抽奖
- 公开与登录态下的核心论坛接口

### 用户与个人中心

- `Radish.Api.User.Profile.http`

对应范围：

- 当前用户资料
- 公开资料：`User/GetPublicProfile` 的 `identifier` 支持 `usr_...` PublicId 与旧 LongId 字符串双读，返回 `UserPublicProfileVo.VoPublicId / VoPublicIndex / VoDisplayName / VoDisplayHandle / VoUserId`
- 公开资料内容：`User/GetPublicUserStats`、`Post/GetPublicUserPosts`、`Comment/GetPublicUserComments` 均使用 `identifier` 查询公开用户内容，公开页面不得再通过 `UserPublicProfileVo.VoUserId` 串接后续请求
- 当前用户资料：`User/GetMyProfile` 返回 `VoPublicId / VoPublicIndex / VoDisplayHandle`，同时保留本人可见的 `VoUserName / VoUserEmail`
- 提及搜索：`User/SearchForMention` 只面向登录态用户，搜索范围限定为 `DisplayName`、`PublicIndex`、完整 `DisplayName#PublicIndex` 或 `PublicId`；不得把 `LoginName`、`Email` 或内部 `Id` 做普通用户公开搜索字段
- 我的浏览历史 / 最近访问

### 文档与 Wiki

- `Radish.Api.Wiki.http`

文档接口当前仍由 `WikiController` 承载，外部产品口径统一称为“文档”。入口职责按 [文档系统](/guide/document-system) 拆分：

- 公开 `/docs` 使用只读读取接口：
  - `Wiki/GetTree`
  - `Wiki/GetList`
  - `Wiki/GetBySlug/{slug}`
  - `Wiki/GetById/{id}`
- 正式 Web 作者入口当前沿用 `Admin/System` 写权限，使用：
  - `Wiki/Create`
  - `Wiki/Update/{id}`
  - `Wiki/GetRevisionList/{id}`
  - `Wiki/GetRevisionDetail/{revisionId}`
- Console `/documents` 使用治理接口：
  - `Wiki/AdminGetList`
  - `Wiki/AdminGetTree`
  - `Wiki/AdminGetById/{id}`
  - `Wiki/Publish/{id}`
  - `Wiki/Unpublish/{id}`
  - `Wiki/Archive/{id}`
  - `Wiki/Delete/{id}`
  - `Wiki/Restore/{id}`
  - `Wiki/UpdateAccessPolicy/{id}`
  - `Wiki/Rollback/{revisionId}`
  - `Wiki/ImportMarkdown`
  - `Wiki/ExportMarkdown/{id}`

权限边界：

- 公开读取接口允许匿名进入，但服务端按文档状态、删除状态、可见性、登录态和角色过滤结果。
- 作者入口不新增 `console.docs.create` 或 `console.docs.edit`，当前由 `SystemOrAdmin` 后端策略和前端 `Admin/System` 入口可见性共同限制。
- Console 治理接口必须走 `console.docs.*` 权限映射，权限覆盖见 [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)。
- 固定文档 `SourceType=builtin` 只读，不允许通过作者入口或 Console 回写仓库 `Docs/` 源文件。

### 公开榜单

公开榜单以 Scalar / OpenAPI 和 [排行榜系统](/features/leaderboard) 为准。当前重点接口包括：

- `Leaderboard/GetLeaderboard`
- `Leaderboard/GetTypes`
- `Leaderboard/GetMyRank`

用户类榜单项同时返回内部用户 ID 和公开用户 ID：

- `voUserId`：内部 LongId 字符串，继续用于需要内部用户 ID 的接口。
- `voUserPublicId`：`usr_...` 公开标识，公开主页跳转与分享优先使用。
- `voUserPublicIndex / voUserDisplayName / voUserDisplayHandle`：公开展示与人工辨识字段，页面展示优先使用 `voUserDisplayHandle`，不要回显登录名或邮箱。

### 电子宠物

电子宠物接口以 Scalar / OpenAPI 和 [Radish 电子宠物开发计划](/features/radish-pet-roadmap) 为准。当前 `PetController` 全部需要登录态 `Client` 授权，使用当前用户身份定位自己的宠物：

- `GET /api/v1/Pet/GetMy`：读取当前用户宠物状态；未领取时返回明确空态，不隐式创建宠物。
- `POST /api/v1/Pet/Claim`：领取默认宠物；同一用户重复领取返回已有宠物，不创建多只宠物。
- `PUT /api/v1/Pet/UpdateProfile`：更新宠物名称或公开展示开关；当前不处理装饰购买或经济消耗。
- `POST /api/v1/Pet/Care`：执行 `feed / clean / play / rest` 四类照顾动作；服务端校验幂等键、每日次数、冷却、状态上下限和成长阶段。
- `GET /api/v1/Pet/GetLogs`：查询当前用户宠物状态流水；未领取时返回分页空态。

`PetProfileVo` 会返回宠物 `VoPublicId`、名称、形态、成长阶段、心情、饱食度、清洁度、精力、成长值、公开展示开关、最后照顾时间和 `VoCareActions`。前端只展示动作可用性与反馈，不计算最终状态；状态变化以服务端返回和 `PetStatLogVo` 为准。

### 附件与上传

- `Radish.Api.Attachment.Upload.http`
- `Radish.Api.Attachment.Manage.http`
- `Radish.Api.Attachment.Guardrail.http`
- `Radish.Api.Attachment.Chunk.http`
- `Radish.Api.Attachment.Token.http`

### 限流与边界能力

- `Radish.Api.RateLimit.Core.http`
- `Radish.Api.RateLimit.Policy.http`
- `Radish.Api.RateLimit.Edge.http`

### 商城与交易

商城购买 / 订单 / 背包接口以 Scalar / OpenAPI 与商城说明文档为准。当前重点接口包括：

- `Shop/CheckCanBuy/{productId}`
- `Shop/Purchase`
- `Shop/GetMyOrders`
- `Shop/GetOrder/{orderId}`
- `Shop/GetMyBenefits`
- `Shop/GetMyInventory`
- `Shop/AdminGetOrders`
- `Coin/AdminGetTransactions`

管理端购买排障当前依赖订单与胡萝卜流水的业务上下文关联：商城购买扣款流水使用 `transactionType=CONSUME`、`businessType=Order`、`businessId=OrderId`，Console 可从订单详情跳转到上述筛选结果。

### 其他专题

- `Radish.Api.Chat.http`
- `Radish.Api.Coin.http`
- `Radish.Api.CommentHighlight.http`
- `Radish.Api.Experience.http`
- `Radish.Api.Sticker.http`
- `Radish.Api.Wiki.http`

## 4. 当前已独立成文的 API 专题

目前 `Docs/` 中已经明确以“API 文档”名义维护的专题并不多，现有代表页包括：

- [通知系统 API 文档](/guide/notification-api)

这说明当前仓库的 API 说明更多依赖于：

- Scalar / OpenAPI 自动文档
- `.http` 回归脚本
- 少量高复杂度模块的专题文档

如果后续某个领域接口明显膨胀，建议新增 `guide/<domain>-api.md` 作为模块级手册，而不是继续把信息散落到回归记录里。

## 5. 新增或修改接口时应同步什么

至少同步以下三类资产：

1. Controller / DTO / OpenAPI 注释与实际实现
2. 对应 `.http` 脚本或可替代的手工回归入口
3. 如果改动影响外部协作理解，再补模块级说明文档

## 相关文档

- [鉴权与授权指南](/guide/authentication)
- [Gateway 服务网关](/guide/gateway)
- [前端 API 客户端使用指南](/frontend/api-client)
- [验证基线](/guide/validation-baseline)
