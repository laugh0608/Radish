# @radish/ui 组件库

## 概述

`@radish/ui` 是 Radish 项目的统一前端 UI 组件库，为所有前端应用（radish.client、radish.console 等）提供一致的组件和工具函数。

> 当前页面只维护高频入口与当前实现口径，不再尝试把所有导出逐项枚举为“完整清单”；更细粒度导出以 `Frontend/radish.ui/src/components/index.ts` 与 `package.json` 为准。

### 核心目标

- 🎨 **统一设计**：保持所有应用的视觉风格一致
- 📦 **开箱即用**：提供常用组件和工具函数
- 🔧 **易于配置**：灵活的配置选项
- 📘 **类型安全**：完整的 TypeScript 支持
- 🚀 **高性能**：优化的组件性能

## 项目结构

```
Frontend/radish.ui/
├── src/
│   ├── api/                  # 仅兼容重导出 @radish/http
│   ├── components/          # UI 组件
│   │   ├── Button/
│   │   ├── Input/
│   │   ├── Modal/
│   │   ├── Toast/
│   │   ├── Notification/
│   │   ├── DataTable/       # 数据表格
│   │   └── index.ts
│   ├── hooks/               # React Hooks
│   │   ├── useDebounce.ts
│   │   ├── useToggle.ts
│   │   ├── useLocalStorage.ts
│   │   └── index.ts
│   ├── utils/               # 工具函数
│   │   ├── format.ts
│   │   ├── intl.ts
│   │   ├── validation.ts
│   │   └── index.ts
│   ├── theme/               # ThemeProvider、Ant Design theme / locale
│   └── index.ts             # 主入口
├── package.json
└── tsconfig.json
```

## 快速开始

### 安装

`@radish/ui` 使用 npm workspaces，无需单独安装。在项目根目录运行：

```bash
npm install
```

### 使用

在 `radish.client` 或 `radish.console` 中导入：

```typescript
// 导入组件
import { Button, Input, Modal, DataTable, Toast, MarkdownEditor } from '@radish/ui';

// 导入 Ant Design 组件（已封装）
import { AntButton, Table, Form, message } from '@radish/ui';

// 导入 Hooks
import { useDebounce, useToggle, useLocalStorage } from '@radish/ui';

// 导入工具函数
import {
  formatDate,
  formatLocalizedDateTime,
  formatLocalizedNumber,
  isEmail,
} from '@radish/ui';
```

### MarkdownEditor 使用示例

```typescript
import { useState } from 'react';
import { MarkdownEditor } from '@radish/ui';

function MyComponent() {
  const [content, setContent] = useState('');

  return (
    <MarkdownEditor
      value={content}
      onChange={setContent}
      placeholder="输入内容，支持 Markdown..."
      minHeight={200}
      maxHeight={500}
      showToolbar={true}
      theme="light"
      disabled={false}
    />
  );
}
```

**Props 说明：**
- `value: string` - 当前内容
- `onChange: (value: string) => void` - 内容变化回调
- `placeholder?: string` - 占位符文本
- `minHeight?: number` - 最小高度（像素）
- `maxHeight?: number` - 最大高度（像素）
- `showToolbar?: boolean` - 是否显示工具栏（默认 true）
- `disabled?: boolean` - 是否禁用（默认 false）
- `theme?: 'dark' | 'light'` - 主题风格（默认 dark）
- `toolbarExtras?: ReactNode` - 工具栏右侧扩展内容
- `className?: string` - 自定义样式类名

## 核心模块

### 1. HTTP 兼容入口 (`api/`)

`src/api/index.ts` 只为旧调用重新导出 `@radish/http`，不再持有 API 客户端实现。正式代码直接从 `@radish/http` 导入 `configureApiClient / apiGet / apiPost / ApiResponseError` 等能力。

**详细文档：** [`@radish/http` 指南](./http-client.md)

### 2. UI 组件 (components/)

