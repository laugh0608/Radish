# F4-G-C Docs / Wiki Author / Console 正式页面完成记录

> 日期：2026-07-20（Asia/Shanghai）
>
> 结论：F4-G-C 已完成静态实现与代码侧验证，下一顺位进入 F4-G-D Gateway 成组验收。本批未启动服务、未执行浏览器 smoke、未修改生产数据。

## 一、完成范围

- 更新 `private-web-workflows.pen` 的 P18 / P19 / P30，覆盖作者草稿列表、所有者 / 协作者、审核状态、CAS 冲突与移动协作面板。
- 更新 `console-governance-workbench.pen` 的 P11 / P16，覆盖待审队列、证据对照、版本、审核动作、留痕，以及移动端固定阅读顺序。
- `@radish/http` 新增 Wiki 作者、协作者、审核队列、审核动作与 LongId 字符串契约。
- 正式 `/docs/mine|compose|edit|revisions` 改用 Author API；所有登录用户可进入作者工作区，页面只按服务端 `VoCanEdit / VoCanSubmit / VoCanManageCollaborators` 裁决动作。
- 作者页完成创建 / 开启草稿、CAS 保存、提交 / 撤回、PublicId 邀请、邀请响应、移除协作者和审核时间线。
- `Wiki.DraftVersionConflict` 保留本地 Markdown，并提供复制本地内容、下载 Markdown、重新载入服务器版本三个显式恢复动作，不伪装自动合并。
- Console 复用现有 `/documents`，增加 `console.docs.review` 待审队列、正式正文 / 草稿双栏证据、版本与协作身份、RequestChanges / Reject / Apply 和审核历史；Apply 不调用 Publish。
- 作者与 Console 均在账号变化时清空页面级敏感状态，并以 epoch 丢弃旧账号的迟到响应。
- WebOS 既有 Wiki 写入兼容函数改为调用同一 Author API，没有新增 WebOS 专属草稿模型。
- 删除 `WikiController.Create / Update` HTTP 入口，并同步 API 索引、HTTP 示例与静态契约，避免绕过 Draft / Review。

## 二、设计源检查

- `private-web-workflows.pen`：P18 / P19 / P30 均完成结构检查，未发现布局问题。
- `console-governance-workbench.pen`：P11 / P16 均完成结构检查，未发现布局问题。
- 两份设计源均已显式保存到仓库；Console 画板保存后再次执行 layout snapshot 验证。

## 三、验证结果

| 验证 | 结果 |
| --- | --- |
| `@radish/http` tests | `18 / 18` 通过 |
| `@radish/ui` tests | `24 / 24` 通过 |
| `radish.client` tests | `449 / 449` 通过 |
| `radish.console` tests | `57 / 57` 通过 |
| Wiki 后端定向 tests | `47` 通过，`1` 项 PostgreSQL 环境用例按配置跳过 |
| 三端相关 lint | 通过，0 warning |
| 四个前端 workspace type-check | 通过 |
| Client / Console production build | 通过 |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，0 warning / 0 error |
| `npm run validate:baseline:quick` | 通过 |
| `npm run check:repo-hygiene:changed` | 通过 |
| `git diff --check` | 通过 |

验证中曾发现一条 Client 静态测试仍匹配旧 `Create / Update` 作者写法，以及旧 Controller 单测仍调用已移除的 `Create` action；两处均按新 Author / CAS 契约更新后复跑通过。

## 四、未在本批执行

- 未启动 API、Auth、Gateway、Client 或 Console 服务。
- 未执行 Gateway PC / mobile 真实浏览器 smoke。
- 未创建临时所有者、协作者、审核者账号或业务数据。
- 未执行 F4-G-D 的 PostgreSQL 实际并发、失权、Apply 冲突、通知与清理矩阵。

上述项目按专题停止线统一留给 F4-G-D 成组验收，不作为 C 批静态实现通过的缺陷。

## 五、下一步

F4-G-D 使用普通所有者、普通协作者、普通无权用户和 Console 审核者，在 Gateway 正式路径覆盖 `zh / en × PC / mobile`，完成邀请、CAS 保存、审核、Apply、独立 Publish、失权、账号切换、公开不污染、数据库完整性与临时数据清理后关闭 F4-G。
