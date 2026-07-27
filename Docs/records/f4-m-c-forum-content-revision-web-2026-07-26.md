# F4-M-C 论坛内容版本正式 Web 完成记录

> **日期**：2026-07-26（Asia/Shanghai）
>
> **结论**：PC / mobile Pencil 与正式 Web 已完成，F4-M 下一顺位进入 D 批成组运行态验收

## 一、完成范围

### 1. Pencil 设计源

在 `Docs/frontend/design-sources/public-web-unified-experience.pen` 增加四个画板：

- `P04B - Forum Post Versions / Compare`：PC 帖子版本时间线、完整快照对比和恢复动作；
- `P04C - Forum Comment Revision / Restore Confirmation`：PC 评论恢复确认；
- `P11B - Mobile Post Versions Bottom Sheet`：mobile 帖子版本 Bottom Sheet；
- `P11C - Mobile Comment Revision / CAS Conflict`：mobile 评论 CAS 冲突与内容保留。

四个画板均通过 Pencil 布局检查，并复核了公开摘要、受权详情、恢复确认、冲突和移动端纵向比较的信息层级。

### 2. 正式 Web

- 新增帖子 / 评论共用 `ContentRevisionModal`，PC 使用 `Modal`，mobile 使用 `BottomSheet`。
- 匿名和非作者只消费编辑次数、当前版本与最近编辑时间，不读取旧正文。
- 作者与管理员可查看版本时间线、目标 / 当前快照、帖子分类标签和附件数量。
- 恢复使用 `ExpectedContentRevision + ClientSubmissionId`，CAS 冲突保留目标版本并刷新当前快照，不自动覆盖。
- “使用此版本编辑”只回填现有帖子或评论编辑器，不写入 URL、localStorage 或持久化草稿。
- 帖子、根评论和子评论均接入版本入口；恢复成功后重新读取帖子详情与列表权威状态。
- 旧 `PostEditHistory / CommentEditHistory` 只在受权区域按需加载，明确标记为早期只读记录，不提供恢复。

### 3. 隐私与生命周期

- API 失败保留结构化 `ApiResponseError`，页面依据稳定错误码区分 CAS、失权和目标不存在。
- 账号、目标或弹窗生命周期变化时递增请求代次，旧异步回包不得重新写回当前内存。
- 失权或目标不存在时清除新版快照、旧历史正文、恢复确认和幂等意图。
- 正式 Web 与 WebOS 复用既有论坛组件，不新增 WebOS、Console、Flutter 或 Tauri 专属实现。

### 4. 双语与样式

- 版本文案拆入独立 `forumRevision` 中英文资源域，避免继续膨胀 `community.ts`。
- 样式只使用语义 token，覆盖 PC / mobile、长正文滚动、可见焦点与 reduced-motion；未新增硬编码十六进制颜色。

## 二、静态验证

已通过：

- `npm run lint --workspace=radish.client`
- `npm run type-check --workspace=radish.client`
- `npm test --workspace=radish.client`：`465` 项
- `npm run build --workspace=radish.client`
- `npm run lint --workspace=@radish/http`
- `npm run type-check --workspace=@radish/http`
- `npm test --workspace=@radish/http`：`23` 项
- `npm run validate:baseline:quick`
- `git diff --check`

新增静态契约覆盖恢复指纹隔离、结构化错误、PC / mobile 容器、CAS 冲突、请求代次、失权清理、旧历史只读入口、帖子 / 根评论 / 子评论接线与编辑器回填。

## 三、未在本批执行

- 未启动 API、Auth、Gateway、Frontend；
- 未执行 Gateway、浏览器 smoke、多身份或四主题运行态矩阵；
- 未执行临时业务数据构造、清理或六库完整性检查。

这些项目属于 F4-M-D。执行真实浏览器验收前，需要按当前任务重新说明启动命令、端口、运行影响与清理方式并取得授权；验收优先使用浏览器插件。

## 四、下一顺位

进入 F4-M-D，按作者、其他登录用户、匿名、Admin / System、失权和目标删除覆盖帖子、根评论、子评论、旧历史、`LegacyIncomplete`、CAS、幂等、分类标签与附件失效、中英文、PC / mobile、键盘、长正文、Back / Forward 和账号切换，完成临时数据清理、六库完整性与 Main migration 严格 verify 后再关闭专题。
