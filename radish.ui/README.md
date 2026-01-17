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
// 自研组件
import { Button, Input, Modal, Icon, Toast } from '@radish/ui';

// Ant Design 组件（通过 @radish/ui 封装）
import { Table, Form, AntButton, Menu, Layout } from '@radish/ui';

// 主题配置
import { ThemeProvider, radishColors } from '@radish/ui';

// Hooks
import { useDebounce, useLocalStorage, useToggle } from '@radish/ui/hooks';

// 工具函数
import { formatDate, isEmail, truncate } from '@radish/ui/utils';

// API 客户端
import { apiGet, apiPost, configureApiClient } from '@radish/ui';

// 类型
import type { ApiResponse, PagedResponse } from '@radish/ui/types';
```

### 基础示例

```tsx
import { Button, Input, ThemeProvider } from '@radish/ui';
import { useToggle } from '@radish/ui/hooks';

function App() {
  return (
    <ThemeProvider>
      <MyComponent />
    </ThemeProvider>
  );
}

function MyComponent() {
  const [isOpen, toggle] = useToggle(false);

  return (
    <div>
      <Input label="用户名" placeholder="请输入用户名" />
      <Button variant="primary" onClick={toggle}>
        切换
      </Button>
    </div>
  );
}
```

## 包含内容

### 自研组件（20+）

这些组件完全自主开发，不依赖第三方 UI 库：

- **基础组件**: Button, Input, Select, Modal, Icon
- **数据展示**: MarkdownRenderer, ExperienceBar, LevelUpModal
- **编辑器**: MarkdownEditor
- **文件上传**: FileUpload, ChunkedFileUpload
- **反馈组件**: Toast, ConfirmDialog
- **通知系统**: Notification, NotificationCenter, NotificationList, NotificationBadge
- **其他**: ContextMenu, GlassPanel, UserMention

### Ant Design 组件封装

通过 @radish/ui 统一封装，便于未来替换：

- **布局**: Layout, Menu, Space, Divider
- **表单**: Form, Input (AntInput), Select (AntSelect), DatePicker, Checkbox, Radio, Switch
- **数据展示**: Table, Tag, Badge, Tooltip, Avatar, Dropdown
- **反馈**: Modal (AntModal), message, notification
- **通用**: Button (AntButton), Popconfirm

### Hooks（10+）

- `useDebounce` - 防抖
- `useLocalStorage` - 本地存储
- `useToggle` - 布尔值切换
- `useClickOutside` - 点击外部检测
- 更多...

### 工具函数（20+）

- **日期**: formatDate, parseDate
- **验证**: isEmail, isPhone, isUrl
- **字符串**: truncate, capitalize, slugify
- **数字**: formatNumber, formatCurrency
- 更多...

### API 客户端

统一的 HTTP 客户端，支持：
- 自动 Token 注入
- 统一错误处理
- 请求/响应拦截
- TypeScript 类型支持

## 封装层设计

### 设计目标

通过 @radish/ui 统一封装第三方 UI 库（当前为 Ant Design），为未来替换为自研组件做准备。

### 封装原则

#### 1. 统一入口

所有项目（radish.client、radish.console）**必须**通过 @radish/ui 使用 UI 组件，**禁止**直接引入 antd。

```typescript
// ❌ 错误：直接引入 antd
import { Button, Table } from 'antd';

// ✅ 正确：通过 @radish/ui 引入
import { AntButton, Table } from '@radish/ui';
```

#### 2. 命名规范

- **Ant Design 原生组件**：使用 `Ant` 前缀（如 `AntButton`、`AntModal`）
- **自研组件**：不加前缀（如 `Button`、`Modal`）
- **未来替换时**：只需修改 @radish/ui 的导出，不影响业务代码

#### 3. 类型导出

所有类型定义也通过 @radish/ui 导出：

```typescript
// @radish/ui/src/components/index.ts
export type {
  ButtonProps as AntButtonProps,
  TableProps,
  FormProps,
  // ...
} from 'antd';
```

### 当前封装状态

#### 已封装的自研组件

这些组件已经是自研的，不依赖 Ant Design：

- `Button` - 自研按钮组件
- `Input` - 自研输入框组件
- `Select` - 自研选择器组件
- `Modal` - 自研模态框组件
- `Icon` - 自研图标组件（基于 Iconify 本地图标集）
- `Toast` - 自研提示组件
- `MarkdownEditor` - 自研 Markdown 编辑器
- `FileUpload` - 自研文件上传组件
- `ExperienceBar` - 自研经验条组件
- `LevelUpModal` - 自研升级弹窗组件
- `Notification` 系列 - 自研通知组件

#### 直接导出的 Ant Design 组件

这些组件当前直接导出 Ant Design，未来需要替换：

- `Layout` - 布局组件
- `Menu` - 菜单组件
- `Table` - 表格组件
- `Form` - 表单组件
- `DatePicker` - 日期选择器
- `Checkbox` - 复选框
- `Radio` - 单选框
- `Switch` - 开关
- `Tag` - 标签
- `Badge` - 徽章
- `Tooltip` - 提示框
- `Avatar` - 头像
- `Dropdown` - 下拉菜单
- `Space` - 间距组件
- `Divider` - 分割线

#### 混合封装的组件

这些组件基于 Ant Design 封装，添加了业务逻辑：

- `DataTable` - 基于 Ant Design Table 封装的数据表格

### 未来替换策略

#### 阶段 1：优先级排序

按使用频率和复杂度排序，优先替换简单且常用的组件：

1. **高优先级**（简单且常用）：
   - Tag, Badge, Tooltip, Avatar, Space, Divider

2. **中优先级**（中等复杂度）：
   - Checkbox, Radio, Switch, Dropdown

3. **低优先级**（复杂度高）：
   - Table, Form, DatePicker, Layout, Menu

#### 阶段 2：渐进式替换

每次替换一个组件，遵循以下步骤：

1. **创建自研组件**
2. **更新导出**
3. **验证兼容性**
4. **逐步迁移**（不需要修改业务代码）

#### 阶段 3：完全移除 Ant Design

当所有组件都替换完成后：

1. 从 @radish/ui 的 package.json 中移除 antd 依赖
2. 删除 antd 相关的导出和类型
3. 移除 ThemeProvider 中的 ConfigProvider
4. 更新文档

### 使用示例

#### Console 项目中使用

```typescript
// radish.console/src/pages/Products/ProductList.tsx
import {
  // 自研组件（无前缀）
  Button,
  Input,
  Modal,
  Icon,
  Toast,

  // Ant Design 组件（Ant 前缀）
  Table,
  Form,
  AntButton,
  Space,
  Tag,

  // 类型
  type TableColumnsType,
  type FormProps,
} from '@radish/ui';

