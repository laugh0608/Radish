# F3 i18n 2026-07-16 日终提交回顾与文档审阅

> 日期：2026-07-16（Asia/Shanghai）
>
> 范围：`d5341095^..762e32ac` 的 2 个当日功能提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- `F3-C8` 已完成本地实现与静态收口，覆盖 client 低频页面、公开承诺、公开页面 head、共享反馈 / 上传组件及其实际消费链路。
- 实际链路复核中发现的附件信任边界、分片一致性、论坛写事务和动态错误参数问题已成组治理，没有新增第二套 HTTP、上传、错误或宿主 i18n 契约。
- 系统词元、状态、动作、错误与格式化参数进入宿主双语资源；运营承诺正文、用户内容、文件名和审计原文继续按来源展示。
- 今天没有数据库迁移、包安装、服务启动、浏览器 smoke、PR、tag、发布或部署操作。

## 今日日终前功能提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `d5341095` | `feat(i18n): 收口 F3-C8 共享反馈与上传契约` | 完成低频页面、公开承诺、共享反馈、Markdown / 上传 labels、locale 展示和实际消费 API 结构化错误，并同步加固 Bootstrap、论坛作者态、Docs / Wiki 导航与附件基础契约。 |
| `762e32ac` | `feat(platform): 收口 F3-C8 上传安全与写入一致性` | 固定附件业务类型、权限、路径、MIME / 文件签名、静态暴露与分片会话边界，统一公开 head 和头像生命周期，并补论坛事务、原子配额、孤立附件引用及对应前后端测试。 |

## 代码边界回顾

### 共享反馈、公开壳层与本地化

- `@radish/ui` 的 Markdown、弹层、裁切和上传由宿主注入 labels / formatter；共享组件不读取 client 或 Console 的语言状态。
- 公开页面由唯一 lifecycle owner 写入并清理 canonical、Open Graph、Twitter 与 JSON-LD；`/legal` 已进入正式入口，浏览器根只保留一个 Toast 容器。
- 结构化错误继续使用 HTTP status、`Code / MessageKey / MessageArguments / TraceId`；动态参数只接受经服务端规范化的短安全标量数组。

### 附件与分片上传

- 普通上传和分片会话共用稳定业务类型、Console 权限、图片类型、路径 containment 与服务端内容判定；SVG 被明确排除，失败产物按生命周期清理。
- 非幂等上传不自动重试，前端取消信号、失效裁切任务隔离和不可撤销合并阶段的交互语义已对齐真实行为。
- 用户上传根目录不再经 `/uploads/**` 静态直出，仅保留可信 `/uploads/DefaultIco/**`；受控读取检查启用 / 删除状态，私有附件只允许上传者或 `System / Admin`，临时 token 继续使用独立下载入口。
- 分片链路补齐精确长度、属主 / 租户、会话串行、原子配额预留、跨租户过期清理和 15 分钟终态结算重放。

### 写入一致性

- 论坛发布 ledger、业务实体与 Reliable Outbox 纳入同一事务；PostgreSQL 唯一冲突使用保存点恢复，提交或成功记账异常按失败关闭处理。
- favicon、Wiki 当前正文和可回滚 revision 引用进入孤立附件检查，避免仍被正式内容引用的文件提前清理。
- 头像绑定由服务端校验属主、租户、业务类型、文件状态与图片类型，并在事务内替换当前关联。

## 最终验证

- `radish.client`：`415` 项测试、type-check、lint、production build 通过。
- `@radish/ui`：`21` 项测试、type-check、lint 通过。
- `radish.console`：`52` 项测试、strict type-check、lint、production build 通过。
- `@radish/http`：`13` 项测试、type-check、lint 通过。
- `Radish.Api.Tests`：`814` 项通过、`12` 项 PostgreSQL 环境用例按配置跳过、`0` 项失败。
- `dotnet build Radish.slnx -c Debug --no-restore`：`0 warning / 0 error`。
- `npm run validate:baseline:quick` 与 `git diff --check` 通过；changed repo hygiene 未发现文本卫生错误，保留 5 个文档超过建议篇幅的提醒。
- client production build 仅保留既有大 chunk 提示；今天未为该历史性能项扩大无关修改。

## 文档审阅与修正

- `Docs/planning/current.md` 已记录 `F3-C8` 完成状态、最终静态基线、并行维护边界与 `F3-C9` 下一顺位。
- `Docs/architecture/i18n.md` 与 `Docs/frontend/i18n-completion-governance.md` 已把长期错误契约更新为可选 `MessageArguments`，明确安全参数边界、宿主翻译签名和前端解析职责。
- `Docs/architecture/specifications.md` 已删除失真的分片 XHR 伪实现，固定 `chunkIndex`、语言、取消、超时和结构化错误要求，并把 BaseService / Repository 细节拆入独立专题，使规范入口回到硬上限以内。
- `Docs/frontend/i18n-completion-governance.md` 已把实施基线推进到 `F3-C8` 两个提交，并明确共享 labels、公开 head、动态参数和 Console 后续职责。
- `Docs/frontend/http-client.md`、兼容入口、错误处理、技术栈、开发说明和快速参考已同步 `translateMessage(key, args?)`、失败响应本地化、client / Console 不同续期模式与认证重放风险；`Docs/guide/console-architecture.md` 不再把 `@radish/ui` 写成 HTTP 客户端，也明确 Sticker 尚待迁移。
- 明日会使用的表情与萝卜专题文档已同步共享附件、LongId 和 `MessageArguments` 契约；Sticker 当前响应体状态、自定义错误解析和 best-effort 缩略图等真实边界已明确列入 `F3-C9`，不冒充已经完成。
- `Docs/features/file-upload-design.md` 已对齐业务类型白名单、服务端内容判定、受控读取、缩略图回退、分片会话、原子配额、历史直链迁移和当前单实例停止线。
- `Docs/guide/configuration.md`、`Docs/guide/gateway.md`、`Radish.Gateway/README.md` 与 `Docs/deployment/guide.md` 已对齐文件限制、唯一可用 Local provider、测试 / 生产外层 TLS、可信静态路径和生产升级检查项。
- `Docs/frontend/public-seo-sharing.md` 与 `Docs/frontend/web-ui-foundation-design.md` 已对齐唯一 head owner、来源身份和公开壳层职责。
- `Docs/guide/forum-content-write-reliability-governance.md` 已对齐事务、保存点、成功记账失败语义和结构化错误参数。
- [F3-C8 静态收口记录](/records/f3-c8-shared-feedback-upload-shell-static-closure-2026-07-16) 集中保留实现边界、已知维护项与静态证据；本记录只补日终提交回顾和下一开发日交接，不重复扩写专题实现细节。

