# @radish/ui 组件库开发指南

## 🔧 开发环境配置

### npm Workspaces 工作原理

Radish 项目使用 npm workspaces 管理多个前端包：

```
Radish/
├── node_modules/
│   └── @radish/
│       └── ui -> ../../ui  (符号链接)
├── Frontend/radish.client/
├── Frontend/radish.console/
└── Frontend/ui/
```

**关键点**：
- `@radish/ui` 通过**符号链接**指向 `Frontend/ui/` 目录
- 修改 `Frontend/ui/` 中的代码会**立即反映**到使用它的项目中
- **无需重新安装**或重启开发服务器（Vite HMR 会自动检测）

## 🚀 启动方式

### 方式 1: 从根目录启动（推荐）

```bash
# 从项目根目录
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console

# 或使用统一启动脚本
pwsh ./start.ps1   # Windows
./start.sh         # Linux/macOS
```

**优点**：
- 确保 workspaces 配置正确
- 所有依赖都在根目录的 node_modules
- 符号链接已正确创建

### 方式 2: 从子目录启动

```bash
# 进入子目录
cd Frontend/radish.client
npm run dev

# 或
cd Frontend/radish.console
npm run dev
```

**前提条件**：
- 必须先在根目录运行过 `npm install`
- 符号链接已创建（`node_modules/@radish/ui` 存在）

**优点**：
- 更快速，直接在子目录工作
- 适合只关注单个项目的开发

## 🔄 热更新机制

### Vite 配置

已在 `vite.config.ts` 中配置了符号链接监听：

```typescript
export default defineConfig({
    resolve: {
        // 不保留符号链接，让 Vite 解析到实际文件
        preserveSymlinks: false
    },
    server: {
        watch: {
            // 跟随符号链接
            followSymlinks: true,
            // 不忽略 @radish/ui
            ignored: ['!**/node_modules/@radish/**']
        }
    }
});
```

### 工作流程

1. **修改 UI 组件**
   ```bash
   # 编辑 Frontend/ui/src/components/Button/Button.tsx
   vim Frontend/ui/src/components/Button/Button.tsx
   ```

2. **自动热更新**
   - Vite 检测到 `Frontend/ui/` 中的文件变化
   - 通过符号链接，`radish.client` 和 `radish.console` 自动更新
   - 浏览器自动刷新，无需手动操作

3. **验证更新**
   - 打开 `http://localhost:3000` (client) 或 `http://localhost:3100` (console)
   - 查看组件是否已更新

## 📝 开发工作流

### 场景 1: 开发新组件

```bash
# 1. 在 radish.ui 中创建新组件
mkdir -p Frontend/ui/src/components/Checkbox
touch Frontend/ui/src/components/Checkbox/Checkbox.tsx
touch Frontend/ui/src/components/Checkbox/Checkbox.css

# 2. 编写组件代码
# ... 编辑 Checkbox.tsx 和 Checkbox.css

# 3. 导出组件
# 编辑 Frontend/ui/src/components/index.ts
# export { Checkbox } from './Checkbox/Checkbox';

# 4. 在 client 或 console 中使用
# import { Checkbox } from '@radish/ui';

# 5. 查看效果（自动热更新）
# 浏览器会自动刷新显示新组件
```

### 场景 2: 修改现有组件

```bash
# 1. 编辑组件文件
vim Frontend/ui/src/components/Button/Button.tsx

# 2. 保存文件
# Vite 自动检测变化并热更新

# 3. 在浏览器中查看效果
# 无需手动刷新
```

### 场景 3: 添加新工具函数

```bash
# 1. 创建工具函数文件
vim Frontend/ui/src/utils/array.ts

# 2. 导出函数
# 编辑 Frontend/ui/src/utils/index.ts
# export { sortBy, groupBy } from './array';

# 3. 在项目中使用
# import { sortBy } from '@radish/ui/utils';

# 4. 自动生效（无需重启）
```

## ⚠️ 常见问题

### Q1: 修改 UI 组件后没有自动更新？

**解决方案**：

