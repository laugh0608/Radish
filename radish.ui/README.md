# @radish/ui

Radish 项目的前端 UI 组件库，包含通用的 React 组件、Hooks、工具函数和类型定义。

> **注意**: 这是前端 UI 组件包，不要与后端的 `Radish.Shared` (C# 项目) 混淆。

## 快速开始

### 安装

由于使用了 npm workspaces，依赖会自动链接：

```bash
npm install
```

### 导入使用

```typescript
// 组件
import { Button, Input, Select, Modal } from '@radish/ui';

// Hooks
import { useDebounce, useLocalStorage, useToggle } from '@radish/ui/hooks';

// 工具函数
import { formatDate, isEmail, truncate } from '@radish/ui/utils';

// 类型
import type { ApiResponse } from '@radish/ui/types';
```

### 示例

```tsx
import { Button, Input } from '@radish/ui';
import { useToggle } from '@radish/ui/hooks';
import { formatDate } from '@radish/ui/utils';

function MyComponent() {
  const [isOpen, toggle] = useToggle(false);

  return (
    <div>
      <Input label="用户名" placeholder="请输入用户名" />
      <Button variant="primary" onClick={toggle}>
        切换
      </Button>
      <p>{formatDate(new Date())}</p>
    </div>
  );
}
```

## 包含内容

- **组件 (4 个)**: Button, Input, Select, Modal
- **Hooks (4 个)**: useDebounce, useLocalStorage, useToggle, useClickOutside
- **工具函数 (12 个)**: 日期格式化、验证、字符串处理等
- **类型定义 (3 个)**: ApiResponse, PaginationParams, PaginatedResponse

## 文档

完整文档请查看：

- **[UI 组件库概览](../radish.docs/docs/frontend/ui-library.md)** - @radish/ui 的入口文档
- **[前端组件文档](../radish.docs/docs/frontend/components.md)** - 组件与用法说明
- **[前端快速参考](../radish.docs/docs/frontend/quick-reference.md)** - 常用 API 速查表
- **[完整示例](../radish.console/src/examples/UIComponentsExample.tsx)** - 可运行的完整示例代码

## 开发

```bash
# 类型检查
npm run type-check --workspace=@radish/ui

# 代码检查
npm run lint --workspace=@radish/ui
```

## 项目结构

```
radish.ui/
├── src/
│   ├── components/     # UI 组件
│   ├── hooks/          # React Hooks
│   ├── utils/          # 工具函数
│   ├── types/          # 类型定义
│   └── index.ts        # 主入口
├── package.json
├── tsconfig.json
└── README.md
```

## 注意事项

- 这是一个内部包（`private: true`），不会发布到 npm
- 使用 `peerDependencies` 避免重复安装 React
- 修改后无需重新安装，Vite HMR 会自动更新
- 所有 CSS 类使用 `.radish-` 前缀避免冲突

---

**版本**: 0.1.0
**维护**: Radish Team
**文档**: [frontend/ui-library.md](../radish.docs/docs/frontend/ui-library.md)
