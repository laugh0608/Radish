# F4-R Web 主题基线与专题退出门禁审计

> 日期：2026-08-15（Asia/Shanghai）
>
> 状态：代码事实、历史证据与退出缺口已审计；[T01](/records/f4-r-t01-web-theme-semantic-baseline-implementation-2026-08-15)与 [T02](/records/f4-r-t02-reduced-motion-static-exit-gate-implementation-2026-08-15)已完成，T03 待关闭，F4-R 尚未关闭
>
> 范围：`radish.client` 四主题、`@radish/ui` 共享主题、Console Workbench、可访问性与 reduced-motion；Flutter 只确定后续入口，本批不实现 Dart 映射

## 1. 结论

正式 Web 的六个 R1、四个 R2 与六组 R3 页面族已经形成设计、实现和运行态闭环，但 F4-R 仍有两个代码缺口和一个证据缺口，不能直接进入 Flutter：

1. `guofeng` 已确认的灰玉品牌仍未进入 Client 运行时，CSS、Ant Design 宿主配置和测试继续固定旧胭脂；
2. 共享 token 只会把标准动效变量归零，大量历史组件仍使用硬编码时长，Client 与 Console 没有宿主级 reduced-motion 兜底；
3. 2026-07-14 的 F2 验收曾真实覆盖四主题权益旅程，但早于本轮页面族重构；近期 R1 / R2 / R3 运行态主要覆盖 `default / guofeng`，不能替代 F4-R 退出前的四主题代表复验。

因此下一顺位固定为：

1. `F4-R-T01 Web 主题语义基线`；
2. `F4-R-T02 reduced-motion 与静态退出门禁`；
3. `F4-R-T03 Web 四主题成组运行态验收`；
4. T03 关闭后，再进入 Flutter Dart 语义映射和高价值移动路径审计。

本审计没有修改运行时代码、主题权益、数据库、Pencil 或依赖，也没有启动服务或浏览器。

> 后续进度：`2026-08-15` 已按本审计第 5、6 节完成 T01 / T02；本页第 3 节继续保留审计时点的代码事实，不改写历史输入。当前下一顺位为 T03。

## 2. 已成立的基线

| 检查面 | 当前事实 | 裁决 |
| --- | --- | --- |
| family-ui 版本 | CSS / JSON 副本固定 `v26.7.3`，亮暗语义包含 `text-on-brand`、brand / action / state 与 motion token | 通过；副本继续保持上游原样，不在 Radish 收口批内修改 |
| 四主题身份 | `default / guofeng / theme-dark-night / theme-sakura` 注册表、内建 / 权益边界、持久化、失效回退和 `data-theme / data-rd-theme` 派生均有测试 | 通过；不改变 ID、权益或 Store 状态机 |
| 品牌 / 操作前景 | Client 已分别映射 `--theme-text-on-brand` 与 `--theme-text-on-accent`，四主题均显式定义对应 `--rd-*` 前景 | 通过；代码迁移期间 `text-on-action` 继续映射 `--rd-text-on-accent` |
| Console | Console 固定 Workbench light Profile，`--console-brand-primary` 追溯到 `--rd-action-primary`，共享 Ant Design 默认也使用 action 与统一状态语义 | 通过；Console 不跟随商城主题 |
| 新共享样式颜色 | 从 `v26.7.3` 基线提交到当前 HEAD，Client 新增共享样式未新增 raw color；`@radish/ui` 命中的新增值均是语义变量 fallback | 当前通过；不为退出门禁建立大规模历史颜色清理专题 |
| Pencil | `R1-F01` 和 `R1-P01` 已固定 brand / action / 两类前景与四主题变量；本轮没有新结构、壳层或响应式模型 | 继承成立；T01 / T02 不修改 `.pen` |

## 3. 必须关闭的代码缺口

### 3.1 `guofeng` 灰玉尚未成为运行时真相

已确认目标为：

