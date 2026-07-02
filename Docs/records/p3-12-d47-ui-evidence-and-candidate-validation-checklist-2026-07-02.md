# P3-12-D47 UI 实现证据收口与候选前验证清单

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：P3-12-D UI 专题内的实现证据收口与候选前验证清单准备
- 范围：D36-D46 的设计源矩阵、代码实现、真实页面复核、真实数据态复核、工具限制和候选前验证入口

## 当前判断

`P3-12-D47` 继续留在 `P3-12-D`。本批不进入 `P3-12-E`，也不创建发布材料；目标是把“按照 Pencil 设计稿对整个 app 的 UI 进行实现”的证据、限制和下一轮验证入口收紧，避免发布候选前再依赖零散记忆。

## 输入依据

- [P3-12-D36 UI 专题差距与退出标准整理记录](/records/p3-12-d36-ui-topic-gap-and-exit-criteria-2026-06-30)
- [P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)
- [P3-12-D38 UI 边界裁决与阶段验收清单](/records/p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01)
- [P3-12-D39 Gateway PC / Mobile 阶段验收记录](/records/p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01)
- [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [P3-12-D42 Docs 作者态真实动作收口记录](/records/p3-12-d42-docs-author-action-closure-2026-07-02)
- [P3-12-D43 Public / Private 主路径真实数据态收口记录](/records/p3-12-d43-public-private-data-state-closure-2026-07-02)
- [P3-12-D44 Console 深层管理动作复核记录](/records/p3-12-d44-console-deep-action-review-2026-07-02)
- [P3-12-D45 移动响应式抽样记录](/records/p3-12-d45-mobile-responsive-sampling-2026-07-02)
- [P3-12-D46 Pencil UI 实现完成度复核与剩余实现清单](/records/p3-12-d46-pencil-ui-implementation-completion-review-2026-07-02)
- [验证基线说明](/guide/validation-baseline)
- [页面真实联调与浏览器 Smoke 规则](/guide/browser-smoke)

## 实现证据收口

| 证据面 | 当前证据 | 候选前处理 |
| --- | --- | --- |
| 设计源到页面族矩阵 | D37 已覆盖 public / private / foundation / console 四类 Pencil 设计源，逐项标注真实路由、代码状态和剩余动作 | D38 后置项不回流；候选前只复核当前范围页面族 |
| 范围裁决 | D38 已确认公开聊天室、独立公开首页、Console 内部调度、内部 Jobs 和独立移动 Console 后置 | 若未来推进，先补产品方案、数据来源、权限动作、API 契约和 Pencil / 说明文档 |
| 源码级页面状态 | D41 已确认 public / private / author / console 主要页面族均有真实路由、页面组件和主要数据态 | 候选前按代表路径刷新页面复核，不把后置平台能力写成缺口 |
| Docs 作者态 | D42 已补编辑页 / 修订页公开阅读回跳、内部导航和草稿稳定性 | 候选前可用非内置、未删除安全测试文档复核编辑、保存、版本栈和公开阅读回跳 |
| Public / Private 主路径 | D43 已补论坛详情 intent 真实 `href`，并确认商城私域、背包、我的内容 / 历史 / 附件链接契约 | 候选前复核公开详情、商城回流、订单 / 背包、内容 / 历史分页和移动任务流 |
| Console 深层动作 | D44 已收口商品、订单、角色权限、文档治理、系统设置和内容治理的深层权限态与 handler 复核 | 候选前优先做只读 / 安全写动作样本，不执行破坏性批量操作 |
| 移动响应式 | D45 已覆盖 public / private / author / console 移动 CSS 视口抽样，并修复系统设置品牌卡片横向溢出 | 若要求完整移动高分屏结论，需补 `390x844 @ DPR 3` 或等价真实设备 / 工具能力 |

## 候选前验证清单

### 启动前自动化

候选前的代码侧验证建议按以下顺序执行：

1. `npm run validate:ci -- --report`
2. `npm run validate:baseline`
3. `npm run validate:identity`
4. `npm run validate:baseline:host -- --report`

说明：

- `validate:ci` 用于本地复现当前 Repo Quality 最小执行面并输出报告。
- `validate:baseline` 用于覆盖前端 type-check、前端测试、Console 权限扫描、后端 build / test 等默认基线。
- `validate:identity` 用于刷新身份语义、Claim 读取、协议输出和 LongId 字符串安全扫描。
- `validate:baseline:host` 用于宿主启动前自检和 DbMigrate 只读校验。

### 启动后运行态

真实运行态验证只在用户明确说明前后端已经启动后执行，不沿用历史会话的启动状态。

建议入口：

1. `npm run check:host-runtime -- --details --report`
2. Gateway PC `1920x1080` 页面复核：`https://localhost:5000`
3. Gateway Console PC `1920x1080` 页面复核：`https://localhost:5000/console/`
4. Gateway mobile CSS `390x844` 页面复核；若工具可设置 DPR，再补 `390x844 @ DPR 3`

建议覆盖：

- Public：`/`、`/discover`、论坛列表 / 详情、Docs 列表 / 详情、商城公开浏览、榜单、公开主页、`/workbench`。
- Private / Author：`/me`、内容 / 历史 / 附件 / 经验、资产 / 流水、订单 / 详情 / 背包、通知、消息、圈子、宠物、论坛发帖 / intent、Docs 作者台。
- Console：Dashboard、订单、商品、胡萝卜、用户、角色 / 权限、文档治理、内容治理、经验治理、分类 / 标签 / 贴纸、系统设置、Hangfire 外壳。

### 数据与动作样本

- 登录态默认使用仓库种子管理员账号；如需普通用户视角，再单独记录账号和权限差异。
- 写动作只选择安全测试数据：Docs 可编辑文档、论坛草稿 / 回答、Console 只读详情或可恢复低风险设置。
- 删除、批量下架、批量导入、版本回滚、订单重试等高影响动作默认不在 UI 候选前 smoke 中直接执行；如要执行，先定义安全数据和恢复方式。

## 保留限制

- D39 与 D45 的移动结论主要是 CSS 视口，不等价于完整 DPR 3 物理高分屏验证。
- D39 以页面挂载、布局宽度、代表详情和只读浮层为主；不覆盖所有会改变数据的动作。
- D45 抽样覆盖移动主路径，但不替代发布候选前 PC / mobile 全量代表清单刷新。
- 公开聊天室、独立公开首页、内部调度中心、内部 Jobs 平台和独立移动 Console 继续后置，不作为当前 UI 实现缺口。

## 下一步

下一顺位进入 `P3-12-D48 UI 候选前验证执行准备`：

- 先执行启动前自动化验证。
- 如需要真实 Gateway 页面复核，先告知用户需要启动前后端并等待明确确认。
- 验证完成后再判断是否具备转入 `P3-12-E` 发布候选准备的工程条件。

## 本批不做

- 不进入 `P3-12-E`。
- 不创建 tag、不恢复 PR / 发布流程。
- 不新增页面、路由、后端 API、权限键、数据库结构或保存载荷。
- 不修改 Pencil 设计源。
- 不执行真实 Gateway 页面联调。

## 验证

- `npm run check:repo-hygiene:changed`
- `npm run check:repo-hygiene:staged`
- `git diff --check`
- `git diff --cached --check`

结果：均通过。
