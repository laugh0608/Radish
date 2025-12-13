# npm Workspaces 使用说明

本项目使用 npm workspaces 管理多个前端项目（radish.client、radish.console、radish.ui）。

## 问题说明

在 Windows 上，由于 npm workspaces 的实现方式，**不能直接在子项目目录中运行 npm 命令**。

### 错误示例

```powershell
# ❌ 错误：在子项目目录运行
PS D:\Code\Radish\radish.client> npm run dev
# 错误：'vite' 不是内部或外部命令
```

### 原因

npm workspaces 将所有依赖安装在根目录的 `node_modules` 中，子项目通过符号链接访问。但在 Windows PowerShell 中，当你进入子项目目录运行命令时，npm 无法正确解析根目录的依赖。

## 解决方案

### 方案 1：从根目录运行（推荐）

始终在项目根目录运行命令：

```powershell
# 在项目根目录 D:\Code\Radish 运行
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
npm run dev --workspace=@radish/ui
```

### 方案 2：使用快捷脚本

根目录的 `package.json` 已配置快捷脚本：

```powershell
# 在项目根目录运行
npm run dev:frontend   # 启动 radish.client
npm run dev:console    # 启动 radish.console
npm run dev:docs       # 启动文档站
```

### 方案 3：创建符号链接（高级）

如果你确实需要在子项目目录中运行命令，可以创建符号链接：

```powershell
# 以管理员身份运行 PowerShell
pwsh -ExecutionPolicy Bypass -File ./setup-workspace-links.ps1
```

**注意**：
- 需要管理员权限
- 每次删除 node_modules 后需要重新运行
- 不推荐，建议使用方案 1 或 2

## 常用命令

### 安装依赖

```powershell
# 在根目录安装所有依赖
npm install

# 为特定项目添加依赖
npm install react-router-dom --workspace=radish.client
npm install lodash --workspace=@radish/ui
```

### 启动开发服务器

```powershell
# 方式 1：使用 workspace 参数
npm run dev --workspace=radish.client

# 方式 2：使用快捷脚本
npm run dev:frontend

# 方式 3：使用启动脚本
pwsh ./start.ps1  # 选择 Frontend 选项
```

### 构建项目

```powershell
# 构建特定项目
npm run build --workspace=radish.client

# 构建所有项目
npm run build --workspaces
```

### 运行测试

```powershell
# 运行特定项目的测试
npm run test --workspace=radish.client

# 运行所有项目的测试
npm run test --workspaces
```

## 最佳实践

1. **始终在根目录管理依赖**
   - 运行 `npm install` 在根目录
   - 不要在子项目中运行 `npm install`

2. **使用 workspace 命令**
   - 使用 `--workspace=<name>` 参数指定项目
   - 或使用根目录配置的快捷脚本

3. **避免手动进入子目录**
   - 不要 `cd radish.client && npm run dev`
   - 使用 `npm run dev --workspace=radish.client`

4. **使用启动脚本**
   - `pwsh ./start.ps1` 提供交互式菜单
   - 自动处理所有路径和依赖问题

## 故障排除

### 问题：找不到命令（如 vite）

```
'vite' 不是内部或外部命令
```

**解决**：回到根目录运行命令
```powershell
cd D:\Code\Radish
npm run dev --workspace=radish.client
```

### 问题：依赖未安装

```
Cannot find module '@radish/ui'
```

**解决**：在根目录重新安装依赖
```powershell
cd D:\Code\Radish
rm -rf node_modules
npm install
```

### 问题：符号链接权限错误

```
EACCES: permission denied
```

**解决**：
1. 删除所有 node_modules
2. 在根目录重新安装
```powershell
rm -rf node_modules radish.*/node_modules
npm install
```

## 参考资料

- [npm workspaces 官方文档](https://docs.npmjs.com/cli/v10/using-npm/workspaces)
- [项目 README](./README.md)
- [前端开发文档](./radish.docs/docs/FrontendDesign.md)
