# F4-R R1-P02 Public 详情与互动代码事实与设计边界审计

> 日期：2026-08-05（Asia/Shanghai）
>
> 状态：代码事实与结构债已审计；2026-08-06 已确认补全正式 Web 能力边界，恢复 PC 代表设计，不包含运行时代码修改

> 2026-08-06 后续裁决：本记录对 `2026-08-05` Public 路由接入事实的描述仍有效，但“当前未开放”不再直接作为长期产品停止线。[正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)的四项决定已经确认：R1-P02 纳入帖子 / 回帖点赞、reaction 与回帖的回帖；作者删除、投票和抽奖按权限 / 类型状态保留；基础资料设置进入后续 Private Web；资产复杂能力独立后置。能力审计冻结解除，继续 PC 代表设计，未确认前不进入 Mobile 或代码。

## 结论

`R1-P02 / Public 详情与互动` 继续维持 R1，锚点为 `/forum/post/:id`，六维评分保持 `2/2/2/2/2/1 = 11`。它不是 `R1-P01` 内容流的详情版，而是一种新的“长内容阅读 + 多层参与 + 作者状态”页面类型。

本轮确认的目标结构是：

> 以连续的文章阅读面为主轴，把帖子动作收敛为一条统一操作带，把问答、轻回应和评论组织成同一讨论进程；桌面辅助区只保留章节定位等独占价值，移动端按任务顺序重排，不把桌面侧栏堆到正文末尾。

本批只重构信息层级、视觉组合和响应式关系，不改变后端 API、数据模型、权限、路由或现有写入语义。全局灰玉品牌 token 仍归共享主题批，不随详情页扩散。

## 1. 审计范围

### 1.1 代码

- `Frontend/radish.client/src/public/forum/PublicForumDetail.tsx`
- `Frontend/radish.client/src/public/forum/PublicForumApp.module.css`
- `Frontend/radish.client/src/public/forumRouteState.ts`
- `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`
- `Frontend/radish.client/src/apps/forum/components/PostAnswerLifecycleSection.tsx`
- `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.tsx`
- `Frontend/radish.client/src/apps/forum/components/CommentTree.tsx`

### 1.2 权威说明

- [公开 forum 应用结构](/features/forum-public-app)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [帖子轻回应墙](/features/forum-quick-reaction-wall)
- [论坛内容赞赏](/features/forum-content-reward)
- [论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)
- [论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)
- [论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)

## 2. 当前功能事实

### 2.1 路由与回流

公开详情接受 `Post.PublicId` 或旧 long 字符串。受控 intent 当前完整集合为：

```text
comment | quickReply | answer | edit | history | reward | bookmark
```

详情还支持 `commentId`、`answer`、`answerPage` 与 `answerSort` 定位评论或回答现场。intent、定位参数和来源状态不进入 canonical、分享链接、OpenGraph、JSON-LD 或 sitemap；普通点击可由 SPA 保持现场，新开标签和复制链接仍使用真实 `href`。

### 2.2 公开详情能力矩阵

| 对象 | 当前读取 | 当前公开页写入 / 动作 | 明确未开放 |
| --- | --- | --- | --- |
| 帖子正文 | 标题、作者、时间、正文、分类 / 标签、赞评阅、结构化问答 / 投票 / 抽奖信息 | 分享；登录用户显式收藏、Post 赞赏、举报；作者编辑与版本历史 | 帖子点赞、帖子 reaction 写入、删除、投票提交、抽奖执行 |
| 轻回应 | 列表、总数、作者与时间 | 登录用户创建；符合组件权限时举报 | 编辑、删除、独立 reaction |
| 评论 | 根评论与既有子回复、排序、分页、定位、神评 / 沙发、赞赏摘要 | 登录用户创建根评论；评论作者编辑 / 历史；Comment 赞赏与举报 | 在 Public 路由新建子回复、评论点赞、评论 reaction、删除 |
| 问答回答 | 问题状态、已采纳回答、回答分页、目标定位、附件与历史 | 登录回答；回答作者编辑 / 恢复 / 删除；问题作者采纳 / 撤销；举报 | 回答投票、回答赞赏、独立回答评论树 |
| 投票 / 抽奖 | 结构化内容只读展示与类型流跳转 | 无 | Public 详情提交投票、结束投票或执行抽奖 |
| 治理 | 公开内容举报入口 | Post、QuickReply、Comment、PostAnswer 进入既有举报链路 | 在 Public 页面执行审核、处置或申诉裁决 |