## 遗留与维护边界

- **P2 / 附件持久化**：附件已落库但分片会话完成状态持续回写失败且响应丢失时，仍需 attachment session correlation、唯一约束与可恢复查询；普通上传和分片终态还需 durable quota ledger / outbox。
- **P2 / 业务访问控制**：`Chat / Document / Wiki` 的领域 ACL 与历史数据迁移尚未完成，这些类型当前不得承载敏感文件，不能把统一 `IsPublic = true` 语义当成私有附件治理。
- **P2 / 验证补强**：合并到 `master` 前应补头像事务 AOP 回滚集成验证，盘点生产旧 `/uploads` 直链与可信 favicon 路径，并在取得运行授权后的专题验收中证明 `/uploads/**` 不可访问、`/uploads/DefaultIco/**` 仅暴露可信内置图标。
- **P2 / 认证重放**：client 配置 `@radish/http` Token 刷新后，认证 `401` 会重放原始 `withAuth` 请求且不区分方法；关键非幂等写入需逐域确认幂等 / 去重保护，页面不得叠加自动重试。
- **P3 / 容量演进**：分片临时存储和会话锁当前只支持严格单实例；多实例共享临时存储、分布式锁与重放批次饥饿治理按实际部署需求成组推进。
- **P3 / 前端配置**：client 附件 XHR 已复用统一 token、timeout、语言和翻译器，但 URL 仍直接来自 `getApiBaseUrl()`；后续附件维护批次应收敛到 `getApiClientConfig()`。
- **P3 / 文档治理**：本轮已把开发规范的 BaseService / Repository 细节拆成独立专题，并清理配置与部署指南的重复草案；`specifications.md`、部署指南、Sticker 系统专题、配置指南和 Gateway 指南仍超过建议上限但均低于硬上限，后续按读者目标继续拆分，禁止重新堆入历史流水。
- **P3 / 前端性能**：client production build 的既有大 chunk 提示继续纳入性能维护线，不在 F3-C9 中顺带重构。
- 本批未启动服务或执行浏览器 smoke；受控附件访问、公开 head、PC / mobile 语言切换和 OIDC 往返仍需在专题验收且取得当轮启动授权后复核。

## 明日事项（2026-07-17）

1. 推进 `F3-C9` Console 剩余管理域，先核对 `/roles`、`/roles/:roleId/permissions`、`/categories`、`/tags`、`/stickers`、`/stickers/:groupId/items`、`/experience`、`/coins` 的正式路由、真实消费者、API、权限动作、数据来源与现有专题文档，形成执行矩阵后再改代码。
2. 优先治理角色权限与分类标签的跨页面稳定契约，再按实际依赖进入表情、经验和萝卜管理；系统枚举、状态、动作和校验进入宿主资源，名称、备注、运营内容及审计原文保持来源语义。
3. 复用 `@radish/http`、`ApiResponseError`、共享上传类型、LongId 字符串和 locale formatter，不为 Console 建立平行的 HTTP、错误、上传白名单或格式化实现。
4. 按真实写入和权限链路同步检查后端 HTTP status、`Code / MessageKey / MessageArguments`；优先把 Sticker 当前响应体兼容状态与普通 `Error` 迁入真实 HTTP + `ApiResponseError`，保留既有事务、权限、排序和批量处理语义。
5. 补 Console 资源 parity、稳定枚举、英文 `0 / 1 / 2` 数量、LongId、权限动作、结构化错误和实际消费者测试，并审计高风险写入的认证重放 / 幂等边界；完成 test、type-check、lint、production build、Baseline Quick、仓库卫生与差异复核。
6. `F3-C9` 静态收口后再准备 `F3-D` 代表矩阵验收；服务启动、Gateway 访问、PC / mobile 浏览器复核和 OIDC 往返必须重新取得当轮授权。

## 当前不做

- 不在日终文档批次修改业务代码、接口、数据库、迁移、依赖或运行配置值；`appsettings.json` 只修正存储 provider 注释。
- 不启动服务或执行浏览器 smoke，不创建 PR、tag、发布或部署。
- 不把在线词元编辑混入 `SystemConfig`，不返工已完成的 client 业务域，不借 F3-C9 顺带展开附件 ACL 或多实例架构迁移。
