# P3-12-D44 Console 深层管理动作复核记录

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：P3-12-D UI 专题内的 Console 深层管理动作代码侧复核
- 范围：商品、订单、用户、角色权限、文档治理、系统设置和内容治理的详情、抽屉、保存、回滚、导入导出、表格操作和权限态

## 输入依据

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [P3-12-D43 Public / Private 主路径真实数据态收口记录](/records/p3-12-d43-public-private-data-state-closure-2026-07-02)

## 本批结论

`P3-12-D44` 已完成 Console 深层管理动作代码侧复核，并收口本批发现的权限态缺口：

- 商品列表和商品详情的编辑入口权限态已统一：`ProductDetail` 的 `onEdit` 只在 `productsEdit` 权限存在时传入，避免详情 footer 暴露超过列表权限的编辑动作。
- 订单列表和订单详情的失败重试权限态已统一：`OrderDetail` 的 `onRetry` 只在 `ordersRetry` 权限存在时传入，避免详情 footer 暴露超过表格操作列的重试动作。
- 角色权限保存动作补充 `saveDisabled` 早返回，和树节点勾选禁用逻辑保持一致，避免保存 handler 脱离按钮禁用态执行。
- 文档治理访问策略、版本回滚、Markdown 导入和导出 handler 已补同层权限 / 状态复核；内置、已删除或无权限文档不会通过深层弹窗继续提交写动作。
- 系统设置 favicon 恢复 / 上传和设置编辑抽屉已补同层权限 / 可编辑状态复核；只读账号或不可编辑设置不会通过 handler 进入写入流程。
- 用户详情、内容治理队列、治理日志和手动治理动作复核未发现新的无条件写入入口；内容治理手动处置区仍只在 `moderationReview` 下渲染，队列 / 日志处置按钮继续受同一权限控制。

## 未改变范围

- 未新增后端 API、权限键、数据库结构、路由接口或保存载荷。
- 未改变文档治理、系统设置、订单重试、商品编辑、内容治理的业务规则，只收紧前端深层动作的权限态传递和 handler 复核。
- 未重新设计 Console 表格布局；D44 只复核当前深层动作与权限态，表格换行 / 滚动布局延续 D33-D35 的代码侧收口结论。
- 未执行 Gateway PC / mobile 真实页面复核；如需要运行态结论，仍需先等待用户明确说明前后端已启动。

## 验证

- `node --test --test-isolation=none ./Frontend/radish.console/tests/consoleTroubleshootingPathContracts.test.ts ./Frontend/radish.console/tests/orderListUrlState.test.ts ./Frontend/radish.console/tests/productListUrlState.test.ts ./Frontend/radish.console/tests/rolePermissionHelpers.test.ts`
- `npm run build --workspace=radish.console`

结果：均通过。

## 下一步

`P3-12-D44` 可转入维护回拉。下一顺位按 D41 排序进入移动响应式抽样：优先复核 public / private / author / console 已落地页面在移动 CSS 视口下的导航、主动作、表格 / 列表、弹窗 / 抽屉和只读权限态。真实 Gateway PC / mobile 复核仍需先等待用户确认前后端已启动。
