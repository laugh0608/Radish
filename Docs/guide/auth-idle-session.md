# Token 不活跃过期治理

本文记录 `P3-10-B6` 的会话不活跃过期口径。完整认证架构仍以 [认证与权限](./authentication.md) 为准。

## 目标

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
