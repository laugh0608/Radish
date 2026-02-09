# Console 实施计划

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 5 章

## 5. 实施计划

### 5.1 开发阶段划分

Console 管理后台采用**分阶段迭代开发**的方式，每个阶段聚焦核心功能，确保快速交付和持续优化。

---

### 5.2 Phase 1: 基础框架 (已完成)

**目标**：搭建基础框架，实现 OIDC 认证和应用管理核心功能。

#### 5.2.1 已完成功能

- ✅ 项目初始化 (React 19 + Vite + TypeScript)
- ✅ 基础布局组件 (AdminLayout)
  - 侧边栏菜单
  - 顶部导航栏
  - 用户下拉菜单
  - 响应式布局
- ✅ OIDC 认证集成
  - Authorization Code Flow
  - Token 管理 (localStorage)
  - 登录页面
  - 回调处理页面
  - Single Sign-Out
- ✅ 应用管理 (OIDC 客户端)
  - 客户端列表展示
  - 新增客户端
  - 编辑客户端
  - 删除客户端
  - 重置客户端密钥
- ✅ Hangfire 集成
  - iframe 嵌入 Hangfire Dashboard
- ✅ 基础 API 客户端封装
- ✅ React Router 集成 (2026-01-16)
  - 基于 react-router-dom v7
  - 支持嵌套路由
  - 路由守卫
