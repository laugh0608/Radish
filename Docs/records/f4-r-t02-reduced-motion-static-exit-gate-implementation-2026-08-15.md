# F4-R-T02 reduced-motion 与静态退出门禁实现

> 日期：2026-08-15（Asia/Shanghai）
>
> 状态：T02 已完成；下一顺位进入 `F4-R-T03 Web 四主题成组运行态验收`
>
> 范围：Client / Console 宿主级 `prefers-reduced-motion`、静态契约与前端回归

## 1. 结论

T02 已在两个正式 Web 宿主建立统一 reduced-motion owner：

- Client 在 `theme-tokens.css` 保留 `--theme-transition-standard: 0ms`，并由宿主媒体规则覆盖后加载的页面 CSS、CSS Module、共享组件和 Ant Design 动效；
- Console 在根 `index.css` 建立等价规则，不再只依赖单个 `ClientBackLink` 的局部处理；
- transition / animation 延迟归零，时长压缩为 `0.01ms`，无限迭代收敛为一次，smooth scroll 回退为 `auto`；
- 规则不隐藏加载、进度、反馈或状态元素，静态文字、图标和轮廓继续可见。

采用极短时长而非全局 `animation: none`，既关闭用户可感知的持续或大幅运动，也避免破坏可能依赖 transition / animation 结束事件完成清理的组件生命周期。高优先级声明用于覆盖按路由延迟加载的样式，不逐文件复制 130 余份媒体规则。

## 2. 实现边界

| Owner | 本批结果 |
| --- | --- |
| Client | `theme-tokens.css` 统一约束 token、transition、animation、iteration 与 scroll behavior |
| Console | `index.css` 建立同构宿主规则，覆盖页面 CSS 与共享组件 |
| `@radish/ui` | family-ui `v26.7.3` 固定副本保持原样，继续提供标准 motion token 归零 |
| 页面 / 组件 | 不逐个重写历史 CSS，不删除 spinner、Skeleton、Toast 或进度状态 |
| 产品契约 | 不新增用户设置、数据库字段、接口、权限、依赖或 Pencil 画板 |

## 3. 静态门禁

- 定向主题 / motion / family-ui 契约：`9 / 9`；
- `npm run test --workspace=@radish/ui`：`32 / 32`；
- `npm run test --workspace=radish.client`：`557 / 557`；
- `npm run test --workspace=radish.console`：`138 / 138`；
- `@radish/ui`、Client、Console 的 type-check 与 Lint 均通过；Client、Console production build 均通过，仅保留既有 chunk-size 提示；
- `npm run validate:baseline:quick`：通过，包含 `@radish/http 48 / 48`、四个 workspace 类型检查、前端测试与仓库级守卫。

本批没有启动服务或浏览器。宿主规则的真实生效、静态加载表面、Modal / BottomSheet、Toast、四主题、PC / mobile 与 keyboard focus 统一由 T03 运行态矩阵复核。

## 4. 下一顺位与授权边界

T03 需在当轮重新说明并取得授权后启动 API `5100`、Auth `5200`、Gateway `5000`、Client `3000` 与 Console `3100`。现有种子账号不持有暗夜 / 樱花权益；如需走真实权益链路，应为种子 Admin 创建固定 ID、`AcceptanceTest` 来源的临时 Theme 权益、活动指针与必要操作流水，验收后精确清理并复核数据库完整性，不通过 DOM 或本地存储绕过主题状态机。
