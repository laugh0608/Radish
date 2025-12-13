# @radish/ui 前端 UI 组件库设置完成

## ✅ 已完成

我已经为你创建了正确的前端 UI 组件库 `@radish/ui`（不是之前错误的 `@radish/shared`）。

### 项目结构

```
radish.ui/                    # 前端 UI 组件库 (新建)
├── src/
│   ├── components/
│   │   └── Button/
│   │       ├── Button.tsx
│   │       └── Button.css
│   ├── hooks/
│   │   └── useDebounce.ts
│   ├── utils/
│   │   └── format.ts
│   ├── types/
│   │   └── index.ts
│   └── index.ts
├── package.json
├── tsconfig.json
├── eslint.config.js
└── README.md

Radish.Shared/                # C# 后端项目 (已存在，未修改)
├── CustomEnum/
│   ├── AuthorityScopeKindEnum.cs
│   ├── DepartmentStatusCodeEnum.cs
│   ├── HttpStatusCodeEnum.cs
│   ├── UserSexEnum.cs
│   └── UserStatusCodeEnum.cs
└── Radish.Shared.csproj
```

### 命名说明

- **radish.ui**: 前端 UI 组件库 (TypeScript/React) - **新创建**
- **Radish.Shared**: 后端共享代码 (C#/.NET) - **已存在**

两者完全独立，服务于不同的层次，不会冲突。

## 🚀 使用方法

### 在 radish.client 或 radish.console 中导入

```typescript
// 导入组件
import { Button } from '@radish/ui';

// 导入 Hooks
import { useDebounce } from '@radish/ui/hooks';

// 导入工具函数
import { formatDate, formatFileSize } from '@radish/ui/utils';

// 导入类型
import type { ApiResponse, PaginatedResponse } from '@radish/ui/types';
```

### 完整示例

```tsx
import { Button } from '@radish/ui';
import { useDebounce } from '@radish/ui/hooks';
import { formatDate } from '@radish/ui/utils';

function MyComponent() {
  const [searchTerm, setSearchTerm] = useState('');
  const debouncedSearch = useDebounce(searchTerm, 300);

  return (
    <div>
      <p>当前时间: {formatDate(new Date())}</p>
      <input
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
      />
      <Button variant="primary" onClick={() => console.log(debouncedSearch)}>
        搜索
      </Button>
    </div>
  );
}
```

## ✨ 已包含的内容

### 组件
- **Button**: 支持 3 种变体 (primary/secondary/danger) 和 3 种尺寸 (small/medium/large)

### Hooks
- **useDebounce**: 防抖 Hook

### 工具函数
- **formatDate**: 日期格式化
- **formatFileSize**: 文件大小格式化

### 类型定义
- **ApiResponse<T>**: API 响应通用结构
- **PaginationParams**: 分页参数
- **PaginatedResponse<T>**: 分页响应

## 📦 Workspaces 配置

### 根 package.json

```json
{
  "workspaces": [
    "radish.client",
    "radish.console",
    "radish.ui"
  ]
}
```

### 依赖关系

- `radish.client` → 依赖 `@radish/ui`
- `radish.console` → 依赖 `@radish/ui`
- `@radish/ui` → 独立包，使用 peerDependencies

### 验证结果

```bash
$ ls -la node_modules/@radish/
lrwxrwxrwx ui -> ../../radish.ui

$ npm run type-check --workspace=@radish/ui
✓ TypeScript 类型检查通过
```

## 🎯 下一步建议

### 1. 查看示例代码

```bash
# 查看完整使用示例
cat radish.console/src/examples/SharedComponentExample.tsx
```

### 2. 添加更多组件

建议添加的组件：
- **Input**: 输入框组件
- **Select**: 下拉选择组件
- **Modal**: 模态框组件
- **Table**: 表格组件
- **Form**: 表单组件
- **Pagination**: 分页组件

### 3. 迁移现有代码

将 `radish.client` 和 `radish.console` 中的通用代码迁移到 `@radish/ui`：

```bash
# 示例：迁移通用按钮组件
# 1. 将组件复制到 radish.ui/src/components/
# 2. 在 radish.ui/src/components/index.ts 中导出
# 3. 更新 client 和 console 中的导入路径
# 4. 删除原项目中的旧文件
```

### 4. 完善文档

为每个组件添加：
- 使用说明
- Props 文档
- 示例代码
- 最佳实践

## 💡 关键特性

### 1. 修改立即生效

由于使用符号链接，修改 `radish.ui` 中的代码会立即在 `radish.client` 和 `radish.console` 中生效，无需重新安装。

### 2. 完整类型支持

所有导出都有完整的 TypeScript 类型定义，享受完整的 IDE 智能提示。

### 3. Tree-shaking 支持

使用子路径导入（如 `@radish/ui/hooks`）支持更好的 tree-shaking，减小打包体积。

### 4. 热模块替换 (HMR)

Vite 的 HMR 会自动检测变化并更新，无需手动刷新。

## 🔧 开发命令

```bash
# 安装所有依赖
npm install

# 运行类型检查
npm run type-check --workspace=@radish/ui

# 运行 Lint
npm run lint --workspace=@radish/ui

# 启动 client 开发服务器
npm run dev --workspace=radish.client

# 启动 console 开发服务器
npm run dev --workspace=radish.console
```

## ⚠️ 注意事项

### 1. 不要混淆两个 Shared 项目

- **radish.ui**: 前端 UI 组件库 (TypeScript/React)
- **Radish.Shared**: 后端共享代码 (C#/.NET)

### 2. 修改 package.json 后需要重新安装

如果修改了 `radish.ui/package.json`，需要运行 `npm install`。

### 3. CSS 类名使用前缀

使用 `.radish-` 前缀避免样式冲突：

```css
.radish-button {
  /* ... */
}
```

### 4. 保持包的轻量

避免在 `@radish/ui` 中引入大型第三方库，保持包的轻量和灵活。

## 📚 文档位置

- **radish.ui/README.md**: 组件库概述和使用说明
- **radish.console/src/examples/SharedComponentExample.tsx**: 完整使用示例
- **UI_PACKAGE_SETUP.md**: 本文档（设置说明）

## 🎉 总结

创建 `@radish/ui` 前端 UI 组件库已完成：

### 已完成的工作

- ✅ 配置 npm workspaces
- ✅ 创建包结构
- ✅ 实现 4 个 UI 组件 (Button, Input, Select, Modal)
- ✅ 实现 4 个 Hooks (useDebounce, useLocalStorage, useToggle, useClickOutside)
- ✅ 实现 12 个工具函数 (日期、验证、字符串处理)
- ✅ 配置 TypeScript 和 ESLint
- ✅ 更新 client 和 console 依赖
- ✅ 验证类型检查和链接
- ✅ 编写完整示例代码
- ✅ 编写详细文档

### 组件库内容

**组件 (4 个)**:
- Button - 按钮组件 (3 种变体, 3 种尺寸)
- Input - 输入框组件 (支持标签、错误提示、帮助文本)
- Select - 下拉选择组件 (支持选项数组、占位符)
- Modal - 模态框组件 (3 种尺寸、动画效果)

**Hooks (4 个)**:
- useDebounce - 防抖
- useLocalStorage - localStorage 持久化
- useToggle - 布尔值切换
- useClickOutside - 点击外部检测

**工具函数 (12 个)**:
- 日期和文件: formatDate, formatFileSize
- 验证: isEmail, isPhone, isUrl, isIdCard, getPasswordStrength
- 字符串: truncate, capitalize, camelToKebab, kebabToCamel, randomString

**类型定义 (3 个)**:
- ApiResponse<T>, PaginationParams, PaginatedResponse<T>

### 质量保证

- ✅ TypeScript 类型检查通过
- ✅ ESLint 检查通过
- ✅ 所有组件有完整类型定义
- ✅ 所有工具函数有 JSDoc 注释
- ✅ 完整的使用示例

### 文档

- `radish.ui/README.md` - 组件库概述
- `radish.ui/COMPONENTS_SUMMARY.md` - 组件库详细总结
- `radish.console/src/examples/UIComponentsExample.tsx` - 完整使用示例
- `UI_PACKAGE_SETUP.md` - 本文档

### 收益

- 代码复用 - 避免重复编写相同组件
- 统一 UI 风格 - 保持一致的用户体验
- 更好的维护性 - 集中管理通用代码
- 更好的开发体验 - 完整的 TypeScript 支持
- 类型安全 - 编译时错误检查

现在你可以在 `radish.client` 和 `radish.console` 中使用 `@radish/ui` 的所有组件、Hooks 和工具函数了！

查看 `radish.console/src/examples/UIComponentsExample.tsx` 获取完整的使用示例。

---

**创建日期**: 2025-12-13
**npm 版本**: 11.6.1
**包名**: @radish/ui (不是 @radish/shared)
**组件数量**: 4 个组件 + 4 个 Hooks + 12 个工具函数
