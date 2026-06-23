# 认证服务统一指南

## 概述

Radish 项目采用 OIDC (OpenID Connect) 标准进行身份认证，前端通过统一的认证服务 (`auth.ts`) 管理登录、登出和 Token 验证逻辑。本指南介绍如何使用认证服务，以及如何在前端应用中集成认证功能。

当前统一访问口径如下：

- 开发运行：前端开发服务器通过 `VITE_*` 指向 `https://localhost:5000`
- 测试部署：前端优先读取 `/runtime-config.js`，默认回落到 `RADISH_PUBLIC_URL=https://IP:port`
- 生产部署：前端优先读取 `/runtime-config.js`，默认回落到 `RADISH_PUBLIC_URL=https://radish.example.com`
- 除本地联调外，不再推荐前端直连 `http://localhost:5200` 或单独暴露 `https://auth.xxx` / `https://api.xxx` 两套公网入口

## 注册与登录凭证

- B6 后注册页不再收集 `Username / LoginName`；登录凭证固定为 `Email + Password`。
- 注册必填 `Email`、`DisplayName`、密码与确认密码。
- `Email` 必填，按邮箱格式校验，长度不超过 `254`，提交后统一转为小写保存；同一邮箱只能绑定一个有效用户。
- `DisplayName` 必填，长度由 `UserIdentity.DisplayName.MinLength` / `UserIdentity.DisplayName.MaxLength` 系统设置治理；当前只允许中文、英文字母和数字。
- 注册页必须提示 `DisplayName` 会公开展示在个人主页、帖子、评论、聊天、榜单和艾特搜索中，后续修改受次数、冷却时间和滚动窗口限制。
- 登录框只接收邮箱和密码，服务端按规范化邮箱匹配可登录用户，并保持统一的失败提示。
- `User.UserName` 数据库列当前仍承接公开展示名语义；公开页面应展示 `DisplayName#PublicIndex` 或等价 `DisplayHandle`，不得把 `LoginName` 或 `Email` 当作公开昵称。
- `LoginName` 如在数据库层短期保留，只作为历史内部字段；`UserIdentity.LoginName.MinLength / MaxLength` 系统设置仅保留给历史兼容，不再影响新注册或登录。
- 创建用户时由服务端分配 `User.PublicId` 与 `User.PublicIndex`，前端不计算公开索引，也不把公开索引用作登录凭证。

## 认证架构

### 认证流程

```text
前端应用 (Client / Console)
    ↓  统一访问公开入口
Radish.Gateway
    ├─ /Account/**、/connect/** → Radish.Auth (OIDC)
    └─ /api/**、/hubs/**        → Radish.Api

典型流程：
1. 前端跳转 `${getAuthBaseUrl()}/connect/authorize`
2. Gateway 转发到 Auth 完成登录
3. Auth 回调 `${window.location.origin}/oidc/callback`
4. 前端向 `${getAuthBaseUrl()}/connect/token` 交换 token
5. 前端后续通过 `${getApiBaseUrl()}` 访问 API / SignalR
```

### 核心组件

- **Radish.Auth**: OIDC 认证服务器 (基于 OpenIddict)
- **Radish.Gateway**: 统一公网入口，透传 `/Account`、`/connect`、`/api`、`/hubs`
- **Frontend/radish.client/src/services/auth.ts**: 前端认证服务
- **@radish/http**: HTTP 客户端，自动添加 Token

## 认证服务 API

### 文件位置

```
Frontend/radish.client/src/services/auth.ts
```

### 导出方法

#### redirectToLogin()

**用途**: 跳转到 OIDC 登录页面

**方法签名**:
```typescript
function redirectToLogin(options?: { returnPath?: string | null }): void
```

**使用场景**:
- 用户点击登录按钮
- 检测到未登录状态
- Token 过期需要重新登录
- 登录后需要回到特定正式 Web 交易上下文，例如公开商品页进入 `/shop/product/:productId?intent=purchase` 后继续购买，或进入 `/shop/order/:orderId` 查看订单
- 登录后需要回到特定 Web 参与现场，例如 `/circle` 我的圈子，公开帖子详情的轻回应 / 根评论输入区，或论坛作者态 `/forum/compose`、`intent=answer|edit|history`
- 登录后需要回到纯 Web 私域入口，例如 `/notifications`、`/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/messages` 或 `/pet`

**示例**:
```typescript
import { redirectToLogin } from '@/services/auth';

// 登录按钮点击事件
const handleLogin = () => {
  redirectToLogin();
};
```

需要保留登录后上下文时，只允许传入经过约束的返回路径：

