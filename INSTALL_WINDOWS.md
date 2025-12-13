# Windows 环境安装说明

## 重要提示

如果你在 **Windows PowerShell** 中开发，需要在 Windows 环境中安装依赖。
如果你在 **WSL** 中开发，需要在 WSL 环境中安装依赖。

**这两个环境的 node_modules 不能混用！**

## Windows PowerShell 安装步骤

```powershell
# 1. 删除 WSL 安装的 node_modules（如果存在）
Remove-Item -Recurse -Force node_modules -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force radish.client\node_modules -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force radish.console\node_modules -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force radish.ui\node_modules -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force radish.docs\node_modules -ErrorAction SilentlyContinue

# 2. 在 Windows 环境中安装依赖
npm install

# 3. 启动项目
npm run dev:frontend
```

## WSL 安装步骤

```bash
# 1. 删除 Windows 安装的 node_modules（如果存在）
rm -rf node_modules radish.*/node_modules

# 2. 在 WSL 环境中安装依赖
npm install

# 3. 启动项目
npm run dev:frontend
```

## 如何选择环境

### 使用 Windows PowerShell（推荐）

**优点**：
- 与 Windows 工具链集成更好
- 性能更好（不需要跨文件系统）
- 更容易调试

**缺点**：
- 需要 Windows 版本的 Node.js

**适合**：
- 主要在 Windows 上开发
- 使用 VS Code 的 Windows 版本
- 需要与其他 Windows 工具集成

### 使用 WSL

**优点**：
- 与 Linux 环境一致
- 更接近生产环境
- 某些 npm 包在 Linux 上更稳定

**缺点**：
- 跨文件系统性能较差（/mnt/d 访问 Windows 文件）
- 调试可能更复杂

**适合**：
- 需要 Linux 特定工具
- 生产环境是 Linux
- 习惯 Linux 命令行

## 当前状态检查

### 检查你在哪个环境

```powershell
# Windows PowerShell
node --version
npm --version
```

```bash
# WSL
node --version
npm --version
```

### 检查 node_modules 是哪个环境安装的

```powershell
# Windows PowerShell
Get-Item node_modules | Select-Object CreationTime
```

```bash
# WSL
stat node_modules | grep Modify
```

## 故障排除

### 问题：找不到 vite 命令

```
'vite' 不是内部或外部命令
```

**原因**：在 Windows 中运行，但 node_modules 是 WSL 安装的（或相反）

**解决**：
1. 删除所有 node_modules
2. 在当前环境重新运行 `npm install`

### 问题：符号链接错误

```
EACCES: permission denied, lstat
```

**原因**：Windows 和 WSL 的符号链接不兼容

**解决**：
1. 删除所有 node_modules
2. 在当前环境重新运行 `npm install`

### 问题：性能很慢

**原因**：在 WSL 中访问 /mnt/d 的 Windows 文件系统

**解决**：
- 方案 1：将项目移到 WSL 文件系统（如 ~/projects/Radish）
- 方案 2：在 Windows PowerShell 中开发

## 推荐配置

### 如果主要在 Windows 开发

```powershell
# 在项目根目录（Windows PowerShell）
npm install
npm run dev:frontend
```

### 如果主要在 WSL 开发

```bash
# 将项目移到 WSL 文件系统
cp -r /mnt/d/Code/Radish ~/projects/
cd ~/projects/Radish

# 安装依赖
npm install

# 启动项目
npm run dev:frontend
```

## 后端开发

后端（.NET）可以在任一环境运行：

```powershell
# Windows PowerShell
dotnet run --project Radish.Api/Radish.Api.csproj
```

```bash
# WSL
dotnet run --project Radish.Api/Radish.Api.csproj
```

但建议前后端在同一环境中运行，以避免网络和文件系统问题。
