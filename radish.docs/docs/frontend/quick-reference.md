# @radish/ui 快速参考

## 📦 安装和导入

```typescript
// 组件
import { Button, Input, Select, Modal } from '@radish/ui';

// Hooks
import { useDebounce, useLocalStorage, useToggle, useClickOutside } from '@radish/ui/hooks';

// 工具函数
import { formatDate, isEmail, truncate } from '@radish/ui/utils';

// 类型
import type { ApiResponse, PaginationParams } from '@radish/ui/types';

// API 分页模型（后端 PagedResponse）
import type { PagedResponse } from '@radish/ui';
```

## 🎨 组件速查

### Button

```tsx
<Button variant="primary" size="medium" onClick={handleClick}>
  点击我
</Button>
```

**变体**: primary | secondary | danger
**尺寸**: small | medium | large

### Input

```tsx
<Input
  label="用户名"
  placeholder="请输入"
  error="错误提示"
  helperText="帮助文本"
  required
  fullWidth
/>
```

### Select

```tsx
<Select
  label="城市"
  placeholder="请选择"
  options={[
    { value: '1', label: '北京' },
    { value: '2', label: '上海' }
  ]}
  value={value}
  onChange={(e) => setValue(e.target.value)}
/>
```

### Modal

```tsx
<Modal
  isOpen={isOpen}
  onClose={handleClose}
  title="标题"
  size="medium"
  footer={<Button>确定</Button>}
>
  内容
</Modal>
```

**尺寸**: small | medium | large

## 🪝 Hooks 速查

### useDebounce

```tsx
const debouncedValue = useDebounce(value, 500);
```

### useLocalStorage

```tsx
const [value, setValue] = useLocalStorage('key', initialValue);
```

### useToggle

```tsx
const [isOpen, toggle, setTrue, setFalse] = useToggle(false);
```

### useClickOutside

```tsx
const ref = useRef(null);
useClickOutside(ref, () => console.log('clicked outside'));
```

## 🛠️ 工具函数速查

### 日期和文件

```tsx
formatDate(new Date())                    // "2025-12-13 16:00:00"
formatDate(new Date(), 'YYYY/MM/DD')      // "2025/12/13"
formatFileSize(1024)                      // "1.00 KB"
```

### 验证

```tsx
isEmail('test@example.com')               // true
isPhone('13800138000')                    // true
isUrl('https://example.com')              // true
isIdCard('110101199001011234')            // true
getPasswordStrength('Abc123!@#')          // 2 (0=弱, 1=中, 2=强)
```

### 字符串

```tsx
truncate('很长的文本', 10)                 // "很长的文本..."
capitalize('hello')                       // "Hello"
camelToKebab('userName')                  // "user-name"
kebabToCamel('user-name')                 // "userName"
randomString(8)                           // "aB3xY9zQ"
```

## 📘 类型速查

### ApiResponse

```typescript
interface ApiResponse<T = unknown> {
  isSuccess: boolean;
  statusCode: number;
  messageInfo: string;
  messageInfoDev?: string;
  responseData?: T;
  code?: string;
  messageKey?: string;
}
```

### PaginationParams

```typescript
interface PaginationParams {
  page: number;
  pageSize: number;
}
```

### PagedResponse

```typescript
interface PagedResponse<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}
```

## 💡 常用模式

### 表单验证

```tsx
const [email, setEmail] = useState('');
const [error, setError] = useState('');

const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
  const value = e.target.value;
  setEmail(value);
  setError(value && !isEmail(value) ? '邮箱格式不正确' : '');
};

<Input
  label="邮箱"
  value={email}
  onChange={handleChange}
  error={error}
/>
```

### 搜索防抖

```tsx
const [searchTerm, setSearchTerm] = useState('');
const debouncedSearch = useDebounce(searchTerm, 500);

useEffect(() => {
  if (debouncedSearch) {
    // 执行搜索
    fetchResults(debouncedSearch);
  }
}, [debouncedSearch]);
```

### 模态框管理

```tsx
const [isOpen, toggle, open, close] = useToggle(false);

<Button onClick={open}>打开</Button>
<Modal isOpen={isOpen} onClose={close} title="标题">
  内容
</Modal>
```

### 持久化状态

```tsx
const [theme, setTheme] = useLocalStorage('theme', 'light');

<Select
  options={[
    { value: 'light', label: '亮色' },
    { value: 'dark', label: '暗色' }
  ]}
  value={theme}
  onChange={(e) => setTheme(e.target.value)}
/>
```

## 📊 统计

- **组件**: 4 个
- **Hooks**: 4 个
- **工具函数**: 12 个
- **类型定义**: 3 个
- **代码行数**: ~550 行
- **文件数量**: 21 个

## 🔗 相关文档

- [README.md](./README.md) - 完整文档
- [COMPONENTS_SUMMARY.md](./COMPONENTS_SUMMARY.md) - 组件库总结
- [UIComponentsExample.tsx](../radish.console/src/examples/UIComponentsExample.tsx) - 完整示例

---

**版本**: 0.1.0
**更新日期**: 2025-12-13
