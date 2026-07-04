# P3-12-D57 Console 深层交互与真实数据态复核记录

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Console 深层弹窗 / 复杂表单 / 真实数据态复核与证据收口
- 前置条件：执行真实 smoke 前，用户已在 `2026-07-04` 明确确认当前前后端已启动；`npm run check:host-runtime -- --details` 确认 Gateway / Api / Auth 健康端点均返回 `200`。
- 设计依据：[Console 治理工作台设计端点](/frontend/console-governance-workbench-design)、[P3-12-D55 Console 响应式后台 UI 差距回拉](/records/p3-12-d55-console-responsive-ui-gap-pullback-2026-07-04)、[P3-12-D56 Console Gateway 成组真实页面复核与证据收口](/records/p3-12-d56-console-gateway-grouped-page-smoke-2026-07-04)

## 覆盖范围

本批继续通过 Gateway `https://localhost:5000/console/` 和种子管理员账户复核 Console 代表页，保留 PC `1920x1080` 与 mobile `390x844` CSS 视图口径：

- `/console/roles`
- `/console/roles/10001/permissions`
- `/console/system-config`
- `/console/moderation`
- `/console/experience`
- `/console/orders`
- `/console/products`
- `/console/documents`

本批重点从 D56 的代表页基础复核下钻到深层交互：

- 角色权限矩阵、权限预览和接口映射长文本。
- 系统设置变更历史弹窗。
- 文档治理版本治理弹窗，以及访问策略按钮的数据态可达性。
- 内容治理手动治理动作区、队列表格和动作按钮组。
- 订单详情弹窗、备注输入区和 footer 按钮组。
- 商品详情弹窗、编辑商品表单、上传按钮组和提交动作区。

## 复核结论

- 8 个代表页在 PC `1920x1080` 与 mobile `390x844` 视图下均未发现全局横向溢出：`documentOverflowX = 0`、`bodyOverflowX = 0`。
- `roles`、`system-config`、`moderation`、`experience`、`orders`、`products`、`documents` 的宽表继续保持 `.ant-table-content { overflow-x: auto }` 局部滚动，不把页面整体撑宽。
- 角色权限页 mobile 下动作区为 `wrap`，内容区单列化，权限矩阵加载 `64` 个权限节点、`318` 个接口映射项，未发现接口路径长文本顶出视口。
- 商品详情和编辑商品两个共享 Modal 在 mobile 下宽约 `351px`，落在 `390px` 视口内；详情 footer 为 `wrap`，编辑表单提交动作区为 `wrap`，上传按钮组命中 `wrap` 样式。
- 订单详情共享 Modal 在 mobile 下宽约 `351px`，备注输入区宽约 `299px`，footer 为 `wrap`，无全局横向溢出。
- 系统设置变更历史 Ant Modal 在 mobile 下宽约 `374px`，历史表格 `326px -> 1150px` 局部横向滚动；PC 下弹窗宽约 `960px`，同样只在弹窗内部滚动。
- 文档版本治理 Ant Modal 在 mobile 下宽约 `374px`，当前种子内置文档版本列表与版本内容文本域可用；PC 下弹窗宽约 `960px`。
- 内容治理手动治理动作区在 mobile 下卡片宽约 `298px`，动作按钮组为 `wrap`，表单网格单列化，两个治理表格分别保持 `264px -> 1580px` 与 `264px -> 1780px` 局部滚动。

## 发现与处理

本批真实复核前已先按代码侧风险点收紧深层按钮组换行，并提交：

- `7047b5fc fix(console): 收紧深层弹窗按钮换行`
- 覆盖 `ProductDetail`、`ProductForm`、`ManualModerationActionSection` 和 `adminForm.css`。
- 保持商品详情、商品编辑、手动治理动作、上传、提交、权限、路由、API、数据库和提交 payload 不变。

真实 Gateway 复核未发现新的 Console UI 问题需要继续修复。

文档治理访问策略弹窗未在本批打开：当前本地种子数据中 `Custom=0`、`Imported=0`，第一页文档均为 `BuiltIn`，访问策略按钮按业务规则隐藏。该项记录为当前数据态未命中，不伪造导入或写入数据。

## 工具限制

- 本批优先接入 Browser 插件；在商品深层页复核时，Browser 插件曾出现深层导航超时和 `domSnapshot` 能力错误。
- 为完成同一 Gateway 真实页面矩阵，后续改用 Playwright CLI 保持登录态并执行 `resize` / 会话内导航 / 快照 / 点击 / 指标采样。
- 上述限制属于浏览器控制工具稳定性问题，不写成页面问题。

## 保持不变

- 不新增或修改业务 API。
- 不新增或修改权限键、路由语义、数据库结构或种子数据。
- 不修改商品、订单、系统设置、文档治理、角色权限或内容治理的提交 payload。
- 不把 Console 改造成独立移动应用；移动画板仍作为响应式后台验收参考。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试或生产部署流程。

## 验证记录

- `npm run check:host-runtime -- --details`：通过，Gateway / Api / Auth 健康端点均返回 `200`。
- Browser 插件：优先接入并读取当前 Gateway Console 页面；因工具会话在深层页能力错误，转为 Playwright CLI 完成同一矩阵。
- Playwright CLI：PC `1920x1080` 与 mobile `390x844` 覆盖 8 个 Console 代表页，未发现全局横向溢出，宽表均由局部滚动承载。
- Playwright CLI：mobile 深层交互覆盖角色权限矩阵、系统设置历史、文档版本治理、内容治理手动动作区、订单详情、商品详情和编辑商品表单。
- Playwright CLI：PC 深层抽样覆盖商品详情 / 编辑、订单详情、系统设置历史和文档版本治理，未发现全局横向溢出。
- `npm run build --workspace=radish.console`：通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `npm run check:repo-hygiene -- Docs/records/p3-12-d57-console-deep-interaction-data-state-review-2026-07-04.md Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md`：通过，已检查 3 个文件。

## 下一步

继续留在 `P3-12-D`。D57 已补 Console 深层交互和真实数据态证据，当前未发现需要继续修复的 Console 真实 UI 问题；下一步应进入 UI 专题候选前集中验收准备，整理 public / private / console 三类证据和剩余限制，仍不提前进入 `P3-12-E`。
