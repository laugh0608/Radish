# 前端应用集成架构决策

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

### 10.4 应用集成架构决策

##### 10.4.1 三种应用类型的选择标准

Radish WebOS 支持三种应用集成方式,选择标准如下:

| 应用类型 | 判断标准 | 适用场景 | 示例 |
|---------|---------|---------|------|
| **内置应用 (Built-in)** | - 无需独立部署<br>- 无复杂路由<br>- 可共享认证状态 | - 简单功能模块<br>- 用户高频使用<br>- 需要与桌面深度集成 | Forum(论坛)<br>Chat(聊天)<br>Settings(设置) |
| **窗口应用 (Window)** | - 需要统一权限与状态<br>- 支持固定文档与在线文档混合展示<br>- 支持编辑/导入/导出 | - 文档<br>- 论坛<br>- 设置 | Document(文档)<br>Forum(论坛) |
| **外部应用 (External)** | - 完整的 SPA<br>- 有 OIDC 认证流程<br>- 复杂路由系统<br>- 需要独立访问 | - 管理后台<br>- 复杂业务系统<br>- 需要独立部署的模块 | Console(管理控制台)<br>Shop(商城) |

##### 10.4.2 为什么 Console 不能嵌入 WebOS?

**技术限制**:

1. **OIDC 认证流程冲突**
   ```
   OIDC 标准流程:
   1. 用户点击登录 → 跳转到 Auth Server
   2. Auth Server 认证成功 → 重定向到 redirect_uri
   3. 应用处理回调 → 获取 token

   在 iframe 中的问题:
   - redirect_uri 无法指向 iframe 内部的 URL
   - 认证服务器无法将用户重定向到 iframe
   - token 存储在 iframe 的 localStorage,父页面无法访问
   ```

2. **路由系统冲突**
   ```
   浏览器地址栏: https://localhost:5000/ (WebOS 的地址)
   Console 内部路由: /dashboard, /users, /settings

   问题:
   - Console 的路由无法反映在地址栏中
   - 用户刷新页面会回到 WebOS 首页
   - 无法分享 Console 内部页面的链接
   - Console 使用的 React Router 无法正常工作
   ```

3. **Gateway 路径剥离导致的混乱**
   ```
   Gateway 配置: /console/dashboard → 剥离前缀 → /dashboard
   Console 认为: 自己在根路径 /
   实际位置: 在 /console/ 下
   iframe 中: 地址栏显示 https://localhost:5000/ (父页面)

   结果: Console 的所有绝对路径引用都会指向错误位置
   ```

4. **用户体验问题**
   ```
   外层: WebOS 窗口系统(可拖动、最小化)
   内层: Console 自己的 UI(导航栏、侧边栏)

   用户困惑:
   - 双层标题栏(WebOS 窗口标题 + Console 标题)
   - 双层滚动条(窗口滚动 + 内容滚动)
   - 操作冲突(窗口拖动 vs 内容交互)
   ```

**架构理由**:

1. **关注点分离 (Separation of Concerns)**
   - Client: 面向 C 端用户,强调易用性和娱乐性
   - Console: 面向管理员,强调功能性和数据安全

2. **权限隔离 (Security Isolation)**
   - 普通用户不应加载管理功能的代码(减少攻击面)
   - 管理功能需要更严格的审计和安全检查

3. **部署灵活性 (Deployment Flexibility)**
   - Client 可部署到公网 CDN(高速访问)
   - Console 可部署到内网(安全隔离)
   - 各自独立扩容和维护

4. **开发独立性 (Development Independence)**
   - Client 团队和 Console 团队可并行开发
   - 代码冲突减少,发版互不影响
   - 可采用不同的技术栈和 UI 库

5. **代码体积控制 (Bundle Size Optimization)**
   - Client 打包体积应尽可能小(普通用户)
   - Console 可以稍大(管理员使用频率低)
   - 避免普通用户下载用不到的管理功能代码

##### 10.4.3 应用集成最佳实践

**添加新应用时的决策流程**:

```typescript
// 决策树
if (应用需要 OIDC 认证 && 有复杂路由) {
  使用 type: 'external'
  在新标签页打开
} else if (应用是展示型 && 无复杂交互) {
  使用 type: 'iframe'
  嵌入 WebOS 窗口
} else {
  使用 type: 'window'
  作为内置应用开发
}
```

**实现示例**:

```typescript
// Frontend/radish.client/src/desktop/AppRegistry.tsx

// ✅ 内置应用 - 论坛
{
  id: 'forum',
  name: '论坛',
  icon: 'mdi:forum',
  component: ForumApp, // React 组件
  type: 'window',
  defaultSize: { width: 1200, height: 800 }
}

// ✅ 内置窗口应用 - 文档
{
  id: 'document',
  name: '文档',
  icon: 'mdi:book-open-page-variant',
  component: WikiApp,
  type: 'window',
  defaultSize: { width: 1200, height: 800 }
}

// ✅ 外部应用 - 管理控制台
{
  id: 'console',
  name: '控制台',
  icon: 'mdi:console',
  component: () => null, // external 不需要组件
  type: 'external',
  externalUrl: typeof window !== 'undefined' &&
    window.location.origin.includes('localhost:5000')
    ? '/console/' // 通过 Gateway
    : 'http://localhost:3100' // 直接访问
}
```

**共享组件策略**:

```
@radish/ui (共享 UI 组件库)
    ↓
┌───┴────┬─────────┬─────────┐
│        │         │         │
Client  Console   Shop    Document
```

- 基础组件(Button, Input, Modal)放在 `@radish/ui`
- 业务特定组件各自维护
- 通过 npm workspaces 实现热更新

##
