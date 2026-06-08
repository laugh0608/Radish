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
- 公开资料：`User/GetPublicProfile` 的 `identifier` 支持 `usr_...` PublicId 与旧 LongId 字符串双读，返回 `UserPublicProfileVo.VoPublicId / VoUserId`
- 我的浏览历史 / 最近访问

### 公开榜单

公开榜单以 Scalar / OpenAPI 和 [排行榜系统](/features/leaderboard) 为准。当前重点接口包括：

- `Leaderboard/GetLeaderboard`
- `Leaderboard/GetTypes`
- `Leaderboard/GetMyRank`

用户类榜单项同时返回内部用户 ID 和公开用户 ID：

- `voUserId`：内部 LongId 字符串，继续用于需要内部用户 ID 的接口。
- `voUserPublicId`：`usr_...` 公开标识，公开主页跳转与分享优先使用。

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
