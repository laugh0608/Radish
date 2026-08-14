# 开放平台设计

本文档冻结 Radish 当前 OIDC 客户端治理边界，并区分已落地能力与未来第三方开放平台规划。当前阶段只治理管理员创建和维护的 OpenIddict 客户端，不开放自助注册、审核、配额或开发者门户。

## 1. 当前目标与停止线

当前目标：

1. 为正式 Web、Console 与 API 文档等内部产品提供统一 OIDC 认证。
2. 允许具备 Console 权限的管理员动态创建、查询、编辑、软删除和轮换客户端密钥。
3. 让客户端类型、Grant、Scope、回调地址、Consent 与 PKCE 配置保持为 OpenIddict 单一真相源。
4. 对分页、搜索、权限和一次性密钥展示提供可验证的管理契约。

当前停止线：

- 不提供第三方开发者自助注册、应用审核、状态流转、配额、调用统计、SDK 或 Webhook。
- 不引入独立 `RadishApplication` 业务实体；客户端事实直接存储于 OpenIddict application 表。
- 不提供独立启用 / 禁用状态操作；需要撤销客户端时使用软删除，恢复与审核状态待未来专题设计。
- 不展示或回读已保存的 Client Secret；Secret 只在创建 confidential 客户端或重置密钥成功后返回一次。

## 2. 系统归属

| 能力 | 当前归属 | 说明 |
| --- | --- | --- |
| OIDC Server | `Radish.Auth` | 承载 `/connect/authorize`、`/connect/token`、`/connect/endsession` 与 `/connect/userinfo` |
| OpenIddict application 存储 | `Radish.Auth.OpenIddict` 数据库 | 是 ClientId、客户端类型、URI、权限和 properties 的权威真相源 |
| 客户端管理 API | `Radish.Api` | 通过 `IOpenIddictApplicationManager` 写入，通过 EF Core 权威查询服务分页读取 |
| 管理界面 | `Frontend/radish.console` `/applications` | PC 连续表格、mobile 卡片与 Bottom Sheet 工作流 |
| API 客户端 | `Frontend/radish.console/src/api/clients.ts` | 复用 `@radish/http` 统一请求与错误契约 |

当前预置内部客户端：

| ClientId | 用途 |
| --- | --- |
| `radish-client` | 社区主站与 WebOS 正式入口 |
| `radish-console` | Console 独立登录、回调和登出 |
| `radish-scalar` | Scalar API 文档调试授权 |

`radish-shop` 遗留种子已经移除，商城能力不再单独占用官方 OIDC 客户端。

## 3. 当前数据契约

### 3.1 列表与详情

`ClientVo` 对 Console 暴露以下稳定字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | `string` | OpenIddict application 主键 |
| `clientId` | `string` | 唯一 ClientId |
| `displayName` | `string?` | 显示名称 |
| `description` | `string?` | 应用描述 |
| `developerName` / `developerEmail` | `string?` | 管理员登记的开发者信息 |
| `clientType` | `public \| confidential` | OpenIddict 客户端类型 |
| `grantTypes` | `string[]` | 授权类型数组 |
| `redirectUris` | `string[]` | 回调地址数组 |
| `postLogoutRedirectUris` | `string[]` | 登出回调地址数组 |
| `scopes` | `string[]` | Scope 数组 |
| `consentType` | `string?` | `explicit / implicit / external / systematic` |
| `requirePkce` | `boolean` | 是否要求 PKCE |
| `createdAt` | `datetime?` | 从 application properties 映射的创建时间 |

数组字段在空值时也返回空数组，不再以逗号字符串或 nullable 集合制造前后端分支。

### 3.2 创建与编辑

创建请求使用 `clientType` 明确区分 `public` 与 `confidential`；`requireClientSecret` 仅保留为旧调用方兼容字段，新代码不得继续依赖它表达客户端类型。

支持的 Grant：

- `authorization_code`
- `client_credentials`
- `refresh_token`
- `password`

核心一致性规则：

- `public` 客户端不得使用 `client_credentials`。
- 使用 `authorization_code` 时必须至少提供一个 `redirectUri`。
- Grant、Scope、Redirect URI 与 Post Logout Redirect URI 会去空、去重并写回 OpenIddict descriptor。
- 更新时由当前完整 descriptor 重建受管理的 endpoint、grant、response type 与 scope permissions，避免增量修改遗留失效权限。
- 编辑不允许改变 `clientId` 或 `clientType`；如需改变安全身份，应新建客户端并迁移调用方。

## 4. API 契约

客户端 API 使用版本化 action 路由：

| 操作 | 路由 | Console 权限 |
| --- | --- | --- |
| 列表 | `GET /api/v1/Client/GetClients` | `console.applications.view` |
| 详情 | `GET /api/v1/Client/GetClient/{id}` | `view` 或 `edit` |
| 创建 | `POST /api/v1/Client/CreateClient` | `console.applications.create` |
| 编辑 | `PUT /api/v1/Client/UpdateClient/{id}` | `console.applications.edit` |
| 软删除 | `DELETE /api/v1/Client/DeleteClient/{id}` | `console.applications.delete` |
| 重置 Secret | `POST /api/v1/Client/ResetClientSecret/{id}` | `console.applications.reset-secret` |

所有接口同时要求 `AuthorizationPolicies.Client`。按钮显隐只是体验层，Controller 权限是最终边界。

### 4.1 权威分页与搜索

列表参数：