```typescript
import { redirectToLogin } from '@/services/auth';

redirectToLogin({
  returnPath: '/shop/product/123?intent=purchase'
});

redirectToLogin({
  returnPath: '/circle?tab=following'
});

redirectToLogin({
  returnPath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?intent=comment'
});

redirectToLogin({
  returnPath: '/forum/compose'
});

redirectToLogin({
  returnPath: '/pet'
});
```

**工作流程**:
1. 构建 OIDC 授权 URL (`/connect/authorize`)
2. 如传入 `returnPath`，先写入 sessionStorage 中的一次性认证返回路径
3. 设置参数:
   - `client_id`: radish-client
   - `response_type`: code
   - `redirect_uri`: {origin}/oidc/callback
   - `scope`: radish-api
   - `culture`: 当前语言设置
4. 跳转到认证服务器

认证返回路径约束：

- 只接受同源相对路径，不接受绝对 URL、`//` 开头路径或包含反斜杠的路径
- 当前只允许以下白名单路径，避免登录回调被外部开放重定向利用：
  - `/desktop`：用于工作台上下文，例如 shop 购买、订单 / 背包入口或 forum 工作台定位
  - `/circle`：用于“我的圈子”登录回流，仅保留 `tab=feed|following|followers` 和正整数 `page`
  - `/notifications`：用于通知中心登录回流，不接受 query 或 hash
  - `/me`：用于我的状态登录回流，不接受 query 或 hash
  - `/messages`：用于消息复访登录回流，可保留合法 `channelId/messageId`，其中存在 `messageId` 时必须同时存在合法 `channelId`
  - `/pet`：用于电子宠物登录回流，不接受 query 或 hash
  - `/forum/post/:postId`：仅用于公开详情参与回流，`postId` 必须是 `pst_...` PublicId 或正整数旧 ID，且必须携带 `intent=comment|quickReply`；可选 `commentId` 必须是正整数
- 普通公开阅读来源、专题返回和分享复制不应进入认证返回路径；公开详情 canonical、OpenGraph、JSON-LD、sitemap 和复制链接不携带 `intent`、`commentId` 或来源状态
- 回调成功后一次性消费；失败、取消或再次登录不会长期保留旧上下文

#### logout()

**用途**: 执行 OIDC 登出，清除本地 Token 并跳转到认证服务器

**方法签名**:
```typescript
function logout(): void
```

**使用场景**:
- 用户点击登出按钮
- 检测到 Token 无效
- 安全退出

**示例**:
```typescript
import { logout } from '@/services/auth';

// 登出按钮点击事件
const handleLogout = () => {
  logout();
};
```

**工作流程**:
1. 清除本地存储:
   - `radish_client_access_token`
   - `radish_client_refresh_token`
   - `radish_client_token_expires_at`
   - `radish_client_token_refresh_at`
   - `cached_user_info`（与当前 access token 身份强绑定的一次性当前用户引导缓存）
2. 构建 OIDC 登出 URL (`/connect/endsession`)
3. 设置参数:
   - `post_logout_redirect_uri`: {origin}
   - `client_id`: radish-client
   - `culture`: 当前语言设置
4. 跳转到认证服务器

#### hasAccessToken()

**用途**: 检查是否有有效的客户端访问令牌

**方法签名**:
```typescript
function hasAccessToken(): boolean
```

**使用场景**:
- 判断用户是否已登录
- 决定是否显示登录/登出按钮
- 决定是否建立 WebSocket 连接

**示例**:
```typescript
import { hasAccessToken } from '@/services/auth';

// 检查登录状态
if (hasAccessToken()) {
  console.log('用户已登录');
  // 显示用户信息
} else {
  console.log('用户未登录');
  // 显示登录按钮
}
```

**注意事项**:
- 此方法仅检查 Token 是否存在，不验证 Token 是否有效
- 如果需要验证 Token 有效性，应调用 API 接口

## 使用示例

### 1. 登录/登出按钮

```typescript
import { useState, useEffect } from 'react';
import { redirectToLogin, logout, hasAccessToken } from '@/services/auth';
import { Button } from '@radish/ui';

export const AuthButton: React.FC = () => {
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  useEffect(() => {
    setIsLoggedIn(hasAccessToken());
  }, []);

  const handleLogin = () => {
    redirectToLogin();
  };

  const handleLogout = () => {
    logout();
  };

  return (
    <div>
      {isLoggedIn ? (
        <Button onClick={handleLogout}>登出</Button>
      ) : (
        <Button onClick={handleLogin}>登录</Button>
      )}
    </div>
  );
};
```

### 2. 路由守卫

