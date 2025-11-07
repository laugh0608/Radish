# Repository Guidelines

## 适用范围
- 本文件适用于 `angular/` 子项目（管理端 UI）。

## 项目结构与组织
- 入口与全局：`src/main.ts`，样式 `src/styles.scss`。
- 业务与组件：`src/app/`（示例：`home/`、`book/`）。
- 静态资源：`src/assets/`；环境配置：`src/environments/`（`environment.ts`、`environment.prod.ts`）。
- 支持文件：`tsconfig.*.json`、`web.config`、`start.ps1`。

## 开发、构建与测试命令
- 安装依赖：`yarn install --frozen-lockfile` 或 `npm ci`。
- 本地开发：`yarn start` 或 `npx ng serve --port 4200`。
- 生产构建：`yarn build` 或 `npx ng build --configuration production`。
- 单元测试：`yarn test` 或 `npx ng test --watch=false --code-coverage`。
- 代码检查：`yarn lint`、`yarn lint:fix`、`yarn format:check`、`yarn format`。
- 端到端（若启用）：`npx ng e2e`。Windows 可用 `start.ps1` 启动。

## 代码风格与命名约定
- 缩进与格式：TypeScript/HTML 2 空格；启用严格类型；优先 SCSS。
- 文件命名：`kebab-case`（例如 `book.component.ts`、`home.component.html`）。
- 类型命名：类/组件/服务用 PascalCase；变量/参数用 camelCase；选择器前缀 `app-`。
- 质量检查：如已配置，运行 `yarn lint` 或 `npx eslint . --ext .ts,.html`；格式化 `npx prettier -w`。

## 测试指南
- 框架：Jasmine + Karma（Angular CLI 默认），断言使用 Jasmine。
- 组织：与功能同名目录，文件以 `.spec.ts` 结尾；例如 `book.component.spec.ts`。
- 覆盖率：关键模块建议 ≥ 70%；服务、管道与复杂组件需添加用例；HTTP 交互通过拦截器/Mock 服务隔离。

## 提交与合并请求
- 提交信息遵循 Conventional Commits：`feat|fix|docs|refactor|test|chore: …`。
- PR 需说明动机、变更点、验证步骤；UI 变更请附截图/录屏；关联 Issue（如 `Closes #123`）。
- 若涉及后端 API 变更，请同步更新 `src/environments/` 与相关服务调用。

## 配置与安全
- 不要硬编码敏感信息；使用环境文件与运行时注入配置。
- 部署到 IIS/静态站点时参考 `web.config`；如需代理，请在本地开发配置相应的代理文件（如 `proxy.conf.json`）。
