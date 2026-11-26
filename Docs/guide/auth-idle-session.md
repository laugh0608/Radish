# Token 不活跃过期治理

普通 Web 开启同源会话后使用 `radish-web` 的服务端期限。以下 `P3-10-B6` 规则继续用于旧 Web 和 Console，Flutter 的客户端策略沿用现状。[认证与权限](./authentication.md)

## 普通 Web 同源会话

- Auth 按服务端时间执行 7 天不活跃窗口和自会话创建起 30 天的绝对上限。前台真实指针或键盘操作可节流上报活动。页面恢复、定时刷新和 Hub 心跳不延长会话。
- 短 access token 最长 5 分钟，并截断在两道会话期限中较早的截止点。到期由 `/web/session/status` 或 `/web/session/token` 确认，浏览器清理内存状态并停止认证 Hub。显式退出成功后，当前页面优先完成 OIDC 登出，受保护页面的自动登录和迟到的登录响应都不能覆盖这次导航。Auth 发出的 `post_logout_redirect_uri` 使用 `radish-web` 登记时的精确字符串。
- 刷新回连暂时失败或返回的短令牌已经到期时，Auth 重新读取当前会话。确认会话到期后，当次 `/web/session/token` 即返回 `401` 并清除会话 Cookie。会话仍有效时返回 `503` 和 `Retry-After`，浏览器保留 Cookie 稍后重试。
- 普通 Web 的开关、旧会话迁移、隔夜与到期验收统一见[同源会话专题](../features/auth-session-overnight-recovery-plan.md#web-session-repair)。

## 旧 Web 与 Console 目标

- 连续多天不访问页面后，本地会话应自动退出，refresh token 也不能继续无感续期。
- 退出只代表当前客户端长期不活跃，不等同于用户主动全局登出，因此不调用 `endsession`。
- 公开阅读页不应因登录态过期整体失效；受保护页面应按现有登录回流进入重新登录。

## 配置

`Radish.Auth` 使用 `OpenIddict:Server:IdleSession` 控制 idle 窗口：

```json
{
  "OpenIddict": {
    "Server": {
      "IdleSession": {
        "Enable": true,
        "IdleTimeoutDays": 7,
        "ClockSkewSeconds": 60,
        "ClientActivityFutureToleranceSeconds": 60
      }
    }
  }
}
```

- `IdleTimeoutDays`：连续不活跃天数，默认 `7`。
- `ClockSkewSeconds`：服务端判定过期时允许的时间偏移，默认 `60`。
- `ClientActivityFutureToleranceSeconds`：客户端上报时间允许超前服务端的偏移，超过后按服务端当前时间收敛。

## 运行时规则

- 前端登录成功后记录最近页面活跃时间，活动来源包括 focus、`pageshow`、可见态恢复、鼠标 / 触控 / 键盘 / 滚动。
- refresh 请求附加 `radish_last_active_at`，值为秒级 Unix 时间戳。
- Auth Server 在 refresh token 成功签发 / 刷新时写入 `radish_idle_last_active_at` claim。
- Auth Server 刷新校验同时读取 refresh token 内 claim 和客户端上报参数，以较新的时间作为有效最近活跃时间。
- 超过 idle 窗口时，Auth Server 拒绝刷新并返回 `invalid_grant / session_idle_expired`。
- 缺少 idle claim 的历史 refresh token 按兼容会话处理，下一次成功刷新后写入新 claim。

## 前端行为

- `radish.client` 和 `radish.console` 都会在本地先判定 idle 过期，避免继续发送已知失效的 refresh 请求。
- 收到 `session_idle_expired` 或本地判定过期后，清理 access token、refresh token、过期时间和最近活跃时间。
- `radish.console` 跳转到 `/console/login?auto=1&reason=idle`。
- `radish.console` 和 `radish.client` 都会在保存 token 时写入 `refresh_at`；刷新前置窗口按 `expires_in` 动态计算，并设置最小 / 最大边界，避免短有效期 token 刚签发就因为固定 5 分钟缓冲立即进入刷新循环。
- `radish.client` 触发统一 token 过期事件，通知 / 聊天 Hub 停止连接，评论 Hub 断开后按已加入帖子组以匿名连接恢复。
- 重新登录成功后会强制刷新最近活跃时间，避免继承旧 idle 状态。

## 验证入口

代码验证优先覆盖：

```bash
dotnet test Radish.Api.Tests
npm run test --workspace=radish.client
npm run type-check --workspace=@radish/http
npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run validate:identity
```

页面真实联调按 [页面真实联调与浏览器 Smoke 规则](./browser-smoke.md) 执行，默认使用 Gateway，同时覆盖 PC 与移动端视图。重点检查：

- 登录成功后最近活跃时间写入。
- refresh 请求携带 `radish_last_active_at`。
- idle 过期后退出登录 UX。
- 前台恢复、focus、`pageshow` 和受保护请求触发的会话恢复。
- 通知 / 聊天 Hub 停止连接，评论详情页以匿名订阅恢复。
