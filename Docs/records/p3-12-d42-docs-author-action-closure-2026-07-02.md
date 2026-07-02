# P3-12-D42 Docs 作者态真实动作收口记录

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：P3-12-D UI 专题内的作者文档页面组动作闭合
- 范围：`radish.client` Docs 作者台内部导航、公开阅读回跳、编辑草稿状态稳定性和静态契约测试

## 输入依据

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)

## 本批结论

`P3-12-D42` 已完成 Docs 作者态代码侧动作补漏：

- `/docs/edit/:id` 编辑页的“修订记录”改为作者台内部路由导航，普通点击不再触发整页重载。
- `/docs/edit/:id` 与 `/docs/revisions/:id` 在文档未删除且存在 slug 时补齐“公开阅读”真实链接，闭合作者态编辑 / 版本回看与公开阅读之间的回跳。
- `/docs/compose` 与 `/docs/edit/:id` 直达时，目录异步加载不再重新触发编辑器初始化，避免用户刚输入的草稿或编辑内容被目录刷新覆盖。
- 静态契约测试同步锁定 Docs 作者入口仍独立于公开 SEO 壳层，且不承载发布、撤回、归档、删除、恢复或回滚治理动作。
- 同步修正 `publicSeoStatic` 中已过期的共享 `WebStateSlot` / `WebShellHeader` 断言，使测试继续检查真实 `href` 与普通点击拦截契约，而不是绑定旧 JSX 形态。

## 未改变范围

- 未新增后端 API、权限键、数据库结构、上传载荷或保存载荷。
- 未开放普通非管理员作者权限模型。
- 未把发布、撤回、归档、恢复、导入、导出或版本回滚放入正式 Web 作者入口。
- 未改 Console 文档治理职责，也未重做 WebOS `WikiApp`。
- 未执行 Gateway PC / mobile 真实页面复核；如需要运行态结论，仍需先等待用户明确说明前后端已启动。

## 验证

- `node --test --test-isolation=none ./Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `node --test --test-isolation=none ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

结果：均通过。

## 下一步

`P3-12-D42` 可转入维护回拉。下一顺位进入 D41 排序中的 `Public / Private 主路径真实数据态复核`：优先覆盖公开论坛类型流 / 详情 intent、作者发帖、商城订单 / 背包、我的内容 / 历史 / 附件等用户主路径。真实 Gateway PC / mobile 复核仍需先等待用户确认前后端已启动。
