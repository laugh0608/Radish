# F4-R R1-A01 Author 编辑代表页成组实现记录

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：正式代表设计、代码实现、静态验证与 Gateway PC / mobile 运行态验收完成，R1-A01 关闭

## 1. 本批结论

- 唯一活动设计源 `radish-web-family-ui-v1.pen` 已完成 `R1-A01 / Author 编辑、协作与提交审核` 正式代表设计；当前代码按已确认的 PC / Mobile 结构落地，没有修改历史 `.pen`。
- 正式锚点保持 `/docs/edit/:id`，代表身份固定为“登录普通 Owner + Custom 已发布文档 + 正式 v1 + `Editing` 活跃共享草稿 v2 + 至少一名 Accepted Editor”。
- 标题与 Markdown 正文继续作为编辑主轴；正式 / 草稿版本、保存状态、保存与提交审核进入紧凑任务头，协作者、状态、属性与正文目录进入克制的任务侧栏。
- PC 使用 `1400px` 最大内容宽度、正文主区与 `320px` sticky context rail；Mobile 使用单任务流，正文保持页面主体，完整 context rail 进入共享 Bottom Sheet。
- 复用现有 API、权限、路由、CAS、LongId 与写入边界。普通 Author 没有审核决定、Apply、Publish、访问策略、归档、删除、恢复、回滚或导入导出动作；Editor 权限仍由服务端 `VoCan*` 决定。
- Published 正式文档的公开回看只使用正式 Slug；Submitted、ChangesRequested、冲突和 Pending Invitee 继续保留为后续必要关键状态，不混入默认代表整页。

## 2. 页面结构

### PC 1440

- 正式 Web 壳层下，页面内容宽度为 `min(1400px, 100vw - 32px)`；`1440px` 视口实测左右留白各 `20px`。
- 任务头压缩为面包屑 / 正式回看、标题、保存状态与主动作、编辑状态带四类必要信息，不重复公开阅读或顶部产品导航。
- 工作区使用 `minmax(0, 1fr) 320px` 两栏和 `12px` 间距；实测编辑主区 `1068px`、侧栏 `320px`。
- 编辑面聚合标题字段、Markdown 工具、正文、编辑 / 预览切换和底部字数 / 草稿证据；侧栏按正文目录、文档状态、文档属性、协作者与审核事件顺序组织。

### Mobile 390

- 页面宽度为 `100vw - 20px`；`390px` 视口实测左右留白各 `10px`，无页面级横向溢出。
- 任务头、正文 / 信息切换、标题和 Markdown 编辑器形成连续单列；信息抽屉可滚动访问目录、状态、属性、协作者与边界说明。
- 运行态复核发现任务标题与操作区同排会压缩长标题；已改为标题独占整行，保存状态、保存与提交审核共用下一条紧凑操作行。
- 修正后任务头高度由 `261px` 收敛为 `170px`，标题轨道和操作轨道均为 `344px`，未改变 PC 布局。

## 3. 权限与状态边界

- Owner：可保存、提交审核并管理协作者；默认代表页显示一名已接受编辑者。
- Accepted Editor：可保存同一活跃共享草稿；不得因 Editor 关系获得提交或协作者管理权。
- Pending Invitee：只读、可接受或拒绝邀请；只作为后续关键状态区。
- Submitted：正文只读、Owner 可撤回；审核决定继续进入 Console。
- ChangesRequested：显示审核意见并允许 Owner 继续编辑、重新提交；不得暗示已形成正式 Revision。
- `Wiki.DraftVersionConflict`：保留本地 Markdown 与复制 / 下载 / 重新载入边界；不做自动合并。
- Apply 与 Publish 继续分离；默认 Author 代表页只展示已发布正式 v1 的正式 Slug 回看入口。

## 4. Gateway 运行态验收

- 启动范围：`./start.sh` 选项 `8` 启动 Gateway / API / Auth，`npm run dev --workspace=radish.client` 启动 Client；验收后四个端口均已停止监听。
- 身份与数据：普通 Owner 使用 `TestUser`，Accepted Editor 使用 `Admin` 种子账号；临时 Custom 文档最终形成 Published 正式 v1 与 `Editing` 共享草稿 v2。
- 真实 Author 流程覆盖创建、提交审核、开启下一份草稿、保存到 v2、邀请协作者和接受邀请。因本轮授权未包含 Console dev server，Apply / Publish 验收前置状态按现有 Service 契约和 CAS 字段精确构造，没有扩大启动范围或修改产品契约。
- PC：CSS 视口 `1440 × 1024`。主容器、任务头与工作区宽度均为 `1400px`，正文 / 侧栏比例正确，无横向溢出。
- Mobile：CSS 视口 `390 × 844`。任务头、正文编辑器、正文 / 信息切换和 Bottom Sheet 均可见；信息抽屉可滚动到 Accepted Editor，底部导航不遮挡当前关键区域。
- 代表状态检查：正式 v1、草稿 v2、Owner、Editing、保存、提交审核、目录、属性、Admin 已接受和正式 Slug 链接均存在；Submitted、ChangesRequested 和 Pending Invitee 未混入默认整页。
- 权限检查：普通 Owner 页面中禁止动作的 button / link 计数为 `0`；保存、提交审核与邀请动作各有一个正式入口。

## 5. 数据清理

- Main：清理 `1` 文档、`2` 草稿、`1` 协作者、`2` 审核事件、`1` 修订、`1` Reliable Outbox；关联附件引用与浏览历史为 `0`。
- Message：清理 `1` 通知、`1` 用户投递、`1` inbox group；Admin inbox 恢复到启动前 `Revision 5 / 未读组 2 / 未读事件 2`。
- Log：清理 `6` 条本轮 Author 写操作审计。
- OpenIddict：清理本轮种子登录新增的 `4` 条 Authorization 与 `25` 条 Token；最终恢复到启动前 `113 / 2461`。
- TestUser 活跃草稿、文档 Slug、Outbox、通知、审计和登录会话增量复核均为 `0`；临时数据库备份已删除，原始项目数据库保留。

## 6. 验证结果

- `npm run test --workspace=radish.client`：`504 passed / 0 failed`。
- `npm run lint --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：production build 通过；仅保留既有大 chunk warning。
- Gateway PC `1440 × 1024` 与 Mobile `390 × 844` 真实页面复核通过。
- `git diff --check`、文档检查和 changed-file repo hygiene 在提交前通过。

## 7. 后续顺位

- R1-A01 已关闭，F4-R C-1B 下一顺位切换为 `R1-W01 / Private 消息列表—详情`。
- Pencil 当前被其他项目占用；可用后按既有 readiness 与能力门禁制作 R1-W01 PC 正式代表设计，不重新全量审计，也不提前制作 R1-C01 / R1-C02。
- 全局灰玉品牌 token、共享 MarkdownEditor 主题治理和 `R2-A02` 列表 / 修订差异继续保持独立边界。

关联记录：[R1-A01 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08)、[R1-A01 能力门禁修复](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)。
