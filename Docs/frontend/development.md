# 前端 workspace 开发指南

本文描述当前 Radish 前端工作区的真实结构、推荐启动方式与最小验证路径。

## 1. 当前 workspace 结构

Radish 使用 npm workspaces 管理四个前端包：

```text
Frontend/
├── radish.http/    # 统一 HTTP 客户端
├── radish.client/  # 纯 Web 默认入口、/workbench 功能地图与 /desktop 历史工作台保留入口
├── radish.console/ # 管理后台
└── radish.ui/      # 共享 UI 组件与样式
```

职责边界：

- `@radish/http`：统一 API 客户端、认证续期、请求类型
- `@radish/ui`：共享组件、交互反馈、展示辅助能力
- `radish.client`：面向普通用户的纯 Web 前台；普通浏览器根路径 `/` 当前进入 `/discover`，公共头部“工作台”进入 `/workbench` 功能地图，WebOS 工作台能力保留在 `/desktop`
- `radish.console`：面向后台治理的独立前端

## 2. 依赖与链接机制

- 仓库根目录的 [`package.json`](</D:/Code/Radish/package.json>) 维护 workspaces 列表
- 首次拉仓库或变更前端依赖后，需要在根目录执行一次 `npm install`
- `@radish/http` 与 `@radish/ui` 通过 workspace 直接链接，不需要单独发布或单独安装

## 3. 推荐启动方式

### 从根目录启动

```bash
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

这是默认推荐方式，因为：

- workspaces 解析路径最稳定
- 根目录脚本统一
- 更容易和其他验证命令配合

### 从子目录启动

```bash
cd Frontend/radish.client
npm run dev

cd Frontend/radish.console
npm run dev
```

前提是你已经在仓库根目录执行过 `npm install`。

## 4. 热更新与共享包开发

- 修改 `Frontend/radish.ui/src/` 后，`radish.client` 与 `radish.console` 应能直接感知变化
- 修改 `Frontend/radish.http/src/` 后，依赖它的前端应用也应随之更新
- 如果热更新异常，优先检查：
  - `node_modules` 是否完整
  - 当前开发服务器是否从正确 workspace 启动
  - `vite.config.ts` 是否仍保留共享包监听配置

## 5. UI 设计源协作

正式 Web 的跨页面视觉改造先看设计源，再进代码：

- 共享 header、按钮、pill、卡片、状态槽和移动 tab 样板以 [Web UI 共享基座设计说明](./web-ui-foundation-design.md) 与 `Docs/frontend/design-sources/web-ui-foundation.pen` 为准。
- 公开浏览、公开详情、公开集合页和 mobile 公开单列以 [公开 Web 统一体验设计说明](./public-web-unified-experience-design.md) 与 `public-web-unified-experience.pen` 为准。
- 私域首页、资产 / 订单 / 背包、作者工作台、编辑器 / 版本回看和 mobile 私域单列以 [私域与作者态 Web 工作流设计说明](./private-web-workflows-design.md) 与 `private-web-workflows.pen` 为准。
- Console 治理和后台工作台继续以 [Console 样式与 Token 使用说明](./console-style-guide.md)、[Console 治理工作台设计端点](./console-governance-workbench-design.md) 和 `console-governance-workbench.pen` 为准。

Pencil 操作注意：

- `.pen` 只能通过 Pencil 修改，不用普通文本工具编辑。
- Pencil 写入依赖当前活动窗口；修改前确认目标 `.pen` 已在 Pencil 当前窗口打开。
- 切换 `.pen` 前必须在 Pencil 内手动保存；否则可能丢失更改或把后续写入落到上一活动文件。
- MCP `filePath` 只能辅助读取、截图和布局检查，不能替代活动窗口确认和手动保存。

## 6. 常用验证命令

```bash
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
npm run type-check --workspace=radish.client
npm run type-check --workspace=radish.console

npm run build --workspace=radish.client
npm run build --workspace=radish.console
```

如果你只改共享包，至少应补：

```bash
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
```

## 7. 常见协作约束

- 不要在业务模块里自定义平行的 HTTP 客户端，统一走 `@radish/http`
- 不要把共享 UI 再拆成第二套“通用组件库”，统一复用 `@radish/ui`
- 修改共享包后，优先同时检查 `radish.client` 和 `radish.console` 的使用面
- 前端运行时配置统一通过 `env.ts` 或运行时配置入口读取，不直接散落读取 `import.meta.env`

## 8. 常见问题

### 修改 `@radish/ui` 或 `@radish/http` 后没有生效

优先检查：

1. 是否已经在根目录执行过 `npm install`
2. 是否从正确 workspace 启动了开发服务器
3. 是否需要重启当前 Vite 进程

### TypeScript 提示找不到 workspace 包

优先执行：

```bash
npm install
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
```

### 改共享包后不知道该补哪些验证

最小建议：

- 改 `@radish/http`：补 `@radish/http` 自身 type-check，并至少打开一个真实 API 调用页面
- 改 `@radish/ui`：补 `@radish/ui` 自身 type-check，并同时查看 client / console 的一个使用页面

## 相关文档

- [前端设计](./design.md)
- [Web UI 共享基座设计说明](./web-ui-foundation-design.md)
- [公开 Web 统一体验设计说明](./public-web-unified-experience-design.md)
- [私域与作者态 Web 工作流设计说明](./private-web-workflows-design.md)
- [前端 API 客户端使用指南](./api-client.md)
- [@radish/http 文档](./http-client.md)
- [UI 组件库概览](./ui-library.md)