共享组件拥有更多可选能力，不代表 Public 详情已经开放。例如 `CommentTree` 定义了回复、点赞、删除和 reaction 回调，但 `PublicForumDetail` 当前没有传入这些写入口；R1-P02 不得因为组件可用就自行扩权。

### 2.3 身份边界

- 匿名用户可以完整阅读公开内容；触发需登录动作时使用受控 return path 回到同一详情现场，不自动提交收藏、赞赏或其他写入。
- 普通登录用户可以按上表参与，最终资格、目标可用性、幂等与冲突仍由服务端权威契约决定。
- 帖子、评论和回答作者只在各自对象上取得既有编辑 / 历史 / 恢复或删除能力。
- 问题作者取得采纳与撤销能力；Console 管理员身份不把治理工作台动作带入 Public 页面。

## 3. 当前状态事实

| 层级 | 当前状态 |
| --- | --- |
| 整页 | `loading / notFound / error / ready` |
| 轻回应 | `loading / error / empty / ready`，失败保持局部隔离 |
| 评论 | 初次加载、分页、局部错误、空态、排序、目标定位、输入中提示 |
| 回答 | 初次加载、分页 / 排序、目标不可用、编辑上传、冲突、历史与恢复 |
| 交易 / 私有动作 | 收藏加载与冲突；赞赏确认、处理中、成功、余额不足、重复或关闭 |
| 作者 / 治理浮层 | 帖子编辑、帖子 / 评论版本历史、举报；PC Dialog 与 mobile Bottom Sheet 语义已有专题基线 |

整页状态继续继承 `R1-F01` 的共享状态槽。交易、版本和举报浮层已有已验收契约，R1-P02 只确认它们与新页面的入口关系，不复制全部状态画板。

## 4. 现有结构问题

### 4.1 操作重复，页面没有单一参与层级

轻回应、评论、回答、编辑和历史同时出现在正文后的工作区、各自功能区和右侧栏；评论还在 reaction 摘要附近再次出现入口。用户需要先理解页面组织方式，才能判断哪个入口是主要动作。

### 4.2 右侧栏以解释为主，而非独占任务

来源返回、参与说明、评论语义、作者模式和阅读指南被拆成多个同权面板。除来源与少量身份上下文外，大部分内容重复正文已有信息，或解释产品内部组织方式。`<= 1120px` 时这些面板移到主内容下方，mobile 又继续纵向堆叠，形成明显的长尾。

### 4.3 卡片套卡片，功能边界替代了阅读节奏

外层详情卡内继续放 reaction strip、workspace action panel、轻回应面板、评论 composer、评论树和多个 notice。每个能力都有自己的边框、圆角和标题，视觉上像功能清单，正文、回答和讨论没有形成连续叙事。

### 4.4 容器职责已越过维护上限

`PublicForumDetail.tsx` 当前为 `2292` 行，超过项目单文件 `1500` 行硬上限；它同时承担路由 intent、head、帖子 / 轻回应 / 评论 / 回答读取、实时评论定位、收藏、赞赏、作者版本、举报和整页渲染。视觉实现前必须按真实领域区块拆分，不能继续在同一组件追加条件分支，也不应为拆分而创建空泛 manager / util 抽象。

## 5. R1-P02 代表设计裁决

### 5.1 完整代表画板

在唯一活动设计源 `radish-web-family-ui-v1.pen` 中只新增：

1. `R1-P02 / 帖子详情 / PC 1440`：普通帖子、登录普通读者，展示完整阅读—参与主轴。
2. `R1-P02 / 帖子详情 / Mobile 390`：使用同一内容与身份，按移动任务顺序独立重排。

