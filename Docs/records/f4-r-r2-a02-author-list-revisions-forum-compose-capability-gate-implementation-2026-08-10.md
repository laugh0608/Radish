# F4-R R2-A02 Author 列表、修订与 Forum 发布差异能力门禁实现

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：五组设计前能力门禁已关闭；下一步进入 R2 局部代表设计
>
> 范围：正式 Web `/docs/mine`、`/docs/revisions/:id`、`/forum/compose`，WebOS Forum 发布外壳，`AuthorGetList` 查询契约，账号级 Forum 本地草稿与 Workbench 继续任务来源

## 1. 结论

R2-A02 readiness 中确认的五组阻断已经成组关闭。实现没有新增数据库、migration、权限、业务实体、移动壳层或第二套发布 API；Wiki Author 继续使用既有关系授权与草稿证据，Forum 继续使用既有附件、标签、问答、投票、抽奖和 `clientSubmissionId`。

能力门禁完成后，Docs Mine、Docs Revisions 与 Forum Compose 可以进入唯一活动 `.pen` 的局部代表设计。后续设计只裁决列表 / 版本 / 发布器的结构密度、关键状态和 PC → mobile 转换，不改变本批冻结的接口、权限、LongId、幂等、存储和 WebOS 边界。

## 2. 已关闭门禁

### 2.1 Author 权威列表

- `AuthorGetList` 新增 `scope=all|owned|collaborating`、`draftStage=all|editable|submitted|terminal|none` 与显式分页；未知词元返回结构化 `400 Wiki.AuthorListQueryInvalid`。
- 专属 Wiki Repository 按 tenant、Owner / Pending / Accepted 关系和活跃 / 终态草稿证据完成数据库筛选，使用 `ModifyTime ?? CreateTime DESC, Id DESC` 稳定分页；Service 只映射当前页 VO，不先取全量再过滤。
- `@radish/http` 与 Client 使用显式查询类型。Mine 消费权威 `Page / PageCount / DataCount`，支持范围、草稿阶段、上一页 / 下一页、首次 unavailable、已有页 stale 与重试。
- 列表建立独立请求代际，快速筛选或翻页只接受最新查询；账号变化重置查询和代际。`Owner / Editor / Invitee / Administrator` 继续作为稳定 code，只通过双语资源展示，权限仍只消费 `VoCan*`。
- `DocsAuthorApp.tsx` 将 Mine 列表展示与筛选提取到独立 `DocsMinePage`，主编排文件回到 `1500` 行硬上限内；拆分不复制请求、权限或导航状态。

### 2.2 Revision 独立状态与竞态

- history 与 detail 拆分为 `historyError / detailError`，并分别使用请求代际、当前文档和当前账号三重校验。
- detail 初次失败呈现局部 unavailable 与重试；同一 Revision 已有快照时刷新失败保留快照并标记 stale，不清空历史列表或伪装成“未选择”。
- 切换文档、快速切换 Revision 或账号变化后，旧 history / detail 响应均不能覆盖当前选择。

### 2.3 单一 Forum Composer 与合法承载面

- 原接近硬上限的 `PublishPostModal.tsx` 拆为单一 `ForumPostComposer` 状态 / 视图核心和薄 `PublishPostModal` Bottom Sheet 外壳。
- 正式 `/forum/compose` 直接使用页面态 Composer，不再自动用 Sheet 遮蔽路由上下文；WebOS `ForumApp` 继续使用 Bottom Sheet，但不复制发布、附件、标签、问答、投票、抽奖或幂等状态机。
- 上传或发布期间，Composer 内部关闭、取消和发布动作统一锁定；WebOS 外壳禁用 overlay / Escape 旁路关闭。

### 2.4 账号草稿隔离与 Workbench 共源

- 新增版本化草稿 envelope，存储键按当前 `userId` 分区并再次校验 `ownerUserId`；旧全局 `forum_post_draft` 无法安全归属，因此失败关闭且不自动迁给当前账号。
- 共享 Composer、兼容 `PublishPostForm` 和 Workbench 使用同一 helper；账号变化先清空内存再读取新账号草稿，发布成功只删除当前账号记录。
- Workbench 只把含真实标题、正文、标签或投票 / 抽奖写作内容的草稿视为继续任务，单独分类预设不伪造待办。

### 2.5 双语与单一安全反馈

- 共享发布器的系统文案、验证提示、按钮和无障碍名称全部进入 `community` 中英文资源；用户输入和服务端业务原文保持原样。
- 发布失败只由 Composer 使用结构化 code / messageKey 与本地化 fallback 反馈一次；父级只处理成功导航。分类失败使用固定本地化 unavailable，原始诊断只写统一日志。
- 静态契约禁止共享发布器重新引入固定中文系统文案、原始 `error.message`、第二次失败 toast、全局草稿键或正式页面 Modal 承载。

## 3. 验证

- Wiki Authoring 定向：`49 passed / 1 skipped`；跳过项为本机未配置 PostgreSQL 连接的既有条件测试。
- 后端全量：`1224 passed / 39 skipped`。
- `@radish/http`：`37 / 37`，type-check 通过。
- `radish.client`：`533 / 533`，type-check、Lint 与 production build 通过；build 仅保留既有大 chunk warning。
- 本批没有启动 Gateway、API、Auth、Vite 或浏览器，也没有修改 Pencil；按协作基线，能力门禁先以代码侧验证收口，运行态在代表设计落地后的成组验收执行。

## 4. 停止线与下一步

- 不新增 Wiki 列表端点、Forum 发布端点、服务端 Forum 草稿、移动 App 或 WebOS 新能力。
- 不把发布、撤回、回滚、导入导出或治理动作迁入普通 Author。
- 不用翻译角色文本、前端首包筛选或旧本地草稿猜测权限 / 归属。
- 下一步使用唯一活动设计源完成 Docs Mine、Docs Revisions、Forum Compose 的 R2 局部代表板与必要关键状态；设计确认后再进入正式视觉实现和 Gateway PC / mobile 成组验收。
