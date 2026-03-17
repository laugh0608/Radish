# 专题回归索引

> 本页用于回答两个问题：
>
> 1. 改了某个模块后，除了 `validate:baseline` 之外还该跑哪些专题回归？
> 2. 规范化后的 `HttpTest` 脚本名分别对应哪个主题？

## 使用顺序

建议按以下顺序执行：

1. 先跑自动化基线：
   - 日常改动先跑 `npm run validate:baseline:quick`
   - 跨层改动再跑 `npm run validate:baseline`
   - 宿主 / 配置 / `DbMigrate` 相关改动再跑 `npm run validate:baseline:host`
2. 再按改动主题补专题回归：
   - 需要账号、Token、运行中宿主的，优先看本页对应 `HttpTest`
   - 需要人工观察 UI、窗口跳转、深链或实时行为的，再补专题文档中的最小人工验收顺序

## 按改动主题选择回归

| 改动主题 | 什么时候补跑 | `HttpTest` / 脚本入口 | 专题文档 / 人工验收入口 | 说明 |
| --- | --- | --- | --- | --- |
| 认证 / OIDC / 基础烟雾 | 登录、Token、网关入口、基础连通性改动后 | `Radish.Api.AuthFlow.http`、`Radish.Api.Smoke.http`、`Radish.Api.Tenant.http` | [验证基线说明](/guide/validation-baseline) | 所有需要 Bearer Token 的专题都默认先从 `AuthFlow` 取 token |
| 社区关系链 / 内容治理 / 分发流 | 关注、举报、审核、分发流、个人主页关系链改动后 | `Radish.Api.Community.http` | [社区主线验收清单](/features/community-m12-p0-acceptance)、[论坛应用功能说明](/features/forum-features) | 当前 `Community` 脚本承接原 `Forum.http` 中的关系链与治理段 |
| 论坛核心主链 / 分类标签 / 帖子编辑历史 | 分类、标签、发帖、帖子列表 / 详情、帖子编辑历史改动后 | `Radish.Api.Forum.Core.http` | [论坛应用功能说明](/features/forum-features)、[论坛编辑历史（专题）](/features/forum-edit-history) | 论坛主链基础能力统一看 `Forum.Core` |
| 论坛评论 / 回复 / 评论编辑历史 | 评论树、回复、评论编辑、评论历史改动后 | `Radish.Api.Forum.Comment.http` | [论坛编辑历史（专题）](/features/forum-edit-history) | 评论链路与帖子主链已拆分，避免混在同一文件里 |
| 论坛投票 | 投票模型、发帖附带投票、投票提交 / 截止 / 展示改动后 | `Radish.Api.Forum.Poll.http` | [论坛投票 MVP 设计方案](/features/forum-poll-mvp) | 需要补列表摘要、详情展示与重复投票拦截时优先跑 |
| 论坛问答 | 问答帖发布、回答提交、采纳、问答视图 / 排序改动后 | `Radish.Api.Forum.Question.http` | [论坛问答 MVP 设计方案](/features/forum-qa-mvp) | `P4-ext` 首轮回归继续复用该脚本 |
| 论坛抽奖 / 浏览记录 | 抽奖参与、开奖、中奖通知、浏览记录写入 / 回看改动后 | `Radish.Api.Forum.Lottery.http`、`Radish.Api.User.Profile.http` | [论坛抽奖 MVP 设计方案](/features/forum-lottery-mvp) | 当前最完整的“脚本 + 最小人工验收顺序”样例 |
| 聊天室 REST 主链 | 频道列表、历史分页、发送、撤回、在线成员、未读同步改动后 | `Radish.Api.Chat.http` | [聊天室 App 文档总览](/features/chat-app-index)、[聊天室 App 实施路线图](/features/chat-app-roadmap) | 实时事件仍需结合运行中的 ChatHub 做人工观察 |
| 文档应用 / Wiki | 文档树、详情、创建、更新、导入导出、发布、归档改动后 | `Radish.Api.Wiki.http` | [文档系统](/guide/document-system) | 文档应用已有一轮业务侧人工验收，当前偏稳定维护 |
| 表情包 / Sticker | 表情包分组、条目 CRUD、批量上传、记录使用改动后 | `Radish.Api.Sticker.http` | [表情包系统设计方案](/features/emoji-sticker-system) | 贴图上传依赖附件能力时应连同附件脚本一起跑 |
| 附件上传 / 访问令牌 / 分片 | 上传、删除、临时令牌、分片、护栏校验改动后 | `Radish.Api.Attachment.Upload.http`、`Radish.Api.Attachment.Manage.http`、`Radish.Api.Attachment.Guardrail.http`、`Radish.Api.Attachment.Chunk.http`、`Radish.Api.Attachment.Token.http`、`test-attachment-upload.ps1`、`test-attachment-upload.sh` | [文件上传设计](/features/file-upload-design) | 附件链当前是最依赖测试素材与本地宿主的专题之一 |
| 限流 | 限流策略、边界返回、策略映射改动后 | `Radish.Api.RateLimit.Core.http`、`Radish.Api.RateLimit.Policy.http`、`Radish.Api.RateLimit.Edge.http`、`test-rate-limit.ps1`、`test-rate-limit.sh` | [文件上传设计](/features/file-upload-design) | 附件 / 高并发接口改动后经常要顺手回归这一层 |
| 其他专项 | 萝卜币、经验值、神评、多租户等专项改动后 | `Radish.Api.Coin.http`、`Radish.Api.Experience.http`、`Radish.Api.CommentHighlight.http`、`Radish.Api.Tenant.http` | 对应专题文档 | 当前更多是专题性校验，不纳入默认日常链 |

## 当前规范化脚本名

### 论坛

- `Radish.Api.Community.http`
- `Radish.Api.Forum.Core.http`
- `Radish.Api.Forum.Comment.http`
- `Radish.Api.Forum.Poll.http`
- `Radish.Api.Forum.Question.http`
- `Radish.Api.Forum.Lottery.http`

### 附件

- `Radish.Api.Attachment.Upload.http`
- `Radish.Api.Attachment.Manage.http`
- `Radish.Api.Attachment.Guardrail.http`
- `Radish.Api.Attachment.Chunk.http`
- `Radish.Api.Attachment.Token.http`

### 限流

- `Radish.Api.RateLimit.Core.http`
- `Radish.Api.RateLimit.Policy.http`
- `Radish.Api.RateLimit.Edge.http`

## 历史名称对照

以下旧名称在规范化后已不再作为当前入口使用：

- 历史 `Radish.Api.Forum.http`
  - 当前按主题拆为 `Community / Forum.Core / Forum.Comment / Forum.Poll / Forum.Question / Forum.Lottery`
- 历史 `Radish.Api.Attachment.http`
  - 当前按主题拆为 `Attachment.Upload / Manage / Guardrail / Chunk / Token`
- 历史 `Radish.Api.RateLimit.http`
  - 当前按主题拆为 `RateLimit.Core / Policy / Edge`

如果某份专题文档仍引用旧名称，以本页和 `Radish.Api.Tests/HttpTest/README.md` 为准，并在下一次触达对应专题时顺手修正文档。
