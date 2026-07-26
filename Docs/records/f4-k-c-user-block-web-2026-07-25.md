# F4-K-C Pencil 与正式 Web 记录

> 日期：2026-07-25（Asia/Shanghai）
>
> 分支：`dev`
>
> 范围：F4-K-C；不包含 Gateway 成组验收、WebOS / Flutter / Tauri 新功能或旧 Direct 字段删除

## 完成内容

- 在公开设计源补充公开主页 PC / mobile 屏蔽确认与通用不可互动状态；在私域设计源补充圈子权威刷新、Direct 只读和 `/me/blocked` PC / mobile 页面。
- 扩展关注状态响应的 `VoCanDirectMessage / VoCanInteract / VoIsBlockedByCurrentUser`，正式 Web 不再从关注、会话或错误文案推断屏蔽方向。
- 新增 UserBlock Web API 消费、本人屏蔽列表、分页、空态、失败重试、解除确认和受控登录回流。
- 公开主页与 Direct 使用稳定、账号隔离的 operation key；失败保留同一 key，成功后清理。Direct 新运行时不再调用旧 Block / Unblock 入口。
- 关系版本通过 Chat Hub 与 Notification Hub 仅广播字符串 revision；客户端按账号和长整数顺序去重，只触发公开主页、圈子、消息和通知权威重取。
- 屏蔽与解除确认支持键盘 Escape、初始焦点、双语、响应式布局和语义主题 token；公开内容保持可见，解除屏蔽不恢复关注。

## 验证

- Pencil：新增画板逐一执行 `snapshot_layout(problemsOnly=true)`，无布局问题；抽查 `/me/blocked` PC 与 Direct 移动只读截图，无裁切、坍塌或溢出。
- `npm run type-check --workspace=@radish/http`
- `npm run type-check --workspace=radish.client`
- `npm run lint --workspace=radish.client`
- `npm run test --workspace=radish.client`：`458 / 458` 通过。
- `npm run test --workspace=@radish/http`：`19 / 19` 通过。
- `npm run build --workspace=radish.client`：通过；保留既有大 chunk 提示。
- `dotnet test Radish.Api.Tests --no-restore --filter "FullyQualifiedName~UserFollowServiceTest|FullyQualifiedName~UserBlock|FullyQualifiedName~UserInteractionRealtimeNotifier"`：`25` 通过、`2` 个 PostgreSQL 环境用例按配置跳过。
- `npm run validate:baseline:quick`：通过，覆盖四个前端 workspace 类型检查、`@radish/http / @radish/ui / client / console` 测试、敏感字面量、时间语义、权限与身份语义等仓库门禁。

## 未在本批执行

- 未安装或更新依赖。
- 未启动 API、Gateway、Auth 或前端服务。
- 未执行 Gateway PC / mobile 真实 smoke、三账号矩阵、PostgreSQL 容器专项、临时数据清理和六库严格 verify；这些属于 F4-K-D 成组验收。
