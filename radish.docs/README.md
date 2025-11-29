# Radish 在线文档

基于 VitePress 构建的 Radish 项目文档站点。

## 快速开始

```bash
# 安装依赖（在 radish.docs 目录下）
npm install

# 开发模式（热重载）
# 默认开发地址：http://localhost:3001/docs
npm run docs:dev

# 构建生产版本
npm run docs:build

# 预览构建结果
npm run docs:preview
```

也可以在仓库根目录使用统一的 `--prefix` 方式：

```bash
# 安装依赖（仓库根目录）
npm install --prefix radish.docs

# 开发模式
npm run docs:dev --prefix radish.docs

# 构建生产版本
npm run docs:build --prefix radish.docs
```

## 目录结构

```
radish.docs/
├── .vitepress/
│   ├── config.mts          # VitePress 配置文件
│   └── cache/              # 构建缓存（自动生成，已忽略）
├── docs/                   # 文档源文件目录（Markdown 与图片等资源）
└── package.json
```

## 技术方案

### 文档源文件位置

- **源文件**：位于 `radish.docs/docs` 目录（同时也是在线文档站的内容源）
- **构建输出**：`radish.docs/dist`（默认），由独立 docs 服务或任意静态服务器托管

### VitePress 配置要点

```typescript
export default defineConfig({
  srcDir: './docs',  // 指向工程内 docs 目录
  outDir: './dist',   // 默认构建到当前工程 dist 目录
  base: '/docs/',

  vite: {
    resolve: {
      preserveSymlinks: true
    },
    ssr: {
      noExternal: ['vue', '@vue/server-renderer']
    }
  }
})
```

### 部署与兼容性

文档工程不再依赖符号链接或 junction，任意平台上只要按标准 Node 环境运行以下命令即可：

```bash
npm install
npm run docs:build
```

## 部署说明

### 本地开发

直接运行 `npm run docs:dev` 或 `npm run docs:build` 即可启动开发和构建。

### CI/CD 环境

在 CI/CD 环境中同样执行 `npm install && npm run docs:build` 即可。

### Linux 服务器部署

```bash
# 克隆仓库
git clone <repository-url>
cd Radish/radish.docs

# 安装依赖
npm install

# 构建文档站点
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

# 构建文档站点
npm run docs:build
```

**注意**：如果 Windows 构建失败，请以管理员身份运行 PowerShell/CMD，或使用 WSL。

## 常见问题

### Q: 文档源文件现在存放在哪里？

A: 所有 Markdown 与图片均位于 `radish.docs/docs` 目录，同时根目录 `docs/` 提供跳转入口，方便在 GitHub 上浏览。

### Q: 还需要手动处理符号链接或 junction 吗？

A: 不需要。当前文档工程不依赖符号链接或 junction，直接使用本地目录结构即可。


## 相关文档

- [VitePress 官方文档](https://vitepress.dev/)
- [Gateway 部署指南](docs/DeploymentGuide.md)
- [开发规范](docs/DevelopmentSpecifications.md)
