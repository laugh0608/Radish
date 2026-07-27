# F4-M-D 论坛内容版本成组验收记录

> 日期：2026-07-26（Asia/Shanghai）
>
> 结论：通过，F4-M 专题关闭

## 验收结论

F4-M 已形成从 Main Revision、普通编辑 CAS、完整快照、旧历史兼容、安全恢复到正式 Web 的闭环。Gateway 运行态覆盖匿名、其他登录用户、作者、Admin / System，公开摘要不暴露旧正文，受权身份可查看完整时间线并执行恢复。

本轮真实执行了帖子与根评论的无变化提交、正常编辑、连续恢复、多次恢复和双标签 CAS。竞争窗口中先提交者成功形成新 Revision，后提交者收到稳定冲突提示，编辑器保持打开且本地标题、正文草稿未丢失。帖子恢复不改变评论、互动或治理状态。

## 共同根因修复

### 版本摘要时间统一

帖子和评论摘要原先读取实体 `ModifyTime`，而 Revision 时间线读取 UTC `CreateTime`，本地 SQLite 下会形成 8 小时偏移。服务端现统一以当前 `ContentRevision` 对应 Revision 的 `CreateTime` 作为最后编辑时间；当前 Revision 缺失时返回稳定 `Incomplete`，不再用另一套时间真相掩盖数据不一致。

### 正式 Web 账号切换入口

正式 `/me` 原先没有退出入口，多身份验收只能依赖历史 WebOS。现在复用统一 `logout`，中英文正式页面均可完成正常账号切换，并补充静态契约。

## 验收矩阵

- 身份：匿名、其他登录用户、作者、Admin、System；权限详情、公开摘要和账号切换符合边界。
- 对象：帖子与根评论完成真实编辑 / 恢复；子评论递归入口、评论编辑时间窗、失权和删除清理由现有前后端定向契约覆盖。
- 写入：无变化不增加 Revision；正常编辑与每次恢复创建连续不可变 Revision；双标签 CAS 阻止陈旧覆盖并保留冲突草稿。
- 兼容与失败：`LegacyIncomplete`、同 key 重放 / 异参、分类标签 / 附件 / 内容规则失效和事务回滚由 Service 与 migration 回归覆盖，不允许部分恢复。
- 页面：`zh / en × PC / mobile`、长正文、Back / Forward、匿名 / 他人摘要、作者详情与 Bottom Sheet 已经 Gateway 复核；运行态代表主题覆盖 `default / guofeng`，四主题、键盘和 reduced-motion 继续由静态契约覆盖。
- 浏览器控制台：两个 CAS 窗口均无 warning / error。

## 验证结果

- `ForumContentRevision` 后端定向：`29` 通过，SQLite / PostgreSQL migration 定向另为 `3` 通过、无跳过。
- 后端全量：`1058` 通过，`31` 个无环境用例按配置跳过；本专题 PostgreSQL 17 用例已在一次性容器单独实跑。
- Client：`466` 项通过，type-check、lint、production build 通过。
- `@radish/http`：`23` 项、type-check、lint 通过。
- `validate:baseline:quick`、changed-only 仓库卫生与 `git diff --check` 通过。
- Main `20260726_013_forum_content_revision` SQLite 严格 verify 通过；六库 `PRAGMA integrity_check` 均为 `ok`。

## 清理与后续

临时帖子、评论、Revision、标签快照、提交记录、Outbox、浏览历史、经验与萝卜币奖励均已精确清理，业务残留为 `0`；用户经验 / 余额、分类和标签计数已恢复到验收前状态。Gateway、API、Auth、Frontend、Console、浏览器会话与一次性 PostgreSQL 容器均已停止或释放。

F4-M 至此关闭。下一顺位进入 `F4-N-A`：对既有[论坛内容赞赏](/features/forum-content-reward)储备方案和当前资产、治理、通知、正式 Web 实现做只读交叉审计，先更新长期边界与停止线，不提前进入代码。
