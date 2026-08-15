# F4-R R3-F02-C 错误与路由边界实现及静态门禁

> 日期：2026-08-15
>
> 状态：代码实现、静态门禁与后续 [Gateway 成组运行态验收](/records/f4-r-r3-f02-grouped-runtime-acceptance-2026-08-15)均已完成，`R3-F02` 已关闭
>
> 依据：[R3-F02 自服务与边界页设计前代码事实与风险拆批审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13)

## 1. 结论

`R3-F02-C` 已关闭 Client / Console 的错误理由与路由归属缺口：

- Client 只有明确的 `/desktop` 与尾随斜杠兼容路径进入历史 WebOS；未知顶层地址进入独立 Not Found，不再由路由 fallback 误开桌面；
- Client 根挂载新增运行时 ErrorBoundary，覆盖路由、懒加载与 Toast 渲染，记录诊断编号但不向用户暴露原始异常；Not Found 与运行时异常均复用 `WebStateSlot`，保持不同语义和恢复动作；
- Console 子路由缺权不再静默跳转，原目标和页面名继续保留，并明确提供当前账号的默认授权入口；
- Console Not Found 区分匿名、非 Console 用户和已授权用户：匿名只提供登录与前台返回，非 Console 用户继续进入身份拒绝，后台搜索只对已授权用户出现；
- React Router `errorElement` 承接路由渲染异常，应用外层 ErrorBoundary 继续承接 Provider / Router 根级异常；两层都提供诊断编号和恢复动作，不用 404 吞掉运行时错误；
- 边界文案、语义 token、PC / Mobile 布局和错误 `alert` 语义均覆盖中英文，没有新增权限键、Route metadata、身份能力或万能状态机。

本批没有修改后端、数据库、migration、依赖或 Pencil，也没有启动服务或浏览器。

## 2. Client 路由与错误边界

- `BrowserEntryKind` 将历史含混的 `root` 拆为 `desktop / not-found`；入口解析继续优先匹配既有 Public / Private / Author 路由，再精确匹配 `/desktop`，最后收口为 Not Found；
- `NotFoundEntry` 只显示当前 pathname，不回显 query / hash，提供 `/discover` 与 `/workbench` 两个安全返回动作；
- `ClientErrorBoundary` 位于主题和语言 Provider 内、路由与 Toast 外，捕获渲染失败后记录脱敏日志与随机诊断编号，提供原位重试、刷新和返回发现页；
- `WebStateSlot` 的 error tone 增加 `role="alert"`，现有 loading、empty、permission 与 auth 语义保持不变。

## 3. Console 状态分型

- `RouteGuard` 的未登录行为保持原有带来源的登录跳转；已登录但缺少目标页面权限时原地显示 403，不再用 `<Navigate>` 掩盖拒绝原因；
- 权限拒绝动作使用 `getDefaultAuthorizedPath(user)`，只进入当前账号已有权限的页面，并保留前台来源返回；
- 顶层非 Console 用户继续由 `canEnterConsole` 进入独立访问拒绝，不伪装成权限缺失或 Not Found；
- Not Found 根据 `canEnterConsole(user)` 裁决动作：匿名不创建 `GlobalSearch`，已授权用户才可打开默认授权页、返回上一页或搜索现有菜单；
- Login、Callback、Authenticated root 与 wildcard 都配置显式 `RouteRuntimeError`；路由异常与应用根异常共用既有 Result 语义和主题 token，但保持独立 owner。

## 4. 验证

- Client：`557 / 557`，TypeScript、ESLint 与 production build 通过；新增明确 desktop / unknown 路由、Not Found、根级错误与恢复动作契约；
- Console：`137 / 137`，strict TypeScript、ESLint 与 production build 通过；新增受限 operator、匿名 / 授权 Not Found、route errorElement 与根级错误契约；
- `npm run validate:baseline:quick`：通过，包含四个前端 workspace 类型与测试、Console 权限、敏感字面量、时间语义、Repo Quality 与身份语义门禁；
- 两端 production build 仍输出仓库既有 chunk-size 提示，没有构建失败或新增错误；
- 文档、changed hygiene 与 `git diff --check` 在提交前复核。

本批按计划未执行 Gateway 页面访问、PC / Mobile 浏览器复核或故障注入。运行态布局、键盘焦点、无横向溢出、真实身份分型和两层异常恢复将在 F02 成组验收中一次完成。

## 5. 停止线

- 保持现有权限常量、Route metadata、身份与 OIDC 契约，不因边界页增加角色能力；
- 不把 Client、Console 和 Auth 错误面合并为跨产品万能状态机；共享只停留在各产品已有的无业务状态语义；
- 不把 `/desktop` 重新设为未知路由 fallback，不让权限拒绝重新退化为自动跳转；
- 不修改 Pencil；本批继续继承 `R1-F01` 和既有 Console 壳层。

## 6. 下一步

`R3-F02-A / B / C` 的代码、静态门禁与 [Gateway 成组运行态验收](/records/f4-r-r3-f02-grouped-runtime-acceptance-2026-08-15)现已全部关闭。运行态发现的 OIDC Consent 参数、StrictMode 回调错误复用和 Profile mobile / Form 挂载问题已在各自 owner 内修正并复验。下一顺位进入 `F4-R Web 主题基线与专题退出门禁审计`。
