# F4-G-D Docs / Wiki 普通作者协作成组验收记录

> 日期：2026-07-20
>
> 结论：通过，F4-G A-D 批全部完成并关闭。

## 一、验收范围

- Gateway 正式路径下的普通所有者、普通协作者、普通无权用户与 Console 审核者。
- Author 创建、邀请、接受 / 拒绝 / 撤销、保存、并发冲突、提交、撤回和下一份草稿。
- Console 请求修改、驳回、批准应用、独立发布与访问策略调整。
- `zh / en`、PC `1920x1080`、mobile `390x844`、默认 / 国风主题及深链恢复。
- 未审核正文、批准但未发布正文和已驳回后续草稿的公开隔离。

验收使用仓库种子普通用户和种子管理员完成登录，另建两个临时普通账号承担协作者与无权用户角色；本文不记录任何凭据。

## 二、运行态结论

1. 所有者可创建文档并通过 `PublicId` 邀请协作者；待接受邀请只提供响应入口和只读上下文，接受后才获得保存能力。
2. 两个编辑标签页以相同 `ExpectedDraftVersion` 保存时只有一个成功；过期标签页明确显示冲突并保留本地文本，没有静默覆盖或自动合并。
3. 撤销协作者后，下一次深链读取即被服务端拒绝；重新邀请后协作者可明确拒绝，无权普通用户无法读取未发布文档。
4. 所有者可提交、撤回并开启下一份草稿；审核者请求修改时必须填写意见，作者修正后可重新提交。
5. `Apply` 只把草稿原子应用为权威正文和 Revision，不自动发布；单独执行 Publish 后公开路由才展示批准正文。
6. 已发布 v1 之后的新草稿被驳回，公开路由仍保持 v1，未出现驳回正文或审核中正文污染。
7. Console mobile 和公开 Docs mobile 布局可操作；中英文与主题切换未改变权限和状态语义。
8. WebOS 继续复用 F4-G-C 已验证的 Author API 与组件，不存在独立草稿、权限或正文写入口。

## 三、验收发现与根因治理

本批没有增加临时兜底，按共同根因修正四处契约：

1. 新建文档、首份草稿和后续草稿统一使用 Repository 返回的 Snowflake ID，避免业务层继续使用插入前实体默认值。
2. 待接受协作者进入作者列表和详情的 `Invitee` 只读视图，既能响应邀请，又不会在接受前取得编辑权。
3. 终态草稿关闭后，所有者的“开启下一份草稿”能力不再错误依赖当前草稿的 `VoCanEdit`。
4. 通知结构化目标校验补齐 `DocsAuthorDraft`，邀请与审核通知可携带合法 `DocumentId / DraftId` 持久化。

相应服务测试覆盖 Repository 生成 ID、待接受邀请只读访问和 Wiki 通知目标持久化。

## 四、数据清理与完整性

- 已删除验收文档及其草稿、协作者、审核事件、Revision、可靠 Outbox 和相关通知。
- 已删除两个临时普通账号及其身份授权、token 和关联消息数据。
- Main、OpenIddict、Message 中按验收标记和临时账号复核的残留均为 `0`。
- `Radish.db`、`Radish.OpenIddict.db`、`Radish.Message.db`、`Radish.Chat.db`、`Radish.Log.db`、`Radish.Hangfire.db` 的 SQLite integrity check 均为 `ok`。
- `Radish.DbMigrate verify` 通过，`20260720_007_wiki_author_collaboration` 已应用且 OpenIddict 无待执行 migration。

## 五、代码侧门禁

- Wiki Authoring / Repository / Notification 定向测试：`22/22` 通过。
- `radish.client` production build：通过。
- `validate:baseline:quick`：通过；client `449`、Console `57`、`@radish/http` `18`、`@radish/ui` `24` 项测试均通过。
- 仓库敏感字面量、身份、时间语义和 Console 权限契约扫描：通过。
- 最终仓库卫生与 `git diff --check` 在提交前执行。

## 六、关闭与下一顺位

F4-G 完成标准全部满足：普通作者贡献、显式协作、审核应用、独立发布、双版本 CAS、公开隔离和临时数据清理均形成闭环。下一顺位进入 `F4-H-A 功能完成线候选复核与专题裁决`，只读复核 F4-G-A 未选候选及其他既有业务域，一次只选择一个长期价值和权威边界清楚的完整专题；不重启主动生产证据采集。
