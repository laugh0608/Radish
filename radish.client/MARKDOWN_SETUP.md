# Markdown 渲染功能 - 依赖安装指南

## 📦 需要安装的 npm 包

为了使论坛应用支持 Markdown 渲染和代码高亮功能，需要安装以下依赖包。

### 在 Windows PowerShell 或 CMD 中执行

```bash
# 进入项目根目录
cd D:\Code\Radish

# 安装 Markdown 相关依赖
npm install --workspace=radish.client react-markdown remark-gfm rehype-highlight highlight.js

# 安装 TypeScript 类型定义（开发依赖）
npm install --workspace=radish.client @types/react-markdown -D
```

### 或者使用单个命令

```bash
npm install --workspace=radish.client react-markdown remark-gfm rehype-highlight highlight.js && npm install --workspace=radish.client @types/react-markdown -D
```

---

## 📋 包说明

| 包名 | 版本 | 大小 | 说明 |
|------|------|------|------|
| `react-markdown` | ^9.0.0 | ~86KB | React Markdown 渲染库 |
| `remark-gfm` | ^4.0.0 | ~15KB | GitHub Flavored Markdown 支持（表格、删除线、任务列表） |
| `rehype-highlight` | ^7.0.0 | ~8KB | 代码高亮插件（基于 highlight.js） |
| `highlight.js` | ^11.9.0 | ~500KB | 代码高亮库（支持 190+ 编程语言） |
| `@types/react-markdown` | ^8.0.0 | - | TypeScript 类型定义（开发依赖） |

**总大小**：约 610KB（gzipped 后约 180KB）

---

## ✅ 安装验证

安装完成后，检查 `radish.client/package.json` 文件，确认以下依赖已添加：

```json
{
  "dependencies": {
    "react-markdown": "^9.0.1",
    "remark-gfm": "^4.0.0",
    "rehype-highlight": "^7.0.0",
    "highlight.js": "^11.9.0"
  },
  "devDependencies": {
    "@types/react-markdown": "^8.0.7"
  }
}
```

---

## 🚀 测试 Markdown 渲染

安装完成后，启动开发服务器测试：

```bash
# 启动前端开发服务器
npm run dev --workspace=radish.client
```

然后访问：
- `http://localhost:3000` - WebOS 桌面
- 双击"论坛"图标
- 查看帖子详情和评论，确认 Markdown 正确渲染

---

## 🎨 支持的 Markdown 语法

### 基础语法
- **标题**：`# H1` 至 `###### H6`
- **加粗**：`**bold**` 或 `__bold__`
- **斜体**：`*italic*` 或 `_italic_`
- **删除线**：`~~strikethrough~~`
- **链接**：`[text](url)`
- **图片**：`![alt](url)`
- **引用**：`> quote`
- **列表**：`-` 或 `1.`
- **任务列表**：`- [ ]` 和 `- [x]`
- **水平线**：`---`

### 代码
- **行内代码**：`` `code` ``
- **代码块**：
  ````markdown
  ```javascript
  console.log('Hello World');
  ```
  ````

### 表格
```markdown
| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |
```

### 代码高亮支持的语言
- JavaScript/TypeScript
- Python
- Java
- C#
- Go
- Rust
- SQL
- HTML/CSS
- JSON/YAML
- Bash/Shell
- 等 190+ 种语言

---

## 📝 已修改的文件

### 新增文件
1. `src/shared/ui/MarkdownRenderer/MarkdownRenderer.tsx` - Markdown 渲染器组件
2. `src/shared/ui/MarkdownRenderer/MarkdownRenderer.module.css` - 样式文件
3. `src/shared/ui/MarkdownRenderer/index.ts` - 导出文件

### 修改文件
1. `src/apps/forum/components/PostDetail.tsx` - 使用 MarkdownRenderer 渲染帖子内容
2. `src/apps/forum/components/CommentNode.tsx` - 使用 MarkdownRenderer 渲染评论内容
3. `src/main.tsx` - 引入代码高亮样式 `highlight.js/styles/github-dark.css`

---

## 🔧 故障排除

### 问题 1：npm install 失败
```bash
# 删除 node_modules 和 lockfile，重新安装
rm -rf node_modules package-lock.json
npm install
```

### 问题 2：TypeScript 报错找不到模块
```bash
# 重启 TypeScript 服务器
# VS Code: Ctrl+Shift+P -> TypeScript: Restart TS Server
```

### 问题 3：代码高亮样式不生效
- 检查 `src/main.tsx` 中是否正确引入了 `highlight.js/styles/github-dark.css`
- 尝试清除浏览器缓存

### 问题 4：Markdown 渲染为纯文本
- 检查是否正确安装了所有依赖包
- 检查浏览器控制台是否有错误信息
- 确认 PostDetail 和 CommentNode 组件已正确引入 MarkdownRenderer

---

## 📚 相关文档

- [react-markdown 官方文档](https://github.com/remarkjs/react-markdown)
- [remark-gfm 文档](https://github.com/remarkjs/remark-gfm)
- [highlight.js 官方网站](https://highlightjs.org/)
- [GitHub Flavored Markdown 规范](https://github.github.com/gfm/)

---

**创建时间**：2025.12.15
**作者**：Claude Code
**状态**：✅ 代码已完成，等待安装依赖并测试