| 语义 | 目标值 | 当前 Client 运行时 |
| --- | --- | --- |
| brand | `#5d6c57` | `#b24057` |
| brand hover | `#6e736d` | `#cd5076` |
| brand soft | `#e2e6de` | `rgba(178, 64, 87, 0.12)` |
| text on brand | `#fffdf8` | `#fffdf8` |
| action | `#435c74` | `#435c74` |

此外还有三项同源偏离：

- `--theme-brand-hover` 没有消费四主题已经定义的 `--rd-brand-hover`，而是在 L2 用 `color-mix` 重新计算，因此显式悬停值并非实际 owner；
- `@radish/ui` 导出的 `radishColors.brand` 仍固定旧胭脂，与共享 family-ui L1 灰玉基线不一致；该值当前不驱动共享 Ant Design，但仍是公开源码 API 和测试契约；
- Client `themeDefinitions` 在 `default / guofeng / theme-sakura` 中把 Ant Design `colorPrimary` 设为品牌色，而既有规范与 `R1-F01` 已明确表单、Workbench 和常规任务主操作使用 action。暗夜主题因 brand 与 action 同值暂未暴露该分叉。

Client 样式中现有 `var(--theme-brand-primary)` 使用约 `692` 次，而 `var(--theme-action-primary)` 约 `11` 次。这个数量证明全局灰玉会产生广泛可见影响，也证明不能在 T01 机械把所有 brand 引用替换成 action；T01 只治理共享 owner 与 Ant Design 操作边界，页面角色由代表路径复核，发现具体误用再回到对应 owner 精确修正。

### 3.2 reduced-motion 只覆盖标准变量，没有覆盖历史硬编码动效

静态扫描结果：

| 范围 | 含 `transition / animation` 的 CSS 文件 | 自带 `prefers-reduced-motion` 的 CSS 文件 |
| --- | ---: | ---: |
| `@radish/ui` | 24 | 1（上游 token） |
| Client | 102 | 8 |
| Console | 4 | 1 |

其中既有普通 hover 过渡，也包含 Modal / BottomSheet 入场、Toast、Skeleton、LevelUp、spinner 和持续脉冲。仅把 `--rd-motion-*` 与 `--theme-transition-standard` 归零，无法约束硬编码 `0.2s / 0.3s / infinite`。

治理 owner 应放在两个宿主，而不是修改必须与上游一致的 `family-ui-tokens.css`：

- Client 在 `theme-tokens.css` 的 reduce media query 中统一关闭非必要 transition、animation 与平滑滚动；
- Console 在 `index.css` 建立等价规则；
- 共享组件继续由宿主规则覆盖，不为 24 个组件逐文件复制同一 media query；
- 动效关闭后仍必须保留文本、图标、进度或加载轮廓，不能让状态只靠动画表达。

## 4. 证据可继承范围与缺口

### 4.1 可以继承

- [F2 主题系统专题验收](/records/f2-theme-system-stage-acceptance-2026-07-14)证明四主题注册、权益激活 / 同类切换 / 停用、刷新、跨标签同步和 `/desktop` 兼容链路曾真实通过；
- 六个 R1、四个 R2 与六组 R3 的记录证明当前正式页面结构、PC / mobile 重排、双语和代表主题没有阻断；
- 主题、共享 UI 和 Console 现有定向测试全部通过；本次审计运行 `theme.test.ts + familyThemeContract.test.ts`，结果 `8 / 8`。

### 4.2 不能继承为最终退出证据

- F2 四主题验收早于 F4-R 页面族重构；
- 近期运行态记录中的权益主题通常因种子账号未持有而只由静态契约覆盖；
- reduced-motion 只有局部页面规则和 token 证据，没有 Client / Console 宿主级行为复核；
- keyboard focus 在近期关键页面已局部通过，但没有与最终四主题代表矩阵一起复验。

因此 T03 必须重新覆盖当前代码，而不是把历史记录简单拼接为“已完成”。

## 5. T01：Web 主题语义基线

### 5.1 实现范围

