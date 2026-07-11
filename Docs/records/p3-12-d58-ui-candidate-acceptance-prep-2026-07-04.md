# P3-12-D58 UI 专题候选前集中验收准备

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：候选前集中验收准备、证据矩阵更新与下一轮验证清单收口
- 范围：汇总 D36-D57 的 public / private / author / console 证据，明确剩余限制和进入 `P3-12-E` 前的验证入口

## 当前判断

`P3-12-D58` 继续留在 `P3-12-D`。D53-D57 已把 D50 后排序出的 Public、Private / Author 和 Console 三类 UI 差距回拉到真实页面证据层面：

- Public Web：D52-D53 已完成移动公开任务流代码侧对齐和 Pencil 到 Gateway mobile 真实页面首轮对齐。
- Private / Author：D51-D54 已完成移动任务流首批对齐、Pencil 抽查、Gateway PC / mobile 真实页面补验和图表 warning 治理。
- Console：D55-D57 已完成响应式后台边界回拉、代表页 Gateway PC / mobile 成组复核、文档治理固定列遮挡修复，以及商品 / 订单详情、商品编辑、系统设置历史、文档版本治理、角色权限矩阵和内容治理手动动作区的深层交互复核。

当前未发现需要继续追加 UI 修复的同类问题。下一步应进入候选前集中验证执行，而不是继续新增零散页面抽样；但在完成最新一轮自动化验证和 Gateway 真实页面总复核前，仍不进入 `P3-12-E`。

## 证据矩阵

| 证据面 | 已有证据 | D58 判断 |
| --- | --- | --- |
| 设计源与范围裁决 | D36-D38 已建立四类 Pencil 设计源、页面族矩阵、阶段验收清单和后置项裁决 | 公开聊天室、内部调度、内部 Jobs、独立移动 Console 不回流到当前候选前验收 |
| Public Web | D52 覆盖移动公开任务流代码侧；D53 通过 Pencil MCP 与 Gateway mobile 修正说明卡前置、论坛详情顺序和商品详情首屏 | 发布前范围内的公开集合页和详情页已有首轮真实页面证据；候选前只需刷新总 smoke |
| Private / Author | D51 收紧移动任务流；D54 通过 `private-web-workflows.pen` 抽查 `P22 / P23`，并覆盖 `/workbench`、`/me` 系列、资产、订单、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态 PC / mobile | 登录态与作者态主路径已有设计源、源码、构建和 Gateway 证据；候选前刷新总 smoke 即可 |
| Console | D55-D57 覆盖页头、指标、工具条、筛选控件、治理工作台、权限矩阵、代表页宽表、深层弹窗和复杂表单 | Console 当前没有新的真实 UI 问题；候选前刷新代表页和深层只读动作即可 |
| 共享基座 | D4 / D7 / D8-D14 已建立 WebShell、状态槽、移动底栏、Console 语义组件和共享 UI 基座 | 不新增壳层变体；候选前只验证现有页面是否仍符合无全局横向溢出和移动任务流口径 |

## 剩余限制

- D54 移动复核为 `390x844` CSS 视口，不写作完整 `@ DPR 3` 设备仿真结论。
- D56-D57 中 Browser 插件在 Console 深层页出现过导航超时和 `domSnapshot` 能力错误；已通过 Playwright CLI 补完同一 Gateway 复核矩阵，但工具稳定性限制应继续记录。
- D57 文档治理访问策略弹窗因当前本地种子数据 `Custom=0`、`Imported=0` 未命中可打开数据态；候选前如要复核访问策略，需要提前准备非内置、未删除、安全可恢复的测试文档。
- D49 之后已有多轮 UI 改动和证据补齐，进入 `P3-12-E` 前必须刷新自动化验证和 Gateway 总复核，不能复用 2026-07-02 的 D48 / D49 结果作为最终门禁。
- 当前仍不执行删除、批量导入、版本回滚、订单重试、调账、系统设置保存等高影响写动作；如要纳入候选前样本，必须先定义安全数据和恢复方式。

## 候选前验证清单

### 启动前自动化

建议下一批按以下顺序刷新：

1. `npm run validate:ci -- --report`
2. `npm run validate:baseline`
3. `npm run validate:identity`
4. `npm run validate:baseline:host -- --report`
5. `git diff --check`
6. 仓库卫生检查：按实际变更选择 `check:repo-hygiene:changed` / `check:repo-hygiene:staged`

### 启动后运行态

真实 Gateway 复核必须在用户当轮明确说明前后端已启动后执行。

建议入口：

1. `npm run check:host-runtime -- --details --report`
2. Gateway PC `1920x1080`
3. Gateway mobile `390x844`；若工具稳定支持，再补 `390x844 @ DPR 3`
4. Console 代表页与深层只读动作样本

建议覆盖：

- Public：`/discover`、`/forum`、论坛详情、`/docs`、Docs 详情、`/shop`、商品详情、`/leaderboard`、公开主页、`/workbench`。
- Private / Author：`/me`、内容 / 历史 / 附件 / 经验、资产 / 流水、订单 / 详情 / 背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态。
- Console：Dashboard、角色 / 权限、系统设置、内容治理、经验治理、订单、商品、文档治理、分类 / 标签 / 贴纸、用户、胡萝卜、应用、Hangfire 外壳。
- 深层样本：商品详情 / 编辑、订单详情、系统设置历史、文档详情 / 版本治理、角色权限矩阵、内容治理手动动作区；访问策略仅在存在安全非内置文档时复核。

## 下一步

进入 `P3-12-D59 UI 专题候选前验证执行`：

- 不需要提前打开 `.pen` 文件；仅当总验证命中新 UI 差距或需要重新对照设计源时，再读取对应 Pencil。
- 执行真实 Gateway smoke 前，需要用户当轮明确说明前后端已经启动。
- D59 完成后再判断是否具备进入 `P3-12-E` 的工程条件。

## 本批不做

- 不进入 `P3-12-E`。
- 不创建 tag、不恢复 PR / 发布流程。
- 不新增页面、路由、后端 API、权限键、数据库结构或保存载荷。
- 不修改 Pencil 设计源。
- 不执行真实 Gateway 页面联调。

## 验证

- `git diff --check`
- `npm run check:repo-hygiene:changed`
- `npm run check:repo-hygiene -- Docs/records/p3-12-d58-ui-candidate-acceptance-prep-2026-07-04.md Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md`

结果：均通过。
