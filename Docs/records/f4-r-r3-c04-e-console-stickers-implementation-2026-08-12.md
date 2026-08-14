# F4-R R3-C04-E Console Stickers 权威媒体资源治理实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console 贴纸分组、贴纸、独立启停、排序草稿、单图 / 批量上传与附件生命周期

## 本批结论

R3-C04-E 已关闭分组启停权限借完整编辑载荷改写资源的越权路径，并把分组归属、级联删除、排序和附件绑定收敛到服务端权威契约。Console 的分组与贴纸列表统一使用可回访查询、请求代际和同一份 PC / Mobile 快照；排序草稿与权威顺序分离，单图及批量上传明确区分未保存附件。既有 `console.stickers.*` 权限、附件 owner 与孤儿清理协议保持不变；未新增数据库字段、migration、权限键或 Pencil 画板。

## 服务端契约

- `UpdateGroup` 只接受 `console.stickers.edit`；新增窄接口 `UpdateGroupStatus/{id}`，只接受 `console.stickers.toggle`，并仅部分更新 `IsEnabled` 与审计字段。
- 贴纸更新、删除、编码检查、分组读取和排序都会先确认分组存在且属于当前租户，阻断跨租户资源写入与读取。
- 分组级联删除进入显式事务；子贴纸删除或附件引用变更失败时整批回滚，不留下只删除分组或部分子项的状态。
- 批量排序请求必须携带 `GroupId`；服务端拒绝重复 ID、跨分组 ID、缺项和过期快照，实际更新数量不完整时抛错并回滚。`StickerSortSnapshotStale` 作为结构化冲突返回给 Console。
- 单贴纸创建 / 编辑的 `AttachmentId` 固定为必填正 LongId，不再允许前端显示无法由服务端真正兑现的“清空图片”；附件绑定继续由现有引用检查器裁决。
- 权限种子将 `console.stickers.toggle` 只映射到独立启停接口；未改变既有权限 key 或角色授权范围。

## Console 页面事实

- 分组和贴纸关键词进入 URL；刷新、历史前进后退与分组返回保持已应用查询。请求代际忽略过期响应，同查询失败为 `stale`，新查询失败为 `unavailable`，两者都冻结写入。
- PC 保持连续表格；Mobile 使用与 PC 相同快照的媒体卡、轻量分页和筛选 Bottom Sheet，不以横向整表作为主路径。
- 分组完整编辑和独立启停分别调用各自权限与 endpoint；handler、Form、上传和提交均再次检查权限、权威状态与 busy 状态。
- 贴纸排序以服务端返回顺序为权威快照，本地只保存发生变化的草稿。保存失败保留草稿；服务端报告快照过期后冻结继续写入，用户必须刷新，返回、刷新或执行其他写操作前必须显式保存或放弃。
- 单贴纸 Form 与批量上传都具备 dirty / busy 离开保护；已上传但尚未绑定的附件在关闭确认中明确说明会进入既有 24 小时孤儿清理，不伪装为已经自动撤销上传。
- 批量上传在 Mobile 改为上传进度卡和逐项编辑卡，保留上传、重试、校验和最终提交的单任务顺序；PC 继续使用紧凑批量表格。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| Sticker Controller / Service 与附件清理定向测试 | `31 / 31` 通过，覆盖权限拆分、租户边界、排序整批回滚、级联事务与附件引用 |
| `dotnet test Radish.Api.Tests --no-restore` | `1246 passed / 39 skipped`，总计 `1285` |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0 warning / 0 error` |
| `npm run test --workspace=radish.console` | `103 / 103` 通过，含权限、权威快照、排序草稿、附件生命周期与 PC / Mobile 合同 |
| Console type-check、strict type-check、Lint、production build | 全部通过 |
| 权限、LongId、文档与变更卫生 | `check:console-permissions`、`check:long-id-safety`、`check-docs.sh`、changed hygiene 与 `git diff --check` 通过；权限扫描只保留既有 `console.hangfire.replay` 未引用警告 |

`check:time-semantics` 仍只被本批外既有 `Radish.Repository/SystemConfigStorageCoordinator.cs` 三处 `DateTime.Now` 漂移拦截；本批新增审计时间使用 `DateTime.UtcNow`，没有扩大该维护债务。

## 停止线与未执行项

- 未新增拖拽网格、Reaction 运营、通用媒体库、数据库、migration、权限键或 Pencil。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC / Mobile 真实页面、最小权限账号、真实附件上传与排序冲突留到专题成组运行态验收。
- `Docs/features/emoji-sticker-system.md` 仍高于专题文档 `800` 行软建议、低于 `1200` 行硬上限；本批只同步权威契约，不混入无关拆文档工作。
- 下一批进入 `R3-C04-F Coins`，先绑定权威调账目标、显式确认、幂等重放 / 冲突和资产原子性，不扩展转账、退款、提现或万能资产编辑器。
