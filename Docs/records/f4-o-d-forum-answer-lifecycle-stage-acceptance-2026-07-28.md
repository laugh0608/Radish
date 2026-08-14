# F4-O-D 论坛回答生命周期成组验收记录

> 日期：2026-07-28（Asia/Shanghai）
>
> 结论：通过；F4-O A-D 批形成完整闭环，专题关闭。

## 验收范围

本批通过 Gateway 对论坛问答执行代表性 PC / mobile 运行态验收，并结合服务端、HTTP、migration 和前端自动化回归覆盖完整生命周期与失败矩阵。

运行态重点覆盖：

- 匿名读者、问题作者兼管理员、回答作者三种代表身份；
- 回答创建、CAS 编辑、历史查看、旧版本恢复为新版本、采纳与撤销；
- 回答 PublicId 路由、服务端回答页、作者动作与问题作者动作；
- `1920 × 1080` PC 与 `390 × 844` mobile 视口；
- 移动导航、单回答区、无水平溢出和浏览器 warning / error；
- 回答创建、采纳和撤销三类通知及 Main Reliable Outbox 的实际生成。

删除、采纳冻结、并发冲突、目标失效、附件归属、治理限制 / 申诉恢复、双向屏蔽抑制、替换采纳和通知定位继续由专属服务端及前端契约测试覆盖；本批没有为重复证明这些边界而改写其他账号或治理数据。

## 验收中修正

### Migration 前置校验兼容

`20260726_013_forum_content_revision` 的回答关联校验曾在 `20260727_015_forum_answer_lifecycle` 应用前加载完整 `PostAnswer` 实体，旧表尚无 `PublicId` 等字段时会导致正式顺序 migration 失败。

本批把关联校验收敛为只查询旧表必然存在的 `Id`，并增加 legacy `PostAnswer` schema 回归。第一次失败发生在写入前；修正后 `015 / 016` 按顺序成功应用，严格 verify 通过。

### 正式 Web 回答区唯一所有者

公开详情已注入新的 `PostAnswerLifecycleSection`，共享 `PostDetail` 下方仍无条件渲染旧回答区，导致 PC / mobile 同时出现两套“解决方案”，并可能展示不同计数。

本批把旧回答区限定为仅在未注入正式生命周期组件时渲染。正式 Web 只保留权威回答区，WebOS 未注入组件的历史兼容路径保持原行为；静态契约测试锁定该所有权边界。

## 验证结果

- 后端全量：`1075` 通过，`33` 个需要外部环境的 PostgreSQL 条件用例跳过，`0` 失败。
- migration 兼容定向：`5` 通过，`2` 个 PostgreSQL 条件用例跳过。
- `radish.client`：`472` 项测试、type-check 和 production build 通过。
- `npm run check:host-runtime -- --details`：Gateway、API、Auth 及前端入口均返回 `200`。
- `20260727_015_forum_answer_lifecycle` 与 `20260728_016_forum_answer_lifecycle_strict` 已应用；`DbMigrate verify` 无 pending。
- 六个 SQLite 数据库 `PRAGMA integrity_check` 均为 `ok`。
- staged / changed repo hygiene、`git diff --check` 通过。

当前机器未配置 PostgreSQL 集成测试环境，因此 PostgreSQL 条件用例保持显式跳过；本批不把 SQLite migration、运行态或严格 verify 表述为 PostgreSQL 实跑结果。

## 清理与关闭

验收结束后已关闭三个独立 Playwright Chrome 会话，后续响应式复核改用单个应用内浏览器标签页；标签页、Gateway、API、Auth、Frontend 与 Console 均已停止或释放。

临时数据已按帖子、回答和通知精确 ID 清理并复核：

- Post、PostQuestion、PostAnswer、3 个 Answer Revision、2 个采纳事件与 Post Revision：`0`；
- PostTag、临时 Tag、提交幂等记录、浏览历史和相关 Reliable Outbox：`0`；
- 三条回答生命周期通知、UserNotification 与 InboxGroup：`0`；
- 发帖产生的 `20` 经验已回退，Admin 经验由 `55` 恢复为验收前 `35`；
- 清理后的 migration verify 与六库完整性检查通过。

F4-O 至此关闭。下一步回到 F4 功能完成线，进入 `F4-P-A` 候选只读审计；先比较现有能力缺口、长期价值与稳定边界，候选裁决前不直接编码。