```http
GET /api/v1/Client/GetClients?page=1&pageSize=20&keyword=console
```

- `page >= 1`
- `1 <= pageSize <= 100`
- `keyword` 去除首尾空白后最长 100 字符
- 搜索范围为 `ClientId` 与 `DisplayName`
- 排序固定为 `ClientId ASC, Id ASC`
- 软删除项在计数和数据页中都被排除

响应使用统一 `MessageModel<PageModel<ClientVo>>`，其中 `dataCount` 与 `pageCount` 均来自数据库权威查询。Console 不得通过请求大页后在内存中假分页。

### 4.2 一次性 Secret

创建 confidential 客户端和重置 Secret 成功时，响应中的 `clientSecret` 只展示一次。public 客户端创建成功时该字段为 `null`，并且重置接口会拒绝 public 客户端。

Console 必须：

1. 在离开结果层前要求管理员确认已经保存 Secret。
2. 结果层关闭后清空前端内存中的 Secret。
3. 不把 Secret 写入 URL、日志、持久化状态或列表 / 详情模型。
4. 重置前明确提示旧 Secret 会立即失效。

## 5. Console 应用管理

### 5.1 列表状态

`/applications` 的 `page`、`pageSize` 与 `keyword` 以 URL query 为唯一列表状态源。PC 使用连续 Table，mobile 重排为卡片；两端共享同一请求代次、分页和权限状态机。

页面区分：

- `ready`：当前 URL 快照已经成功加载，允许执行写操作。
- `stale`：新请求进行中但旧数据仍可见，冻结当前写操作，避免对错误快照操作。
- `unavailable`：当前查询不可用，展示错误恢复入口并冻结写操作。

搜索提交会回到第 1 页；改变 pageSize 也回到第 1 页。翻页、前进 / 后退与刷新都必须从 URL 恢复同一查询。

### 5.2 创建与编辑

PC 使用 Modal，mobile 使用 Bottom Sheet。表单覆盖显示信息、客户端类型、Grant、Scope、Consent、PKCE 与回调地址；编辑打开前必须读取详情。

- 提交期间锁定重复提交与关闭。
- 表单变脏后，关闭或浏览器离开都需要确认。
- 所有入口与最终 handler 都复核对应 Console 权限。
- 删除使用明确确认，并在成功后重新读取当前权威页。

### 5.3 视觉继承

Applications 属于 `R3-C04` 普通资源页，继承 `R1-C01` Console 表格 / 明细工作台的结构、筛选、状态和移动端重排规则。本批不新增或修改 Pencil 代表画板。

## 6. Scope 与授权确认

`Radish.Auth` 当前注册：

- `openid`
- `profile`
- `email`
- `offline_access`
- `radish-api`

Scope permission 表达客户端可请求的范围，业务 API 的最终权限仍由 token、角色和服务端授权策略共同裁决；不得把 `radish-api` 解释为自动取得全部业务权限。

授权确认由客户端 `ConsentType` 与 Auth 授权流程共同决定。预置客户端可由 seed 固定差异化策略；动态客户端默认使用 `explicit`。具体运行时行为以 [鉴权与授权指南](/guide/authentication) 为准。

## 7. 安全与审计

- OpenIddict 负责 Secret 哈希与凭证校验，管理 API 不回读原始 Secret。
- Redirect URI 严格匹配；生产客户端应使用 HTTPS 回调。
- public + authorization code 场景应启用 PKCE。
- 创建、编辑、软删除和重置 Secret 写入 application properties 中的审计元数据。
- 软删除客户端不会进入 Console 列表，也不能通过详情或写接口继续操作。
- API 与前端日志禁止记录 Secret、Bearer token 或完整敏感请求体。

## 8. 未来第三方开放平台

以下能力尚未进入当前开发范围，需要独立专题设计后才能实施：

1. 开发者身份、组织与自助应用注册。
2. 草稿、待审核、通过、驳回、停用与恢复状态机。
3. 第三方可申请 Scope 白名单与敏感 Scope 审核。
4. 配额、调用统计、异常授权审计和告警。
5. 开发者文档、SDK、Webhook 与密钥轮换协作流程。

未来设计不得复用当前软删除字段冒充审核状态，也不得在缺少产品状态机时给现有 Console 增加“第三方审核”占位入口。

## 9. 验证入口

日常代码侧验证：

```bash
dotnet test Radish.Api.Tests
npm run test --workspace=radish.console
npm run type-check:strict --workspace=radish.console
npm run lint --workspace=radish.console
npm run build --workspace=radish.console
```

分页、过滤和稳定排序由 `ClientApplicationQueryServiceTest` 覆盖；Controller 契约与 public / confidential Secret 边界由 `ClientControllerTest` 覆盖；Console URL、权限和一次性 Secret 契约由 `r3C04ApplicationsContract.test.ts` 覆盖。

真实 Gateway PC / mobile smoke 只在 Applications 专题准备成组验收时执行，并遵循[浏览器联调指南](/guide/browser-smoke)的启动授权要求。

## 10. 关联文档

- [鉴权与授权指南](/guide/authentication)
- [Console 模块说明](/guide/console-modules)
- [R3-C04 普通资源风险拆批审计](/records/f4-r-r3-c04-console-ordinary-resources-readiness-audit-2026-08-11)
- [R3-C04-C Applications 实现记录](/records/f4-r-r3-c04-c-console-applications-implementation-2026-08-12)
- [当前规划](/planning/current)
