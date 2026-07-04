# P3-12-D59 UI 专题候选前验证执行

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：候选前自动化验证、Gateway 真实页面总复核与证据收口
- 范围：覆盖 public、private / author、console 三类正式 Web 页面在 PC `1920x1080` 与 mobile `390x844` CSS 视图下的真实 Gateway 状态

## 当前判断

`P3-12-D59` 继续留在 `P3-12-D`。本批在用户当轮确认前后端仍处于启动状态后，先刷新候选前自动化验证，再通过 Browser 插件覆盖 public、private / author、console 的 PC 与 mobile CSS 视图，并抽查 Console 深层弹窗 / 抽屉 / 权限矩阵 / 治理工作台。

本批未发现新的真实 UI 问题，不需要追加代码修复；同时不把 D59 写成 UI 专题退出结论。下一步应进入 `P3-12-D60`，集中复核 D36-D59 证据、剩余限制和进入 `P3-12-E` 的工程条件。

## 启动与工具前提

- 用户已在本轮明确说明前后端仍处于启动状态。
- Gateway 入口：`https://localhost:5000`
- Browser 插件作为本批真实页面复核主工具。
- Browser viewport 能力只支持 CSS 宽高设置；本批移动端记录为 `390x844` CSS 视图，不写作完整 DPR 设备仿真。
- Console 复核过程中遇到 OIDC consent 刷新，已使用种子管理员账号完成同意后重新执行 Console 矩阵；首次落到 Auth consent 页的 Console 采样不计入通过证据。
- Browser 运行时出现过 `https://ab.chatgpt.com/...` telemetry 超时噪声；该超时来自 Browser 插件侧，不属于 Radish 页面控制台错误。

## 自动化验证

| 命令 | 结果 | 说明 |
| --- | --- | --- |
| `npm run validate:ci -- --report` | 通过 | `radish.client` 节点测试 `295 passed`，Console 权限扫描、repo quality contract 和 identity 扫描通过；无 changed files |
| `npm run validate:baseline` | 通过 | 后端测试 `551 passed`；构建输出中保留既有 XML comment warning，不阻断本批结论 |
| `npm run validate:identity` | 通过 | identity runtime / protocol scan、LongId scan 与 15 个定向后端测试通过 |
| `npm run validate:baseline:host -- --report` | 通过 | 前端类型检查、节点测试、权限扫描、后端 solution build、后端测试和 DbMigrate doctor / verify 通过 |
| `npm run check:host-runtime -- --details --report` | 通过 | Gateway / API / Auth health 均返回 `200` |
| `git diff --check` | 通过 | 未发现空白错误 |
| `npm run check:repo-hygiene:changed` | 通过 | 验证执行前工作区无待检查变更；文档收口后另以显式 hygiene 覆盖 |
| `npm run check:repo-hygiene -- Docs/records/p3-12-d59-ui-candidate-validation-execution-2026-07-04.md Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md` | 通过 | 显式覆盖新增 D59 记录和规划入口 |

## Public Web 复核

### 覆盖页面

- `/discover`
- `/forum`
- 论坛详情：`/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4`
- `/docs`
- Docs 详情：`/docs/adr-0001-branch-and-pr-governance`
- `/shop`
- 商品详情：`/shop/product/100062`
- `/leaderboard`
- 公开主页：`/u/usr_019ef99117377efb85389bfe6d9d55a5`
- `/workbench`

### 结果

- PC `1920x1080`：`10/10` 通过。
- Mobile `390x844` CSS：`10/10` 通过。
- 未发现页面级横向溢出、空白首屏、缺失标题或阻断级控制台错误。
- Public 页面未命中需要记录为表格局部滚动的宽表区域。

## Private / Author 复核

### 覆盖页面

- `/workbench`
- `/me`
- `/me/content`
- `/me/history`
- `/me/attachments`
- `/me/experience`
- `/me/assets`
- `/me/assets/transactions`
- `/shop/orders`
- `/shop/inventory`
- `/notifications`
- `/messages`
- `/circle`
- `/pet`
- `/forum/compose`
- `/docs/mine`
- `/docs/compose`
- `/shop/order/931200000001`
- `/docs/revisions/2072303610636533760`
- `/docs/edit/2072303610636533760`

### 结果

- PC `1920x1080`：`20/20` 通过。
- Mobile `390x844` CSS：`20/20` 通过。
- `/me/attachments` mobile 采样出现 `1px` 全局宽度差异，未命中具体宽元素，按 CSS rounding 记录，不作为真实横向溢出问题。
- `/forum/compose` mobile 采样曾识别到隐藏设置抽屉的 off-canvas 元素，但页面 `body` 无横向溢出，截图未出现可操作区域被遮挡；按隐藏抽屉探针误报记录。
- `/docs/edit/2072303610636533760` 在连续快速导航中曾返回一次 `HTTP 429` 初始化提示，等待后重试通过；按 smoke 频率导致的临时限流记录，不作为 UI 缺陷。