#### 基础组件
- **Button** - 按钮组件
- **Input** - 输入框组件
- **Select** - 下拉选择组件
- **Modal** - 模态框组件
- **Icon** - 图标组件（基于 Iconify，本地 Iconify JSON 集合加载，不依赖公共 API，内置渲染兜底）
- **ContextMenu** - 右键菜单组件
- **ConfirmDialog** - 确认对话框组件
- **UserMention** - 用户提及组件（@用户名）
  - 可见加载、空态、选择提示通过 `UserMentionLabels` 由宿主注入
- **BottomSheet** - 底部抽屉 / 弹层容器
- **Skeleton** - 骨架屏组件

#### 内容编辑
- **MarkdownEditor** - Markdown 富文本编辑器
  - 完整的工具栏（标题、加粗、斜体、代码、列表、链接、图片等）
  - 编辑/预览模式切换
  - Emoji 选择器（160+ 常用表情）
  - 快捷键支持（Ctrl+B、Ctrl+I、Ctrl+K）
  - 可配置高度和禁用状态
- **MarkdownRenderer** - Markdown 渲染组件
  - 支持 GitHub Flavored Markdown
  - 代码高亮（基于 highlight.js）
  - 暗色主题优化

#### 数据展示
- **DataTable** - 数据表格组件（支持分页、loading、empty 状态）
- **GlassPanel** - 毛玻璃面板组件
- **ExperienceBar** - 经验值进度条组件
- **LevelUpModal** - 升级弹窗组件
- **ReactionBar** - 反应条组件
  - 支持 `Post / Comment / ChatMessage` 展示目标、只读状态、展开 / 收起、快捷 emoji 与 StickerPicker
  - 数量、选择状态、展开、取消 / 添加回应和表情选择器文案通过 `ReactionBarLabels` 由宿主注入
  - 组件只负责展示与交互，不请求通用 Reaction API；Forum 与 Chat 分别提供自己的权威状态和写入回调

#### 反馈与通知
- **Toast** - 轻量临时提示
  - 支持 `success / error / info / warning / custom`
  - 支持 `duration` 自动关闭，`duration = 0` 时保持常驻
  - 当前已支持底部剩余时间进度条，跟随自动关闭时长同步缩减
- **Notification / NotificationList / NotificationCenter / NotificationBadge** - 持久化通知组件族
  - 用于通知列表、未读状态、通知中心聚合展示
  - 与 `radish.client` 通知应用和实时通知链路配套使用

#### 媒体与上传
- **FileUpload / ChunkedFileUpload** - 文件上传与分片上传组件
- **ImageLightbox / ImageCropper** - 图片预览与裁剪组件
- **StickerPicker** - 表情包选择器
  - 搜索、空态和模式提示通过 `StickerPickerLabels` 由宿主注入

#### Ant Design 组件封装
`@radish/ui` 重新导出了常用的 Ant Design 组件，确保版本一致：

```typescript
// Layout
Layout, Menu

// Form
Form, AntInput, InputNumber, AntSelect, Switch, DatePicker, Checkbox, Radio

// Table
Table

// Feedback
AntModal, message, notification

// Data Display
Tag, Badge, Tooltip, Avatar, Dropdown

// General
AntButton, Space, Divider

// Other
Popconfirm
```

**详细文档：** [DataTable 组件](./data-table.md)

### 3. React Hooks (hooks/)

常用的自定义 Hooks：

```typescript
// 防抖
const debouncedValue = useDebounce(value, 300);

// 切换状态
const [isOpen, toggle] = useToggle(false);

// LocalStorage
const [value, setValue] = useLocalStorage('key', defaultValue);

// 点击外部关闭
useClickOutside(ref, () => setIsOpen(false));
```

### 4. 工具函数 (utils/)

#### 格式化 (format.ts)
```typescript
formatDate(date)          // 兼容日期格式
formatFileSize(bytes)     // 文件大小
```

#### 本地化格式化 (`intl.ts`)
```typescript
resolveIntlLocale(language)
formatLocalizedDateTime(value, language, options)
formatLocalizedNumber(value, language, options)
formatLocalizedRelativeTime(value, language, now)
```

