# Radish WebOS - 快速开始

## 🚀 启动开发服务器

```bash
cd radish.client
npm run dev
```

## 📱 访问不同页面

启动后，你可以通过不同的 URL 访问不同的页面：

### 1. **WebOS Desktop Shell** (默认)
```
http://localhost:3000/
```
- 完整的桌面操作系统体验
- 状态栏显示用户信息和时间（当前默认用户：`userId=1`, `userName='test'`, `roles=['User','Admin']`）
- 桌面显示应用图标
- 双击图标打开应用
- Dock 显示运行中的应用
- 支持窗口拖拽、最小化、关闭

### 2. **组件展示页面**
```
http://localhost:3000/?showcase
```
- 查看所有基础 UI 组件
- Button、Icon、GlassPanel 等组件预览
- 组合使用示例

### 3. **OIDC Demo 页面**
```
http://localhost:3000/?demo
```
- 原有的 OIDC 认证流程演示
- 天气预报 API 测试
- 用户信息获取测试

---

## 🎮 WebOS 使用指南

### 桌面操作
- **双击图标** - 打开应用
- **拖拽窗口** - 点击窗口标题栏拖动
- **关闭窗口** - 点击右上角红色按钮
- **最小化窗口** - 点击右上角黄色按钮

### Dock 操作
- **点击应用图标** - 切换到该应用（如果已打开）
- **点击最小化的应用** - 恢复窗口
- **运行指示器** - 底部白色小圆点表示应用正在运行

### 状态栏
- **左侧** - 品牌名称 + 当前用户信息
- **右侧** - 实时时钟

---

## 🧩 当前可用应用

### 欢迎应用
- **图标**: 👋 (mdi:hand-wave)
- **功能**:
  - 显示用户信息（取自 `useUserStore` 当前用户，默认 `test/Admin`）
  - WebOS 使用指南
  - 快速开始提示

---

## 📦 项目结构

```
src/
├── desktop/              # 桌面系统核心
│   ├── Shell.tsx        # 桌面外壳
│   ├── StatusBar.tsx    # 状态栏
│   ├── Desktop.tsx      # 桌面图标网格
│   ├── Dock.tsx         # Dock 栏
│   ├── WindowManager.tsx # 窗口管理器
│   ├── AppRegistry.tsx  # 应用注册表
│   └── types.ts         # 类型定义
│
├── apps/                # 子应用
│   └── welcome/         # 欢迎应用
│
├── widgets/             # 桌面小部件
│   ├── AppIcon.tsx      # 应用图标
│   └── DesktopWindow.tsx # 桌面窗口
│
├── stores/              # 全局状态
│   ├── windowStore.ts   # 窗口状态管理
│   └── userStore.ts     # 用户状态管理
│
└── shared/ui/           # 通用 UI 组件
    ├── base/            # 基础组件
    └── desktop/         # 桌面专用组件
```

---

## 🔧 开发相关

### 当前模拟用户
```typescript
{
  userId: 1,
  userName: 'test',
  tenantId: 1,
  roles: ['User', 'Admin']
}
```

**注意**: 这是临时的模拟数据，实际应该从 OIDC 登录流程中获取。

### 添加新应用

1. 在 `src/apps/` 下创建新应用目录
2. 创建应用组件（如 `ForumApp.tsx`）
3. 在 `desktop/AppRegistry.tsx` 中注册应用：

```typescript
{
  id: 'forum',
  name: '论坛',
  icon: 'mdi:forum',
  description: '社区讨论与内容分享',
  component: ForumApp,
  type: 'window',
  defaultSize: { width: 1200, height: 800 },
  requiredRoles: ['User'],
  category: 'content'
}
```

---

## 🐛 调试技巧

### 查看窗口状态
在浏览器控制台中：
```javascript
// 查看所有打开的窗口
useWindowStore.getState().openWindows

// 查看当前用户
useUserStore.getState()
```

### 修改用户角色
在浏览器控制台中：
```javascript
// 添加角色
useUserStore.getState().setUser({
  userId: 1,
  userName: 'test',
  tenantId: 1,
  roles: ['User', 'Admin', 'Developer']
})
```

---

## 📝 TODO

- [ ] 集成真实的 OIDC 登录流程
- [ ] 添加论坛应用
- [ ] 添加聊天室应用
- [ ] 添加后台管理应用
- [ ] 实现应用权限控制
- [ ] 优化移动端适配
- [ ] 添加快捷键支持
- [ ] 实现窗口最大化功能
- [ ] 添加桌面右键菜单
- [ ] 实现应用间通信

---

## 💡 技术栈

- **React 19** - UI 框架
- **TypeScript** - 类型安全
- **Zustand** - 状态管理
- **react-rnd** - 窗口拖拽和调整大小
- **@radish/ui/Icon（基于 @iconify/react + 本地 Iconify JSON 集合）** - 图标系统
- **CSS Modules** - 样式隔离
- **Vite (Rolldown)** - 构建工具

---

## 📚 相关文档

- [UI 组件库](/frontend/ui-library) - @radish/ui 入口文档
- [前端设计文档](/frontend/design) - WebOS 架构设计
- [开发规范](/architecture/specifications) - 项目开发规范

---

## ❓ 常见问题

### Q: 为什么桌面上只有一个"欢迎"应用？
A: 目前只实现了欢迎应用作为演示。更多应用（论坛、聊天等）会在后续添加。

### Q: 如何回到原来的 OIDC Demo 页面？
A: 访问 `http://localhost:3000/?demo`

### Q: 如何查看组件库？
A: 访问 `http://localhost:3000/?showcase`

### Q: 窗口拖拽不流畅怎么办？
A: 确保浏览器硬件加速已开启，或尝试减少打开的窗口数量。

---

**享受 Radish WebOS 吧！** 🎉
