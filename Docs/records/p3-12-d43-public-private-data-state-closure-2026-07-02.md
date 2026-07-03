# P3-12-D43 Public / Private 主路径真实数据态收口记录

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：P3-12-D UI 专题内的 Public / Private 主路径代码侧数据态与链接契约复核
- 范围：公开论坛类型流 / 详情 intent、作者发帖入口、商城订单 / 背包、我的内容 / 历史 / 附件

## 输入依据

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [P3-12-D42 Docs 作者态真实动作收口记录](/records/p3-12-d42-docs-author-action-closure-2026-07-02)

## 本批结论

`P3-12-D43` 已完成 Public / Private 主路径的代码侧复核与一处链接语义补漏：

- 公开论坛类型流、列表分页、搜索、标签、发帖入口和详情加载态已有真实路由、数据态和公开链接契约。
- 公开论坛详情工作区动作已从纯按钮补为真实 `href`：回答、轻回应、编辑、历史和评论均暴露正式 Web intent return path，普通点击仍通过 `handlePublicForumLinkClick` 进入原有交互，新标签页 / 复制链接保留浏览器默认行为。
- 编辑动作在分类加载中使用 `aria-disabled` 与等待态样式，避免把作者态编辑入口误导为可立即打开，同时不丢失目标 URL。
- 商城私域 `/shop/orders`、`/shop/order/:id` 和 `/shop/inventory` 已确认使用正式 Web 路由、登录回流、订单详情链接、商品来源链接和背包来源链接。
- 个人中心 `/me/content`、`/me/history` 和 `/me/attachments` 已确认保留分页 / 筛选 URL 状态，内容、评论、轻回应和浏览历史条目均提供真实公开 `href`。
- 静态契约测试已补充论坛详情工作区动作必须使用真实 `href` 与统一普通点击拦截，避免后续 UI 调整回退为不可复制的按钮。

## 未改变范围

- 未新增后端 API、权限键、数据库结构、保存载荷或路由接口。
- 未扩展商城订单详情的来源回退模型；例如从背包来源订单进入详情后仍按当前设计返回订单列表，若要保留“返回背包”来源态，需要单独定义路由来源契约。
- 未重做个人中心附件下载、删除、筛选 API 或业务语义。
- 未执行 Gateway PC / mobile 真实页面复核；如需要运行态结论，仍需先等待用户明确说明前后端已启动。

## 验证

- `node --test --test-isolation=none ./Frontend/radish.client/tests/publicSeoStatic.test.ts ./Frontend/radish.client/tests/forumNavigation.test.ts ./Frontend/radish.client/tests/publicRouteState.test.ts ./Frontend/radish.client/tests/meRouteState.test.ts ./Frontend/radish.client/tests/shopRouteState.test.ts`
- `node --test --test-isolation=none ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

结果：均通过。

## 下一步

`P3-12-D43` 可转入维护回拉。下一顺位进入 D41 排序中的 `Console 深层管理动作复核`：优先覆盖商品、订单、用户、角色权限、文档治理、系统设置和内容治理的保存、抽屉、详情、表格换行和权限态。真实 Gateway PC / mobile 复核仍需先等待用户确认前后端已启动。
