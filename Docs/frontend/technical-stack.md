# 前端技术栈细节

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

## 7. 技术栈

| 层级 | 技术选型 |
|------|---------|
| 框架 | React 19 + TypeScript |
| 构建 | Vite (Rolldown) |
| 路由 | 轻量路径解析 + History API；公开内容壳层由 route state 辅助能力承接 |
| 状态管理 | Zustand (窗口/Dock 与公开壳层局部状态) + TanStack Query (数据) |
| UI 框架 | TailwindCSS + 自研组件 |
| 窗口拖拽 | react-rnd |
| 动效 | Framer Motion |
| 后台组件 | Ant Design (仅 admin 应用使用) |
| 表单 | React Hook Form + Zod |
| 国际化 | react-i18next |
| HTTP 客户端 | @radish/http (统一 API 客户端) |
| 认证 | OIDC (OpenIddict) + 统一认证服务 |

#### 7.1 HTTP 客户端 (@radish/http)

**统一的 API 请求封装**，从 `@radish/ui` 中独立出来，专注于 HTTP 通信。

**核心特性**：
- 统一配置管理（baseUrl、timeout、token、language、message translator）
- 类型安全的 TypeScript 定义
- 自动添加 Bearer Token 认证
- 自动发送 `Accept-Language` 并优先使用本地 `MessageKey` 翻译
- 保留 HTTP status、`Code / MessageKey / MessageInfo / TraceId` 的结构化错误
- 请求/响应/错误拦截器
- 超时控制和错误处理

**使用示例**：

```typescript
import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
} from '@radish/http';
import i18n from './i18n';

// 配置 API 客户端
configureApiClient({
  baseUrl: 'https://localhost:5000',
  timeout: 30000,
  getToken: () => localStorage.getItem('access_token'),
  getLanguage: () => i18n.resolvedLanguage ?? i18n.language,
  translateMessage: (key) => i18n.exists(key) ? i18n.t(key) : undefined,
});

// 发送请求
const response = await apiGet<Product[]>('/api/v1/Shop/GetProducts', {
  withAuth: true,
});

if (!response.ok || !response.data) {
  throw createApiResponseError(response, i18n.t('shop.loadFailed'));
}
```

业务控制流只读取真实 HTTP status、稳定 `Code` 或明确数据状态，不匹配 `response.message` 的中英文文本。client / Console 日志统一使用各自 `log` 工具。

**详细文档**：参见 [@radish/http 包文档](./http-client.md)

#### 7.2 认证服务

**统一的 OIDC 认证管理**，位于 `Frontend/radish.client/src/services/auth.ts`。

**核心方法**：
- `redirectToLogin()` - 跳转到 OIDC 登录页面
- `logout()` - 执行 OIDC 登出，清除本地 Token
- `hasAccessToken()` - 检查是否有有效的 access_token

**认证流程**：

```
用户访问 → 检查 Token → 未登录 → redirectToLogin()
                ↓
            已登录 → 保持当前入口（纯 Web 或 /desktop）
                ↓
        API 请求自动添加 Token (withAuth: true)
                ↓
        Token 过期 → 401 错误 → redirectToLogin()
```

OIDC 回调页由 `Frontend/radish.client/src/auth/OidcCallbackPage.tsx` 承接，成功换取 Token 并恢复用户状态后回到根入口。普通浏览器根路径 `/` 当前进入 `/discover` 公开分发页；Tauri 或显式工作台场景才进入 `/desktop`。

**使用示例**：

```typescript
import { redirectToLogin, logout, hasAccessToken } from '@/services/auth';

// 检查登录状态
if (!hasAccessToken()) {
  redirectToLogin();
}

// 登出
const handleLogout = () => {
  logout();
};
```

**WebSocket 认证集成**：

```typescript
import { hasAccessToken } from '@/services/auth';

// 仅在已登录时建立 WebSocket 连接
if (hasAccessToken()) {
  const token = localStorage.getItem('access_token');
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notification', {
      accessTokenFactory: () => token || '',
    })
    .build();
}
```

**详细文档**：参见 [认证服务统一指南](../guide/authentication-service.md)

#### 7.3 附件与媒体协议

前端当前已经统一采用“`attachmentId` 为真值、URL 运行时解析”的媒体口径：

- 帖子、评论、Wiki 正文中的图片 / 文档链接统一保存为 `attachment://{id}`，而不是完整 URL。
- `MarkdownEditor` 与 `RichTextMarkdownEditor` 在上传成功后统一调用 `buildAttachmentMarkdownUrl()` 写回正文。
- `MarkdownRenderer`、论坛富文本工作区、聊天室图片预览等展示链路统一通过 `buildAttachmentAssetUrl()` / `resolveConfiguredMediaUrl()` 解析当前环境下的真实访问地址。
- `AttachmentVo.voUrl` / `voThumbnailUrl`、`StickerVo.voImageUrl` / `voThumbnailUrl`、`ProductVo.voIcon` / `voCoverImage`、`ChannelMessageVo.voImageUrl` / `voImageThumbnailUrl` 都属于运行时派生展示字段，不是前端回写数据库的依据。

推荐模式如下：

```typescript
import {
  buildAttachmentAssetUrl,
  buildAttachmentMarkdownUrl,
  parseAttachmentMarkdownUrl,
} from '@radish/ui';

const markdownUrl = buildAttachmentMarkdownUrl(result.attachmentId, {
  displayVariant: 'thumbnail',
  scalePercent: 60,
});

const parsed = parseAttachmentMarkdownUrl(markdownUrl);
const previewUrl = parsed
  ? buildAttachmentAssetUrl(parsed.attachmentId, parsed.displayVariant)
  : null;
```

这套约束的目标只有一个：前端可以跟随当前 `baseUrl`、Gateway、反向代理与部署域名自然切换，而不会把旧域名硬编码进业务数据。

##