- ✅ 主题配置 (2026-01-17)
  - Ant Design 主题定制
  - ThemeProvider 统一管理
  - 萝卜橙主题色 (#FF6B35)
  - 支持亮色/暗色主题
- ✅ 用户信息管理 (2026-01-17)
  - UserContext 状态管理
  - 显示真实用户名和头像
  - 用户信息自动加载
- ✅ 架构安全修复 (2026-01-17)
  - 修复后端Controller架构违规问题
  - 修复前端枚举不一致问题
  - 修复前端构建错误
  - 提升类型安全性和开发体验

#### 5.2.2 技术债务

- ⚠️ 缺少统一的错误处理机制
- ⚠️ 缺少 Token 自动刷新机制
- ⚠️ 缺少权限验证 Hook
- ⚠️ 缺少全局 Loading 组件
- ✅ 错误边界 (Error Boundary) - 已有基础实现
- ✅ 前端构建问题 - 已修复 (2026-01-17)

---

### 5.3 Phase 1.5: 基础架构完善 (进行中)

**目标**：完善基础架构，提升应用稳定性和用户体验。

**预计工期**：1 周

#### 5.3.1 高优先级（基础架构）

**错误边界 (Error Boundary)** (1 天)：
- [x] 创建 ErrorBoundary 组件
- [x] 捕获组件渲染错误
- [x] 提供友好的错误提示页面
- [ ] 错误信息上报（可选）

**加载状态管理** (1 天)：
- [ ] 创建全局 Loading 组件
- [ ] 页面切换时的加载指示器
- [ ] 骨架屏组件 (Skeleton)
- [ ] 统一的加载状态管理

**权限控制** (2 天)：
- [ ] 创建 usePermission Hook
- [ ] 基于角色的路由守卫
- [ ] 菜单项根据权限动态显示/隐藏
- [ ] 按钮级别的权限控制组件

**Token 刷新机制** (1 天)：
- [ ] Token 过期检测
- [ ] 自动刷新 Token
- [ ] 刷新失败自动跳转登录页
- [ ] 并发请求的 Token 刷新处理

**环境配置** (0.5 天)：
- [ ] 开发/生产环境配置分离
- [ ] 环境变量管理 (`.env.development`, `.env.production`)
- [ ] API 基础 URL 配置
- [ ] 功能开关配置

#### 5.3.2 中优先级（用户体验）

**面包屑导航** (0.5 天)：
- [ ] 创建 Breadcrumb 组件
- [ ] 根据路由自动生成面包屑
- [ ] 支持自定义面包屑标题
- [ ] 支持快速返回上级页面

**页面标题管理** (0.5 天)：
- [ ] 创建 useDocumentTitle Hook
- [ ] 根据路由动态设置页面标题
- [ ] 格式：`页面名称 - Radish Console`

**404 页面** (0.5 天)：
- [ ] 创建 NotFound 组件
- [ ] 友好的 404 错误页面
- [ ] 提供返回首页的链接
- [ ] 提供搜索功能入口

**全局搜索** (1 天)：
- [ ] 顶部搜索框组件
- [ ] 支持搜索菜单
- [ ] 支持搜索用户（可选）
- [ ] 快捷键支持 (Ctrl+K)

**通知中心** (1 天)：
- [ ] 集成 SignalR 实时通知
- [ ] 显示系统消息
- [ ] 显示操作结果通知
- [ ] 通知历史记录

#### 5.3.3 验收标准

- [ ] 组件错误不会导致整个应用崩溃
- [ ] 页面切换有明确的加载指示
- [ ] 用户只能看到有权限的菜单和功能
- [ ] Token 过期自动刷新，无需手动登录
- [ ] 不同环境使用不同的配置
- [ ] 面包屑导航正确显示当前位置
- [ ] 页面标题正确显示
- [ ] 访问不存在的页面显示 404 页面

---

### 5.4 Phase 2: 用户与角色管理 (规划中)

**目标**：实现用户管理和角色权限管理核心功能。

**预计工期**：3-4 周

#### 5.3.1 后端开发

**用户管理 API** (1 周)：
- [ ] `GET /api/v1/User/GetList` - 用户列表 (分页、搜索、筛选)
- [ ] `GET /api/v1/User/GetById/{id}` - 用户详情
- [ ] `PUT /api/v1/User/UpdateStatus` - 更新用户状态
- [ ] `POST /api/v1/User/ResetPassword` - 重置密码
- [ ] `POST /api/v1/User/AssignRoles` - 分配角色
- [ ] `POST /api/v1/User/ForceLogout` - 强制下线

**角色管理 API** (1 周)：
- [ ] `GET /api/v1/Role/GetList` - 角色列表
- [ ] `GET /api/v1/Role/GetById/{id}` - 角色详情
- [ ] `POST /api/v1/Role/Create` - 创建角色
- [ ] `PUT /api/v1/Role/Update` - 更新角色
- [ ] `DELETE /api/v1/Role/Delete/{id}` - 删除角色
- [ ] `GET /api/v1/Permission/GetAll` - 获取所有权限

**权限验证中间件** (3 天)：
- [ ] 实现权限验证 Attribute
- [ ] Gateway 集成权限验证
- [ ] 权限缓存机制

#### 5.3.2 前端开发

**用户管理页面** (1 周)：
- [ ] 用户列表页面
  - 表格展示
  - 搜索和筛选
  - 分页
- [ ] 用户详情页面
  - 基本信息展示
  - 统计信息展示
  - 操作记录
- [ ] 用户状态管理
  - 启用/禁用
  - 锁定/解锁
  - 重置密码
  - 强制下线
- [ ] 角色分配对话框

**角色管理页面** (1 周)：
- [ ] 角色列表页面
- [ ] 新增/编辑角色对话框
  - 基本信息表单
  - 权限选择 (树形或分组)
- [ ] 删除角色确认
- [ ] 角色详情页面

**权限管理** (3 天)：
- [ ] 权限列表页面
- [ ] 权限分组展示
- [ ] 权限说明文档

#### 5.3.3 验收标准

- [ ] 管理员可以查看、搜索、筛选用户列表
- [ ] 管理员可以查看用户详情和统计信息
- [ ] 管理员可以启用/禁用/锁定用户
- [ ] 管理员可以为用户分配角色
- [ ] 管理员可以创建、编辑、删除自定义角色
- [ ] 管理员可以为角色配置权限
- [ ] 系统角色不可删除
- [ ] 所有操作记录审计日志

---

### 5.5 Phase 3: 系统监控与审计 (规划中)

**目标**：实现系统监控、日志查询和操作审计功能。

**预计工期**：2-3 周

#### 5.5.1 后端开发

**系统监控 API** (1 周)：
- [ ] `GET /api/v1/Monitor/ServiceStatus` - 服务状态
- [ ] `GET /api/v1/Monitor/Performance` - 性能指标
- [ ] `GET /api/v1/Monitor/ResourceUsage` - 资源使用
- [ ] `GET /api/v1/Log/Query` - 日志查询
- [ ] `GET /api/v1/Alert/GetList` - 告警列表
- [ ] `POST /api/v1/Alert/MarkAsHandled` - 标记告警已处理

**审计日志** (1 周)：
- [ ] 实现审计日志记录中间件
- [ ] `GET /api/v1/Audit/GetList` - 审计日志列表
- [ ] `GET /api/v1/Audit/GetById/{id}` - 审计日志详情
- [ ] `POST /api/v1/Audit/Export` - 导出审计日志

#### 5.5.2 前端开发

**仪表盘优化** (1 周)：
- [ ] 数据统计卡片
- [ ] 图表展示 (ECharts)
- [ ] 实时数据刷新
- [ ] 快速操作入口

**系统监控页面** (1 周)：
- [ ] 服务状态监控
- [ ] 性能指标图表
- [ ] 资源使用图表
- [ ] 日志查询界面
- [ ] 告警管理

**审计日志页面** (3 天)：
- [ ] 审计日志列表
- [ ] 日志详情对话框
- [ ] 日志导出功能

#### 5.5.3 验收标准

- [ ] 仪表盘展示关键指标和图表
- [ ] 管理员可以查看服务状态和性能指标
- [ ] 管理员可以查询系统日志
- [ ] 管理员可以查看和处理告警
- [ ] 所有管理员操作记录审计日志
- [ ] 管理员可以查询和导出审计日志

---

### 5.6 Phase 4: 功能完善与优化 (规划中)

**目标**：完善功能细节，优化用户体验，提升系统性能。

**预计工期**：2 周

#### 5.6.1 功能完善

- [ ] React Router 集成
- [ ] Token 自动刷新机制
- [ ] 统一错误处理
- [ ] 权限验证 Hook (`usePermission`)
- [ ] 面包屑导航
- [ ] 页面加载骨架屏
- [ ] 批量操作功能
- [ ] 数据导出功能 (Excel/CSV)

#### 5.6.2 用户体验优化

- [ ] 响应式布局优化
- [ ] 加载状态优化
- [ ] 错误提示优化
- [ ] 表单验证优化
- [ ] 快捷键支持
- [ ] 暗色主题支持

#### 5.6.3 性能优化

- [ ] 组件懒加载
- [ ] 列表虚拟滚动
- [ ] API 请求缓存
- [ ] 图片懒加载
- [ ] 代码分割优化

#### 5.6.4 测试与文档

- [ ] 单元测试 (Vitest)
- [ ] E2E 测试 (Playwright)
- [ ] API 文档完善
- [ ] 用户使用手册
- [ ] 开发者文档

---

### 5.7 技术优化建议

#### 5.7.1 短期优化 (1-2 周内)

**React Router 集成**：
```typescript
// 替换当前的状态切换为真正的路由
import { BrowserRouter, Routes, Route } from 'react-router-dom';

<BrowserRouter basename="/console">
  <Routes>
    <Route path="/" element={<Dashboard />} />
    <Route path="/applications" element={<Applications />} />
    <Route path="/users" element={<Users />} />
    <Route path="/roles" element={<Roles />} />
  </Routes>
</BrowserRouter>
```

**Token 自动刷新**：
```typescript
// 在 API 客户端中实现自动刷新
async function refreshToken() {
  const refreshToken = localStorage.getItem('refresh_token');
  const response = await fetch('/connect/token', {
    method: 'POST',
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: 'radish-console',
    }),
  });
  const data = await response.json();
  localStorage.setItem('access_token', data.access_token);
  return data.access_token;
}
```

**权限验证 Hook**：
```typescript
// hooks/usePermission.ts
export function usePermission(permission: string): boolean {
  const { permissions } = useAppContext();
  return permissions.includes(permission);
}

// 使用示例
const canCreateUser = usePermission('User:Create');
```

#### 5.7.2 中期优化 (1-2 个月内)

- 引入状态管理库 (Zustand 或 Jotai)
- 实现 WebSocket 实时通知
- 添加国际化支持 (i18n)
- 实现主题切换功能

#### 5.7.3 长期优化 (3-6 个月内)

- 微前端架构探索
- 插件化系统设计
- 性能监控和错误追踪 (Sentry)
- 自动化测试覆盖率 > 80%

---

### 5.8 里程碑规划

| 里程碑 | 目标 | 预计完成时间 | 状态 |
|--------|------|-------------|------|
| M1 | 基础框架 + 应用管理 | 2025-12 | ✅ 已完成 |
| **M10** | **Console 后台管理系统** | **2026-01-22** | **✅ 已完成** |

---

## 🎉 Console 项目完成总结 (2026-01-22)

**Console 后台管理系统已于 2026-01-22 基本完成**，实现了完整的后台管理功能。

### 已完成功能清单

1. **✅ 基础架构**
   - React 19 + Vite + TypeScript 项目架构
   - OIDC 统一认证集成
   - 响应式布局设计 (AdminLayout)
   - 路由系统 (React Router v7)
   - 主题配置 (萝卜橙主题)
   - 错误边界和异常处理

2. **✅ 用户管理**
   - 用户列表展示 (分页、搜索、筛选)
   - 用户详情页面 (等级、经验、萝卜币、流水)
   - 用户状态管理
   - 个人信息管理 (头像上传、信息编辑)

3. **✅ 商品管理**
   - 商品列表展示
   - 商品创建/编辑表单
   - 商品上下架管理
   - 商品删除功能

4. **✅ 订单管理**
   - 订单列表展示
   - 订单详情查看
   - 失败订单重试
   - 订单状态追踪

5. **✅ 角色管理**
   - 角色列表展示
   - 权限信息展示
   - API 封装完成

6. **✅ Dashboard 仪表盘**
   - 关键指标卡片
   - 快速操作入口
   - 最近订单展示
   - 统计数据可视化

7. **✅ 基础设施**
   - 组件库复用 (@radish/ui)
   - API 客户端封装
   - 统一错误处理
   - 加载状态管理
   - 面包屑导航
   - 全局搜索功能

### 技术成果

- **严格的分层架构**：遵循后端分层架构规范
- **组件化设计**：TableSkeleton 等组件迁移到 @radish/ui
- **类型安全**：完整的 TypeScript 类型定义
- **用户体验**：响应式设计、加载状态、错误处理
- **代码质量**：统一的代码规范和最佳实践

### 项目状态

**Console 后台管理系统现已具备完整的管理功能，可以投入生产使用。**

后续优化将在 M11 查漏补缺阶段进行，重点关注 UI 美化和用户体验提升。

---

### 5.9 风险与挑战

#### 5.9.1 技术风险

- **权限系统复杂度**：RBAC 实现需要仔细设计，避免权限漏洞
- **性能问题**：大量用户和日志数据可能导致查询性能问题
- **Token 安全**：localStorage 存储 Token 存在 XSS 风险

**应对措施**：
- 权限系统参考成熟方案，进行充分测试
- 实现分页、索引优化、缓存机制
- 考虑使用 HttpOnly Cookie 存储 Token

#### 5.9.2 业务风险

- **需求变更**：管理后台需求可能随业务发展而变化
- **用户体验**：管理员对后台易用性要求较高

**应对措施**：
- 采用迭代开发，快速响应需求变更
- 定期收集管理员反馈，持续优化体验

---

## 相关文档

- [核心概念](/guide/console-core-concepts) - 权限模型和实体定义
- [功能模块](/guide/console-modules) - 详细功能说明
- [技术架构](/guide/console-architecture) - 技术实现细节
- [开发计划](/development-plan) - 整体项目规划