1. **检查符号链接**
   ```bash
   ls -la node_modules/@radish/ui
   # 应该显示: ui -> ../../ui
   ```

2. **重新安装依赖**
   ```bash
   rm -rf node_modules package-lock.json
   npm install
   ```

3. **重启开发服务器**
   ```bash
   # 停止当前服务器 (Ctrl+C)
   npm run dev --workspace=radish.client
   ```

### Q2: 从子目录启动报错找不到 @radish/ui？

**原因**: 子目录没有 node_modules，或符号链接未创建。

**解决方案**：
```bash
# 回到根目录
cd /mnt/d/Code/Radish

# 重新安装
npm install

# 验证符号链接
ls -la node_modules/@radish/ui

# 再次从子目录启动
cd Frontend/radish.client
npm run dev
```

### Q3: 修改 package.json 后需要重新安装吗？

**是的**。如果修改了以下文件，需要重新运行 `npm install`：

- `Frontend/ui/package.json`
- `Frontend/radish.client/package.json`
- `Frontend/radish.console/package.json`
- 根目录的 `package.json`

```bash
# 从根目录
npm install
```

### Q4: TypeScript 报错找不到 @radish/ui 的类型？

**解决方案**：

1. **重启 TypeScript 服务器**
   - VS Code: `Ctrl+Shift+P` → "TypeScript: Restart TS Server"
   - 或重启 IDE

2. **检查 tsconfig.json**
   ```json
   {
     "compilerOptions": {
       "moduleResolution": "bundler",
       "allowImportingTsExtensions": true
     }
   }
   ```

3. **运行类型检查**
   ```bash
   npm run type-check --workspace=@radish/ui
   ```

## 🎯 最佳实践

### 1. 开发新功能时

```bash
# 推荐工作流
1. 在 radish.ui 中开发组件
2. 在 Frontend/radish.console/src/examples/ 中创建示例
3. 启动 console 查看效果
4. 完成后在 client 中使用
```

### 2. 调试组件时

```bash
# 使用 console 项目作为测试环境
cd Frontend/radish.console
npm run dev

# 在 src/examples/UIComponentsExample.tsx 中测试组件
```

### 3. 提交代码前

```bash
# 1. 运行类型检查
npm run type-check --workspace=@radish/ui

# 2. 运行 Lint
npm run lint --workspace=@radish/ui

# 3. 确保所有项目都能正常启动
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

## 📊 性能优化

### 减少不必要的重新编译

Vite 已配置为只监听必要的文件：

```typescript
watch: {
    followSymlinks: true,
    // 只监听 @radish/ui，忽略其他 node_modules
    ignored: ['!**/node_modules/@radish/**']
}
```

### 使用子路径导入

```typescript
// ✅ 推荐：只导入需要的模块
import { Button } from '@radish/ui/components';
import { useDebounce } from '@radish/ui/hooks';

// ❌ 避免：导入整个包
import { Button, useDebounce } from '@radish/ui';
```

**优点**：
- 更好的 tree-shaking
- 更快的编译速度
- 更小的打包体积

## 🔗 相关文档

- [UI 组件库完整文档](./ui-library.md)
- [快速参考](./quick-reference.md)

## 📝 总结

### 关键点

1. **符号链接机制** - 修改 UI 组件会立即反映到使用它的项目
2. **Vite HMR** - 自动检测变化并热更新，无需手动刷新
3. **灵活启动** - 可以从根目录或子目录启动
4. **类型安全** - 完整的 TypeScript 支持

### 推荐工作流

```bash
# 1. 首次设置（只需一次）
npm install

# 2. 日常开发
npm run dev --workspace=radish.console  # 开发和测试 UI 组件
npm run dev --workspace=radish.client   # 在实际项目中使用

# 3. 修改 UI 组件
# 编辑 Frontend/ui/src/... 文件
# 浏览器自动更新，无需任何操作

# 4. 提交前检查
npm run type-check --workspace=@radish/ui
npm run lint --workspace=@radish/ui
```

---

**更新日期**: 2025-12-13
**适用版本**: @radish/ui 0.1.0