## Console 复核

### 覆盖页面

- `/console/`
- `/console/roles`
- `/console/roles/10001/permissions`
- `/console/system-config`
- `/console/moderation`
- `/console/experience`
- `/console/orders`
- `/console/products`
- `/console/documents`
- `/console/categories`
- `/console/tags`
- `/console/stickers`
- `/console/users`
- `/console/coins`
- `/console/applications`
- `/console/hangfire`

### 列表与工作台结果

- PC `1920x1080`：`16/16` 通过。
- Mobile `390x844` CSS：`16/16` 通过。
- 未发现 Console 全局横向溢出、缺失标题、页头 / 工具条 / 筛选控件破版、治理工作台遮挡或移动端无法触达的问题。
- PC 局部滚动表格出现在角色、系统设置、内容治理、订单、商品、文档治理、分类、标签、贴纸和胡萝卜等页面，均限制在 `.ant-table-content` 等表格容器内。
- Mobile 局部滚动表格出现在 Dashboard、角色、系统设置、内容治理、经验治理、订单、商品、文档治理、分类、标签、贴纸、用户、胡萝卜和应用等页面，均未扩散为页面级横向溢出。
- `/console/hangfire` 仍按受保护外部 iframe 运维入口复核，不扩展为 Radish 内部任务调度平台。

### 深层交互样本

| 样本 | PC `1920x1080` | Mobile `390x844` CSS | 结论 |
| --- | --- | --- | --- |
| 角色创建弹窗 | Ant modal `600px`，无横向溢出 | Ant modal `374px`，左右边距约 `8px` | 可打开、可关闭 |
| 角色权限矩阵 | 无页面级横向溢出 | 无页面级横向溢出 | 权限矩阵移动端保持局部滚动与可读性 |
| 订单详情抽屉 | `radish-modal` 约 `900px`，居中 | `radish-modal` 约 `351px` | 可打开、可关闭 |
| 商品详情抽屉 | `radish-modal` 约 `900px`，居中 | `radish-modal` 约 `351px` | 可打开、可关闭 |
| 商品编辑弹窗 | `radish-modal` 约 `900px`，居中 | `radish-modal` 约 `351px` | 仅打开查看，不保存 |
| 系统设置历史 | Ant modal `960px`，无横向溢出 | Ant modal `374px`，左右边距约 `8px` | 可打开；关闭探针受 Browser selector 状态限制，后续导航恢复 |
| 文档详情弹窗 | Ant modal `860px`，无横向溢出 | Ant modal `374px` | 可打开、可关闭 |
| 文档版本弹窗 | Ant modal `960px`，无横向溢出 | Ant modal `374px` | 可打开、可关闭 |
| 内容治理手动动作区 | `?section=manual` 无页面级横向溢出 | `?section=manual` 无页面级横向溢出 | 手动治理区筛选与表格保持可用 |

## 未执行的高影响动作

本批不执行以下写动作：

- 删除、恢复、批量导入、版本回滚。
- 订单重试、调账、冻结 / 解冻、治理处罚提交。
- 系统设置保存、商品保存、角色权限保存。
- 上传文件、批量上传或修改真实测试数据。

## 剩余限制

- Browser mobile 仅记录 CSS 视图，不记录 DPR 3 设备级仿真。
- Browser 插件 telemetry 超时与部分 hidden selector 等待失败只作为工具限制记录，不计为 Radish UI 错误。
- 文档访问策略仍需要安全的非内置文档数据态才能完整覆盖；本批没有新增测试文档。
- D59 验证证明当前候选前页面矩阵无新增真实 UI 问题，但还需要 D60 汇总 D36-D59 的证据、限制和风险后再判断是否进入 `P3-12-E`。

## 本批不做

- 不进入 `P3-12-E`。
- 不创建 tag、不恢复 PR / 发布流程。
- 不新增页面、路由、后端 API、权限键、数据库结构或保存载荷。
- 不修改 Pencil 设计源。
- 不修改 UI 代码。

## 下一步

进入 `P3-12-D60 UI 专题退出条件复核与 P3-12-E 进入判断`：

- 汇总 D36-D59 的设计源、实现、自动化验证和 Gateway 真实页面证据。
- 将剩余限制拆为发布前必须补齐、可后置评审和工具能力限制三类。
- 若仍无阻断，再准备 `P3-12-E` 的进入条件、发布候选验证范围和 PR / tag / 部署前门禁。

## 验证

- `npm run validate:ci -- --report`
- `npm run validate:baseline`
- `npm run validate:identity`
- `npm run validate:baseline:host -- --report`
- `npm run check:host-runtime -- --details --report`
- `git diff --check`
- `npm run check:repo-hygiene:changed`
- `npm run check:repo-hygiene -- Docs/records/p3-12-d59-ui-candidate-validation-execution-2026-07-04.md Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md`

结果：均通过。