```typescript
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { hasAccessToken, redirectToLogin } from '@/services/auth';

export const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const navigate = useNavigate();

  useEffect(() => {
    if (!hasAccessToken()) {
      // 未登录，跳转到登录页
      redirectToLogin();
    }
  }, [navigate]);

  if (!hasAccessToken()) {
    return <div>正在跳转到登录页...</div>;
  }

  return <>{children}</>;
};
```

### 3. API 请求集成

```typescript
import { apiGet } from '@radish/http';
import { hasAccessToken, redirectToLogin } from '@/services/auth';

export async function getUserProfile() {
  // 检查是否已登录
  if (!hasAccessToken()) {
    redirectToLogin();
    return null;
  }

  // 发送 API 请求（自动添加 Token）
  const response = await apiGet<UserProfile>('/api/v1/User/GetProfile', {
    withAuth: true,
  });

  if (!response.ok) {
    // Token 可能已过期
    if (response.statusCode === 401) {
      redirectToLogin();
    }
    return null;
  }

  return response.data;
}
```

### 4. WebSocket 认证集成

```typescript
import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';
import { getApiBaseUrl } from '@/config/env';
import { hasAccessToken } from '@/services/auth';
import { tokenService } from '@/services/tokenService';

let connection: HubConnection | null = null;

export async function connectToNotificationHub() {
  // 检查是否已登录
  if (!hasAccessToken()) {
    console.log('未登录，跳过 WebSocket 连接');
    return;
  }

  // 获取 Token
  const token = tokenService.getAccessToken();
  const apiBaseUrl = getApiBaseUrl();

  // 创建连接
  connection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/notification`, {
      accessTokenFactory: () => token || '',
    })
    .withAutomaticReconnect()
    .build();

  try {
    await connection.start();
    console.log('WebSocket 连接成功');
  } catch (error) {
    console.error('WebSocket 连接失败:', error);
  }
}
```

### 5. 自动刷新 Token

Radish 使用 **refresh_token** 自动续期 access_token，需要在登录时申请 `offline_access` scope：

```
scope=openid profile offline_access radish-api
```

前端通过 `@radish/http` + `tokenService` 统一处理刷新：

```typescript
import { configureTokenRefresh } from '@radish/http';
import { tokenService } from '@/services/tokenService';

configureTokenRefresh({
  refreshEndpoint: `${authServerBaseUrl}/connect/token`,
  getRefreshToken: () => tokenService.getRefreshToken(),
  onTokenRefreshed: (accessToken, refreshToken) => {
    tokenService.setTokenInfoFromJwt(accessToken, refreshToken);
  },
  onRefreshFailed: () => {
    tokenService.clearTokens();
  }
});

tokenService.startAutoRefresh();
```

**说明**：

- `tokenService` 会持久化 `radish_client_token_expires_at` 与 `radish_client_token_refresh_at`，短 Token 使用动态提前量刷新
- 401 响应会触发刷新并重试（通过 `apiGet/apiPost` 等统一客户端）
- 没有 `radish_client_refresh_token` 时不会自动续期，需要重新登录

## Token 管理最佳实践

### 1. Token 存储

**推荐方式**: 通过 `tokenService` 管理 localStorage（不要在业务代码直接读写）

```typescript
import { tokenService } from '@/services/tokenService';

// 存储 Token
tokenService.setTokenInfo({
  access_token: token,
  refresh_token: refreshToken,
  expires_in: expiresIn,
  token_type: 'Bearer',
});

// 读取 Token
const token = tokenService.getAccessToken();

// 清除 Token
tokenService.clearTokens();
```

**注意事项**:
- localStorage 在同源下全局共享，不同前端应用必须使用命名空间键隔离
- 不要在 URL 参数中传递 Token
- 不要在 console.log 中输出 Token
- `cached_user_info` 只用于登录态 bootstrap 阶段的短暂用户资料复用，缓存内容必须与当前 access token 的 `userId + tenantId + token 会话` 绑定，并在消费后立即清除；不能把它当作跨账号、跨租户的长期资料缓存

### 2. Token 验证

```typescript
import { apiGet } from '@radish/http';
import { tokenService } from '@/services/tokenService';
import { redirectToLogin } from '@/services/auth';

