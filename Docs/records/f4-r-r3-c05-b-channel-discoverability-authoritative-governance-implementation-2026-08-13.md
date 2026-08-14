# F4-R R3-C05-B Channel Discoverability 权威公开资格治理实现

> 日期：2026-08-13（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当前阶段约束未启动服务、浏览器或运行态验收
>
> 范围：频道公开资格权威查询、历史真实分页、CAS 冲突草稿、成功响应快照、PC / Mobile 频道与事件承载

## 本批结论

R3-C05-B 已关闭筛选和分页不能由 URL 回访、列表与历史请求互相覆盖、历史固定截断最近 `20` 条、CAS 冲突后目标可能退出当前筛选而无法精确刷新，以及写入成功后先依赖列表刷新才更新页面等问题。Channel Discoverability 继续只治理现有匿名摘要资格，不修改 Public Discover 输出规则、频道消息授权、`DiscoverVisibility` 枚举、数据库字段或权限键；既有代表继承足以表达本批结构，未修改 Pencil。

## URL、读取与历史权威性

- `page`、`pageSize`、`keyword`、`visibility`、`lifecycle` 与 `includeDeleted` 进入 URL，筛选草稿和已应用查询分离；刷新、前进后退和分享链接可恢复同一查询。
- 列表与历史各自维护请求代际、查询快照和 `loading / ready / unavailable / stale`；过期响应不能覆盖当前结果，已有快照刷新失败继续保留可见证据并冻结非权威写入。
- `GetHistory` 改为 `PageModel<ChannelDiscoverVisibilityEventVo>`，Repository 在当前租户目标内按 `ResultVersion DESC, Id DESC` 稳定排序并执行真实服务端分页，不再固定截断最近 `20` 条。
- 新增 `GetById` 精确读取当前租户频道权威快照，并复用 `console.channel-discoverability.view`；它只服务于冲突目标刷新，没有新增权限键或扩大治理范围。

## 写入冲突与响应快照

- 开启 / 关闭匿名摘要资格继续携带 `ExpectedVersion` 和必填理由；确认窗口绑定明确频道、当前状态与版本，并具备 dirty / busy 关闭停止线。
- CAS 冲突后保留原理由草稿，通过 `GetById` 精确刷新目标，即使目标已退出当前筛选仍可展示最新状态并要求操作者重新确认；精确刷新失败时进入 stale，不允许基于未知快照继续提交。
- 写入成功先消费接口返回的权威频道快照，再异步刷新列表；如果新状态退出当前筛选，当前页立即移除该项并校正数量，避免成功结果依赖后续读取才能成立。
- 前端只根据结构化错误码输出本地化诊断，不直接暴露服务端原始错误文本。

## PC / Mobile 承载

- PC 使用连续频道表格、显式单目标确认和分页事件时间线；筛选、读取状态与动作资格在同一权威快照上裁决。
- Mobile 使用同一列表快照的频道卡、按需筛选层、单一显隐确认和事件卡，不把 PC 表格或宽时间线压缩到手机宽度。
- 历史目标由操作者显式选择；页面不自动把第一条记录当作权威治理目标。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| Channel Repository / Service / Authorization 定向回归 | `46 passed / 1 skipped`；跳过项仅为当前环境未提供 PostgreSQL migration 连接 |
| `npm run test --workspace=radish.console` | `116 / 116` 通过 |
| `npm run type-check --workspace=radish.console` | 普通与 strict type-check 通过 |
| `npm run lint --workspace=radish.console` | 通过，`0 warning` |
| `npm run build --workspace=radish.console` | production build 通过 |
| `npm run check:console-permissions` | 通过；只保留既有 `console.hangfire.replay` 未引用警告 |
| `git diff --check`、changed hygiene | 通过 |

## 停止线与下一步

- 未新增数据库、migration、权限键、治理枚举、公共发现规则、频道消息授权或跨领域状态层。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC / Mobile、双语、代表主题和角色权限运行态复核继续留到 R3-C05 四批代码与静态门禁成组关闭后申请授权执行。
- 未修改 Pencil；既有 `R1-C01 + R1-C02` 队列—证据—动作—事件继承继续成立。
- 下一顺位进入 `R3-C05-C Documents 文档状态与审核治理`。该批会新增独立 `GovernanceVersion`、append-only `WikiDocumentGovernanceEvent`、migration 与写入接口契约，必须先反查当前事实、给出精确方案并再次取得确认。