1. Client `guofeng` 显式改为 `#5d6c57 / #6e736d / #e2e6de / #fffdf8`；保持纸色、墨色、action、状态色与其余主题不变；
2. `--theme-brand-hover` 直接映射 `--rd-brand-hover`，让四主题显式 L1 值成为唯一 owner；
3. `@radish/ui` 的 `radishColors.brand` 改为共享灰玉基线，`radishColors.primary` 继续保持 action；
4. Client 四主题 Ant Design `colorPrimary` 对齐各主题 action：`default / guofeng = #435c74`、`theme-dark-night = #8bb9ca`、`theme-sakura = #596f88`；必要的 hover / link 也从同一 action 家族配置，不把品牌色重新作为常规操作色；
5. 更新错误固定旧值的测试与 `@radish/ui` README 示例，增加四主题 brand / action / 前景 / 状态映射契约。

### 5.2 停止线

- 不修改 family-ui CSS / JSON 固定副本；
- 不改变主题 ID、默认主题、权益、持久化或服务端契约；
- 不机械替换页面中的 `--theme-brand-primary`，不改状态色和其他主题身份；
- 不修改 Pencil；若真实复核暴露共享视觉结构冲突，再单独裁决，不在 token 批顺手重画。

## 6. T02：reduced-motion 与静态退出门禁

### 6.1 实现范围

1. 在 Client / Console 宿主样式加入一致的 `prefers-reduced-motion: reduce` 规则；
2. 保留现有 motion token 归零，并统一限制硬编码 transition、animation、迭代次数与 smooth scroll；
3. 加入静态契约，守卫两个宿主都存在 reduce 规则、family-ui 固定副本未被改写、四主题仍完整；
4. 执行 `@radish/ui`、Client、Console 测试、type-check、Lint、production build 与 `validate:baseline:quick`。

### 6.2 停止线

- 不逐个重写 130 个历史 CSS 文件；
- 不删除加载、进度或反馈状态，只关闭非必要运动；
- 不把 reduced-motion 变成用户设置或数据库字段，继续服从操作系统媒体偏好；
- 不扩入既有页面布局、功能或文案重构。

## 7. T03：Web 四主题成组运行态验收

T01 / T02 静态门禁通过后，按当轮授权启动 Gateway、API、Auth、Client 与 Console，再使用 Gateway 入口验收：

- 视图：PC `1920 × 1080` 与 Mobile `390 × 844`；工具不能设置 DPR 时如实记录；
- 代表路径：Public `/discover`、Private `/messages` 或 `/workbench`、Author `/docs/mine`、Console `/console/` 与兼容入口 `/desktop`；
- 主题：`default / guofeng / theme-dark-night / theme-sakura`，以代表路径组合覆盖，不复制四套完整路由矩阵；
- 语言：`zh / en` 按页面族交叉覆盖；
- 可访问性：keyboard focus、实底文字对比、状态双通道、无横向溢出；
- reduced-motion：浏览器模拟 `reduce`，复核 Modal / BottomSheet、Toast / 状态反馈、加载表面与页面交互不再持续或大幅运动；
- WebOS：只验证阻断级兼容，不扩展桌面功能；Tauri 不进入矩阵。

当前种子账号本身不持有下架的暗夜 / 樱花权益。T03 若要遵守真实权益链路覆盖两套主题，需要像 F2 验收一样为种子 Admin 建立固定 ID、`AcceptanceTest` 来源的临时 Theme 权益；不创建订单、不修改余额，验收后精确删除权益、活动指针和操作流水并复核数据库完整性。该数据动作必须在 T03 前随服务启动范围一并获得明确授权，不能通过 DOM 或本地存储绕过权益边界。

## 8. F4-R 与 Flutter 后续边界

T03 通过只关闭 F4-R 的 Web 主题退出门禁；F4-R 整体仍需完成定义中的 Flutter 可维护语义映射。随后只审计：

- Dart `ThemeExtension` 的 owner、四主题可承接范围和原生明暗模式；
- 登录、自服务、消息或其他高价值移动路径需要的语义集合；
- 导航、触控、安全区、Android Back 和生命周期等原生边界。

Flutter 不解析 CSS、不像素复制 Web，也不为关闭专题机械追平全部 Web 功能。T01 / T02 / T03 未关闭前，不提前进入 Flutter 代码。