// 验证 Token 是否有效
export async function validateToken(): Promise<boolean> {
  const response = await apiGet('/api/v1/User/GetProfile', {
    withAuth: true,
  });

  if (!response.ok) {
    if (response.statusCode === 401) {
      // Token 无效，清除并重新登录
      tokenService.clearTokens();
      redirectToLogin();
    }
    return false;
  }

  return true;
}
```

### 3. Token 过期处理

在 HTTP 客户端配置中统一处理 401 错误：

```typescript
import { configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';
import { redirectToLogin } from '@/services/auth';

configureApiClient({
  baseUrl: getApiBaseUrl(),
  onError: (error) => {
    // 检查是否是 401 错误
    if (error.message.includes('401')) {
      // 清除 Token
      tokenService.clearTokens();
      // 跳转到登录页
      redirectToLogin();
    }
  },
});
```

### 4. 多标签页同步

使用 `storage` 事件监听其他标签页的登出操作：

```typescript
import { hasAccessToken } from '@/services/auth';

// 监听 storage 事件
window.addEventListener('storage', (event) => {
  if (event.key === 'radish_client_access_token') {
    if (!event.newValue && hasAccessToken()) {
      // 其他标签页已登出，刷新当前页面
      window.location.reload();
    }
  }
});
```

## OIDC 回调处理

### 回调路由

Web 侧 OIDC 回调由 `Frontend/radish.client/src/auth/OidcCallbackPage.tsx` 独立承载，不再依赖早期 `App.tsx` 或 `?demo` 页面。

当前实现要点：

- 回调地址固定为 `${window.location.origin}/oidc/callback`
- 回调页调用 `@radish/http` 的 `redeemOidcAuthorizationCode()` 完成授权码换 Token
- Token 写入统一的 `tokenService`
- 写入 Token 后调用 `hydrateAuthUser()` 预热当前用户资料
- 成功后优先消费一次性认证返回路径；若不存在有效返回路径，则执行 `window.location.replace('/')` 回到根入口
- 普通浏览器根入口 `/` 会进入 `/discover` 纯 Web 公开分发页；Tauri 当前仍保留进入 `/desktop`
- 当前认证返回路径使用严格白名单：`/desktop` 用于工作台上下文，`/circle` 用于我的圈子登录回流，`/notifications`、`/me`、`/messages` 和 `/pet` 用于纯 Web 登录态私域复访，`/forum/post/:postId?intent=comment|quickReply` 用于公开详情轻参与回流

回调页只负责协议闭环和用户资料预热，不承载登录测试 UI、天气示例或其他 demo 行为。

### 当前实现补充

- OIDC 回调完成并写入 token 后，`radish.client` 当前会先执行一次当前用户资料预热，再恢复一次性认证返回路径或返回根入口；普通浏览器根入口进入 `/discover`，Tauri 当前进入 `/desktop`，以优先同步头像、昵称、等级经验等高频信息。
- 这一步的目标不是扩大缓存生命周期，而是缩短“刚登录成功但桌面头像 / 等级信息还要再等几秒”的体感空窗。
- `cached_user_info` 仍只允许作为与当前 `access token` 身份强绑定的一次性引导缓存；资料预热完成后应继续按既有规则消费并清理，不能回退为跨账号、跨租户的长期缓存。

### 入口分流

`Frontend/radish.client/src/main.tsx` 会先识别 `/oidc/callback` 并加载独立回调页；公开内容路径进入 `PublicEntry`；`/workbench` 进入正式 Web 功能地图；`/desktop` 进入 WebOS 历史入口。历史 `/?demo` 不再有特殊分流。

## 环境配置

### 运行时优先级

部署态前端认证地址优先级如下：

1. `/runtime-config.js`
2. `VITE_AUTH_BASE_URL` / `VITE_API_BASE_URL`
3. 默认回退值 `https://localhost:5000`

因此，测试部署与生产部署通常不需要把前端写死到独立的 Auth/API 域名；大多数情况下，统一使用 Gateway 公开入口即可。

### 环境变量

```env
# .env.development
VITE_AUTH_BASE_URL=https://localhost:5000
VITE_API_BASE_URL=https://localhost:5000

# Deploy/.env for test
RADISH_PUBLIC_URL=https://test.radish.example.com
RADISH_IMAGE_TAG=v26.3.2-test
# 可选：若不填，frontend 容器会回退到 RADISH_PUBLIC_URL
# VITE_AUTH_BASE_URL=https://test.radish.example.com
# VITE_API_BASE_URL=https://test.radish.example.com

# Deploy/.env for release
RADISH_PUBLIC_URL=https://radish.example.com
RADISH_IMAGE_TAG=v26.3.2-release
# 可选：若不填，frontend 容器会回退到 RADISH_PUBLIC_URL
# VITE_AUTH_BASE_URL=https://radish.example.com
# VITE_API_BASE_URL=https://radish.example.com
```

### 配置读取

```typescript
// src/config/env.ts
const defaultPublicUrl = 'https://localhost:5000';

export function getAuthBaseUrl(): string {
  const authBaseUrl =
    window.__RADISH_RUNTIME_CONFIG__?.authBaseUrl ||
    import.meta.env.VITE_AUTH_BASE_URL ||
    defaultPublicUrl;

  if (window.location.port === '5000') {
    return window.location.origin;
  }

  return authBaseUrl.replace(/\/$/, '');
}

export function getApiBaseUrl(): string {
  const apiBaseUrl =
    window.__RADISH_RUNTIME_CONFIG__?.apiBaseUrl ||
    import.meta.env.VITE_API_BASE_URL ||
    defaultPublicUrl;

  if (window.location.port === '5000') {
    return window.location.origin;
  }

  return apiBaseUrl.replace(/\/$/, '');
}
```

补充约束：

- 当前推荐让 `VITE_AUTH_BASE_URL` 与 `VITE_API_BASE_URL` 在部署态都指向同一个 Gateway 公开入口。
- 如果未来确实拆成独立 `auth.example.com` / `api.example.com`，必须同时复核 Gateway 转发、OIDC Issuer、客户端回调地址与 CORS，不能只改前端环境变量。

## 常见问题

### Q: 为什么要统一认证服务？

A: 统一认证服务的好处：
- **避免代码重复**：登录/登出逻辑只需维护一处
- **统一行为**：所有组件使用相同的认证流程
- **易于维护**：修改认证逻辑只需更新一个文件
- **类型安全**：TypeScript 类型定义确保正确使用

### Q: 如何处理 Token 过期？

A: 有两种方式：
1. **被动处理**：API 返回 401 时，清除 Token 并跳转登录
2. **主动刷新**：定期检查 Token 过期时间，提前刷新

推荐使用被动处理，简单可靠。

### Q: 为什么要使用 Refresh Token？

A: 当前实现已启用 Refresh Token 自动续期，原因：
- 减少短 Token 场景下的频繁重登
- 降低页面长驻时的会话中断概率
- 与 `@radish/http` 的 401 刷新重试机制配合

### Q: 多个前端应用如何避免登录态互相污染？

A: 不同前端应用必须同时做两件事：
- 使用不同的 `client_id`（如 `radish-client`、`radish-console`）
- 使用不同的本地存储键命名空间（如 `radish_client_*`、`radish_console_*`）

如果直接共用 `access_token/refresh_token`，会出现 A 应用登录覆盖 B 应用刷新令牌的问题。

### Q: 如何实现"记住我"功能？

A: OIDC 服务器端实现：
- 在登录页面添加"记住我"选项
- 服务器根据选项设置不同的 Session 过期时间
- 前端无需特殊处理

### Q: 如何处理并发请求的认证？

A: HTTP 客户端自动处理：
- 每个请求独立添加 Token
- 如果 Token 过期，所有请求都会返回 401
- 错误拦截器统一处理，只跳转一次登录页

## 安全建议

### 1. HTTPS 入口约束

生产环境必须通过外部 HTTPS 入口访问 Gateway，例如 `https://radish.example.com`。

补充说明：

- 测试部署也建议直接使用 `https://IP:port`，即使浏览器对自签名证书弹出告警也是预期行为。
- 不要在前端代码里硬编码把 `http:` 替换成 `https:`；协议、域名与端口应由 `RADISH_PUBLIC_URL` 和运行时配置统一决定。
- 如果生产环境仍以 `http://` 访问公开入口，通常意味着外部反代或域名配置有误，而不是前端认证逻辑问题。

### 2. Token 不要暴露

```typescript
// ❌ 错误：在 URL 中传递 Token
window.location.href = `/profile?token=${token}`;

// ✅ 正确：使用 HTTP Header
const response = await apiGet('/api/v1/User/GetProfile', {
  withAuth: true,
});
```

### 3. 清理敏感数据

登出时清理所有敏感数据：
```typescript
import { tokenService } from '@/services/tokenService';

export function logout(): void {
  // 清理 Token 与缓存
  tokenService.clearTokens();

  // 清理其他敏感数据
  sessionStorage.clear();

  // 跳转到登出端点
  // ...
}
```

### 4. XSS 防护

不要使用 `dangerouslySetInnerHTML` 渲染用户输入：
```typescript
// ❌ 错误：可能导致 XSS
<div dangerouslySetInnerHTML={{ __html: userInput }} />

// ✅ 正确：React 自动转义
<div>{userInput}</div>
```

## 相关文档

- [@radish/http 包文档](../frontend/http-client.md)
- [前端设计](../frontend/design.md)
- [Gateway 配置指南](./gateway.md)
- [OIDC 规范](https://openid.net/specs/openid-connect-core-1_0.html)
