# 认证服务统一指南

## 概述

Radish 项目采用 OIDC (OpenID Connect) 标准进行身份认证，前端通过统一的认证服务 (`auth.ts`) 管理登录、登出和 Token 验证逻辑。本指南介绍如何使用认证服务，以及如何在前端应用中集成认证功能。

## 认证架构

### 认证流程

```
┌─────────────┐      ┌──────────────┐      ┌─────────────┐
│             │      │              │      │             │
│  前端应用   │─────▶│  Auth 服务   │─────▶│  API 服务   │
│  (Client)   │      │  (OIDC)      │      │  (Backend)  │
│             │◀─────│              │◀─────│             │
└─────────────┘      └──────────────┘      └─────────────┘
     │                     │                      │
     │  1. 跳转登录        │                      │
     │────────────────────▶│                      │
     │                     │                      │
     │  2. 用户认证        │                      │
     │                     │                      │
     │  3. 返回 code       │                      │
     │◀────────────────────│                      │
     │                     │                      │
     │  4. 交换 token      │                      │
     │────────────────────▶│                      │
     │                     │                      │
     │  5. 返回 token      │                      │
     │◀────────────────────│                      │
     │                     │                      │
     │  6. 携带 token 请求 │                      │
     │────────────────────────────────────────────▶│
     │                     │                      │
     │  7. 验证 token 并返回数据                  │
     │◀────────────────────────────────────────────│
```

### 核心组件

- **Radish.Auth**: OIDC 认证服务器 (基于 OpenIddict)
- **Radish.Gateway**: API 网关，验证 JWT Token
- **radish.client/src/services/auth.ts**: 前端认证服务
- **@radish/http**: HTTP 客户端，自动添加 Token

## 认证服务 API

### 文件位置

```
radish.client/src/services/auth.ts
```

### 导出方法

#### redirectToLogin()

**用途**: 跳转到 OIDC 登录页面

**方法签名**:
```typescript
function redirectToLogin(): void
```

**使用场景**:
- 用户点击登录按钮
- 检测到未登录状态
- Token 过期需要重新登录

**示例**:
```typescript
import { redirectToLogin } from '@/services/auth';

// 登录按钮点击事件
const handleLogin = () => {
  redirectToLogin();
};
```

**工作流程**:
1. 构建 OIDC 授权 URL (`/connect/authorize`)
2. 设置参数:
   - `client_id`: radish-client
   - `response_type`: code
   - `redirect_uri`: {origin}/oidc/callback
   - `scope`: radish-api
   - `culture`: 当前语言设置
3. 跳转到认证服务器

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
   - `access_token`
   - `refresh_token`
   - `cached_user_info`
2. 构建 OIDC 登出 URL (`/connect/endsession`)
3. 设置参数:
   - `post_logout_redirect_uri`: {origin}
   - `client_id`: radish-client
   - `culture`: 当前语言设置
4. 跳转到认证服务器

#### hasAccessToken()

**用途**: 检查是否有有效的 access_token

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
import { hasAccessToken } from '@/services/auth';

let connection: HubConnection | null = null;