export function ProductList() {
  return (
    <div>
      {/* 自研按钮 */}
      <Button onClick={() => {}}>自研按钮</Button>

      {/* Ant Design 按钮 */}
      <AntButton type="primary">Ant 按钮</AntButton>

      {/* Ant Design 表格 */}
      <Table columns={columns} dataSource={data} />
    </div>
  );
}
```

#### Client 项目中使用

```typescript
// radish.client/src/apps/forum/PostList.tsx
import {
  Button,
  Input,
  Modal,
  MarkdownEditor,
  Toast,
} from '@radish/ui';

// Client 项目主要使用自研组件，不使用 Ant Design
```

### 注意事项

1. **禁止直接引入 antd**：所有项目必须通过 @radish/ui 使用组件
2. **保持 API 兼容**：替换组件时尽量保持 API 兼容，减少迁移成本
3. **渐进式替换**：不要一次性替换所有组件，逐步进行
4. **文档同步**：每次替换后更新本文档的封装状态
5. **测试覆盖**：替换前后都要进行充分测试

## 主题配置

### 使用主题提供者

```tsx
import { ThemeProvider } from '@radish/ui';

function App() {
  return (
    <ThemeProvider>
      <YourApp />
    </ThemeProvider>
  );
}

// 暗色主题
function DarkApp() {
  return (
    <ThemeProvider dark>
      <YourApp />
    </ThemeProvider>
  );
}
```

### 主题色

```typescript
import { radishColors } from '@radish/ui';

console.log(radishColors.primary);  // #FF6B35 (萝卜橙)
console.log(radishColors.success);  // #52c41a
console.log(radishColors.warning);  // #faad14
console.log(radishColors.error);    // #ff4d4f
console.log(radishColors.info);     // #1890ff
```

### 自定义主题

修改 `radish.ui/src/theme/antd-theme.ts` 文件：

```typescript
export const radishColors = {
  primary: '#FF6B35',  // 修改主色调
  // ...
};

export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: radishColors.primary,
    fontSize: 14,
    borderRadius: 6,
    // ...
  },
  components: {
    Button: {
      controlHeight: 32,
      // ...
    },
    // ...
  },
};
```

## API 客户端

### 配置

```typescript
import { configureApiClient, configureErrorHandling } from '@radish/ui';

// 配置 API 客户端
configureApiClient({
  baseUrl: 'https://api.example.com',
  timeout: 30000,
  getToken: () => localStorage.getItem('access_token'),
});

// 配置错误处理
configureErrorHandling({
  autoShowMessage: true,
  showMessage: (msg) => {
    console.error(msg);
  },
});
```

### 使用

```typescript
import { apiGet, apiPost, apiPut, apiDelete } from '@radish/ui';
import type { PagedResponse } from '@radish/ui';

// GET 请求
const response = await apiGet<User>('/api/users/1', { withAuth: true });
if (response.success) {
  console.log(response.data);
}

// POST 请求
const createResponse = await apiPost<User>('/api/users', {
  name: 'John',
  email: 'john@example.com',
}, { withAuth: true });

// 分页请求
const pagedResponse = await apiGet<PagedResponse<User>>(
  '/api/users?page=1&pageSize=10',
  { withAuth: true }
);
```

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
│   ├── api/            # API 客户端
│   ├── theme/          # 主题配置
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
- **所有项目必须通过 @radish/ui 使用 UI 组件，禁止直接引入 antd**
- Ant Design 组件使用 `Ant` 前缀，自研组件不加前缀
- 图标系统基于本地 Iconify JSON 集合，不依赖在线 API

## 关于 Ant Design

### 为什么使用 Ant Design？

- ✅ 组件丰富，开发效率高
- ✅ 通过 npm 安装，完全本地打包
- ✅ CSS-in-JS 技术，不依赖外部 CDN
- ✅ 即使在内网/离线环境也能正常工作

### 为什么要封装？

- ✅ 统一管理版本和主题
- ✅ 便于未来替换为自研组件
- ✅ 保持代码可维护性
- ✅ 逐步实现完全自主可控

### 不会有 CDN 问题

Ant Design 通过 npm 安装，所有代码都在 `node_modules` 中，构建后完全自包含，不需要任何外部资源。这与之前的图标库问题不同（图标库之前是调用在线 API，现在已改为本地图标集）。

---

**版本**: 0.1.0
**维护**: Radish Team
**文档**: [frontend/ui-library.md](../radish.docs/docs/frontend/ui-library.md)
