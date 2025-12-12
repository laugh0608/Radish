# ContextMenu 右键菜单组件

一个功能丰富的 React 右键菜单组件，支持多级菜单、图标、分隔线和禁用状态。

## 特性

- ✨ 支持基础右键菜单
- 🎯 支持多级子菜单
- 🎨 支持自定义图标
- 📏 支持分隔线
- 🚫 支持禁用状态
- 🎪 自动边界检测，防止菜单超出视口
- 💫 优雅的动画效果
- 🎭 毛玻璃背景效果

## 安装

组件已包含在项目中，无需额外安装。

## 基础用法

```tsx
import { ContextMenu } from '@/shared/ui/base/ContextMenu';
import { Icon } from '@/shared/ui/base/Icon';

function MyComponent() {
  return (
    <ContextMenu
      items={[
        {
          id: 'open',
          label: '打开',
          icon: <Icon icon="mdi:folder-open" />,
          onClick: () => console.log('打开')
        },
        {
          id: 'divider-1',
          label: '',
          divider: true
        },
        {
          id: 'delete',
          label: '删除',
          icon: <Icon icon="mdi:delete" />,
          onClick: () => console.log('删除')
        }
      ]}
    >
      <div>右键点击这里</div>
    </ContextMenu>
  );
}
```

## 高级用法

### 多级子菜单

```tsx
<ContextMenu
  items={[
    {
      id: 'new',
      label: '新建',
      icon: <Icon icon="mdi:plus" />,
      children: [
        {
          id: 'new-folder',
          label: '文件夹',
          icon: <Icon icon="mdi:folder" />,
          onClick: () => console.log('新建文件夹')
        },
        {
          id: 'new-file',
          label: '文件',
          icon: <Icon icon="mdi:file" />,
          onClick: () => console.log('新建文件')
        }
      ]
    }
  ]}
>
  <div>右键点击查看子菜单</div>
</ContextMenu>
```

### 禁用菜单项

```tsx
<ContextMenu
  items={[
    {
      id: 'copy',
      label: '复制',
      icon: <Icon icon="mdi:content-copy" />,
      onClick: () => console.log('复制')
    },
    {
      id: 'paste',
      label: '粘贴',
      icon: <Icon icon="mdi:content-paste" />,
      disabled: true, // 禁用该项
      onClick: () => console.log('粘贴')
    }
  ]}
>
  <div>右键点击</div>
</ContextMenu>
```

## API

### ContextMenu Props

| 属性 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| items | `ContextMenuItem[]` | 是 | - | 菜单项列表 |
| children | `ReactNode` | 是 | - | 触发右键菜单的子元素 |
| onClose | `() => void` | 否 | - | 菜单关闭时的回调函数 |

### ContextMenuItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| id | `string` | 是 | - | 菜单项唯一标识 |
| label | `string` | 是 | - | 菜单项文本 |
| icon | `ReactNode` | 否 | - | 菜单项图标 |
| onClick | `() => void` | 否 | - | 点击回调函数 |
| disabled | `boolean` | 否 | `false` | 是否禁用 |
| divider | `boolean` | 否 | `false` | 是否为分隔线 |
| children | `ContextMenuItem[]` | 否 | - | 子菜单项 |

## 样式定制

组件使用 CSS Modules，样式文件位于 `ContextMenu.module.css`。可以通过修改以下 CSS 变量来定制样式：

```css
/* 菜单背景 */
.contextMenu {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(20px) saturate(180%);
}

/* 菜单项悬停颜色 */
.menuItem:hover:not(.disabled) {
  background: rgba(0, 122, 255, 0.1);
}
```

## 注意事项

1. **边界检测**：组件会自动检测菜单是否超出视口，并调整位置
2. **点击外部关闭**：点击菜单外部区域会自动关闭菜单
3. **子菜单**：有子菜单的项点击后会展开/收起子菜单，而不是执行 onClick
4. **分隔线**：设置 `divider: true` 的项会渲染为分隔线，label 可以为空字符串
5. **禁用状态**：禁用的菜单项不可点击，且显示为半透明

## 示例

在 `ComponentShowcase.tsx` 中可以查看完整的使用示例，包括：
- 基础右键菜单
- 带子菜单的右键菜单
- 带禁用项的右键菜单

## License

MIT
