# F4-R R3-C04-C Console Applications 权威目录与一次性 Secret 实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console Applications 列表、创建 / 编辑、软删除与 Client Secret 轮换

## 本批结论

R3-C04-C 已把 Applications 从“API 内存全量读取 + Console 前 100 条本地分页 + 表单契约缺字段”收敛为 OpenIddict 存储权威分页、完整 OIDC 表单契约和一次性 Secret 工作流。现有 `console.applications.*` 权限保持不变；未新增第三方审核、Scope 治理模型、数据库字段或 migration。

## 服务端契约

- `ClientApplicationQueryService` 直接在 OpenIddict EF Core 存储中排除软删除，按 Client ID / 显示名称搜索，按 `ClientId + Id` 稳定排序后执行 `Count / Skip / Take`；`pageSize` 固定在 `1-100`，关键词最多 `100` 字符。
- `ClientVo` 的授权类型、Scopes 与两类回调 URI 改为数组，并显式返回 `clientType / requirePkce / consentType / createdAt`；Console 不再猜测或拆分逗号字符串。
- 创建客户端显式落库 `public / confidential` 类型；只允许既有四类 grant，拒绝公开客户端使用 `client_credentials`，并在进入 OpenIddict 前校验授权码回调和绝对 URI。
- 机密客户端只在创建成功或轮换成功后返回一次明文 Secret；公开客户端返回 `null`，不再用“无需密钥”伪装 Secret。服务端同时拒绝公开客户端执行密钥轮换。
- 密钥轮换 Controller 路由统一为既有前端与文档契约 `/api/v1/Client/ResetClientSecret/{id}`，并增加反射测试防止 action 路由再次漂移。
- 权限、endpoint、response type 与 Scope permission 由同一契约函数重建，避免编辑 grant 后遗留不一致权限；软删除 Client ID 因唯一索引不能复用，接口返回明确诊断。

## Console 页面事实

- `/applications` 的搜索、页码和页大小进入 URL；同查询刷新保留快照并区分 `stale / unavailable`，请求代际忽略过期响应，非 `ready` 时冻结写入口。
- PC 使用连续表格和按需 Modal；Mobile 使用同一数据快照的应用卡片、轻量分页、按需搜索 Bottom Sheet，以及单任务表单 / Secret 结果。
- 编辑先读取权威详情；写 handler 与表单双重复核权限。表单覆盖 client type、grant、Scopes、consent、PKCE、回调 URI 与开发者信息，提交期间禁止关闭，dirty 关闭及浏览器离开必须确认。
- Secret 轮换需要显示 Client ID 和旧密钥立即失效影响的二次确认；结果支持复制，并要求主动确认已保存后才可关闭。关闭时页面清除内存中的明文结果。
- 中英文资源覆盖分页真相、读取状态、客户端类型、授权字段、危险确认和一次性 Secret 提示；未新增硬编码颜色或第二套 HTTP 客户端。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| Applications query / contract / Secret 定向测试 | `11 / 11` 通过，含 SQLite 数据库过滤、搜索、稳定分页、数组契约、公开 / 机密创建、公开客户端轮换拒绝与路由契约 |
| `dotnet test Radish.Api.Tests --no-restore` | `1234 passed / 39 skipped`，总计 `1273` |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0 warning / 0 error` |
| `npm run test --workspace=radish.console` | `94 / 94` 通过，含 R3-C04-C URL、权威分页、响应式、dirty / busy 与 Secret 契约 |
| Console type-check、strict type-check、Lint、production build | 全部通过 |
| 权限、LongId 与 changed hygiene | `check:console-permissions`、`check:long-id-safety`、`check:repo-hygiene:changed` 全部通过；权限扫描只保留既有 `console.hangfire.replay` 未引用警告 |

## 停止线与未执行项

- 未新增第三方应用审核、动态 Scope 模型、客户端使用统计或独立详情页；未修改数据库、migration、权限键或 Pencil。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC `1440 × 900`、Mobile `390 × 844`、最小权限、双语和真实 Secret 写入验收留到获得当轮启动授权后的成组验收。
- 既有三处 `DateTime.Now` baseline 与全仓历史卫生债务继续留在独立维护线。
- 下一批只进入 `R3-C04-D Products`，先关闭 `IsOnSale` 权限绕行，不提前迁移 Stickers 或 Coins。
