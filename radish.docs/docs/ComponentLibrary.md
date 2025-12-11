# Radish WebOS 组件库

## 📦 已实现的组件

### 基础组件 (base)

#### Icon - 图标组件
封装 @iconify/react，提供统一的图标使用方式。

**位置**: `src/shared/ui/base/Icon/`

**使用示例**:
```tsx
import { Icon } from '@/shared/ui/base/Icon';

<Icon icon="mdi:home" size={24} color="#333" />
<Icon icon="mdi:account-circle" size={32} />
```

**Props**:
- `icon`: 图标名称（Iconify 图标集，如 "mdi:home"）
- `size`: 图标大小（像素，默认 24）
- `color`: 图标颜色（默认 "currentColor"）

**参考**: [Iconify 图标搜索](https://icon-sets.iconify.design/)

---

#### Button - 按钮组件
通用按钮组件，支持多种变体、尺寸和图标。

**位置**: `src/shared/ui/base/Button/`

**使用示例**:
```tsx
import { Button } from '@/shared/ui/base/Button';
import { Icon } from '@/shared/ui/base/Icon';

<Button variant="primary" size="medium" onClick={handleClick}>
  点击我
</Button>

<Button variant="ghost" icon={<Icon icon="mdi:plus" />}>
  添加
</Button>
```

**Props**:
- `variant`: 按钮变体（'primary' | 'secondary' | 'ghost'，默认 'primary'）
- `size`: 按钮尺寸（'small' | 'medium' | 'large'，默认 'medium'）
- `icon`: 按钮前置图标（ReactNode）
- `children`: 按钮内容
- 其他所有原生 button 属性

---

### 桌面组件 (desktop)

#### GlassPanel - 毛玻璃面板
提供毛玻璃效果的容器，常用于桌面 UI、弹窗等场景。

**位置**: `src/shared/ui/desktop/GlassPanel/`

**使用示例**:
```tsx
import { GlassPanel } from '@/shared/ui/desktop/GlassPanel';

<GlassPanel blur="medium" background="light">
  <h2>标题</h2>
  <p>内容...</p>
</GlassPanel>
```

**Props**:
- `blur`: 模糊强度（'light' | 'medium' | 'strong'，默认 'medium'）
- `background`: 背景透明度（'light' | 'dark'，默认 'light'）
- `bordered`: 是否显示边框（boolean，默认 true）
- `children`: 面板内容
- 其他所有原生 div 属性

---

## 🎨 设计原则

1. **CSS Modules**: 使用 CSS Modules 实现样式隔离，避免全局污染
2. **TypeScript**: 完整的类型定义，提供良好的开发体验
3. **可组合**: 组件支持组合使用，通过 props 灵活配置
4. **轻量级**: 不依赖重量级 UI 框架，保持体积小巧
5. **无障碍**: 基本的 ARIA 支持（后续完善）

---

## 🚀 查看组件效果

### 方法一：访问组件展示页面
1. 启动开发服务器：`npm run dev`
2. 在浏览器中访问：`http://localhost:3000/?showcase`

### 方法二：导入到你的代码中
```tsx
import { Button } from './shared/ui/base/Button';
import { Icon } from './shared/ui/base/Icon';
import { GlassPanel } from './shared/ui/desktop/GlassPanel';

function MyComponent() {
  return (
    <GlassPanel>
      <Button icon={<Icon icon="mdi:home" />}>
        返回首页
      </Button>
    </GlassPanel>
  );
}
```

---

## 📁 目录结构

```
src/
├── shared/
│   └── ui/
│       ├── base/              # 基础组件
│       │   ├── Button/
│       │   │   ├── Button.tsx
│       │   │   ├── Button.module.css
│       │   │   └── index.ts
│       │   ├── Icon/
│       │   │   ├── Icon.tsx
│       │   │   └── index.ts
│       │   └── Input/         # 待实现
│       │
│       └── desktop/           # 桌面专用组件
│           ├── GlassPanel/
│           │   ├── GlassPanel.tsx
│           │   ├── GlassPanel.module.css
│           │   └── index.ts
│           ├── DesktopIcon/   # 待实现
│           └── WindowChrome/  # 待实现
```

---

## 🔧 后续计划

### 基础组件
- [ ] Input - 输入框组件
- [ ] Modal - 弹窗组件
- [ ] Select - 选择器组件
- [ ] Checkbox - 复选框组件

### 桌面组件
- [ ] DesktopIcon - 桌面图标组件
- [ ] WindowChrome - 窗口标题栏和控制按钮
- [ ] DesktopCard - 桌面卡片组件

---

## 💡 为什么不用 TailwindCSS？

1. **WebOS 风格更适合纯 CSS**: 桌面 UI 样式相对固定，精细控制时纯 CSS 更灵活
2. **参考 webos.html 原型**: 原型使用纯 CSS 实现，效果已经很好
3. **减少依赖**: 不需要额外的构建配置和依赖
4. **更好的性能**: 纯 CSS 文件更小，不需要 PostCSS 处理

---

## 🎨 UI 资源参考

- **Uiverse Galaxy**: https://github.com/uiverse-io/galaxy (3500+ 开源 UI 组件)
- **Iconify**: https://icon-sets.iconify.design/ (20万+ 图标)
- **webos.html**: `radish.client/public/webos.html` (项目原型)

---

## ❓ 常见问题

**Q: 如何在 radish.console 中复用这些组件？**

A: 可以通过以下方式：
1. 将 `shared/ui/` 目录提取为独立的 npm 包
2. 或者使用 npm workspace 在 monorepo 中共享
3. 或者直接复制代码到 radish.console

**Q: 为什么使用 CSS Modules 而不是 styled-components？**

A: CSS Modules 更轻量，无需运行时，与 Vite 原生集成，适合我们的场景。

---

## 📝 贡献指南

添加新组件时，请遵循以下规范：

1. 在 `shared/ui/base/` 或 `shared/ui/desktop/` 下创建组件目录
2. 创建 `ComponentName.tsx`（组件逻辑）
3. 创建 `ComponentName.module.css`（组件样式）
4. 创建 `index.ts`（导出文件）
5. 添加完整的 TypeScript 类型定义
6. 添加 JSDoc 注释和使用示例
7. 在 ComponentShowcase.tsx 中添加展示示例