语言与时区是不同输入：locale 决定展示格式，`Intl.DateTimeFormatOptions.timeZone` 决定显示哪个时刻。单位和业务文案仍由宿主翻译资源提供。

#### 验证 (validation.ts)
```typescript
isEmail(email)            // 验证邮箱
isPhone(phone)            // 验证手机号
isUrl(url)                // 验证 URL
isEmpty(value)            // 检查是否为空
```

#### 字符串 (string.ts)
```typescript
capitalize(str)           // 首字母大写
truncate(str, length)     // 截断字符串
slugify(str)              // 生成 slug
```

### 5. 主题与 Ant Design locale

```tsx
import { ThemeProvider, antdLocales } from '@radish/ui';

<ThemeProvider
  themeConfig={themeConfig}
  dark={colorScheme === 'dark'}
  locale={language === 'en' ? antdLocales.en : antdLocales.zh}
>
  {children}
</ThemeProvider>
```

`@radish/ui` 不读取宿主 i18next、`localStorage` 或认证状态。宿主负责解析主题和语言，再把 theme config、Ant Design locale、labels 与 formatter 输入共享组件。

## 开发模式

### 热更新

修改 `Frontend/radish.ui/` 中的代码会自动触发 Vite HMR，无需重启开发服务器。

### 类型检查

```bash
# 在 radish.ui 中进行类型检查
npm run type-check --workspace=@radish/ui

# Lint 检查
npm run lint --workspace=@radish/ui
```

## 最佳实践

### 1. 组件使用

**推荐：** 优先使用 `@radish/ui` 提供的组件

```typescript
// ✅ 推荐
import { AntButton, message } from '@radish/ui';

// ❌ 不推荐（版本可能不一致）
import { Button, message } from 'antd';
```

### 2. API 调用

**推荐：** 使用统一的 API 客户端

```typescript
// ✅ 推荐
import { apiGet, apiPost } from '@radish/http';

const result = await apiGet('/api/v1/Users', { withAuth: true });

// ❌ 不推荐（每个项目自己实现 fetch）
const response = await fetch('/api/v1/Users', {
  headers: { Authorization: `Bearer ${token}` }
});
```

### 3. 错误处理

**推荐：** 保留 `@radish/http` 的结构化响应，再由页面决定反馈层级

```typescript
const result = await apiGet<UserVo>(path, { withAuth: true });
if (!result.ok || !result.data) {
  message.error(result.message ?? t('error.common.loadFailed'));
  return;
}
```

not-found、conflict、权限等控制流只读取 status / `Code` / 数据状态，不匹配消息文本。完整规则见[错误处理指南](./error-handling.md)。

## 版本管理

`@radish/ui` 使用 npm workspaces 管理，版本与主项目保持同步。

### 添加新依赖

```bash
# 为 @radish/ui 添加依赖
npm install <package> --workspace=@radish/ui

# 为 @radish/ui 添加开发依赖
npm install -D <package> --workspace=@radish/ui
```

## 相关文档

- [API 客户端使用指南](./api-client.md)
- [错误处理指南](./error-handling.md)
- [DataTable 组件](./data-table.md)
- [快速参考](./quick-reference.md)

## 常见问题

### Q: 修改 radish.ui 后需要重启服务器吗？

**A:** 不需要。Vite 的 HMR 会自动更新。

### Q: 如何在新应用中使用 @radish/ui？

**A:** 在 `package.json` 中添加依赖：

```json
{
  "dependencies": {
    "@radish/ui": "workspace:*"
  }
}
```

然后运行 `npm install`。

### Q: Ant Design 组件冲突怎么办？

**A:** 统一从 `@radish/ui` 导入，不要直接从 `antd` 导入：

```typescript
// ✅ 正确
import { AntButton, message } from '@radish/ui';

// ❌ 错误
import { Button, message } from 'antd';
```

### Q: 类型错误怎么处理？

**A:** 运行类型检查：

```bash
npm run type-check --workspace=@radish/ui
```

确保所有组件都有完整的类型定义。
