# Radish 在线文档

基于 VitePress 构建的 Radish 项目文档站点。

## 快速开始

```bash
# 安装依赖
npm install

# 开发模式（热重载）
npm run docs:dev

# 构建生产版本
npm run docs:build

# 预览构建结果
npm run docs:preview
```

## 目录结构

```
radish.docs/
├── .vitepress/
│   ├── config.mts          # VitePress 配置文件
│   └── cache/              # 构建缓存（自动生成，已忽略）
├── scripts/
│   └── setup-docs-link.js  # 跨平台文档链接设置脚本
├── docs -> ../docs         # 符号链接，指向仓库根目录的 docs/（自动生成，已忽略）
└── package.json
```

## 技术方案

### 文档源文件位置

- **源文件**：位于仓库根目录的 `/docs` 目录
- **符号链接**：`radish.docs/docs` → `../docs`（自动创建，不提交到 Git）
- **构建输出**：`Radish.Gateway/DocsSite`

### 跨平台兼容性

为了解决符号链接在不同操作系统上的兼容性问题，我们采用了以下方案：

1. **自动化脚本**：`scripts/setup-docs-link.js` 在每次构建/开发前自动运行
2. **平台检测**：
   - **Linux/macOS**：创建标准符号链接
   - **Windows**：尝试创建 junction（目录连接），无需管理员权限
3. **构建集成**：通过 `prebuild` 和 `predev` npm 钩子自动触发

### VitePress 配置要点

```typescript
export default defineConfig({
  srcDir: './docs',  // 指向符号链接
  outDir: '../Radish.Gateway/DocsSite',
  base: '/docs/',

  vite: {
    resolve: {
      preserveSymlinks: true  // 支持符号链接
    },
    ssr: {
      noExternal: ['vue', '@vue/server-renderer']  // SSR 构建优化
    }
  }
})
```

## 部署说明

### 本地开发

直接运行 `npm run docs:dev` 或 `npm run docs:build`，脚本会自动处理符号链接。

### CI/CD 环境

构建脚本会自动在任何环境中创建必要的链接，无需额外配置。

### Linux 服务器部署

```bash
# 克隆仓库
git clone <repository-url>
cd Radish/radish.docs

# 安装依赖
npm install

# 构建（脚本自动创建符号链接）
npm run docs:build

# 输出目录: ../Radish.Gateway/DocsSite
```

### Windows 部署

```powershell
# 克隆仓库
git clone <repository-url>
cd Radish\radish.docs

# 安装依赖
npm install

# 构建（脚本会自动创建 junction）
npm run docs:build
```

**注意**：如果 Windows 构建失败，请以管理员身份运行 PowerShell/CMD，或使用 WSL。

## 常见问题

### Q: 为什么不直接把文档放在 radish.docs 内部？

A: 为了保持文档在仓库根目录，便于其他开发者查看和编辑，同时也符合项目规范。

### Q: 符号链接会被提交到 Git 吗？

A: 不会。`radish.docs/docs` 已在 `.gitignore` 中忽略，每次构建时自动创建。

### Q: 如果符号链接创建失败怎么办？

A: 脚本会输出警告信息。在 Windows 上，建议以管理员身份运行或使用 WSL。

## 相关文档

- [VitePress 官方文档](https://vitepress.dev/)
- [Gateway 部署指南](../docs/DeploymentGuide.md)
- [开发规范](../docs/DevelopmentSpecifications.md)
