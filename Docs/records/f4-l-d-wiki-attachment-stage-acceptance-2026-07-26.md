# F4-L-D Wiki 附件成组验收记录

> 日期：2026-07-26（Asia/Shanghai）
>
> 结论：通过，F4-L Wiki 附件隐私与生命周期权威闭环专题关闭。

## 验收范围

- Owner、Accepted Collaborator、Revoked Collaborator、Restricted Reader、Reviewer 与匿名用户覆盖未绑定上传、Draft、Submitted、Applied、Published、Revision、Archived 和 Deleted。
- 文档访问策略覆盖 Public、Authenticated 与 Restricted；附件覆盖正文图片、封面、普通文件、灯箱和直接资源路由。
- 正式 Gateway 覆盖 `zh / en`、PC 与 `390×844` mobile、多标签、Back / Forward、键盘灯箱、上传失败和失权恢复。
- 数据层覆盖 SQLite apply / doctor / verify、PostgreSQL migration 事务回滚与重入、引用目标集合软删除恢复和并发收敛。

## 关键结论

1. Wiki 新上传保持私有。未绑定附件只有上传者能读取；Pending Invitee、撤销后的协作者、无权登录用户和匿名用户不能借正文、封面或文件入口穿透。
2. Accepted Collaborator 与 Owner 通过同一认证 Blob 契约读取 Draft 正文、封面和文件；撤销协作后，作者深链与附件请求立即拒绝，不沿用旧关系状态。
3. Reviewer 在 Console 审核证据中可以读取 Submitted Draft，Apply 后生成正式 v1；Publish 与访问策略变更保持独立，Authenticated、Restricted 和 Public 的读者矩阵与资源结果一致。
4. Public + Published 匿名正文继续使用稳定受控 URL；下架后匿名页面立即不可访问且附件节点为 `0`。Archived 与 Deleted 不残留公开读取路径。
5. 认证附件使用 object URL，公开附件使用稳定受控 URL；正文图片支持灯箱与 Escape 关闭，Back / Forward、多标签和中英文 PC / mobile 布局均保持可用。
6. 伪装 PDF 因文件签名与 MIME 不一致被拒绝，合法图片与文本文件成功；失败状态不泄露存储路径、鉴权细节或服务端正文。
7. Wiki token 的当前 ACL、未授权不消费、撤销、过期和次数并发继续由 F4-L-B 的服务 / API 自动化矩阵覆盖；运行态没有把 token 作为私有文档分享权限，也没有新增匿名分享入口。

## 验收中修复

### Console 中等宽度固定列遮挡

Console `/documents` 在 `1280px` 视口仍保留右侧详情栏，主表实际宽度只有约 `580px`，小于左右固定列所需宽度。标题固定列覆盖操作列，语义定位可以找到“下架 / 归档 / 删除”，但真实点击会命中上层单元格。

修复后文档治理主表按自身容器宽度判断：低于 `760px` 时解除左右固定列并保留横向滚动；宽表继续维持固定列。相同视口复测中，目标文档的下架、归档和删除均准确触发，未误操作其他行。

### PostgreSQL 测试命名配置

Wiki 附件的两个 PostgreSQL 用例此前因环境未配置而跳过，并额外显式设置 `PgSqlIsAutoToLower=false`，与项目 PostgreSQL 小写物理命名约定冲突，首次实跑在 Code First 索引阶段报 `42703 column "TenantId" does not exist`。

定向测试已移除这项局部特例并复用项目默认命名规则。隔离 PostgreSQL 17 中，migration 回滚后恢复、第二次 Apply、严格 verify、Repository 目标集合、软删除恢复和并发来源写入全部通过。

## 数据库与自动化

- 隔离 PostgreSQL 17：Wiki Attachment migration / Repository 共 `13` 项通过，无跳过。
- Wiki Attachment 后端定向矩阵：`20` 项通过，`2` 项 PostgreSQL 用例在无环境的普通入口按设计跳过；上述两项已在隔离 PostgreSQL 单独实跑通过。
- 后端全量：`1052` 项通过，`30` 项环境型用例按默认配置跳过。
- `radish.console` production build：通过。
- `radish.console`：`61` 项测试、type-check 与 lint 通过；`npm run validate:baseline:quick` 通过。
- 清理后 `Radish.DbMigrate doctor / verify`：通过，Main `main.20260725_012_wiki_attachment_authority` 已应用，OpenIddict pending 为 `0`。
- Main、Log、Message、Chat、OpenIddict、Hangfire 六库 `PRAGMA integrity_check` 均为 `ok`。

## 清理与恢复

- 验收服务、浏览器会话和隔离 PostgreSQL 容器均已停止并清理，Gateway、API、Auth、Frontend 与 Console 端口已释放。
- 临时账号、余额与注册奖励、文档、草稿、Revision、协作者、审核事件、附件引用、浏览历史、通知、Outbox、token、authorization 和审计日志精确残留均为 `0`。
- 三个临时附件文件和迁移前验收备份均已删除，仓库上传目录与本轮 `/tmp` 目录精确残留为 `0`；本轮没有恢复整库备份或覆盖其他本地数据。

## 下一顺位

F4-L 已完成 A-D 批并关闭。下一步进入 `F4-M-A`：只读交叉复核现有明确功能缺口，只选择一个与内容、关系、贡献、治理或复访主轴直接相关、边界可以稳定定义的完整专题；先形成权威设计并获得确认，再进入代码，不并行启动多个专题，也不重启主动生产证据采集。