不按主题、locale、身份或每种帖子类型复制完整页面。

### 5.2 页面主轴

PC 采用“阅读主轴 + 线程索引”的不等权关系：

1. 轻量来源返回与分享工具；
2. 分类 / 标签、标题、作者 / 时间 / 统计和正文组成连续文章头与正文；
3. 正文后只有一条帖子操作带，按阅读反馈、私有回访、交易、分享 / 举报和作者动作分组；
4. 问题帖紧接正文进入回答区，已采纳回答优先；普通帖不保留空占位；
5. 轻回应作为紧凑的低成本参与带，不与评论区争夺第二个“大应用”表面；
6. 评论 composer、排序 / 状态与评论树构成一个连续讨论区；
7. 桌面从属区只保留正文 / 回答 / 轻回应 / 评论的章节定位与真实数量，不重复参与按钮、不展示内部语义说明，也不形成全高 Dashboard rail。

Mobile 顺序固定为：来源 / 分享 → 标题与元数据 → 正文 → 帖子操作带 → 可用回答区 → 轻回应 → 评论 composer 与评论流。桌面线程索引变为正文后的紧凑内联导航，不追加到页面末尾。

### 5.3 必要关键状态

- 问答状态区：待回答、已采纳、其他回答、回答输入与作者 / 问题作者动作层级；以关键区块表达，不复制第二套完整页面。
- 身份与回流区：匿名触发收藏 / 赞赏 / 回答 / 评论时的登录提示，以及作者动作在同一操作带中的归属。
- 浮层状态区：复用既有 PC Dialog / mobile Bottom Sheet，只用一个紧凑状态板确认赞赏、版本 / 冲突与举报入口关系。
- 加载、空态、错误、未找到和目标不可用继承 `R1-F01` 与对应功能专题，不另建完整画板。

### 5.4 视觉继承卡

```text
继承基准：R1-F01 + R1-P01 / 社区发现
保留语法：现代无衬线、国风暖白、灰玉品牌、墨蓝操作、发丝边框、克制圆角与单层轻阴影
页面差异：长内容阅读主轴、统一帖子操作带、多层讨论、问答条件区和线程定位
mobile 转换：从属线程索引内联化；正文、当前动作和讨论优先，不堆叠桌面辅助栏
事实缺口：无新增 API / 数据模型缺口；只需重组现有能力和组件
```

继承的是视觉语言，不复制 `R1-P01` 的混合信息轨道、焦点事件或洞察区。

## 6. 实现前置与停止线

### 6.1 设计确认后才进入代码

1. 先在活动设计源建立 `2～3` 个低保真 PC 构图，比较正文宽度、操作带和线程索引关系；方向确认后只保留唯一 PC / mobile 正式画板。
2. 代码实现时按领域职责拆分 `PublicForumDetail.tsx`，优先抽出阅读 / 帖子动作、讨论流和线程索引等真实页面区块，保留现有 Answer、QuickReply、Comment、Reward 与 Revision 组件。
3. 更新现有静态契约，并补覆盖关键身份 / intent / 响应式结构的组件级验证；专题准备验收时再启动服务执行 Gateway PC / mobile smoke。

### 6.2 当前停止线

- 不新增或改变后端 API、数据库、权限、路由和 canonical 规则。
- 不开放 Public 评论回复、点赞、reaction、删除，不开放帖子点赞 / reaction 写入、投票提交或抽奖执行。
- 不改历史 `.pen`，不为 R3 路由、主题、语言或等价状态复制完整页面。
- 不把全局灰玉 token、Author、Private、Console 或 Flutter 页面族混入本批。
- 本审计不启动前后端、不执行真实 smoke，也不把旧运行状态当作当前证据。

## 7. 下一门禁

用户确认上述页面主轴、PC / mobile 代表范围和停止线后，下一步才进入 `radish-web-family-ui-v1.pen` 的 R1-P02 低保真构图比较。Pencil 方向确认前不修改运行时代码。
