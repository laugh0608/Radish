# 2026-02 第一周 (02-03 至 02-09)

## 2026-02-05 (周四)

### 论坛交互与 UI 组件

- **Icon 渲染兜底**：为 `@radish/ui/Icon` 增加统一样式兜底，修复紧凑按钮中图标不显示的问题
- **MarkdownEditor 扩展**：新增 `theme` 与 `toolbarExtras` 支持，便于统一弹窗配色与工具栏扩展
- **论坛发帖体验**：发帖弹窗配置项收敛至编辑器工具栏，整体布局与交互一致化

## 2026-02-03 (周二)

### 前端认证与续期

- **OIDC 登录 scope 补全**：前端登录请求加入 `offline_access`，确保下发 refresh_token
- **短 Token 续期策略优化**：`tokenService` 记录 `token_expires_at`/`token_refresh_at`，动态提前量刷新以适配 3 分钟 Token
- **回调与刷新一致化**：OIDC 回调与自动刷新统一走 `tokenService` 写入过期与刷新时间
- **个人中心请求统一化**：Profile/Balance 改用 `@radish/ui` 的 `apiGet`，401 时可自动刷新并重试