export async function connectToNotificationHub() {
  // 检查是否已登录
  if (!hasAccessToken()) {
    console.log('未登录，跳过 WebSocket 连接');
    return;
  }

  // 获取 Token
  const token = localStorage.getItem('access_token');

  // 创建连接
  connection = new HubConnectionBuilder()
    .withUrl('https://localhost:5000/hubs/notification', {
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

```typescript
import { redirectToLogin } from '@/services/auth';

// 解析 JWT Token
function parseJwt(token: string) {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch {
    return null;
  }
}

// 检查 Token 是否即将过期
function isTokenExpiringSoon(token: string): boolean {
  const payload = parseJwt(token);
  if (!payload || !payload.exp) return true;

  const expirationTime = payload.exp * 1000; // 转换为毫秒
  const currentTime = Date.now();
  const timeUntilExpiration = expirationTime - currentTime;

  // 如果 Token 在 5 分钟内过期，返回 true
  return timeUntilExpiration < 5 * 60 * 1000;
}

// 定期检查 Token 状态
export function startTokenRefreshCheck() {
  setInterval(() => {
    const token = localStorage.getItem('access_token');
    if (!token) return;

    if (isTokenExpiringSoon(token)) {
      console.log('Token 即将过期，重新登录');
      redirectToLogin();
    }
  }, 60 * 1000); // 每分钟检查一次
}
```

## Token 管理最佳实践

### 1. Token 存储

**推荐方式**: localStorage

```typescript
// 存储 Token
localStorage.setItem('access_token', token);
localStorage.setItem('refresh_token', refreshToken);

// 读取 Token
const token = localStorage.getItem('access_token');

// 清除 Token
localStorage.removeItem('access_token');
localStorage.removeItem('refresh_token');
```

**注意事项**:
- localStorage 在同源下全局共享
- 不要在 URL 参数中传递 Token
- 不要在 console.log 中输出 Token

### 2. Token 验证

```typescript
import { apiGet } from '@radish/http';
import { redirectToLogin } from '@/services/auth';

// 验证 Token 是否有效
export async function validateToken(): Promise<boolean> {
  const response = await apiGet('/api/v1/User/GetProfile', {
    withAuth: true,
  });

  if (!response.ok) {
    if (response.statusCode === 401) {
      // Token 无效，清除并重新登录
      localStorage.removeItem('access_token');
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
import { redirectToLogin } from '@/services/auth';

configureApiClient({
  baseUrl: 'https://localhost:5000',
  onError: (error) => {
    // 检查是否是 401 错误
    if (error.message.includes('401')) {
      // 清除 Token
      localStorage.removeItem('access_token');
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
  if (event.key === 'access_token') {
    if (!event.newValue && hasAccessToken()) {
      // 其他标签页已登出，刷新当前页面
      window.location.reload();
    }
  }
});
```

## OIDC 回调处理

### 回调路由

```typescript
// src/routes/OidcCallback.tsx
import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

export const OidcCallback: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    const code = searchParams.get('code');
    const error = searchParams.get('error');

    if (error) {
      console.error('OIDC 认证失败:', error);
      navigate('/');
      return;
    }

    if (code) {
      // 交换 code 获取 token
      exchangeCodeForToken(code);
    }
  }, [searchParams, navigate]);

  const exchangeCodeForToken = async (code: string) => {
    try {
      const response = await fetch('https://localhost:5200/connect/token', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: new URLSearchParams({
          grant_type: 'authorization_code',
          code: code,
          redirect_uri: `${window.location.origin}/oidc/callback`,
          client_id: 'radish-client',
        }),
      });

      const data = await response.json();

      if (data.access_token) {
        // 存储 Token
        localStorage.setItem('access_token', data.access_token);
        if (data.refresh_token) {
          localStorage.setItem('refresh_token', data.refresh_token);
        }

        // 跳转到首页
        navigate('/');
      }
    } catch (error) {
      console.error('Token 交换失败:', error);
      navigate('/');
    }
  };

  return <div>正在登录...</div>;
};
```

### 路由配置

```typescript
// src/App.tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { OidcCallback } from './routes/OidcCallback';

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/oidc/callback" element={<OidcCallback />} />
        {/* 其他路由 */}
      </Routes>
    </BrowserRouter>
  );
};
```

## 环境配置

### 环境变量

```env
# .env.development
VITE_AUTH_BASE_URL=http://localhost:5200
VITE_API_BASE_URL=https://localhost:5000

# .env.production
VITE_AUTH_BASE_URL=https://auth.radish.com
VITE_API_BASE_URL=https://api.radish.com
```

### 配置读取

```typescript
// src/config/env.ts
export function getAuthBaseUrl(): string {
  return import.meta.env.VITE_AUTH_BASE_URL || 'http://localhost:5200';
}

export function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';
}
```

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

### Q: 为什么不使用 Refresh Token？

A: 当前实现中，Refresh Token 存储在 localStorage 但未使用。原因：
- OIDC 授权码流程已经足够安全
- 简化前端逻辑
- Token 过期后重新登录体验可接受

如果需要实现 Refresh Token，可以在 Token 即将过期时调用 `/connect/token` 端点。

### Q: 如何在多个前端应用间共享认证？

A: 使用相同的 `client_id` 和 localStorage 存储：
- `radish.client` 和 `radish.console` 使用不同的 `client_id`
- 如果需要共享登录状态，可以使用相同的 `client_id`
- 或者使用 Cookie 存储 Token（需要配置 SameSite）

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

### 1. HTTPS Only

生产环境必须使用 HTTPS：
```typescript
if (import.meta.env.PROD && window.location.protocol !== 'https:') {
  window.location.href = window.location.href.replace('http:', 'https:');
}
```

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
export function logout(): void {
  // 清理 Token
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');

  // 清理缓存的用户信息
  localStorage.removeItem('cached_user_info');

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
